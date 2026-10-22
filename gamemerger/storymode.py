"""The merge standard: what every merge gets, and how deep it can go in each Host game.

Every merge has four layers:
  1. Gameplay brought over: host-game mods for each part of the Extension game (auto-installed).
  2. Gear: the Extension game's signature gear, made from items the Host game has (a starter kit, a shop).
  3. A story like a DLC: acts and quests that continue the Host game's world after its own start.
  4. Characters with new roles: a weapons dealer, a rival boss, a crooked cop, a fixer, a driver ...

How much of that runs inside the game depends on the host. Where Frankenstein has its own in-game loader
the story runs in the game (phone texts, quest journal, rewards, shops). Elsewhere it's a companion
questline in Frankenstein, shown in the rundown, that you tick off yourself."""

import json
import os

# step types the in-game loader understands (mods/schedule1/Story.cs)
STEP_TYPES = ("text", "earn", "cash", "takedown", "wanted", "escape", "buy", "days")
ROLES = ("Weapons dealer", "Rival boss", "Crooked cop", "Fixer", "Getaway driver", "Insider")
MAX_QUESTS = 16

DEPTH = {
    "schedule1": {"level": "in-game", "label": "In-game story",
                  "note": "Quests in the game's journal, texts from new characters, rewards and shops, through "
                          "Frankenstein's own Schedule I mod.",
                  "path": "UserData/Frankenstein/story.json", "ready": False},
    "gta5": {"level": "in-game", "label": "In-game story",
             "note": "Quests arrive as texts from the merge's characters, the objective shows on screen (F9 repeats it), "
                     "with cash and weapon rewards, through Frankenstein's own GTA V script.",
             "path": "scripts/Frankenstein/story.json", "ready": True},
    "cyberpunk2077": {"level": "in-game", "label": "In-game story",
                      "note": "Messages from the merge's characters and the objective on screen, a Story tab (press ~) "
                              "and eddie rewards, through Frankenstein's own Cyberpunk mod.",
                      "path": "bin/x64/plugins/cyber_engine_tweaks/mods/frankenstein_items/story.json", "ready": True},
}
DEFAULT = {"level": "companion", "label": "Companion story",
           "note": "Gameplay mods plus a story you follow from Frankenstein and the rundown.", "path": "", "ready": False}
LIMITED = {"level": "light", "label": "Light",
           "note": "This game has little mod support, so a merge is mostly a story to play along with.", "path": "",
           "ready": False}


def depth(recipe):
    """How deep a merge can go in this Host game."""
    if recipe is None:
        return LIMITED
    if recipe.id in DEPTH:
        return DEPTH[recipe.id]
    moddable = (getattr(recipe, "auto", None) or {}).get("moddable", "yes")
    return LIMITED if moddable != "yes" else DEFAULT


