"""Minecraft: Java Edition as a main game.

A Minecraft merge is its own instance: a folder with its own mods, config and worlds, plus a profile
in the Minecraft Launcher that points at it and uses the mod loader the merge picked. Your normal
Minecraft stays as it is.

  * Fabric and Quilt are set up directly (their version files are written into the launcher).
  * Forge and NeoForge are set up by running their official installer with the Java that the
    Minecraft Launcher already has."""

import glob
import json
import os
import re
import shutil
import subprocess
import sys
import time
import urllib.request

from .modsources import UA, SourceError, download

LOADERS = {"fabric": "Fabric", "quilt": "Quilt", "forge": "Forge", "neoforge": "NeoForge"}
STORE_LAUNCHER = r"shell:AppsFolder\Microsoft.4297127D64EC6_8wekyb3d8bbwe!Minecraft"
LAUNCHERS = [r"C:\Program Files (x86)\Minecraft Launcher\MinecraftLauncher.exe",
             r"C:\Program Files\Minecraft Launcher\MinecraftLauncher.exe"]


def default_root():
    return os.path.join(os.environ.get("APPDATA", os.path.expanduser("~")), ".minecraft")


def is_minecraft(folder):
    return os.path.isfile(os.path.join(folder, "launcher_profiles.json")) or (
        os.path.isdir(os.path.join(folder, "versions")) and os.path.isdir(os.path.join(folder, "assets")))


def _json(url):
    req = urllib.request.Request(url, headers=UA)
    try:
        with urllib.request.urlopen(req, timeout=20) as r:
            return json.load(r)
    except (OSError, ValueError):
        raise SourceError("Can't reach %s right now." % url.split("/")[2])


def _vkey(v):
    return [int(x) for x in re.findall(r"\d+", v)]


def pick_version(loader):
    """The newest Minecraft release this loader supports."""
    if loader == "fabric":
        return next(g["version"] for g in _json("https://meta.fabricmc.net/v2/versions/game") if g.get("stable"))
    if loader == "quilt":
        return next(g["version"] for g in _json("https://meta.quiltmc.org/v3/versions/game") if g.get("stable"))
    if loader == "forge":
        promos = _json("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json")["promos"]
        return max({k.rsplit("-", 1)[0] for k in promos}, key=_vkey)
    if loader == "neoforge":
        v = _neoforge_versions()[-1]
        major, minor = v.split(".")[:2]
        return "1.%s.%s" % (major, minor) if minor != "0" else "1.%s" % major
    raise SourceError("Unknown mod loader: %s" % loader)


def _neoforge_versions():
    data = _json("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge")
    return sorted([v for v in data.get("versions", []) if "beta" not in v], key=_vkey)


def find_java(root):
    """The Java the Minecraft Launcher downloaded for itself, or java on the PATH."""
    places = [os.path.join(root, "runtime"),
              os.path.join(os.environ.get("LOCALAPPDATA", ""), "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe",
                           "LocalCache", "Local", "runtime")]
    found = []
    for p in places:
        found += glob.glob(os.path.join(p, "*", "*", "*", "bin", "java.exe")) + glob.glob(os.path.join(p, "*", "*", "bin", "java.exe"))
    if found:
        return sorted(found)[-1]
    return shutil.which("java")


