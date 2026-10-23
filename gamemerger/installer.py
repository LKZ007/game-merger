"""Backups, separate game copies, mod installs, and undo.

Every change Frankenstein makes is written to a manifest, so a merge can always
be rolled back: files it added are deleted and files it replaced are restored.
"""

import datetime
import filecmp
import json
import os
import re
import shutil
import stat
import subprocess
import tempfile
import threading
import time
import uuid
import zipfile

from . import paths

MODE_HARDLINK = "hardlink"  # separate copy that shares unchanged files with the original (almost no extra space)
MODE_COPY = "copy"          # separate full copy (safest, needs as much space as the game)
MODE_INPLACE = "inplace"    # mod the original install, with backups of anything replaced

COPY_MARKER = ".gamemerger-copy"
IGNORED_EXT = {".txt", ".md", ".jpg", ".jpeg", ".png", ".gif", ".url", ".pdf", ".webp", ".html", ".nfo"}
SEVEN_ZIP = [r"C:\Program Files\7-Zip\7z.exe", r"C:\Program Files (x86)\7-Zip\7z.exe"]


class MergeError(Exception):
    pass


HEADROOM = 300 * 1024 * 1024  # room left over after a full copy, so the game can still write its logs and saves


def room_needed(total):
    return total + HEADROOM


def _noop(*_a, **_k):
    pass


def safe_name(name):
    name = re.sub(r'[<>:"/\\|?*\x00-\x1f]', "", name).strip(" .")
    return name or "Merged game"


def safe_rel(rel):
    rel = os.path.normpath(rel.replace("\\", "/"))
    if os.path.isabs(rel) or rel.startswith("..") or os.path.splitdrive(rel)[0]:
        raise MergeError("Unsafe path inside mod: %s" % rel)
    return rel


# ---------- copying the base game ----------

def tree_size(src):
    total = count = 0
    for dirpath, _dirs, files in os.walk(src):
        for f in files:
            try:
                total += os.path.getsize(os.path.join(dirpath, f))
                count += 1
            except OSError:
                pass
    return total, count


