"""Your account on every device: each PC signed in to the same Frankenstein account can see what the others have
installed and merged, and a PC on the same Wi-Fi can join another's account with one click on each.

Two ways devices hear from each other, both without any server or key:
- The cloud folder (OneDrive, Google Drive, Dropbox) when it's on: each device writes
  "Frankenstein devices/<account>/<device>.json" there.
- The local network: every Frankenstein says hello on UDP port 47811 every 15 seconds. A device answers what's on
  it only to a device signed in to the same account, and only when it asks.

Joining over the network needs a yes on the device whose account it is: it shows "LAPTOP wants to join your account"
and only then sends its link code back. Or pair with a code: the PC whose account it is shows a code like
"K7QM-3XWD" and sends its account, locked with that code, to the local network every two seconds; the other PC
types the code and unlocks it. Only the PC that types the code has to let messages in through Windows' firewall,
and nothing leaves the local network.

Hellos go to every network this PC is on (each one's broadcast address), not only the default one: a PC with a
VPN, Hyper-V or VirtualBox adapter sends a plain broadcast out of the wrong one, which is how two PCs on the same
Wi-Fi never saw each other."""

import base64
import hashlib
import hmac
import ipaddress
import json
import os
import secrets
import socket
import subprocess
import sys
import threading
import time
import uuid

from . import accounts

PORT = 47811
HELLO_EVERY = 15
MAX_PACKET = 60000


def device_name():
    return accounts.device_name()


# ---------- what's on this device ----------

def inventory(games, merges, version):
    """games: [{name, source, appid}]; merges: [{name, host, extension, packed}]."""
    return {"device": device_name(), "time": time.time(), "version": version,
            "games": [{"name": g.get("name", ""), "source": g.get("source", "")} for g in games][:400],
            "merges": [{k: m.get(k, "") for k in ("name", "host", "extension", "packed")} for m in merges][:200]}


def _fit(inv):
    """The inventory, cut down until it fits one network packet."""
    inv = dict(inv)
    while len(json.dumps(inv).encode("utf-8")) > MAX_PACKET - 500 and inv["games"]:
        inv["games"] = inv["games"][: len(inv["games"]) // 2]
        inv["games_cut"] = True
    return inv


# ---------- through the cloud folder ----------

def _cloud_dir(root, account_uid):
    return os.path.join(root, "Frankenstein devices", account_uid)


def publish_cloud(roots, account_uid, inv):
    for root in roots[:1]:
        try:
            d = _cloud_dir(root, account_uid)
            os.makedirs(d, exist_ok=True)
            tmp = os.path.join(d, device_name() + ".json.tmp")
            with open(tmp, "w", encoding="utf-8") as f:
                json.dump(inv, f)
            os.replace(tmp, os.path.join(d, device_name() + ".json"))
        except OSError:
            pass


def read_cloud(roots, account_uid):
    out = {}
    for root in roots:
        d = _cloud_dir(root, account_uid)
        if not os.path.isdir(d):
            continue
        for name in os.listdir(d):
            if not name.endswith(".json") or name[:-5].lower() == device_name().lower():
                continue
            try:
                with open(os.path.join(d, name), encoding="utf-8") as f:
                    inv = json.load(f)
            except (OSError, ValueError):
                continue
            key = (inv.get("device") or name[:-5]).lower()
            if key not in out or inv.get("time", 0) > out[key].get("time", 0):
                out[key] = dict(inv, via="cloud")
    return out


# ---------- through the local network ----------

def local_ips():
    ips = set()
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ips.add(info[4][0])
    except OSError:
        pass
    try:  # the address of the network the internet goes through (no packet is sent)
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("10.255.255.255", 1))
        ips.add(s.getsockname()[0])
        s.close()
    except OSError:
        pass
    return sorted(ip for ip in ips if not ip.startswith("127.") and not ip.startswith("169.254."))


def broadcast_addrs(ips=None):
    """255.255.255.255 and each local network's own broadcast address (as a /24, what home routers use)."""
    out = ["255.255.255.255"]
    for ip in (local_ips() if ips is None else ips):
        try:
            net = ipaddress.ip_network(ip + "/24", strict=False)
        except ValueError:
            continue
        if str(net.broadcast_address) not in out:
            out.append(str(net.broadcast_address))
    return out


# ---------- pairing with a code ----------

PAIR_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"  # no 0/O or 1/I to mix up
PAIR_LIFETIME = 10 * 60


class PairError(Exception):
    pass


def new_pair_code():
    raw = "".join(secrets.choice(PAIR_ALPHABET) for _ in range(8))
    return raw[:4] + "-" + raw[4:]


def clean_code(code):
    c = "".join(ch for ch in (code or "").upper() if ch.isalnum())
    if len(c) != 8 or any(ch not in PAIR_ALPHABET for ch in c):
        raise PairError("A pairing code is 8 letters and numbers, like K7QM-3XWD.")
    return c


