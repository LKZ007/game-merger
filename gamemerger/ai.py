"""The AI features behind Frankenstein, on Claude with the player's own API key: fun ratings for
pairings, recipes for games without a built-in one, merge names and taglines, crash repair,
questlines and the rundown shown when a merge starts. No chat and no character; it works in the
background and its results show up as plain text in the app.

The AI only ever sees text: game names, the chosen merge, mod file names and the paths inside
them, and log files. Game files themselves are never sent."""

import glob
import json
import os
import re

MODEL = "claude-opus-5-5"
LOG_TAIL_LINES = 150

SYSTEM = """You work behind the scenes in Frankenstein, a Windows app for PC gamers (it was called Game Merger \
before). Games on different engines can't truly be merged, so Frankenstein "merges" them the realistic way: the \
player picks a base game they will actually play and a style to bring in from another game. Frankenstein makes a \
safe copy of the base game, installs the mod loaders it needs and mods the player downloads (Nexus Mods, \
CurseForge, Modrinth). Nothing is copied out of the other game. Everything can be undone.

What you write is shown in the app as plain text, so: no greetings, no persona, no slang, no swearing, no \
markdown. Be clear, short and honest. Only name a specific mod if you are confident it exists; otherwise describe \
what to search for. Never invent download links. Modded files in online modes can get accounts banned."""


class AIError(Exception):
    pass


def read_logs(game_folder, patterns, max_files=6):
    """Return [(relative path, last lines)] for the newest matching log files."""
    found = []
    for pat in patterns:
        for p in glob.glob(os.path.join(game_folder, pat)):
            if os.path.isfile(p):
                found.append(p)
    found.sort(key=os.path.getmtime, reverse=True)
    out = []
    for p in found[:max_files]:
        try:
            with open(p, encoding="utf-8", errors="replace") as f:
                lines = f.readlines()[-LOG_TAIL_LINES:]
        except OSError:
            continue
        out.append((os.path.relpath(p, game_folder), "".join(lines)))
    return out


def build_context(ctx):
    """Turn what the app knows into a short text block for the AI."""
    lines = ["[What Frankenstein knows]"]
    if ctx.get("screen"):
        lines.append("Screen: %s" % ctx["screen"])
    if ctx.get("installed_games"):
        lines.append("Installed games: %s" % ", ".join(ctx["installed_games"]))
    if ctx.get("pc"):
        lines.append("Player's PC: %s" % ctx["pc"])
    if ctx.get("base_game"):
        lines.append("Base game: %s" % ctx["base_game"])
    if ctx.get("theme"):
        lines.append("Merging in: %s. %s" % (ctx["theme"], ctx.get("theme_description", "")))
    if ctx.get("frameworks"):
        lines.append("Frameworks that will be installed: %s" % ", ".join(ctx["frameworks"]))
    for mod in ctx.get("mods", []):
        lines.append("Mod file '%s' (for: %s) installs %d files, e.g. %s"
                     % (mod["file"], mod.get("slot", "?"), len(mod.get("paths", [])),
                        ", ".join(mod.get("paths", [])[:8]) or "nothing recognised"))
    for clash in ctx.get("clashes", []):
        lines.append("Clash: %s" % clash)
    if ctx.get("merge"):
        lines.append("Selected merge: %s" % ctx["merge"])
    if ctx.get("quest"):
        lines.append("Questline they're on: %s" % ctx["quest"])
    for name, text in ctx.get("logs", []):
        lines.append("\n--- log file %s (last lines) ---\n%s" % (name, text))
    return "\n".join(lines)


def _json(text):
    m = re.search(r"\{.*\}", text or "", re.S)
    try:
        return json.loads(m.group(0)) if m else {}
    except ValueError:
        return {}


