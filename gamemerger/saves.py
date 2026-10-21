"""Each merge keeps its own save games.

When you press Play on a merge, the game's normal save folder is moved aside and the
merge's own saves are put in its place. When the game closes they are swapped back, so
the original game never sees the merge's saves (and modded saves can't break it).
If the PC crashes mid-game, the swap is undone the next time Game Merger starts."""

import json
import os
import shutil
import subprocess
import sys
import threading
import time

from . import paths

ORIGINAL_SUFFIX = ".gamemerger-original"


def known_folder(name):
    """Documents / Saved Games / Downloads, following OneDrive or other redirection on Windows."""
    home = os.path.expanduser("~")
    fallback = {"Documents": os.path.join(home, "Documents"),
                "SavedGames": os.path.join(home, "Saved Games"),
                "Downloads": os.path.join(home, "Downloads")}[name]
    if sys.platform != "win32":
        return fallback
    try:
        import winreg
        value = {"Documents": "Personal", "SavedGames": "{4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4}",
                 "Downloads": "{374DE290-123F-4565-9164-39C4925E467B}"}[name]
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER,
                            r"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders") as k:
            return os.path.expandvars(winreg.QueryValueEx(k, value)[0])
    except OSError:
        return fallback


def expand(p):
    p = p.replace("{Documents}", known_folder("Documents")).replace("{SavedGames}", known_folder("SavedGames"))
    p = p.replace("{LocalLow}", os.path.join(os.path.expanduser("~"), "AppData", "LocalLow"))
    return os.path.normpath(os.path.expandvars(os.path.expanduser(p)))


def save_dirs(recipe):
    return [expand(p) for p in getattr(recipe, "saves", [])]


def slot_root(merge_id):
    return os.path.join(paths.sub("saves"), merge_id)


def _active_file():
    return os.path.join(paths.data_dir(), "active-saves.json")


def active():
    try:
        with open(_active_file(), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def create_slot(merge_id, recipe, copy_current=True, log=lambda *_: None):
    """Give a new merge its own saves, starting from a copy of the player's current saves."""
    root = slot_root(merge_id)
    for i, d in enumerate(save_dirs(recipe)):
        dest = os.path.join(root, str(i))
        if os.path.exists(dest):
            continue
        if copy_current and os.path.isdir(d):
            shutil.copytree(d, dest)
            log("Copied your current saves so you can keep playing the same character.")
        else:
            os.makedirs(dest, exist_ok=True)


def swap_in(merge_id, recipe):
    if active():
        swap_out()
    dirs = save_dirs(recipe)
    root = slot_root(merge_id)
    state = {"merge_id": merge_id, "dirs": dirs, "started": time.time()}
    with open(_active_file(), "w", encoding="utf-8") as f:
        json.dump(state, f)
    for i, d in enumerate(dirs):
        slot = os.path.join(root, str(i))
        os.makedirs(slot, exist_ok=True)
        if os.path.exists(d) and not os.path.exists(d + ORIGINAL_SUFFIX):
            os.rename(d, d + ORIGINAL_SUFFIX)
        os.makedirs(os.path.dirname(d), exist_ok=True)
        shutil.move(slot, d)


def swap_out():
    state = active()
    if not state:
        return False
    root = slot_root(state["merge_id"])
    for i, d in enumerate(state["dirs"]):
        slot = os.path.join(root, str(i))
        if os.path.exists(d):
            if os.path.exists(slot):
                shutil.rmtree(slot)
            os.makedirs(root, exist_ok=True)
            shutil.move(d, slot)
        if os.path.exists(d + ORIGINAL_SUFFIX):
            os.rename(d + ORIGINAL_SUFFIX, d)
    os.remove(_active_file())
    return True


def delete_slot(merge_id):
    shutil.rmtree(slot_root(merge_id), ignore_errors=True)


def running(exe_names):
    """True while any of these .exe files is running (Windows)."""
    if sys.platform != "win32":
        return False
    try:
        out = subprocess.run(["tasklist", "/fo", "csv", "/nh"], capture_output=True, text=True,
                             creationflags=0x08000000).stdout.lower()
    except OSError:
        return False
    return any('"%s"' % n.lower() in out for n in exe_names)


def watch_until_closed(exe_names, on_closed, grace=90, poll=5, is_running=running):
    """Wait for the game to start (up to `grace` seconds), then until it closes."""
    def loop():
        start = time.time()
        while not is_running(exe_names):
            if time.time() - start > grace:
                break
            time.sleep(poll)
        while is_running(exe_names):
            time.sleep(poll)
        on_closed()
    t = threading.Thread(target=loop, daemon=True)
    t.start()
    return t
