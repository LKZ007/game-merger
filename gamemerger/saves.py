"""Each merge keeps its own save games.

When you press Play on a merge, the game's normal save folder is moved aside and the
merge's own saves are put in its place. When the game closes they are swapped back, so
the original game never sees the merge's saves (and modded saves can't break it).
If the PC crashes mid-game, the swap is undone the next time Frankenstein starts."""

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


# ---------- keeping saves safe ----------
# Before every play the merge's saves are snapshotted (last few kept). A save the game would call
# "damaged" (cut off half way, empty, broken metadata) is swapped for its last good copy, and the
# broken one is kept aside, never deleted. The player's own saves get a one-time backup too.

KEEP_SNAPSHOTS = 4


def _snap_root(slot_id):
    return os.path.join(paths.sub("save-snapshots"), slot_id)


def snapshot(slot_id):
    src = slot_root(slot_id)
    if not os.path.isdir(src) or not os.listdir(src):
        return None
    root = _snap_root(slot_id)
    dest = os.path.join(root, time.strftime("%Y%m%d-%H%M%S"))
    if os.path.exists(dest):
        return dest
    shutil.copytree(src, dest)
    for old in sorted(os.listdir(root))[:-KEEP_SNAPSHOTS]:
        shutil.rmtree(os.path.join(root, old), ignore_errors=True)
    return dest


def _mirror_dir(recipe_id, i):
    return os.path.join(paths.sub("original-saves-backup"), recipe_id, str(i))


def _files(root):
    out = {}
    for dirpath, _dirs, files in os.walk(root):
        for f in files:
            p = os.path.join(dirpath, f)
            st = os.stat(p)
            out[os.path.relpath(p, root)] = (st.st_size, int(st.st_mtime))
    return out


def backup_originals(recipe):
    """Mirror the player's own saves (new and changed files only, so it's quick after the first time)
    before they're moved aside for a merge. check_originals() compares against this after playing."""
    for i, d in enumerate(save_dirs(recipe)):
        dest = _mirror_dir(getattr(recipe, "id", "game"), i)
        if not os.path.isdir(d):
            continue
        have, want = (_files(dest) if os.path.isdir(dest) else {}), _files(d)
        for rel, meta in want.items():
            if have.get(rel) != meta:
                os.makedirs(os.path.dirname(os.path.join(dest, rel)), exist_ok=True)
                shutil.copy2(os.path.join(d, rel), os.path.join(dest, rel))
        for rel in set(have) - set(want):
            os.remove(os.path.join(dest, rel))


def check_originals(slot_id):
    """After a session: the player's own saves must be exactly what they were before. Anything that changed
    (Steam Cloud syncing merge saves into them, a mod writing there) is set aside and the backup put back.
    Returns the files that were put back."""
    state = _last_session()
    if not state or state.get("merge_id") != slot_id:
        return []
    restored = []
    for i, d in enumerate(state.get("dirs", [])):
        mirror = _mirror_dir(state.get("recipe_id", "game"), i)
        if not os.path.isdir(mirror) or not os.path.isdir(d):
            continue
        have, want = _files(d), _files(mirror)
        aside = os.path.join(paths.sub("changed-original-saves"), time.strftime("%Y%m%d-%H%M%S"), str(i))
        for rel in set(have) | set(want):
            if have.get(rel) == want.get(rel):
                continue
            cur = os.path.join(d, rel)
            if os.path.exists(cur):
                os.makedirs(os.path.dirname(os.path.join(aside, rel)), exist_ok=True)
                shutil.move(cur, os.path.join(aside, rel))
            if rel in want:
                os.makedirs(os.path.dirname(cur), exist_ok=True)
                shutil.copy2(os.path.join(mirror, rel), cur)
            restored.append(rel)
    return restored


def _last_file():
    return os.path.join(paths.data_dir(), "last-session.json")


