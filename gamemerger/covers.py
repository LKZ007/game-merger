"""Cover art for the library and for merges.

For each game, in order: the cover Steam already keeps on disk, then Steam's public box art
(found by store id, or by looking the game's name up in the Steam store), and finally a
cover made from the game's own .exe icon. Merges get a combined cover: the base game's art
with the merged-in game's art cut in diagonally."""

import base64
import glob
import hashlib
import io
import json
import os
import re
import urllib.parse
import urllib.request

from . import paths

# Steam store ids for games that are often installed from somewhere else (Xbox app, Epic, GOG).
KNOWN_APPIDS = {
    "cyberpunk 2077": "1091500",
    "forza horizon 5": "1551360",
    "forza horizon 4": "1293830",
    "forza motorsport": "2440510",
    "grand theft auto v": "271590",
    "grand theft auto v enhanced": "3240220",
    "schedule i": "3164500",
}
CDN = ["https://cdn.cloudflare.steamstatic.com/steam/apps/%s/library_600x900_2x.jpg",
       "https://cdn.cloudflare.steamstatic.com/steam/apps/%s/library_600x900.jpg",
       "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/%s/library_600x900.jpg"]
SEARCH = "https://store.steampowered.com/api/storesearch/?term=%s&cc=us&l=english"
UA = {"User-Agent": "Frankenstein/3.0"}
W, H = 600, 900


def _norm(name):
    name = re.sub(r"[®™©:]", "", name.lower())
    return re.sub(r"\s+", " ", name).strip()


def _get(url, timeout=15):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=timeout) as r:
        return r.read()


def _lookups():
    p = os.path.join(paths.sub("covers"), "lookups.json")
    try:
        with open(p, encoding="utf-8") as f:
            return p, json.load(f)
    except (OSError, ValueError):
        return p, {}


def search_appid(name):
    """Find a game's Steam id by name (exact title match only, so we never show the wrong game)."""
    key = _norm(name)
    if key in KNOWN_APPIDS:
        return KNOWN_APPIDS[key]
    p, cache = _lookups()
    if key in cache:
        return cache[key]
    appid = None
    try:
        items = json.loads(_get(SEARCH % urllib.parse.quote(name))).get("items", [])
        for it in items:
            if _norm(it.get("name", "")) == key:
                appid = str(it["id"])
                break
    except (OSError, ValueError):
        return None  # offline: try again next time
    cache[key] = appid
    with open(p, "w", encoding="utf-8") as f:
        json.dump(cache, f)
    return appid


def appid_for(game, allow_search=False):
    if getattr(game, "appid", ""):
        return game.appid
    recipe = getattr(game, "recipe", None)
    if recipe and recipe.steam_appids:
        return recipe.steam_appids[0]
    known = KNOWN_APPIDS.get(_norm(game.name))
    if known or not allow_search:
        return known
    return search_appid(game.name)


def _steam_cached(appid, steam_root):
    if not steam_root:
        return None
    cache = os.path.join(steam_root, "appcache", "librarycache")
    for p in (os.path.join(cache, "%s_library_600x900.jpg" % appid),
              os.path.join(cache, appid, "library_600x900.jpg")):
        if os.path.isfile(p):
            return p
    found = glob.glob(os.path.join(cache, appid, "*", "library_600x900.jpg"))
    return found[0] if found else None


def steam_cover(appid, steam_root=None, allow_download=True):
    mine = os.path.join(paths.sub("covers"), appid + ".jpg")
    if os.path.isfile(mine) and os.path.getsize(mine) > 0:
        return mine
    local = _steam_cached(appid, steam_root)
    if local:
        return local
    if not allow_download:
        return None
    for url in CDN:
        try:
            data = _get(url % appid)
        except OSError:
            continue
        if data[:3] == b"\xff\xd8\xff":  # a real JPEG, not an error page
            with open(mine, "wb") as f:
                f.write(data)
            return mine
    return None


# ---------- covers made with Pillow ----------

