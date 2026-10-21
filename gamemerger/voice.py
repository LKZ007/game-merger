"""Franklin's voice.

By default he uses Microsoft's free natural-sounding British voices (the "Read aloud" voices
in Edge, through edge-tts), which need no key and sound far more human than the old built-in
Windows voices. With an ElevenLabs key he gets a proper Manchester accent instead. If neither
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
    """Pick a male Manchester voice from the ElevenLabs voice library and add it to the account."""
    for query in ({"search": "manchester", "gender": "male"}, {"search": "mancunian", "gender": "male"},
                  {"accent": "british", "gender": "male", "search": "northern"}):
        q = dict(query, page_size="10")
        found = json.loads(_request("GET", "/shared-voices?" + urllib.parse.urlencode(q), key))
        voices = found.get("voices") or []
        if voices:
            v = voices[0]
            added = json.loads(_request("POST", "/voices/add/%s/%s" % (v["public_owner_id"], v["voice_id"]), key,
                                        {"new_name": "Franklin"}))
            return added.get("voice_id") or v["voice_id"], v.get("name", "")
    raise VoiceError("Couldn't find a Manchester voice in the ElevenLabs library.")


def speak(key, voice_id, text):
    """Return an mp3 data URI of `text` spoken in the given voice."""
    audio = _request("POST", "/text-to-speech/%s?output_format=mp3_44100_128" % voice_id, key,
                     {"text": text, "model_id": MODEL}, accept="audio/mpeg")
    return "data:audio/mpeg;base64," + base64.b64encode(audio).decode("ascii")


# Free natural British male voices. Ryan is the default.
NEURAL_VOICES = {"ryan": ("en-GB-RyanNeural", "Ryan (British, warm)"),
                 "thomas": ("en-GB-ThomasNeural", "Thomas (British, lighter)")}


def neural(text, style="ryan"):
    """Return an mp3 data URI of `text` in a natural British voice, or raise VoiceError."""
    try:
        import edge_tts
    except ImportError:
        raise VoiceError("Natural voice isn't installed.")
    name = NEURAL_VOICES.get(style, NEURAL_VOICES["ryan"])[0]

    async def run():
        audio = bytearray()
        async for chunk in edge_tts.Communicate(text, name, rate="-4%", pitch="-2Hz").stream():
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
