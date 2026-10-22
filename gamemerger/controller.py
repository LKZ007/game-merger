"""Every game playable with a controller.

Games with full controller support just use it. For the rest, Frankenstein runs its own small
key mapper while the game is in front: the left stick walks (WASD), the right stick moves the mouse,
the triggers click, and the buttons press keys. The mapping can be changed per merge.

It reads Xbox-style controllers through XInput (PlayStation pads work through Steam Input or DS4Windows)
and presses keys the same way a keyboard does, using scan codes so games see them."""

import ctypes
import os
import sys
import threading
import time

BUTTONS = {  # XInput bit -> name
    0x0001: "dpad_up", 0x0002: "dpad_down", 0x0004: "dpad_left", 0x0008: "dpad_right",
    0x0010: "start", 0x0020: "back", 0x0040: "ls", 0x0080: "rs",
    0x0100: "lb", 0x0200: "rb", 0x1000: "a", 0x2000: "b", 0x4000: "x", 0x8000: "y",
}
NAMES = {"a": "A", "b": "B", "x": "X", "y": "Y", "lb": "LB", "rb": "RB", "lt": "LT", "rt": "RT", "ls": "Left stick click",
         "rs": "Right stick click", "start": "Start", "back": "Back / View", "dpad_up": "D-pad up", "dpad_down": "D-pad down",
         "dpad_left": "D-pad left", "dpad_right": "D-pad right"}
# What each button presses unless the merge says otherwise. Works for most PC games' default keys.
DEFAULT = {"a": "space", "b": "ctrl", "x": "r", "y": "e", "lb": "q", "rb": "f", "lt": "mouse_right", "rt": "mouse_left",
           "ls": "shift", "rs": "v", "start": "esc", "back": "tab", "dpad_up": "1", "dpad_down": "2", "dpad_left": "3",
           "dpad_right": "4", "left_stick": "wasd", "right_stick": "mouse", "sensitivity": 14}
SCAN = {"esc": 0x01, "1": 0x02, "2": 0x03, "3": 0x04, "4": 0x05, "5": 0x06, "6": 0x07, "7": 0x08, "8": 0x09, "9": 0x0A,
        "0": 0x0B, "tab": 0x0F, "q": 0x10, "w": 0x11, "e": 0x12, "r": 0x13, "t": 0x14, "y": 0x15, "u": 0x16, "i": 0x17,
        "o": 0x18, "p": 0x19, "enter": 0x1C, "ctrl": 0x1D, "a": 0x1E, "s": 0x1F, "d": 0x20, "f": 0x21, "g": 0x22,
        "h": 0x23, "j": 0x24, "k": 0x25, "l": 0x26, "shift": 0x2A, "z": 0x2C, "x": 0x2D, "c": 0x2E, "v": 0x2F,
        "b": 0x30, "n": 0x31, "m": 0x32, "alt": 0x38, "space": 0x39, "f1": 0x3B, "f2": 0x3C, "f3": 0x3D, "f4": 0x3E,
        "f5": 0x3F, "up": 0xC8, "left": 0xCB, "right": 0xCD, "down": 0xD0}
KEYS = sorted(SCAN) + ["mouse_left", "mouse_right", "mouse_middle", "wheel_up", "wheel_down", "none"]
DEADZONE = 0.24


def badge(status):
    """What the controller badge says for a store status."""
    return {"full": ("native", "Full controller support"),
            "partial": ("mapped", "Partial controller support; Frankenstein's key mapper fills the gaps"),
            }.get(status, ("mapped", "Played with Frankenstein's key mapper (sticks move and look, buttons press keys)"))


def merged_map(custom):
    m = dict(DEFAULT)
    m.update({k: v for k, v in (custom or {}).items() if k in DEFAULT})
    return m


# ---------- Windows plumbing ----------

class _Gamepad(ctypes.Structure):
    _fields_ = [("buttons", ctypes.c_ushort), ("lt", ctypes.c_ubyte), ("rt", ctypes.c_ubyte),
                ("lx", ctypes.c_short), ("ly", ctypes.c_short), ("rx", ctypes.c_short), ("ry", ctypes.c_short)]


class _State(ctypes.Structure):
    _fields_ = [("packet", ctypes.c_ulong), ("pad", _Gamepad)]


class _KeyBd(ctypes.Structure):
    _fields_ = [("vk", ctypes.c_ushort), ("scan", ctypes.c_ushort), ("flags", ctypes.c_ulong),
                ("time", ctypes.c_ulong), ("extra", ctypes.c_size_t)]


class _Mouse(ctypes.Structure):
    _fields_ = [("dx", ctypes.c_long), ("dy", ctypes.c_long), ("data", ctypes.c_ulong), ("flags", ctypes.c_ulong),
                ("time", ctypes.c_ulong), ("extra", ctypes.c_size_t)]


class _Union(ctypes.Union):
    _fields_ = [("ki", _KeyBd), ("mi", _Mouse), ("pad", ctypes.c_byte * 32)]