def _font(size):
    from PIL import ImageFont
    for name in ("segoeuib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    try:
        return ImageFont.load_default(size)
    except TypeError:
        return ImageFont.load_default()


def _wrap(draw, text, font, width):
    words, lines, line = text.split(), [], ""
    for w in words:
        test = (line + " " + w).strip()
        if draw.textlength(test, font=font) <= width or not line:
            line = test
        else:
            lines.append(line)
            line = w
    if line:
        lines.append(line)
    return lines


def _exe_icon(folder, exe_names=()):
    """The biggest icon inside the game's .exe, as a Pillow image (Windows .exe files only)."""
    try:
        from icoextract import IconExtractor
        from PIL import Image
    except ImportError:
        return None
    candidates = [os.path.join(folder, e) for e in exe_names]
    if not candidates:
        candidates = sorted(glob.glob(os.path.join(folder, "*.exe")) + glob.glob(os.path.join(folder, "*", "*.exe")),
                            key=lambda p: os.path.getsize(p), reverse=True)[:4]
    for exe in candidates:
        if not os.path.isfile(exe) or re.search(r"unins|setup|crash|redist|launcher", os.path.basename(exe), re.I):
            continue
        try:
            ico = Image.open(io.BytesIO(IconExtractor(exe).get_icon().getvalue()))
            sizes = sorted(getattr(ico, "info", {}).get("sizes", []) or [ico.size], key=lambda s: s[0] * s[1])
            ico.size = sizes[-1]
            return ico.convert("RGBA")
        except Exception:
            continue
    return None


def made_cover(name, folder=None, exe_names=()):
    """A cover for games with no box art: the game's own icon over a blurred, violet-tinted copy of it."""
    try:
        from PIL import Image, ImageDraw, ImageFilter
    except ImportError:
        return None
    key = hashlib.sha1((name + "|" + (folder or "")).encode()).hexdigest()[:16]
    out = os.path.join(paths.sub("covers"), "made-%s.png" % key)
    if os.path.isfile(out):
        return out
    img = Image.new("RGB", (W, H), (21, 13, 38))
    icon = _exe_icon(folder, exe_names) if folder else None
    if icon is not None:
        bg = icon.resize((H, H)).filter(ImageFilter.GaussianBlur(40)).crop(((H - W) // 2, 0, (H - W) // 2 + W, H))
        img.paste(bg.convert("RGB"), (0, 0))
        img = Image.blend(img, Image.new("RGB", (W, H), (21, 13, 38)), 0.45)
        big = icon.resize((300, 300), Image.LANCZOS)
        img.paste(big, ((W - 300) // 2, 230), big)
    d = ImageDraw.Draw(img)
    d.rectangle([0, 640, W, H], fill=(14, 9, 26))
    d.rectangle([0, 640, W, 646], fill=(167, 139, 250))
    font = _font(54)
    y = 680
    for line in _wrap(d, name.upper(), font, W - 80)[:3]:
        d.text((40, y), line, font=font, fill=(236, 232, 245))
        y += 64
    img.save(out)
    return out


def merged_cover(base_path, source_path, label, key):
    """Base game art with the merged-in game's art (or its name) cut in from the bottom right."""
    try:
        from PIL import Image, ImageDraw, ImageOps
    except ImportError:
        return base_path
    out = os.path.join(paths.sub("covers"), "merge-%s.png" % key)
    if os.path.isfile(out):
        return out
    base = ImageOps.fit(Image.open(base_path).convert("RGB"), (W, H)) if base_path else Image.new("RGB", (W, H), (28, 18, 50))
    if source_path:
        # the merged-in game's art fills the bottom-right half
        edge = [(W, int(H * 0.42)), (int(W * 0.12), H)]
        corner = (W, H)
        src = ImageOps.fit(Image.open(source_path).convert("RGB"), (W, H))
    else:
        # no art for it: its name goes in the top-right corner, clear of the base game's title
        edge = [(int(W * 0.22), 0), (W, int(H * 0.5))]
        corner = (W, 0)
        src = Image.new("RGB", (W, H), (46, 26, 87))
        d = ImageDraw.Draw(src)
        font = _font(44)
        y = 40
        for line in _wrap(d, label.upper(), font, 300)[:3]:
            d.text((W - 40 - d.textlength(line, font=font), y), line, font=font, fill=(236, 232, 245))
            y += 52
    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).polygon(edge + [corner], fill=255)
    base.paste(src, (0, 0), mask)
    d = ImageDraw.Draw(base)
    d.line(edge, fill=(167, 139, 250), width=8)
    cx, cy = (edge[0][0] + edge[1][0]) // 2, (edge[0][1] + edge[1][1]) // 2
    d.ellipse([cx - 44, cy - 44, cx + 44, cy + 44], fill=(167, 139, 250), outline=(22, 11, 46), width=6)
    f = _font(64)
    d.text((cx - d.textlength("×", font=f) / 2, cy - 42), "×", font=f, fill=(22, 11, 46))
    base.save(out)
    return out


def cover_path(game, steam_root=None, allow_download=True):
    appid = appid_for(game, allow_search=allow_download)
    if appid:
        found = steam_cover(appid, steam_root, allow_download)
        if found:
            return found
    recipe = getattr(game, "recipe", None)
    return made_cover(game.name, getattr(game, "path", None), recipe.exe if recipe else ())


def as_data_uri(path):
    if not path:
        return None
    mime = "image/png" if path.lower().endswith(".png") else "image/jpeg"
    with open(path, "rb") as f:
        return "data:%s;base64,%s" % (mime, base64.b64encode(f.read()).decode("ascii"))
