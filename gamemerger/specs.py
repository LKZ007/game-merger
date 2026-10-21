"""This PC's specs (CPU, graphics card, memory), so Franklin can say whether a merge will run well."""

import json
import subprocess
import sys

_cache = None


def specs():
    global _cache
    if _cache is not None or sys.platform != "win32":
        return _cache or {}
    ps = ("$c=(Get-CimInstance Win32_Processor | Select-Object -First 1).Name;"
          "$g=(Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name }) -join ', ';"
          "$m=[math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory/1GB);"
          "@{cpu=$c;gpu=$g;ram_gb=$m} | ConvertTo-Json -Compress")
    try:
        out = subprocess.run(["powershell", "-NoProfile", "-Command", ps], capture_output=True, text=True,
                             timeout=20, creationflags=0x08000000).stdout
        _cache = json.loads(out)
    except (OSError, ValueError, subprocess.TimeoutExpired):
        _cache = {}
    return _cache


def summary():
    s = specs()
    if not s:
        return ""
    return "CPU %s; graphics %s; %s GB RAM" % (s.get("cpu", "?"), s.get("gpu", "?"), s.get("ram_gb", "?"))
