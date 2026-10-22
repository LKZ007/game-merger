"""Play as: the Extension game's character inside the Host game.

Nothing is copied out of the Extension game. The character comes from a character or model replacer mod made for
the Host game where one can be downloaded, and from Frankenstein's own mod where it has one for that game:
it picks the Host game's closest character and gives them the Extension game's way of moving. Failing both, the
part is a stand-in (the name, plus a quest in the story) with one-click picks for character mods only a person
can download. Each game says honestly how close it gets.
"""
import re

from . import recipes

# the Extension game's kind of character: (match on its name, style, who you play as, how they move)
STYLES = [
    (r"gang beasts|human fall flat|totally accurate|party animals|fall guys|wobbly",
     "wobbly", "a Gang Beasts style brawler", "wobbly walk, flops over when hit, grabs and throws"),
    (r"stick fight|stickman", "stick", "a Stick Fight fighter", "light and quick, goes flying when hit"),
    (r"\bdc\b|batman|superman|injustice|justice league|arkham|marvel|spider-?man|avengers|x-men|hero",
     "superhero", "a superhero", "super jump, fast sprint, hard to hurt"),
    (r"elden ring|dark souls|souls|skyrim|witcher|for honor|chivalry|knight|mordhau",
     "knight", "an armoured knight", "heavy, tough, slow swagger"),
    (r"call of duty|battlefield|halo|rainbow six|counter-?strike|arma|squad|ready or not|helldivers",
     "soldier", "a soldier", "tactical walk, steady aim"),
    (r"gta|grand theft|saints row|mafia|hood|schedule i|cyberpunk|sleeping dogs|yakuza",
     "gangster", "a street gangster", "gangster walk"),
    (r"forza|need for speed|the crew|gran turismo|racing", "racer", "a racing driver", "quick on foot"),
    (r"minecraft|roblox|lego", "blocky", "a blocky hero", "bouncy and quick"),
]
# games built around their characters: there, Play as is the hero part of the merge
CHARACTER_FIRST = {"wobbly", "stick", "superhero", "knight"}

# how close each Host game gets, and what Frankenstein's own mod does there
HOSTS = {
    "gta5": {"how": "Frankenstein's GTA V script swaps you into the closest GTA V character (%s) and gives them the "
                    "Extension game's moves: %s. F11 switches back to your own character (story missions want them).",
             "close": "real swap: a GTA V character with the Extension game's moves, not its exact model"},
    "cyberpunk2077": {"how": "Frankenstein's sandbox menu (press ~, This merge tab): plays as %s with the moves it can "
                             "give V: %s. V's body stays V's unless a character mod is added.",
                      "close": "stand-in: V keeps their body, the moves and name change"},
    "schedule1": {"how": "Frankenstein's Schedule I mod shows who you play as (%s) on screen, and the story treats you "
                         "as them. Their moves (%s) need a character mod.",
                  "close": "stand-in: name and story only, the body stays yours"},
}
GTA_LOOK = {"wobbly": "Pogo the Monkey", "stick": "a street runner", "superhero": "Impotent Rage", "knight": "the Juggernaut",
            "soldier": "a Marine", "gangster": "a Ballas gangster", "racer": "a motocross rider", "blocky": "a street runner",
            "": "your own character"}


def style(source_name):
    """(style, who, moves) for the Extension game, or a plain one named after it."""
    name = (source_name or "").lower()
    for pattern, key, who, moves in STYLES:
        if re.search(pattern, name):
            return key, who, moves
    return "", "%s's hero" % (source_name or "the Extension game"), "the moves it can give"


def slot_name(source_name):
    return "Play as %s's character" % (source_name or "the Extension game")


def source_of(name):
    m = re.match(r"(?i)play as (.*?)(?:'s character)?$", name or "")
    return m.group(1) if m else ""


def must_match(name):
    """Only character mods that name the Extension game (or its style) count for Play as, never a random hit."""
    src = source_of(name)
    words = [w for w in re.findall(r"[a-z0-9]+", src.lower()) if len(w) > 2 and w not in ("the", "and", "game")]
    key = style(src)[0]
    extra = {"superhero": ["batman", "superman", "spider", "hero"], "wobbly": ["gang beasts", "wobbly", "ragdoll"],
             "stick": ["stick"], "knight": ["knight", "tarnished"]}.get(key, [])
    alts = ([" ".join(words)] if words else []) + extra
    return "(?i)" + "|".join(re.escape(a) for a in alts) if alts else None


def nexus_pick(domain, name):
    """A one-click search on Nexus for this character, for when nothing can be downloaded by itself."""
    if not domain:
        return []
    import urllib.parse
    src = source_of(name) or "character"
    return [{"name": "%s character mods on Nexus" % src,
             "url": "https://www.nexusmods.com/games/%s/mods?keyword=%s" % (domain, urllib.parse.quote_plus(src))}]


def is_play_as(name):
    return (name or "").lower().startswith("play as")


def host_text(recipe_id, source_name):
    """What Frankenstein's own mod does for Play as in this game, or "" if it can't."""
    h = HOSTS.get(recipe_id)
    if not h:
        return ""
    key, who, moves = style(source_name)
    if recipe_id == "gta5":
        return h["how"] % (GTA_LOOK.get(key, GTA_LOOK[""]), moves)
    return h["how"] % (who, moves)


def closeness(recipe_id, source_name):
    h = HOSTS.get(recipe_id)
    return h["close"] if h else "only with a character mod (picks below); otherwise a stand-in in the story"


def slot(recipe, theme):
    """The Play as part every merge gets."""
    source = (theme.from_game or theme.name or "").split(" /")[0]
    key = style(source)[0]
    return recipes.Slot(name=slot_name(source), search="%s character" % source,
                        builtin=host_text(recipe.id, source), hero=key in CHARACTER_FIRST)


def add_to(recipe):
    """Give every style of this game a Play as part. When the Extension game is all about its characters it becomes the
    hero part (first, and the only hero)."""
    for t in recipe.themes:
        if any(is_play_as(s.name) for s in t.slots):
            continue
        s = slot(recipe, t)
        if s.hero:
            for other in t.slots:
                other.hero = False
            t.slots.insert(0, s)
        else:
            t.slots.append(s)


def settings(recipe_id, source_name):
    """What Frankenstein's own mod reads at launch."""
    key, who, moves = style(source_name)
    return {"style": key, "who": who, "moves": moves, "source": source_name or "", "close": closeness(recipe_id, source_name)}
