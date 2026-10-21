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


def bring_to_front(window):
    """Show the open window when someone starts the app again (un-minimise it, put it on top)."""
    if sys.platform != "win32":
        return
    try:
        import ctypes
        hwnd = window.native.Handle.ToInt32()
        u32 = ctypes.windll.user32
        if u32.IsIconic(hwnd):
            u32.ShowWindow(hwnd, 9)  # SW_RESTORE: back to how it was, full screen included
        u32.SetForegroundWindow(hwnd)
    except Exception:
        pass  # not worth failing over; the window is still open


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
    if "--browser" not in sys.argv and not instance.claim():
        # already open: pass on what we were opened with (or just bring it to the front) and leave
        instance.hand_off_patiently(arg or instance.FOCUS)
        return
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
    # borderless like a game launcher: the page draws its own minimise / full screen / close buttons
    window = webview.create_window("Frankenstein", os.path.join(WEB, "index.html"), js_api=api,
                                   width=1360, height=860, min_size=(1100, 700), background_color="#0c0912",
                                   frameless=True, easy_drag=False,
                                   fullscreen=api._settings.get("fullscreen", True))
    api._window = window  # underscore: keeps it out of what the page can call

    def on_handoff(msg):
        if msg != instance.FOCUS:
            api._handoffs.append(msg)
        bring_to_front(window)
    instance.listen(on_handoff)
    try:
        webview.start()
    finally:
        own = getattr(api, "_own", None)
        if own:
            own.stop()  # don't leave Franklin's voice model using memory after the app closes
