"""Starts Frankenstein: in its own window when pywebview is installed, otherwise in the browser."""

import os
import sys

from . import instance
from .api import Api

WEB = os.path.join(os.path.dirname(os.path.abspath(__file__)), "web")


def run_browser():
    from .server import serve
    httpd, url = serve()
    print("Frankenstein is running at %s (close this window to quit)." % url, flush=True)
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        pass


def register_file_type():
    """Double-clicking a .gmerge file a friend sent opens it in Frankenstein (per user, no admin)."""
    if sys.platform != "win32" or not getattr(sys, "frozen", False):
        return
    try:
        import winreg
        exe = sys.executable
        for key, value in ((r"Software\Classes\.gmerge", "GameMerger.Share"),
                           (r"Software\Classes\GameMerger.Share", "Frankenstein shared merge"),
                           (r"Software\Classes\GameMerger.Share\DefaultIcon", '"%s",0' % exe),
                           (r"Software\Classes\GameMerger.Share\shell\open\command", '"%s" "%%1"' % exe)):
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, key) as k:
                winreg.SetValueEx(k, "", 0, winreg.REG_SZ, value)
    except OSError:
        pass


def register_nxm():
    """Make Nexus "Mod Manager Download" buttons send files to Frankenstein (only once the player adds
    a Nexus key, so it never takes over from Vortex or Mod Organizer without asking)."""
    if sys.platform != "win32" or not getattr(sys, "frozen", False):
        return
    try:
        import winreg
        exe = sys.executable
        for key, name, value in ((r"Software\Classes\nxm", "", "URL:NXM Protocol"),
                                 (r"Software\Classes\nxm", "URL Protocol", ""),
                                 (r"Software\Classes\nxm\shell\open\command", "", '"%s" "%%1"' % exe)):
            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, key) as k:
                winreg.SetValueEx(k, name, 0, winreg.REG_SZ, value)
    except OSError:
        pass


def opened_arg():
    """A .gmerge file or nxm:// link the app was started with."""
    for a in sys.argv[1:]:
        if a.lower().startswith("nxm://"):
            return a
        if a.lower().endswith(".gmerge") and os.path.isfile(a):
            return os.path.abspath(a)
    return None


def main():
    arg = opened_arg()
    if arg and instance.hand_off(arg):
        return  # the open window takes it from here
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
    api._handoffs = [arg] if arg else []
    api._register_nxm = register_nxm
    instance.listen(api._handoffs.append)
    window = webview.create_window("Frankenstein", os.path.join(WEB, "index.html"), js_api=api,
                                   width=1360, height=860, min_size=(1100, 700), background_color="#0c0912")
    api._window = window  # underscore: keeps it out of what the page can call
    try:
        webview.start()
    finally:
        own = getattr(api, "_own", None)
        if own:
            own.stop()  # don't leave Franklin's voice model using memory after the app closes
