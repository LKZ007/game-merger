"""Everything the window can ask the app to do. The web UI calls these methods through
pywebview (or the local browser bridge in server.py); every argument and return value is plain JSON."""

import os
import shutil
import subprocess
import sys
import threading
import time
import traceback
import urllib.parse
import uuid
import webbrowser

from . import covers, detect, downloader, franklin, network, paths, permissions, saves, sharing, updater, voice
from .version import VERSION
from .installer import MODE_COPY, MODE_HARDLINK, MODE_INPLACE, Merge, MergeError, find_clashes, preview
from .recipes import load_recipes, resource_dir

SHARE_TYPES = ("Shared merges (*.gmerge)", "All files (*.*)")
MOD_TYPES = ("Mod files (*.zip;*.7z;*.rar;*.archive;*.dll;*.asi;*.reds;*.lua;*.yaml;*.xl)", "All files (*.*)")


def default_destination(base_path):
    if sys.platform == "win32":
        drive = os.path.splitdrive(os.path.abspath(base_path))[0] or "C:"
        return drive + "\\GameMerger"
    return os.path.join(os.path.expanduser("~"), "GameMerger")


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
        self._recipes = load_recipes([resource_dir(), paths.user_recipes_dir()])
        self._games = []
        self._jobs = {}
        self._chat_history = []
        self._chat_streams = {}
        self._lock = threading.Lock()
        self._playing = None
        self._recover_saves()

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
        saves.swap_out()
        self._playing = None

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
                    raise MergeError("Game Merger needs permission to change %s. Press Merge again and click Yes "
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
                    merge.install(m["path"], "%s (%s)" % (name, m["slot"]), r.layout, log=job.write)
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
            job.write("Downloading Game Merger %s ..." % info["version"])
            setup = updater.download(info, progress=lambda d, n: setattr(job, "progress", [d, n]))
            job.write("Installing. Game Merger will close and open again in a few seconds.")
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

    def pending_import(self):
        """A .gmerge file the app was opened with (double-clicked), once."""
        p, self._pending = getattr(self, "_pending", None), None
        return p

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
            raise MergeError("This merge uses a style this version of Game Merger doesn't know. Update the app.")
        mode = self._settings.get("mode", MODE_HARDLINK)
        mode = MODE_HARDLINK if mode == MODE_INPLACE else mode  # never change a friend's original install
        dest = self._settings.get("destination") or default_destination(g.path)

        def work(job):
            job.write("Unpacking %s ..." % info["name"])
            info2, folder, items = sharing.unpack(path, log=job.write)
            try:
                needs = permissions.folders_needing_access([g.path], [dest])
                if needs and not permissions.grant(needs):
                    raise MergeError("Game Merger needs permission to change %s." % needs[0])
                merge = self._new_merge(g, t, mode, dest, info["name"], job)
                merge.data["shared_from"] = os.path.basename(path)
                merge.save()
                problems = []
                for inst, archive in items:
                    try:
                        merge.install(archive, inst["label"], r.layout, flat_root=inst.get("flat_root", False),
                                      subdir=inst.get("subdir", ""), log=job.write)
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
        if r.saves and self._playing != merge_id:
            saves.swap_in(merge_id, r)
            self._playing = merge_id
            saves.watch_until_closed(r.processes or [os.path.basename(exe)], self._game_closed)
        subprocess.Popen([exe], cwd=os.path.dirname(exe))
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
                    m.install(p, os.path.basename(p), r.layout, log=job.write)
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
        """Folders Game Merger still needs permission for (asked once, on first run)."""
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
                "voice_styles": ([["own", "Your own cloned voice (made on this PC)"]] if self._own_voice() else [])
                + [[k, v[1]] for k, v in voice.NEURAL_VOICES.items()]}

    def save_settings(self, values):
        for k in ("mode", "destination", "voice_on", "voice_style", "explicit"):
            if k in values:
                self._settings[k] = values[k]
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

    def franklin_poll(self, sid, since=0):
        s = self._chat_streams[sid]
        return {"text": s["text"][since:], "next": len(s["text"]), "done": s["done"], "error": s["error"]}

    def franklin_reset(self):
        with self._lock:
            self._chat_history = []
        return True

    def _own_voice(self):
        """Franklin's own XTTS voice, started on first use, or None if it isn't set up on this PC."""
        if getattr(self, "_own", None) is None:
            folder = voice.LocalVoice.find(self._settings.get("xtts_folder"))
            if not folder or sys.platform != "win32":
                self._own = False
            else:
                self._own = voice.LocalVoice(folder, os.path.join(os.path.dirname(resource_dir()), "xtts_server.py"),
                                             paths.sub("voice-cache"))
                self._own.start()
        return self._own or None

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
        """Franklin's voice as mp3: your cloned voice or ElevenLabs if set up, otherwise a free natural
        British voice. None means use the voice built into Windows."""
        if not force and not self._settings.get("voice_on", True):
            return None
        text = text[:1200]
        own = self._own_voice()
        if own and self._settings.get("voice_style") == "own":
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
            return voice.neural(text, self._settings.get("voice_style", voice.DEFAULT_VOICE))
        except voice.VoiceError:
            return None
