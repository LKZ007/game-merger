"""The minimum standard for every merge (levy, 2026-10-23): everything the Schedule I merge has, every Host game gets.
Each merge is checked against this list, and the merge card shows it as a checklist with an honest note per line.

  built     Frankenstein's own mod for this Host builds it (installed and kept up to date by itself)
  native    the Host game already does it itself, so nothing needs adding
  partial   part of it is there; the note says what's missing
  planned   not built for this Host yet; Frankenstein's own mod for it is still to come
  can't     this Host's engine can't do it with mods; said plainly

A line only counts as met when it's built or native. Schedule I is the reference, so it meets every line.
"""

MET = ("built", "native")
LEVELS = ("built", "native", "partial", "planned", "can't")

# (id, name, what the Schedule I merge does)
STANDARD = [
    ("menu", "Redesigned main menu", "The merge's own main menu over the game's: title lockup, list with Creative, cash and level, version."),
    ("loading", "Own loading screen", "The merge's own loading screen with its title, a progress bar and the tagline."),
    ("profiles", "Profiles", "A card per save with its stats and a Play button."),
    ("creative", "In-game Creative", "Creative as a per-save mode chosen from the game's own menu, starting empty and skipping the tutorial."),
    ("spawner", "Item spawner", "Every item, the merge's own included, from an in-game menu (F6)."),
    ("weapons", "Own weapons with real stats", "The Extension's weapons as items of their own: own model, icon, fire rate, damage and spread."),
    ("crosshairs", "Crosshair per weapon", "Each weapon's own crosshair, a scope for the sniper, an arc for launchers."),
    ("cars", "Own cars", "The Extension's cars with their own full bodies, spawned from the menu and kept in your saves."),
    ("npcs", "Gangs and NPCs", "The Extension's crews and named characters in the world, with their own turf."),
    ("controller", "Controller", "Everything works on a controller, with one wheel for Frankenstein's tools."),
    ("save", "Save Game", "A Save Game button in the pause menu."),
    ("debug", "Debug logs", "Each feature writes what it did to a file, so problems can be fixed without guessing."),
]

HOSTS = {
    "schedule1": {k: ("built", "") for k, _n, _w in STANDARD},
    "gta5": {
        "menu": ("can't", "GTA V's title screen can't be modded; Frankenstein's F10 menu in game has the merge's options instead."),
        "loading": ("partial", "Frankenstein shows its own loading screen while the game starts; GTA V's in-game loading screens need a replacer mod."),
        "profiles": ("partial", "Creative keeps its own saves; GTA V's story saves stay in its own Load Game screen."),
        "creative": ("built", "Shift+F10 switches to Creative and back."),
        "spawner": ("built", "Frankenstein's Spawner and F7 for every weapon."),
        "weapons": ("partial", "GTA V's own weapons are given; the Extension's own weapon models aren't built yet."),
        "crosshairs": ("native", "GTA V has its own crosshair and scope per weapon."),
        "cars": ("partial", "Any GTA V car spawns; the Extension's own car bodies aren't built yet."),
        "npcs": ("planned", "Gangs and named crews from the Extension aren't built for GTA V yet."),
        "controller": ("native", "GTA V plays on a controller itself."),
        "save": ("native", "GTA V saves from its pause menu and when you sleep in a safehouse."),
        "debug": ("partial", "The script logs to ScriptHookVDotNet's log; per-feature debug files aren't written yet."),
    },
    "cyberpunk2077": {
        "menu": ("partial", "Frankenstein's This merge tab is on the main menu (press ~); the menu itself isn't redrawn yet."),
        "loading": ("partial", "Frankenstein shows its own loading screen while the game starts; the game's own loading screens need a replacer mod."),
        "profiles": ("native", "Cyberpunk's own Load Game screen lists every save with its details."),
        "creative": ("built", "Creative with its own saves, from the This merge tab."),
        "spawner": ("built", "The sandbox menu: every item and every car into your garage."),
        "weapons": ("partial", "Cyberpunk's own weapons are given; the Extension's own weapon models aren't built yet."),
        "crosshairs": ("native", "Cyberpunk has its own crosshair and scope per weapon."),
        "cars": ("partial", "Every Cyberpunk car into your garage; the Extension's own car bodies aren't built yet."),
        "npcs": ("planned", "Gangs and named crews from the Extension aren't built for Cyberpunk yet."),
        "controller": ("native", "Cyberpunk plays on a controller itself."),
        "save": ("native", "Cyberpunk saves from its pause menu."),
        "debug": ("partial", "The sandbox menu logs to Cyberpunk Engine Tweaks' log; per-feature debug files aren't written yet."),
    },
}

# a Host Frankenstein has no mod of its own for yet
GENERIC = {
    "menu": ("planned", "Frankenstein's own mod for this game is still to come; the merge's options stay in the launcher."),
    "loading": ("partial", "Frankenstein shows its own loading screen while the game starts."),
    "profiles": ("partial", "The merge keeps its own saves; the game's own screen lists them."),
    "creative": ("partial", "Creative runs from the launcher with its own saves, with mods that add sandbox tools where they exist."),
    "spawner": ("partial", "A spawner mod for this game where one exists (one-click pick)."),
    "weapons": ("planned", "Own weapons need Frankenstein's own mod for this game."),
    "crosshairs": ("planned", "Own crosshairs need Frankenstein's own mod for this game."),
    "cars": ("planned", "Own cars need Frankenstein's own mod for this game."),
    "npcs": ("planned", "Gangs and named crews need Frankenstein's own mod for this game."),
    "controller": ("built", "Frankenstein's key mapper runs while you play when the game has no controller support."),
    "save": ("native", "The game's own save."),
    "debug": ("partial", "Frankenstein logs the merge and the mods it installed; in-game debug files need its own mod."),
}


def check(recipe_id):
    """Each line of the standard for this Host: {id, name, what, level, note, met}, plus the count met."""
    host = HOSTS.get(recipe_id or "", GENERIC)
    rows = []
    for key, name, what in STANDARD:
        level, note = host.get(key, GENERIC[key])
        rows.append({"id": key, "name": name, "what": what, "level": level, "note": note, "met": level in MET})
    return {"rows": rows, "met": sum(1 for r in rows if r["met"]), "total": len(rows)}
