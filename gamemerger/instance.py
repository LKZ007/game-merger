"""Keeps Frankenstein to one window: when you double-click a .gmerge file or a Nexus "Mod Manager
Download" link while the app is open, the new copy hands it to the open one and closes."""

import json
import os
import secrets
import threading
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from . import paths


def _file():
    return os.path.join(paths.data_dir(), "instance.json")


def listen(on_message):
    token = secrets.token_hex(16)

    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *a):
            pass

        def do_POST(self):
            if self.path != "/handoff" or self.headers.get("X-GM-Token") != token:
                self.send_response(403)
                self.end_headers()
                return
            n = int(self.headers.get("Content-Length") or 0)
            try:
                on_message(json.loads(self.rfile.read(n).decode("utf-8"))["arg"])
                self.send_response(200)
            except (ValueError, KeyError):
                self.send_response(400)
            self.end_headers()

    httpd = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    with open(_file(), "w", encoding="utf-8") as f:
        json.dump({"port": httpd.server_address[1], "token": token, "pid": os.getpid()}, f)
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    return httpd


def hand_off(arg):
    """Give arg to an already-open Frankenstein. True if one took it."""
    try:
        with open(_file(), encoding="utf-8") as f:
            info = json.load(f)
        req = urllib.request.Request("http://127.0.0.1:%d/handoff" % info["port"], method="POST",
                                     data=json.dumps({"arg": arg}).encode("utf-8"),
                                     headers={"X-GM-Token": info["token"], "Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=3) as r:
            return r.status == 200
    except (OSError, ValueError, KeyError):
        return False
