"""Accounts on this PC. Each one has its own merges, saves, achievements and
personal settings; the admin also manages the shared settings (API keys, where copies go, cloud
backup) and what each account is allowed to do.

These are local accounts to keep people's stuff apart (like profiles on a console), not online
accounts. A PIN stops someone else opening your account; it isn't strong security against someone
with full access to the PC."""

import hashlib
import json
import os
import secrets
import time

from . import paths

# Settings each account keeps for itself. Everything else is shared by the whole PC (admin's call).
PERSONAL = {"fun_style", "achievements",
            "counters", "party", "cloud_folder", "steam_on", "steam_id", "steam_error", "gog_on", "fullscreen", "discord_on"}
# What the admin can allow or block per account
PERMISSIONS = {
    "online": "Play together (online parties) and sharing merges",
    "mods": "Making merges and installing or switching mods",
}
DEFAULT_PERMS = {"online": True, "mods": True}


class ProfileError(Exception):
    pass


def _file():
    return os.path.join(paths.data_dir(), "profiles.json")


def _profile_dir(pid):
    return paths.sub(os.path.join("profiles", pid))


def load():
    try:
        with open(_file(), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {"profiles": [], "last": None}


def save(data):
    with open(_file(), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)


def _hash(pin, salt):
    return hashlib.pbkdf2_hmac("sha256", pin.encode("utf-8"), bytes.fromhex(salt), 150_000).hex()


def public(p):
    return {"id": p["id"], "name": p["name"], "admin": p.get("admin", False), "has_pin": bool(p.get("pin")),
            "perms": {k: v for k, v in dict(DEFAULT_PERMS, **p.get("perms", {})).items() if k in PERMISSIONS}, "color": p.get("color", "#8b5cf6")}


def ensure_admin(shared, name):
    """First run with accounts: the existing user becomes the admin and keeps everything they had."""
    data = load()
    if data["profiles"]:
        return data
    pid = "p" + secrets.token_hex(4)
    data["profiles"].append({"id": pid, "name": name or "Player 1", "admin": True, "color": "#8b5cf6"})
    data["last"] = pid
    mine = {k: shared.pop(k) for k in list(shared) if k in PERSONAL}
    save_personal(pid, mine)
    save(data)
    paths.save_settings(shared)
    return data


def find(pid):
    return next((p for p in load()["profiles"] if p["id"] == pid), None)


def check_pin(p, pin):
    if not p.get("pin"):
        return True
    return secrets.compare_digest(_hash(pin or "", p["salt"]), p["pin"])


def add(name, pin="", perms=None, color="#8b5cf6"):
    name = (name or "").strip()[:24]
    if not name:
        raise ProfileError("Give the account a name.")
    data = load()
    if any(p["name"].lower() == name.lower() for p in data["profiles"]):
        raise ProfileError("There's already an account called %s." % name)
    p = {"id": "p" + secrets.token_hex(4), "name": name, "admin": False, "color": color,
         "perms": dict(DEFAULT_PERMS, **(perms or {}))}
    _set_pin(p, pin)
    data["profiles"].append(p)
    save(data)
    return public(p)


def _set_pin(p, pin):
    pin = (pin or "").strip()
    if pin and (not pin.isdigit() or not 4 <= len(pin) <= 8):
        raise ProfileError("A PIN is 4 to 8 numbers.")
    if pin:
        p["salt"] = secrets.token_hex(16)
        p["pin"] = _hash(pin, p["salt"])
    else:
        p.pop("pin", None)
        p.pop("salt", None)


def update(pid, name=None, pin=None, perms=None, color=None):
    data = load()
    p = next((x for x in data["profiles"] if x["id"] == pid), None)
    if not p:
        raise ProfileError("That account doesn't exist.")
    if name is not None and name.strip():
        p["name"] = name.strip()[:24]
    if pin is not None:
        _set_pin(p, pin)
    if perms is not None and not p.get("admin"):
        p["perms"] = dict(DEFAULT_PERMS, **{k: bool(v) for k, v in perms.items() if k in PERMISSIONS})
    if color:
        p["color"] = color
    save(data)
    return public(p)


def remove(pid):
    data = load()
    p = next((x for x in data["profiles"] if x["id"] == pid), None)
    if not p:
        return
    if p.get("admin"):
        raise ProfileError("The admin account can't be removed.")
    data["profiles"].remove(p)
    if data.get("last") == pid:
        data["last"] = next((x["id"] for x in data["profiles"] if x.get("admin")), None)
    save(data)


def set_last(pid):
    data = load()
    data["last"] = pid
    save(data)


def load_personal(pid):
    try:
        with open(os.path.join(_profile_dir(pid), "settings.json"), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def save_personal(pid, values):
    with open(os.path.join(_profile_dir(pid), "settings.json"), "w", encoding="utf-8") as f:
        json.dump(values, f, indent=2)


class Settings(dict):
    """One dict for the app, saved to two places: the account's own keys to its profile, the rest
    to the PC's shared settings. Personal values win when reading."""

    def __init__(self, shared, pid):
        super().__init__(shared)
        self.pid = pid
        self.update(load_personal(pid))
        self.loaded = time.time()

    def save(self):
        shared = paths.load_settings()
        for k in list(shared):
            if k in PERSONAL:
                shared.pop(k)
        for k, v in self.items():
            if k not in PERSONAL:
                shared[k] = v
        for k in [k for k in shared if k not in self and k not in PERSONAL]:
            shared.pop(k)
        paths.save_settings(shared)
        save_personal(self.pid, {k: v for k, v in self.items() if k in PERSONAL})


# ---------- staying signed in, and the same account on every PC ----------
# "remember" is per PC: Frankenstein opens straight into that account, no picker and no PIN, until you
# press Switch. An account is linked across PCs by its "uid" (the id it was made with on the first PC):
# through the cloud backup folder automatically, or with a link code you paste on the other PC.

LINK_PREFIX = "FRK1-"
# kept per PC even when an account is linked: these describe this PC, not the person
DEVICE_KEYS = {"steam_on", "steam_error", "gog_on", "fullscreen", "cloud_folder"}


def uid(p):
    return p.get("link") or p["id"]


def remembered():
    data = load()
    pid = data.get("remember")
    return pid if any(p["id"] == pid for p in data["profiles"]) else None


def set_remember(pid):
    data = load()
    if pid:
        data["remember"] = pid
    else:
        data.pop("remember", None)
    save(data)


def export(pid):
    p = find(pid)
    if not p:
        raise ProfileError("That account doesn't exist.")
    rec = {k: p[k] for k in ("name", "admin", "color", "perms", "pin", "salt") if k in p}
    rec["uid"] = uid(p)
    rec["time"] = time.time()
    rec["personal"] = {k: v for k, v in load_personal(pid).items() if k not in DEVICE_KEYS}
    return rec


def link_code(pid):
    import base64
    import zlib
    raw = zlib.compress(json.dumps(export(pid), separators=(",", ":")).encode("utf-8"), 9)
    return LINK_PREFIX + base64.urlsafe_b64encode(raw).decode("ascii").rstrip("=")


def read_code(code):
    import base64
    import zlib
    code = "".join((code or "").split())
    if not code.startswith(LINK_PREFIX):
        raise ProfileError("That isn't a Frankenstein link code. It starts with " + LINK_PREFIX)
    body = code[len(LINK_PREFIX):]
    try:
        rec = json.loads(zlib.decompress(base64.urlsafe_b64decode(body + "=" * (-len(body) % 4))).decode("utf-8"))
    except Exception:
        raise ProfileError("That link code is cut off or mistyped. Copy the whole thing again.")
    if not rec.get("uid") or not rec.get("name"):
        raise ProfileError("That link code doesn't have an account in it.")
    return rec


def _merge_personal(pid, theirs):
    mine = load_personal(pid)
    for k, v in theirs.items():
        if k in DEVICE_KEYS:
            continue
        if k == "achievements" and isinstance(v, dict) and isinstance(mine.get(k), dict):
            for a, t in v.items():
                mine[k][a] = min(t, mine[k].get(a, t)) if isinstance(t, (int, float)) else mine[k].get(a, t)
        elif k == "counters" and isinstance(v, dict) and isinstance(mine.get(k), dict):
            for c, n in v.items():
                if isinstance(n, (int, float)):
                    mine[k][c] = max(n, mine[k].get(c, 0))
        elif k not in mine:
            mine[k] = v
    save_personal(pid, mine)


def link(rec, newer_only=False):
    """Bring an account from another PC onto this one. The same account (same uid) is updated; the other
    PC's admin becomes this PC's admin if this one isn't linked yet, so you're one person everywhere.
    From the cloud folder (newer_only), two PCs read each other at once: each would take the other's id and they'd
    stay apart, swapped. So there only the lower id is taken, and both end up on it. Returns the local account id."""
    data = load()
    p = next((x for x in data["profiles"] if uid(x) == rec["uid"]), None)
    if p is None and rec.get("admin"):
        p = next((x for x in data["profiles"] if x.get("admin") and not x.get("link")), None)
        if p is not None and newer_only and rec["uid"] > p["id"]:
            return p["id"]  # the other PC takes ours instead
        if p is not None and p["id"] != rec["uid"]:
            p["link"] = rec["uid"]
    if p is not None and newer_only and p.get("synced", 0) >= rec.get("time", 0):
        return p["id"]
    if p is None:
        if any(x["name"].lower() == rec["name"].lower() for x in data["profiles"]):
            rec = dict(rec, name=(rec["name"] + " (2)")[:24])
        p = {"id": "p" + secrets.token_hex(4), "link": rec["uid"], "admin": False}
        data["profiles"].append(p)
    p["name"] = rec["name"][:24]
    p["color"] = rec.get("color", p.get("color", "#8b5cf6"))
    if not p.get("admin"):
        p["perms"] = dict(DEFAULT_PERMS, **(rec.get("perms") or {}))
    if rec.get("pin") and rec.get("salt"):
        p["pin"], p["salt"] = rec["pin"], rec["salt"]
    elif "pin" not in rec:
        p.pop("pin", None)
        p.pop("salt", None)
    p["synced"] = rec.get("time", time.time())
    save(data)
    _merge_personal(p["id"], rec.get("personal") or {})
    return p["id"]


def _sync_file(cloud_root):
    return os.path.join(cloud_root, "Frankenstein accounts", "accounts.json")


def publish(cloud_root):
    """Write every account on this PC to the cloud folder, so your other PCs pick them up."""
    if not cloud_root or not os.path.isdir(cloud_root):
        return
    f = _sync_file(cloud_root)
    try:
        with open(f, encoding="utf-8") as h:
            have = json.load(h)
    except (OSError, ValueError):
        have = {}
    for p in load()["profiles"]:
        have[uid(p)] = export(p["id"])
    os.makedirs(os.path.dirname(f), exist_ok=True)
    with open(f, "w", encoding="utf-8") as h:
        json.dump(have, h)


def pull(cloud_roots):
    """Accounts your other PCs wrote to a cloud folder. Returns how many were added or updated."""
    n = 0
    for root in cloud_roots:
        try:
            with open(_sync_file(root), encoding="utf-8") as h:
                recs = json.load(h)
        except (OSError, ValueError, TypeError):
            continue
        for rec in recs.values():
            if isinstance(rec, dict) and rec.get("uid") and rec.get("name"):
                before = {x["id"]: x.get("synced") for x in load()["profiles"]}
                pid = link(rec, newer_only=True)
                n += before.get(pid, -1) != (find(pid) or {}).get("synced")
    return n
