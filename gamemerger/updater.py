"""Keeps Frankenstein up to date from its GitHub releases.

On start the app asks GitHub for the latest release. If it's newer, Franklin offers it; one
click downloads FrankensteinSetup.exe, closes the app, and the installer updates it silently and
opens it again. Merges, saves and settings live in %LOCALAPPDATA%\\GameMerger, so updates never
touch them."""

import json
import os
import re
import subprocess
import sys
import urllib.request

from . import paths
from .version import REPO, VERSION

UA = {"User-Agent": "Frankenstein/" + VERSION, "Accept": "application/vnd.github+json"}
ASSET = re.compile(r"FrankensteinSetup.*\.exe$", re.I)
OLD_ASSET = re.compile(r"GameMergerSetup.*\.exe$", re.I)  # releases from before the rename


def _v(tag):
    return tuple(int(x) for x in re.findall(r"\d+", tag)[:3]) or (0,)


def latest(timeout=10, force=False):
    """{"version", "notes", "url", "size"} for the newest release, or None if there's nothing newer.
    force: the newest release even if it's this version (to repair a damaged install)."""
    req = urllib.request.Request("https://api.github.com/repos/%s/releases/latest" % REPO, headers=UA)
    with urllib.request.urlopen(req, timeout=timeout) as r:
        rel = json.load(r)
    tag = rel.get("tag_name", "")
    if _v(tag) <= _v(VERSION) and not force:
        return None
    assets = rel.get("assets", [])
    for a in [a for a in assets if ASSET.search(a["name"])] or [a for a in assets if OLD_ASSET.search(a["name"])]:
        return {"version": tag.lstrip("v"), "notes": (rel.get("body") or "").strip()[:2000],
                "url": a["browser_download_url"], "size": a.get("size", 0)}
    return None


def download(info, progress=lambda done, total: None):
    dest = os.path.join(paths.sub("updates"), "FrankensteinSetup-%s.exe" % info["version"])
    if os.path.isfile(dest) and os.path.getsize(dest) == info.get("size"):
        return dest
    tmp = dest + ".part"
    req = urllib.request.Request(info["url"], headers={"User-Agent": UA["User-Agent"]})
    with urllib.request.urlopen(req, timeout=120) as r, open(tmp, "wb") as f:
        total, done = int(r.headers.get("Content-Length") or info.get("size") or 0), 0
        while True:
            chunk = r.read(1 << 16)
            if not chunk:
                break
            f.write(chunk)
            done += len(chunk)
            progress(done, total)
    os.replace(tmp, dest)
    return dest


def run_installer(setup):
    """Start the silent installer (it waits for us to close, then reopens the app)."""
    if sys.platform != "win32":
        raise OSError("Updates install on Windows only.")
    subprocess.Popen([setup, "/S", "/UPDATE"], close_fds=True,
                     creationflags=0x00000008 | 0x00000200)  # detached, new process group