class AI:
    def __init__(self, api_key):
        if not api_key:
            raise AIError("Add a Claude API key in Settings > AI features to use this.")
        import anthropic  # imported here so the rest of the app works without it
        self._anthropic = anthropic
        self.client = anthropic.Anthropic(api_key=api_key)

    def diagnose(self, context, mods):
        """Crash repair: pick the one mod most likely to cause the crash, from the logs.
        Returns {"culprit": label or None, "why": short reason}."""
        ask = (context + "\n\nThe game crashed or won't start. These mods are switched on, in install order:\n" +
               "\n".join("- " + m for m in mods) +
               "\n\nPick the single mod most likely to be causing it, using the logs. Mod frameworks and loaders "
               "(script hooks, Cyber Engine Tweaks, MelonLoader and similar) are only the culprit if the logs clearly "
               "show it. Answer with only JSON: {\"culprit\": \"<exact mod name from the list, or null if the logs "
               "don't point at one>\", \"why\": \"<one or two plain sentences>\"}")
        text = self.ask(ask)
        m = re.search(r"\{.*\}", text, re.S)
        try:
            out = json.loads(m.group(0)) if m else {}
        except ValueError:
            out = {}
        culprit = out.get("culprit")
        return {"culprit": culprit if culprit in mods else None, "why": str(out.get("why") or text.strip())[:600]}

    def questline(self, base, theme, merge_name, mods, idea=""):
        """A questline the player can play in their merged game with what's already in it."""
        ask = ("Write a questline for my merged game \"%s\": %s with %s style mods (%s). %s"
               "It has to be playable in %s as it is, using places, activities and things that really exist in %s "
               "plus what those mods add; don't invent features the game doesn't have. 4 to 6 quests that build to "
               "a finale, each with 2 to 4 clear objectives the player can do and tick off themselves. Keep it fun. Answer with only JSON: {\"title\": \"...\", \"intro\": \"two sentences\", "
               "\"quests\": [{\"title\": \"...\", \"briefing\": \"one or two sentences\", \"objectives\": "
               "[\"...\"], \"reward\": \"what they get or bragging rights\"}]}" % (
                   merge_name, base, theme, ", ".join(mods) or "none yet",
                   ("The player wants: %s. " % idea) if idea else "", base, base))
        text = self.ask(ask)
        m = re.search(r"\{.*\}", text, re.S)
        try:
            data = json.loads(m.group(0)) if m else {}
        except ValueError:
            data = {}
        quests = []
        for q in data.get("quests", [])[:8]:
            objs = [str(o)[:160] for o in q.get("objectives", [])[:6] if str(o).strip()]
            if q.get("title") and objs:
                quests.append({"title": str(q["title"])[:80], "briefing": str(q.get("briefing", ""))[:400],
                               "objectives": objs, "reward": str(q.get("reward", ""))[:160]})
        if not quests:
            raise AIError("The questline came out garbled. Try again.")
        return {"title": str(data.get("title") or merge_name)[:80], "intro": str(data.get("intro", ""))[:500], "quests": quests}

    def story_pack(self, base, source, merge_name, mods, idea="", items=(), people=(), in_game=False):
        """The merge standard's story: a DLC-style storyline for the host game with the other game's gameplay
        in it, characters with new roles, quests the game can check, shops and a starter kit (raw JSON)."""
        known = ""
        if items:
            known += "Items that really exist in %s (use these exact names for shops, rewards and the kit): %s. " % (
                base, ", ".join(items[:250]))
        if people:
            known += "People already in %s: %s. " % (base, ", ".join(people[:80]))
        ask = (
            "Write a story expansion, like a DLC, for my merged game \"%s\": %s with %s mixed in through mods (%s). %s"
            "It continues %s's own world and characters after the start of its main story, and brings in %s's kind of "
            "gameplay (for example weapons, gangs and fighting police if that's what %s is about). %s"
            "Make 6 characters with roles that fit the merge (weapons dealer, rival boss, crooked cop, fixer, getaway "
            "driver, insider), each with a short bio and how they talk. 3 acts, 8 to 12 quests in order that build to a "
            "finale. Each quest has a giver (a character id), a start_text (a phone message from the giver, in their "
            "voice, 1-3 sentences), a done_text, and 2 to 4 steps. Step types the game can check: text (reply to the "
            "giver; give a short reply line), earn (make $amount more), cash (have $amount), takedown (amount, "
            "target: police, any, or a person's name), wanted (reach wanted level amount 1-4), escape (lose the "
            "police after being wanted), buy (buy item from a character's shop), days (wait amount in-game days). "
            "Rewards: cash, xp and items. Give the weapons dealer and one other character a shop of 2-5 items with "
            "prices and after_quest (the quest index after which it's on sale, -1 for from the start). Add a kit: 2 to "
            "6 starting items that give %s's signature gear. Keep it fun, 18+ crime-drama tone is fine, no slurs. "
            "Answer with only JSON: {\"title\", \"intro\", \"unlock_day\": 1, \"kit\": [\"Item*qty\"], "
            "\"characters\": [{\"id\": \"c0\", \"first\", \"last\", \"role\", \"bio\", \"shop_line\", "
            "\"shop\": [{\"item\", \"price\", \"after_quest\"}]}], \"quests\": [{\"title\", \"act\", "
            "\"briefing\", \"giver\", \"start_text\", \"done_text\", \"steps\": [{\"type\", \"text\", "
            "\"amount\", \"target\", \"item\", \"reply\"}], \"reward\": {\"cash\", \"xp\", "
            "\"items\": [{\"item\", \"qty\"}], \"text\"}}]}" % (
                merge_name, base, source, ", ".join(mods[:20]) or "none yet",
                ("The player wants: %s. " % idea) if idea else "", base, source, source, known, source))
        if not in_game:
            ask += " This game has no in-game loader yet, so the player ticks steps off themselves: keep steps clear."
        data = _json(self.ask(ask))
        if not isinstance(data, dict):
            raise AIError("The story came out garbled. Try again.")
        return data

    def game_identity(self, base, theme, slots, avoid=()):
        """A name and tagline for the merged game, as if it were a new game of its own."""
        ask = ("Name a new game that is %s merged with %s (mods: %s). Give it a catchy, original title (2 to 4 words, "
               "no colon, don't just glue the two names together) and a punchy one-line tagline under 70 characters.%s "
               "Answer with only JSON: {\"title\": \"...\", \"tagline\": \"...\"}" % (
                   base, theme, ", ".join(slots), (" Not any of: " + ", ".join(avoid) + ".") if avoid else ""))
        text = self.ask(ask)
        m = re.search(r"\{.*\}", text, re.S)
        try:
            out = json.loads(m.group(0)) if m else {}
        except ValueError:
            out = {}
        title = re.sub(r"[\\/:*?\"<>|]", "", str(out.get("title") or "")).strip()[:40]
        return {"title": title, "tagline": str(out.get("tagline") or "").strip()[:90]} if title else None

    def preview_pitch(self, base, theme, slots):
        """Two or three sentences on what this merge would feel like to play."""
        ask = ("In two or three short sentences, describe what playing %s merged with %s would feel like: what "
               "you'd actually do and see. The mods it adds: %s. Be honest, it's mods layered on %s, not a new game. "
               "No lists, no markdown." % (base, theme, ", ".join(slots), base))
        return self.ask(ask).strip()[:600]

    def fun_scores(self, base, others):
        """How FUN each game would be merged into `base` (not how hard it is).
        Returns {name: {"rating": "green"|"yellow"|"red", "why": one line}}."""
        ask = ("Rate how fun it would be to merge each of these games into %s, as a mod-based merge where %s stays the "
               "game you play and the other game's style, vehicles, weapons, characters, activities or vibe get layered "
               "on. Rate only on fun: how many cool things you could do together and how well the worlds and gameplay "
               "mix. Do NOT rate on how hard it is to mod. green = great pairing, yellow = OK, red = doable but not as "
               "great. Games:\n%s\n\nAnswer with only JSON: {\"<exact game name>\": {\"rating\": \"green|yellow|red\", "
               "\"why\": \"<one short plain line, under 90 characters>\"}}" % (base, base, "\n".join("- " + o for o in others)))
        text = self.ask(ask)
        m = re.search(r"\{.*\}", text, re.S)
        try:
            data = json.loads(m.group(0)) if m else {}
        except ValueError:
            data = {}
        out = {}
        for name in others:
            v = data.get(name) or {}
            if v.get("rating") in ("green", "yellow", "red"):
                out[name] = {"rating": v["rating"], "why": str(v.get("why") or "")[:140]}
        return out

    def style_recipe(self, base, engine, moddable, source, site="Nexus Mods"):
        """For a base game without a built-in recipe: what bringing `source`'s style in means, and what
        to search for. {"description", "slots": [{"name", "search"}]} or None."""
        ask = ("Base game: %s (%s, mod support: %s). Bring in the style of %s, using only mods made for %s on %s. "
               "Write a two-sentence description of the merge (honest: if mod support is limited or missing, say what "
               "is realistically possible), and 3 to 5 mod categories to look for, each with a short search term "
               "that works on %s (2 to 3 words, no game name). The FIRST category is the other game's hero: its single "
               "most iconic thing that people recognise in merge videos (its signature car, main character or skin, "
               "signature weapon, creature, blocks or HUD), named like \"Hero: <the thing>\". Answer with only JSON: {\"description\": \"...\", "
               "\"slots\": [{\"name\": \"<category, e.g. Racing cars>\", \"search\": \"<search term>\"}]}"
               % (base, engine, moddable, source, base, site, site))
        data = _json(self.ask(ask, 4000))
        slots = [{"name": str(x.get("name", ""))[:60], "search": str(x.get("search", ""))[:40]}
                 for x in data.get("slots", [])[:6] if x.get("name") and x.get("search")]
        for x in slots:
            if x["name"].lower().startswith("hero"):
                x["hero"] = True
                x["name"] = x["name"].split(":", 1)[-1].strip() or x["name"]
        if not slots:
            return None
        return {"description": str(data.get("description") or "")[:500], "slots": slots}

    def rundown(self, base, source, merge_name, mods, quest="", controls="", creative=False):
        """What's new and what to do, shown while the merged game loads. Plain, short, skimmable."""
        ask = ("The player is starting \"%s\": %s merged with %s style%s. Mods installed: %s.%s%s "
               "Write a quick rundown they read while it loads: what's new and what they can do now. Only mention "
               "things these mods really add (by their names; if a name is unclear, say what kind of mod it is). Short "
               "lines, no hype. Answer with only JSON: {\"headline\": \"one line\", \"new\": [\"up to 5 things that are "
               "new\"], \"try\": [\"up to 4 things to try first\"], \"keys\": [\"up to 4 keys or buttons for the new "
               "stuff, only if you know them (e.g. mod menus)\"], \"tips\": [\"up to 3 tips\"]}" % (
                   merge_name, base, source, " in creative/sandbox mode" if creative else "",
                   ", ".join(mods[:30]) or "none yet", (" Current quest: %s." % quest) if quest else "",
                   (" Controller: %s." % controls) if controls else ""))
        data = _json(self.ask(ask, 4000))
        clean = lambda xs, n: [str(x)[:160] for x in (xs or [])[:n] if str(x).strip()]
        out = {"headline": str(data.get("headline") or "")[:140], "new": clean(data.get("new"), 5),
               "try": clean(data.get("try"), 4), "keys": clean(data.get("keys"), 4), "tips": clean(data.get("tips"), 3)}
        if not (out["headline"] or out["new"]):
            raise AIError("The rundown came out empty.")
        return out

    def ask(self, text, max_tokens=16000):
        """One question, the whole answer back as text."""
        a = self._anthropic
        try:
            with self.client.beta.messages.stream(
                model=MODEL,
                max_tokens=max_tokens,
                system=SYSTEM,
                messages=[{"role": "user", "content": text}],
                output_config={"effort": "low"},
                betas=["server-side-fallback-2026-07-01"],
                fallbacks="default",
            ) as stream:
                out = "".join(stream.text_stream)
                final = stream.get_final_message()
            if final.stop_reason == "refusal":
                raise AIError("The AI couldn't help with that one.")
            return out
        except a.AuthenticationError:
            raise AIError("The Claude API key didn't work. Check it in Settings > AI features.")
        except a.PermissionDeniedError:
            raise AIError("The Claude API key isn't allowed to use this model. Check the Anthropic account.")
        except a.RateLimitError:
            raise AIError("Too many AI requests right now. Wait a moment and try again.")
        except a.APIStatusError as e:
            raise AIError("Claude returned an error (%s). Try again in a bit." % e.status_code)
        except a.APIConnectionError:
            raise AIError("Can't reach the AI right now (offline?).")
