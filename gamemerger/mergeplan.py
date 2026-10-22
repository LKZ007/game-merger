"""The Merge plan: before a merge is made, what the Host game becomes once the Extension game is coded onto it.

Every merge is a conversion of the Host game, still on the Host's own engine and built from the Host's own
assets (nothing is ever copied out of the Extension game). The plan covers the joined world (which Host areas
take on which Extension look, where Frankenstein builds its zone, lighting, weather, terrain and foliage), the
game modes, the HUD, loading and menus, characters and items. Every line says honestly how it's done in this
Host's engine:

  built      Frankenstein's own mod for this Host builds it, with the Host's engine and assets
  mod        a mod made for the Host does it (Frankenstein installs it, or offers a one-click pick)
  stand-in   something close stands in for it (a quest, a label, the launcher)
  can't      this Host's engine can't do it with mods yet; said plainly

The AI can write the wording of a plan when a Claude key is set, but it never decides a line's level: that
comes from what Frankenstein can really do in each Host (CAN below).
"""
import json
import os

from . import paths, playas

LEVELS = ("built", "mod", "stand-in", "can't")
SECTIONS = ("World", "Game modes", "HUD", "Loading and menus", "Characters", "Items and vehicles")

# what Frankenstein can really do in each Host, per part of the plan: (level, how it's done there)
CAN = {
    "cyberpunk2077": {
        "lighting": ("built", "Frankenstein's sandbox menu sets the weather and time of day to the Extension's look (This merge > World)."),
        "zone": ("built", "Frankenstein places the zone where you stand, built from Cyberpunk's own cars and set pieces (This merge > World)."),
        "areas": ("mod", "Restyling whole districts needs map mods made for Cyberpunk; Frankenstein offers one-click picks."),
        "terrain": ("can't", "Cyberpunk's ground, buildings and foliage can't be reshaped by Frankenstein's own mod; texture mods only."),
        "modes": ("built", "Frankenstein's own events run in Cyberpunk (sandbox menu > Races)."),
        "hud": ("built", "Frankenstein draws the Extension-style HUD over Cyberpunk's own, while you play."),
        "hud_replace": ("mod", "Replacing Cyberpunk's own HUD needs a HUD mod (one-click picks)."),
        "loading": ("stand-in", "Frankenstein shows its merged loading screen while the game starts. Cyberpunk's own loading screens need a "
                                "replacer mod (one-click picks)."),
        "menu": ("built", "Frankenstein's This merge tab is on Cyberpunk's main menu (press ~)."),
    },
    "schedule1": {
        "lighting": ("built", "Frankenstein's Schedule I mod tints the light, fog and sky toward the Extension's look."),
        "zone": ("built", "Frankenstein builds the zone where you stand, from Schedule I's own props (F8 in game)."),
        "areas": ("stand-in", "Schedule I's map is fixed; Frankenstein's zone and the story stand in for the Extension's areas."),
        "terrain": ("can't", "Schedule I's ground and buildings can't be reshaped or retextured by mods yet."),
        "modes": ("built", "Frankenstein's own modes run in Schedule I (F8 in game)."),
        "hud": ("built", "Frankenstein draws the Extension-style HUD over Schedule I's own."),
        "hud_replace": ("can't", "Schedule I's own HUD can't be swapped out yet."),
        "loading": ("built", "Frankenstein shows a merged title card when the world loads, and its panel on the main menu."),
        "menu": ("built", "Frankenstein's panel sits on Schedule I's main menu, under Continue and New game."),
    },
    "gta5": {
        "lighting": ("built", "Frankenstein's GTA V script sets the weather and time to the Extension's look."),
        "zone": ("built", "Frankenstein's GTA V script builds the zone where you stand, from GTA V's own props and cars (F10 menu)."),
        "areas": ("mod", "Map mods for GTA V (Menyoo maps, map editors) can restyle areas; Frankenstein offers picks."),
        "terrain": ("mod", "Texture and map mods can change GTA V's look; Frankenstein's own script can't reshape terrain."),
        "modes": ("stand-in", "The Extension's modes come as story quests in GTA V."),
        "hud": ("built", "Frankenstein's GTA V script shows the Extension-style notices and objective."),
        "hud_replace": ("mod", "Replacing GTA V's own HUD needs a HUD mod."),
        "loading": ("stand-in", "Frankenstein shows its merged loading screen while the game starts."),
        "menu": ("built", "F10 in game opens Frankenstein's menu."),
    },
}
GENERIC = {
    "lighting": ("mod", "A lighting or ReShade preset made for this game, if one exists."),
    "zone": ("can't", "Frankenstein has no mod of its own for this game yet, so it can't build a zone in it."),
    "areas": ("mod", "Map mods for this game, if any exist (one-click picks)."),
    "terrain": ("mod", "Texture mods for this game, if any exist."),
    "modes": ("stand-in", "The Extension's modes become quests in the merge's story."),
    "hud": ("mod", "A HUD mod for this game, if one exists."),
    "hud_replace": ("mod", "A HUD mod for this game, if one exists."),
    "loading": ("stand-in", "Frankenstein shows its merged loading screen while the game starts."),
    "menu": ("stand-in", "Frankenstein's buttons stay in the launcher for this game."),
}