def install_loader(root, loader, game_version, log=lambda *_: None):
    """Add the loader to the Minecraft Launcher. Returns the launcher version id to play with."""
    if loader in ("fabric", "quilt"):
        base = "https://meta.fabricmc.net/v2" if loader == "fabric" else "https://meta.quiltmc.org/v3"
        lv = _json("%s/versions/loader/%s" % (base, game_version))
        if not lv:
            raise SourceError("%s doesn't support Minecraft %s yet." % (LOADERS[loader], game_version))
        profile = _json("%s/versions/loader/%s/%s/profile/json" % (base, game_version, lv[0]["loader"]["version"]))
        vid = profile["id"]
        folder = os.path.join(root, "versions", vid)
        os.makedirs(folder, exist_ok=True)
        with open(os.path.join(folder, vid + ".json"), "w", encoding="utf-8") as f:
            json.dump(profile, f, indent=2)
        log("Added %s %s for Minecraft %s." % (LOADERS[loader], lv[0]["loader"]["version"], game_version))
        return vid
    if loader == "forge":
        promos = _json("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json")["promos"]
        fv = promos.get(game_version + "-recommended") or promos.get(game_version + "-latest")
        if not fv:
            raise SourceError("Forge doesn't support Minecraft %s." % game_version)
        full = "%s-%s" % (game_version, fv)
        url = "https://maven.minecraftforge.net/net/minecraftforge/forge/%s/forge-%s-installer.jar" % (full, full)
        vid = "%s-forge-%s" % (game_version, fv)
    elif loader == "neoforge":
        prefix = ".".join(game_version.split(".")[1:3]) + "."
        if game_version.count(".") == 1:
            prefix = game_version.split(".")[1] + ".0."
        vs = [v for v in _neoforge_versions() if v.startswith(prefix)]
        if not vs:
            raise SourceError("NeoForge doesn't support Minecraft %s." % game_version)
        url = "https://maven.neoforged.net/releases/net/neoforged/neoforge/%s/neoforge-%s-installer.jar" % (vs[-1], vs[-1])
        vid = "neoforge-%s" % vs[-1]
    else:
        raise SourceError("Unknown mod loader: %s" % loader)
    if os.path.isdir(os.path.join(root, "versions", vid)):
        return vid
    java = find_java(root)
    if not java:
        raise SourceError("%s needs Java to install. Open Minecraft once from its launcher (it brings its own Java), "
                          "then try again." % LOADERS[loader])
    log("Downloading the %s installer …" % LOADERS[loader])
    jar = download(url, "minecraft")
    log("Running the %s installer (this takes a minute) …" % LOADERS[loader])
    r = subprocess.run([java, "-jar", jar, "--installClient", root], capture_output=True, text=True, timeout=900,
                       creationflags=0x08000000 if sys.platform == "win32" else 0)
    if r.returncode != 0 or not os.path.isdir(os.path.join(root, "versions", vid)):
        raise SourceError("The %s installer didn't finish. Run %s yourself and pick Install client." % (LOADERS[loader], jar))
    return vid


def _profiles_file(root):
    return os.path.join(root, "launcher_profiles.json")


def add_profile(root, key, name, game_dir, version_id):
    """A Minecraft Launcher profile for this merge, playing from its own folder."""
    p = _profiles_file(root)
    try:
        with open(p, encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, ValueError):
        data = {"profiles": {}, "settings": {}, "version": 3}
    now = time.strftime("%Y-%m-%dT%H:%M:%S.000Z", time.gmtime())
    data.setdefault("profiles", {})["frankenstein-" + key] = {
        "name": name, "type": "custom", "created": now, "lastUsed": now, "icon": "Crafting_Table",
        "lastVersionId": version_id, "gameDir": game_dir}
    with open(p, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)


def remove_profile(root, key):
    p = _profiles_file(root)
    try:
        with open(p, encoding="utf-8") as f:
            data = json.load(f)
        if data.get("profiles", {}).pop("frankenstein-" + key, None) is not None:
            with open(p, "w", encoding="utf-8") as f:
                json.dump(data, f, indent=2)
    except (OSError, ValueError):
        pass


def select_profile(root, key):
    """Make this merge's profile the launcher's most recent, so it's the one picked on Play."""
    p = _profiles_file(root)
    with open(p, encoding="utf-8") as f:
        data = json.load(f)
    prof = data.get("profiles", {}).get("frankenstein-" + key)
    if not prof:
        raise SourceError("The Minecraft Launcher profile for this merge is gone. Press Upgrade on the merge.")
    prof["lastUsed"] = time.strftime("%Y-%m-%dT%H:%M:%S.000Z", time.gmtime())
    with open(p, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)


def launch():
    for exe in LAUNCHERS:
        if os.path.isfile(exe):
            subprocess.Popen([exe])
            return True
    if sys.platform == "win32":
        subprocess.Popen(["explorer.exe", STORE_LAUNCHER])
        return True
    return False
