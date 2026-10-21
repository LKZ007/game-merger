"""Find games installed through Steam, Epic, GOG, the Xbox app, and folders the user added."""

import glob
import json
import os
import re
import string
from dataclasses import dataclass

try:
    import winreg
except ImportError:  # not on Windows
    winreg = None


@dataclass
class InstalledGame:
    name: str
    path: str
    source: str
    appid: str = ""
    recipe: object = None  # set when Frankenstein knows how to mod this game


def _reg_value(root, key, value):
    if winreg is None:
        return None
    for view in (0, getattr(winreg, "KEY_WOW64_32KEY", 0)):
        try:
            with winreg.OpenKey(root, key, 0, winreg.KEY_READ | view) as k:
                return winreg.QueryValueEx(k, value)[0]
        except OSError:
            continue
    return None


def _vdf_values(text, key):
    return re.findall(r'"%s"\s+"([^"]*)"' % re.escape(key), text, re.IGNORECASE)


def steam_libraries(steam_root):
    libs = [steam_root]
    vdf = os.path.join(steam_root, "steamapps", "libraryfolders.vdf")
    try:
        with open(vdf, encoding="utf-8", errors="ignore") as f:
            for p in _vdf_values(f.read(), "path"):
                p = p.replace("\\\\", "\\")
                if os.path.normcase(p) not in {os.path.normcase(x) for x in libs}:
                    libs.append(p)
    except OSError:
        pass
    return libs


def find_steam(steam_root=None):
    roots = [steam_root] if steam_root else []
    if not roots:
        if winreg is not None:
            r = _reg_value(winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath")
            r = r or _reg_value(winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\Valve\Steam", "InstallPath")
            if r:
                roots.append(r)
        roots.append(r"C:\Program Files (x86)\Steam")
    games = []
    seen = set()
    for root in roots:
        if not root or not os.path.isdir(root):
            continue
        for lib in steam_libraries(root):
            for acf in glob.glob(os.path.join(lib, "steamapps", "appmanifest_*.acf")):
                try:
                    with open(acf, encoding="utf-8", errors="ignore") as f:
                        text = f.read()
                except OSError:
                    continue
                names = _vdf_values(text, "name")
                dirs = _vdf_values(text, "installdir")
                appids = _vdf_values(text, "appid")
                if not dirs:
                    continue
                path = os.path.join(lib, "steamapps", "common", dirs[0])
                if os.path.isdir(path) and os.path.normcase(path) not in seen:
                    seen.add(os.path.normcase(path))
                    games.append(InstalledGame(names[0] if names else dirs[0], path, "Steam",
                                               appid=appids[0] if appids else ""))
    return games


def find_epic(manifest_dir=None):
    manifest_dir = manifest_dir or os.path.join(
        os.environ.get("PROGRAMDATA", r"C:\ProgramData"), "Epic", "EpicGamesLauncher", "Data", "Manifests")
    games = []
    for item in glob.glob(os.path.join(manifest_dir, "*.item")):
        try:
            with open(item, encoding="utf-8") as f:
                data = json.load(f)
        except (OSError, ValueError):
            continue
        path = data.get("InstallLocation")
        if path and os.path.isdir(path):
            games.append(InstalledGame(data.get("DisplayName") or os.path.basename(path), path, "Epic"))
    return games


def find_gog():
    games = []
    if winreg is None:
        return games
    key = r"SOFTWARE\WOW6432Node\GOG.com\Games"
    try:
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, key) as k:
            i = 0
            while True:
                try:
                    sub = winreg.EnumKey(k, i)
                except OSError:
                    break
                i += 1
                name = _reg_value(winreg.HKEY_LOCAL_MACHINE, key + "\\" + sub, "gameName")
                path = _reg_value(winreg.HKEY_LOCAL_MACHINE, key + "\\" + sub, "path")
                if path and os.path.isdir(path):
                    games.append(InstalledGame(name or os.path.basename(path), path, "GOG"))
    except OSError:
        pass
    return games


def find_xbox(drives=None):
    """Xbox app / Microsoft Store games installed to <drive>:\\XboxGames (where Forza usually lives)."""
    if drives is None:
        drives = ["%s:\\" % d for d in string.ascii_uppercase] if os.name == "nt" else []
    games = []
    for d in drives:
        root = os.path.join(d, "XboxGames")
        if not os.path.isdir(root):
            continue
        for name in sorted(os.listdir(root)):
            path = os.path.join(root, name)
            if os.path.isdir(path):
                content = os.path.join(path, "Content")
                games.append(InstalledGame(name, content if os.path.isdir(content) else path, "Xbox app"))
    return games


def find_manual(folders):
    return [InstalledGame(os.path.basename(os.path.normpath(p)) or p, p, "Added by you")
            for p in folders if os.path.isdir(p)]


def match_recipes(games, recipes):
    for g in games:
        for r in recipes:
            if (g.appid and g.appid in r.steam_appids) or r.matches_folder(g.path):
                g.recipe = r
                if g.source == "Added by you":
                    g.name = r.name
                break
    return games


def scan(recipes, manual_folders=()):
    games = []
    for finder in (find_steam, find_epic, find_gog, find_xbox):
        try:
            games.extend(finder())
        except Exception:  # one broken launcher must not hide the others
            pass
    games.extend(find_manual(manual_folders))
    unique, seen = [], set()
    for g in games:
        key = os.path.normcase(os.path.normpath(g.path))
        if key not in seen:
            seen.add(key)
            unique.append(g)
    match_recipes(unique, recipes)
    unique.sort(key=lambda g: (g.recipe is None, g.name.lower()))
    return unique
