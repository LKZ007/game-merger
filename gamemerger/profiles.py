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
