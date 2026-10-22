"""Mods from more places than Nexus: CurseForge, Modrinth, and any download link.

  * Modrinth: open API, no key. It's Minecraft's mod site, so it's used for Minecraft.
  * CurseForge: needs a free API key (console.curseforge.com). Covers Minecraft and many other games
    (The Sims 4, Stardew Valley, World of Warcraft add-ons, Valheim and more).
  * A link: any direct download link to a mod file.

Required dependencies are downloaded too, so a mod that needs another mod just works."""

import json
import os
import re
import urllib.error
import urllib.parse
import urllib.request

from . import paths
from .version import VERSION

UA = {"User-Agent": "LKZ007/game-merger/%s (Frankenstein mod launcher)" % VERSION}
MODRINTH = "https://api.modrinth.com/v2"
CURSEFORGE = "https://api.curseforge.com/v1"
CF_REQUIRED = 3  # relationType for a required dependency
CF_LOADERS = {"forge": 1, "fabric": 4, "quilt": 5, "neoforge": 6}
CF_MC_MODS = 6  # Minecraft's "Mods" class


class SourceError(Exception):
    pass


def _get(url, headers=None, timeout=20):
    req = urllib.request.Request(url, headers=dict(UA, Accept="application/json", **(headers or {})))
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.load(r)
    except urllib.error.HTTPError as e:
        if e.code in (401, 403) and "curseforge" in url:
            raise SourceError("Your CurseForge API key didn't work. Check it in Settings.")
        raise SourceError("%s returned an error (%s)." % (urllib.parse.urlparse(url).netloc, e.code))
    except (OSError, ValueError):
        raise SourceError("Can't reach %s right now." % urllib.parse.urlparse(url).netloc)


def safe_file_name(name):
    return re.sub(r'[<>:"/\\|?*\x00-\x1f]', "_", name).strip(" .") or "mod"


def download(url, folder, name=None, progress=lambda d, t: None):
    """Download a file into Frankenstein's downloads folder. Refuses web pages."""
    req = urllib.request.Request(url, headers=UA)
    try:
        r = urllib.request.urlopen(req, timeout=120)
    except urllib.error.HTTPError as e:
        raise SourceError("The download failed (%s)." % e.code)
    except (OSError, ValueError):
        raise SourceError("Couldn't download that. Check the link and your connection.")
    with r:
        if "text/html" in (r.headers.get("Content-Type") or ""):
            raise SourceError("That link opens a web page, not a file. Copy the link of the download button itself.")
        cd = r.headers.get("Content-Disposition") or ""
        m = re.search(r"filename\*?=(?:UTF-8'')?\"?([^\";]+)", cd)
        name = name or (urllib.parse.unquote(m.group(1)) if m else "") or \
            urllib.parse.unquote(os.path.basename(urllib.parse.urlparse(r.geturl()).path)) or "mod.zip"
        dest = os.path.join(paths.sub(os.path.join("downloads", folder)), safe_file_name(name))
        total, done = int(r.headers.get("Content-Length") or 0), 0
        with open(dest + ".part", "wb") as f:
            while True:
                chunk = r.read(1 << 16)
                if not chunk:
                    break
                f.write(chunk)
                done += len(chunk)
                progress(done, total)
    os.replace(dest + ".part", dest)
    return dest


def from_link(link, progress=lambda d, t: None):
    link = (link or "").strip()
    if not re.match(r"^https?://", link, re.I):
        raise SourceError("Paste a full link starting with https://")
    return download(link, "links", progress=progress)


# ---------- Modrinth ----------

def modrinth_search(text, loader=None, game_version=None, count=20):
    facets = [["project_type:mod"]]
    if loader:
        facets.append(["categories:%s" % loader])
    if game_version:
        facets.append(["versions:%s" % game_version])
    q = urllib.parse.urlencode({"query": text, "limit": count, "index": "relevance", "facets": json.dumps(facets)})
    hits = _get("%s/search?%s" % (MODRINTH, q)).get("hits", [])
    return [{"source": "Modrinth", "id": h["project_id"], "name": h.get("title", ""), "summary": (h.get("description") or "")[:220],
             "picture": h.get("icon_url") or "", "downloads": h.get("downloads", 0), "author": h.get("author", ""),
             "page": "https://modrinth.com/mod/%s" % h.get("slug", h["project_id"])} for h in hits]


def modrinth_version(project, loader=None, game_version=None):
    q = {}
    if loader:
        q["loaders"] = json.dumps([loader])
    if game_version:
        q["game_versions"] = json.dumps([game_version])
    versions = _get("%s/project/%s/version?%s" % (MODRINTH, urllib.parse.quote(project), urllib.parse.urlencode(q)))
    if not versions:
        raise SourceError("That mod has no version for %s%s." % (game_version or "this game", " with " + loader if loader else ""))
    release = [v for v in versions if v.get("version_type") == "release"] or versions
    return release[0]


