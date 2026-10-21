"""Every game you own, not just the ones installed on this PC.

What each launcher allows (being honest about it):
  * Steam: your account is found from the Steam app on this PC. Your owned games come from Steam's
    public profile list when "Game details" is public, otherwise from Steam's Web API with your own
    free key (steamcommunity.com/dev/apikey).
  * Epic: one sign-in in a small Epic window, then your library comes from Epic's launcher service
    (the same unofficial API that Legendary and Heroic use; Epic has no public library API).
  * GOG: GOG Galaxy already keeps your whole owned list on this PC, so nothing to sign in to.
  * Xbox / Microsoft Store: Microsoft has no way for other apps to list the games you own. Only
    installed ones show.
Sign-in tokens and keys are kept in Windows Credential Manager, never in plain files.

Devices: with cloud backup on, each PC writes a small list of what's installed on it to the cloud
folder, so the laptop can say "Installed on DESKTOP-ABC"."""

import base64
import concurrent.futures
import json
import os
import re
import socket
import sqlite3
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

from . import paths
from .version import VERSION

UA = {"User-Agent": "Frankenstein/" + VERSION}


class AccountError(Exception):
    pass


# ---------- Windows Credential Manager ----------

_TARGET = "Frankenstein/%s"


def _creds_file():
    return os.path.join(paths.data_dir(), "credentials-dev.json")  # only used off Windows (tests)


def secret_set(name, value):
    if sys.platform != "win32":
        data = _dev_creds()
        data[name] = value
        with open(_creds_file(), "w", encoding="utf-8") as f:
            json.dump(data, f)
        return
    import ctypes
    from ctypes import wintypes

    class CREDENTIAL(ctypes.Structure):
        _fields_ = [("Flags", wintypes.DWORD), ("Type", wintypes.DWORD), ("TargetName", wintypes.LPWSTR),
                    ("Comment", wintypes.LPWSTR), ("LastWritten", wintypes.FILETIME),
                    ("CredentialBlobSize", wintypes.DWORD), ("CredentialBlob", ctypes.c_void_p),
                    ("Persist", wintypes.DWORD), ("AttributeCount", wintypes.DWORD), ("Attributes", ctypes.c_void_p),
                    ("TargetAlias", wintypes.LPWSTR), ("UserName", wintypes.LPWSTR)]

    blob = value.encode("utf-8")
    buf = ctypes.create_string_buffer(blob, len(blob))
    cred = CREDENTIAL(Type=1, TargetName=_TARGET % name, CredentialBlobSize=len(blob),
                      CredentialBlob=ctypes.cast(buf, ctypes.c_void_p), Persist=2, UserName="Frankenstein")
    if not ctypes.windll.advapi32.CredWriteW(ctypes.byref(cred), 0):
        raise AccountError("Windows wouldn't save the sign-in.")


def secret_get(name):
    if sys.platform != "win32":
        return _dev_creds().get(name)
    import ctypes
    from ctypes import wintypes

    class CREDENTIAL(ctypes.Structure):
        _fields_ = [("Flags", wintypes.DWORD), ("Type", wintypes.DWORD), ("TargetName", wintypes.LPWSTR),
                    ("Comment", wintypes.LPWSTR), ("LastWritten", wintypes.FILETIME),
                    ("CredentialBlobSize", wintypes.DWORD), ("CredentialBlob", ctypes.c_void_p),
                    ("Persist", wintypes.DWORD), ("AttributeCount", wintypes.DWORD), ("Attributes", ctypes.c_void_p),
                    ("TargetAlias", wintypes.LPWSTR), ("UserName", wintypes.LPWSTR)]

    p = ctypes.POINTER(CREDENTIAL)()
    if not ctypes.windll.advapi32.CredReadW(_TARGET % name, 1, 0, ctypes.byref(p)):
        return None
    try:
        c = p.contents
        return ctypes.string_at(c.CredentialBlob, c.CredentialBlobSize).decode("utf-8")
    finally:
        ctypes.windll.advapi32.CredFree(p)


def secret_delete(name):
    if sys.platform != "win32":
        data = _dev_creds()
        data.pop(name, None)
        with open(_creds_file(), "w", encoding="utf-8") as f:
            json.dump(data, f)
        return
    import ctypes
    ctypes.windll.advapi32.CredDeleteW(_TARGET % name, 1, 0)


