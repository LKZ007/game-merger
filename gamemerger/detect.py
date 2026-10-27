"""Find games installed through Steam, Epic, GOG, the Xbox app, the Rockstar Games Launcher, Ubisoft Connect,
and folders the user added."""

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


def _reg_subkeys(root, key):
    """(subkey name, full key) under a registry key, from both registry views."""
    if winreg is None:
        return []
    out = []
    for view in (0, getattr(winreg, "KEY_WOW64_32KEY", 0)):
        try:
            with winreg.OpenKey(root, key, 0, winreg.KEY_READ | view) as k:
                i = 0
                while True:
                    try:
                        sub = winreg.EnumKey(k, i)
                    except OSError:
                        break
                    i += 1
                    if sub not in [s for s, _ in out]:
                        out.append((sub, key + "\\" + sub))
        except OSError:
            continue
    return out


# folders the Rockstar Games Launcher keeps for itself, not games
ROCKSTAR_NOT_GAMES = {"launcher", "social club", "rockstar games launcher"}


def find_rockstar(program_dirs=None):
    """Games from the Rockstar Games Launcher (GTA V, GTA V Enhanced, Red Dead Redemption 2...): each title's
    InstallFolder in the registry under Rockstar Games, plus the launcher's default Program Files folder."""
    games, seen = [], set()

    def add(name, path):
        if path and os.path.isdir(path) and os.path.normcase(os.path.normpath(path)) not in seen:
            seen.add(os.path.normcase(os.path.normpath(path)))
            games.append(InstalledGame(name, os.path.normpath(path), "Rockstar Games Launcher"))

    if winreg is not None:
        for key in (r"SOFTWARE\WOW6432Node\Rockstar Games", r"SOFTWARE\Rockstar Games"):
            for sub, full in _reg_subkeys(winreg.HKEY_LOCAL_MACHINE, key):
                if sub.lower() not in ROCKSTAR_NOT_GAMES:
                    for value in ("InstallFolder", "InstallFolderEpic", "InstallFolderSteam", "InstallLocation"):
                        add(sub, _reg_value(winreg.HKEY_LOCAL_MACHINE, full, value))
        # the launcher also registers each game it installs with Windows' Apps list, publisher Rockstar Games
        for key in (r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
                    r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"):
            for _sub, full in _reg_subkeys(winreg.HKEY_LOCAL_MACHINE, key):
                if "rockstar" not in str(_reg_value(winreg.HKEY_LOCAL_MACHINE, full, "Publisher") or "").lower():
                    continue
                name = _reg_value(winreg.HKEY_LOCAL_MACHINE, full, "DisplayName") or ""
                if name.lower() not in ROCKSTAR_NOT_GAMES and "launcher" not in name.lower() and "social club" not in name.lower():
                    add(name, (_reg_value(winreg.HKEY_LOCAL_MACHINE, full, "InstallLocation") or "").strip('"'))
    if program_dirs is None:
        program_dirs = [os.path.join(os.environ.get(v, d), "Rockstar Games")
                        for v, d in (("PROGRAMFILES", r"C:\Program Files"), ("PROGRAMFILES(X86)", r"C:\Program Files (x86)"))]
        # games put on another drive from the launcher's install dialog
        if os.name == "nt":
            for d in string.ascii_uppercase:
                for sub in (r"Program Files\Rockstar Games", "Rockstar Games", r"Games\Rockstar Games"):
                    program_dirs.append("%s:\\%s" % (d, sub))
    for root in program_dirs:
        if not os.path.isdir(root):
            continue
        for name in sorted(os.listdir(root)):
            path = os.path.join(root, name)
            if name.lower() in ROCKSTAR_NOT_GAMES or not os.path.isdir(path):
                continue
            if any(f.lower().endswith(".exe") for f in os.listdir(path)):
                add(name, path)
    if os.name == "nt" and program_dirs is not None:
        # GTA V moved straight onto a drive or a Games folder, outside any launcher's own folder
        for d in string.ascii_uppercase:
            for sub in ("Grand Theft Auto V", "Grand Theft Auto V Enhanced", r"Games\Grand Theft Auto V", r"Games\Grand Theft Auto V Enhanced"):
                path = "%s:\\%s" % (d, sub)
                if os.path.isfile(os.path.join(path, "GTA5.exe")) or os.path.isfile(os.path.join(path, "GTA5_Enhanced.exe")):
                    add(os.path.basename(path), path)
    return games


def find_ubisoft():
    """Games from Ubisoft Connect: each install's InstallDir in the registry under Ubisoft\\Launcher\\Installs."""
    games = []
    if winreg is None:
        return games
    for key in (r"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs", r"SOFTWARE\Ubisoft\Launcher\Installs"):
        for _sub, full in _reg_subkeys(winreg.HKEY_LOCAL_MACHINE, key):
            path = _reg_value(winreg.HKEY_LOCAL_MACHINE, full, "InstallDir")
            if path and os.path.isdir(path):
                path = os.path.normpath(path)
                games.append(InstalledGame(os.path.basename(path) or path, path, "Ubisoft Connect"))
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


def find_minecraft():
    """Minecraft: Java Edition, if the Minecraft Launcher has been used on this PC."""
    from . import minecraft
    root = minecraft.default_root()
    return [InstalledGame("Minecraft: Java Edition", root, "Minecraft Launcher")] if os.path.isdir(os.path.join(root, "versions")) else []


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
    for finder in (find_steam, find_epic, find_gog, find_rockstar, find_ubisoft, find_xbox, find_minecraft):
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
