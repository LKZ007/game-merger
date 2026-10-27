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
import urllib.error
import urllib.request

from . import paths
from .version import REPO, VERSION

UA = {"User-Agent": "Frankenstein/" + VERSION, "Accept": "application/vnd.github+json"}
ASSET = re.compile(r"FrankensteinSetup.*\.exe$", re.I)
OLD_ASSET = re.compile(r"GameMergerSetup.*\.exe$", re.I)  # releases from before the rename


def _v(tag):
    return tuple(int(x) for x in re.findall(r"\d+", tag)[:3]) or (0,)


STABLE = "https://github.com/%s/releases/latest/download/FrankensteinSetup.exe" % REPO
PAGE = "https://github.com/%s/releases/latest" % REPO


class UpdateError(Exception):
    """Every way of reaching GitHub failed. The message says why, in plain words."""


def _contexts():
    """Certificate stores to try: Windows' own (what browsers use, and what antivirus HTTPS scanning adds its
    certificate to), then certifi's bundle if it's there."""
    import ssl
    out = [ssl.create_default_context()]
    try:
        import certifi
        out.append(ssl.create_default_context(cafile=certifi.where()))
    except Exception:
        pass
    return out


def _why(e):
    text = str(getattr(e, "reason", "") or e)
    if getattr(e, "code", None) == 403:
        return "GitHub is limiting how often this PC can ask (try again in an hour)"
    if "CERTIFICATE" in text.upper():
        return "the secure connection was blocked (often antivirus HTTPS scanning)"
    if "getaddrinfo" in text or "Name or service" in text or "11001" in text:
        return "this PC couldn't find github.com (no internet, or DNS is blocked)"
    if "timed out" in text.lower():
        return "GitHub took too long to answer"
    return text[:160] or e.__class__.__name__


def _open(url, headers, timeout, tries=3):
    """urllib with retries (2 s, 4 s) and each certificate store. Returns the open response."""
    import time
    last = None
    for attempt in range(tries):
        for ctx in _contexts():
            try:
                return urllib.request.urlopen(urllib.request.Request(url, headers=headers), timeout=timeout, context=ctx)
            except urllib.error.HTTPError as e:
                if e.code in (403, 404):
                    raise  # rate limit or missing: retrying won't help
                last = e
            except Exception as e:  # network down, TLS, DNS: try the next store, then again after a pause
                last = e
        if attempt < tries - 1:
            time.sleep(2 * (attempt + 1))
    raise last


def _curl():
    """Windows 10 and 11 ship curl.exe, which goes through Windows' own network stack and proxy settings."""
    if sys.platform != "win32":
        return None
    p = os.path.join(os.environ.get("SystemRoot", r"C:\Windows"), "System32", "curl.exe")
    return p if os.path.isfile(p) else None


def _curl_run(args, timeout):
    exe = _curl()
    if not exe:
        raise UpdateError("curl.exe isn't on this PC")
    r = subprocess.run([exe, "-sSL", "--retry", "2", "--connect-timeout", "15"] + args, capture_output=True,
                       timeout=timeout, creationflags=0x08000000 if sys.platform == "win32" else 0)
    if r.returncode != 0:
        raise UpdateError((r.stderr or b"").decode("utf-8", "ignore").strip()[:160] or "curl failed (%d)" % r.returncode)
    return (r.stdout or b"").decode("utf-8", "ignore")


def _info(tag, rel=None):
    rel = rel or {}
    assets = rel.get("assets", [])
    for a in [a for a in assets if ASSET.search(a["name"])] or [a for a in assets if OLD_ASSET.search(a["name"])]:
        return {"version": tag.lstrip("v"), "notes": (rel.get("body") or "").strip()[:2000],
                "url": a["browser_download_url"], "size": a.get("size", 0)}
    if rel:
        return None
    # found without the API: the installer sits at the release's own download link
    return {"version": tag.lstrip("v"), "notes": "", "size": 0,
            "url": "https://github.com/%s/releases/download/%s/FrankensteinSetup.exe" % (REPO, tag)}


def _tag_from_page(url):
    m = re.search(r"/releases/tag/([^/?#\s]+)", url or "")
    return m.group(1) if m else ""


def latest(timeout=10, force=False):
    """{"version", "notes", "url", "size"} for the newest release, or None if there's nothing newer.
    force: the newest release even if it's this version (to repair a damaged install).
    Three ways in, so one blocked road doesn't stop updates: GitHub's API, then the releases page (it redirects to
    the newest tag, no API limit), then Windows' own curl.exe. UpdateError says why when all three fail."""
    why = []
    tag, rel = "", None
    try:
        with _open("https://api.github.com/repos/%s/releases/latest" % REPO, UA, timeout) as r:
            rel = json.load(r)
        tag = rel.get("tag_name", "")
    except Exception as e:
        why.append("GitHub's API: " + _why(e))
    if not tag:
        try:
            with _open(PAGE, {"User-Agent": UA["User-Agent"]}, timeout) as r:
                tag = _tag_from_page(r.geturl())
            rel = None
        except Exception as e:
            why.append("the releases page: " + _why(e))
    if not tag:
        try:
            tag = _tag_from_page(_curl_run(["-o", os.devnull, "-w", "%{url_effective}", PAGE], timeout + 20))
            rel = None
        except Exception as e:
            why.append("Windows' curl: " + _why(e))
    if not tag:
        raise UpdateError("Couldn't reach GitHub to check for updates: " + "; ".join(why) + ".")
    if _v(tag) <= _v(VERSION) and not force:
        return None
    return _info(tag, rel)


def download(info, progress=lambda done, total: None):
    dest = os.path.join(paths.sub("updates"), "FrankensteinSetup-%s.exe" % info["version"])
    if os.path.isfile(dest) and info.get("size") and os.path.getsize(dest) == info.get("size"):
        return dest
    tmp = dest + ".part"
    try:
        with _open(info["url"], {"User-Agent": UA["User-Agent"]}, 120) as r, open(tmp, "wb") as f:
            total, done = int(r.headers.get("Content-Length") or info.get("size") or 0), 0
            while True:
                chunk = r.read(1 << 16)
                if not chunk:
                    break
                f.write(chunk)
                done += len(chunk)
                progress(done, total)
    except Exception as e:
        try:  # the same file through Windows' own curl.exe
            _curl_run(["-o", tmp, info["url"]], 900)
        except Exception as e2:
            raise UpdateError("Couldn't download the update (%s; %s). Download it yourself: %s" % (_why(e), _why(e2), STABLE))
    if os.path.getsize(tmp) < 1 << 20:  # an error page, not an installer
        os.remove(tmp)
        raise UpdateError("The download came back broken. Download it yourself: %s" % STABLE)
    os.replace(tmp, dest)
    return dest


def run_installer(setup):
    """Start the silent installer (it waits for us to close, then reopens the app)."""
    if sys.platform != "win32":
        raise OSError("Updates install on Windows only.")
    subprocess.Popen([setup, "/S", "/UPDATE"], close_fds=True,
                     creationflags=0x00000008 | 0x00000200)  # detached, new process group
