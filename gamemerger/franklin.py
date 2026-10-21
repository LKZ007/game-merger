"""Franklin, the built-in assistant. Runs on Claude using the player's own API key.

Franklin only ever sees text: game names, the chosen merge, mod file names and the
paths inside them, and log files. Game files themselves are never sent."""

import glob
import os

MODEL = "claude-opus-5-5"
LOG_TAIL_LINES = 150

SYSTEM = """You are Franklin, the assistant built into Game Merger, a Windows app for PC gamers.

What Game Merger does: games on different engines can't truly be merged, so it "merges" them the realistic way. \
The player picks a base game they will actually play (for example Cyberpunk 2077) and a style to bring in from \
another game (for example Forza: cars, driving feel, racing). Game Merger makes a safe copy of the base game \
(or backs up the original), installs the mod frameworks the base game needs, and installs mods the player \
downloaded, usually from Nexus Mods. Nothing is ever copied out of the other game. Everything can be undone.

Your job:
- Explain in plain, friendly language what a merge will do and what it can't do.
- Suggest which kinds of mods to look for, and what to search for on Nexus Mods. Only name a specific mod if you \
are confident it exists; otherwise describe what to search for.
- Look at the mod files the player added and point out clashes (two mods changing the same files), missing \
requirements, or files that look like they are for a different game.
- When the player shares log files after a crash, find the most likely cause and give clear steps to fix it, \
like which mod to remove or update.
- Warn about online play: modded files in online games (GTA Online and similar) can get accounts banned.

Your personality: you're Franklin, a laid-back, street-smart lad from Manchester who knows his games inside out. \
You're warm, confident and a bit cheeky, and you talk like a mate. Use British English and a light sprinkle of \
natural Manchester slang where it fits ("our kid", "sound", "mint", "buzzin", "nowt", "summat", "ta"), roughly \
once or twice per reply, never forced and never at the cost of clarity. Don't do an exaggerated accent in writing. \
Your replies may be read aloud, so keep sentences natural to say.

Keep answers short: a few sentences or a short list. The player is a gamer, not a programmer, so avoid jargon. \
Never invent download links. If you aren't sure, say so."""


class FranklinError(Exception):
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
    """Turn what the app knows right now into a short text block for Franklin."""
    lines = ["[What the player is looking at in Game Merger right now]"]
    if ctx.get("screen"):
        lines.append("Screen: %s" % ctx["screen"])
    if ctx.get("installed_games"):
        lines.append("Installed games: %s" % ", ".join(ctx["installed_games"]))
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
    for name, text in ctx.get("logs", []):
        lines.append("\n--- log file %s (last lines) ---\n%s" % (name, text))
    return "\n".join(lines)


class Franklin:
    def __init__(self, api_key):
        if not api_key:
            raise FranklinError("Add your Claude API key in Settings to talk to Franklin.")
        import anthropic  # imported here so the rest of the app works without it
        self._anthropic = anthropic
        self.client = anthropic.Anthropic(api_key=api_key)

    def stream_reply(self, history):
        """history: [{"role": "user"|"assistant", "content": str}]. Yields text as it arrives."""
        a = self._anthropic
        try:
            with self.client.beta.messages.stream(
                model=MODEL,
                max_tokens=16000,
                system=SYSTEM,
                messages=history,
                output_config={"effort": "low"},
                betas=["server-side-fallback-2026-07-01"],
                fallbacks="default",
            ) as stream:
                for text in stream.text_stream:
                    yield text
                final = stream.get_final_message()
            if final.stop_reason == "refusal":
                yield "\n\nSorry, I can't help with that one."
            elif final.stop_reason == "max_tokens":
                yield "\n\n(I ran out of room there. Ask me to continue.)"
        except a.AuthenticationError:
            raise FranklinError("Your Claude API key didn't work. Check it in Settings.")
        except a.PermissionDeniedError:
            raise FranklinError("Your Claude API key isn't allowed to use this model. Check your Anthropic account.")
        except a.RateLimitError:
            raise FranklinError("Franklin is getting too many questions right now. Wait a moment and try again.")
        except a.APIStatusError as e:
            raise FranklinError("Claude returned an error (%s). Try again in a bit." % e.status_code)
        except a.APIConnectionError:
            raise FranklinError("Franklin can't reach the internet right now.")
