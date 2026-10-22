"""Discord status: while you play a merge, your Discord profile shows "Playing Frankenstein" with the
merge's name, like "Night City Horizon · Cyberpunk 2077 × Forza".

Talks to the Discord app on this PC over its local connection (no Discord login or bot needed).
Discord needs an application ID to show a name; Frankenstein's is built in, or you can paste your own."""

import json
import os
import struct
import sys
import threading
import time
import uuid

APP_ID = ""  # Frankenstein's Discord application ID (set once it's registered)


class Presence:
    def __init__(self):
        self.pipe = None
        self.app_id = None
        self.lock = threading.Lock()

    def _connect(self, app_id):
        if sys.platform != "win32" or not app_id:
            return False
        if self.pipe and self.app_id == app_id:
            return True
        self.close()
        for i in range(10):
            try:
                self.pipe = open(r"\\?\pipe\discord-ipc-%d" % i, "r+b", buffering=0)
                break
            except OSError:
                continue
        if not self.pipe:
            return False  # Discord isn't running
        try:
            self._send(0, {"v": 1, "client_id": app_id})
            self._read()
        except OSError:
            self.close()
            return False
        self.app_id = app_id
        return True

    def _send(self, op, payload):
        data = json.dumps(payload).encode("utf-8")
        self.pipe.write(struct.pack("<ii", op, len(data)) + data)

    def _read(self):
        head = self.pipe.read(8)
        if len(head) < 8:
            raise OSError("closed")
        _, n = struct.unpack("<ii", head)
        return json.loads(self.pipe.read(n).decode("utf-8") or "{}")

    def show(self, app_id, details, state, started=None):
        """Show what's being played. False if Discord isn't open (it's simply skipped)."""
        activity = {"details": details[:128], "state": state[:128], "timestamps": {"start": int(started or time.time())},
                    "instance": False}
        return self._activity(app_id, activity)

    def clear(self, app_id):
        return self._activity(app_id, None)

    def _activity(self, app_id, activity):
        with self.lock:
            for _ in range(2):  # reconnect once if Discord restarted
                if not self._connect(app_id):
                    return False
                try:
                    self._send(1, {"cmd": "SET_ACTIVITY", "args": {"pid": os.getpid(), "activity": activity},
                                   "nonce": str(uuid.uuid4())})
                    self._read()
                    return True
                except OSError:
                    self.close()
            return False

    def close(self):
        if self.pipe:
            try:
                self.pipe.close()
            except OSError:
                pass
        self.pipe = None
        self.app_id = None