def pair_tag(code):
    return hashlib.sha256(("frk-pair|" + clean_code(code)).encode()).hexdigest()[:24]


def _pair_key(code):
    return hashlib.pbkdf2_hmac("sha256", clean_code(code).encode(), b"frankenstein-pair-v1", 100000, 32)


def _stream(key, nonce, n):
    out = bytearray()
    i = 0
    while len(out) < n:
        out += hashlib.sha256(key + nonce + i.to_bytes(8, "big")).digest()
        i += 1
    return bytes(out[:n])


def seal(code, text, key=None):
    key = key or _pair_key(code)
    nonce = os.urandom(16)
    data = json.dumps({"t": time.time(), "v": text}).encode("utf-8")
    ct = bytes(a ^ b for a, b in zip(data, _stream(key, nonce, len(data))))
    mac = hmac.new(key, nonce + ct, hashlib.sha256).digest()
    return base64.b64encode(nonce + mac + ct).decode("ascii")


def unseal(code, blob, key=None):
    key = key or _pair_key(code)
    try:
        raw = base64.b64decode(blob)
    except (ValueError, TypeError):
        raise PairError("That pairing message is damaged.")
    nonce, mac, ct = raw[:16], raw[16:48], raw[48:]
    if not hmac.compare_digest(mac, hmac.new(key, nonce + ct, hashlib.sha256).digest()):
        raise PairError("That code doesn't match. Check it on the other PC.")
    msg = json.loads(bytes(a ^ b for a, b in zip(ct, _stream(key, nonce, len(ct)))).decode("utf-8"))
    if time.time() - msg.get("t", 0) > PAIR_LIFETIME:
        raise PairError("That code has run out. Make a new one on the other PC.")
    return msg["v"]


# ---------- Windows' firewall ----------

FIREWALL_RULE = "Frankenstein devices"


def firewall_command(exe):
    """The rule that lets other PCs' hellos and pairing in: UDP 47811, this program only, on every network type."""
    return ["advfirewall", "firewall", "add", "rule", "name=" + FIREWALL_RULE, "dir=in", "action=allow",
            "protocol=UDP", "localport=%d" % PORT, "profile=any", "program=" + exe]


def firewall_allowed():
    if os.name != "nt":
        return True
    try:
        out = subprocess.run(["netsh", "advfirewall", "firewall", "show", "rule", "name=" + FIREWALL_RULE],
                             capture_output=True, text=True, timeout=10, creationflags=0x08000000).stdout
        return FIREWALL_RULE in out
    except (OSError, subprocess.SubprocessError):
        return False


def allow_through_firewall():
    """Asks Windows (one "Yes" on its admin prompt) to add the rule. Returns False if that couldn't be asked."""
    if os.name != "nt":
        return False
    import ctypes
    args = subprocess.list2cmdline(firewall_command(sys.executable))
    return ctypes.windll.shell32.ShellExecuteW(None, "runas", "netsh", args, None, 0) > 32

