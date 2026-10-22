"""Carry over my save: the Extension game's character, rebuilt in the Host game.

Frankenstein reads the Extension's save (read only: the original save is never changed) and makes a character
card: name, money, level, stats, items and abilities. Each ability becomes the closest power the Host's engine
can really give, coded in Frankenstein's own mod for that Host. What can be read depends on the game:

  Schedule I        plain save files: name, cash, bank, rank and XP are read
  Cyberpunk 2077    the save's readable summary (metadata JSON): level, street cred, attributes, life path;
                    cyberware sits in the packed save, so it's picked by hand
  everything else   locked or packed saves: the card is filled in by hand
"""
import glob
import json
import os
import re

# where each game keeps its saves (Windows), newest first is picked
SAVES = {
    "schedule1": [r"%USERPROFILE%\AppData\LocalLow\TVGS\Schedule I\Saves\*\SaveGame_*"],
    "cyberpunk2077": [r"%USERPROFILE%\Saved Games\CD Projekt Red\Cyberpunk 2077\*"],
}
# names the Extension game goes by, to find its save rules
MATCH = [(r"schedule", "schedule1"), (r"cyberpunk", "cyberpunk2077")]

# the abilities a character can bring, and what they become in each Host: (power id, how it's done there, level)
# power ids are what Frankenstein's own mod for the Host understands.
POWERS = {
    "slow_time": {"name": "Slow time (Sandevistan, Kerenzikov, bullet time)",
                  "gta5": ("slowmo", "F6 slows time around you", "built"),
                  "cyberpunk2077": ("slowmo", "Slow time button (press ~, This merge > Your character)", "built"),
                  "schedule1": ("", "Not possible in Schedule I yet; it's on your card", "can't")},
    "tough": {"name": "Tough (armour, Body, Vigor, cyberware plating)",
              "gta5": ("tough", "Double health and full armour that refills", "built"),
              "cyberpunk2077": ("tough", "Hard to hurt (god mode while it's on)", "built"),
              "schedule1": ("", "Schedule I has no armour or health boost to give; it's on your card", "can't")},
    "agile": {"name": "Agile (Reflexes, Dexterity, double jump)",
              "gta5": ("agile", "Super jump and faster sprint", "built"),
              "cyberpunk2077": ("", "Cyberpunk's own Reinforced Tendons (double jump) do this; it's on your card", "stand-in"),
              "schedule1": ("", "Not possible in Schedule I yet; it's on your card", "can't")},
    "stealth": {"name": "Stealth (Cool, optical camo, sneaking)",
                "gta5": ("ghost", "Police ignore you while you're on foot and calm", "built"),
                "cyberpunk2077": ("ghost", "Lose the police button (press ~, This merge > Your character)", "built"),
                "schedule1": ("", "Not possible in Schedule I yet; it's on your card", "can't")},
    "hacker": {"name": "Hacker (Intelligence, quickhacks)",
               "gta5": ("hacker", "Unlocks and starts any car you aim at", "built"),
               "cyberpunk2077": ("", "Cyberpunk's own quickhacks do this; it's on your card", "stand-in"),
               "schedule1": ("", "Not possible in Schedule I yet; it's on your card", "can't")},
    "strength": {"name": "Strength (Strength stat, gorilla arms, heavy hits)",
                 "gta5": ("strength", "Explosive punches", "built"),
                 "cyberpunk2077": ("", "Cyberpunk's own Gorilla Arms do this; it's on your card", "stand-in"),
                 "schedule1": ("strength", "A baseball bat into your inventory (once)", "stand-in")},
    "magic": {"name": "Magic (spells, incantations, sorceries)",
              "gta5": ("magic", "Fire punches and a fire trail (spells as GTA V effects)", "built"),
              "cyberpunk2077": ("", "Quickhacks stand in for spells; it's on your card", "stand-in"),
              "schedule1": ("", "Not possible in Schedule I yet; it's on your card", "can't")},
    "rich": {"name": "Money (cash, eddies, runes, bank)",
             "gta5": ("money", "Your money, as GTA V cash (once)", "built"),
             "cyberpunk2077": ("money", "Your money, as eddies (once)", "built"),
             "schedule1": ("money", "Your money, as cash (once)", "built")},
}


def game_of(name):
    n = (name or "").lower()
    for pattern, gid in MATCH:
        if re.search(pattern, n):
            return gid
    return ""


def _newest(patterns):
    found = []
    for p in patterns:
        found += [x for x in glob.glob(os.path.expandvars(p)) if os.path.isdir(x)]
    return max(found, key=os.path.getmtime) if found else None


