"""Any game as the base: work out its engine from the install folder and build a recipe for it on the fly.

Hand-made recipes (Cyberpunk, Schedule I, GTA V) know each game in detail. For everything else this
looks at the files to tell the engine apart, picks the mod loader that engine uses, and offers every
other game you have as a style to bring in. The AI fills in what to look for, in the background.
Where an engine has no real mod support, the game can still be picked, and the merge says so."""

import glob
import hashlib
import json
import os
import re

from . import minecraft, paths
from .recipes import Slot, Theme, _parse

SKIP_EXE = re.compile(r"(unins|crash|setup|redist|vc_?redist|dxsetup|directx|launcherhelper|easyanticheat|eac_|"
                      r"battleye|be_service|unitycrashhandler|ue4prereq|dotnet|cefprocess|helper|report|updater)", re.I)

BEPINEX = {"id": "bepinex", "name": "BepInEx (mod loader for Unity games)", "github": "BepInEx/BepInEx",
           "asset": r"(?i)BepInEx_win_x64_5\.[\d.]+\.zip$"}
MELONLOADER = {"id": "melonloader", "name": "MelonLoader (mod loader)", "github": "LavaGang/MelonLoader",
               "asset": r"(?i)melonloader\.x64\.zip$"}
BETHESDA_DATA = ["meshes", "textures", "scripts", "interface", "sound", "music", "seq", "strings", "materials",
                 "skse", "f4se", "sfse", "obse", "nvse", "fose"]


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", (text or "").lower()).strip("-")[:40] or "game"


def nexus_guess(name):
    """Nexus Mods' short name for a game is usually its title squashed together."""
    name = re.sub(r"(?i)\b(the|game of the year|goty|definitive|enhanced|edition|remastered)\b", " ", name)
    name = re.sub(r"(?i):.*$", "", name) if len(re.sub(r"[^a-z0-9]", "", name.lower())) > 24 else name
    return re.sub(r"[^a-z0-9]", "", name.lower())


def main_exe(folder):
    """The game's own .exe in its top folder (or bin folder): the biggest one that isn't a helper."""
    found = []
    for pattern in ("*.exe", "bin/*.exe", "Binaries/Win64/*.exe", "x64/*.exe", "bin/x64/*.exe"):
        for p in glob.glob(os.path.join(folder, pattern)):
            if not SKIP_EXE.search(os.path.basename(p)):
                try:
                    found.append((os.path.getsize(p), os.path.relpath(p, folder)))
                except OSError:
                    pass
    if not found:
        return []
    found.sort(reverse=True)
    return [found[0][1]] + [f for _, f in found[1:3]]