class Lan:
    """Hello, inventory and join messages between Frankenstein on the same network (UDP, JSON)."""

    def __init__(self, me):
        """me(): {"accounts": [{"uid", "name"}], "current": uid, "inventory": {...}, "link_code": fn(uid) or None}."""
        self.me = me
        self.peers = {}      # device (lower) -> {"device", "ip", "accounts", "seen"}
        self.inventories = {}  # device (lower) -> inventory
        self.requests = {}   # id -> {"id", "device", "ip", "uid", "time"}: others asking to join one of our accounts
        self.answers = {}    # device (lower) -> {"ok", "code" or "why", "time"}: answers to our own join requests
        self.offer = None    # (tag, sealed account, until): this PC is showing a pairing code
        self.seeking = None  # (code, tag, key): this PC is waiting for the account under a typed code
        self.paired = None   # {"device", "code"}: the account that arrived for it
        self.pair_error = ""
        self.ever_heard = False
        self.sock = None
        self.error = ""
        self._stop = False

    def start(self):
        try:
            s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
            s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            s.setsockopt(socket.SOL_SOCKET, socket.SO_BROADCAST, 1)
            s.bind(("", PORT))
            s.settimeout(1.0)
            self.sock = s
        except OSError as e:
            self.error = "The local network isn't reachable (%s)." % e
            return False
        threading.Thread(target=self._listen, daemon=True).start()
        threading.Thread(target=self._hello_loop, daemon=True).start()
        return True

    def stop(self):
        self._stop = True

    def _send(self, msg, ip):
        if not self.sock:
            return
        try:
            self.sock.sendto(json.dumps(msg).encode("utf-8"), (ip, PORT))
        except OSError:
            pass

    def _broadcast(self, msg):
        for addr in broadcast_addrs():
            self._send(msg, addr)

    def hello(self):
        me = self.me()
        self._broadcast({"t": "hello", "device": device_name(), "accounts": me["accounts"]})

    # pairing: the PC showing the code sends its account, locked with the code, every two seconds
    def start_offer(self, code, link_code):
        self.offer = (pair_tag(code), seal(code, link_code), time.time() + PAIR_LIFETIME)
        if len(json.dumps({"t": "pair", "blob": self.offer[1]})) > MAX_PACKET:
            self.offer = None
            raise PairError("This account is too big to send in one go. Use the link code instead.")
        threading.Thread(target=self._offer_loop, daemon=True).start()

    def stop_offer(self):
        self.offer = None

    def _offer_loop(self):
        while self.offer and time.time() < self.offer[2] and not self._stop:
            self._broadcast({"t": "pair", "device": device_name(), "tag": self.offer[0], "blob": self.offer[1]})
            time.sleep(2)
        self.offer = None

    def seek(self, code):
        self.paired = None
        self.pair_error = ""
        self.seeking = (code, pair_tag(code), _pair_key(code))

    def _hello_loop(self):
        while not self._stop:
            self.hello()
            # ask same-account devices for what's on them
            mine = {a["uid"] for a in self.me()["accounts"]}
            for key, p in list(self.peers.items()):
                if mine & {a["uid"] for a in p["accounts"]}:
                    self._send({"t": "inv?", "device": device_name(), "uids": sorted(mine)}, p["ip"])
            for _ in range(HELLO_EVERY):
                if self._stop:
                    return
                time.sleep(1)

    def _listen(self):
        while not self._stop:
            try:
                data, (ip, _port) = self.sock.recvfrom(65535)
                msg = json.loads(data.decode("utf-8"))
            except socket.timeout:
                continue
            except (OSError, ValueError):
                continue
            try:
                self.handle(msg, ip)
            except Exception:
                pass

    def handle(self, msg, ip):
        t = msg.get("t")
        dev = str(msg.get("device") or "")[:64]
        if not dev or dev.lower() == device_name().lower():
            return
        self.ever_heard = True
        key = dev.lower()
        me = self.me()
        mine = {a["uid"] for a in me["accounts"]}
        if t == "hello":
            accs = [{"uid": str(a.get("uid", ""))[:64], "name": str(a.get("name", ""))[:40]} for a in msg.get("accounts", [])][:12]
            first = key not in self.peers
            self.peers[key] = {"device": dev, "ip": ip, "accounts": accs, "seen": time.time()}
            if first:
                self.hello()  # answer a newcomer straight away, so it sees us without waiting
                if mine & {a["uid"] for a in accs}:
                    self._send({"t": "inv?", "device": device_name(), "uids": sorted(mine)}, ip)
        elif t == "inv?":
            # only to a device signed in to one of the same accounts
            if mine & set(msg.get("uids", [])):
                self._send(dict(_fit(me["inventory"]), t="inv", uids=sorted(mine & set(msg.get("uids", [])))), ip)
        elif t == "inv":
            if mine & set(msg.get("uids", [])):
                inv = {k: msg.get(k) for k in ("device", "time", "version", "games", "merges", "games_cut")}
                self.inventories[key] = dict(inv, via="network", time=time.time())
        elif t == "join?":
            uid = str(msg.get("uid", ""))
            if uid in mine and len(self.requests) < 5 and not any(r["device"].lower() == key for r in self.requests.values()):
                rid = uuid.uuid4().hex[:8]
                self.requests[rid] = {"id": rid, "device": dev, "ip": ip, "uid": uid, "time": time.time(),
                                      "account": next((a["name"] for a in me["accounts"] if a["uid"] == uid), "")}
        elif t == "pair":
            seek = self.seeking
            if seek and msg.get("tag") == seek[1]:
                try:
                    self.paired = {"device": dev, "code": unseal(seek[0], str(msg.get("blob", "")), seek[2])}
                    self.seeking = None
                except PairError as e:
                    self.pair_error = str(e)
        elif t == "join":
            self.answers[key] = {"ok": bool(msg.get("ok")), "code": str(msg.get("code", ""))[:20000],
                                 "why": str(msg.get("why", ""))[:200], "time": time.time()}

    def ask_to_join(self, device, uid):
        p = self.peers.get(device.lower())
        if not p:
            raise KeyError(device)
        self.answers.pop(device.lower(), None)
        self._send({"t": "join?", "device": device_name(), "uid": uid}, p["ip"])

    def answer(self, rid, allow, code=""):
        r = self.requests.pop(rid, None)
        if not r:
            return False
        self._send({"t": "join", "device": device_name(), "ok": bool(allow), "code": code if allow else "",
                    "why": "" if allow else "%s said no." % device_name()}, r["ip"])
        return True

    def live_peers(self, within=60):
        now = time.time()
        return [p for p in self.peers.values() if now - p["seen"] < within]
