"""Everything the window can ask the app to do. The web UI calls these methods through
pywebview (or the local browser bridge in server.py); every argument and return value is plain JSON."""

import json
import os
import re
import shutil
import subprocess
import sys
import threading
import time
import traceback
import urllib.parse
import uuid
import webbrowser

from . import accounts, achievements, ai, autorecipe, controller, covers, minecraft, modsources, storeinfo, discord, profiles, detect, downloader, gallery, network, nexus, paths, permissions, saves, scan, sharing, updater
from .version import VERSION
from .installer import MODE_COPY, MODE_HARDLINK, MODE_INPLACE, Merge, MergeError, find_clashes, preview
from . import recipes
from .recipes import load_recipes, resource_dir

SHARE_TYPES = ("Shared merges (*.gmerge)", "All files (*.*)")
MOD_TYPES = ("Mod files (*.zip;*.7z;*.rar;*.archive;*.dll;*.asi;*.reds;*.lua;*.yaml;*.xl)", "All files (*.*)")


def default_destination(base_path):
    if sys.platform == "win32":
        drive = os.path.splitdrive(os.path.abspath(base_path))[0] or "C:"
        return drive + "\\Frankenstein"
    return os.path.join(os.path.expanduser("~"), "Frankenstein")


class Job:
    def __init__(self):
        self.id = uuid.uuid4().hex[:10]
        self.log, self.progress = [], None
        self.done, self.error, self.result = False, None, None

    def write(self, text):
        self.log.append(text)