def modrinth_get(project, loader=None, game_version=None, progress=lambda d, t: None, seen=None):
    """Download a Modrinth mod and its required dependencies. Returns [(name, path)]."""
    seen = seen if seen is not None else set()
    if project in seen:
        return []
    seen.add(project)
    v = modrinth_version(project, loader, game_version)
    files = v.get("files") or []
    f = next((x for x in files if x.get("primary")), files[0] if files else None)
    if not f:
        raise SourceError("That mod version has no file.")
    out = [(v.get("name") or f["filename"], download(f["url"], "modrinth", f["filename"], progress))]
    for dep in v.get("dependencies") or []:
        if dep.get("dependency_type") == "required":
            pid = dep.get("project_id")
            if not pid and dep.get("version_id"):
                pid = _get("%s/version/%s" % (MODRINTH, dep["version_id"])).get("project_id")
            if pid:
                out += modrinth_get(pid, loader, game_version, progress, seen)
    return out


# ---------- CurseForge ----------

def _cf(path, key):
    if not key:
        raise SourceError("Add your CurseForge API key in Settings to use CurseForge.")
    return _get(CURSEFORGE + path, {"x-api-key": key})


def _norm(name):
    return re.sub(r"[^a-z0-9]", "", (name or "").lower())


def curseforge_game(name, key):
    """CurseForge's id for a game, by name (cached). None if CurseForge doesn't have it."""
    cache = os.path.join(paths.sub("library"), "curseforge-games.json")
    try:
        with open(cache, encoding="utf-8") as f:
            games = json.load(f)
    except (OSError, ValueError):
        games, index = [], 0
        while True:
            page = _cf("/games?index=%d&pageSize=50" % index, key)
            games += [{"id": g["id"], "name": g["name"]} for g in page.get("data", [])]
            total = (page.get("pagination") or {}).get("totalCount", 0)
            index += 50
            if index >= total or not page.get("data"):
                break
        with open(cache, "w", encoding="utf-8") as f:
            json.dump(games, f)
    n = _norm(re.sub(r"(?i):? ?(java edition|bedrock edition)$", "", name))
    return next((g["id"] for g in games if _norm(g["name"]) == n), None)


def curseforge_search(game_id, text, key, loader=None, game_version=None, count=20):
    q = {"gameId": game_id, "searchFilter": text, "sortField": 2, "sortOrder": "desc", "pageSize": count}
    if loader in CF_LOADERS:
        q["modLoaderType"] = CF_LOADERS[loader]
        q["classId"] = CF_MC_MODS
    if game_version:
        q["gameVersion"] = game_version
    data = _cf("/mods/search?" + urllib.parse.urlencode(q), key).get("data", [])
    return [{"source": "CurseForge", "id": m["id"], "name": m.get("name", ""), "summary": (m.get("summary") or "")[:220],
             "picture": ((m.get("logo") or {}).get("thumbnailUrl")) or "", "downloads": int(m.get("downloadCount") or 0),
             "author": ((m.get("authors") or [{}])[0]).get("name", ""), "page": (m.get("links") or {}).get("websiteUrl", "")}
            for m in data]


def curseforge_file(mod_id, key, loader=None, game_version=None):
    q = {"pageSize": 20}
    if loader in CF_LOADERS:
        q["modLoaderType"] = CF_LOADERS[loader]
    if game_version:
        q["gameVersion"] = game_version
    files = _cf("/mods/%s/files?%s" % (mod_id, urllib.parse.urlencode(q)), key).get("data", [])
    files = [f for f in files if f.get("isAvailable", True)]
    if not files:
        raise SourceError("That mod has no file for %s." % (game_version or "this game"))
    release = [f for f in files if f.get("releaseType") == 1] or files  # 1 = release
    return max(release, key=lambda f: f.get("fileDate", ""))


def curseforge_get(mod_id, key, loader=None, game_version=None, progress=lambda d, t: None, seen=None):
    """Download a CurseForge mod and its required dependencies. Returns ([(name, path)], [pages to open]):
    some authors only allow downloads on CurseForge's own site, so those come back as pages."""
    seen = seen if seen is not None else set()
    if mod_id in seen:
        return [], []
    seen.add(mod_id)
    f = curseforge_file(mod_id, key, loader, game_version)
    got, pages = [], []
    if f.get("downloadUrl"):
        got.append((f.get("displayName") or f["fileName"], download(f["downloadUrl"], "curseforge", f["fileName"], progress)))
    else:
        mod = _cf("/mods/%s" % mod_id, key).get("data", {})
        pages.append("%s/files/%s" % ((mod.get("links") or {}).get("websiteUrl", "").rstrip("/"), f["id"]))
    for dep in f.get("dependencies") or []:
        if dep.get("relationType") == CF_REQUIRED:
            a, b = curseforge_get(dep["modId"], key, loader, game_version, progress, seen)
            got += a
            pages += b
    return got, pages
