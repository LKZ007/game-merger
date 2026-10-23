"""Cuts levy's Schedule 1 x Hood Warfare reference (installer/art/hoodwarfare-reference.png: loading screen top left,
main menu top right, profiles below) into the art Frankenstein's Schedule I menu uses, so the screens look exactly
like it. The words and buttons that Frankenstein draws itself (the menu list, the cash and level pills, the version,
the loading bar, the profile cards) are painted out; the rest stays as it is. Each picture is widened to 16:9 with its
own edges, blurred and darkened. Run it after changing the reference:  python installer/make_hood_art.py"""

import os

import cv2
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "art", "hoodwarfare-reference.png")
OUT = os.path.join(HERE, "..", "gamemerger", "mods", "_art", "hoodwarfare")  # copied into a Hood Warfare merge's UserData/Frankenstein


def erase(img, boxes, radius=9):
    a = cv2.cvtColor(np.array(img), cv2.COLOR_RGB2BGR)
    mask = np.zeros(a.shape[:2], np.uint8)
    for x0, y0, x1, y1 in boxes:
        mask[y0:y1, x0:x1] = 255
    a = cv2.inpaint(a, mask, radius, cv2.INPAINT_TELEA)
    return Image.fromarray(cv2.cvtColor(a, cv2.COLOR_BGR2RGB))


def shade(img, x1, y0, y1, color=(10, 10, 16), feather=40):
    """Darkens the left side (0..x1, y0..y1) toward the given colour, fading out to the right and at the top and bottom,
    so the painted-out menu list sits on a dark panel like the reference's."""
    a = np.array(img).astype(np.float32)
    H, W = a.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    fx = np.clip(1 - xx / float(x1), 0, 1) ** 0.6
    fy = np.clip(np.minimum((yy - y0) / float(feather), (y1 - yy) / float(feather)), 0, 1)
    k = (0.92 * fx * fy)[..., None]
    a = a * (1 - k) + np.array(color, np.float32) * k
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))


def widen(img, left, right, top=0, bottom=0):
    """Adds the given margins by stretching the picture's own edge pixels (no mirrored copies of the words), blurred and darkened toward the outside."""
    w, h = img.size
    a = np.array(img).astype(np.float32)
    a = np.pad(a, ((top, bottom), (left, right), (0, 0)), mode="edge")
    big = Image.fromarray(a.astype(np.uint8)).filter(ImageFilter.GaussianBlur(24))
    b = np.array(big).astype(np.float32)
    H, W = b.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    dx = np.maximum(np.maximum(left - xx, xx - (left + w - 1)), 0) / max(1, max(left, right, 1))
    dy = np.maximum(np.maximum(top - yy, yy - (top + h - 1)), 0) / max(1, max(top, bottom, 1))
    dark = 1 - 0.75 * np.clip(np.maximum(dx, dy), 0, 1)
    b *= dark[..., None]
    b[top:top + h, left:left + w] = np.array(img).astype(np.float32)
    return Image.fromarray(np.clip(b, 0, 255).astype(np.uint8))


def cutout(img, size):
    """A white icon on the reference's dark card, as a white icon on a clear background."""
    g = np.array(img.convert("L").resize(size, Image.LANCZOS)).astype(np.float32)
    a = np.clip((g - 70) * 2.6, 0, 255).astype(np.uint8)
    out = np.dstack([np.full_like(a, 240)] * 3 + [a])
    return Image.fromarray(out, "RGBA")


def main():
    ref = Image.open(SRC).convert("RGB")
    os.makedirs(OUT, exist_ok=True)
    # main menu: the list, the pills and the version are Frankenstein's own; the title, city and figure stay
    menu = erase(ref.crop((770, 0, 1536, 508)), [(30, 158, 340, 468), (500, 8, 756, 54), (700, 476, 766, 504)])
    menu = shade(menu, 560, 130, 500)
    menu = widen(menu, 137, 0)
    menu.resize((1920, 1080), Image.LANCZOS).save(os.path.join(OUT, "ref-menu.jpg"), quality=92)
    # loading: only the bar is Frankenstein's (it moves); LOADING... and the tagline stay
    load = erase(ref.crop((0, 0, 766, 508)), [(172, 422, 594, 440)], 5)
    load = widen(load, 68, 69)
    load.resize((1920, 1080), Image.LANCZOS).save(os.path.join(OUT, "ref-loading.jpg"), quality=92)
    # profiles: the list and the cards are Frankenstein's; the title, PROFILES and the tagline stay
    prof = ref.crop((0, 512, 1536, 1024))
    avatar = prof.crop((442, 96, 687, 206))
    icons = [prof.crop((456, 268 + i * 28.6, 478, 290 + i * 28.6)) for i in range(4)]
    prof = erase(prof, [(30, 105, 330, 370), (430, 88, 1490, 445)], 7)
    prof = widen(prof, 0, 0, 176, 176)
    prof.resize((1920, 1080), Image.LANCZOS).save(os.path.join(OUT, "ref-profiles.jpg"), quality=92)
    avatar.resize((500, 224), Image.LANCZOS).save(os.path.join(OUT, "ref-avatar.jpg"), quality=92)
    for name, im in zip(("rep", "cash", "empire", "house"), icons):
        cutout(im, (44, 44)).save(os.path.join(OUT, "ref-icon-%s.png" % name))
    # the pills' own icons
    m = ref.crop((770, 0, 1536, 508))
    cutout(m.crop((515, 20, 543, 44)), (56, 48)).save(os.path.join(OUT, "ref-icon-money.png"))
    cutout(m.crop((620, 20, 648, 44)), (56, 48)).save(os.path.join(OUT, "ref-icon-crown.png"))


if __name__ == "__main__":
    main()
