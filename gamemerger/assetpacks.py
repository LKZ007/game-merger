"""Free 3D model packs for Frankenstein's own world and cars.

Frankenstein doesn't ship other people's models inside its installer. Your PC downloads each pack from its maker's
own page the first time it's needed, keeps it in the app's assets folder, and puts the models into a merge when you
press Play. Every pack here is CC0 (free for any use, no credit needed), and its licence is written next to the
models. The first download's SHA-256 is recorded, so a pack that changes later is noticed and logged."""

import hashlib
import json
import os
import re
import shutil
import threading
import time
import zipfile

from . import downloader, paths

# Kenney's packs are CC0 1.0: https://kenney.nl/support (each pack's page links its own License.txt)
PACKS = [
    {"id": "kenney-car-kit", "name": "Car Kit", "author": "Kenney", "page": "https://kenney.nl/assets/car-kit",
     "kind": "cars", "licence": "CC0 1.0 (public domain)"},
    {"id": "kenney-city-commercial", "name": "City Kit (Commercial)", "author": "Kenney",
     "page": "https://kenney.nl/assets/city-kit-commercial", "kind": "buildings", "licence": "CC0 1.0 (public domain)"},
    {"id": "kenney-city-suburban", "name": "City Kit (Suburban)", "author": "Kenney",
     "page": "https://kenney.nl/assets/city-kit-suburban", "kind": "buildings", "licence": "CC0 1.0 (public domain)"},
    {"id": "kenney-city-industrial", "name": "City Kit (Industrial)", "author": "Kenney",
     "page": "https://kenney.nl/assets/city-kit-industrial", "kind": "buildings", "licence": "CC0 1.0 (public domain)"},
    {"id": "kenney-city-roads", "name": "City Kit (Roads)", "author": "Kenney", "page": "https://kenney.nl/assets/city-kit-roads",
     "kind": "roads", "licence": "CC0 1.0 (public domain)"},
]
ZIP_LINK = r'href="([^"]+\.zip)"'
KEEP = (".glb", ".gltf", ".bin", ".png", ".jpg", ".jpeg")
MODELS_DIR = ("UserData", "Frankenstein", "Models")

_lock = threading.Lock()
_state = {"running": False, "log": []}


def root():
    return paths.sub("assets")


def _say(line, log):
    _state["log"] = (_state["log"] + [time.strftime("%H:%M:%S ") + line])[-200:]
    log(line)


def _manifest_path():
    return os.path.join(root(), "packs.json")