def _size(n):
    return "%.1f MB" % (n / 1e6) if n >= 1e6 else "%d KB" % max(1, n // 1000)


def clone_tree(src, dst, hardlink=True, log=_noop, progress=_noop, trace=_noop):
    """Copy a game folder. With hardlink=True, unchanged files are shared with the
    original (falls back to real copies if the target is on another drive)."""
    total, count = tree_size(src)
    os.makedirs(dst, exist_ok=True)
    if not hardlink:
        free = shutil.disk_usage(dst).free
        if free < room_needed(total):
            raise MergeError("Not enough disk space: the copy needs %.1f GB and the drive has %.1f GB free. "
                             "Free up %.1f GB (empty the Recycle Bin or run Disk Cleanup) and try again."
                             % (room_needed(total) / 1e9, free / 1e9, (room_needed(total) - free) / 1e9 + 0.05))
    done = 0
    for dirpath, _dirs, files in os.walk(src):
        rel_dir = os.path.relpath(dirpath, src)
        out_dir = os.path.normpath(os.path.join(dst, rel_dir))
        os.makedirs(out_dir, exist_ok=True)
        for f in files:
            s, d = os.path.join(dirpath, f), os.path.join(out_dir, f)
            if hardlink:
                try:
                    os.link(s, d)
                except OSError:
                    log("Hard links aren't possible here (different drive?), making a full copy instead.")
                    hardlink = False
                    free = shutil.disk_usage(dst).free
                    if free < room_needed(total):
                        raise MergeError("Not enough disk space for a full copy: needs %.1f GB, %.1f GB free."
                                         % (room_needed(total) / 1e9, free / 1e9))
                    shutil.copy2(s, d)
            else:
                shutil.copy2(s, d)
            try:
                trace("%s %s  (%s)" % ("link" if hardlink else "copy", os.path.join(rel_dir, f) if rel_dir != "." else f, _size(os.path.getsize(d))))
            except OSError:
                pass
            done += 1
            if done % 200 == 0 or done == count:
                progress(done, count)
    return hardlink


# Files a game or its mods rewrite while playing (configs, caches, compiled scripts). In a space-saver
# copy these must never stay shared with the original: writing to a shared file changes both.
WRITABLE_EXT = (".ini", ".cfg", ".json", ".xml", ".txt", ".log", ".toml", ".yaml", ".yml", ".redscripts", ".bk",
                ".dll", ".asi", ".lua", ".reds", ".bin", ".dat", ".cache", ".exe", ".config", ".db", ".sqlite")
WRITABLE_DIRS = ("cache", "config", "configs", "settings", "plugins", "mods", "scripts", "r6", "red4ext", "bepinex",
                 "melonloader", "userdata", "engine", "bin")
KEEP_SHARED_OVER = 256 * 1024 * 1024  # only big read-only data files (game archives) stay shared


_SAVE_LOCK = threading.RLock()


def write_safely(path, text):
    """Write a small file so it's never left half written, even on Windows where a virus scanner, the search
    indexer or another of Frankenstein's own threads can hold the file for a moment ("Access is denied").
    Each write has its own temporary file, a read-only flag is cleared, and the swap is retried; if it still
    can't swap, the file is written in place."""
    tmp = "%s.%s.tmp" % (path, uuid.uuid4().hex[:8])
    with open(tmp, "w", encoding="utf-8") as f:
        f.write(text)
    try:
        for wait in (0, 0.05, 0.1, 0.2, 0.4, 0.8, 1.2):
            time.sleep(wait)
            try:
                if os.path.exists(path) and not os.access(path, os.W_OK):
                    os.chmod(path, stat.S_IWRITE | stat.S_IREAD)
                os.replace(tmp, path)
                return
            except PermissionError:
                continue
        with open(path, "w", encoding="utf-8") as f:  # last resort: the swap stays blocked, so write in place
            f.write(text)
    finally:
        if os.path.exists(tmp):
            try:
                os.remove(tmp)
            except OSError:
                pass


def _unshare(path):
    tmp = path + ".frankenstein-tmp"
    shutil.copy2(path, tmp)
    os.replace(tmp, path)


def harden(target, log=_noop):
    """Make a space-saver copy safe: every file that could be written while playing gets its own real copy,
    so nothing the merge does can reach the original install. Returns how many files were split off."""
    n = 0
    for dirpath, _dirs, files in os.walk(target):
        parts = {x.lower() for x in os.path.relpath(dirpath, target).split(os.sep)}
        for f in files:
            p = os.path.join(dirpath, f)
            try:
                st = os.stat(p)
                if st.st_nlink < 2:
                    continue
                if st.st_size < KEEP_SHARED_OVER or f.lower().endswith(WRITABLE_EXT) or parts & set(WRITABLE_DIRS):
                    _unshare(p)
                    n += 1
            except OSError as e:
                log("Couldn't split off %s: %s" % (f, e))
    if n:
        log("Gave %d file(s) their own copy so the original game can't be changed." % n)
    return n


def shared_changed(target, since):
    """Files still shared with the original install that were written after `since` (a timestamp):
    if any, the original game was changed through them."""
    out = []
    for dirpath, _dirs, files in os.walk(target):
        for f in files:
            p = os.path.join(dirpath, f)
            try:
                st = os.stat(p)
            except OSError:
                continue
            if st.st_nlink > 1 and st.st_mtime > since + 2:
                out.append(os.path.relpath(p, target))
    return out


# ---------- working out where a mod's files go ----------

def _unwrap(root, markers):
    while True:
        entries = [e for e in os.listdir(root) if not e.startswith("__MACOSX")]
        if len(entries) == 1 and os.path.isdir(os.path.join(root, entries[0])) and entries[0].lower() not in markers:
            root = os.path.join(root, entries[0])
        else:
            return root


def _find_mod_root(root, markers, max_depth=3):
    queue = [(root, 0)]
    while queue:
        d, depth = queue.pop(0)
        try:
            children = os.listdir(d)
        except OSError:
            continue
        if any(c.lower() in markers and os.path.isdir(os.path.join(d, c)) for c in children):
            return d
        if depth < max_depth:
            queue.extend((os.path.join(d, c), depth + 1) for c in sorted(children)
                         if os.path.isdir(os.path.join(d, c)))
    return None


def plan_files(extracted, layout, flat_root=False, subdir=""):
    """Return [(source_file, path_relative_to_game_root)] for an extracted mod."""
    markers = {m.lower() for m in layout.get("root_markers", [])}
    top = extracted
    root = _unwrap(extracted, markers)
    if subdir and os.path.isdir(os.path.join(root, subdir)):
        root = os.path.join(root, subdir)
    mod_root = root if flat_root else _find_mod_root(root, markers)
    plan, unknown = [], []
    if mod_root:
        # e.g. Bethesda: a mod with meshes/ and textures/ at its top belongs inside Data/
        prefix = layout.get("root_prefix", "")
        if prefix and any(c.lower() == prefix.lower() for c in os.listdir(mod_root)):
            prefix = ""
        for dirpath, _dirs, files in os.walk(mod_root):
            for f in files:
                s = os.path.join(dirpath, f)
                rel = os.path.relpath(s, mod_root)
                if dirpath == mod_root and os.path.splitext(f)[1].lower() in IGNORED_EXT:
                    continue
                plan.append((s, safe_rel(os.path.join(prefix, rel) if prefix else rel)))
        return plan, unknown
    rules = layout.get("loose_rules", [])
    for dirpath, _dirs, files in os.walk(root):
        for f in files:
            s = os.path.join(dirpath, f)
            ext = os.path.splitext(f)[1].lower()
            rule = next((r for r in rules if ext in [e.lower() for e in r["ext"]]), None)
            if rule:
                if ext == ".lua" or rule.get("keep_folder"):
                    rel = os.path.join(rule["dest"], os.path.relpath(s, top))
                else:
                    rel = os.path.join(rule["dest"], f)
                plan.append((s, safe_rel(rel)))
            elif ext not in IGNORED_EXT:
                unknown.append(os.path.relpath(s, top))
    return plan, unknown


def extract(archive, into):
    ext = os.path.splitext(archive)[1].lower()
    if ext == ".zip":
        with zipfile.ZipFile(archive) as z:
            for info in z.infolist():
                safe_rel(info.filename)  # refuse paths that escape the folder
            z.extractall(into)
    elif ext in (".7z", ".rar"):
        exe = next((p for p in SEVEN_ZIP if os.path.isfile(p)), None) or shutil.which("7z")
        if not exe:
            raise MergeError("%s files need 7-Zip. Install it from https://www.7-zip.org and try again."
                             % ext)
        r = subprocess.run([exe, "x", "-y", "-o" + into, archive], capture_output=True, text=True)
        if r.returncode != 0:
            raise MergeError("7-Zip couldn't open %s: %s" % (os.path.basename(archive), r.stderr.strip()[:300]))
    else:
        shutil.copy2(archive, os.path.join(into, os.path.basename(archive)))


def preview(archive, layout, flat_root=False, subdir=""):
    """Where a mod's files would go, without installing anything: (paths, unrecognised files)."""
    tmp = tempfile.mkdtemp(prefix="gm-", dir=paths.sub("tmp"))
    try:
        extract(archive, tmp)
        plan, unknown = plan_files(tmp, layout, flat_root=flat_root, subdir=subdir)
        return [rel.replace(os.sep, "/") for _s, rel in plan], unknown
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


def find_clashes(previews):
    """previews: {mod name: [paths]} -> ["a.zip and b.zip both change x", ...]"""
    owner, clashes = {}, []
    for mod, rels in previews.items():
        for rel in rels:
            if rel in owner and owner[rel] != mod:
                clashes.append((owner[rel], mod, rel))
            else:
                owner[rel] = mod
    out, seen = [], set()
    for a, b, rel in clashes:
        if (a, b) not in seen:
            seen.add((a, b))
            n = sum(1 for x in clashes if x[:2] == (a, b))
            out.append("%s and %s both change %s%s" % (a, b, rel, " (+%d more)" % (n - 1) if n > 1 else ""))
    return out


# ---------- a merge and its manifest ----------

class Merge:
    def __init__(self, data):
        self.data = data

    # stored fields
    id = property(lambda s: s.data["id"])
    name = property(lambda s: s.data["name"])
    mode = property(lambda s: s.data["mode"])
    base_path = property(lambda s: s.data["base_path"])
    target_path = property(lambda s: s.data["target_path"])

    @staticmethod
    def record_dir(merge_id):
        return os.path.join(paths.sub("merges"), merge_id)

    @property
    def backup_dir(self):
        return os.path.join(self.record_dir(self.id), "backup")

    def save(self):
        d = self.record_dir(self.id)
        os.makedirs(d, exist_ok=True)
        with _SAVE_LOCK:
            write_safely(os.path.join(d, "manifest.json"), json.dumps(self.data, indent=2))

    @classmethod
    def load(cls, merge_id):
        with open(os.path.join(cls.record_dir(merge_id), "manifest.json"), encoding="utf-8") as f:
            return cls(json.load(f))

    @classmethod
    def all(cls):
        out = []
        root = paths.sub("merges")
        for mid in sorted(os.listdir(root)):
            try:
                out.append(cls.load(mid))
            except (OSError, ValueError):
                continue
        out.sort(key=lambda m: m.data.get("created", ""), reverse=True)
        return out

    @classmethod
    def create(cls, recipe, theme, base_path, target_parent, mode, name, appid="", log=_noop, progress=_noop, trace=_noop):
        name = safe_name(name)
        if mode == MODE_INPLACE:
            target = base_path
        else:
            target = os.path.join(target_parent, name)
            n = 2
            while os.path.exists(target) and os.listdir(target):  # name taken: use "Name (2)" and so on
                target = os.path.join(target_parent, "%s (%d)" % (name, n))
                n += 1
            if n > 2:
                name = os.path.basename(target)
                log("A merge called that already exists, so this one is \"%s\"." % name)
            if os.path.normcase(os.path.abspath(target)).startswith(os.path.normcase(os.path.abspath(base_path)) + os.sep):
                raise MergeError("The copy can't go inside the original game folder.")
        m = cls({
            "id": datetime.datetime.now().strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:6],
            "name": name, "recipe_id": recipe.id, "game_name": recipe.name,
            "theme_id": theme.id, "theme_name": theme.name,
            "base_path": base_path, "target_path": target, "mode": mode,
            "created": datetime.datetime.now().isoformat(timespec="seconds"),
            "files": {}, "installs": [], "status": "creating",
        })
        m.save()
        if mode != MODE_INPLACE:
            os.makedirs(target, exist_ok=True)
            with open(os.path.join(target, COPY_MARKER), "w", encoding="utf-8") as f:
                f.write("Made by Frankenstein from %s\n" % base_path)
            log("Copying %s to %s ..." % (base_path, target))
            linked = clone_tree(base_path, target, hardlink=(mode == MODE_HARDLINK), log=log, progress=progress, trace=trace)
            if mode == MODE_HARDLINK and not linked:
                m.data["mode"] = MODE_COPY
            elif mode == MODE_HARDLINK:
                harden(target, log=log)
            if appid:
                # Stops Steam games from relaunching the original install instead of this copy.
                exe = recipe.launch_exe(target)
                if exe:
                    tmp = os.path.join(paths.sub("tmp"), "steam_appid.txt")
                    with open(tmp, "w") as f:
                        f.write(appid)
                    m.put_file(tmp, os.path.relpath(os.path.join(os.path.dirname(exe), "steam_appid.txt"), target))
        else:
            log("Modding the original folder. Anything replaced is backed up first.")
        m.data["status"] = "ready"
        m.save()
        return m

    def put_file(self, src, rel):
        self.must_be_unpacked()
        rel = safe_rel(rel)
        dest = os.path.join(self.target_path, rel)
        files = self.data["files"]
        if rel not in files:
            entry = {"replaced": False}
            if os.path.lexists(dest):
                entry["replaced"] = True
                if self.mode == MODE_INPLACE:
                    backup = os.path.join(self.backup_dir, rel)
                    os.makedirs(os.path.dirname(backup), exist_ok=True)
                    shutil.move(dest, backup)
                    entry["backup"] = backup
                else:
                    os.remove(dest)  # may be a hard link to the original: never write through it
            files[rel] = entry
        elif os.path.lexists(dest):
            os.remove(dest)
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        shutil.copy2(src, dest)

    def install(self, archive, label, layout, flat_root=False, subdir="", log=_noop, seq=None, check=None):
        if check:
            verdict = check(archive)
            if verdict == "threat":
                raise MergeError("Microsoft Defender found a threat in %s, so it wasn't installed. Delete that file."
                                 % os.path.basename(archive))
            if verdict == "clean":
                log("Defender checked %s: clean." % os.path.basename(archive))
        tmp = tempfile.mkdtemp(prefix="gm-", dir=paths.sub("tmp"))
        try:
            extract(archive, tmp)
            plan, unknown = plan_files(tmp, layout, flat_root=flat_root, subdir=subdir)
            if not plan:
                raise MergeError("Couldn't work out where %s goes. It may need a manual install; check its mod page."
                                 % os.path.basename(archive))
            for src, rel in plan:
                self.put_file(src, rel)
            if seq is None:
                self.data["seq"] = seq = self.data.get("seq", 0) + 1
            self.data["installs"].append({"label": label, "file": os.path.basename(archive), "seq": seq,
                                          "paths": [rel for _s, rel in plan], "kept": self._keep(archive),
                                          "flat_root": flat_root, "subdir": subdir})
            self.save()
            log("Installed %s (%d files)." % (label, len(plan)))
            if unknown:
                log("  Skipped %d file(s) it didn't recognise, e.g. %s" % (len(unknown), unknown[0]))
            return plan
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    def _keep(self, archive):
        """Keep the mod file with the merge (a hard link when possible, so it takes no extra space),
        so the merge can be shared or rebuilt even after the download is deleted."""
        d = os.path.join(self.record_dir(self.id), "archives")
        os.makedirs(d, exist_ok=True)
        name = os.path.basename(archive)
        dest = os.path.join(d, name)
        n = 2
        while os.path.exists(dest):
            if os.path.getsize(dest) == os.path.getsize(archive):
                return os.path.basename(dest)
            base, ext = os.path.splitext(name)
            dest = os.path.join(d, "%s (%d)%s" % (base, n, ext))
            n += 1
        try:
            os.link(archive, dest)
        except OSError:
            try:
                shutil.copy2(archive, dest)
            except OSError:
                return None
        return os.path.basename(dest)

    def kept_archive(self, install):
        if not install.get("kept"):
            return None
        p = os.path.join(self.record_dir(self.id), "archives", install["kept"])
        return p if os.path.isfile(p) else None

    def undo(self, log=_noop):
        """Remove everything Frankenstein added and put replaced files back."""
        self.must_be_unpacked()
        for rel, entry in sorted(self.data["files"].items(), key=lambda kv: -kv[0].count(os.sep)):
            dest = os.path.join(self.target_path, rel)
            if os.path.lexists(dest):
                os.remove(dest)
            if entry.get("replaced"):
                if entry.get("backup") and os.path.exists(entry["backup"]):
                    os.makedirs(os.path.dirname(dest), exist_ok=True)
                    shutil.move(entry["backup"], dest)
                else:
                    original = os.path.join(self.base_path, rel)
                    if os.path.exists(original):
                        os.makedirs(os.path.dirname(dest), exist_ok=True)
                        try:
                            if self.mode == MODE_HARDLINK:
                                os.link(original, dest)
                            else:
                                shutil.copy2(original, dest)
                        except OSError:
                            shutil.copy2(original, dest)
            self._prune(os.path.dirname(dest))
        log("Removed %d mod file(s)." % len(self.data["files"]))
        # keep what was installed (and its kept file) so mods can be switched back on later
        self.data["parked"] = self.data.get("parked", []) + [i for i in self.data["installs"] if i.get("kept")]
        self.data["files"], self.data["installs"] = {}, []
        self.data["status"] = "restored"
        self.save()

    def all_mods(self):
        """Every mod this merge knows about, switched on or off, in install order."""
        on = [dict(i, on=True) for i in self.data.get("installs", [])]
        off = [dict(i, on=False) for i in self.data.get("parked", [])]
        return sorted(on + off, key=lambda x: x.get("seq", 0))

    def rebuild(self, layout, enabled, log=_noop):
        self.must_be_unpacked()
        """Switch mods on and off: undo everything, then reinstall the enabled ones (by label) in their
        original order from the files the merge kept. Used by mod profiles and crash repair."""
        wanted = set(enabled)
        order = self.all_mods()
        missing = [m["label"] for m in order if m["label"] in wanted and not self.kept_archive(m)]
        if missing:
            raise MergeError("Frankenstein doesn't have the files for: %s. Add them again with Add mods." % ", ".join(missing))
        self.undo(log)
        self.data["parked"] = []
        for m in order:
            archive = self.kept_archive(m)
            if m["label"] in wanted and archive:
                self.install(archive, m["label"], layout, flat_root=m.get("flat_root", False),
                             subdir=m.get("subdir", ""), log=log, seq=m.get("seq"))
            elif archive:
                self.data["parked"].append({k: v for k, v in m.items() if k != "on"})
        self.data["status"] = "ready"
        self.save()

    # ---------- packed away: only what the merge changed is kept, the rest comes back from your own game ----------

    PACK = "packed.zip"
    PACK_INDEX = "_frankenstein_pack.json"

    @property
    def packed(self):
        return bool(self.data.get("packed")) and os.path.isfile(os.path.join(self.record_dir(self.id), self.PACK))

    def must_be_unpacked(self):
        if self.packed:
            raise MergeError("%s is packed away to save space. Press Play or Unpack first." % self.name)

    def can_pack(self):
        """Why this merge can't be packed, or None if it can."""
        if self.mode == MODE_INPLACE:
            return "it mods the original game folder, so there's no copy to pack"
        if self.packed:
            return "it's already packed"
        if not os.path.isfile(os.path.join(self.target_path, COPY_MARKER)):
            return "its folder isn't a Frankenstein copy"
        if not os.path.isdir(self.base_path):
            return "the original game isn't installed at %s any more, and packing needs it to unpack again" % self.base_path
        return None

    def pack(self, log=_noop, progress=_noop):
        """Keep only the files this merge changed or added (mods, their settings, anything the game wrote into its
        folder) in one zip, then delete the copy. Unpack rebuilds it as a full copy of your own installed game with
        those files on top. The original game is only ever read."""
        why = self.can_pack()
        if why:
            raise MergeError("Can't pack %s: %s." % (self.name, why))
        base, target = self.base_path, self.target_path
        orig = {}
        for dirpath, _dirs, files in os.walk(base):
            for f in files:
                p = os.path.join(dirpath, f)
                try:
                    st = os.stat(p)
                except OSError:
                    continue
                orig[os.path.relpath(p, base)] = (st.st_size, st.st_mtime)
        changed, seen, total = [], set(), 0
        for dirpath, _dirs, files in os.walk(target):
            for f in files:
                p = os.path.join(dirpath, f)
                rel = os.path.relpath(p, target)
                seen.add(rel)
                try:
                    st = os.stat(p)
                except OSError:
                    continue
                total += st.st_size
                o = orig.get(rel)
                if o is None or o[0] != st.st_size or abs(o[1] - st.st_mtime) > 2:
                    changed.append(rel)
                elif st.st_size < 4 << 20 and not filecmp.cmp(p, os.path.join(base, rel), shallow=False):
                    changed.append(rel)  # small files (settings) are compared byte by byte, whatever their dates say
        removed = sorted(r for r in orig if r not in seen)
        d = self.record_dir(self.id)
        zpath = os.path.join(d, self.PACK)
        tmp = zpath + ".tmp"
        log("Keeping the %d file(s) this merge changed ..." % len(changed))
        with zipfile.ZipFile(tmp, "w", zipfile.ZIP_DEFLATED, allowZip64=True) as z:
            for i, rel in enumerate(changed):
                z.write(os.path.join(target, rel), rel.replace(os.sep, "/"))
                if i % 50 == 0:
                    progress(i, len(changed))
            z.writestr(self.PACK_INDEX, json.dumps({"removed": removed, "files": len(changed)}))
        with zipfile.ZipFile(tmp) as z:  # nothing is deleted until the zip reads back whole
            bad = z.testzip()
            if bad:
                os.remove(tmp)
                raise MergeError("The packed copy didn't check out (%s), so nothing was deleted." % bad)
        os.replace(tmp, zpath)
        log("Deleting the copy at %s (the original game isn't touched) ..." % target)
        shutil.rmtree(target)
        self.data["packed"] = {"time": time.time(), "copy_bytes": total, "zip_bytes": os.path.getsize(zpath),
                               "files": len(changed)}
        self.save()
        return self.data["packed"]

    def unpack_room(self):
        """How much room rebuilding the copy takes, and how much its drive has."""
        total = tree_size(self.base_path)[0] if os.path.isdir(self.base_path) else 0
        try:
            with zipfile.ZipFile(os.path.join(self.record_dir(self.id), self.PACK)) as z:
                total += sum(i.file_size for i in z.infolist())  # the merge's own files go on top
        except (OSError, zipfile.BadZipFile):
            pass
        here = self.target_path
        while here and not os.path.isdir(here) and os.path.dirname(here) != here:
            here = os.path.dirname(here)
        free = shutil.disk_usage(here).free if here and os.path.isdir(here) else 0
        t = self.target_path
        if os.path.isdir(t) and (os.path.isfile(os.path.join(t, COPY_MARKER)) or
                                 self._leftover_is_ours(os.path.join(self.record_dir(self.id), self.PACK))):
            free += tree_size(t)[0]  # a half-done rebuild is cleared first, so its room counts as free
        return {"need": room_needed(total), "free": free}

    def move_copy_to(self, folder):
        """While packed: rebuild the copy in another folder (another drive with room), not where it was."""
        if not self.packed:
            raise MergeError("Only a packed merge can be moved this way.")
        new = os.path.join(os.path.abspath(folder), os.path.basename(os.path.normpath(self.target_path)))
        base = os.path.normcase(os.path.abspath(self.base_path))
        if os.path.normcase(new) == base or os.path.normcase(new).startswith(base + os.sep):
            raise MergeError("That's inside the original game's folder. Pick another place.")
        if os.path.isdir(new) and os.listdir(new) and not os.path.isfile(os.path.join(new, COPY_MARKER)):
            raise MergeError("%s already has other files in it. Pick another place." % new)
        self.data["target_path"] = new
        self.save()
        return new

    def _leftover_is_ours(self, zpath):
        """True when every file in the folder that's in the way is one of the installed game's files or one of this
        merge's packed files: what a rebuild that was cut off leaves behind."""
        key = (lambda r: r.replace("\\", "/").lower()) if os.name == "nt" else (lambda r: r.replace("\\", "/"))
        known = {key(COPY_MARKER)}
        for dirpath, _dirs, files in os.walk(self.base_path):
            for f in files:
                known.add(key(os.path.relpath(os.path.join(dirpath, f), self.base_path)))
        try:
            with zipfile.ZipFile(zpath) as z:
                known.update(key(n) for n in z.namelist())
        except (OSError, zipfile.BadZipFile):
            pass
        for dirpath, _dirs, files in os.walk(self.target_path):
            for f in files:
                if key(os.path.relpath(os.path.join(dirpath, f), self.target_path)) not in known:
                    return False
        return True

    def _move_aside(self):
        """Rename the folder that's in the way (same drive, nothing copied or deleted) so the copy can be rebuilt."""
        base = os.path.normcase(os.path.abspath(self.base_path))
        here = os.path.normcase(os.path.abspath(self.target_path))
        if here == base or base.startswith(here + os.sep) or here.startswith(base + os.sep):
            raise MergeError("%s is the original game's folder, so it was left alone." % self.target_path)
        aside = "%s (moved aside %s)" % (os.path.normpath(self.target_path), datetime.datetime.now().strftime("%Y-%m-%d %H%M%S"))
        os.rename(self.target_path, aside)
        return aside

    def unpack(self, log=_noop, progress=_noop):
        """Make the working copy again: a full copy of your installed game, then the merge's own files on top."""
        if not self.packed:
            return 0
        if not os.path.isdir(self.base_path):
            raise MergeError("The original game isn't at %s any more. Install it there again, then press Play." % self.base_path)
        start = time.time()
        zpath = os.path.join(self.record_dir(self.id), self.PACK)
        aside = None
        if os.path.isdir(self.target_path):
            if os.path.isfile(os.path.join(self.target_path, COPY_MARKER)) or not os.listdir(self.target_path):
                log("Clearing a copy that was cut off half way ...")
                shutil.rmtree(self.target_path)
            elif self._leftover_is_ours(zpath):
                # an unpack cut off before 3.45.1 left no marker: every file in it is the game's or this merge's own
                log("Clearing what an earlier rebuild left half done (only the game's and this merge's own files) ...")
                shutil.rmtree(self.target_path)
            else:
                aside = self._move_aside()
                log("%s had other files in it, so it was moved aside to %s (nothing in it was deleted)." % (self.target_path, aside))
        log("Copying %s ..." % self.base_path)
        try:
            os.makedirs(self.target_path, exist_ok=True)
            with open(os.path.join(self.target_path, COPY_MARKER), "w", encoding="utf-8") as f:
                f.write("Made by Frankenstein from %s\n" % self.base_path)  # first, so a cut-off rebuild is still known as ours
            clone_tree(self.base_path, self.target_path, hardlink=False, log=log, progress=progress)
        except BaseException as e:
            shutil.rmtree(self.target_path, ignore_errors=True)  # never leave half a copy filling the drive
            if aside and isinstance(e, MergeError):
                raise MergeError("%s The folder that was in the way is at %s: delete it if you don't need it, to make room." % (e, aside))
            raise
        log("Putting the merge's own files back ...")
        with zipfile.ZipFile(zpath) as z:
            index = json.loads(z.read(self.PACK_INDEX).decode("utf-8")) if self.PACK_INDEX in z.namelist() else {}
            for name in z.namelist():
                if name == self.PACK_INDEX:
                    continue
                rel = safe_rel(name.replace("/", os.sep))
                dest = os.path.join(self.target_path, rel)
                os.makedirs(os.path.dirname(dest), exist_ok=True)
                if os.path.lexists(dest):
                    os.remove(dest)
                with z.open(name) as src, open(dest, "wb") as out:
                    shutil.copyfileobj(src, out, 1 << 20)
        for rel in index.get("removed", []):
            p = os.path.join(self.target_path, safe_rel(rel))
            if os.path.lexists(p):
                os.remove(p)
        if self.mode == MODE_HARDLINK:
            self.data["mode"] = MODE_COPY  # unpacked copies are always full copies
        secs = time.time() - start
        self.data["unpack_secs"] = round(secs, 1)
        self.data.pop("packed", None)
        self.save()
        os.remove(zpath)
        return secs

    def _prune(self, d):
        root = os.path.normcase(os.path.abspath(self.target_path))
        while os.path.normcase(os.path.abspath(d)).startswith(root + os.sep):
            try:
                os.rmdir(d)
            except OSError:
                return
            d = os.path.dirname(d)

    def unmarked(self):
        """True when this merge's copy is there but has no Frankenstein marker (copies adopted from older versions)."""
        return (self.mode != MODE_INPLACE and os.path.isdir(self.target_path)
                and not os.path.isfile(os.path.join(self.target_path, COPY_MARKER)))

    def why_not_delete_anyway(self, protect=()):
        """Why an unmarked copy can't be deleted even when you say "delete anyway", or "" when it can. It must be
        the folder Frankenstein's own record names for this merge, a real folder (not a link), at least two levels
        below a drive, outside Steam's and Windows' folders, and not the original game or any installed game, nor
        inside or around one."""
        here = os.path.normcase(os.path.abspath(self.target_path))
        if os.path.islink(self.target_path) or getattr(os.path, "isjunction", lambda _p: False)(self.target_path):
            return "it's a link to another folder"
        parts = [x for x in os.path.splitdrive(here)[1].split(os.sep) if x]
        if len(parts) < 2:
            return "it's a top-level folder"
        if {"steamapps", "windows", "program files", "program files (x86)", "programdata"} & {x.lower() for x in parts}:
            return "it's inside Steam's or Windows' own folders"
        for other in [self.base_path] + list(protect):
            if not other:
                continue
            o = os.path.normcase(os.path.abspath(other))
            if here == o or o.startswith(here + os.sep) or here.startswith(o + os.sep):
                return "it is, or holds, an installed game (%s)" % other
        return ""

    def delete(self, log=_noop, force=False, protect=()):
        """Delete the separate copy (never the original), or undo an in-place merge. A copy without Frankenstein's
        marker (one adopted from an older version) is only deleted with force, after the safety checks above."""
        if self.mode == MODE_INPLACE:
            self.undo(log)
        else:
            if os.path.isfile(os.path.join(self.target_path, COPY_MARKER)):
                shutil.rmtree(self.target_path)
                log("Deleted %s." % self.target_path)
            elif os.path.exists(self.target_path):
                if not force:
                    raise MergeError("%s doesn't look like a Frankenstein copy, so it was left alone." % self.target_path)
                why = self.why_not_delete_anyway(protect)
                if why:
                    raise MergeError("%s was left alone: %s." % (self.target_path, why))
                shutil.rmtree(self.target_path)
                log("Deleted %s (an older copy without Frankenstein's marker; the original at %s is untouched)." % (self.target_path, self.base_path))
        shutil.rmtree(self.record_dir(self.id), ignore_errors=True)