def detect(folder):
    """{"engine", "label", "moddable": "yes"|"limited"|"no", "frameworks", "layout", "logs", "note"}."""
    def has(*parts):
        return glob.glob(os.path.join(folder, *parts))

    if minecraft.is_minecraft(folder):
        return {"engine": "minecraft", "label": "Minecraft: Java Edition", "moddable": "yes", "frameworks": [],
                "layout": {"root_markers": ["mods", "config"], "loose_rules": [{"ext": [".jar"], "dest": "mods"}]},
                "logs": ["logs/latest.log", "crash-reports/*.txt"],
                "note": "Minecraft: each merge is its own launcher profile with Fabric, Quilt, Forge or NeoForge, "
                        "and mods from Modrinth or CurseForge."}

    data_dirs = [d for d in has("*_Data") if os.path.isdir(d)]
    if data_dirs and (has("UnityPlayer.dll") or has("*_Data", "globalgamemanagers") or has("*_Data", "data.unity3d")):
        if has("GameAssembly.dll"):
            return {"engine": "unity-il2cpp", "label": "Unity (IL2CPP)", "moddable": "yes",
                    "frameworks": [MELONLOADER],
                    "layout": {"root_markers": ["Mods", "Plugins", "UserLibs", "UserData", "MelonLoader"],
                               "loose_rules": [{"ext": [".dll"], "dest": "Mods"}, {"ext": [".cfg", ".json"], "dest": "UserData"}]},
                    "logs": ["MelonLoader/Latest.log"], "note": "Unity game: mods load through MelonLoader."}
        return {"engine": "unity-mono", "label": "Unity", "moddable": "yes", "frameworks": [BEPINEX],
                "layout": {"root_markers": ["BepInEx"],
                           "loose_rules": [{"ext": [".dll"], "dest": "BepInEx/plugins"}, {"ext": [".cfg"], "dest": "BepInEx/config"}]},
                "logs": ["BepInEx/LogOutput.log"], "note": "Unity game: mods load through BepInEx."}

    paks = has("*", "Content", "Paks")
    if paks and os.path.isdir(paks[0]):
        project = os.path.basename(os.path.dirname(os.path.dirname(paks[0])))
        mods = "%s/Content/Paks/~mods" % project
        return {"engine": "unreal", "label": "Unreal Engine", "moddable": "yes", "frameworks": [],
                "layout": {"root_markers": [project], "loose_rules": [{"ext": [".pak", ".utoc", ".ucas", ".sig"], "dest": mods}]},
                "logs": ["%s/Saved/Logs/*.log" % project],
                "note": "Unreal Engine game: most mods are .pak files that go in ~mods. Script mods may also need UE4SS."}

    if has("Data", "*.esm"):
        return {"engine": "bethesda", "label": "Bethesda Creation Engine", "moddable": "yes", "frameworks": [],
                "layout": {"root_markers": ["Data"] + BETHESDA_DATA, "root_prefix": "Data",
                           "loose_rules": [{"ext": [".esp", ".esm", ".esl", ".bsa", ".ba2"], "dest": "Data"}]},
                "logs": [], "note": "Bethesda game: plugins (.esp/.esm) and their files go in Data. Some mods also need a script extender."}

    gameinfo = has("*", "gameinfo.txt")
    if gameinfo:
        gamedir = os.path.basename(os.path.dirname(gameinfo[0]))
        return {"engine": "source", "label": "Source engine", "moddable": "limited", "frameworks": [],
                "layout": {"root_markers": [gamedir], "loose_rules": [{"ext": [".vpk"], "dest": "%s/custom" % gamedir}]},
                "logs": [], "note": "Source engine game: .vpk mods go in the custom folder; some games use their own addons system."}

    if has("*.pck"):
        return {"engine": "godot", "label": "Godot", "moddable": "limited", "frameworks": [], "layout": {}, "logs": [],
                "note": "Godot game: modding depends on the game; usually it's swapping or adding .pck files by hand."}
    if has("data.win"):
        return {"engine": "gamemaker", "label": "GameMaker", "moddable": "limited", "frameworks": [], "layout": {}, "logs": [],
                "note": "GameMaker game: mods usually patch data.win with their own installers."}
    if has("www", "js", "rpg_core.js") or has("js", "rmmz_core.js") or has("Game.rgss3a"):
        return {"engine": "rpgmaker", "label": "RPG Maker", "moddable": "limited", "frameworks": [], "layout": {}, "logs": [],
                "note": "RPG Maker game: plugin mods go in js/plugins, but each one is wired up by hand."}
    return {"engine": "unknown", "label": "Unknown engine", "moddable": "no", "frameworks": [], "layout": {}, "logs": [],
            "note": "Frankenstein can't tell how this game is modded, so it can only copy the game and drop files in where you say."}


def sandbox_for(eng):
    """What creative mode can offer on this engine, honestly."""
    e = eng["engine"]
    if e.startswith("unity"):
        return {"note": "Free camera, an object explorer that can change values (money, health, items), plus any cheat mods.",
                "slots": [{"name": "Free camera and object tools", "search": "UnityExplorer"},
                          {"name": "Cheat / sandbox menu", "search": "cheat"}]}
    if e == "unreal":
        return {"note": "Unreal games often hide a developer console; a console unlocker or cheat mod opens it up.",
                "slots": [{"name": "Console unlocker", "search": "console unlocker"}, {"name": "Cheat / trainer", "search": "cheat"}]}
    if e == "bethesda":
        return {"native": "The game's own console: press ~ (tgm = god mode, tcl = fly through walls, player.additem to add items).",
                "note": "Using the console can switch off achievements in some Bethesda games.",
                "slots": [{"name": "Spawner / cheat menu", "search": "cheat room"}]}
    if e == "source":
        return {"native": "The developer console: turn it on in Options, press ~, type sv_cheats 1, then god, noclip or give.",
                "note": "Works on your own (local) games only.", "slots": []}
    if e == "minecraft":
        return {"native": "Minecraft's own Creative mode: pick Creative when you make a world (or /gamemode creative with cheats on).",
                "note": "Building tools make it even better.", "slots": [{"name": "Building tools", "search": "WorldEdit"}]}
    if eng.get("moddable") == "limited":
        return {"note": "Limited: only if someone has made a trainer for this game.",
                "slots": [{"name": "Trainer / cheats", "search": "trainer"}]}
    return {"note": "Frankenstein can't add sandbox tools to this game, but Creative still gets its own save slot "
                    "and you can add a trainer file yourself.",
            "slots": [{"name": "Trainer / cheats", "search": "trainer"}]}


