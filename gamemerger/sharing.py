"""Share a merge with friends as one .gmerge file.

The file holds the recipe and theme the merge was made from, the mod files in the order they
were installed, and a fingerprint of the game version. Importing it rebuilds the same merge on a
friend's PC from their own copy of the base game, so game files are never shared, only mods."""

import datetime
import json
import os
import zipfile

from . import paths
from .installer import MergeError, safe_name

FORMAT = 1
EXT = ".gmerge"


def fingerprint(game_folder, exe_names):
    """Size of the game's .exe: a cheap way to notice when two players have different game versions."""
    for e in exe_names:
        p = os.path.join(game_folder, e)
        if os.path.isfile(p):
            return {"exe": e, "size": os.path.getsize(p)}
    return None


def export(merge, recipe, dest_folder, log=lambda *_: None):
    """Write <merge name>.gmerge into dest_folder and return its path."""
    installs, sources, missing = [], [], []
    for i, inst in enumerate(merge.data.get("installs", [])):
        archive = merge.kept_archive(inst)
        if not archive:
            missing.append(inst["label"])
            continue
        sources.append(archive)
        installs.append({"label": inst["label"], "file": inst["file"], "flat_root": inst.get("flat_root", False),
                         "subdir": inst.get("subdir", ""), "entry": "mods/%02d-%s" % (i, os.path.basename(archive))})
    if missing:
        raise MergeError("These mods were added before sharing existed, so Frankenstein doesn't have their files: %s. "
                         "Use Add mods on this merge to add them again, then share." % ", ".join(missing))
    info = {
        "format": FORMAT, "app": "Frankenstein",
        "name": merge.name, "recipe_id": merge.data.get("recipe_id"), "game_name": merge.data.get("game_name"),
        "theme_id": merge.data.get("theme_id"), "theme_name": merge.data.get("theme_name"),
        "source_appid": merge.data.get("source_appid", ""), "source_name": merge.data.get("source_name", ""),
        "game": fingerprint(merge.target_path, recipe.exe if recipe else []),
        "shared": datetime.datetime.now().isoformat(timespec="seconds"),
        "installs": installs,
    }
    os.makedirs(dest_folder, exist_ok=True)
    out = os.path.join(dest_folder, safe_name(merge.name) + EXT)
    tmp = out + ".part"
    with zipfile.ZipFile(tmp, "w", zipfile.ZIP_STORED, allowZip64=True) as z:  # mods are already compressed
        z.writestr("merge.json", json.dumps(info, indent=2))
        for inst, src in zip(installs, sources):
            log("Packing %s ..." % inst["label"])
            z.write(src, inst["entry"])
    os.replace(tmp, out)
    return out


def read(path):
    """The merge.json inside a .gmerge file, checked."""
    try:
        with zipfile.ZipFile(path) as z:
            info = json.loads(z.read("merge.json").decode("utf-8"))
            names = set(z.namelist())
    except (OSError, KeyError, ValueError, zipfile.BadZipFile):
        raise MergeError("That file isn't a Frankenstein share file, or it's damaged.")
    if info.get("format", 0) > FORMAT:
        raise MergeError("That merge was shared from a newer Frankenstein. Update the app, then try again.")
    for inst in info.get("installs", []):
        e = inst.get("entry", "")
        if e not in names or not e.startswith("mods/") or ".." in e:
            raise MergeError("That share file is incomplete (missing %s)." % inst.get("label", e))
    info["size"] = os.path.getsize(path)
    return info


def unpack(path, log=lambda *_: None):
    """Extract the mod files to a temp folder. Returns (info, [(install, archive_path)])."""
    info = read(path)
    folder = os.path.join(paths.sub("tmp"), "import-" + datetime.datetime.now().strftime("%H%M%S%f"))
    os.makedirs(folder)
    out = []
    with zipfile.ZipFile(path) as z:
        for inst in info["installs"]:
            order, name = os.path.basename(inst["entry"]).split("-", 1)
            os.makedirs(os.path.join(folder, order))
            dest = os.path.join(folder, order, name)
            with z.open(inst["entry"]) as src, open(dest, "wb") as f:
                while True:
                    chunk = src.read(1 << 20)
                    if not chunk:
                        break
                    f.write(chunk)
            out.append((inst, dest))
    return info, folder, out


def version_note(info, game_folder, exe_names):
    theirs, mine = info.get("game"), fingerprint(game_folder, exe_names)
    if theirs and mine and theirs.get("size") != mine.get("size"):
        return ("Your copy of %s looks like a different version from your friend's. Update the game on both PCs "
                "so the mods behave the same." % info.get("game_name", "the game"))
    return ""
