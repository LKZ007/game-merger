"""Nexus Mods: find and download mods without leaving Frankenstein.

Needs the player's personal API key (nexusmods.com > Settings > API keys).
  * Premium members: Frankenstein downloads the mod's main file directly.
  * Free members: Nexus only allows downloads after a click on its site, so Frankenstein opens the
    mod's Files page. Clicking "Mod Manager Download" there sends the file straight back into the app
    (Frankenstein registers itself for nxm:// links), and it's added to your merge automatically."""

import json
import os
import re
import urllib.error
import urllib.parse
import urllib.request

from . import paths
from .version import VERSION

V1 = "https://api.nexusmods.com/v1"
GRAPHQL = "https://api.nexusmods.com/v2/graphql"
HEADERS = {"Application-Name": "Frankenstein", "Application-Version": VERSION, "User-Agent": "Frankenstein/" + VERSION}


class NexusError(Exception):
    pass


def _get(path, key):
    req = urllib.request.Request(V1 + path, headers=dict(HEADERS, apikey=key, Accept="application/json"))
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        if e.code == 401:
            raise NexusError("Your Nexus Mods API key didn't work. Check it in Settings.")
        if e.code == 403:
            raise NexusError("Nexus only lets premium members download directly.")
        if e.code == 429:
            raise NexusError("Nexus says we've hit its hourly limit. Try again in a bit.")
        raise NexusError("Nexus Mods returned an error (%s)." % e.code)
    except OSError:
        raise NexusError("Can't reach Nexus Mods right now.")


def account(key):
    """{"name", "premium"} for this API key."""
    u = _get("/users/validate.json", key)
    return {"name": u.get("name", ""), "premium": bool(u.get("is_premium") or u.get("is_supporter"))}


def search(domain, text, count=20):
    """Most-endorsed mods for this game matching the text."""
    query = """query($game: String!, $name: String!, $count: Int!) {
      mods(filter: {gameDomainName: [{value: $game, op: EQUALS}], name: [{value: $name, op: WILDCARD}]},
           sort: [{endorsements: {direction: DESC}}], count: $count) {
        nodes { modId name summary pictureUrl endorsements author }
      }
    }"""
    body = json.dumps({"query": query, "variables": {"game": domain, "name": "*%s*" % text.strip(), "count": count}})
    req = urllib.request.Request(GRAPHQL, data=body.encode("utf-8"), method="POST",
                                 headers=dict(HEADERS, **{"Content-Type": "application/json"}))
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            data = json.load(r)
    except (OSError, ValueError):
        raise NexusError("Nexus search isn't answering right now.")
    nodes = (((data or {}).get("data") or {}).get("mods") or {}).get("nodes")
    if nodes is None:
        raise NexusError("Nexus search isn't answering right now.")
    return [{"id": n["modId"], "name": n.get("name", ""), "summary": (n.get("summary") or "")[:220],
             "picture": n.get("pictureUrl") or "", "endorsements": n.get("endorsements") or 0,
             "author": n.get("author") or ""} for n in nodes]


def main_file(domain, mod_id, key):
    files = _get("/games/%s/mods/%s/files.json" % (domain, mod_id), key).get("files", [])
    main = [f for f in files if (f.get("category_name") or "").upper() == "MAIN"] or files
    if not main:
        raise NexusError("That mod has no files to download.")
    return max(main, key=lambda f: f.get("uploaded_timestamp") or 0)


def files_page(domain, mod_id):
    return "https://www.nexusmods.com/%s/mods/%s?tab=files" % (domain, mod_id)


def download(domain, mod_id, file_id, key, nxm_key=None, expires=None, progress=lambda d, t: None):
    """Download a mod file into Frankenstein's downloads folder. Returns its path."""
    q = "?key=%s&expires=%s" % (urllib.parse.quote(nxm_key), urllib.parse.quote(str(expires))) if nxm_key else ""
    links = _get("/games/%s/mods/%s/files/%s/download_link.json%s" % (domain, mod_id, file_id, q), key)
    if not links:
        raise NexusError("Nexus didn't give a download link.")
    url = links[0]["URI"]
    name = urllib.parse.unquote(os.path.basename(urllib.parse.urlparse(url).path)) or "%s-%s.zip" % (mod_id, file_id)
    name = re.sub(r'[<>:"/\\|?*]', "_", name)
    dest = os.path.join(paths.sub("downloads"), "nexus", name)
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    req = urllib.request.Request(url, headers={"User-Agent": HEADERS["User-Agent"]})
    with urllib.request.urlopen(req, timeout=120) as r, open(dest + ".part", "wb") as f:
        total, done = int(r.headers.get("Content-Length") or 0), 0
        while True:
            chunk = r.read(1 << 16)
            if not chunk:
                break
            f.write(chunk)
            done += len(chunk)
            progress(done, total)
    os.replace(dest + ".part", dest)
    return dest


def parse_nxm(link):
    """nxm://cyberpunk2077/mods/107/files/12345?key=...&expires=...&user_id=... -> dict, or None."""
    m = re.match(r"^nxm://([^/]+)/mods/(\d+)/files/(\d+)(\?.*)?$", link.strip(), re.I)
    if not m:
        return None
    q = urllib.parse.parse_qs((m.group(4) or "?")[1:])
    return {"domain": m.group(1).lower(), "mod": m.group(2), "file": m.group(3),
            "key": (q.get("key") or [None])[0], "expires": (q.get("expires") or [None])[0]}