def guess_saves(folder, eng):
    """Where the game probably keeps its saves, so a merge can have its own. [] when unsure."""
    if eng["engine"].startswith("unity"):
        for info in glob.glob(os.path.join(folder, "*_Data", "app.info")):
            try:
                with open(info, encoding="utf-8", errors="ignore") as f:
                    company, product = [x.strip() for x in f.read().splitlines()[:2]]
            except (OSError, ValueError):
                continue
            if company and product:
                return ["{LocalLow}/%s/%s" % (company, product)]
    if eng["engine"] == "unreal":
        project = eng["layout"]["root_markers"][0]
        return ["%%LOCALAPPDATA%%/%s/Saved/SaveGames" % project]
    return []


def _store():
    return os.path.join(paths.sub("auto-recipes"), "styles.json")


def _styles():
    try:
        with open(_store(), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def set_theme(rid, tid, description, slots):
    """Keep what the AI worked out for one style (description and mod searches)."""
    data = _styles()
    data["%s/%s" % (rid, tid)] = {"description": description, "slots": slots}
    with open(_store(), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)


def style_themes(rid, base_name, styles, frameworks):
    """One theme per other game: bring that game's style into the base game with the base game's mods."""
    saved = _styles()
    out = []
    for name in styles:
        tid = "style-" + slug(name)
        got = saved.get("%s/%s" % (rid, tid))
        t = Theme(id=tid, name="%s style" % name, from_game=name, frameworks=list(frameworks),
                  source_match=[name.lower()],
                  description=got["description"] if got else
                  "Bring %s's feel into %s with %s mods. %s's own files are never copied." % (name, base_name, base_name, name),
                  slots=[Slot(**x) for x in got["slots"]] if got else [Slot("%s-style mods" % name, name)])
        t.filled = bool(got)
        out.append(t)
    return out


def extend(recipe, styles):
    """Built-in recipes keep their hand-made styles and get every other game as a style too."""
    recipe.themes = [t for t in recipe.themes if not t.id.startswith("style-")]
    for t in recipe.themes:
        t.filled = True
    covered = [m for t in recipe.themes for m in t.source_match]
    fws = list(dict.fromkeys(f for t in recipe.themes for f in t.frameworks))
    extra = [n for n in styles if n.lower() != recipe.name.lower() and not any(m in n.lower() for m in covered)]
    recipe.themes += style_themes(recipe.id, recipe.name, extra, fws)
    return recipe


def _file(rid):
    return os.path.join(paths.sub("auto-recipes"), rid + ".json")


def build(game, styles, nexus_domain=None):
    """A recipe for `game` (detect.InstalledGame) with one style per other game name in `styles`.
    Saved so merges made from it keep working after the game is uninstalled or renamed."""
    eng = detect(game.path)
    rid = "auto-" + slug(game.name) + "-" + hashlib.sha1(os.path.normcase(game.path).encode()).hexdigest()[:6]
    warning = "" if eng["moddable"] == "yes" else eng["note"]
    exe = main_exe(game.path)
    data = {"id": rid, "name": game.name, "exe": exe, "steam_appids": [game.appid] if game.appid else [],
            "nexus_domain": nexus_domain or nexus_guess(game.name), "layout": eng["layout"],
            "frameworks": eng["frameworks"], "themes": [], "warning": warning, "logs": eng["logs"],
            "saves": guess_saves(game.path, eng), "sandbox": sandbox_for(eng), "processes": [os.path.basename(e) for e in exe[:1]],
            "auto": {"engine": eng["label"], "moddable": eng["moddable"], "note": eng["note"], "path": game.path}}
    with open(_file(rid), "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1)
    r = parse(data)
    r.themes = style_themes(rid, game.name, [n for n in styles if n.lower() != game.name.lower()],
                            [f["id"] for f in eng["frameworks"]])
    return r


def parse(data):
    r = _parse(data)
    r.auto = data.get("auto")
    return r


def load_all(styles=()):
    """Every recipe made so far (merges keep working even if the game is no longer found)."""
    out = []
    folder = paths.sub("auto-recipes")
    for name in os.listdir(folder):
        if name.endswith(".json") and name != "styles.json":
            try:
                with open(os.path.join(folder, name), encoding="utf-8") as f:
                    r = parse(json.load(f))
            except (OSError, ValueError, KeyError, TypeError):
                continue
            r.themes = style_themes(r.id, r.name, [n for n in styles if n.lower() != r.name.lower()],
                                    list(r.frameworks))
            out.append(r)
    return out