class _Input(ctypes.Structure):
    _fields_ = [("type", ctypes.c_ulong), ("u", _Union)]


MOUSE_FLAGS = {"mouse_left": (0x0002, 0x0004), "mouse_right": (0x0008, 0x0010), "mouse_middle": (0x0020, 0x0040)}


class Mapper:
    """Turns controller input into keys and mouse while one of `processes` is the window in front."""

    def __init__(self, processes, mapping):
        self.processes = {p.lower() for p in processes}
        self.map = merged_map(mapping)
        self.down = set()
        self.stop_flag = threading.Event()
        self.thread = None

    def start(self):
        if sys.platform != "win32" or self.thread:
            return False
        try:
            self.xinput = getattr(ctypes.windll, "xinput1_4", None) or ctypes.windll.xinput9_1_0
        except OSError:
            return False
        self.user32 = ctypes.windll.user32
        self.thread = threading.Thread(target=self._loop, daemon=True)
        self.thread.start()
        return True

    def stop(self):
        self.stop_flag.set()

    # -- sending input --
    def _send(self, inp):
        self.user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(_Input))

    def _key(self, name, press):
        if name in MOUSE_FLAGS:
            inp = _Input(type=0)
            inp.u.mi = _Mouse(0, 0, 0, MOUSE_FLAGS[name][0 if press else 1], 0, 0)
            return self._send(inp)
        if name in ("wheel_up", "wheel_down"):
            if press:
                inp = _Input(type=0)
                inp.u.mi = _Mouse(0, 0, 120 if name == "wheel_up" else (-120) & 0xFFFFFFFF, 0x0800, 0, 0)
                self._send(inp)
            return
        code = SCAN.get(name)
        if code is None:
            return
        flags = 0x0008 | (0 if press else 0x0002)  # scan code, key up
        if code > 0x7F:
            flags |= 0x0001  # extended (arrow keys)
        inp = _Input(type=1)
        inp.u.ki = _KeyBd(0, code & 0x7F, flags, 0, 0)
        self._send(inp)

    def _set(self, name, on):
        if not name or name == "none":
            return
        tag = name
        if on and tag not in self.down:
            self.down.add(tag)
            self._key(name, True)
        elif not on and tag in self.down:
            self.down.discard(tag)
            self._key(name, False)

    def _release_all(self):
        for name in list(self.down):
            self._set(name, False)

    def _move(self, dx, dy):
        inp = _Input(type=0)
        inp.u.mi = _Mouse(int(dx), int(dy), 0, 0x0001, 0, 0)
        self._send(inp)

    # -- which window is in front --
    def _front_is_game(self):
        hwnd = self.user32.GetForegroundWindow()
        pid = ctypes.c_ulong()
        self.user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
        k32 = ctypes.windll.kernel32
        h = k32.OpenProcess(0x1000, False, pid.value)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not h:
            return False
        try:
            buf = ctypes.create_unicode_buffer(520)
            size = ctypes.c_ulong(520)
            if not k32.QueryFullProcessImageNameW(h, 0, buf, ctypes.byref(size)):
                return False
            return os.path.basename(buf.value).lower() in self.processes
        finally:
            k32.CloseHandle(h)

    def _loop(self):
        st = _State()
        check_front = 0
        front = False
        while not self.stop_flag.is_set():
            time.sleep(0.008)
            if time.time() > check_front:
                front = self._front_is_game()
                check_front = time.time() + 0.5
                if not front:
                    self._release_all()
            if not front or self.xinput.XInputGetState(0, ctypes.byref(st)) != 0:
                continue
            p = st.pad
            m = self.map
            for bit, name in BUTTONS.items():
                self._set(m.get(name), bool(p.buttons & bit))
            self._set(m.get("lt"), p.lt > 60)
            self._set(m.get("rt"), p.rt > 60)
            lx, ly = p.lx / 32768.0, p.ly / 32768.0
            if m.get("left_stick") == "wasd":
                self._set("w", ly > DEADZONE)
                self._set("s", ly < -DEADZONE)
                self._set("a", lx < -DEADZONE)
                self._set("d", lx > DEADZONE)
            elif m.get("left_stick") == "arrows":
                self._set("up", ly > DEADZONE)
                self._set("down", ly < -DEADZONE)
                self._set("left", lx < -DEADZONE)
                self._set("right", lx > DEADZONE)
            rx, ry = p.rx / 32768.0, p.ry / 32768.0
            if m.get("right_stick") == "mouse" and (abs(rx) > DEADZONE or abs(ry) > DEADZONE):
                speed = float(m.get("sensitivity") or 14)
                curve = lambda v: (abs(v) - DEADZONE) / (1 - DEADZONE) * (1 if v > 0 else -1) if abs(v) > DEADZONE else 0
                self._move(curve(rx) * abs(curve(rx)) * speed, -curve(ry) * abs(curve(ry)) * speed)
        self._release_all()
