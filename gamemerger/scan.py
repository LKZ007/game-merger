"""Checks downloaded mod files with Microsoft Defender before they're installed."""

import os
import subprocess
import sys


def _mpcmdrun():
    root = os.path.join(os.environ.get("ProgramData", r"C:\ProgramData"), "Microsoft", "Windows Defender", "Platform")
    try:  # newest platform version first
        for v in sorted(os.listdir(root), reverse=True):
            p = os.path.join(root, v, "MpCmdRun.exe")
            if os.path.isfile(p):
                return p
    except OSError:
        pass
    p = os.path.join(os.environ.get("ProgramFiles", r"C:\Program Files"), "Windows Defender", "MpCmdRun.exe")
    return p if os.path.isfile(p) else None


def scan(path):
    """"clean", "threat", or "unavailable" (Defender off, another antivirus in charge, not Windows)."""
    exe = _mpcmdrun() if sys.platform == "win32" else None
    if not exe:
        return "unavailable"
    try:
        r = subprocess.run([exe, "-Scan", "-ScanType", "3", "-File", os.path.abspath(path), "-DisableRemediation"],
                           capture_output=True, text=True, timeout=300, creationflags=0x08000000)
    except (OSError, subprocess.TimeoutExpired):
        return "unavailable"
    if r.returncode == 0:
        return "clean"
    if r.returncode == 2 or "threat" in (r.stdout or "").lower():
        return "threat"
    return "unavailable"
