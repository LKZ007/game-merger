"""Browser mode: serves the same window in your normal web browser, for PCs where the
built-in window (pywebview / Edge WebView2) isn't available. Listens on this PC only."""

import json
import mimetypes
import os
import secrets
import threading
import webbrowser
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

from .api import Api

WEB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "web")


def make_handler(api, token):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_args):
            pass

        def _send(self, code, body, ctype="application/json"):
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            path = self.path.split("?")[0]
            rel = "index.html" if path in ("/", "") else path.lstrip("/")
            full = os.path.normpath(os.path.join(WEB, rel))
            if not full.startswith(WEB) or not os.path.isfile(full):
                return self._send(404, b"not found", "text/plain")
            with open(full, "rb") as f:
                body = f.read()
            if rel == "index.html":
                body = body.replace(b"<head>", b'<head><meta name="gm-token" content="%s">' % token.encode(), 1)
            self._send(200, body, mimetypes.guess_type(full)[0] or "application/octet-stream")

        def do_POST(self):
            name = self.path.rsplit("/", 1)[-1]
            if self.headers.get("X-GM-Token") != token or name.startswith("_") or not hasattr(api, name):
                return self._send(403, b'{"error":"forbidden"}')
            length = int(self.headers.get("Content-Length") or 0)
            args = json.loads(self.rfile.read(length) or b"[]")
            try:
                result = getattr(api, name)(*args)
                self._send(200, json.dumps({"result": result}).encode())
            except Exception as e:
                self._send(200, json.dumps({"error": str(e)}).encode())
    return Handler


def serve(port=0, open_browser=True, api=None):
    api = api or Api()
    token = secrets.token_hex(16)
    httpd = ThreadingHTTPServer(("127.0.0.1", port), make_handler(api, token))
    url = "http://127.0.0.1:%d/" % httpd.server_address[1]
    if open_browser:
        threading.Timer(0.5, lambda: webbrowser.open(url)).start()
    return httpd, url