def manifest():
    try:
        with open(_manifest_path(), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def _save_manifest(data):
    tmp = _manifest_path() + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    os.replace(tmp, _manifest_path())


def pack_dir(pack_id):
    return os.path.join(root(), pack_id, "models")


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def extract(zpath, dest):
    """Keep only the model files (and the textures they use), with their folders, minus the zip's top folder."""
    tmp = dest + ".tmp"
    shutil.rmtree(tmp, ignore_errors=True)
    n = 0
    with zipfile.ZipFile(zpath) as z:
        names = [i for i in z.infolist() if not i.is_dir() and i.filename.lower().endswith(KEEP)]
        tops = {i.filename.split("/", 1)[0] for i in z.infolist()}
        strip = len(tops) == 1
        for i in names:
            rel = i.filename.split("/", 1)[1] if strip and "/" in i.filename else i.filename
            parts = [p for p in rel.replace("\\", "/").split("/") if p not in ("", ".", "..")]
            if not parts:
                continue
            out = os.path.join(tmp, *parts)
            os.makedirs(os.path.dirname(out), exist_ok=True)
            with z.open(i) as src, open(out, "wb") as f:
                shutil.copyfileobj(src, f, 1 << 20)
            n += 1
    shutil.rmtree(dest, ignore_errors=True)
    os.replace(tmp, dest)
    return n


def fetch(pack, log=lambda *_: None):
    """Download one pack from its maker's page (cached), check it, and unpack its models."""
    man = manifest()
    have = man.get(pack["id"])
    if have and os.path.isdir(pack_dir(pack["id"])):
        return have
    _say("Finding %s by %s on %s ..." % (pack["name"], pack["author"], pack["page"]), log)
    _tag, name, url = downloader.scraped_asset(pack["page"], ZIP_LINK)
    folder = os.path.join(root(), pack["id"])
    os.makedirs(folder, exist_ok=True)
    zpath = os.path.join(folder, re.sub(r"[^\w.-]", "_", name))
    if not os.path.isfile(zpath):
        _say("Downloading %s ..." % name, log)
        downloader.download_file(url, zpath)
    if not zipfile.is_zipfile(zpath):
        os.remove(zpath)
        raise ValueError("%s didn't download as a zip file" % name)
    digest = sha256(zpath)
    first = (man.get("_seen") or {}).get(pack["id"])
    if first and first != digest:
        _say("Note: %s changed since it was first downloaded (the maker updated it)." % pack["name"], log)
    n = extract(zpath, pack_dir(pack["id"]))
    if not any(f.lower().endswith((".glb", ".gltf")) for _d, _s, fs in os.walk(pack_dir(pack["id"])) for f in fs):
        raise ValueError("%s has no .glb or .gltf models in it" % name)
    entry = {"name": pack["name"], "author": pack["author"], "page": pack["page"], "url": url, "zip": name,
             "sha256": digest, "files": n, "licence": pack["licence"], "kind": pack["kind"], "at": time.time()}
    man[pack["id"]] = entry
    man.setdefault("_seen", {})[pack["id"]] = first or digest
    _save_manifest(man)
    _say("%s ready: %d files." % (pack["name"], n), log)
    return entry


def fetch_all(log=lambda *_: None):
    """Every pack, one after another. A pack that fails is logged and skipped: the game starts without it."""
    got, failed = [], []
    for p in PACKS:
        try:
            fetch(p, log)
            got.append(p["id"])
        except Exception as e:  # no internet, the page changed: the rest still come
            failed.append({"id": p["id"], "error": str(e)[:200]})
            _say("Couldn't get %s: %s" % (p["name"], e), log)
    return {"got": got, "failed": failed}


def fetch_in_background(log=lambda *_: None):
    """Start downloading missing packs without holding anything up. Returns False if a download is already going."""
    with _lock:
        if _state["running"]:
            return False
        if all(p["id"] in manifest() and os.path.isdir(pack_dir(p["id"])) for p in PACKS):
            return False
        _state["running"] = True

    def run():
        try:
            fetch_all(log)
        finally:
            _state["running"] = False
    threading.Thread(target=run, daemon=True).start()
    return True


def status():
    man = manifest()
    return {"running": _state["running"], "log": _state["log"][-20:],
            "packs": [dict(p, ready=p["id"] in man and os.path.isdir(pack_dir(p["id"])), files=(man.get(p["id"]) or {}).get("files", 0))
                      for p in PACKS]}


def install_into(game_root, log=lambda *_: None):
    """Copy the downloaded packs into a merge's game copy (only files that differ), plus a licence list and a
    catalogue the mod reads. The installed game and the app's own copy are only read."""
    dest_root = os.path.join(game_root, *MODELS_DIR)
    man = manifest()
    catalog, copied = [], 0
    for p in PACKS:
        src = pack_dir(p["id"])
        if p["id"] not in man or not os.path.isdir(src):
            continue
        for dirpath, _dirs, files in os.walk(src):
            for f in files:
                s = os.path.join(dirpath, f)
                rel = os.path.relpath(s, src)
                d = os.path.join(dest_root, p["id"], rel)
                if not (os.path.isfile(d) and os.path.getsize(d) == os.path.getsize(s)):
                    os.makedirs(os.path.dirname(d), exist_ok=True)
                    shutil.copyfile(s, d)
                    copied += 1
                if f.lower().endswith((".glb", ".gltf")):
                    catalog.append({"pack": p["id"], "kind": p["kind"], "name": os.path.splitext(f)[0],
                                    "file": "/".join([p["id"]] + rel.replace("\\", "/").split("/"))})
    if not catalog:
        return {"models": 0, "copied": 0}
    os.makedirs(dest_root, exist_ok=True)
    with open(os.path.join(dest_root, "catalog.json"), "w", encoding="utf-8") as f:
        json.dump({"models": catalog}, f)
    with open(os.path.join(dest_root, "LICENCES.txt"), "w", encoding="utf-8") as f:
        f.write("Models Frankenstein downloaded for this merge, from their makers' own pages.\n\n")
        for pid, e in man.items():
            if pid.startswith("_"):
                continue
            f.write("%s by %s: %s\n  from %s\n  sha256 %s\n\n" % (e["name"], e["author"], e["licence"], e["page"], e["sha256"]))
    log("Models: %d in the game (%d files copied)." % (len(catalog), copied))
    return {"models": len(catalog), "copied": copied}
