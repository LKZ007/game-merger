"""Downloads mod frameworks from their official GitHub release pages."""

import json
import os
import re
import urllib.parse
import urllib.request

from . import paths

UA = {"User-Agent": "Frankenstein/1.0"}


def _get(url, timeout=30):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=timeout)


def latest_asset(repo, pattern):
    with _get("https://api.github.com/repos/%s/releases/latest" % repo) as r:
        release = json.load(r)
    rx = re.compile(pattern)
    for a in release.get("assets", []):
        if rx.search(a["name"]):
            return release.get("tag_name", ""), a["name"], a["browser_download_url"]
    raise LookupError("No download matching %s in the latest %s release." % (pattern, repo))


def scraped_asset(page, pattern):
    """Find the newest download link on a project's own page (for tools not hosted on GitHub)."""
    req = urllib.request.Request(page, headers=dict(UA, Referer=page))
    with urllib.request.urlopen(req, timeout=30) as r:
        html = r.read().decode("utf-8", "replace")
    m = re.search(pattern, html)
    if not m:
        raise LookupError("Couldn't find the download link on %s." % page)
    url = urllib.parse.urljoin(page, m.group(1))
    name = url.rsplit("/", 1)[-1]
    return os.path.splitext(name)[0], name, url


def fetch_framework(fw, log=lambda *_: None):
    """Return a local path to the framework's latest release archive, downloading it if needed."""
    if fw.scrape:
        tag, name, url = scraped_asset(fw.scrape["page"], fw.scrape["pattern"])
        referer = fw.scrape["page"]
    else:
        tag, name, url = latest_asset(fw.github, fw.asset)
        referer = None
    folder = os.path.join(paths.sub("downloads"), fw.id, re.sub(r"[^\w.-]", "_", tag or "latest"))
    os.makedirs(folder, exist_ok=True)
    dest = os.path.join(folder, name)
    if os.path.isfile(dest) and os.path.getsize(dest) > 0:
        log("Using cached %s %s." % (fw.name, tag))
        return dest
    log("Downloading %s %s ..." % (fw.name, tag))
    tmp = dest + ".part"
    req = urllib.request.Request(url, headers=dict(UA, Referer=referer) if referer else UA)
    with urllib.request.urlopen(req, timeout=120) as r, open(tmp, "wb") as f:
        while True:
            chunk = r.read(1 << 16)
            if not chunk:
                break
            f.write(chunk)
    os.replace(tmp, dest)
    return dest


def download_file(url, dest):
    tmp = dest + ".part"
    with _get(url) as r, open(tmp, "wb") as f:
        while True:
            chunk = r.read(1 << 16)
            if not chunk:
                break
            f.write(chunk)
    os.replace(tmp, dest)
    return dest
