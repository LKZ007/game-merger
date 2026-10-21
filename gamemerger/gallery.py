"""Screenshots from your merged games, and a gallery to look through them.

Press Ctrl+Alt+S while playing: Frankenstein saves a screenshot into the merge's gallery. For video
clips, Windows' own Game Bar (Win+Alt+R) is the best recorder; its clips show up in the gallery too."""

import base64
import datetime
import glob
import io
import os
import sys
import threading

from . import paths, saves

HOTKEY = "Ctrl+Alt+S"


def folder(name):
    from .installer import safe_name
    return os.path.join(paths.sub("gallery"), safe_name(name or "Other"))


def capture(name):
    from PIL import ImageGrab
    img = ImageGrab.grab(all_screens=False)
    d = folder(name)
    os.makedirs(d, exist_ok=True)
    out = os.path.join(d, datetime.datetime.now().strftime("%Y-%m-%d %H-%M-%S") + ".png")
    img.save(out)
    return out


def listen(get_name, on_shot):
    """Watch for the hotkey in the background (Windows)."""
    if sys.platform != "win32":
        return

    def loop():
        import ctypes
        from ctypes import wintypes
        user32 = ctypes.windll.user32
        MOD_ALT, MOD_CONTROL, MOD_NOREPEAT, WM_HOTKEY = 0x1, 0x2, 0x4000, 0x0312
        if not user32.RegisterHotKey(None, 1, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, ord("S")):
            return  # another app already uses it
        msg = wintypes.MSG()
        while user32.GetMessageW(ctypes.byref(msg), None, 0, 0) != 0:
            if msg.message == WM_HOTKEY:
                try:
                    on_shot(capture(get_name()))
                except Exception:
                    pass
    threading.Thread(target=loop, daemon=True).start()


def clips_folder():
    return os.path.join(os.path.expanduser("~"), "Videos", "Captures")


def items(limit=200):
    """Newest first: [{path, merge, kind, time}]."""
    out = []
    root = paths.sub("gallery")
    for p in glob.glob(os.path.join(root, "*", "*.png")):
        out.append({"path": p, "merge": os.path.basename(os.path.dirname(p)), "kind": "shot", "time": os.path.getmtime(p)})
    for p in glob.glob(os.path.join(clips_folder(), "*.mp4")):
        out.append({"path": p, "merge": "Game Bar clips", "kind": "clip", "time": os.path.getmtime(p)})
    out.sort(key=lambda x: -x["time"])
    return out[:limit]


def thumb(path, size=360):
    from PIL import Image
    im = Image.open(path)
    im.thumbnail((size, size))
    buf = io.BytesIO()
    im.convert("RGB").save(buf, "JPEG", quality=80)
    return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode("ascii")