def _dev_creds():
    try:
        with open(_creds_file(), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


# ---------- small web helpers ----------

def _request(url, data=None, headers=None, timeout=20):
    req = urllib.request.Request(url, data=data, headers=dict(UA, **(headers or {})))
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def _json(url, **kw):
    return json.loads(_request(url, **kw).decode("utf-8"))


def _cache_file(name):
    return os.path.join(paths.sub("library"), name)


def _save_cache(name, games):
    with open(_cache_file(name), "w", encoding="utf-8") as f:
        json.dump({"time": time.time(), "games": games}, f)


def cached(name):
    try:
        with open(_cache_file(name), encoding="utf-8") as f:
            return json.load(f).get("games", [])
    except (OSError, ValueError):
        return []


# ---------- Steam ----------

def steam_account(steam_root):
    """(steamid64, persona name) of the most recent Steam user on this PC, or (None, None)."""
    if not steam_root:
        return None, None
    try:
        with open(os.path.join(steam_root, "config", "loginusers.vdf"), encoding="utf-8", errors="ignore") as f:
            text = f.read()
    except OSError:
        return None, None
    return parse_loginusers(text)


def parse_loginusers(text):
    users = re.findall(r'"(\d{17})"\s*\{([^}]*)\}', text)
    if not users:
        return None, None

    def val(body, key):
        m = re.search(r'"%s"\s+"([^"]*)"' % key, body, re.I)
        return m.group(1) if m else ""
    best = next((u for u in users if val(u[1], "MostRecent") == "1"), users[0])
    return best[0], val(best[1], "PersonaName") or None


def steam_id_from(text):
    """A SteamID64 from what the player typed: the number or a profile link."""
    m = re.search(r"(\d{17})", text or "")
    return m.group(1) if m else None


def steam_owned(steamid, key=None):
    """[{id, name}] of every game on the Steam account."""
    if key:
        url = ("https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key=%s&steamid=%s"
               "&include_appinfo=1&include_played_free_games=1&format=json") % (urllib.parse.quote(key), steamid)
        try:
            data = _json(url)
        except urllib.error.HTTPError as e:
            raise AccountError("Steam didn't accept that key." if e.code in (401, 403) else "Steam said no (%s)." % e.code)
        except OSError:
            raise AccountError("Can't reach Steam right now.")
        games = (data.get("response") or {}).get("games")
        if games is None:
            raise AccountError("Steam sent no games. Is your profile's Game details set to Public?")
        return [{"id": str(g["appid"]), "name": g.get("name") or str(g["appid"])} for g in games]
    try:
        raw = _request("https://steamcommunity.com/profiles/%s/games?tab=all&xml=1" % steamid)
    except OSError:
        raise AccountError("Can't reach Steam right now.")
    return parse_steam_xml(raw)


def parse_steam_xml(raw):
    try:
        root = ET.fromstring(raw)
    except ET.ParseError:
        raise AccountError("private")
    games = [{"id": g.findtext("appID"), "name": (g.findtext("name") or "").strip()} for g in root.iter("game")]
    games = [g for g in games if g["id"]]
    if not games:
        raise AccountError("private")
    return games


def steam_install_link(appid):
    return "steam://install/%s" % appid


# ---------- Epic ----------
# The Epic Games Launcher's public client (the same one Legendary and Heroic use).
EPIC_CLIENT = ("34a02cf8f4414e29b15921876da36f9a", "daafbccc737745039dffe53d94fc76cf")
EPIC_LOGIN = ("https://www.epicgames.com/id/login?redirectUrl=" + urllib.parse.quote(
    "https://www.epicgames.com/id/api/redirect?clientId=%s&responseType=code" % EPIC_CLIENT[0], safe=""))
EPIC_TOKEN = "https://account-public-service-prod03.ol.epicgames.com/account/api/oauth/token"
EPIC_LIBRARY = "https://library-service.live.use1a.on.epicgames.com/library/api/public/items?includeMetadata=true"
EPIC_CATALOG = ("https://catalog-public-service-prod06.ol.epicgames.com/catalog/api/shared/namespace/%s/bulk/items"
                "?id=%s&includeDLCDetails=false&includeMainGameDetails=true&country=US&locale=en")


def epic_code_from(page_text):
    """The authorization code from Epic's redirect page (JSON), or None."""
    try:
        return json.loads(page_text).get("authorizationCode")
    except (ValueError, AttributeError):
        m = re.search(r'"authorizationCode"\s*:\s*"([0-9a-f]{32})"', page_text or "")
        return m.group(1) if m else None


def _epic_token(form):
    auth = base64.b64encode(("%s:%s" % EPIC_CLIENT).encode()).decode()
    try:
        data = _json(EPIC_TOKEN, data=urllib.parse.urlencode(dict(form, token_type="eg1")).encode(),
                     headers={"Authorization": "basic " + auth, "Content-Type": "application/x-www-form-urlencoded"})
    except urllib.error.HTTPError:
        raise AccountError("Epic sign-in expired. Sign in to Epic again in Settings.")
    except OSError:
        raise AccountError("Can't reach Epic right now.")
    secret_set("epic", json.dumps({"refresh_token": data["refresh_token"], "name": data.get("displayName", "")}))
    return data


def epic_sign_in(code):
    """Swap the code from the sign-in window for a session. Returns the Epic display name."""
    return _epic_token({"grant_type": "authorization_code", "code": code}).get("displayName", "")


def epic_account():
    saved = secret_get("epic")
    return json.loads(saved) if saved else None


def epic_owned():
    saved = epic_account()
    if not saved:
        raise AccountError("Sign in to Epic in Settings first.")
    token = _epic_token({"grant_type": "refresh_token", "refresh_token": saved["refresh_token"]})["access_token"]
    head = {"Authorization": "bearer " + token}
    items, cursor = [], None
    for _ in range(50):
        url = EPIC_LIBRARY + ("&cursor=" + urllib.parse.quote(cursor) if cursor else "")
        try:
            data = _json(url, headers=head)
        except OSError:
            raise AccountError("Can't reach Epic right now.")
        items += data.get("records", [])
        cursor = (data.get("responseMetadata") or {}).get("nextCursor")
        if not cursor:
            break
    titles = _epic_titles([i for i in items if i.get("namespace") != "ue" and i.get("catalogItemId")], head)
    out, seen = [], set()
    for i in items:
        t = titles.get(i.get("catalogItemId"))
        if not t or t["id"] in seen:
            continue
        seen.add(t["id"])
        out.append(dict(t, install=epic_install_link(i)))
    return out


def _epic_titles(items, head):
    """catalogItemId -> {id, name, image} for games (not DLC, not engine assets). Cached on disk."""
    path = _cache_file("epic-catalog.json")
    try:
        with open(path, encoding="utf-8") as f:
            known = json.load(f)
    except (OSError, ValueError):
        known = {}

    def look(i):
        try:
            data = _json(EPIC_CATALOG % (i["namespace"], i["catalogItemId"]), headers=head)
        except OSError:
            return i["catalogItemId"], None
        c = data.get(i["catalogItemId"]) or {}
        if c.get("mainGameItem") or not c.get("title"):
            return i["catalogItemId"], False  # DLC or nothing to show
        imgs = {k.get("type"): k.get("url") for k in c.get("keyImages", [])}
        image = imgs.get("DieselGameBoxTall") or imgs.get("OfferImageTall") or imgs.get("Thumbnail") or ""
        return i["catalogItemId"], {"id": i["catalogItemId"], "name": c["title"], "image": image}

    todo = [i for i in items if i["catalogItemId"] not in known]
    with concurrent.futures.ThreadPoolExecutor(8) as pool:
        for cid, t in pool.map(look, todo):
            if t is not None:
                known[cid] = t
    with open(path, "w", encoding="utf-8") as f:
        json.dump(known, f)
    return {k: v for k, v in known.items() if v}


def epic_install_link(item):
    return "com.epicgames.launcher://apps/%s?action=install" % urllib.parse.quote(
        "%s:%s:%s" % (item.get("namespace", ""), item.get("catalogItemId", ""), item.get("appName", "")), safe="")


# ---------- GOG (from GOG Galaxy on this PC) ----------

def galaxy_db():
    p = os.path.join(os.environ.get("PROGRAMDATA", r"C:\ProgramData"), "GOG.com", "Galaxy", "storage", "galaxy-2.0.db")
    return p if os.path.isfile(p) else None


def gog_owned(db=None):
    db = db or galaxy_db()
    if not db:
        raise AccountError("GOG Galaxy isn't on this PC.")
    try:
        con = sqlite3.connect("file:%s?mode=ro" % db.replace("\\", "/"), uri=True, timeout=5)
    except sqlite3.Error:
        raise AccountError("Couldn't read GOG Galaxy's game list.")
    try:
        rows = con.execute(
            "SELECT lr.releaseKey, gp.value, t.type FROM LibraryReleases lr "
            "JOIN GamePieces gp ON gp.releaseKey = lr.releaseKey "
            "JOIN GamePieceTypes t ON t.id = gp.gamePieceTypeId "
            "WHERE lr.releaseKey LIKE 'gog_%' AND t.type IN ('originalTitle', 'title', 'originalImages')").fetchall()
    except sqlite3.Error:
        raise AccountError("Couldn't read GOG Galaxy's game list.")
    finally:
        con.close()
    games = {}
    for key, value, kind in rows:
        g = games.setdefault(key, {"id": key[4:], "name": "", "image": ""})
        try:
            v = json.loads(value) or {}
        except ValueError:
            continue
        if kind in ("originalTitle", "title") and v.get("title") and (kind == "title" or not g["name"]):
            g["name"] = v["title"]
        elif kind == "originalImages":
            g["image"] = v.get("verticalCover") or v.get("background") or ""
    return [dict(g, install="goggalaxy://openGameView/%s" % g["id"]) for g in games.values() if g["name"]]


# ---------- which PC has what (through the cloud backup folder) ----------

def device_name():
    return os.environ.get("COMPUTERNAME") or socket.gethostname()


def _devices_dir(cloud_root):
    return os.path.join(cloud_root, "Frankenstein devices")


def publish_installed(cloud_root, installed):
    """installed: [{name, source, appid}] on this PC."""
    if not cloud_root or not os.path.isdir(cloud_root):
        return
    os.makedirs(_devices_dir(cloud_root), exist_ok=True)
    with open(os.path.join(_devices_dir(cloud_root), device_name() + ".json"), "w", encoding="utf-8") as f:
        json.dump({"device": device_name(), "time": time.time(), "games": installed}, f)


def other_devices(cloud_root):
    out = []
    if not cloud_root or not os.path.isdir(_devices_dir(cloud_root)):
        return out
    for name in os.listdir(_devices_dir(cloud_root)):
        if not name.endswith(".json") or name[:-5].lower() == device_name().lower():
            continue
        try:
            with open(os.path.join(_devices_dir(cloud_root), name), encoding="utf-8") as f:
                out.append(json.load(f))
        except (OSError, ValueError):
            pass
    return out


def norm(name):
    name = re.sub(r"[®™©:\-–]", " ", (name or "").lower())
    return re.sub(r"\s+", " ", name).strip()


def merge_lists(installed, owned_by_source, devices):
    """Owned games that aren't installed on this PC, each with where else it's installed.
    installed: [{name, source, appid}]. owned_by_source: {"Steam": [...], "Epic": [...], "GOG": [...]}."""
    here_ids = {(g["source"], str(g.get("appid") or "")) for g in installed if g.get("appid")}
    here_names = {norm(g["name"]) for g in installed}
    elsewhere = {}
    for d in devices:
        for g in d.get("games", []):
            for k in (("id", g.get("source"), str(g.get("appid") or "")), ("name", norm(g.get("name")))):
                elsewhere.setdefault(k, d.get("device", "another PC"))
    out = []
    for source, games in owned_by_source.items():
        for g in games:
            if (source, str(g["id"])) in here_ids or norm(g["name"]) in here_names:
                continue
            where = elsewhere.get(("id", source, str(g["id"]))) or elsewhere.get(("name", norm(g["name"])))
            out.append({"key": "own:%s:%s" % (source.lower(), g["id"]), "name": g["name"], "source": source,
                        "appid": g["id"] if source == "Steam" else "", "image": g.get("image", ""),
                        "install": g.get("install") or (steam_install_link(g["id"]) if source == "Steam" else ""),
                        "elsewhere": where})
    out.sort(key=lambda g: (g["elsewhere"] is None, norm(g["name"])))
    return out
