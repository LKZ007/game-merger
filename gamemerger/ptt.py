"""Push-to-talk with Franklin: hold F8 (in the app or while playing), say your question, let go.

Speech is turned into text by Windows' own speech recognition (offline, nothing sent anywhere),
through a small PowerShell script. Franklin's answer is spoken back."""

import json
import os
import subprocess
import sys
import threading
import time

from . import paths

VK_F8 = 0x77
KEY_NAME = "F8"

SCRIPT = r"""
$ErrorActionPreference = 'Stop'
$stop = $args[0]
try {
  Add-Type -AssemblyName System.Speech
  $r = New-Object System.Speech.Recognition.SpeechRecognitionEngine
  $r.SetInputToDefaultAudioDevice()
  $r.LoadGrammar((New-Object System.Speech.Recognition.DictationGrammar))
} catch {
  [Console]::Out.WriteLine((@{ error = $_.Exception.Message } | ConvertTo-Json -Compress)); exit
}
[Console]::Out.WriteLine('{"ready":true}')
$words = @()
$until = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $until) {
  if (Test-Path $stop) {
    # catch the last words: one more short listen once the key is let go
    $res = $r.Recognize([TimeSpan]::FromMilliseconds(400)); if ($res) { $words += $res.Text }
    break
  }
  $res = $r.Recognize([TimeSpan]::FromMilliseconds(700))
  if ($res) { $words += $res.Text }
}
[Console]::Out.WriteLine((@{ text = ($words -join ' ') } | ConvertTo-Json -Compress))
"""


class PushToTalk:
    def __init__(self, on_text):
        self.on_text = on_text  # called with the words heard (or an error dict)
        self.proc = None
        self.listening = False
        self._stop = os.path.join(paths.sub("ptt"), "stop")

    def start(self):
        if sys.platform != "win32" or self.listening:
            return False
        script = os.path.join(paths.sub("ptt"), "listen.ps1")
        with open(script, "w", encoding="utf-8") as f:
            f.write(SCRIPT)
        try:
            os.remove(self._stop)
        except OSError:
            pass
        self.proc = subprocess.Popen(["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, self._stop],
                                     stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, text=True,
                                     creationflags=0x08000000)
        self.listening = True
        return True

    def stop(self):
        if not self.listening:
            return
        self.listening = False
        open(self._stop, "w").close()
        proc, self.proc = self.proc, None
        threading.Thread(target=self._finish, args=(proc,), daemon=True).start()

    def _finish(self, proc):
        try:
            out, _ = proc.communicate(timeout=10)
        except subprocess.TimeoutExpired:
            proc.kill()
            out = ""
        result = {}
        for line in (out or "").splitlines():
            try:
                result.update(json.loads(line))
            except ValueError:
                pass
        if result.get("error"):
            self.on_text({"error": "Windows speech recognition isn't working (%s). Check your mic, and that "
                                   "Settings > Time & language > Speech has English installed." % result["error"][:120]})
        else:
            self.on_text({"text": (result.get("text") or "").strip()})

    def watch_key(self):
        """Hold F8 anywhere (even in a game) to talk. Polls the key, so nothing is hooked."""
        if sys.platform != "win32":
            return
        import ctypes
        get = ctypes.windll.user32.GetAsyncKeyState

        def loop():
            down = False
            while True:
                now = bool(get(VK_F8) & 0x8000)
                if now and not down:
                    self.start()
                elif down and not now:
                    self.stop()
                down = now
                time.sleep(0.05)
        threading.Thread(target=loop, daemon=True).start()
