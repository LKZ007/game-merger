"""Frankenstein.ico and the installer's pictures (welcome.bmp, header.bmp) from the app's logo (gamemerger/web/logo/icon.png): the hooded figure holding the brain.
The small sizes are cropped closer, so the figure still reads at 16-48 px on the desktop and taskbar."""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
src = Image.open(os.path.join(HERE, "..", "gamemerger", "web", "logo", "icon.png")).convert("RGBA")
w, h = src.size


def at(n):
    crop = 0.0 if n >= 64 else 0.10  # trim the outer ring for the small sizes
    box = (int(w * crop), int(h * crop), int(w * (1 - crop)), int(h * (1 - crop)))
    return src.crop(box).resize((n, n), Image.LANCZOS)


sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
imgs = [at(n) for n in sizes]
imgs[-1].save(os.path.join(HERE, "Frankenstein.ico"), format="ICO", append_images=imgs[:-1], sizes=[(n, n) for n in sizes])

# the installer's side picture and header: the logo on the launcher's black and dark purple
bg = (8, 4, 16)
welcome = Image.new("RGB", (164, 314), bg)
logo = src.resize((150, 150), Image.LANCZOS)
welcome.paste(logo, (7, 82), logo)
welcome.save(os.path.join(HERE, "welcome.bmp"))
header = Image.new("RGB", (150, 57), bg)
small = src.resize((53, 53), Image.LANCZOS)
header.paste(small, (95, 2), small)
header.save(os.path.join(HERE, "header.bmp"))
