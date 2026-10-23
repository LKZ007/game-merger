"""Your account on every device: each PC signed in to the same Frankenstein account can see what the others have
installed and merged, and a PC on the same Wi-Fi can join another's account with one click on each.

Two ways devices hear from each other, both without any server or key:
- The cloud folder (OneDrive, Google Drive, Dropbox) when it's on: each device writes
  "Frankenstein devices/<account>/<device>.json" there.
- The local network: every Frankenstein says hello on UDP port 47811 every 15 seconds. A device answers what's on
  it only to a device signed in to the same account, and only when it asks.

Joining over the network needs a yes on the device whose account it is: it shows "LAPTOP wants to join your account"
and only then sends its link code back."""

import json
import os
import socket
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

class Lan:
    """Hello, inventory and join messages between Frankenstein on the same network (UDP, JSON)."""

    def __init__(self, me):
        """me(): {"accounts": [{"uid", "name"}], "current": uid, "inventory": {...}, "link_code": fn(uid) or None}."""
        self.me = me
        self.peers = {}      # device (lower) -> {"device", "ip", "accounts", "seen"}
        self.inventories = {}  # device (lower) -> inventory
        self.requests = {}   # id -> {"id", "device", "ip", "uid", "time"}: others asking to join one of our accounts
        self.answers = {}    # device (lower) -> {"ok", "code" or "why", "time"}: answers to our own join requests
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

    def hello(self):
        me = self.me()
        self._send({"t": "hello", "device": device_name(), "accounts": me["accounts"]}, "255.255.255.255")

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
