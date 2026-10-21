"""One-time permission setup.

Games usually live in protected folders (like C:\\Program Files (x86)\\Steam). Instead of asking
for admin rights every time, Game Merger asks Windows once and gives your own account
permission to change those game folders and its own merge folder. After that it never asks
again for the same game libraries."""

import os
import subprocess
import sys
import tempfile
import uuid


def writable(folder):
    """Can we create and delete a file here (or create the folder)?"""
    try:
        os.makedirs(folder, exist_ok=True)
        probe = os.path.join(folder, ".gm-%s.tmp" % uuid.uuid4().hex[:8])
        with open(probe, "w") as f:
            f.write("x")
        os.remove(probe)
        return True
    except OSError:
        return False


def library_root(game_path):
    """Grant on the whole Steam library (steamapps\\common) so games installed later are covered too."""
    parts = os.path.normpath(game_path).split(os.sep)
    lower = [p.lower() for p in parts]
    if "steamapps" in lower:
        i = lower.index("steamapps")
        return os.sep.join(parts[: i + 2])
    return game_path


def folders_needing_access(game_paths, extra=()):
    targets = []
    for p in list(game_paths) + list(extra):
        root = library_root(p)
        if root not in targets:
            targets.append(root)
    return [t for t in targets if not writable(t)]


def grant(folders):
    """Ask Windows (one UAC prompt) to give the current user Modify rights on these folders.
    Returns True if it worked."""
    if sys.platform != "win32" or not folders:
        return not folders
    user = "%s\\%s" % (os.environ.get("USERDOMAIN", ""), os.environ.get("USERNAME", ""))
    script = os.path.join(tempfile.gettempdir(), "gamemerger-permissions.cmd")
    with open(script, "w", encoding="utf-8") as f:
        f.write("@echo off\r\n")
        for folder in folders:
            f.write('if not exist "%s" mkdir "%s"\r\n' % (folder, folder))
            f.write('icacls "%s" /grant "%s":(OI)(CI)M /C /Q >nul\r\n' % (folder, user))
    ps = ("$p = Start-Process -FilePath cmd.exe -ArgumentList '/c \"\"%s\"\"' -Verb RunAs -WindowStyle Hidden "
          "-Wait -PassThru; exit $p.ExitCode" % script.replace("'", "''"))
    try:
        r = subprocess.run(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", ps],
                           capture_output=True, creationflags=0x08000000)
    except OSError:
        return False
    return r.returncode == 0 and all(writable(f) for f in folders)
