"""Starts Game Merger: in its own window when pywebview is installed, otherwise in the browser."""

import os
import sys

from .api import Api

WEB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "web")


def run_browser():
    from .server import serve
    httpd, url = serve()
    print("Game Merger is running at %s (close this window to quit)." % url, flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass


def register_file_type():
    """Double-clicking a .gmerge file a friend sent opens it in Game Merger (per user, no admin)."""
    if sys.platform != "win32" or not getattr(sys, "frozen", False):
        return
    try:
        import winreg
        exe = sys.executable
        for key, value in ((r"Software\Classes\.gmerge", "GameMerger.Share"),
                           (r"Software\Classes\GameMerger.Share", "Game Merger shared merge"),
                           (r"Software\Classes\GameMerger.Share\DefaultIcon", '"%s",0' % exe),
                           (r"Software\Classes\GameMerger.Share\shell\open\command", '"%s" "%%1"' % exe)):
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, key) as k:
                winreg.SetValueEx(k, "", 0, winreg.REG_SZ, value)
    except OSError:
        pass


def opened_file():
    for a in sys.argv[1:]:
        if a.lower().endswith(".gmerge") and os.path.isfile(a):
            return os.path.abspath(a)
    return None


def main():
    register_file_type()
    if "--browser" in sys.argv:
        return run_browser()
    try:
        import webview
    except ImportError:
        return run_browser()
    # Let Franklin say hello on the startup screen without waiting for a click first.
    os.environ.setdefault("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--autoplay-policy=no-user-gesture-required")
    api = Api()
    api._pending = opened_file()
    window = webview.create_window("Game Merger", os.path.join(WEB, "index.html"), js_api=api,
                                   width=1360, height=860, min_size=(1100, 700), background_color="#0c0912")
    api._window = window  # underscore: keeps it out of what the page can call
    try:
        webview.start()
    finally:
        own = getattr(api, "_own", None)
        if own:
            own.stop()  # don't leave Franklin's voice model using memory after the app closes