# the first two full showcases: what the Extension becomes in the Host, in detail
SHOWCASE = {
    ("cyberpunk2077", "forza"): {
        "zone": "Horizon Festival site: a ring of Night City's fastest cars around a start line, with the festival's events starting there",
        "areas": ["The Badlands become the Horizon open-road country (highway sprints, dust and sun)",
                  "Westbrook and Charter Hill become the festival's showroom streets",
                  "Watson's docks become the night street-racing scene"],
        "lighting": "Festival golden hour: clear sky, late-afternoon sun",
        "terrain": "Desert, highway and city stay Cyberpunk's own; texture mods can brighten roads and foliage",
        "modes": ["Sprint: point to point across Night City", "Circuit: laps around checkpoints from the festival",
                  "Speed trap: hit the festival's line as fast as you can (stars for speed)",
                  "Drift zone: score points for sliding for 30 seconds"],
        "hud": "Forza-style speedometer: big speed, a speed bar, event name, timer, position and drift score",
        "loading": "Merged title and festival loading screen while Cyberpunk starts",
        "items": "Forza garage: the real Porsche 911 Turbo and Cyberpunk's fastest cars, one click into your garage; tuning for every car",
    },
    ("schedule1", "hoodwarfare"): {
        "zone": "The Block: a Hood Warfare street corner built from Schedule I's own props (dumpsters, fences, cars, crates) "
                "where you stand",
        "areas": ["Six gangs each run a district: Crips (Northtown), Bloods (Westville), South London Mandem (Downtown), "
                  "Yakuza (Docks), Mafia (Uptown), East London Firm (Suburbia)",
                  "Each gang's HQ is built in its district from the town's own props, with its flag",
                  "Your block becomes a Hood Warfare turf", "Handy Hank's hardware store becomes the gun shop"],
        "lighting": "Hood night: cooler light, purple haze and fog",
        "terrain": "Schedule I's land can't be made bigger; gang HQs and the Block are new places built on top of it",
        "modes": ["Gangs (F9): join one through its initiation and climb its ranks, or start your own gang on drugs, "
                  "guns or both, recruit a crew and take districts in three-wave turf wars",
                  "Rob rival dealers: knock them out and press G for their cash and product",
                  "Trade product and guns with the gangs: each pays and charges differently",
                  "Hold the block: stay on your block for three minutes to win cash",
                  "Gun run: buy the Hood Warfare arsenal at Handy Hank's"],
        "hud": "Hood Warfare HUD: cash, turf status and mode timer in Hood Warfare colours",
        "loading": "A Schedule I × Hood Warfare loading screen (both games' scenes merged, Hood tips), the merged title card, "
                   "and Creative in the game's own main menu under New Game",
        "items": "Hood Warfare's guns rebuilt on Schedule I's and More Guns': their fire rate, damage, magazine, reload, "
                 "length, size and parts (curved mag, extended mag, scope, drum); a starter kit and the gun shop at Handy Hank's",
    },
}


def _row(part, plan, can):
    level, how = can
    return {"part": part, "plan": plan, "level": level, "how": how}