def _last_session():
    try:
        with open(_last_file(), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def _broken(save_dir):
    """Why the game would call this save damaged, or "" if it looks fine."""
    files = {f.name.lower(): f for f in os.scandir(save_dir) if f.is_file()}
    if "sav.dat" in files:  # REDengine (Cyberpunk 2077)
        f = files["sav.dat"]
        if f.stat().st_size < 1024:
            return "the save file is empty or cut off"
        with open(f.path, "rb") as h:
            head = h.read(4)
        if head != b"CSAV":
            return "the save file is cut off or overwritten"
        for name, meta in files.items():
            if name.startswith("metadata") and name.endswith(".json"):
                try:
                    with open(meta.path, encoding="utf-8") as h:
                        json.load(h)
                except (OSError, ValueError):
                    return "its details file is broken"
        return ""
    saves_here = [f for n, f in files.items() if n.endswith((".sav", ".save", ".dat", ".es3"))]
    if saves_here and all(f.stat().st_size == 0 for f in saves_here):
        return "the save file is empty"
    return ""


def damaged(slot_id):
    """[(relative save folder, why)] for the saves in a merge's slot that won't load."""
    out = []
    root = slot_root(slot_id)
    for dirpath, _dirs, _files in os.walk(root):
        try:
            why = _broken(dirpath)
        except OSError as e:
            why = str(e)
        if why:
            out.append((os.path.relpath(dirpath, root), why))
    return out


def repair(slot_id, log=lambda *_: None):
    """Swap each damaged save for its newest good copy. Returns {fixed: [...], set_aside: [...]}"""
    root = slot_root(slot_id)
    fixed, aside = [], []
    snaps = sorted(os.listdir(_snap_root(slot_id)), reverse=True) if os.path.isdir(_snap_root(slot_id)) else []
    for rel, why in damaged(slot_id):
        name = os.path.basename(rel)
        good = next((os.path.join(_snap_root(slot_id), sn, rel) for sn in snaps
                     if os.path.isdir(os.path.join(_snap_root(slot_id), sn, rel))
                     and not _broken(os.path.join(_snap_root(slot_id), sn, rel))), None)
        keep = os.path.join(paths.sub("damaged-saves"), slot_id, time.strftime("%Y%m%d-%H%M%S"), rel)
        os.makedirs(os.path.dirname(keep), exist_ok=True)
        shutil.move(os.path.join(root, rel), keep)  # never deleted, only set aside
        if good:
            shutil.copytree(good, os.path.join(root, rel))
            fixed.append(name)
            log("Fixed %s (%s): put back its last good copy." % (name, why))
        else:
            aside.append(name)
            log("Moved %s aside (%s). The game loads your next newest save instead." % (name, why))
    return {"fixed": fixed, "set_aside": aside}


def swap_in(merge_id, recipe):
    if active():
        swap_out()
    try:
        backup_originals(recipe)
        repair(merge_id)
        snapshot(merge_id)
    except OSError:
        pass  # never stop a game starting over a backup
    dirs = save_dirs(recipe)
    root = slot_root(merge_id)
    state = {"merge_id": merge_id, "dirs": dirs, "started": time.time(), "recipe_id": getattr(recipe, "id", "game")}
    with open(_active_file(), "w", encoding="utf-8") as f:
        json.dump(state, f)
    with open(_last_file(), "w", encoding="utf-8") as f:
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


def watch_until_closed(exe_names, on_closed, grace=300, poll=5, is_running=running, settle=6):
    """Wait for the game to start (up to `grace` seconds), then until it closes. The game has to be gone
    for `settle` checks in a row: games that restart themselves through Steam or a launcher would
    otherwise get their saves swapped out from under them mid-start (and a save cut off mid-write
    is what the game calls "damaged")."""
    def loop():
        start = time.time()
        while not is_running(exe_names):
            if time.time() - start > grace:
                break
            time.sleep(poll)
        gone = 0
        while gone < settle:
            gone = 0 if is_running(exe_names) else gone + 1
            time.sleep(poll)
        on_closed()
    t = threading.Thread(target=loop, daemon=True)
    t.start()
    return t


# ---------- cloud backup ----------
# Merge saves are mirrored into a OneDrive or Google Drive folder after every session, so a PC and a
# laptop stay in step: when the cloud copy is newer, it's used the next time you press Play.

def cloud_folders():
    """OneDrive / Google Drive folders found on this PC: [(label, path)]."""
    home = os.path.expanduser("~")
    out = []
    for env in ("OneDrive", "OneDriveConsumer", "OneDriveCommercial"):
        p = os.environ.get(env)
        if p and os.path.isdir(p) and all(p != x[1] for x in out):
            out.append(("OneDrive", p))
    for p in [os.path.join(home, "Google Drive", "My Drive"), os.path.join(home, "Google Drive")] + \
             ["%s:\\My Drive" % d for d in "GHIJKLM"]:
        if os.path.isdir(p):
            out.append(("Google Drive", p))
            break
    return out


def _newest(folder):
    newest = 0
    for root, _dirs, files in os.walk(folder):
        for f in files:
            newest = max(newest, os.path.getmtime(os.path.join(root, f)))
    return newest


def cloud_slot(cloud_root, merge_name):
    new, old = os.path.join(cloud_root, "Frankenstein saves"), os.path.join(cloud_root, "Game Merger saves")
    if os.path.isdir(old) and not os.path.exists(new):
        try:
            os.rename(old, new)  # the app was called Game Merger before
        except OSError:
            return os.path.join(old, merge_name)
    return os.path.join(new, merge_name)


def push_cloud(merge_id, cloud_root, merge_name):
    """Copy this merge's saves to the cloud folder."""
    src = slot_root(merge_id)
    if not cloud_root or not os.path.isdir(src):
        return False
    dest = cloud_slot(cloud_root, merge_name)
    tmp = dest + ".uploading"
    shutil.rmtree(tmp, ignore_errors=True)
    shutil.copytree(src, tmp)
    shutil.rmtree(dest, ignore_errors=True)
    os.replace(tmp, dest)
    return True


def pull_cloud(merge_id, cloud_root, merge_name):
    """Use the cloud copy if it's newer than the saves on this PC (played on another PC since)."""
    src = cloud_slot(cloud_root, merge_name) if cloud_root else None
    if not src or not os.path.isdir(src):
        return False
    local = slot_root(merge_id)
    if os.path.isdir(local) and _newest(local) >= _newest(src):
        return False
    shutil.rmtree(local, ignore_errors=True)
    shutil.copytree(src, local)
    return True
