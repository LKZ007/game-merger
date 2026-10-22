"""Play together: a private network between you and your friends, like Hamachi.

Built on ZeroTier One (free). Frankenstein installs it once (one Windows prompt), then:
  * Host: your own PC runs the network, no account or website needed. You get a party code.
  * Join: your friend types the code. The host lets them in with one click.
Everyone in the party then sees each other as if on the same home network (LAN), so games and
mod servers that use LAN or direct IP connections work between you.

ZeroTier's local service API (127.0.0.1:9993) needs its auth token, which only admins can read,
so the one-time setup also copies it somewhere only your account can read."""

import json
import os
import random
import sys
import urllib.error
import urllib.request

from . import paths, permissions

MSI_URL = "https://download.zerotier.com/dist/ZeroTier%20One.msi"
API = "http://127.0.0.1:9993"
SUBNET = "10.147.%d"


class NetworkError(Exception):
    pass


def _token_file():
    return os.path.join(paths.data_dir(), "zerotier-token")


def _program_data():
    return os.path.join(os.environ.get("ProgramData", r"C:\ProgramData"), "ZeroTier", "One")


def installed():
    pf = [os.environ.get(k, "") for k in ("ProgramFiles(x86)", "ProgramFiles")]
    return any(os.path.isfile(os.path.join(p, "ZeroTier", "One", "zerotier-one_x64.exe")) or
               os.path.isfile(os.path.join(p, "ZeroTier", "One", "ZeroTier One.exe")) for p in pf if p) \
        or os.path.isdir(_program_data())


def ready():
    return installed() and os.path.isfile(_token_file())


def setup(download, log=lambda *_: None):
    """Install ZeroTier (if needed) and make its token readable by this account. One UAC prompt.
    `download(url, dest)` fetches the installer."""
    if sys.platform != "win32":
        raise NetworkError("Play together works on Windows only.")
    lines = []
    if not installed():
        msi = os.path.join(paths.sub("downloads"), "ZeroTierOne.msi")
        log("Downloading ZeroTier (the free network that links your PCs) ...")
        download(MSI_URL, msi)
        lines.append('msiexec /i "%s" /qn /norestart' % msi)
    src = os.path.join(_program_data(), "authtoken.secret")
    dest = _token_file()
    # the service writes its token a few seconds after it first starts
    lines += ['for /l %%i in (1,1,30) do (if exist "%s" goto have) & timeout /t 1 >nul' % src,
              ":have",
              'copy /y "%s" "%s" >nul' % (src, dest),
              'icacls "%s" /inheritance:r /grant:r "%s":R >nul' % (dest, permissions.current_user())]
    lines += FIREWALL
    log("Windows will ask once to set it up.")
    if not permissions.run_elevated(lines, name="gamemerger-network") or not os.path.isfile(dest):
        raise NetworkError("Setup didn't finish. Press Set it up again and click Yes when Windows asks.")


# Windows puts the ZeroTier adapter on the "Public" profile, whose firewall blocks games from answering friends.
# Let in anything from party addresses (10.147.x.x), and mark ZeroTier networks private.
FIREWALL = [
    'netsh advfirewall firewall delete rule name="Frankenstein party" >nul 2>&1',
    'netsh advfirewall firewall add rule name="Frankenstein party" dir=in action=allow remoteip=10.147.0.0/16 profile=any >nul',
    'powershell -NoProfile -Command "Get-NetConnectionProfile | Where-Object {$_.InterfaceAlias -like \'ZeroTier*\'} | '
    'Set-NetConnectionProfile -NetworkCategory Private" >nul 2>&1',
]
SHARE_PORT = 47777
# party traffic goes straight to the friend's PC, never through a system proxy
_direct = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def repair():
    """Fix the usual reasons friends can't connect: the firewall, and a stopped ZeroTier service. One prompt."""
    if sys.platform != "win32":
        raise NetworkError("Play together works on Windows only.")
    lines = ['sc config ZeroTierOneService start= auto >nul', 'net start ZeroTierOneService >nul 2>&1'] + FIREWALL
    if not permissions.run_elevated(lines, name="gamemerger-network-fix"):
        raise NetworkError("The fix didn't run. Press Fix connection again and click Yes when Windows asks.")


def find_host(my_ip, timeout=1.5):
    """The host's merge server on the party network: (ip, info) or None. Tries every address in the party."""
    import concurrent.futures
    if not my_ip:
        return None
    base = my_ip.rsplit(".", 1)[0]

    def probe(n):
        ip = "%s.%d" % (base, n)
        if ip == my_ip:
            return None
        try:
            with _direct.open("http://%s:%d/info" % (ip, SHARE_PORT), timeout=timeout) as r:
                return ip, json.load(r)
        except (OSError, ValueError):
            return None
    with concurrent.futures.ThreadPoolExecutor(64) as pool:
        for hit in pool.map(probe, range(1, 255)):
            if hit:
                return hit
    return None


