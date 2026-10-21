"""Franklin's voice.

By default he uses Microsoft's free natural-sounding voices (the "Read aloud" voices
in Edge, through edge-tts), which need no key and sound far more human than the old built-in
Windows voices. With an ElevenLabs key he gets an even more lifelike voice. If neither
can be reached (offline), the window falls back to the voice built into Windows."""

import asyncio
import base64
import json
import urllib.error
import urllib.parse
import urllib.request

API = "https://api.elevenlabs.io/v1"
MODEL = "eleven_multilingual_v2"


class VoiceError(Exception):
    pass


def _request(method, path, key, body=None, accept="application/json"):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method,
                                 headers={"xi-api-key": key, "Content-Type": "application/json", "Accept": accept})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.read()
    except urllib.error.HTTPError as e:
        if e.code == 401:
            raise VoiceError("Your ElevenLabs key didn't work. Check it in Settings.")
        raise VoiceError("ElevenLabs returned an error (%s)." % e.code)
    except OSError:
        raise VoiceError("Can't reach ElevenLabs right now.")


def find_manchester_voice(key):
    """Pick a warm male radio-host style voice from the ElevenLabs voice library and add it to the account."""
    for query in ({"search": "radio host", "gender": "male", "accent": "american"},
                  {"search": "smooth narrator", "gender": "male", "accent": "american"},
                  {"search": "radio", "gender": "male"}):
        q = dict(query, page_size="10")
        found = json.loads(_request("GET", "/shared-voices?" + urllib.parse.urlencode(q), key))
        voices = found.get("voices") or []
        if voices:
            v = voices[0]
            added = json.loads(_request("POST", "/voices/add/%s/%s" % (v["public_owner_id"], v["voice_id"]), key,
                                        {"new_name": "Franklin"}))
            return added.get("voice_id") or v["voice_id"], v.get("name", "")
    raise VoiceError("Couldn't find a radio-host voice in the ElevenLabs library.")


def speak(key, voice_id, text):
    """Return an mp3 data URI of `text` spoken in the given voice."""
    audio = _request("POST", "/text-to-speech/%s?output_format=mp3_44100_128" % voice_id, key,
                     {"text": text, "model_id": MODEL}, accept="audio/mpeg")
    return "data:audio/mpeg;base64," + base64.b64encode(audio).decode("ascii")


# Free natural voices. Andrew (warm, smooth, radio-host feel) is the default.
NEURAL_VOICES = {"andrew": ("en-US-AndrewNeural", "Andrew (deep, smooth)"),
                 "brian": ("en-US-BrianNeural", "Brian (relaxed, friendly)"),
                 "christopher": ("en-US-ChristopherNeural", "Christopher (deep, calm)"),
                 "roger": ("en-US-RogerNeural", "Roger (lively)"),
                 "ryan": ("en-GB-RyanNeural", "Ryan (British)"),
                 "thomas": ("en-GB-ThomasNeural", "Thomas (British)")}
DEFAULT_VOICE = "andrew"


def neural(text, style=DEFAULT_VOICE):
    """Return an mp3 data URI of `text` in a natural British voice, or raise VoiceError."""
    try:
        import edge_tts
    except ImportError:
        raise VoiceError("Natural voice isn't installed.")
    name = NEURAL_VOICES.get(style, NEURAL_VOICES[DEFAULT_VOICE])[0]

    async def run():
        audio = bytearray()
        async for chunk in edge_tts.Communicate(text, name, rate="-3%", pitch="-12Hz")  # deeper.stream():
            if chunk["type"] == "audio":
                audio.extend(chunk["data"])
        return bytes(audio)

    try:
        audio = asyncio.run(run())
    except Exception as e:  # network trouble or the service changed: fall back to the Windows voice
        raise VoiceError("Couldn't reach the natural voice service (%s)." % type(e).__name__)
    if not audio:
        raise VoiceError("The natural voice service sent no audio.")
    return "data:audio/mpeg;base64," + base64.b64encode(audio).decode("ascii")


# ---------- Franklin's own voice (local XTTS clone) ----------

class LocalVoice:
    """Runs xtts_server.py with the Python from the franklin-voice folder and keeps it running.
    Clips are cached, so lines Franklin says often (greetings) are instant after the first time."""

    PORT = 5123

    def __init__(self, folder, script, cache_dir):
        self.folder, self.script, self.cache_dir = folder, script, cache_dir
        self.proc = None

    @staticmethod
    def find(folder=None):
        """The franklin-voice folder if it has a venv and a voice sample, else None."""
        import os
        for f in [folder, os.path.join(os.path.expanduser("~"), "claude", "franklin-voice")]:
            if f and os.path.isfile(os.path.join(f, "voice_sample.wav")) and os.path.isfile(LocalVoice.python(f)):
                return f
        return None

    @staticmethod
    def python(folder):
        import os
        return os.path.join(folder, ".venv", "Scripts", "python.exe")

    def start(self):
        import subprocess
        if self.proc and self.proc.poll() is None:
            return
        if self.ready():
            return  # already running (e.g. left over from a previous start)
        self.proc = subprocess.Popen([self.python(self.folder), self.script, "--folder", self.folder,
                                      "--port", str(self.PORT)], cwd=self.folder,
                                     stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                     creationflags=0x08000000)  # no console window

    def stop(self):
        if self.proc and self.proc.poll() is None:
            self.proc.terminate()

    def ready(self):
        try:
            with urllib.request.urlopen("http://127.0.0.1:%d/health" % self.PORT, timeout=1) as r:
                return r.status == 200
        except OSError:
            return False

    def _cache(self, text):
        import hashlib
        import os
        key = hashlib.sha1(text.strip().lower().encode("utf-8")).hexdigest()[:20]
        return os.path.join(self.cache_dir, key + ".wav")

    def wav(self, text):
        import os
        path = self._cache(text)
        if not os.path.isfile(path):
            req = urllib.request.Request("http://127.0.0.1:%d/speak" % self.PORT, method="POST",
                                         data=json.dumps({"text": text}).encode("utf-8"),
                                         headers={"Content-Type": "application/json"})
            try:
                with urllib.request.urlopen(req, timeout=180) as r:
                    audio = r.read()
            except OSError:
                raise VoiceError("Franklin's own voice isn't ready yet.")
            os.makedirs(self.cache_dir, exist_ok=True)
            with open(path + ".part", "wb") as f:
                f.write(audio)
            os.replace(path + ".part", path)
        with open(path, "rb") as f:
            return "data:audio/wav;base64," + base64.b64encode(f.read()).decode("ascii")
