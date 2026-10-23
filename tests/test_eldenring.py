"""Frankenstein's Elden Ring mod: the regulation round trip, on a small regulation built here the way the game lays it out."""
import os
import struct
import sys
import tempfile
import unittest
import zlib

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from gamemerger import carryover, eldenring as er  # noqa: E402


def make_param(ids, size=er.ROW_SIZE):
    """A little-endian PARAM with long offsets, named rows and its type at an offset, like Elden Ring's."""
    n = len(ids)
    head = bytearray(0x40)
    struct.pack_into("<H", head, 0x0A, n)
    rows_start = 0x40
    data_start = rows_start + n * 24
    strings = data_start + n * size
    head[0x2C], head[0x2D], head[0x2E], head[0x2F] = 0, 0x85, 0, 5
    struct.pack_into("<q", head, 0x10, strings)
    struct.pack_into("<q", head, 0x30, data_start)
    table = b"".join(struct.pack("<iiqq", i, 0, data_start + k * size, 0) for k, i in enumerate(ids))
    rows = b"".join(bytes([k % 7]) * size for k in range(n))
    return bytes(head) + table + rows + b"CHARACTER_INIT_PARAM\0"


def make_bnd(files):
    """A BND4 with format 0x74 (IDs, names, compression sizes), like Elden Ring's regulation."""
    n = len(files)
    fhs = 0x24
    names = b""
    name_offs = []
    base = 0x40 + n * fhs
    for name, _d in files:
        name_offs.append(base + len(names))
        names += name.encode("utf-16-le") + b"\0\0"
    data_off = base + len(names)
    data_off += -data_off % 16
    head = bytearray(b"BND4" + b"\0" * 0x3C)
    head[10] = 1  # bit big endian off
    struct.pack_into("<iq", head, 0x0C, n, 0x40)
    head[0x18:0x20] = b"07D7R6\0\0"
    struct.pack_into("<qq", head, 0x20, fhs, data_off)
    head[0x30], head[0x31] = 1, 0x74
    entries = b""
    blob = b""
    for (name, d), noff in zip(files, name_offs):
        entries += struct.pack("<B3xiqqIiI", 0x40, -1, len(d), len(d), data_off + len(blob), len(entries) // fhs, noff)
        blob += d + b"\0" * (-len(d) % 16)
    out = bytes(head) + entries + names
    return out + b"\0" * (data_off - len(out)) + blob


def make_dcx(data):
    body = zlib.compress(data, 9)
    h = bytearray(b"DCX\0") + struct.pack(">5i", 0x11000, 0x18, 0x24, 0x44, 0x4C)
    h += b"DCS\0" + struct.pack(">II", len(data), len(body)) + b"DCP\0DFLT" + struct.pack(">i", 0x20)
    h += bytes([9, 0, 0, 0]) + struct.pack(">iiii", 0, 0, 0, 0x10100) + b"DCA\0" + struct.pack(">i", 8)
    assert len(h) == 0x4C
    return bytes(h) + body


class EldenRing(unittest.TestCase):
    def regulation(self):
        ids = [2400, 3000, 3001, 3002, 3003, 3004, 3005, 3006, 3007, 3008, 3009, 20000]
        bnd = make_bnd([("N:\\GR\\data\\Param\\param\\GameParam\\AtkParam_Npc.param", b"x" * 40),
                        ("N:\\GR\\data\\Param\\param\\GameParam\\CharaInitParam.param", make_param(ids))])
        return er.encrypt_regulation(make_dcx(bnd)), bnd

    def test_round_trip_and_kits(self):
        raw, bnd = self.regulation()
        header, kind, got = er.dcx_unpack(er.decrypt_regulation(raw))
        self.assertEqual((kind, got), ("DFLT", bnd))
        files = er.bnd4_files(got)
        self.assertEqual([f["name"].split("\\")[-1] for f in files], ["AtkParam_Npc.param", "CharaInitParam.param"])
        buf = bytearray(got)
        self.assertEqual(set(er.apply_kits(buf)), {"Gryffindor", "Slytherin", "Ravenclaw", "Hufflepuff"})
        p = files[1]["offset"]
        ptype, rows, size = er.param_rows(bytes(buf[p:p + files[1]["size"]]))
        self.assertEqual((ptype, size), ("CHARACTER_INIT_PARAM", er.ROW_SIZE))
        off = p + rows[3004]  # Ravenclaw
        self.assertEqual(er._get(buf, off, "equip_Wep_Right"), er.WAND[0])
        self.assertEqual(er._get(buf, off, "equip_Spell_02"), 4420)  # Glacius
        self.assertEqual(er._get(buf, off, "soulLv"), sum(er.HOUSES[2]["stats"]) - 79)
        self.assertEqual(er._get(buf, off, "baseMag"), 20)
        self.assertEqual(len(buf), len(got))                       # nothing grows or moves
        self.assertEqual(buf[p + rows[3000]:p + rows[3000] + size], got[p + rows[3000]:p + rows[3000] + size])  # others untouched
        again = er.dcx_unpack(er.decrypt_regulation(er.encrypt_regulation(er.dcx_pack(header, kind, bytes(buf)))))[2]
        self.assertEqual(again, bytes(buf))

    def test_build_writes_only_the_mod_folder(self):
        raw, _bnd = self.regulation()
        with tempfile.TemporaryDirectory() as d:
            os.makedirs(os.path.join(d, "Game"))
            for f, b in (("eldenring.exe", b"MZ"), ("regulation.bin", raw)):
                with open(os.path.join(d, "Game", f), "wb") as fh:
                    fh.write(b)
            got = er.build(d)
            self.assertTrue(got["ok"], got)
            with open(os.path.join(d, "Game", "regulation.bin"), "rb") as fh:
                self.assertEqual(fh.read(), raw)                    # the copy's own file is left as it was
            self.assertTrue(os.path.isfile(os.path.join(d, er.MOD_DIR, "regulation.bin")))
            self.assertTrue(os.path.isfile(os.path.join(d, *er.DEBUG.split("/"))))
            cfg = open(os.path.join(d, er.CONFIG)).read()
            self.assertIn('path = "frankenstein"', cfg)
            self.assertEqual(open(os.path.join(d, "Game", "steam_appid.txt")).read(), "1245620")
            self.assertIsNone(er.launch_cmd(d))                      # no Mod Engine 2 yet: Play falls back
            os.makedirs(os.path.join(d, "modengine2"))
            open(os.path.join(d, "modengine2_launcher.exe"), "wb").close()
            cmd = er.launch_cmd(d)
            self.assertEqual(cmd[1:4], ["-t", "er", "-p"])
            self.assertTrue(cmd[4].startswith(d))                    # the copy's exe, never the original

    def test_bad_regulation_is_never_half_built(self):
        with tempfile.TemporaryDirectory() as d:
            os.makedirs(os.path.join(d, "Game"))
            open(os.path.join(d, "Game", "eldenring.exe"), "wb").close()
            with open(os.path.join(d, "Game", "regulation.bin"), "wb") as fh:
                fh.write(os.urandom(4096))
            got = er.build(d)
            self.assertFalse(got["ok"])
            self.assertTrue(got["error"])
            self.assertFalse(os.path.isfile(os.path.join(d, er.MOD_DIR, "regulation.bin")))

    def test_hogwarts_save_is_read(self):
        import sqlite3
        with tempfile.TemporaryDirectory() as d:
            db = os.path.join(d, "x.db")
            con = sqlite3.connect(db)
            con.execute("create table MiscDataDynamic (DataOwner text, DataName text, DataValue text)")
            con.executemany("insert into MiscDataDynamic values (?,?,?)", [("Player", "PlayerFirstName", "Levy"),
                            ("Player", "PlayerLastName", "Zokol"), ("Player", "HouseID", "Ravenclaw")])
            con.commit()
            con.close()
            blob = open(db, "rb").read()
            os.makedirs(os.path.join(d, "save", "123"))
            with open(os.path.join(d, "save", "123", "HL-00-00.sav"), "wb") as fh:
                fh.write(b"GVAS" + b"\0" * 50 + struct.pack("<i", len(blob)) + blob + b"\0" * 20)
            card = carryover.read("Hogwarts Legacy", roots=[os.path.join(d, "save", "*")])
            self.assertEqual((card["name"], card["house"]), ("Levy Zokol", "Ravenclaw"))
            self.assertIn("Astrologer", er.lookalike(card))


if __name__ == "__main__":
    unittest.main()


class Adopt(unittest.TestCase):
    def test_old_elden_ring_merge_moves_onto_the_recipe_in_place(self):
        import types
        from gamemerger import api, mergeplan, playas, recipes
        r = next(x for x in recipes.load_recipes() if x.id == "eldenring")
        playas.add_to(r)
        mergeplan.add_world(r)
        a = api.Api.__new__(api.Api)
        a._hand = [r]
        a._recipes = [r]
        with tempfile.TemporaryDirectory() as d:
            os.makedirs(os.path.join(d, "Game"))
            open(os.path.join(d, "Game", "eldenring.exe"), "wb").close()
            saved = []
            m = types.SimpleNamespace(id="m1", target_path=d, save=lambda: saved.append(1),
                                      data={"recipe_id": "auto-elden-ring-abc123", "source_name": "Hogwarts Legacy",
                                            "theme_name": "Hogwarts Legacy style",
                                            "adds": [{"slot": "Hogwarts Legacy-style mods", "ok": False},
                                                     {"slot": "x", "ok": True, "source": "Nexus", "mod": "kept"}]})
            old_dir, old_slot = api.Merge.record_dir, api.saves.create_slot
            slots = []
            api.Merge.record_dir = staticmethod(lambda mid: d)
            api.saves.create_slot = lambda mid, rec, **k: slots.append((mid, rec.id))
            try:
                self.assertTrue(a._adopt_builtin(m))
                self.assertFalse(a._adopt_builtin(m))  # once
            finally:
                api.Merge.record_dir, api.saves.create_slot = old_dir, old_slot
            self.assertEqual((m.data["recipe_id"], m.data["theme_id"]), ("eldenring", "hogwarts"))
            self.assertEqual(slots, [("m1", "eldenring")])          # the merge gets its own copy of your saves
            names = [x["slot"] for x in m.data["adds"]]
            self.assertEqual(names[0], "Wand and Hogwarts spells")
            self.assertIn("x", names)                                # mods it installed stay
            self.assertNotIn("Hogwarts Legacy-style mods", names)
            self.assertTrue(all(x["ok"] for x in m.data["adds"]))
