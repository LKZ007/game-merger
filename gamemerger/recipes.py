"""Game recipes: which games can be a base, which mod frameworks they need,
and which "merge themes" can be layered on top. Each recipe is one JSON file
in the recipes folder, so new games can be added without touching code."""

import json
import os
import sys
from dataclasses import dataclass, field


def resource_dir():
    # PyInstaller unpacks bundled data into sys._MEIPASS.
    if hasattr(sys, "_MEIPASS"):
        return os.path.join(sys._MEIPASS, "gamemerger", "recipes")
    return os.path.join(os.path.dirname(os.path.abspath(__file__)), "recipes")


@dataclass
class Framework:
    id: str
    name: str
    github: str = ""
    asset: str = ""
    manual_url: str = ""
    flat_root: bool = False  # copy the archive's files straight into the game folder
    subdir: str = ""  # with flat_root: take files from this folder inside the archive, if it has one
    scrape: dict = None  # {"page", "pattern"}: download link found on the project's own page


@dataclass
class Slot:
    name: str
    search: str
    prefer: str = ""  # a mod picked by hand for this slot (matched by name), tried before the search results
    github: str = ""  # or a tool published on its own GitHub page
    asset: str = ""
    flat_root: bool = False
    picks: list = field(default_factory=list)  # hand-picked pages ({name, url}) for mods only a person can download
    builtin: str = ""  # what Frankenstein's own mod does for this slot when no download is found


@dataclass
class Theme:
    id: str
    name: str
    from_game: str
    description: str
    frameworks: list
    slots: list
    source_match: list = field(default_factory=list)
    source_appid: str = ""  # Steam id of the merged-in game, for its cover art


@dataclass
class Recipe:
    id: str
    name: str
    exe: list
    steam_appids: list
    nexus_domain: str
    layout: dict
    frameworks: dict
    themes: list
    warning: str = ""
    logs: list = field(default_factory=list)  # crash/debug logs, relative to the game folder (globs allowed)
    saves: list = field(default_factory=list)  # save folders, see saves.expand()
    processes: list = field(default_factory=list)  # .exe names to watch while the game runs
    multiplayer: str = ""  # how to play this game's merges with friends (shown on Play together)
    launch_args: list = field(default_factory=list)  # e.g. keep modded GTA V out of GTA Online
    sandbox: dict = field(default_factory=dict)  # creative mode: {"native", "note", "slots"}

    def framework(self, fid):
        return self.frameworks[fid]

    def theme(self, tid):
        for t in self.themes:
            if t.id == tid:
                return t
        raise KeyError(tid)

    def matches_folder(self, folder):
        return any(os.path.isfile(os.path.join(folder, e)) for e in self.exe)

    def launch_exe(self, folder):
        for e in self.exe:
            p = os.path.join(folder, e)
            if os.path.isfile(p):
                return p
        return None


def _parse(data):
    fws = {f["id"]: Framework(**f) for f in data.get("frameworks", [])}
    themes = []
    for t in data.get("themes", []):
        t = dict(t)
        t["slots"] = [Slot(**s) for s in t.get("slots", [])]
        themes.append(Theme(**t))
    return Recipe(
        id=data["id"],
        name=data["name"],
        exe=data["exe"],
        steam_appids=data.get("steam_appids", []),
        nexus_domain=data.get("nexus_domain", ""),
        layout=data.get("layout", {}),
        frameworks=fws,
        themes=themes,
        warning=data.get("warning", ""),
        logs=data.get("logs", []),
        saves=data.get("saves", []),
        processes=data.get("processes", []),
        multiplayer=data.get("multiplayer", ""),
        launch_args=data.get("launch_args", []),
        sandbox=data.get("sandbox", {}),
    )


def load_recipes(folders=None):
    folders = folders or [resource_dir()]
    recipes = []
    for folder in folders:
        if not os.path.isdir(folder):
            continue
        for name in sorted(os.listdir(folder)):
            if name.endswith(".json"):
                with open(os.path.join(folder, name), encoding="utf-8") as f:
                    recipes.append(_parse(json.load(f)))
    return recipes