class Api:
    def __init__(self):
        self._window = None  # set by app.py when running inside pywebview
        data = profiles.ensure_admin(paths.load_settings(), self._default_admin_name())
        last = profiles.find(data.get("last")) or data["profiles"][0]
        self._settings = profiles.Settings(paths.load_settings(), last["id"])
        # one account without a PIN opens straight in; otherwise the page shows who's playing first
        self._locked = len(data["profiles"]) > 1 or bool(last.get("pin"))
        self._hand = load_recipes([resource_dir(), paths.user_recipes_dir()])
        self._recipes = self._hand + autorecipe.load_all()
        self._games = []
        self._owned = {}
        self._jobs = {}
        self._fun_busy = set()
        self._pad_busy = False
        self._mapper = None
        self._lock = threading.Lock()
        self._playing = None
        self._recover_saves()
        self._start_hotkey()
        self._presence = discord.Presence()
        threading.Thread(target=self._upgrade_all, daemon=True).start()

    # ---------- accounts ----------

    @staticmethod
    def _default_admin_name():
        try:
            return os.getlogin()
        except OSError:
            return os.environ.get("USERNAME") or "Player 1"

    def _me(self):
        return profiles.find(self._settings.pid) or {}

    def _perm(self, key, what):
        p = self._me()
        if not p.get("admin") and not dict(profiles.DEFAULT_PERMS, **p.get("perms", {})).get(key, True):
            raise MergeError("Your account isn't allowed to %s. Ask %s to change it in Settings > Accounts." % (
                what, next((x["name"] for x in profiles.load()["profiles"] if x.get("admin")), "the admin")))

    def _admin(self):
        if not self._me().get("admin"):
            raise MergeError("Only the admin account can change that.")

    def profiles_state(self):
        data = profiles.load()
        return {"current": profiles.public(self._me()) if self._me() else None, "locked": self._locked,
                "profiles": [profiles.public(p) for p in data["profiles"]], "permissions": profiles.PERMISSIONS}

    def profile_switch(self, pid, pin=""):
        p = profiles.find(pid)
        if not p:
            raise MergeError("That account doesn't exist any more.")
        if not profiles.check_pin(p, pin):
            raise MergeError("Wrong PIN.")
        with self._lock:
            self._settings = profiles.Settings(paths.load_settings(), pid)
        profiles.set_last(pid)
        self._locked = False
        return self.profiles_state()

    def profile_lock(self):
        """Back to "who's playing?" (switch account)."""
        self._locked = True
        return self.profiles_state()

    def profile_add(self, name, pin="", perms=None):
        self._admin()
        try:
            profiles.add(name, pin, perms)
        except profiles.ProfileError as e:
            raise MergeError(str(e))
        return self.profiles_state()

    def profile_update(self, pid, name=None, pin=None, perms=None):
        me = self._me()
        if pid != me.get("id"):
            self._admin()
        elif perms is not None and not me.get("admin"):
            raise MergeError("Only the admin account can change what you're allowed to do.")
        try:
            profiles.update(pid, name, pin, perms)
        except profiles.ProfileError as e:
            raise MergeError(str(e))
        return self.profiles_state()

    def profile_remove(self, pid):
        self._admin()
        try:
            profiles.remove(pid)
        except profiles.ProfileError as e:
            raise MergeError(str(e))
        return self.profiles_state()

    # ---------- upgrading merges made by older versions ----------

    UPGRADE = 2  # bump when a release adds something old merges should get

    def _upgrade_all(self):
        """Once per release: give merges made by older versions everything new ones get."""
        if self._settings.get("merges_upgraded", 0) >= self.UPGRADE:
            return
        for m in Merge.all():
            try:
                self._upgrade(m)
            except Exception:
                pass  # left for the Upgrade button
        self._settings["merges_upgraded"] = self.UPGRADE
        paths.save_settings(self._settings)

    def _upgrade(self, m):
        """Back up the merge's record, then fill in what's missing. Mods, files and saves aren't touched."""
        if m.data.get("upgraded", 0) >= self.UPGRADE:
            return False
        shutil.copy2(os.path.join(Merge.record_dir(m.id), "manifest.json"),
                     os.path.join(Merge.record_dir(m.id), "manifest.v%d.bak.json" % m.data.get("upgraded", 1)))
        if not self._recipe(m.data.get("recipe_id")) and os.path.isdir(m.base_path):
            # recipe gone (or never had one): make one from the base game's files
            g = detect.InstalledGame(m.data.get("game_name") or os.path.basename(m.base_path), m.base_path, "Merge")
            r = autorecipe.build(g, [])
            self._recipes.append(r)
            m.data["recipe_id"] = r.id
        if not m.data.get("source_name"):
            m.data["source_name"] = (m.data.get("theme_name") or "").split(" style")[0]
        if not m.data.get("tagline") and self._settings.get("claude_api_key"):
            try:
                got = self._ai().game_identity(m.data.get("game_name", ""), m.data.get("source_name", ""),
                                               [i["label"] for i in m.data.get("installs", [])][:8], [])
                if got:
                    m.data["tagline"] = got["tagline"]
            except Exception:
                pass
        m.data.setdefault("owner", None)
        m.data["upgraded"] = self.UPGRADE
        m.save()
        return True

    def merge_upgrade(self, merge_id):
        """The Upgrade button: run the upgrade for one merge again."""
        m = Merge.load(merge_id)
        m.data["upgraded"] = min(m.data.get("upgraded", 1), self.UPGRADE - 1)
        self._upgrade(m)
        return True

    # ---------- helpers ----------

    def _recipe(self, rid):
        return next((r for r in self._recipes if r.id == rid), None)

    def _ai(self):
        return ai.AI(self._settings.get("claude_api_key", ""))

    def _game(self, key):
        return self._games[int(key)]

    def _run(self, fn):
        job = Job()
        self._jobs[job.id] = job

        def runner():
            try:
                job.result = fn(job)
            except MergeError as e:
                job.error = str(e)
            except Exception as e:
                job.write(traceback.format_exc())
                job.error = "Something went wrong: %s" % e
            finally:
                job.done = True
        threading.Thread(target=runner, daemon=True).start()
        return job.id

    def _dialog(self, kind, multiple=False, title="", types=None):
        if self._window is not None:
            import webview
            ft = getattr(webview, "FileDialog", None)
            if kind == "folder":
                d = ft.FOLDER if ft else webview.FOLDER_DIALOG
                res = self._window.create_file_dialog(d)
            else:
                d = ft.OPEN if ft else webview.OPEN_DIALOG
                res = self._window.create_file_dialog(d, allow_multiple=multiple, file_types=types or MOD_TYPES)
            return list(res or [])
        try:  # browser mode: fall back to a plain system dialog
            import tkinter as tk
            from tkinter import filedialog
            root = tk.Tk()
            root.withdraw()
            root.attributes("-topmost", True)
            if kind == "folder":
                r = filedialog.askdirectory(title=title)
                res = [r] if r else []
            elif multiple:
                res = list(filedialog.askopenfilenames(title=title))
            else:
                r = filedialog.askopenfilename(title=title)
                res = [r] if r else []
            root.destroy()
            return [os.path.normpath(p) for p in res]
        except Exception:
            return []

    def _recover_saves(self):
        """If the PC crashed or the app was closed mid-game, put the original saves back."""
        state = saves.active()
        if not state:
            return
        try:
            r = self._recipe(Merge.load(state["merge_id"].split("~")[0]).data.get("recipe_id"))
            if r and saves.running(r.processes):
                self._playing = state["merge_id"].split("~")[0]
                saves.watch_until_closed(r.processes, self._game_closed, grace=0)
                return
        except (OSError, ValueError):
            pass
        saves.swap_out()

    def _game_closed(self):
        mid = self._playing or (saves.active() or {}).get("merge_id")
        saves.swap_out()
        self._playing = None
        cloud = self._settings.get("cloud_folder")
        if cloud and mid:
            try:
                saves.push_cloud(mid, cloud, Merge.load(mid).name)
            except OSError:
                pass  # cloud folder offline: next session tries again

    def _steam_root(self):
        if detect.winreg is not None:
            return detect._reg_value(detect.winreg.HKEY_CURRENT_USER, r"Software\Valve\Steam", "SteamPath")
        return None

    # ---------- library ----------

    def scan(self):
        self._games = detect.scan(self._hand, self._settings.get("manual_folders", []))
        self._build_recipes()
        return self.library()

    def _style_names(self):
        names = [g.name for g in self._games] + [g["name"] for g in self._owned.values()]
        return list(dict.fromkeys(names))

    def _build_recipes(self):
        """Every game can be the base: games without a built-in recipe get one made from their files,
        and every other game you have (installed or owned) is offered as a style."""
        styles = self._style_names()
        for r in self._hand:
            autorecipe.extend(r, styles)
        for g in self._games:
            if g.recipe is None or getattr(g.recipe, "auto", None):
                try:
                    g.recipe = autorecipe.build(g, styles)
                except OSError:
                    g.recipe = None
        built = {g.recipe.id: g.recipe for g in self._games if g.recipe and getattr(g.recipe, "auto", None)}
        self._recipes = self._hand + list(built.values()) + [r for r in autorecipe.load_all(styles) if r.id not in built]

    def library(self):
        installed = {g.name.lower() for g in self._games}
        out = []
        for i, g in enumerate(self._games):
            r = g.recipe
            themes = []
            for t in (r.themes if r else []):
                themes.append({"id": t.id, "name": t.name, "from_game": t.from_game, "description": t.description,
                               "found": any(any(m in n for m in t.source_match) for n in installed)})
            themes.sort(key=lambda t: (t["id"].startswith("style-"), not t["found"], t["from_game"].lower()))
            auto = getattr(r, "auto", None) or {}
            out.append({"key": str(i), "name": g.name, "source": g.source, "path": g.path, "base": r is not None,
                        "recipe_id": r.id if r else None, "warning": r.warning if r else "", "themes": themes,
                        "engine": auto.get("engine", ""), "moddable": auto.get("moddable", "yes" if r else "no"),
                        "mod_note": auto.get("note", ""), "creative": bool(r and r.sandbox)})
        return out

    # ---------- every game you own (all launchers, any PC) ----------

    def _installed_here(self):
        return [{"name": g.name, "source": g.source, "appid": g.appid} for g in getattr(self, "_games", [])]

    def library_status(self):
        sid, persona = accounts.steam_account(self._steam_root())
        sid = self._settings.get("steam_id") or sid
        epic = accounts.epic_account()
        return {"steam": {"id": sid or "", "name": persona if sid and not self._settings.get("steam_id") else "",
                          "has_key": bool(accounts.secret_get("steam_key")), "on": bool(self._settings.get("steam_on")),
                          "error": self._settings.get("steam_error", "")},
                "epic": {"name": epic.get("name", "") if epic else "", "on": bool(epic)},
                "gog": {"available": bool(accounts.galaxy_db()), "on": bool(self._settings.get("gog_on"))},
                "devices": [d.get("device") for d in accounts.other_devices(self._settings.get("cloud_folder"))]}

    def steam_connect(self, profile=None, key=None):
        """Turn on Steam's owned list. profile: an id or profile link if this PC's Steam isn't signed in."""
        if profile:
            sid = accounts.steam_id_from(profile)
            if not sid:
                raise MergeError("Paste your Steam profile link (steamcommunity.com/profiles/7656...) or SteamID.")
            self._settings["steam_id"] = sid
        if key is not None:
            key = key.strip()
            if key:
                accounts.secret_set("steam_key", key)
            else:
                accounts.secret_delete("steam_key")
        self._settings["steam_on"] = True
        paths.save_settings(self._settings)
        self._refresh_source("Steam")
        return self.library_status()

    def epic_connect(self):
        """Open Epic's sign-in in a small window and wait for it. Browser mode: returns the link instead."""
        if self._window is None:
            return {"manual": True, "url": accounts.EPIC_LOGIN}
        import webview
        win = webview.create_window("Sign in to Epic Games", accounts.EPIC_LOGIN, width=520, height=760)
        code = None
        for _ in range(600):  # five minutes to sign in
            time.sleep(0.5)
            try:
                url = win.get_current_url() or ""
            except Exception:
                break  # closed
            if "/id/api/redirect" in url:
                try:
                    code = accounts.epic_code_from(win.evaluate_js("document.body.innerText"))
                except Exception:
                    code = None
                break
        try:
            win.destroy()
        except Exception:
            pass
        if not code:
            raise MergeError("Epic sign-in didn't finish. Try again.")
        return self._epic_finish(code)

    def epic_paste(self, text):
        code = accounts.epic_code_from(text) or (text.strip() if re.fullmatch(r"[0-9a-f]{32}", text.strip()) else None)
        if not code:
            raise MergeError("That doesn't look like Epic's code. Copy the whole page after signing in.")
        return self._epic_finish(code)

    def _epic_finish(self, code):
        try:
            accounts.epic_sign_in(code)
        except accounts.AccountError as e:
            raise MergeError(str(e))
        self._refresh_source("Epic")
        return self.library_status()

    def gog_connect(self):
        self._settings["gog_on"] = True
        paths.save_settings(self._settings)
        self._refresh_source("GOG")
        return self.library_status()

    def library_disconnect(self, source):
        if source == "Steam":
            self._settings.pop("steam_on", None)
            self._settings.pop("steam_id", None)
            accounts.secret_delete("steam_key")
        elif source == "Epic":
            accounts.secret_delete("epic")
        elif source == "GOG":
            self._settings.pop("gog_on", None)
        paths.save_settings(self._settings)
        try:
            os.remove(accounts._cache_file(source.lower() + ".json"))
        except OSError:
            pass
        return self.library_status()

    def _refresh_source(self, source):
        try:
            if source == "Steam":
                st = self.library_status()["steam"]
                if not st["id"]:
                    raise accounts.AccountError("Steam isn't signed in on this PC. Paste your Steam profile link.")
                try:
                    games = accounts.steam_owned(st["id"], accounts.secret_get("steam_key"))
                except accounts.AccountError as e:
                    if str(e) == "private":
                        raise accounts.AccountError("Your Steam game list is private. Make Game details public on your "
                                                    "Steam profile, or add a free Steam Web API key.")
                    raise
                self._settings.pop("steam_error", None)
            elif source == "Epic":
                games = accounts.epic_owned()
            else:
                games = accounts.gog_owned()
            accounts._save_cache(source.lower() + ".json", games)
            st = self.library_status()
            if sum(1 for x in (st["steam"]["on"], st["epic"]["on"], st["gog"]["on"]) if x) >= 2:
                self._award("collector")
        except accounts.AccountError as e:
            if source == "Steam":
                self._settings["steam_error"] = str(e)
                paths.save_settings(self._settings)
            raise MergeError(str(e))

    def library_owned(self, refresh=False):
        """Owned games that aren't on this PC, from the launchers that are switched on."""
        st = self.library_status()
        sources = [s for s, on in (("Steam", st["steam"]["on"]), ("Epic", st["epic"]["on"]), ("GOG", st["gog"]["on"])) if on]
        errors = []
        if refresh:
            for src in sources:
                try:
                    self._refresh_source(src)
                except MergeError as e:
                    errors.append("%s: %s" % (src, e))
        owned = {src: accounts.cached(src.lower() + ".json") for src in sources}
        cloud = self._settings.get("cloud_folder")
        try:
            accounts.publish_installed(cloud, self._installed_here())
        except OSError:
            pass
        games = accounts.merge_lists(self._installed_here(), owned, accounts.other_devices(cloud))
        before = set(self._style_names())
        self._owned = {g["key"]: g for g in games}
        if set(self._style_names()) != before:
            self._build_recipes()  # newly seen owned games become styles for every base game
        return {"games": games, "errors": errors, "sources": sources, "styles_changed": set(self._style_names()) != before}

    def fun_scores(self, base_key, names):
        """How fun merging each named game into the base game would be: {"scores": {name: {rating, why}},
        "self": rating of the base game itself, "pending": still working}. Every pairing gets a rating:
        built-in pairings are green, the AI rates the rest in the background (cached, asked once), and
        without an AI key a rough guess from the games' Steam genres is used. A base game with limited or
        no mod support caps every rating, since there's less you can actually do."""
        g = self._game(base_key)
        cache = self._fun_cache()
        mine = cache.get(g.name, {})
        if g.recipe:
            for t in g.recipe.themes:
                if getattr(t, "filled", True) and not t.id.startswith("style-"):
                    for n in names:
                        if any(m in n.lower() for m in t.source_match):
                            mine.setdefault(n, {"rating": "green", "why": "Ready-made merge: " + t.name, "src": "recipe"})
        has_key = bool(self._settings.get("claude_api_key"))
        todo = [n for n in names if n != g.name and (n not in mine or (has_key and mine[n].get("src") == "guess"))]
        pending = bool(todo)
        if todo and g.name not in self._fun_busy:
            self._fun_busy.add(g.name)
            threading.Thread(target=self._rate, args=(g, todo, has_key), daemon=True).start()
        moddable = (getattr(g.recipe, "auto", None) or {}).get("moddable", "yes") if g.recipe else "no"
        cap = {"yes": None, "limited": "yellow", "no": "red"}[moddable]
        order = ["green", "yellow", "red"]
        out = {}
        for n in names:
            if n in mine:
                v = dict(mine[n])
                if cap and order.index(v["rating"]) < order.index(cap):
                    v["rating"] = cap
                    v["why"] = (v["why"] + " (capped: %s has %s mod support)" % (g.name, "limited" if cap == "yellow" else "no real"))
                out[n] = v
        mine_self = {"yes": ("green", "Moddable: %s" % ((getattr(g.recipe, "auto", None) or {}).get("note") or "built-in recipe")),
                     "limited": ("yellow", "Limited mod support"), "no": ("red", "No real mod support")}[moddable]
        return {"scores": out, "self": {"rating": mine_self[0], "why": mine_self[1]}, "pending": pending}

    def game_ratings(self, names):
        """Before a main game is picked: every game's own colour, for how much you can mod it as the main game.
        {"scores": {name: {rating, why}}, "pending"}. Installed games are judged from their files; games that
        are only owned are judged from a built-in recipe or Steam Workshop, and say so when it can't be told yet."""
        out, todo = {}, []
        installed = {g.name: g for g in self._games}
        words = {"yes": ("green", "Fully moddable"), "limited": ("yellow", "Limited mod support"),
                 "no": ("red", "No real mod support that Frankenstein can find")}
        for n in names:
            g = installed.get(n)
            if g is not None:
                moddable = (getattr(g.recipe, "auto", None) or {}).get("moddable", "yes") if g.recipe else "no"
                note = (getattr(g.recipe, "auto", None) or {}).get("note") if g.recipe else ""
                r, why = words[moddable]
                out[n] = {"rating": r, "why": why + (": " + note if note and moddable == "yes" else "")}
                continue
            appid = self._appid_for_name(n)
            key = re.sub(r"[^a-z0-9]", "", n.lower())
            if any((appid and str(appid) in [str(a) for a in r.steam_appids]) or
                   (len(key) > 3 and re.sub(r"[^a-z0-9]", "", r.name.split("(")[0].lower()) == key) for r in self._hand):
                out[n] = {"rating": "green", "why": "Fully moddable: built-in recipe"}
                continue
            info = storeinfo.cached(appid) if appid else None
            if info is None:
                if n in self.__dict__.setdefault("_store_tried", set()):
                    out[n] = {"rating": "yellow", "why": "Couldn't look this game up; its mod support is checked properly once it's installed"}
                else:
                    todo.append(n)  # no colour yet: the tile shows "Rating…" until it's looked up
            elif info.get("workshop"):
                out[n] = {"rating": "green", "why": "Has Steam Workshop mods"}
            else:
                out[n] = {"rating": "yellow", "why": "Mod support is checked properly once it's installed"}
        if todo and not getattr(self, "_store_busy", False):
            self._store_busy = True

            def run():
                try:
                    for n in todo[:60]:
                        self._store_info(n, self._appid_for_name(n))
                        self._store_tried.add(n)
                finally:
                    self._store_busy = False
            threading.Thread(target=run, daemon=True).start()
        return {"scores": out, "pending": bool(todo)}

    def _fun_file(self):
        return os.path.join(paths.sub("library"), "fun-scores.json")

    def _fun_cache(self):
        try:
            with open(self._fun_file(), encoding="utf-8") as f:
                return json.load(f)
        except (OSError, ValueError):
            return {}

    def _rate(self, g, todo, has_key):
        """Background: rate every pairing not rated yet, saving as it goes."""
        try:
            got = {}
            if has_key:
                try:
                    bot = self._ai()
                    for i in range(0, len(todo), 50):
                        part = bot.fun_scores(g.name, todo[i:i + 50])
                        got.update({n: dict(v, src="ai") for n, v in part.items()})
                        self._save_fun(g.name, got)
                except Exception:
                    pass  # offline or bad key: fall back to a guess for now, ask again next time
            base_info = self._store_info(g.name, g.appid)
            for n in todo:
                if n not in got:
                    got[n] = dict(storeinfo.guess_fun(base_info, self._store_info(n, self._appid_for_name(n))), src="guess")
                    if len(got) % 10 == 0:
                        self._save_fun(g.name, got)
            self._save_fun(g.name, got)
        finally:
            self._fun_busy.discard(g.name)

    def _save_fun(self, base, got):
        with self._lock:
            cache = self._fun_cache()
            cache.setdefault(base, {}).update(got)
            with open(self._fun_file(), "w", encoding="utf-8") as f:
                json.dump(cache, f)

    def _appid_for_name(self, name):
        for g in self._games:
            if g.name == name and g.appid:
                return g.appid
        for g in self._owned.values():
            if g["name"] == name and g.get("appid"):
                return g["appid"]
        return None

    def _store_info(self, name, appid=None):
        try:
            appid = appid or covers.search_appid(name)
            return storeinfo.details(appid) if appid else None
        except Exception:
            return None

    def merge_identity(self, base_key, theme_id, avoid=None):
        """A new-game name and tagline for this merge, made up by the AI; without a key it's a simple
        name from the two games."""
        g = self._game(base_key)
        t = next((x for x in (g.recipe.themes if g.recipe else []) if x.id == theme_id), None)
        if not t:
            raise MergeError("Pick a style to merge in first.")
        if self._settings.get("claude_api_key"):
            try:
                bot = self._ai()
                got = bot.game_identity(g.name, t.from_game, [s.name for s in t.slots], avoid or [])
                if got:
                    return got
            except Exception:
                pass
        source = t.from_game.split(" /")[0]
        return {"title": "%s: %s Edition" % (g.name, source), "tagline": t.name}

    def merge_preview(self, base_key, theme_id):
        """What a merge could look like, before anything is downloaded: low-res trailers streamed from
        Steam (the base game and the game whose style comes in), pictures of popular mods for each part
        of the merge from Nexus Mods, and a short AI-written pitch. It's a preview of the parts, not a render
        of your merged game."""
        g = self._game(base_key)
        r = g.recipe
        t = next((x for x in (r.themes if r else []) if x.id == theme_id), None)
        if not t:
            raise MergeError("Pick a style to merge in first.")
        self._award("preview")
        out = {"base": g.name, "theme": t.name, "from_game": t.from_game, "clips": [], "pictures": [],
               "summary": t.description, "videos_url": "https://www.youtube.com/results?search_query=" +
               urllib.parse.quote("%s %s mod showcase" % (g.name, t.slots[0].search if t.slots else t.from_game))}

        def clip(appid, label):
            try:
                c = covers.steam_trailer(appid)
            except Exception:
                c = None
            if c:
                out["clips"].append(dict(c, label=label))

        def pictures(slot):
            try:
                mods = nexus.search(r.nexus_domain, slot.search, count=3)
            except nexus.NexusError:
                return
            for m in mods:
                if m["picture"]:
                    out["pictures"].append({"slot": slot.name, "name": m["name"], "url": m["picture"]})
                    break

        def pitch():
            if not self._settings.get("claude_api_key"):
                return
            try:
                bot = self._ai()
                out["summary"] = bot.preview_pitch(g.name, t.from_game, [s.name for s in t.slots]) or out["summary"]
            except Exception:
                pass

        base_id = g.appid or (r.steam_appids[0] if r.steam_appids else "")
        work = [threading.Thread(target=f, args=a, daemon=True) for f, a in
                [(clip, (base_id, g.name)), (clip, (t.source_appid, t.from_game)), (pitch, ())] +
                [(pictures, (s,)) for s in t.slots]]
        for w in work:
            w.start()
        for w in work:
            w.join(25)
        out["clips"].sort(key=lambda c: c["label"] != g.name)
        order = [s.name for s in t.slots]
        out["pictures"].sort(key=lambda p: order.index(p["slot"]))
        return out

    def owned_cover(self, key):
        g = getattr(self, "_owned", {}).get(key)
        if not g:
            return None
        if g["source"] == "Steam":
            return covers.as_data_uri(covers.steam_cover(g["appid"], steam_root=self._steam_root()))
        return covers.as_data_uri(covers.url_cover(g["image"])) if g.get("image") else None

    def owned_install(self, key):
        """Open the game's own launcher at its install page."""
        g = getattr(self, "_owned", {}).get(key)
        if not g or not g.get("install"):
            raise MergeError("No install link for that game.")
        if sys.platform == "win32":
            try:
                os.startfile(g["install"])
            except OSError:
                raise MergeError("Couldn't open %s. Is it installed on this PC?" % g["source"])
        return True

    def cover(self, key):
        g = self._game(key)
        return covers.as_data_uri(covers.cover_path(g, steam_root=self._steam_root()))

    def add_game_folder(self):
        picked = self._dialog("folder", title="Pick the folder the game is installed in")
        if not picked:
            return None
        manual = self._settings.setdefault("manual_folders", [])
        if picked[0] not in manual:
            manual.append(picked[0])
            paths.save_settings(self._settings)
        return self.scan()

    def merge_options(self, key, theme_id):
        g = self._game(key)
        r = g.recipe
        t = r.theme(theme_id)
        self._fill_style(r, t)
        return {
            "frameworks": [{"id": fid, "name": r.framework(fid).name, "manual_url": r.framework(fid).manual_url}
                           for fid in t.frameworks],
            "slots": [{"name": s.name, "search": s.search} for s in t.slots] + (
                [{"name": "Controller support (optional)", "search": "controller"}]
                if self._pad_status(g.name, g.appid) in ("partial", "none") and not self._is_mc(r) else []),
            "default_name": "%s - %s" % (r.name.split(" (")[0], t.from_game.split(" /")[0]),
            "destination": self._settings.get("destination") or default_destination(g.path),
            "mode": self._settings.get("mode", MODE_HARDLINK),
            "sources": self._sources_for(r),
            "minecraft": {"loaders": minecraft.LOADERS} if self._is_mc(r) else None,
        }

    def _fill_style(self, r, t):
        """A style that's only a placeholder gets real mod categories and searches from the AI (once,
        then kept). Without a key the placeholder stays: one slot searching for the other game's name."""
        if getattr(t, "filled", True) or not self._settings.get("claude_api_key"):
            return
        auto = getattr(r, "auto", None) or {}
        try:
            got = self._ai().style_recipe(r.name, auto.get("engine", "its own engine"), auto.get("moddable", "yes"), t.from_game)
        except Exception:
            return
        if got:
            autorecipe.set_theme(r.id, t.id, got["description"] or t.description, got["slots"])
            t.description = got["description"] or t.description
            t.slots = [recipes.Slot(**x) for x in got["slots"]]
            t.filled = True

    @staticmethod
    def _is_mc(r):
        return bool(r) and (getattr(r, "auto", None) or {}).get("engine", "").startswith("Minecraft")

    def _sources_for(self, r):
        """Where mods for this game can come from, best first."""
        out = []
        if self._is_mc(r):
            out.append("Modrinth")
        if self._settings.get("curseforge_key"):
            out.append("CurseForge")
        if r and r.nexus_domain and not self._is_mc(r):
            out.append("Nexus Mods")
        return out

    def mc_version(self, loader):
        """The newest Minecraft version a loader supports (asked when the loader is picked)."""
        try:
            return minecraft.pick_version(loader)
        except (modsources.SourceError, StopIteration, KeyError, ValueError) as e:
            raise MergeError(str(e) or "Couldn't find a Minecraft version for that loader.")

    def mod_search(self, recipe_id, text, source="all", loader=None, game_version=None):
        """Search every mod site that has this game at once. Each result says where it's from."""
        r = self._recipe(recipe_id)
        sources = self._sources_for(r) if source == "all" else [source]
        results, errors = [], []

        def nx():
            try:
                for m in nexus.search(r.nexus_domain, text):
                    results.append(dict(m, source="Nexus Mods", downloads=m.get("endorsements", 0),
                                        page=nexus.files_page(r.nexus_domain, m["id"])))
            except nexus.NexusError as e:
                errors.append("Nexus Mods: %s" % e)

        def mr():
            try:
                results.extend(modsources.modrinth_search(text, loader, game_version))
            except modsources.SourceError as e:
                errors.append("Modrinth: %s" % e)

        def cf():
            key = self._settings.get("curseforge_key")
            try:
                gid = modsources.curseforge_game(r.name, key)
                if not gid:
                    return errors.append("CurseForge doesn't have %s." % r.name) if source != "all" else None
                results.extend(modsources.curseforge_search(gid, text, key, loader, game_version))
            except modsources.SourceError as e:
                errors.append("CurseForge: %s" % e)
        work = [threading.Thread(target={"Nexus Mods": nx, "Modrinth": mr, "CurseForge": cf}[x], daemon=True)
                for x in sources if x in ("Nexus Mods", "Modrinth", "CurseForge")]
        for w in work:
            w.start()
        for w in work:
            w.join(30)
        # interleave the sites so the best of each shows near the top
        by = {}
        for x in results:
            by.setdefault(x["source"], []).append(x)
        mixed = [x for row in __import__("itertools").zip_longest(*by.values()) for x in row if x]
        return {"results": mixed, "errors": errors, "sources": sources}

    def mod_get(self, recipe_id, source, mod_id, loader=None, game_version=None):
        """Download a mod (and the mods it needs). Returns a job, or None when a page was opened instead
        (free Nexus accounts, and CurseForge mods whose authors only allow downloads on their site)."""
        self._perm("mods", "download mods")
        if source == "Nexus Mods":
            return self.nexus_get(recipe_id, mod_id)

        def work(job):
            prog = lambda d, t: setattr(job, "progress", [d, t] if t else None)
            pages = []
            if source == "Modrinth":
                got = modsources.modrinth_get(mod_id, loader, game_version, prog)
            else:
                got, pages = modsources.curseforge_get(int(mod_id), self._settings.get("curseforge_key"), loader, game_version, prog)
            for name, _p in got:
                job.write("Downloaded %s" % name)
            if len(got) > 1:
                job.write("(%d of those are mods it needs)" % (len(got) - 1))
            for page in pages:
                job.write("Opening CurseForge for a file its author only shares there …")
                webbrowser.open(page)
            return {"files": [{"path": p, "name": os.path.basename(p)} for _n, p in got], "pages": pages}
        try:
            return self._run(work)
        except modsources.SourceError as e:
            raise MergeError(str(e))

    def mod_link(self, link):
        """Download a mod from any direct link."""
        self._perm("mods", "download mods")

        def work(job):
            job.write("Downloading …")
            try:
                p = modsources.from_link(link, lambda d, t: setattr(job, "progress", [d, t] if t else None))
            except modsources.SourceError as e:
                raise MergeError(str(e))
            return {"files": [{"path": p, "name": os.path.basename(p)}], "pages": []}
        return self._run(work)

    def install_files(self, merge_id, files):
        """Put downloaded or picked mod files into an existing merge."""
        self._perm("mods", "add mods")
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))

        def work(job):
            for p in files:
                try:
                    m.install(p, os.path.basename(p), r.layout, log=job.write, check=scan.scan)
                except Exception as e:
                    job.write("✖ %s failed: %s" % (os.path.basename(p), e))
            self._rundown_stale(m)
            return True
        return self._run(work)

    def curseforge_set_key(self, key):
        self._admin()
        key = (key or "").strip()
        if key:
            try:
                modsources._cf("/games?pageSize=1", key)
            except modsources.SourceError as e:
                raise MergeError(str(e))
        self._settings["curseforge_key"] = key
        paths.save_settings(self._settings)
        return {"has_key": bool(key)}

    def find_mods(self, key, query):
        r = self._game(key).recipe
        webbrowser.open("https://www.nexusmods.com/games/%s/mods?keyword=%s"
                        % (r.nexus_domain, urllib.parse.quote(query)))

    # ---------- controllers ----------

    def _pad_cache(self):
        try:
            with open(os.path.join(paths.sub("library"), "controllers.json"), encoding="utf-8") as f:
                return json.load(f)
        except (OSError, ValueError):
            return {}

    def _pad_status(self, name, appid=None, fetch=False):
        """"full", "partial", "none" or "unknown" (from the Steam store, cached)."""
        cache = self._pad_cache()
        if name in cache or not fetch:
            return cache.get(name, "unknown")
        info = self._store_info(name, appid or self._appid_for_name(name))
        return (info or {}).get("controller", "unknown")

    def controller_info(self, names):
        """The controller badge for each game: {"games": {name: {"badge", "why"}}, "pending"}. Every game is
        playable with a controller: natively, or through Frankenstein's key mapper."""
        cache = self._pad_cache()
        todo = [n for n in names if n not in cache]
        if todo and not self._pad_busy:
            self._pad_busy = True

            def run():
                try:
                    for i, n in enumerate(todo):
                        info = self._store_info(n, self._appid_for_name(n))
                        if info is None:
                            continue  # offline or not on Steam: ask again next time
                        with self._lock:
                            c = self._pad_cache()
                            c[n] = info.get("controller", "unknown")
                            with open(os.path.join(paths.sub("library"), "controllers.json"), "w", encoding="utf-8") as f:
                                json.dump(c, f)
                finally:
                    self._pad_busy = False
            threading.Thread(target=run, daemon=True).start()
        out = {}
        for n in names:
            b, why = controller.badge(cache.get(n, "unknown"))
            out[n] = {"badge": b, "why": why}
        return {"games": out, "pending": bool(todo)}

    def pad_settings(self, merge_id):
        m = Merge.load(merge_id)
        status = self._pad_status(m.data.get("game_name", ""))
        return {"status": status, "mode": m.data.get("pad_mode", "auto"), "map": controller.merged_map(m.data.get("pad_map")),
                "keys": controller.KEYS, "names": controller.NAMES, "default": controller.DEFAULT,
                "mapper_on": self._mapper_wanted(m, status)}

    def pad_save(self, merge_id, mode, mapping=None):
        """mode: "auto" (mapper unless the game supports controllers fully), "on" or "off"."""
        m = Merge.load(merge_id)
        m.data["pad_mode"] = mode if mode in ("auto", "on", "off") else "auto"
        if mapping is not None:
            m.data["pad_map"] = {k: v for k, v in mapping.items() if k in controller.DEFAULT}
        m.save()
        return self.pad_settings(merge_id)

    @staticmethod
    def _mapper_wanted(m, status):
        mode = m.data.get("pad_mode", "auto")
        return mode == "on" or (mode == "auto" and status != "full")

    def _start_mapper(self, m, processes):
        """Run the key mapper while the game runs (only acts while the game's window is in front)."""
        if not self._mapper_wanted(m, self._pad_status(m.data.get("game_name", ""))) or not processes:
            return
        if self._mapper:
            self._mapper.stop()
        mapper = self._mapper = controller.Mapper(processes, m.data.get("pad_map"))
        if not mapper.start():
            return

        def until_closed():
            time.sleep(20)
            for _ in range(18):  # the game can take a while to start
                if saves.running(processes):
                    break
                time.sleep(5)
            while saves.running(processes):
                time.sleep(5)
            mapper.stop()
        threading.Thread(target=until_closed, daemon=True).start()

    # ---------- Nexus Mods ----------

    def _nexus_key(self):
        key = self._settings.get("nexus_key", "")
        if not key:
            raise MergeError("Add your Nexus Mods API key in Settings first.")
        return key

    def nexus_status(self):
        return {"has_key": bool(self._settings.get("nexus_key")), "premium": self._settings.get("nexus_premium", False),
                "name": self._settings.get("nexus_name", "")}

    def nexus_set_key(self, key):
        self._admin()
        key = (key or "").strip()
        if not key:
            for k in ("nexus_key", "nexus_premium", "nexus_name"):
                self._settings.pop(k, None)
            paths.save_settings(self._settings)
            return self.nexus_status()
        try:
            acct = nexus.account(key)
        except nexus.NexusError as e:
            raise MergeError(str(e))
        self._settings.update(nexus_key=key, nexus_premium=acct["premium"], nexus_name=acct["name"])
        paths.save_settings(self._settings)
        if getattr(self, "_register_nxm", None):
            self._register_nxm()
        return self.nexus_status()

    def nexus_search(self, recipe_id, text):
        r = self._recipe(recipe_id)
        try:
            return nexus.search(r.nexus_domain, text)
        except nexus.NexusError as e:
            raise MergeError(str(e))

    def nexus_get(self, recipe_id, mod_id):
        """Premium: download the mod's main file now. Free: open its Files page, where "Mod Manager
        Download" sends it back to the app."""
        self._perm("mods", "download mods")
        r = self._recipe(recipe_id)
        key = self._settings.get("nexus_key", "")
        if not key or not self._settings.get("nexus_premium"):
            webbrowser.open(nexus.files_page(r.nexus_domain, mod_id))
            return None

        def work(job):
            f = nexus.main_file(r.nexus_domain, mod_id, key)
            job.write("Downloading %s ..." % f.get("file_name", f.get("name", "")))
            path = nexus.download(r.nexus_domain, mod_id, f["file_id"], key,
                                  progress=lambda d, t: setattr(job, "progress", [d, t] if t else None))
            return {"path": path, "name": os.path.basename(path)}
        return self._run(work)

    def nexus_link(self, link):
        """Download from a "Mod Manager Download" (nxm://) link."""
        info = nexus.parse_nxm(link)
        if not info:
            raise MergeError("That Nexus link didn't look right.")
        key = self._nexus_key()

        def work(job):
            job.write("Downloading from Nexus Mods ...")
            path = nexus.download(info["domain"], info["mod"], info["file"], key, info["key"], info["expires"],
                                  progress=lambda d, t: setattr(job, "progress", [d, t] if t else None))
            return {"path": path, "name": os.path.basename(path), "domain": info["domain"]}
        return self._run(work)

    def open_url(self, url):
        if url.startswith(("https://", "http://")):
            webbrowser.open(url)

    def pick_mod_files(self):
        return self._dialog("files", multiple=True, title="Pick downloaded mod files")

    def pick_file(self):
        return (self._dialog("files", title="Pick the downloaded file") or [None])[0]

    def pick_folder(self):
        return (self._dialog("folder", title="Where should merged copies go?") or [None])[0]

    def check_mods(self, recipe_id, mods):
        """mods: [{slot, path}] -> where each mod's files go, plus clashes between them."""
        r = self._recipe(recipe_id)
        out, previews = [], {}
        for m in mods:
            name = os.path.basename(m["path"])
            try:
                rels, unknown = preview(m["path"], r.layout)
                previews[name] = rels
                out.append({"file": name, "slot": m["slot"], "paths": rels, "unknown": unknown, "error": None})
            except Exception as e:
                out.append({"file": name, "slot": m["slot"], "paths": [], "unknown": [], "error": str(e)})
        return {"mods": out, "clashes": find_clashes(previews)}

    # ---------- merging ----------

    def start_merge(self, p):
        self._perm("mods", "make merges")
        g = self._game(p["key"])
        r = g.recipe
        t = r.theme(p["theme_id"])
        self._settings.update(mode=p["mode"], destination=p["destination"])
        paths.save_settings(self._settings)
        frameworks = [r.framework(fid) for fid in p["frameworks"]]
        manual = p.get("manual_files", {})

        def work(job):
            job.write("Getting %s ready…" % p["name"])
            needs = permissions.folders_needing_access(
                [g.path] if p["mode"] != MODE_COPY else [], [] if p["mode"] == MODE_INPLACE else [p["destination"]])
            if needs:
                job.write("Asking Windows for permission to change: %s" % ", ".join(needs))
                if not permissions.grant(needs):
                    raise MergeError("Frankenstein needs permission to change %s. Press Merge again and click Yes "
                                     "when Windows asks." % needs[0])
            merge = self._new_merge(g, t, p["mode"], p["destination"], p["name"], job, p.get("tagline", ""))
            problems = []
            if self._is_mc(r):
                try:
                    self._mc_setup(merge, g.path, p.get("mc_loader") or "fabric", p.get("mc_version"), job.write)
                except (modsources.SourceError, OSError) as e:
                    problems.append("Mod loader: %s" % e)
                    job.write("✖ Mod loader failed: %s" % e)
            for fw in frameworks:
                try:
                    if fw.id in manual:
                        archive = manual[fw.id]
                    elif fw.github or fw.scrape:
                        try:
                            archive = downloader.fetch_framework(fw, log=job.write)
                        except Exception:
                            if not fw.manual_url:
                                raise
                            raise MergeError("couldn't download it automatically. Get it from %s, then use "
                                             "Add mods on this merge." % fw.manual_url)
                    else:
                        job.write("Skipped %s (no file picked)." % fw.name)
                        continue
                    merge.install(archive, fw.name, r.layout, flat_root=fw.flat_root, subdir=fw.subdir, log=job.write)
                except Exception as e:
                    problems.append("%s: %s" % (fw.name, e))
                    job.write("✖ %s failed: %s" % (fw.name, e))
            for m in p.get("mods", []):
                name = os.path.basename(m["path"])
                try:
                    merge.install(m["path"], "%s (%s)" % (name, m["slot"]), r.layout, log=job.write, check=scan.scan)
                except Exception as e:
                    problems.append("%s: %s" % (name, e))
                    job.write("✖ %s failed: %s" % (name, e))
            job.write("Done.")
            return {"merge_id": merge.id, "name": merge.name, "problems": problems}
        return self._run(work)

    def _mc_setup(self, merge, root, loader, version, log):
        """A Minecraft merge: the loader in the launcher, and a launcher profile playing from the merge's folder."""
        version = version or minecraft.pick_version(loader)
        vid = minecraft.install_loader(root, loader, version, log)
        minecraft.add_profile(root, merge.id, merge.name, merge.target_path, vid)
        merge.data.update(mc_root=root, mc_loader=loader, mc_version=version, mc_version_id=vid)
        merge.save()
        log("Added “%s” to the Minecraft Launcher. Worlds you make in it stay in this merge." % merge.name)

    def _rundown_stale(self, m):
        """The mods changed, so the rundown shown on Play gets written again."""
        if m.data.pop("rundown", None) is not None or m.data.pop("rundown_creative", None) is not None:
            m.save()

    def _new_merge(self, g, t, mode, destination, name, job, tagline=""):
        """Copy the base game, give the merge its cover info and its own saves."""
        r = g.recipe
        base = g.path
        if self._is_mc(r):
            # a Minecraft merge is a fresh instance folder; the game itself stays in the launcher
            base, mode = paths.sub("mc-empty"), MODE_COPY
        merge = Merge.create(r, t, base, destination, mode, name,
                             appid=g.appid if g.source == "Steam" else "", log=job.write,
                             progress=lambda d, n: setattr(job, "progress", [d, n]))
        merge.data["cover_appid"] = covers.appid_for(g) or ""
        merge.data["source_appid"] = t.source_appid
        merge.data["source_name"] = t.from_game.split(" /")[0]
        merge.data["tagline"] = (tagline or "")[:90]
        merge.data["owner"] = self._settings.pid
        merge.save()
        if r.saves:
            saves.create_slot(merge.id, r, log=job.write)
            job.write("This merge has its own save games, so your normal saves stay safe.")
        job.progress = None
        n = self._bump("merges")
        self._award("first_merge")
        if n >= 5:
            self._award("five_merges")
        return merge

    # ---------- gallery ----------

    def _start_hotkey(self):
        def name():
            try:
                return Merge.load(self._playing).name if self._playing else "Other"
            except (OSError, ValueError):
                return "Other"
        self._shots = 0
        def shot(_path):
            self._shots += 1
            if self._bump("shots") >= 10:
                self._award("photographer")
        gallery.listen(name, shot)

    def gallery(self):
        return {"hotkey": gallery.HOTKEY, "items": [dict(i, thumb=None) for i in gallery.items()]}

    def gallery_thumb(self, path):
        if path.lower().endswith(".png") and any(i["path"] == path for i in gallery.items(1000)):
            return gallery.thumb(path)
        return None

    def open_path(self, path):
        if sys.platform == "win32" and any(i["path"] == path for i in gallery.items(1000)):
            os.startfile(path)
        return True

    def shots_taken(self):
        return getattr(self, "_shots", 0)

    # ---------- updates ----------

    def version(self):
        return VERSION

    def check_update(self):
        try:
            self._update = updater.latest()
        except Exception:
            self._update = None  # offline or no releases yet: try again next start
        return self._update

    def install_update(self):
        info = getattr(self, "_update", None) or updater.latest()
        if not info:
            raise MergeError("You're already on the newest version.")

        def work(job):
            job.write("Downloading Frankenstein %s ..." % info["version"])
            setup = updater.download(info, progress=lambda d, n: setattr(job, "progress", [d, n]))
            job.write("Installing. Frankenstein will close and open again in a few seconds.")
            updater.run_installer(setup)
            threading.Timer(1.5, self._quit).start()
            return {"version": info["version"]}
        return self._run(work)

    def _quit(self):
        try:
            if self._window is not None:
                self._window.destroy()
        finally:
            os._exit(0)

    # ---------- play together ----------

    def party(self):
        """Everything the Play together screen needs."""
        out = {"ready": network.ready(), "installed": network.installed(), "party": None, "error": None,
               "games": [{"name": r.name, "how": r.multiplayer} for r in self._recipes if r.multiplayer]}
        code = self._settings.get("party")
        if out["ready"] and code:
            try:
                out["party"] = network.status(code)
            except network.NetworkError as e:
                out["error"] = str(e)
        return out

    def party_setup(self):
        def work(job):
            network.setup(lambda url, dest: downloader.download_file(url, dest), log=job.write)
            job.write("All set. You can host or join a party now.")
            return True
        return self._run(work)

    def party_host(self):
        self._perm("online", "host online parties")
        code = network.host()
        self._award("party_host")
        self._settings["party"] = code
        paths.save_settings(self._settings)
        return network.status(code)

    def party_join(self, code):
        self._perm("online", "join online parties")
        code = network.join(code)
        self._award("party_join")
        self._settings["party"] = code
        paths.save_settings(self._settings)
        return network.status(code)

    def party_leave(self):
        code = self._settings.pop("party", None)
        paths.save_settings(self._settings)
        if code:
            try:
                network.leave(code, end=network.is_host(code))
            except network.NetworkError:
                pass
        return True

    def party_let_in(self, member, name=""):
        network.let_in(self._settings["party"], member, name)
        return network.status(self._settings["party"])

    def party_kick(self, member):
        network.kick(self._settings["party"], member)
        return network.status(self._settings["party"])

    def copy_text(self, text):
        if sys.platform == "win32":
            subprocess.run(["clip"], input=text.encode("utf-16-le"), creationflags=0x08000000)
        return True

    # ---------- sharing ----------

    def share_merge(self, merge_id):
        """Pack a merge into a .gmerge file in Downloads and show it in Explorer."""
        self._perm("online", "share merges")
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))

        def work(job):
            out = sharing.export(m, r, saves.known_folder("Downloads"), log=job.write)
            job.write("Saved %s" % out)
            self._reveal(out)
            self._award("shared")
            return {"path": out, "size": os.path.getsize(out)}
        return self._run(work)

    def _reveal(self, path):
        if sys.platform == "win32":
            subprocess.Popen(["explorer", "/select,", path])

    def pick_share_file(self):
        return (self._dialog("files", title="Pick the merge your friend sent", types=SHARE_TYPES) or [None])[0]

    # ---------- achievements ----------

    def _award(self, key):
        with self._lock:
            new = achievements.award(self._settings, key)
            if new:
                paths.save_settings(self._settings)
                self._new_awards = getattr(self, "_new_awards", []) + [key]
        return new

    def _bump(self, counter, n=1):
        with self._lock:
            v = achievements.count(self._settings, counter, n)
            paths.save_settings(self._settings)
        return v

    def achievements(self):
        return achievements.board(self._settings)

    def achievements_new(self):
        """Achievements unlocked since the page last asked (each shown once)."""
        keys, self._new_awards = getattr(self, "_new_awards", []), []
        return [{"id": k, "title": achievements.LIST[k][0], "how": achievements.LIST[k][1]} for k in keys]

    def controller_used(self):
        self._award("controller")

    def handoffs(self):
        """.gmerge files and nxm:// links the app was opened with or handed since (taken once)."""
        items = list(getattr(self, "_handoffs", []))
        del getattr(self, "_handoffs", [])[:]
        return items

    def _base_for(self, info):
        """The installed game a shared merge is built on. Recipes made on the fly have a different id on
        each PC, so those are matched by the game's name."""
        rid = info.get("recipe_id") or ""
        for x in self._games:
            if x.recipe and (x.recipe.id == rid or (rid.startswith("auto-") and getattr(x.recipe, "auto", None)
                                                    and x.recipe.name.lower() == (info.get("game_name") or "").lower())):
                return x
        return None

    def import_info(self, path):
        info = sharing.read(path)
        g = self._base_for(info)
        r = g.recipe if g else self._recipe(info.get("recipe_id"))
        return {"path": path, "name": info["name"], "game_name": info.get("game_name", ""),
                "theme_name": info.get("theme_name", ""), "mods": [i["label"] for i in info["installs"]],
                "size": info["size"], "have_game": bool(g), "known_recipe": bool(r),
                "version_note": sharing.version_note(info, g.path, r.exe) if g and r else ""}

    def import_merge(self, path):
        self._perm("mods", "add merges from friends")
        info = sharing.read(path)
        g = self._base_for(info)
        if not g:
            raise MergeError("You need %s installed to play this merge." % info.get("game_name", "the base game"))
        r = g.recipe
        try:
            t = r.theme(info.get("theme_id"))
        except KeyError:
            t = None
        if t is None and (info.get("theme_id") or "").startswith("style-"):
            # a style from a game this PC doesn't have: the mods come in the file anyway
            t = autorecipe.style_themes(r.id, r.name, [(info.get("theme_name") or "Other game").rsplit(" style", 1)[0]],
                                        list(r.frameworks))[0]
        if t is None:
            raise MergeError("This merge uses a style this version of Frankenstein doesn't know. Update the app.")
        mode = self._settings.get("mode", MODE_HARDLINK)
        mode = MODE_HARDLINK if mode == MODE_INPLACE else mode  # never change a friend's original install
        dest = self._settings.get("destination") or default_destination(g.path)

        def work(job):
            job.write("Unpacking %s ..." % info["name"])
            info2, folder, items = sharing.unpack(path, log=job.write)
            try:
                needs = permissions.folders_needing_access([g.path], [dest])
                if needs and not permissions.grant(needs):
                    raise MergeError("Frankenstein needs permission to change %s." % needs[0])
                merge = self._new_merge(g, t, mode, dest, info["name"], job)
                merge.data["shared_from"] = os.path.basename(path)
                self._award("imported")
                merge.save()
                problems = []
                for inst, archive in items:
                    try:
                        merge.install(archive, inst["label"], r.layout, flat_root=inst.get("flat_root", False),
                                      subdir=inst.get("subdir", ""), log=job.write, check=scan.scan)
                    except Exception as e:
                        problems.append("%s: %s" % (inst["label"], e))
                        job.write("✖ %s failed: %s" % (inst["label"], e))
                job.write("Done. It's the same merge your friend has.")
                return {"merge_id": merge.id, "name": merge.name, "problems": problems}
            finally:
                shutil.rmtree(folder, ignore_errors=True)
        return self._run(work)

    def job(self, job_id, since=0):
        j = self._jobs[job_id]
        return {"log": j.log[since:], "next": len(j.log), "progress": j.progress,
                "done": j.done, "error": j.error, "result": j.result}

    # ---------- my merges ----------

    def merges(self):
        kinds = {MODE_HARDLINK: "Fast copy", MODE_COPY: "Full copy", MODE_INPLACE: "Original (backed up)"}
        out = []
        me = self._me()
        for m in Merge.all():
            owner = m.data.get("owner")
            if owner != me.get("id") and not (owner is None and me.get("admin")):
                continue  # someone else's merge
            out.append({"id": m.id, "name": m.name, "tagline": m.data.get("tagline", ""), "game": m.data.get("game_name", ""),
                        "recipe_id": m.data.get("recipe_id"), "theme": m.data.get("theme_name", ""),
                        "mode": m.mode, "mode_label": kinds.get(m.mode, m.mode), "folder": m.target_path,
                        "created": m.data.get("created", ""), "status": m.data.get("status", ""),
                        "mods": [i["label"] for i in m.data.get("installs", [])],
                        "replaced": sum(1 for e in m.data.get("files", {}).values() if e.get("replaced")),
                        "cover_appid": m.data.get("cover_appid", ""),
                        "has_saves": os.path.isdir(saves.slot_root(m.id)),
                        "offline_lock": bool(self._recipe(m.data.get("recipe_id")) and
                                             self._recipe(m.data.get("recipe_id")).launch_args and m.mode != MODE_INPLACE),
                        "playing": self._playing == m.id, "needs_upgrade": m.data.get("upgraded", 1) < self.UPGRADE,
                        "sources": self._sources_for(self._recipe(m.data.get("recipe_id"))),
                        "mc_loader": m.data.get("mc_loader"), "mc_version": m.data.get("mc_version"),
                        "creative": bool(self._recipe(m.data.get("recipe_id")) and self._recipe(m.data.get("recipe_id")).sandbox),
                        "pad": "native" if not self._mapper_wanted(m, self._pad_status(m.data.get("game_name", ""))) else "mapped"})
        return out

    def _cover_parts(self, m):
        root = self._steam_root()
        base = covers.steam_cover(m.data["cover_appid"], root) if m.data.get("cover_appid") else None
        if not base:
            r = self._recipe(m.data.get("recipe_id"))
            base = covers.made_cover(m.data.get("game_name", ""), m.base_path, r.exe if r else ())
        sid = m.data.get("source_appid")
        if not sid and m.data.get("source_name"):
            sid = covers.search_appid(m.data["source_name"])
        source = covers.steam_cover(sid, root) if sid else None
        merged = covers.merged_cover(base, source, m.data.get("source_name", "") or m.data.get("theme_name", ""),
                                     "%s-%s" % (m.id, "s" if source else "n"), title=m.name, tagline=m.data.get("tagline", ""))
        return base, source, merged

    def merge_cover(self, merge_id):
        """Combined cover: the base game's art with the merged-in game's art cut in."""
        return covers.as_data_uri(self._cover_parts(Merge.load(merge_id))[2])

    def fusion_art(self, merge_id):
        """The covers for the merge animation: the two games, then the one they become."""
        m = Merge.load(merge_id)
        base, source, merged = self._cover_parts(m)
        return {"name": m.name, "tagline": m.data.get("tagline", ""), "base_name": m.data.get("game_name", ""),
                "source_name": m.data.get("source_name", "") or m.data.get("theme_name", ""),
                "base": covers.as_data_uri(base), "source": covers.as_data_uri(source), "merged": covers.as_data_uri(merged)}

    def play(self, merge_id, creative=False):
        """Swap in this merge's own saves, start the game, and swap them back when it closes.
        creative: the sandbox version, with its creative mods switched on and its own separate saves."""
        if self._playing and self._playing != merge_id:
            raise MergeError("Another merged game is still running. Close it first.")
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))
        if creative and not (r and r.sandbox):
            raise MergeError("Creative mode isn't available for %s." % m.data.get("game_name", "this game"))
        if m.data.get("mc_root"):
            try:
                minecraft.select_profile(m.data["mc_root"], m.id)
            except (OSError, ValueError, modsources.SourceError) as e:
                raise MergeError(str(e))
            minecraft.launch()
            self._award("first_play")
            note = "The Minecraft Launcher is opening with “%s” picked. Press Play there." % m.name
            if creative:
                note += " Pick Creative when you make a world."
            return {"note": note}
        exe = r.launch_exe(m.target_path) if r else None
        if not exe:
            raise MergeError("Couldn't find the game's .exe in %s." % m.target_path)
        self._switch_creative(m, r, creative)
        m.data["plays"] = m.data.get("plays", 0) + 1
        m.save()
        self._award("first_play")
        if self._bump("plays") >= 10:
            self._award("ten_plays")
        slot = merge_id + "~creative" if creative else merge_id
        cloud = self._settings.get("cloud_folder")
        if cloud and r and r.saves and self._playing != merge_id and not creative:
            try:
                saves.pull_cloud(merge_id, cloud, m.name)
            except OSError:
                pass
        if r.saves and self._playing != merge_id:
            if creative and not os.path.isdir(saves.slot_root(slot)):
                # creative starts from a copy of the merge's normal saves, then goes its own way
                if os.path.isdir(saves.slot_root(merge_id)):
                    shutil.copytree(saves.slot_root(merge_id), saves.slot_root(slot))
            saves.swap_in(slot, r)
            self._playing = merge_id
            saves.watch_until_closed(r.processes or [os.path.basename(exe)], self._game_closed)
        # modded copies start offline-only where the game supports it, so mods never reach online modes;
        # creative mode always does
        args = list(r.launch_args) if r and (m.mode != MODE_INPLACE or creative) else []
        subprocess.Popen([exe] + args, cwd=os.path.dirname(exe))
        self._discord_playing(m, r.processes or [os.path.basename(exe)])
        self._start_mapper(m, r.processes or [os.path.basename(exe)])
        return True

    # ---------- creative / sandbox mode ----------

    def creative_info(self, merge_id):
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))
        sb = (r.sandbox if r else {}) or {}
        return {"available": bool(sb), "native": sb.get("native", ""), "note": sb.get("note", ""),
                "slots": sb.get("slots", []), "mods": m.data.get("creative_mods", []),
                "own_saves": bool(r and r.saves), "offline": bool(r and r.launch_args),
                "sources": self._sources_for(r), "recipe_id": r.id if r else None,
                "mc_loader": m.data.get("mc_loader"), "mc_version": m.data.get("mc_version")}

    def creative_add(self, merge_id, files):
        """Add sandbox mods (trainers, spawners, free camera). They're only switched on in creative mode."""
        self._perm("mods", "add mods")
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))
        if self._playing == merge_id:
            raise MergeError("Close the game first.")

        def work(job):
            for p in files:
                label = "[Creative] " + os.path.basename(p)
                try:
                    m.install(p, label, r.layout, log=job.write, check=scan.scan)
                    m.data.setdefault("creative_mods", []).append(label)
                except Exception as e:
                    job.write("✖ %s failed: %s" % (os.path.basename(p), e))
            m.data["creative_on"] = True
            m.save()
            self._rundown_stale(m)
            job.write("These only run in creative mode. Normal play switches them off again.")
            return True
        return self._run(work)

    def _switch_creative(self, m, r, creative):
        """Creative mods on for creative play, off for normal play (only reinstalls when it changes)."""
        extra = set(m.data.get("creative_mods", []))
        if not extra or bool(m.data.get("creative_on")) == bool(creative):
            return
        on = [x["label"] for x in m.all_mods() if x["on"] and x["label"] not in extra]
        if creative:
            on += [x["label"] for x in m.all_mods() if x["label"] in extra]
        m.rebuild(r.layout, on)
        m.data["creative_on"] = bool(creative)
        m.save()

    # ---------- the rundown shown while a merge loads ----------

    def _rundown_basic(self, m, creative):
        """The rundown without AI: what's installed, controls, quest and tips."""
        r = self._recipe(m.data.get("recipe_id"))
        extra = set(m.data.get("creative_mods", []))
        mods = [x["label"] for x in m.all_mods() if x["on"] or (creative and x["label"] in extra)]
        pretty = []
        for label in mods:
            mm = re.match(r"^(.*?)\.(?:zip|7z|rar|jar|dll|asi|pak)?\s*\((.+)\)$", label)
            pretty.append("%s: %s" % (mm.group(2), mm.group(1)) if mm else label)
        keys = []
        status = self._pad_status(m.data.get("game_name", ""))
        if self._mapper_wanted(m, status):
            mp = controller.merged_map(m.data.get("pad_map"))
            keys.append("Controller: left stick moves, right stick looks, RT = %s, LT = %s, A = %s, B = %s, Y = %s, Start = %s."
                        % (mp["rt"], mp["lt"], mp["a"], mp["b"], mp["y"], mp["start"]))
        if creative and r and r.sandbox.get("native"):
            keys.append(r.sandbox["native"])
        quest = ""
        for q in m.data.get("questlines", []):
            if not q.get("finished"):
                for i, row in enumerate(q["done"]):
                    if False in row:
                        quest = "%s: %s (next: %s)" % (q["title"], q["quests"][i]["title"], q["quests"][i]["objectives"][row.index(False)])
                        break
                break
        tips = ["This merge has its own saves, so your normal game is safe."] if r and r.saves else []
        if creative:
            tips.insert(0, "Creative mode has its own saves too, and it always starts offline where the game allows it.")
        tips.append("If it crashes, press Fix crash in My merges.")
        return {"headline": "%s: %s with %s style%s" % (m.name, m.data.get("game_name", ""), m.data.get("source_name") or m.data.get("theme_name", ""),
                                                         " · Creative" if creative else ""),
                "tagline": m.data.get("tagline", ""), "new": pretty[:8], "try": [], "keys": keys, "quest": quest, "tips": tips,
                "mods": mods}

    def merge_rundown(self, merge_id, creative=False):
        """{"headline", "new", "try", "keys", "quest", "tips", "ready", "show"}. The AI version is made in the
        background (once per set of mods) and the plain one is shown until it's ready."""
        m = Merge.load(merge_id)
        basic = self._rundown_basic(m, creative)
        key = "rundown_creative" if creative else "rundown"
        sig = "|".join(basic["mods"])
        got = m.data.get(key)
        show = m.data.get("rundown_on_play", True)
        if got and got.get("sig") == sig:
            return dict(basic, **{k: got[k] for k in ("headline", "new", "try", "keys", "tips") if got.get(k)},
                        keys=basic["keys"] + got.get("keys", []), ready=True, show=show)
        busy = getattr(self, "_rundown_busy", set())
        self._rundown_busy = busy
        if not self._settings.get("claude_api_key"):
            return dict(basic, ready=True, show=show)
        if (merge_id, creative) not in busy:
            busy.add((merge_id, creative))

            def run():
                try:
                    out = self._ai().rundown(m.data.get("game_name", ""), basic["headline"].split(" with ", 1)[-1],
                                             m.name, basic["mods"], basic["quest"], "; ".join(basic["keys"])[:300], creative)
                    fresh = Merge.load(merge_id)
                    fresh.data[key] = dict(out, sig=sig)
                    fresh.save()
                except Exception:
                    pass
                finally:
                    busy.discard((merge_id, creative))
            threading.Thread(target=run, daemon=True).start()
        return dict(basic, ready=False, show=show)

    def rundown_pref(self, merge_id, show):
        m = Merge.load(merge_id)
        m.data["rundown_on_play"] = bool(show)
        m.save()
        return True

    def screens(self):
        """How many screens there are (for "show it on my other screen")."""
        if self._window is None:
            return 0
        try:
            import webview
            return len(webview.screens)
        except Exception:
            return 1

    def rundown_window(self, merge_id, where="second", creative=False):
        """Show the rundown in its own window: on the second screen, or small and always on top."""
        if self._window is None:
            return False
        import webview
        import html as h
        d = self.merge_rundown(merge_id, creative)
        sec = lambda title, xs: ("<h3>%s</h3><ul>%s</ul>" % (title, "".join("<li>%s</li>" % h.escape(x) for x in xs))) if xs else ""
        page = ("<!doctype html><meta charset=utf-8><body style='margin:0;background:#0c0912;color:#ece8f5;font:15px/1.5 Segoe UI,sans-serif;"
                "padding:22px'><div style='color:#a78bfa;font-size:12px;letter-spacing:2px;text-transform:uppercase'>Now playing</div>"
                "<h2 style='margin:4px 0 12px'>%s</h2>%s%s%s%s%s</body>" % (
                    h.escape(d["headline"]), sec("What's new", d["new"]), sec("Try first", d["try"]),
                    sec("Keys and buttons", d["keys"]), sec("Quest", [d["quest"]] if d["quest"] else []), sec("Tips", d["tips"])))
        try:
            screens = list(webview.screens)
        except Exception:
            screens = []
        if where == "second" and len(screens) > 1:
            sc = screens[1]
            webview.create_window("Rundown: " + Merge.load(merge_id).name, html=page, x=sc.x + 40, y=sc.y + 40,
                                  width=min(560, sc.width - 80), height=min(900, sc.height - 80))
        else:
            sc = screens[0] if screens else None
            w, hgt = 420, 620
            x = (sc.x + sc.width - w - 30) if sc else None
            y = (sc.y + 60) if sc else None
            webview.create_window("Rundown", html=page, x=x, y=y, width=w, height=hgt, on_top=True)
        return True

    # ---------- questlines ----------

    def questlines(self, merge_id):
        return Merge.load(merge_id).data.get("questlines", [])

    def questline_new(self, merge_id, idea=""):
        """The AI writes a questline for this merge (runs as a job)."""
        m = Merge.load(merge_id)
        bot = self._ai()

        def work(job):
            job.write("Writing your questline …")
            try:
                q = bot.questline(m.data.get("game_name", ""), m.data.get("source_name") or m.data.get("theme_name", ""),
                                  m.name, [i["label"] for i in m.data.get("installs", [])], (idea or "").strip()[:300])
            except ai.AIError as e:
                raise MergeError(str(e))
            q["id"] = uuid.uuid4().hex[:8]
            q["done"] = [[False] * len(x["objectives"]) for x in q["quests"]]
            q["created"] = time.time()
            m.data.setdefault("questlines", []).insert(0, q)
            m.save()
            job.write("Done: %s" % q["title"])
            return q
        return self._run(work)

    def questline_tick(self, merge_id, qid, quest, objective, done):
        m = Merge.load(merge_id)
        q = next((x for x in m.data.get("questlines", []) if x["id"] == qid), None)
        if not q:
            raise MergeError("That questline is gone.")
        q["done"][quest][objective] = bool(done)
        finished = all(all(row) for row in q["done"])
        if finished and not q.get("finished"):
            q["finished"] = time.time()
            self._award("quest_done")
        m.save()
        return q

    def questline_delete(self, merge_id, qid):
        m = Merge.load(merge_id)
        m.data["questlines"] = [x for x in m.data.get("questlines", []) if x["id"] != qid]
        m.save()
        return True

    # ---------- Discord status ----------

    def _discord_id(self):
        return self._settings.get("discord_app_id") or discord.APP_ID

    def _discord_playing(self, m, processes):
        if not self._settings.get("discord_on", True) or not self._discord_id():
            return

        def run():
            started = time.time()
            shown = self._presence.show(self._discord_id(), m.name,
                                        "%s × %s" % (m.data.get("game_name", ""), m.data.get("source_name") or m.data.get("theme_name", "")),
                                        started)
            if not shown:
                return
            time.sleep(20)  # give the game time to start before watching for it to close
            while saves.running(processes):
                time.sleep(5)
            self._presence.clear(self._discord_id())
        threading.Thread(target=run, daemon=True).start()

    def discord_set(self, on=None, app_id=None):
        if on is not None:
            self._settings["discord_on"] = bool(on)
        if app_id is not None:
            self._admin()
            app_id = re.sub(r"\D", "", app_id)
            if app_id and len(app_id) < 15:
                raise MergeError("That doesn't look like a Discord Application ID (a long number).")
            self._settings["discord_app_id"] = app_id
        paths.save_settings(self._settings)
        return {"on": self._settings.get("discord_on", True), "has_id": bool(self._discord_id()), "built_in": bool(discord.APP_ID)}

    def playing(self):
        return self._playing

    def open_folder(self, merge_id):
        p = Merge.load(merge_id).target_path
        if sys.platform == "win32":
            os.startfile(p)
        else:
            subprocess.Popen(["xdg-open", p])

    def add_mods(self, merge_id):
        files = self.pick_mod_files()
        if not files:
            return None
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))

        def work(job):
            for p in files:
                try:
                    m.install(p, os.path.basename(p), r.layout, log=job.write, check=scan.scan)
                except Exception as e:
                    job.write("✖ %s failed: %s" % (os.path.basename(p), e))
            self._rundown_stale(m)
        return self._run(work)

    # ---------- mod profiles ----------

    def merge_mods(self, merge_id):
        m = Merge.load(merge_id)
        return {"mods": [{"label": x["label"], "on": x["on"], "kept": bool(m.kept_archive(x))} for x in m.all_mods()],
                "profiles": m.data.get("profiles", {}), "profile": m.data.get("profile", "")}

    def set_mods(self, merge_id, enabled, profile=""):
        """Switch mods on/off (optionally saving the set as a named profile, like "Chill" or "Chaos")."""
        self._perm("mods", "switch mods")
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))
        if self._playing == merge_id:
            raise MergeError("Close the game first, then switch mods.")

        def work(job):
            m.rebuild(r.layout, enabled, log=job.write)
            if profile:
                m.data.setdefault("profiles", {})[profile] = list(enabled)
            m.data["profile"] = profile
            m.save()
            if "chaos" in profile.lower():
                self._award("chaos")
            job.write("Done. %d mod(s) on." % len(enabled))
            return True
        return self._run(work)

    def delete_profile(self, merge_id, name):
        m = Merge.load(merge_id)
        m.data.get("profiles", {}).pop(name, None)
        if m.data.get("profile") == name:
            m.data["profile"] = ""
        m.save()
        return self.merge_mods(merge_id)

    def fix_crash(self, merge_id):
        """One-click crash repair: the AI reads the logs, the mod causing it is turned off, and the game restarts."""
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))
        if self._playing == merge_id and saves.running(r.processes if r else []):
            raise MergeError("The game is still running. Close it first.")
        bot = self._ai()

        def work(job):
            job.write("Reading the crash logs …")
            logs = ai.read_logs(m.target_path, r.logs if r else [])
            if not logs:
                raise MergeError("There are no log files yet. Play until it crashes, then press Fix it again.")
            on = [x["label"] for x in m.all_mods() if x["on"]]
            ctx = ai.build_context({"base_game": m.data.get("game_name"), "theme": m.data.get("theme_name"),
                                          "logs": logs})
            try:
                verdict = bot.diagnose(ctx, on)
            except ai.AIError as e:
                raise MergeError(str(e))
            job.write(verdict["why"])
            if not verdict["culprit"]:
                return {"fixed": False, "why": verdict["why"]}
            if not m.kept_archive(next(x for x in m.all_mods() if x["label"] == verdict["culprit"])):
                return {"fixed": False, "why": verdict["why"] + " Remove that mod with Undo all mods, then add the others back."}
            job.write("Turning off %s …" % verdict["culprit"])
            m.rebuild(r.layout, [x for x in on if x != verdict["culprit"]], log=job.write)
            m.data["profile"] = ""
            m.save()
            job.write("Starting the game again …")
            self.play(merge_id)
            self._award("crash_fixed")
            return {"fixed": True, "culprit": verdict["culprit"], "why": verdict["why"]}
        return self._run(work)

    def undo(self, merge_id):
        m = Merge.load(merge_id)
        return self._run(lambda job: m.undo(log=job.write))

    def delete(self, merge_id):
        if self._playing == merge_id:
            raise MergeError("Close the game before deleting this merge.")
        m = Merge.load(merge_id)

        def work(job):
            m.delete(log=job.write)
            saves.delete_slot(merge_id)
            if m.data.get("mc_root"):
                minecraft.remove_profile(m.data["mc_root"], merge_id)
        return self._run(work)

    # ---------- first run and downloads ----------

    def setup_status(self):
        """Folders Frankenstein still needs permission for (asked once, on first run)."""
        bases = [g.path for g in self._games if g.recipe and not getattr(g.recipe, "auto", None)]
        dest = self._settings.get("destination") or (default_destination(bases[0]) if bases else None)
        needs = permissions.folders_needing_access(bases, [dest] if dest else [])
        return {"done": bool(self._settings.get("setup_done")) and not needs, "needs": needs}

    def run_setup(self):
        needs = self.setup_status()["needs"]
        ok = permissions.grant(needs)
        if ok:
            self._settings["setup_done"] = True
            paths.save_settings(self._settings)
        return ok

    def new_downloads(self, since):
        """Mod archives that appeared in the Downloads folder after `since` (seconds since epoch)."""
        folder = saves.known_folder("Downloads")
        out = []
        try:
            names = os.listdir(folder)
        except OSError:
            return out
        now = time.time()
        for n in names:
            p = os.path.join(folder, n)
            if not n.lower().endswith((".zip", ".7z", ".rar")) or not os.path.isfile(p):
                continue
            mt = os.path.getmtime(p)
            if since < mt < now - 2:  # finished downloading
                out.append({"path": p, "name": n, "time": mt})
        return sorted(out, key=lambda d: d["time"])

    def now(self):
        return time.time()

    # ---------- window (borderless, so the page has its own buttons) ----------

    def win_state(self):
        return {"framed": self._window is not None, "fullscreen": self._settings.get("fullscreen", True)}

    def win_minimize(self):
        if self._window is not None:
            self._window.minimize()

    def win_close(self):
        if self._window is not None:
            self._window.destroy()

    def win_fullscreen(self, on):
        """Switch between full screen and a window. Remembered for next time."""
        on = bool(on)
        if self._window is not None and on != self._settings.get("fullscreen", True):
            self._window.toggle_fullscreen()
        self._settings["fullscreen"] = on
        paths.save_settings(self._settings)
        return self.win_state()

    # ---------- settings ----------

    def get_settings(self):
        key = self._settings.get("claude_api_key", "")
        me = self._me()
        return {"has_key": bool(key), "key_hint": ("…" + key[-4:]) if key else "",
                "admin": bool(me.get("admin")), "perms": dict(profiles.DEFAULT_PERMS, **me.get("perms", {})),
                "mode": self._settings.get("mode", MODE_HARDLINK),
                "destination": self._settings.get("destination", ""),
                "data_folder": paths.data_dir(),
                "version": VERSION,
                "fun_style": self._settings.get("fun_style", "dot"),
                "has_curseforge": bool(self._settings.get("curseforge_key")),
                "cloud_folder": self._settings.get("cloud_folder", ""),
                "cloud_options": saves.cloud_folders()}

    def save_settings(self, values):
        shared = {"mode", "destination", "claude_api_key"}
        if shared & {k for k, v in values.items() if v is not None} and not self._me().get("admin"):
            raise MergeError("Only the admin account can change keys and where copies go.")
        for k in ("mode", "destination", "cloud_folder", "fun_style"):
            if k in values:
                self._settings[k] = values[k]
        if values.get("claude_api_key") is not None:
            self._settings["claude_api_key"] = values["claude_api_key"].strip()
        paths.save_settings(self._settings)
        return self.get_settings()