def world(target_path, recipe):
    """What the game itself says it has (written by Frankenstein's mod on the last play): items and people."""
    d = DEPTH.get(recipe.id if recipe else "", {})
    if not d.get("path"):
        return {}
    p = os.path.join(target_path, *os.path.dirname(d["path"]).split("/"), "world.json")
    try:
        with open(p, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def _int(v, lo, hi, default):
    try:
        return max(lo, min(hi, int(float(v))))
    except (TypeError, ValueError):
        return default


def clean(data, merge_name):
    """A story pack the game loader can trust, from what the AI wrote. Raises ValueError if it's unusable."""
    chars = []
    for i, c in enumerate((data.get("characters") or [])[:6]):
        chars.append({"id": "c%d" % i, "first": str(c.get("first") or "Unknown")[:20], "last": str(c.get("last") or "")[:20],
                      "role": str(c.get("role") or ROLES[i % len(ROLES)])[:40], "bio": str(c.get("bio") or "")[:300],
                      "shop_line": str(c.get("shop_line") or "Here's what I've got.")[:120],
                      "shop": [{"item": str(s.get("item"))[:40], "price": _int(s.get("price"), 1, 1000000, 500),
                                "after_quest": _int(s.get("after_quest"), -1, MAX_QUESTS, -1)}
                               for s in (c.get("shop") or [])[:6] if s.get("item")]})
    ids = {c.get("id") or "": "c%d" % i for i, c in enumerate((data.get("characters") or [])[:6])}
    ids.update({c["first"]: c["id"] for c in chars})
    quests = []
    for q in (data.get("quests") or [])[:MAX_QUESTS]:
        steps = []
        for s in (q.get("steps") or [])[:6]:
            t = str(s.get("type") or "").lower()
            if t not in STEP_TYPES or not s.get("text"):
                continue
            step = {"type": t, "text": str(s["text"])[:120], "amount": _int(s.get("amount"), 1, 10000000, 1)}
            if t == "takedown":
                step["target"] = str(s.get("target") or "any")[:40]
            if t == "buy":
                step["item"] = str(s.get("item") or "")[:40]
            if t == "text":
                step["reply"] = str(s.get("reply") or "I'm in.")[:60]
            steps.append(step)
        if not q.get("title") or not steps:
            continue
        reward = q.get("reward") or {}
        if not isinstance(reward, dict):
            reward = {"text": str(reward)}
        giver = ids.get(str(q.get("giver") or ""), chars[0]["id"] if chars else "c0")
        quests.append({"title": str(q["title"])[:60], "act": _int(q.get("act"), 1, 5, 1), "briefing": str(q.get("briefing") or "")[:400],
                       "giver": giver, "start_text": str(q.get("start_text") or q.get("briefing") or "")[:400],
                       "done_text": str(q.get("done_text") or "Nice work.")[:300], "steps": steps,
                       "objectives": [s["text"] for s in steps],
                       "reward": {"cash": _int(reward.get("cash"), 0, 1000000, 0), "xp": _int(reward.get("xp"), 0, 100000, 0),
                                  "items": [{"item": str(i.get("item"))[:40], "qty": _int(i.get("qty"), 1, 999, 1)}
                                            for i in (reward.get("items") or [])[:4] if isinstance(i, dict) and i.get("item")],
                                  "text": str(reward.get("text") or "")[:160]}})
    if not quests:
        raise ValueError("no playable quests")
    while len(chars) < 6:  # the loader always has six contacts; spare ones stay quiet
        chars.append({"id": "c%d" % len(chars), "first": ROLES[len(chars)].split()[0], "last": "", "role": ROLES[len(chars)],
                      "bio": "", "shop_line": "", "shop": []})
    kit = [str(k)[:40] for k in (data.get("kit") or [])[:8]]
    return {"version": 1, "title": str(data.get("title") or merge_name)[:80], "intro": str(data.get("intro") or "")[:600],
            "unlock_day": _int(data.get("unlock_day"), 0, 30, 1), "characters": chars, "quests": quests, "kit": kit}


def as_questline(pack):
    """The same story in the shape Frankenstein's Questlines page shows."""
    return {"title": pack["title"], "intro": pack["intro"], "story": True, "characters": pack["characters"],
            "quests": [{"title": q["title"], "briefing": q["briefing"], "objectives": q["objectives"],
                        "reward": q["reward"]["text"] or ("$%d" % q["reward"]["cash"] if q["reward"]["cash"] else ""),
                        "giver": next((c["first"] + " " + c["last"]).strip() for c in pack["characters"] if c["id"] == q["giver"])}
                       for q in pack["quests"]]}


def install(pack, target_path, recipe):
    """Write the pack where the game's loader reads it. Returns the path, or None for companion-only games."""
    d = DEPTH.get(recipe.id if recipe else "", {})
    if not d.get("path"):
        return None
    p = os.path.join(target_path, *d["path"].split("/"))
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(pack, f, indent=1)
    return p
