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

from . import accounts, covers, detect, downloader, franklin, gallery, network, nexus, paths, permissions, saves, scan, sharing, specs, updater, voice
from .version import VERSION
from .installer import MODE_COPY, MODE_HARDLINK, MODE_INPLACE, Merge, MergeError, find_clashes, preview
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
        self._settings = paths.load_settings()
        if not self._settings.get("voice_defaults_v2"):
            # Andrew is Franklin's voice now; the cloned XTTS voice is opt-in from Settings
            if self._settings.get("voice_style") == "own":
                self._settings["voice_style"] = voice.DEFAULT_VOICE
            self._settings["voice_defaults_v2"] = True
            paths.save_settings(self._settings)
        if self._settings.get("voice_style") != "own":
            threading.Thread(target=voice.LocalVoice.stop_leftover, daemon=True).start()
        self._recipes = load_recipes([resource_dir(), paths.user_recipes_dir()])
        self._games = []
        self._jobs = {}
        self._chat_history = []
        self._chat_streams = {}
        self._lock = threading.Lock()
        self._playing = None
        self._recover_saves()
        self._start_hotkey()

    # ---------- helpers ----------

    def _recipe(self, rid):
        return next((r for r in self._recipes if r.id == rid), None)

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
            r = self._recipe(Merge.load(state["merge_id"]).data.get("recipe_id"))
            if r and saves.running(r.processes):
                self._playing = state["merge_id"]
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
        self._games = detect.scan(self._recipes, self._settings.get("manual_folders", []))
        names = [g.name.lower() for g in self._games]
        out = []
        for i, g in enumerate(self._games):
            themes = []
            if g.recipe:
                for t in g.recipe.themes:
                    found = next((n for n in names if any(m in n for m in t.source_match)), None)
                    themes.append({"id": t.id, "name": t.name, "from_game": t.from_game,
                                   "description": t.description, "found": bool(found)})
            out.append({"key": str(i), "name": g.name, "source": g.source, "path": g.path,
                        "base": g.recipe is not None, "recipe_id": g.recipe.id if g.recipe else None,
                        "warning": g.recipe.warning if g.recipe else "", "themes": themes})
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
        self._owned = {g["key"]: g for g in games}
        return {"games": games, "errors": errors, "sources": sources}

    def fun_scores(self, base_key, names):
        """Franklin's fun rating for merging each named game into the base game. Cached per base game,
        so each pairing is only asked once. Without a Claude key, only the styles Frankenstein has
        recipes for get rated."""
        g = self._game(base_key)
        path = os.path.join(paths.sub("library"), "fun-scores.json")
        try:
            with open(path, encoding="utf-8") as f:
                cache = json.load(f)
        except (OSError, ValueError):
            cache = {}
        mine = cache.setdefault(g.name, {})
        if g.recipe:  # the styles we have recipes for are hand-picked pairings
            for t in g.recipe.themes:
                for n in names:
                    if n not in mine and any(m in n.lower() for m in t.source_match):
                        mine[n] = {"rating": "green", "why": "Ready-made merge: " + t.name}
        todo = [n for n in names if n not in mine and n != g.name][:150]
        if todo and self._settings.get("claude_api_key"):
            try:
                bot = franklin.Franklin(self._settings["claude_api_key"], self._settings.get("explicit", True))
                for i in range(0, len(todo), 50):
                    mine.update(bot.fun_scores(g.name, todo[i:i + 50]))
            except Exception:  # offline, bad key, odd answer: show what we have, ask again next time
                pass
        with open(path, "w", encoding="utf-8") as f:
            json.dump(cache, f)
        return {n: mine[n] for n in names if n in mine}

    def merge_preview(self, base_key, theme_id):
        """What a merge could look like, before anything is downloaded: low-res trailers streamed from
        Steam (the base game and the game whose style comes in), pictures of popular mods for each part
        of the merge from Nexus Mods, and Franklin's quick pitch. It's a preview of the parts, not a render
        of your merged game."""
        g = self._game(base_key)
        r = g.recipe
        t = next((x for x in (r.themes if r else []) if x.id == theme_id), None)
        if not t:
            raise MergeError("Pick a style to merge in first.")
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
                bot = franklin.Franklin(self._settings["claude_api_key"], self._settings.get("explicit", True))
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
        return {
            "frameworks": [{"id": fid, "name": r.framework(fid).name, "manual_url": r.framework(fid).manual_url}
                           for fid in t.frameworks],
            "slots": [{"name": s.name, "search": s.search} for s in t.slots],
            "default_name": "%s - %s" % (r.name.split(" (")[0], t.from_game.split(" /")[0]),
            "destination": self._settings.get("destination") or default_destination(g.path),
            "mode": self._settings.get("mode", MODE_HARDLINK),
        }

    def find_mods(self, key, query):
        r = self._game(key).recipe
        webbrowser.open("https://www.nexusmods.com/games/%s/mods?keyword=%s"
                        % (r.nexus_domain, urllib.parse.quote(query)))

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
        r = self._recipe(recipe_id)
        key = self._nexus_key()
        if not self._settings.get("nexus_premium"):
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
            merge = self._new_merge(g, t, p["mode"], p["destination"], p["name"], job)
            problems = []
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

    def _new_merge(self, g, t, mode, destination, name, job):
        """Copy the base game, give the merge its cover info and its own saves."""
        r = g.recipe
        merge = Merge.create(r, t, g.path, destination, mode, name,
                             appid=g.appid if g.source == "Steam" else "", log=job.write,
                             progress=lambda d, n: setattr(job, "progress", [d, n]))
        merge.data["cover_appid"] = covers.appid_for(g) or ""
        merge.data["source_appid"] = t.source_appid
        merge.data["source_name"] = t.from_game.split(" /")[0]
        merge.save()
        if r.saves:
            saves.create_slot(merge.id, r, log=job.write)
            job.write("This merge has its own save games, so your normal saves stay safe.")
        job.progress = None
        return merge

    # ---------- gallery ----------

    def _start_hotkey(self):
        def name():
            try:
                return Merge.load(self._playing).name if self._playing else "Other"
            except (OSError, ValueError):
                return "Other"
        self._shots = 0
        gallery.listen(name, lambda p: setattr(self, "_shots", self._shots + 1))

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
        code = network.host()
        self._settings["party"] = code
        paths.save_settings(self._settings)
        return network.status(code)

    def party_join(self, code):
        code = network.join(code)
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
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))

        def work(job):
            out = sharing.export(m, r, saves.known_folder("Downloads"), log=job.write)
            job.write("Saved %s" % out)
            self._reveal(out)
            return {"path": out, "size": os.path.getsize(out)}
        return self._run(work)

    def _reveal(self, path):
        if sys.platform == "win32":
            subprocess.Popen(["explorer", "/select,", path])

    def pick_share_file(self):
        return (self._dialog("files", title="Pick the merge your friend sent", types=SHARE_TYPES) or [None])[0]

    def handoffs(self):
        """.gmerge files and nxm:// links the app was opened with or handed since (taken once)."""
        items = list(getattr(self, "_handoffs", []))
        del getattr(self, "_handoffs", [])[:]
        return items

    def import_info(self, path):
        info = sharing.read(path)
        r = self._recipe(info.get("recipe_id"))
        g = next((x for x in self._games if x.recipe and x.recipe.id == info.get("recipe_id")), None)
        return {"path": path, "name": info["name"], "game_name": info.get("game_name", ""),
                "theme_name": info.get("theme_name", ""), "mods": [i["label"] for i in info["installs"]],
                "size": info["size"], "have_game": bool(g), "known_recipe": bool(r),
                "version_note": sharing.version_note(info, g.path, r.exe) if g and r else ""}

    def import_merge(self, path):
        info = sharing.read(path)
        g = next((x for x in self._games if x.recipe and x.recipe.id == info.get("recipe_id")), None)
        if not g:
            raise MergeError("You need %s installed to play this merge." % info.get("game_name", "the base game"))
        r = g.recipe
        try:
            t = r.theme(info.get("theme_id"))
        except Exception:
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
        for m in Merge.all():
            out.append({"id": m.id, "name": m.name, "game": m.data.get("game_name", ""),
                        "recipe_id": m.data.get("recipe_id"), "theme": m.data.get("theme_name", ""),
                        "mode": m.mode, "mode_label": kinds.get(m.mode, m.mode), "folder": m.target_path,
                        "created": m.data.get("created", ""), "status": m.data.get("status", ""),
                        "mods": [i["label"] for i in m.data.get("installs", [])],
                        "replaced": sum(1 for e in m.data.get("files", {}).values() if e.get("replaced")),
                        "cover_appid": m.data.get("cover_appid", ""),
                        "has_saves": os.path.isdir(saves.slot_root(m.id)),
                        "offline_lock": bool(self._recipe(m.data.get("recipe_id")) and
                                             self._recipe(m.data.get("recipe_id")).launch_args and m.mode != MODE_INPLACE),
                        "playing": self._playing == m.id})
        return out

    def merge_cover(self, merge_id):
        """Combined cover: the base game's art with the merged-in game's art cut in."""
        m = Merge.load(merge_id)
        root = self._steam_root()
        base = covers.steam_cover(m.data["cover_appid"], root) if m.data.get("cover_appid") else None
        if not base:
            r = self._recipe(m.data.get("recipe_id"))
            base = covers.made_cover(m.data.get("game_name", ""), m.base_path, r.exe if r else ())
        sid = m.data.get("source_appid")
        if not sid and m.data.get("source_name"):
            sid = covers.search_appid(m.data["source_name"])
        source = covers.steam_cover(sid, root) if sid else None
        path = covers.merged_cover(base, source, m.data.get("source_name", "") or m.data.get("theme_name", ""),
                                   "%s-%s" % (m.id, "s" if source else "n"))
        return covers.as_data_uri(path)

    def play(self, merge_id):
        """Swap in this merge's own saves, start the game, and swap them back when it closes."""
        if self._playing and self._playing != merge_id:
            raise MergeError("Another merged game is still running. Close it first.")
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))
        exe = r.launch_exe(m.target_path) if r else None
        if not exe:
            raise MergeError("Couldn't find the game's .exe in %s." % m.target_path)
        m.data["plays"] = m.data.get("plays", 0) + 1
        m.save()
        cloud = self._settings.get("cloud_folder")
        if cloud and r and r.saves and self._playing != merge_id:
            try:
                saves.pull_cloud(merge_id, cloud, m.name)
            except OSError:
                pass
        if r.saves and self._playing != merge_id:
            saves.swap_in(merge_id, r)
            self._playing = merge_id
            saves.watch_until_closed(r.processes or [os.path.basename(exe)], self._game_closed)
        # modded copies start offline-only where the game supports it, so mods never reach online modes
        args = list(r.launch_args) if r and m.mode != MODE_INPLACE else []
        subprocess.Popen([exe] + args, cwd=os.path.dirname(exe))
        return True

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
        return self._run(work)

    # ---------- mod profiles ----------

    def merge_mods(self, merge_id):
        m = Merge.load(merge_id)
        return {"mods": [{"label": x["label"], "on": x["on"], "kept": bool(m.kept_archive(x))} for x in m.all_mods()],
                "profiles": m.data.get("profiles", {}), "profile": m.data.get("profile", "")}

    def set_mods(self, merge_id, enabled, profile=""):
        """Switch mods on/off (optionally saving the set as a named profile, like "Chill" or "Chaos")."""
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
        """One-click crash repair: Franklin reads the logs, turns off the mod causing it, and restarts the game."""
        m = Merge.load(merge_id)
        r = self._recipe(m.data.get("recipe_id"))
        if self._playing == merge_id and saves.running(r.processes if r else []):
            raise MergeError("The game is still running. Close it first.")
        bot = franklin.Franklin(self._settings.get("claude_api_key", ""), self._settings.get("explicit", True))

        def work(job):
            job.write("Franklin is reading the crash logs …")
            logs = franklin.read_logs(m.target_path, r.logs if r else [])
            if not logs:
                raise MergeError("There are no log files yet. Play until it crashes, then press Fix it again.")
            on = [x["label"] for x in m.all_mods() if x["on"]]
            ctx = franklin.build_context({"base_game": m.data.get("game_name"), "theme": m.data.get("theme_name"),
                                          "logs": logs})
            try:
                verdict = bot.diagnose(ctx, on)
            except franklin.FranklinError as e:
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
        return self._run(work)

    # ---------- first run and downloads ----------

    def setup_status(self):
        """Folders Frankenstein still needs permission for (asked once, on first run)."""
        bases = [g.path for g in self._games if g.recipe]
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
        return {"has_key": bool(key), "key_hint": ("…" + key[-4:]) if key else "",
                "mode": self._settings.get("mode", MODE_HARDLINK),
                "destination": self._settings.get("destination", ""),
                "data_folder": paths.data_dir(),
                "voice_on": self._settings.get("voice_on", True),
                "has_eleven": bool(self._settings.get("elevenlabs_key")),
                "voice_name": self._settings.get("voice_name", ""),
                "voice_style": self._settings.get("voice_style", voice.DEFAULT_VOICE),
                "version": VERSION,
                "explicit": self._settings.get("explicit", True),
                "fun_style": self._settings.get("fun_style", "dot"),
                "cloud_folder": self._settings.get("cloud_folder", ""),
                "cloud_options": saves.cloud_folders(),
                "voice_styles": ([["own", "Your own cloned voice (slow to load)"]] if self._own_voice_folder() else [])
                + [[k, v[1]] for k, v in voice.NEURAL_VOICES.items()]}

    def save_settings(self, values):
        for k in ("mode", "destination", "voice_on", "voice_style", "explicit", "cloud_folder", "fun_style"):
            if k in values:
                self._settings[k] = values[k]
        if self._settings.get("voice_style") != "own":
            self._stop_own_voice()
        if values.get("elevenlabs_key") is not None:
            self._settings["elevenlabs_key"] = values["elevenlabs_key"].strip()
            self._settings.pop("voice_id", None)
            self._settings.pop("voice_name", None)
        if values.get("claude_api_key") is not None:
            self._settings["claude_api_key"] = values["claude_api_key"].strip()
        paths.save_settings(self._settings)
        return self.get_settings()

    # ---------- Franklin ----------

    def franklin_send(self, text, context, include_logs_for=None):
        """Start a Franklin reply. include_logs_for: a merge id or "game:<key>" whose logs to attach."""
        ctx = dict(context or {})
        ctx["about_player"] = self._about_player()
        ctx["pc"] = specs.summary()
        if include_logs_for:
            if include_logs_for.startswith("game:"):
                g = self._game(include_logs_for[5:])
                folder, r = g.path, g.recipe
                # The player most likely ran their newest merge of this game, so read its logs.
                latest = next((m for m in Merge.all() if os.path.normcase(m.base_path) == os.path.normcase(g.path)), None)
                if latest and os.path.isdir(latest.target_path):
                    folder = latest.target_path
            else:
                m = Merge.load(include_logs_for)
                folder, r = m.target_path, self._recipe(m.data.get("recipe_id"))
            ctx["logs"] = franklin.read_logs(folder, r.logs if r else [])
            if not ctx["logs"]:
                ctx["logs"] = [("(none)", "No log files were found. The game may not have been started yet.")]
        sid = uuid.uuid4().hex[:10]
        stream = {"text": "", "done": False, "error": None}
        self._chat_streams[sid] = stream
        with self._lock:
            self._chat_history.append({"role": "user",
                                      "content": franklin.build_context(ctx) + "\n\n[Player's message]\n" + text})
            history = list(self._chat_history)

        def run():
            try:
                bot = franklin.Franklin(self._settings.get("claude_api_key", ""), self._settings.get("explicit", True))
                for chunk in bot.stream_reply(history):
                    stream["text"] += chunk
                self._remember(stream["text"])
                with self._lock:
                    self._chat_history.append({"role": "assistant", "content": stream["text"]})
            except franklin.FranklinError as e:
                stream["error"] = str(e)
            except Exception as e:
                stream["error"] = "Franklin hit a problem: %s" % e
            finally:
                if stream["error"]:
                    with self._lock:  # keep the history alternating user/assistant
                        if self._chat_history and self._chat_history[-1]["role"] == "user":
                            self._chat_history.pop()
                stream["done"] = True
        threading.Thread(target=run, daemon=True).start()
        return sid

    def _about_player(self):
        """What Franklin remembers between sessions: notes he saved, and the merges played most."""
        out = list(self._settings.get("franklin_memory", []))
        played = sorted((m for m in Merge.all() if m.data.get("plays")), key=lambda m: -m.data["plays"])[:3]
        for m in played:
            out.append("Plays \"%s\" (%s with %s style) a lot: %d times." % (m.name, m.data.get("game_name"),
                                                                      m.data.get("theme_name"), m.data["plays"]))
        return out

    def _remember(self, reply):
        notes = [n.strip() for n in re.findall(r"\[remember:\s*([^\]]+)\]", reply) if n.strip()]
        if notes:
            mem = [n for n in self._settings.get("franklin_memory", []) if n not in notes] + notes
            self._settings["franklin_memory"] = mem[-30:]
            paths.save_settings(self._settings)

    def franklin_forget(self):
        self._settings["franklin_memory"] = []
        paths.save_settings(self._settings)
        return True

    def franklin_poll(self, sid, since=0):
        s = self._chat_streams[sid]
        return {"text": s["text"][since:], "next": len(s["text"]), "done": s["done"], "error": s["error"]}

    def franklin_reset(self):
        with self._lock:
            self._chat_history = []
        return True

    def _own_voice_folder(self):
        """Where the XTTS cloned voice is set up on this PC, or None. Only looks, never starts it."""
        if sys.platform != "win32":
            return None
        if not hasattr(self, "_own_folder"):
            self._own_folder = voice.LocalVoice.find(self._settings.get("xtts_folder"))
        return self._own_folder

    def _own_voice(self):
        """Franklin's own XTTS voice, or None. It's slow to load (a minute or two of CPU), so it only
        starts when the player has picked it in Settings."""
        if self._settings.get("voice_style") != "own":
            return None
        if not getattr(self, "_own", None):
            folder = self._own_voice_folder()
            if not folder:
                return None
            self._own = voice.LocalVoice(folder, os.path.join(os.path.dirname(resource_dir()), "xtts_server.py"),
                                         paths.sub("voice-cache"))
            self._own.start()
        return self._own

    def _stop_own_voice(self):
        own, self._own = getattr(self, "_own", None), None
        if own:
            own.stop()

    def franklin_voice_warm(self, lines):
        """Make the clips for lines Franklin says a lot, in the background, once his voice is loaded."""
        own = self._own_voice()
        if not own:
            return False

        def run():
            for _ in range(120):  # the model can take a minute or two to load on a laptop CPU
                if own.ready():
                    break
                time.sleep(2)
            for line in lines:
                try:
                    own.wav(line)
                except voice.VoiceError:
                    return
        threading.Thread(target=run, daemon=True).start()
        return True

    def franklin_voice(self, text, force=False):
        """Franklin's voice as mp3: your cloned voice or ElevenLabs if set up, otherwise the free Andrew
        voice (or whichever free voice is picked). None means use the voice built into Windows."""
        if not force and not self._settings.get("voice_on", True):
            return None
        text = text[:1200]
        own = self._own_voice()
        if own:
            try:
                return own.wav(text)
            except voice.VoiceError:
                pass  # still loading: use the next voice for now
        key = self._settings.get("elevenlabs_key")
        if key:
            try:
                if not self._settings.get("voice_id"):
                    vid, name = voice.find_manchester_voice(key)
                    self._settings.update(voice_id=vid, voice_name=name)
                    paths.save_settings(self._settings)
                return voice.speak(key, self._settings["voice_id"], text)
            except voice.VoiceError:
                pass
        try:
            style = self._settings.get("voice_style", voice.DEFAULT_VOICE)
            return voice.neural(text, voice.DEFAULT_VOICE if style == "own" else style)
        except voice.VoiceError:
            return None
