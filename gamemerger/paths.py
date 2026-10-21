"""Where Game Merger keeps its own data (settings, downloads, backups, merge records)."""

import json
import os


def data_dir():
    base = os.environ.get("GAMEMERGER_HOME")
    if not base:
        local = os.environ.get("LOCALAPPDATA")
        base = os.path.join(local, "GameMerger") if local else os.path.join(os.path.expanduser("~"), ".gamemerger")
    os.makedirs(base, exist_ok=True)
    return base


def sub(name):
    p = os.path.join(data_dir(), name)
    os.makedirs(p, exist_ok=True)
    return p


def user_recipes_dir():
    return sub("recipes")


def settings_path():
    return os.path.join(data_dir(), "settings.json")


def load_settings():
    try:
        with open(settings_path(), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def save_settings(settings):
    with open(settings_path(), "w", encoding="utf-8") as f:
        json.dump(settings, f, indent=2)