def build(recipe, theme, ai_text=None):
    """{"host", "extension", "sections": [{"name", "rows": [{part, plan, level, how}]}], "by": "template" | "ai", "summary"}"""
    rid = getattr(recipe, "id", "")
    ext = (theme.from_game or theme.name or "").split(" /")[0]
    host = getattr(recipe, "name", "") or rid
    can = dict(GENERIC, **CAN.get(rid, {}))
    show = SHOWCASE.get((rid, theme.id), {})
    text = ai_text or {}

    def say(key, fallback):
        v = text.get(key) or show.get(key)
        return v if v else fallback

    areas = say("areas", ["%s's streets take on %s's look where mods allow" % (host, ext)])
    modes = say("modes", ["%s's signature mode, rebuilt in %s" % (ext, host)])
    sections = [
        {"name": "World", "rows": [
            _row("Areas", "; ".join(areas) if isinstance(areas, list) else areas, can["areas"]),
            _row("Built zone", say("zone", "A %s-style zone built from %s's own props" % (ext, host)), can["zone"]),
            _row("Lighting and weather", say("lighting", "%s's light and weather" % ext), can["lighting"]),
            _row("Terrain and foliage", say("terrain", "%s's ground and plants" % ext), can["terrain"]),
        ]},
        {"name": "Game modes", "rows": [_row("Mode", m, can["modes"]) for m in (modes if isinstance(modes, list) else [modes])]},
        {"name": "HUD", "rows": [
            _row("HUD", say("hud", "%s-style HUD" % ext), can["hud"]),
            _row("Host's own HUD", "Swapped for %s's" % ext, can["hud_replace"]),
        ]},
        {"name": "Loading and menus", "rows": [
            _row("Loading screen", say("loading", "Merged %s × %s loading screen" % (host, ext)), can["loading"]),
            _row("Main menu", "Frankenstein's merge menu inside %s" % host, can["menu"]),
        ]},
        {"name": "Characters", "rows": [
            _row("Play as", playas.settings(rid, ext)["who"].capitalize(),
                 ("built" if rid == "gta5" else "stand-in" if rid in CAN else "mod", playas.closeness(rid, ext))),
        ]},
        {"name": "Items and vehicles", "rows": [
            _row("Items", say("items", "%s's signature gear, as close %s stand-ins" % (ext, host)),
                 ("built", "Frankenstein's own mod and the mods it installs.") if rid in CAN else ("mod", "Mods made for %s." % host)),
        ]},
    ]
    counts = {lv: sum(r["level"] == lv for s in sections for r in s["rows"]) for lv in LEVELS}
    summary = "%s, rebuilt as %s on %s's own engine. %d parts built by Frankenstein, %d by mods, %d stand-ins, %d not possible yet." % (
        host, ext, host, counts["built"], counts["mod"], counts["stand-in"], counts["can't"])
    return {"host": host, "extension": ext, "sections": sections, "by": "ai" if ai_text else "template", "summary": summary,
            "showcase": bool(show)}


def world_text(recipe_id, theme_id, ext):
    """The World part of What it adds: what Frankenstein builds into the Host's world, or "" if it can't."""
    can = CAN.get(recipe_id)
    if not can:
        return ""
    show = SHOWCASE.get((recipe_id, theme_id), {})
    zone = show.get("zone") or "a %s-style zone built from the Host game's own props" % ext
    light = show.get("lighting") or "%s's light and weather" % ext
    return "Frankenstein's World: %s; %s. %s" % (zone, light.lower(), can["zone"][1])


def world_slot(recipe, theme):
    from . import recipes
    ext = (theme.from_game or theme.name or "").split(" /")[0]
    return recipes.Slot(name="World: %s's look and places" % ext, search="%s map" % ext,
                        builtin=world_text(recipe.id, theme.id, ext))


def must_match(name):
    """Only map or look mods that name the Extension game count for World, never a random hit."""
    import re
    ext = name[len("World: "):].split("'s look")[0]
    words = [w for w in re.findall(r"[a-z0-9]+", ext.lower()) if len(w) > 2]
    return "(?i)" + re.escape(" ".join(words)) if words else None


def is_world(name):
    return (name or "").startswith("World:")


def add_world(recipe):
    """Every merge's World part: lighting, weather, a built zone and map mods."""
    for t in recipe.themes:
        if not any(is_world(s.name) for s in t.slots):
            t.slots.append(world_slot(recipe, t))


def settings(recipe_id, theme_id, ext):
    """What Frankenstein's own mod reads at launch for the World and the modes."""
    show = SHOWCASE.get((recipe_id, theme_id), {})
    return {"style": theme_id, "zone": show.get("zone", "%s zone" % ext), "lighting": show.get("lighting", ""),
            "modes": show.get("modes", []), "hud": show.get("hud", ""), "loading": show.get("loading", ""),
            "title": "%s" % ext}


def cached(recipe_id, theme_id):
    p = os.path.join(paths.sub("plans"), "%s-%s.json" % (recipe_id, theme_id))
    try:
        with open(p, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def keep(recipe_id, theme_id, text):
    with open(os.path.join(paths.sub("plans"), "%s-%s.json" % (recipe_id, theme_id)), "w", encoding="utf-8") as f:
        json.dump(text, f)
