"""Franklin's own voice: a tiny local server around Coqui XTTS v2.

Game Merger starts this with the Python from your franklin-voice folder (the one with XTTS
installed), so the model is loaded once and stays loaded. It only listens on 127.0.0.1.

    POST /speak  {"text": "..."}  ->  audio/wav
    GET  /health                   ->  "ok" once the model is loaded

Run by hand:  .venv\\Scripts\\python.exe xtts_server.py --folder C:\\path\\to\\franklin-voice --port 5123
This file uses only the standard library plus what the franklin-voice venv already has."""

import argparse
import io
import json
import os
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PITCH_STEPS = -1.5  # matches speak.py: a touch deeper
SPEED = 1.05        # and a touch quicker


def load(folder):
    os.environ.setdefault("COQUI_TOS_AGREED", "1")
    from TTS.api import TTS
    tts = TTS("tts_models/multilingual/multi-dataset/xtts_v2")
    sample = os.path.join(folder, "voice_sample.wav")
    if not os.path.isfile(sample):
        raise SystemExit("voice_sample.wav not found in %s" % folder)
    latents = tts.synthesizer.tts_model.get_conditioning_latents(audio_path=[sample])  # do the slow part once
    return tts, latents


def synth(tts, latents, text):
    import numpy as np
    import soundfile as sf
    model = tts.synthesizer.tts_model
    gpt_latent, speaker_emb = latents
    out = model.inference(text, "en", gpt_latent, speaker_emb, temperature=0.7, enable_text_splitting=True)
    wav = np.asarray(out["wav"], dtype="float32")
    sr = 24000
    try:
        import librosa
        wav = librosa.effects.pitch_shift(wav, sr=sr, n_steps=PITCH_STEPS)
        wav = librosa.effects.time_stretch(wav, rate=SPEED)
    except Exception:
        pass  # librosa missing: plain voice is fine
    buf = io.BytesIO()
    sf.write(buf, wav, sr, format="WAV")
    return buf.getvalue()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--folder", required=True)
    ap.add_argument("--port", type=int, default=5123)
    args = ap.parse_args()
    state = {"tts": None}
    lock = threading.Lock()

    def warm():
        state["tts"] = load(args.folder)

    threading.Thread(target=warm, daemon=True).start()

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *a):
            pass

        def _send(self, code, body, ctype="text/plain"):
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            if self.path == "/health":
                return self._send(200 if state["tts"] else 503, b"ok" if state["tts"] else b"loading")
            self._send(404, b"")

        def do_POST(self):
            if self.path != "/speak":
                return self._send(404, b"")
            if not state["tts"]:
                return self._send(503, b"loading")
            try:
                n = int(self.headers.get("Content-Length") or 0)
                text = json.loads(self.rfile.read(n).decode("utf-8")).get("text", "").strip()[:1200]
                if not text:
                    return self._send(400, b"no text")
                with lock:  # one at a time on CPU
                    audio = synth(*state["tts"], text)
                self._send(200, audio, "audio/wav")
            except Exception as e:
                self._send(500, str(e).encode("utf-8", "replace"))

    ThreadingHTTPServer(("127.0.0.1", args.port), Handler).serve_forever()


if __name__ == "__main__":
    main()
