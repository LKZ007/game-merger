"""Backups, separate game copies, mod installs, and undo.

Every change Frankenstein makes is written to a manifest, so a merge can always
be rolled back: files it added are deleted and files it replaced are restored.
"""

import datetime
import json
import os
import re
import shutil
import subprocess
import tempfile
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


def clone_tree(src, dst, hardlink=True, log=_noop, progress=_noop):
    """Copy a game folder. With hardlink=True, unchanged files are shared with the
    original (falls back to real copies if the target is on another drive)."""
    total, count = tree_size(src)
    if not hardlink:
        free = shutil.disk_usage(os.path.dirname(os.path.abspath(dst)) or ".").free
        if free < total * 1.05:
            raise MergeError("Not enough disk space: the copy needs %.1f GB, %.1f GB free."
                             % (total / 1e9, free / 1e9))
    os.makedirs(dst, exist_ok=True)
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
                    if free < total * 1.05:
                        raise MergeError("Not enough disk space for a full copy: needs %.1f GB, %.1f GB free."
                                         % (total / 1e9, free / 1e9))
                    shutil.copy2(s, d)
            else:
                shutil.copy2(s, d)
            done += 1
            if done % 200 == 0 or done == count:
                progress(done, count)
    return hardlink


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
        for dirpath, _dirs, files in os.walk(mod_root):
            for f in files:
                s = os.path.join(dirpath, f)
                rel = os.path.relpath(s, mod_root)
                if dirpath == mod_root and os.path.splitext(f)[1].lower() in IGNORED_EXT:
                    continue
                plan.append((s, safe_rel(rel)))
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
        tmp = os.path.join(d, "manifest.json.tmp")
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(self.data, f, indent=2)
        os.replace(tmp, os.path.join(d, "manifest.json"))

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
    def create(cls, recipe, theme, base_path, target_parent, mode, name, appid="", log=_noop, progress=_noop):
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
            linked = clone_tree(base_path, target, hardlink=(mode == MODE_HARDLINK), log=log, progress=progress)
            if mode == MODE_HARDLINK and not linked:
                m.data["mode"] = MODE_COPY
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

    def _prune(self, d):
        root = os.path.normcase(os.path.abspath(self.target_path))
        while os.path.normcase(os.path.abspath(d)).startswith(root + os.sep):
            try:
                os.rmdir(d)
            except OSError:
                return
            d = os.path.dirname(d)

    def delete(self, log=_noop):
        """Delete the separate copy (never the original), or undo an in-place merge."""
        if self.mode == MODE_INPLACE:
            self.undo(log)
        else:
            if os.path.isfile(os.path.join(self.target_path, COPY_MARKER)):
                shutil.rmtree(self.target_path)
                log("Deleted %s." % self.target_path)
            elif os.path.exists(self.target_path):
                raise MergeError("%s doesn't look like a Frankenstein copy, so it was left alone." % self.target_path)
        shutil.rmtree(self.record_dir(self.id), ignore_errors=True)
