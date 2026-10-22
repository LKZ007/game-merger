"""Frankenstein's own achievements: unlocked by things you do in the app, kept in settings."""

import time

# id: (title, how to get it)
LIST = {
    "first_merge": ("It's alive", "Make your first merge."),
    "five_merges": ("Mad scientist", "Make 5 merges."),
    "first_play": ("Test drive", "Play a merged game."),
    "ten_plays": ("Regular", "Play merged games 10 times."),
    "preview": ("Window shopper", "Preview a merge before making it."),
    "shared": ("Pass it on", "Share a merge with a friend."),
    "imported": ("Gift received", "Open a merge a friend shared."),
    "party_host": ("Party starter", "Host a Play together party."),
    "party_join": ("Plus one", "Join a friend's party."),
    "crash_fixed": ("Doctor Franklin", "Let Franklin fix a crash."),
    "chaos": ("Agent of chaos", "Switch a merge to a chaos mod profile."),
    "photographer": ("Photographer", "Take 10 screenshots with Ctrl+Alt+S."),
    "collector": ("Collector", "Connect two or more game launchers."),
    "talker": ("Hey Franklin", "Talk to Franklin with push-to-talk."),
    "quest_done": ("Questgiver", "Finish a questline."),
    "controller": ("Couch mode", "Drive Frankenstein with a controller."),
}


def award(settings, key):
    """Unlock key. True if it's new."""
    got = settings.setdefault("achievements", {})
    if key not in LIST or key in got:
        return False
    got[key] = time.time()
    return True


def count(settings, key, n):
    """Bump a counter; returns the new value."""
    c = settings.setdefault("counters", {})
    c[key] = c.get(key, 0) + n
    return c[key]


def board(settings):
    got = settings.get("achievements", {})
    return [{"id": k, "title": t, "how": h, "unlocked": got.get(k)} for k, (t, h) in LIST.items()]
