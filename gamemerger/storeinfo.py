"""What the Steam store says about a game: its genres and how well it supports a controller.
Cached on disk, since it hardly ever changes. Used for the rough fun guess (when there's no AI key)
and for each game's controller badge."""

import json
import os
import time
import urllib.request

from . import paths

URL = "https://store.steampowered.com/api/appdetails?appids=%s&filters=basic,genres,categories&cc=us&l=english"
UA = {"User-Agent": "Frankenstein"}
GENERIC = {"indie", "free to play", "early access", "casual", "massively multiplayer", "utilities", "software"}
FULL_PAD, PARTIAL_PAD = 28, 18  # Steam category ids


def _file(appid):
    return os.path.join(paths.sub(os.path.join("library", "store")), "%s.json" % appid)


def parse(appid, raw):
    entry = (raw or {}).get(str(appid)) or {}
    if not entry.get("success"):
        return {"appid": str(appid), "name": "", "genres": [], "controller": "unknown"}
    d = entry.get("data") or {}
    cats = {c.get("id") for c in d.get("categories") or []}
    return {"appid": str(appid), "name": d.get("name", ""),
            "genres": [g.get("description", "") for g in d.get("genres") or []],
            "controller": "full" if FULL_PAD in cats else "partial" if PARTIAL_PAD in cats else "none"}


def details(appid, max_age=30 * 86400):
    """{"appid", "name", "genres": [...], "controller": "full"|"partial"|"none"|"unknown"} or None offline."""
    if not appid:
        return None
    p = _file(appid)
    try:
        if time.time() - os.path.getmtime(p) < max_age:
            with open(p, encoding="utf-8") as f:
                return json.load(f)
    except (OSError, ValueError):
        pass
    try:
        req = urllib.request.Request(URL % appid, headers=UA)
        with urllib.request.urlopen(req, timeout=15) as r:
            info = parse(appid, json.load(r))
    except (OSError, ValueError):
        return None
    with open(p, "w", encoding="utf-8") as f:
        json.dump(info, f)
    return info


def guess_fun(base, other):
    """A rough fun rating from shared genres: {"rating", "why"}. The AI's rating replaces it when there's a key."""
    tail = " (rough guess; add an AI key in Settings for a real rating)"
    a = {g.lower() for g in (base or {}).get("genres", [])} - GENERIC
    b = {g.lower() for g in (other or {}).get("genres", [])} - GENERIC
    if not a or not b:
        return {"rating": "yellow", "why": "Not enough info to tell" + tail}
    shared = sorted(a & b)
    if len(shared) >= 2:
        return {"rating": "green", "why": "Both are %s games" % " / ".join(shared[:2]) + tail}
    if shared:
        return {"rating": "yellow", "why": "Both are %s games, otherwise quite different" % shared[0] + tail}
    return {"rating": "red", "why": "Different kinds of game (%s vs %s)" % (sorted(a)[0], sorted(b)[0]) + tail}