def _walk_json(folder, limit=400):
    """Every JSON value in a save folder, flattened to {lowercase key: value} (first one wins). Read only."""
    out = {}
    n = 0
    for root, _d, files in os.walk(folder):
        for f in files:
            if not f.lower().endswith(".json") or n >= limit:
                continue
            n += 1
            try:
                with open(os.path.join(root, f), encoding="utf-8", errors="replace") as fh:
                    data = json.load(fh)
            except (OSError, ValueError):
                continue
            stack = [data]
            while stack:
                v = stack.pop()
                if isinstance(v, dict):
                    for k, x in v.items():
                        if isinstance(x, (dict, list)):
                            stack.append(x)
                        elif isinstance(x, str) and x[:1] in "{[":
                            try:
                                stack.append(json.loads(x))  # Schedule I keeps JSON inside strings
                            except ValueError:
                                pass
                        else:
                            out.setdefault(k.lower(), x)
                elif isinstance(v, list):
                    stack.extend(v[:200])
    return out


def _num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def read(source_name, roots=None):
    """The Extension game's character from its newest save: a card dict. Never writes anything."""
    gid = game_of(source_name)
    card = {"from": source_name, "read": [], "name": "", "money": 0, "level": 0, "stats": {}, "abilities": [],
            "how": "Locked or packed save: fill in your character by hand.", "save": ""}
    folder = _newest(roots or SAVES.get(gid, []))
    if not folder:
        if gid:
            card["how"] = "No %s save was found on this PC: fill in your character by hand." % source_name
        return card
    card["save"] = folder
    flat = _walk_json(folder)
    if gid == "schedule1":
        cash = sum(x for x in (_num(flat.get("cashbalance")), _num(flat.get("onlinebalance"))) if x)
        card.update(money=int(cash), level=int(_num(flat.get("rank")) or 0),
                    name=str(flat.get("organisationname") or flat.get("playername") or ""))
        card["stats"] = {k: int(_num(flat.get(k)) or 0) for k in ("xp", "tier", "networth") if _num(flat.get(k)) is not None}
        card["read"] = [k for k in ("name", "money", "level") if card[k]] + list(card["stats"])
        card["how"] = "Read from your Schedule I save (plain files): cash, bank, rank and XP."
        card["abilities"] = ["rich"] + (["tough"] if card["level"] >= 10 else [])
    elif gid == "cyberpunk2077":
        stats = {}
        for key, label in (("strength", "Body"), ("reflexes", "Reflexes"), ("technicalability", "Technical"),
                           ("intelligence", "Intelligence"), ("cool", "Cool")):
            v = _num(flat.get(key))
            if v is not None:
                stats[label] = int(v)
        card.update(level=int(_num(flat.get("level")) or 0), stats=stats,
                    name=str(flat.get("name") or flat.get("lifepath") or "V"))
        if _num(flat.get("streetcred")) is not None:
            stats["Street cred"] = int(_num(flat.get("streetcred")))
        card["read"] = [k for k in ("level",) if card[k]] + list(stats)
        card["how"] = ("Read from your Cyberpunk save's summary: level, street cred and attributes. Cyberware and money "
                       "are inside the packed save, so tick those by hand.")
        top = {"Body": "tough", "Reflexes": "agile", "Technical": "strength", "Intelligence": "hacker", "Cool": "stealth"}
        card["abilities"] = [top[k] for k, v in sorted(stats.items(), key=lambda x: -x[1]) if k in top and v >= 6][:3]
    return card


def powers_for(host_id, abilities):
    """What each ability becomes in this Host: [{id, name, power, how, level}]."""
    out = []
    for a in abilities:
        p = POWERS.get(a)
        if not p:
            continue
        power, how, level = p.get(host_id) or ("", "Frankenstein has no mod of its own for this game yet: story stand-in", "stand-in")
        out.append({"id": a, "name": p["name"], "power": power, "how": how, "level": level})
    return out


def settings(host_id, card):
    """What Frankenstein's own mod reads at launch. "id" changes whenever the card does, so money and items are
    given once per card, never again on every launch."""
    if not card:
        return {}
    ps = powers_for(host_id, card.get("abilities", []))
    return {"id": str(card.get("saved", 0)), "name": card.get("name", ""), "from": card.get("from", ""),
            "money": int(card.get("money") or 0), "level": int(card.get("level") or 0),
            "powers": [p["power"] for p in ps if p["power"]], "card": ["%s: %s" % (p["name"].split(" (")[0], p["how"]) for p in ps]}
