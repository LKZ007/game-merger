"""Painted art for the in-game menu and loading screen, made by Frankenstein itself (nothing copied from the games):
- a night city backdrop (one scene: dusk sky, skyline with lit windows, palms, a street glow), and
- a title lockup: the Host game's name, a cross, and the Extension game's name in its colour, with a crown.
Drawn with Pillow; Windows' own Impact font when it's there."""

import math
import os
import random

FONTS = ["impact.ttf", "Impact.ttf", "C:/Windows/Fonts/impact.ttf", "arialbd.ttf", "C:/Windows/Fonts/arialbd.ttf",
         "/usr/share/fonts/opentype/inter/InterDisplay-Black.otf", "/usr/share/fonts/opentype/inter/Inter-ExtraBold.otf",
         "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "DejaVuSans-Bold.ttf"]


def _font(size):
    from PIL import ImageFont
    for f in FONTS:
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            continue
    return ImageFont.load_default()


def backdrop(path, accent=(225, 29, 72), seed=7, size=(1920, 1080)):
    """The night city, full screen."""
    from PIL import Image, ImageDraw, ImageFilter
    W, H = size
    rnd = random.Random(seed)
    img = Image.new("RGB", size)
    d = ImageDraw.Draw(img)
    # sky: deep blue at the top, purple, then a warm orange glow on the horizon
    stops = [(0.0, (8, 10, 30)), (0.35, (34, 22, 64)), (0.55, (92, 40, 88)), (0.68, (196, 92, 70)), (0.74, (236, 150, 80))]
    for y in range(H):
        f = y / H
        for (a, ca), (b, cb) in zip(stops, stops[1:]):
            if a <= f <= b:
                t = (f - a) / (b - a)
                c = tuple(int(ca[i] + (cb[i] - ca[i]) * t) for i in range(3))
                break
        else:
            c = stops[-1][1]
        d.line([(0, y), (W, y)], fill=c)
    for _ in range(220):  # stars
        x, y = rnd.randrange(W), rnd.randrange(int(H * 0.45))
        v = rnd.randint(120, 255)
        d.point((x, y), fill=(v, v, min(255, v + 20)))
    horizon = int(H * 0.72)
    # far skyline: towers in the haze
    far = Image.new("RGBA", size, (0, 0, 0, 0))
    fd = ImageDraw.Draw(far)
    x = int(W * 0.35)
    while x < W:
        w = rnd.randint(28, 70)
        h = rnd.randint(60, 300) if rnd.random() < 0.8 else rnd.randint(300, 420)
        fd.rectangle([x, horizon - h, x + w, horizon], fill=(40, 28, 60, 255))
        for wy in range(horizon - h + 8, horizon - 6, 12):
            for wx in range(x + 5, x + w - 5, 9):
                if rnd.random() < 0.35:
                    fd.rectangle([wx, wy, wx + 3, wy + 4], fill=(255, 196, 120, 200))
        x += w + rnd.randint(2, 18)
    img.paste(far.filter(ImageFilter.GaussianBlur(1.2)), (0, 0), far)
    d = ImageDraw.Draw(img)
    # the city below: rows of low buildings getting darker toward us, lit windows and street lights
    for row, (top, col) in enumerate([(horizon - 20, (30, 22, 44)), (horizon + 40, (20, 15, 32)), (horizon + 120, (12, 10, 22))]):
        x = -20
        while x < W:
            w = rnd.randint(70, 180)
            h = rnd.randint(40, 120) + row * 30
            d.rectangle([x, min(H - 1, top - h + row * 40), x + w, H], fill=col)
            for wy in range(top - h + row * 40 + 10, min(H, top + 140), 18):
                for wx in range(x + 8, x + w - 8, 16):
                    if rnd.random() < 0.28:
                        warm = rnd.random() < 0.8
                        d.rectangle([wx, wy, wx + 6, wy + 8], fill=(255, 190, 110) if warm else (140, 190, 255))
            x += w + rnd.randint(0, 10)
    glow = Image.new("RGBA", size, (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for _ in range(40):  # street lights
        gx, gy = rnd.randrange(W), rnd.randint(horizon + 30, H - 40)
        r = rnd.randint(18, 40)
        gd.ellipse([gx - r, gy - r, gx + r, gy + r], fill=(255, 170, 80, 70))
    img.paste(glow.filter(ImageFilter.GaussianBlur(14)), (0, 0), glow.filter(ImageFilter.GaussianBlur(14)))
    d = ImageDraw.Draw(img)
    # palms against the glow
    for px in [int(W * f) for f in (0.47, 0.6, 0.71, 0.86, 0.95)]:
        base = horizon + rnd.randint(-10, 30)
        top = base - rnd.randint(170, 260)
        lean = rnd.randint(-25, 25)
        d.line([(px, base), (px + lean, top)], fill=(10, 8, 18), width=7)
        for a in range(0, 360, 36):
            ang = math.radians(a + rnd.randint(-10, 10))
            ex, ey = px + lean + math.cos(ang) * 70, top + math.sin(ang) * 30 + 22
            d.line([(px + lean, top), (ex, ey)], fill=(10, 8, 18), width=6)
    # the menu side stays dark so the list reads; a touch of the Extension's colour over the street
    shade = Image.new("RGBA", size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(shade)
    for x in range(W):
        a = int(max(0, 210 * (1 - x / (W * 0.55)))) if x < W * 0.55 else 0
        sd.line([(x, 0), (x, H)], fill=(4, 6, 10, a))
    for y in range(H - 260, H):
        sd.line([(0, y), (W, y)], fill=accent + (int(40 * (y - (H - 260)) / 260),))
    img.paste(shade, (0, 0), shade)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path, quality=90)
    return path


def title(path, host="Schedule I", extension="Hood Warfare", accent=(225, 29, 72), height=260):
    """The lockup, on a clear background: HOST (with its last word or numeral big and green) × EXTENSION in its colour."""
    from PIL import Image, ImageDraw, ImageFilter
    host_words = host.upper().split()
    big = host_words[-1] if len(host_words) > 1 and len(host_words[-1]) <= 2 else ""
    small = " ".join(host_words[:-1]) if big else host.upper()
    big = {"I": "1"}.get(big, big)
    ext = extension.upper().split()
    lines = [" ".join(ext[: (len(ext) + 1) // 2]), " ".join(ext[(len(ext) + 1) // 2:])] if len(ext) > 1 else [extension.upper(), ""]
    f_small, f_big, f_x = _font(int(height * 0.36)), _font(int(height * 0.8)), _font(int(height * 0.3))
    f_ext = _font(int(height * 0.38))
    probe = ImageDraw.Draw(Image.new("RGB", (1, 1)))
    wid = lambda t, f: probe.textbbox((0, 0), t, font=f)[2] if t else 0
    w_host = wid(small, f_small) + (wid(big, f_big) + 10 if big else 0)
    w_ext = max(wid(lines[0], f_ext), wid(lines[1], f_ext))
    W = w_host + 110 + w_ext + 40
    img = Image.new("RGBA", (W, height), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    green = (66, 186, 72)
    y_small = int(height * 0.34)
    d.text((0, y_small), small, font=f_small, fill=(236, 236, 236), stroke_width=4, stroke_fill=(0, 0, 0))
    x = wid(small, f_small) + 10
    if big:
        d.text((x, int(height * 0.06)), big, font=f_big, fill=green, stroke_width=5, stroke_fill=(0, 0, 0))
        x += wid(big, f_big)
    x += 30
    d.text((x, int(height * 0.38)), "×", font=f_x, fill=(236, 236, 236), stroke_width=3, stroke_fill=(0, 0, 0))
    x += 80
    # the Extension's name: white first line, coloured second, a small crown above
    cx = x + wid(lines[0], f_ext) // 2
    cy = int(height * 0.06)
    d.polygon([(cx - 26, cy + 30), (cx - 30, cy + 2), (cx - 12, cy + 16), (cx, cy - 4), (cx + 12, cy + 16), (cx + 30, cy + 2), (cx + 26, cy + 30)],
              fill=(240, 240, 240), outline=(0, 0, 0))
    d.text((x, int(height * 0.18)), lines[0], font=f_ext, fill=(240, 240, 240), stroke_width=5, stroke_fill=(0, 0, 0))
    if lines[1]:
        d.text((x + 12, int(height * 0.54)), lines[1], font=f_ext, fill=accent, stroke_width=5, stroke_fill=(0, 0, 0))
    shadow = img.split()[3].filter(ImageFilter.GaussianBlur(6))
    out = Image.new("RGBA", img.size, (0, 0, 0, 0))
    out.paste((0, 0, 0, 150), (6, 8), shadow)
    out.alpha_composite(img)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.save(path)
    return path