class MergeServer:
    """The host hands its merge (a .gmerge file) to friends in the party, so everyone has the same mods."""

    def __init__(self):
        self.httpd = None
        self.file = None
        self.info = {}

    def start(self, ip, path, info):
        from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
        import threading
        self.stop()
        self.file, self.info = path, info
        me = self

        class H(BaseHTTPRequestHandler):
            def log_message(self, *a):
                pass

            def do_GET(self):
                if self.path == "/info":
                    body = json.dumps(me.info).encode()
                    self.send_response(200)
                    self.send_header("Content-Type", "application/json")
                    self.send_header("Content-Length", str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)
                elif self.path == "/merge" and me.file and os.path.isfile(me.file):
                    self.send_response(200)
                    self.send_header("Content-Type", "application/octet-stream")
                    self.send_header("Content-Length", str(os.path.getsize(me.file)))
                    self.end_headers()
                    with open(me.file, "rb") as f:
                        while True:
                            chunk = f.read(1 << 16)
                            if not chunk:
                                break
                            self.wfile.write(chunk)
                else:
                    self.send_error(404)
        try:
            self.httpd = ThreadingHTTPServer((ip, SHARE_PORT), H)
        except OSError as e:
            raise NetworkError("Couldn't share on the party network (%s). Wait until you're connected." % e)
        threading.Thread(target=self.httpd.serve_forever, daemon=True).start()

    def stop(self):
        if self.httpd:
            self.httpd.shutdown()
            self.httpd.server_close()
            self.httpd = None


def fetch_merge(ip, dest, progress=lambda d, t: None):
    with _direct.open("http://%s:%d/merge" % (ip, SHARE_PORT), timeout=30) as r:
        total, done = int(r.headers.get("Content-Length") or 0), 0
        with open(dest, "wb") as f:
            while True:
                chunk = r.read(1 << 16)
                if not chunk:
                    break
                f.write(chunk)
                done += len(chunk)
                progress(done, total)
    return dest


def _call(method, path, body=None):
    try:
        with open(_token_file(), encoding="ascii") as f:
            token = f.read().strip()
    except OSError:
        raise NetworkError("Play together isn't set up yet.")
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method,
                                 headers={"X-ZT1-Auth": token, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            raw = r.read()
    except urllib.error.HTTPError as e:
        raise NetworkError("The network service said no (%s)." % e.code)
    except OSError:
        raise NetworkError("The network service isn't running. Restart your PC, or press Set it up again.")
    return json.loads(raw) if raw else None


def node_id():
    return _call("GET", "/status")["address"]


def host(name="Frankenstein party"):
    """Create a private network run by this PC and join it. Returns the party code."""
    me = node_id()
    third = random.randint(20, 250)
    cfg = {"name": name, "private": True, "enableBroadcast": True, "v4AssignMode": {"zt": True},
           "ipAssignmentPools": [{"ipRangeStart": (SUBNET % third) + ".1", "ipRangeEnd": (SUBNET % third) + ".254"}],
           "routes": [{"target": (SUBNET % third) + ".0/24"}]}
    net = _call("POST", "/controller/network/%s______" % me, cfg)
    nwid = net["id"]
    _call("POST", "/network/%s" % nwid, {})
    _call("POST", "/controller/network/%s/member/%s" % (nwid, me), {"authorized": True})
    return nwid


def is_host(nwid):
    return nwid[:10] == node_id()


def join(code):
    code = "".join(c for c in code.lower() if c in "0123456789abcdef")
    if len(code) != 16:
        raise NetworkError("A party code is 16 letters and numbers. Check it with your friend.")
    _call("POST", "/network/%s" % code, {})
    return code


def leave(nwid, end=False):
    try:
        _call("DELETE", "/network/%s" % nwid)
    finally:
        if end:
            try:
                _call("DELETE", "/controller/network/%s" % nwid)
            except NetworkError:
                pass


def status(nwid):
    """What the party looks like from this PC."""
    me = node_id()
    mine = next((n for n in _call("GET", "/network") or [] if n.get("id") == nwid), None)
    out = {"code": nwid, "me": me, "host": nwid[:10] == me,
           "connected": bool(mine and mine.get("status") == "OK"),
           "waiting": bool(mine and mine.get("status") == "ACCESS_DENIED"),
           "my_ip": ((mine or {}).get("assignedAddresses") or [""])[0].split("/")[0],
           "members": []}
    if out["host"]:
        for mid in (_call("GET", "/controller/network/%s/member" % nwid) or {}):
            m = _call("GET", "/controller/network/%s/member/%s" % (nwid, mid))
            out["members"].append({"id": mid, "you": mid == me, "authorized": m.get("authorized", False),
                                   "ip": (m.get("ipAssignments") or [""])[0], "name": m.get("name", "")})
    return out


def let_in(nwid, member, name=""):
    _call("POST", "/controller/network/%s/member/%s" % (nwid, member), {"authorized": True, "name": name})


def kick(nwid, member):
    _call("POST", "/controller/network/%s/member/%s" % (nwid, member), {"authorized": False})
