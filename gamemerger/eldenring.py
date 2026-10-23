"""Elden Ring as the Host game: Frankenstein's own mod for it.

Elden Ring has no script loader like Schedule I's MelonLoader, so Frankenstein's mod for it is data plus two tools:

  Mod Engine 2       starts the merge copy offline (no Easy Anti-Cheat, so it can never reach online play) and loads
                     Frankenstein's mod folder over the game's own files, in memory. Nothing in the game is replaced.
  the house kits     on every Play, Frankenstein reads the merge copy's own regulation.bin (the game's numbers), changes
                     what the merge needs (four starting classes become the Extension's kits: wand, robes, stats and
                     spells) and writes the result into its mod folder. The copy's own regulation.bin stays as it was.
  Creative tools     the Elden Ring Practice Tool (open source, from its own GitHub page), set up by Frankenstein: item
                     spawner, no damage, unlimited FP, fly, a flying Torrent, speed and runes, on keys and on one
                     controller wheel (press both sticks).

Only bytes inside existing rows are changed, never the size or layout of anything, and every step is checked before
it's trusted (the decrypted file must be a DCX, the binder must list CharaInitParam, the rows must be the size the
param's layout says). When a check fails nothing is written and the reason goes to frankenstein-debug.txt in the mod
folder, so the game always starts with either the whole kit or the game's own numbers, never something half-done.
Elden Ring's text (item and class names) is packed inside its encrypted archives, so the Extension's names for things
are shown on the merge card and the rundown instead of in the game's menus.
"""
import glob
import json
import os
import re
import struct
import subprocess
import threading
import time
import zlib

MOD_DIR = "frankenstein"  # Frankenstein's mod folder in the merge copy, loaded by Mod Engine 2
CONFIG = "frankenstein_eldenring.toml"
DEBUG = MOD_DIR + "/frankenstein-debug.txt"
TOOL_EXE = "jdsd_er_practice_tool.exe"
TOOL_CONFIG = "jdsd_er_practice_tool.toml"
APPID = "1245620"
ER_KEY = bytes.fromhex("99BFFC366A6BC8C6F5827D093602D676C42892A01C207FB024D3AF4E493FEF99")

# CharaInitParam (Paramdex ER layout, 0x140 bytes a row): field -> (offset, struct format)
ROW_SIZE = 0x140
FIELDS = {
    "equip_Wep_Right": (16, "<i"), "equip_Subwep_Right": (20, "<i"), "equip_Wep_Left": (24, "<i"),
    "equip_Subwep_Left": (28, "<i"), "equip_Helm": (32, "<i"), "equip_Armer": (36, "<i"), "equip_Gaunt": (40, "<i"),
    "equip_Leg": (44, "<i"), "equip_Arrow": (48, "<i"), "equip_Bolt": (52, "<i"), "equip_SubArrow": (56, "<i"),
    "equip_SubBolt": (60, "<i"),
    **{"equip_Spell_%02d" % (i + 1): (96 + 4 * i, "<i") for i in range(7)},
    **{"item_%02d" % (i + 1): (124 + 4 * i, "<i") for i in range(10)},
    "arrowNum": (178, "<H"), "boltNum": (180, "<H"), "subArrowNum": (182, "<H"), "subBoltNum": (184, "<H"),
    "soulLv": (192, "<h"),
    "baseVit": (194, "<B"), "baseWil": (195, "<B"), "baseEnd": (196, "<B"), "baseStr": (197, "<B"),
    "baseDex": (198, "<B"), "baseMag": (199, "<B"), "baseFai": (200, "<B"), "baseLuc": (201, "<B"),
    **{"itemNum_%02d" % (i + 1): (204 + i, "<B") for i in range(10)},
}
STATS = ("baseVit", "baseWil", "baseEnd", "baseStr", "baseDex", "baseMag", "baseFai", "baseLuc")

# ---------- the Extension's content, built from Elden Ring's own items ----------

# Hogwarts spells as Elden Ring sorceries (all cast from the wand): (spell, sorcery id, Elden Ring's name, what it does)
SPELLS = [
    ("Stupefy", 4000, "Glintstone Pebble", "a quick stunning bolt"),
    ("Lumos", 4140, "Starlight", "a light that follows you"),
    ("Protego", 4630, "Thops's Barrier", "a shield that blocks spells"),
    ("Expelliarmus", 4640, "Carian Retaliation", "turns a spell back on the caster"),
    ("Depulso", 4110, "Rock Blaster", "blasts enemies away from you"),
    ("Incendio", 4800, "Magma Shot", "a burst of fire"),
    ("Accio", 4720, "Gravity Well", "pulls enemies towards you"),
    ("Glacius", 4420, "Freezing Mist", "freezes what it touches"),
    ("Bombarda", 4080, "Cannon of Haima", "an exploding blast"),
]
WAND = (33130000, "Astrologer's Staff")  # the wand (staves cast sorceries); the Academy staff comes later
MEMORY_STONE = 10030
BUCKLER = 30000000

# one starting class per house: (house, class row id, Elden Ring class, house spell, robes, stats, sidearm)
# robes: (hat, robe, gloves, trousers); stats: Vigor, Mind, Endurance, Strength, Dexterity, Intelligence, Faith, Arcane
HOUSES = [
    {"house": "Gryffindor", "row": 3002, "class": "Hero", "spell": "Incendio", "colours": "red and gold",
     "robes": (130000, 130100, 130200, 130300), "robe_names": "Spellblade's hat and travelling attire",
     "stats": (12, 11, 11, 14, 10, 16, 7, 7), "side": (2040000, "Lordsworn's Straight Sword")},
    {"house": "Slytherin", "row": 3008, "class": "Prisoner", "spell": "Expelliarmus", "colours": "green and silver",
     "robes": (120000, 120100, 120200, 120300), "robe_names": "Alberich's pointed hat and robe",
     "stats": (10, 12, 9, 9, 13, 18, 6, 11), "side": (1070000, "Glintstone Kris")},
    {"house": "Ravenclaw", "row": 3004, "class": "Astrologer", "spell": "Glacius", "colours": "blue and bronze",
     "robes": (630000, 830100, 630200, 630300), "robe_names": "Astrologer hood and Raya Lucarian robe",
     "stats": (9, 15, 9, 8, 10, 20, 7, 10), "side": None},
    {"house": "Hufflepuff", "row": 3006, "class": "Confessor", "spell": "Protego", "colours": "yellow and black",
     "robes": (430000, 430100, 630200, 430300), "robe_names": "Sage hood and robe",
     "stats": (13, 12, 13, 12, 9, 16, 7, 6), "side": None},
]

CREATURES = [  # Elden Ring's own creatures standing in for the wizarding world's (their names and looks stay Elden Ring's)
    ("Trolls", "Elden Ring's own trolls (Limgrave, Liurnia and Caelid)"),
    ("Acromantula", "the giant crabs and Caelid's rot creatures stand in; Elden Ring has no giant spiders"),
    ("Dementors", "the Death Rite Birds and Night's Cavalry: night-time hunters that drain you"),
    ("Inferi", "Elden Ring's skeletons and revenants, who get back up"),
    ("Dark wizards", "the Raya Lucaria sorcerers and the Academy's battlemages"),
]


def is_hogwarts(name):
    return bool(re.search(r"hogwarts|harry potter|wizarding", name or "", re.I))


def spellbook():
    """The Extension's spells and which Elden Ring sorcery each one is: shown on the card and in the rundown."""
    return ["%s = %s (%s)" % (s, er, what) for s, _i, er, what in SPELLS]


def houses_text():
    return ["%s: pick %s at character creation (%s, %s, house spell %s)" % (
        h["house"], h["class"], h["robe_names"], h["colours"], h["spell"]) for h in HOUSES]


def lookalike(card):
    """How to recreate the Extension character (read from its save by Carry over my save) in Elden Ring's creator."""
    card = card or {}
    house = next((h for h in HOUSES if h["house"] == card.get("house")), None)
    name = card.get("name") or "your Hogwarts Legacy character"
    if house:
        return ("Your Hogwarts Legacy character: make a new character called %s, pick %s for %s, and match their face, "
                "hair and skin in Elden Ring's creator (a lookalike, not an exact copy)." % (name, house["class"], house["house"]))
    return ("Your Hogwarts Legacy character: make a new character with their name, pick your house's class, and match "
            "their face and hair in Elden Ring's creator (a lookalike). Carry over my save reads their name and house.")


# ---------- encryption and compression ----------

def _aes(key, iv, data, encrypt):
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
    c = Cipher(algorithms.AES(key), modes.CBC(iv))
    op = c.encryptor() if encrypt else c.decryptor()
    return op.update(data) + op.finalize()


def decrypt_regulation(raw):
    if len(raw) < 32 or (len(raw) - 16) % 16:
        raise ValueError("regulation.bin isn't the size an encrypted file should be")
    plain = _aes(ER_KEY, raw[:16], raw[16:], False)
    if plain[:4] != b"DCX\0":
        raise ValueError("regulation.bin didn't decrypt to a DCX file (an Elden Ring version Frankenstein doesn't know)")
    return plain


def encrypt_regulation(plain):
    pad = 16 - len(plain) % 16
    iv = os.urandom(16)
    return iv + _aes(ER_KEY, iv, plain + bytes([pad]) * pad, True)


class Oodle:
    """Oodle (KRAK) from the game's own oo2core DLL. Windows only; only needed when a file uses KRAK."""
    def __init__(self, game_dir):
        import ctypes
        dlls = sorted(glob.glob(os.path.join(game_dir, "oo2core_*_win64.dll")))
        if not dlls:
            raise ValueError("the game's Oodle DLL (oo2core_*_win64.dll) wasn't found")
        self.dll = ctypes.WinDLL(dlls[-1])
        self.ct = ctypes

    def decompress(self, data, size):
        ct = self.ct
        out = ct.create_string_buffer(size)
        n = self.dll.OodleLZ_Decompress(data, ct.c_int64(len(data)), out, ct.c_int64(size), 1, 0, 0, None, 0, None,
                                        None, None, 0, 3)
        if n != size:
            raise ValueError("Oodle couldn't unpack the file")
        return out.raw

    def compress(self, data, level):
        ct = self.ct
        out = ct.create_string_buffer(len(data) + 274 * (len(data) // 0x40000 + 2) + 0x10000)
        n = self.dll.OodleLZ_Compress(8, data, ct.c_int64(len(data)), out, level, None, None, None, None, 0)
        if n <= 0:
            raise ValueError("Oodle couldn't pack the file")
        return out.raw[:n]


def dcx_unpack(dcx, game_dir=""):
    """(header, kind, payload) of a DCX file. The header is kept as it is so the file can be packed the same way."""
    if dcx[:4] != b"DCX\0" or dcx[0x18:0x1C] != b"DCS\0" or dcx[0x44:0x48] != b"DCA\0":
        raise ValueError("not a DCX file Frankenstein knows")
    size, csize = struct.unpack(">II", dcx[0x1C:0x24])
    kind = dcx[0x28:0x2C].decode("ascii", "replace")
    body = dcx[0x4C:0x4C + csize]
    if kind == "DFLT":
        data = zlib.decompress(body)
    elif kind == "ZSTD":
        import zstandard
        data = zstandard.ZstdDecompressor().decompress(body, max_output_size=size)
    elif kind == "KRAK":
        data = Oodle(game_dir).decompress(body, size)
    else:
        raise ValueError("DCX compression %s isn't supported" % kind)
    if len(data) != size:
        raise ValueError("the unpacked file isn't the size its header says")
    return dcx[:0x4C], kind, data


def dcx_pack(header, kind, data, game_dir=""):
    level = header[0x30]
    if kind == "DFLT":
        body = zlib.compress(data, 9)
    elif kind == "ZSTD":
        import zstandard
        body = zstandard.ZstdCompressor(level=level or 15).compress(data)
    elif kind == "KRAK":
        body = Oodle(game_dir).compress(data, level)
    else:
        raise ValueError("DCX compression %s isn't supported" % kind)
    out = bytearray(header) + body
    struct.pack_into(">II", out, 0x1C, len(data), len(body))
    out += b"\0" * (-len(out) % 16)
    return bytes(out)


# ---------- the binder (BND4) and params inside regulation.bin ----------

def _bits(b):
    return int("{:08b}".format(b)[::-1], 2)


def bnd4_files(data):
    """[{name, offset, size}] of the files in a little-endian BND4. Raises if the layout isn't one it knows."""
    if data[:4] != b"BND4" or data[9]:
        raise ValueError("not a little-endian BND4 binder")
    bit_big = not data[10]
    count, = struct.unpack_from("<i", data, 0x0C)
    fhs, = struct.unpack_from("<q", data, 0x20)
    unicode, raw = data[0x30], data[0x31]
    reverse = bit_big or (raw & 1 and not raw & 0x80)
    fmt = raw if reverse else _bits(raw)
    ids, names, long_off, comp = fmt & 2, fmt & 0x0C, fmt & 0x10, fmt & 0x20
    want = 0x10 + (8 if long_off else 4) + (8 if comp else 0) + (4 if ids else 0) + (4 if names else 0) + (8 if fmt == 4 else 0)
    if fhs != want or not names:
        raise ValueError("binder file headers are 0x%X bytes, expected 0x%X" % (fhs, want))
    out = []
    for i in range(count):
        p = 0x40 + i * fhs
        flags = data[p] if bit_big else _bits(data[p])
        size, = struct.unpack_from("<q", data, p + 8)
        q = p + 16
        if comp:
            q += 8
        off, = struct.unpack_from("<q" if long_off else "<I", data, q)
        q += 8 if long_off else 4
        if ids:
            q += 4
        noff, = struct.unpack_from("<I", data, q)
        if unicode:
            end = noff
            while data[end:end + 2] != b"\0\0":
                end += 2
            name = data[noff:end].decode("utf-16-le", "replace")
        else:
            name = data[noff:data.index(b"\0", noff)].decode("shift_jis", "replace")
        if off + size > len(data):
            raise ValueError("binder file %s runs past the end" % name)
        out.append({"name": name, "offset": off, "size": size, "compressed": bool(flags & 1)})
    return out


def param_rows(p):
    """(param type, {row id: data offset}, row size) of a little-endian PARAM file."""
    if p[0x2C] != 0:
        raise ValueError("param isn't little-endian")
    f2d = p[0x2D]
    long_off = f2d & 0x04
    count, = struct.unpack_from("<H", p, 0x0A)
    if f2d & 0x80:
        toff, = struct.unpack_from("<q", p, 0x10)
        ptype = p[toff:p.index(b"\0", toff)].decode("ascii", "replace") if 0 < toff < len(p) else ""
    else:
        ptype = p[0x0C:0x2C].split(b"\0")[0].decode("ascii", "replace")
    start = 0x40 if (long_off or (f2d & 0x01 and f2d & 0x02)) else 0x30
    first = struct.unpack_from("<q", p, start + 8)[0] if long_off else struct.unpack_from("<I", p, start + 4)[0]
    head = (24 if long_off else 12) if first - start >= count * (24 if long_off else 12) else (16 if long_off else 8)
    rows = {}
    offsets = []
    for i in range(count):
        q = start + i * head
        rid, = struct.unpack_from("<i", p, q)
        off = struct.unpack_from("<q", p, q + 8)[0] if long_off else struct.unpack_from("<I", p, q + 4)[0]
        rows.setdefault(rid, off)
        offsets.append(off)
    size = offsets[1] - offsets[0] if len(offsets) > 1 else 0
    for a, b in zip(offsets, offsets[1:]):
        if b - a != size:
            raise ValueError("%s rows aren't evenly spaced" % ptype)
    if offsets and offsets[-1] + size > len(p):
        raise ValueError("%s rows run past the end" % ptype)
    return ptype, rows, size


def _get(buf, off, field):
    o, f = FIELDS[field]
    return struct.unpack_from(f, buf, off + o)[0]


def _set(buf, off, field, value):
    o, f = FIELDS[field]
    struct.pack_into(f, buf, off + o, value)


def apply_kits(bnd, houses=HOUSES, log=lambda *_: None):
    """Turn the houses' starting classes into their kits, in place in the decompressed regulation binder."""
    files = [f for f in bnd4_files(bnd) if f["name"].replace("/", "\\").split("\\")[-1].lower() == "charainitparam.param"]
    if not files:
        raise ValueError("CharaInitParam isn't in this regulation.bin")
    f = files[0]
    if f["compressed"]:
        raise ValueError("CharaInitParam is compressed inside the binder")
    base = f["offset"]
    ptype, rows, size = param_rows(bytes(bnd[base:base + f["size"]]))
    if ptype and ptype != "CHARACTER_INIT_PARAM":
        raise ValueError("the CharaInitParam file is a %s" % ptype)
    if size != ROW_SIZE:
        raise ValueError("CharaInitParam rows are 0x%X bytes, this layout is for 0x%X" % (size, ROW_SIZE))
    spell_ids = {s: i for s, i, _e, _w in SPELLS}
    done = []
    for h in houses:
        if h["row"] not in rows:
            log("✖ class row %d (%s) isn't there" % (h["row"], h["class"]))
            continue
        off = base + rows[h["row"]]
        hat, robe, gloves, legs = h["robes"]
        put = {"equip_Wep_Right": WAND[0], "equip_Subwep_Right": h["side"][0] if h["side"] else -1,
               "equip_Wep_Left": BUCKLER, "equip_Subwep_Left": -1, "equip_Helm": hat, "equip_Armer": robe,
               "equip_Gaunt": gloves, "equip_Leg": legs, "equip_Arrow": -1, "equip_Bolt": -1, "equip_SubArrow": -1,
               "equip_SubBolt": -1, "arrowNum": 0, "boltNum": 0, "subArrowNum": 0, "subBoltNum": 0}
        attuned = [spell_ids["Stupefy"], spell_ids[h["spell"]]]
        for i in range(7):
            put["equip_Spell_%02d" % (i + 1)] = attuned[i] if i < len(attuned) else -1
        for k, v in zip(STATS, h["stats"]):
            put[k] = v
        put["soulLv"] = max(1, sum(h["stats"]) - 79)  # Elden Ring's level is the stats' sum minus 79
        for k, v in put.items():
            _set(bnd, off, k, v)
        # the rest of the spellbook (and memory stones for more spell slots) goes into empty item slots only,
        # so whatever the class already starts with (flasks, keys) stays
        extra = [(i, 1) for _s, i, _e, _w in SPELLS if i not in attuned] + [(MEMORY_STONE, 2)]
        have = {_get(bnd, off, "item_%02d" % (n + 1)) for n in range(10)}
        extra = [(i, q) for i, q in extra if i not in have]
        for n in range(10):
            if not extra:
                break
            if _get(bnd, off, "item_%02d" % (n + 1)) <= 0:
                item, qty = extra.pop(0)
                _set(bnd, off, "item_%02d" % (n + 1), item)
                _set(bnd, off, "itemNum_%02d" % (n + 1), qty)
        if extra:
            log("  %s: %d spell(s) didn't fit the class's item slots (the item spawner has them)" % (h["house"], len(extra)))
        done.append(h["house"])
        log("✔ %s: %s now starts with the wand, %s, %s and %s attuned (level %d)" % (
            h["house"], h["class"], h["robe_names"], "Stupefy", h["spell"], put["soulLv"]))
    return done


# ---------- the merge copy ----------

def game_dir(target):
    return os.path.join(target, "Game") if os.path.isfile(os.path.join(target, "Game", "eldenring.exe")) else target


def _find(target, name):
    for pattern in (name, "*/" + name, "*/*/" + name):
        hits = glob.glob(os.path.join(target, pattern))
        if hits:
            return hits[0]
    return None


def launcher(target):
    return _find(target, "modengine2_launcher.exe")


def source_regulation(target):
    """Where the numbers to build on come from: a mod's regulation.bin in Mod Engine's own mod folder when there is
    one (so an overhaul mod keeps its changes), else the game copy's."""
    for p in (os.path.join(target, "mod", "regulation.bin"), os.path.join(game_dir(target), "regulation.bin")):
        if os.path.isfile(p):
            return p
    return None


def write_config(target):
    """Mod Engine 2's settings for this merge: Frankenstein's folder first, then any mods added to the merge."""
    mods = ['    { enabled = true, name = "frankenstein", path = "%s" },' % MOD_DIR]
    if os.path.isdir(os.path.join(target, "mod")):
        mods.append('    { enabled = true, name = "mods", path = "mod" },')
    dlls = sorted(glob.glob(os.path.join(target, "dlls", "*.dll")))
    text = "\n".join([
        "# written by Frankenstein on every Play; your own mods go in the mod folder next to this file",
        "[modengine]", "debug = false",
        "external_dlls = [%s]" % ", ".join(json.dumps(os.path.relpath(d, target).replace(os.sep, "/")) for d in dlls),
        "", "[extension.mod_loader]", "enabled = true", "loose_params = false", "mods = [", *mods, "]",
        "", "[extension.scylla_hide]", "enabled = false", ""])
    p = os.path.join(target, CONFIG)
    with open(p, "w", encoding="utf-8") as f:
        f.write(text)
    return p


def write_tool_config(target):
    """The Practice Tool, laid out as Frankenstein's Creative tools, with one controller wheel (press both sticks)."""
    exe = _find(target, TOOL_EXE)
    if not exe:
        return None
    text = """# written by Frankenstein on every Play: Elden Ring's Creative tools
commands = [
  { item_spawner = "ctrl+u" },
  { character_stats = true },
  { warp = true },
  { flag = "no_damage", hotkey = "1" },
  { flag = "no_stamina_consume", hotkey = "2" },
  { flags = ["no_fp_consume", "no_ashes_of_war_fp_consume"], hotkey = "3", label = "Unlimited spells (FP)" },
  { flags = ["no_goods_consume", "no_arrows_consume"], hotkey = "4", label = "Unlimited items" },
  { flag = "no_dead", hotkey = "6" },
  { flag = "one_shot", hotkey = "7" },
  { cycle_speed = [1.0, 2.0, 4.0, 8.0], hotkey = "8" },
  { runes = 100000, hotkey = "9" },
  { flag = "gravity", hotkey = "f2" },
  { flag = "torrent_gravity", hotkey = "f3" },
  { flags = ["collision", "torrent_collision"], label = "No Collision" },
  { flag = "no_update_ai", hotkey = "f1" },
  { flags = ["show_all_map_layers", "show_all_graces"], hotkey = "f4", label = "Show all graces/landmarks" },
  { group = "Position storage", commands = [
    { nudge = 1.0, nudge_up = "[", nudge_down = "]" },
    { position = "h", save = "rshift+h" },
  ]},
  { savefile_manager = "ctrl+o" },
  { quitout = "p" },
]

radial-menu = [
  { key = "ctrl+u", label = "Item spawner" },
  { key = "1", label = "No damage" },
  { key = "3", label = "Unlimited spells" },
  { key = "f2", label = "Fly" },
  { key = "f3", label = "Broom (Torrent flies)" },
  { key = "8", label = "Speed" },
  { key = "9", label = "+100,000 runes" },
  { key = "p", label = "Quit to menu" },
]

[settings]
log_level = "INFO"
display = "0"
hide = "rshift+0"
dxgi_debug = false
show_console = false
radial_menu_open = "l3+r3"
indicators = [
  { indicator = "game_version", enabled = false },
  { indicator = "igt", enabled = false },
]
"""
    p = os.path.join(os.path.dirname(exe), TOOL_CONFIG)
    with open(p, "w", encoding="utf-8") as f:
        f.write(text)
    return p


def launch_cmd(target):
    """[launcher, args...] that starts the merge copy (never the original) offline with Frankenstein's mod."""
    exe = os.path.join(game_dir(target), "eldenring.exe")
    me2 = launcher(target)
    if not me2:
        return None
    return [me2, "-t", "er", "-p", exe, "-c", os.path.join(target, CONFIG)]


def write_appid(target):
    """steam_appid.txt next to the copy's eldenring.exe, so the copy starts without Easy Anti-Cheat."""
    p = os.path.join(game_dir(target), "steam_appid.txt")
    if not os.path.isfile(p):
        with open(p, "w") as f:
            f.write(APPID)


def build(target, houses=True, log=None):
    """Everything Frankenstein's Elden Ring mod needs before a Play. Returns a summary for the merge card; never raises."""
    lines = []

    def say(text):
        lines.append(text)
        if log:
            log(text)

    out = {"ok": False, "houses": [], "error": "", "tools": False, "launcher": bool(launcher(target))}
    mod = os.path.join(target, MOD_DIR)
    os.makedirs(mod, exist_ok=True)
    say("Frankenstein's Elden Ring mod, %s" % time.strftime("%Y-%m-%d %H:%M:%S"))
    try:
        write_config(target)
        write_appid(target)
        say("✔ Mod Engine 2 settings written (%s)" % CONFIG)
    except OSError as e:
        say("✖ Mod Engine 2 settings: %s" % e)
    if not out["launcher"]:
        say("✖ Mod Engine 2 isn't in this merge yet (it's added on Play when it can be downloaded)")
    try:
        out["tools"] = bool(write_tool_config(target))
        say("✔ Creative tools set up" if out["tools"] else "  Creative tools (Practice Tool) aren't in this merge yet")
    except OSError as e:
        say("✖ Creative tools: %s" % e)
    dest = os.path.join(mod, "regulation.bin")
    if houses:
        src = source_regulation(target)
        try:
            if not src:
                raise ValueError("regulation.bin wasn't found in the merge copy")
            say("Reading %s" % os.path.relpath(src, target))
            with open(src, "rb") as f:
                plain = decrypt_regulation(f.read())
            header, kind, bnd = dcx_unpack(plain, game_dir(target))
            say("  decrypted: %s, %d bytes" % (kind, len(bnd)))
            bnd = bytearray(bnd)
            out["houses"] = apply_kits(bnd, log=say)
            if not out["houses"]:
                raise ValueError("no class could be changed")
            data = encrypt_regulation(dcx_pack(header, kind, bytes(bnd), game_dir(target)))
            check = dcx_unpack(decrypt_regulation(data), game_dir(target))[2]
            if check != bytes(bnd):
                raise ValueError("the rebuilt regulation.bin didn't read back the same")
            tmp = dest + ".tmp"
            with open(tmp, "wb") as f:
                f.write(data)
            os.replace(tmp, dest)
            out["ok"] = True
            say("✔ House kits written to %s/regulation.bin" % MOD_DIR)
        except Exception as e:  # anything at all: the game then starts with its own numbers
            out["error"] = str(e)[:300]
            say("✖ House kits not built: %s. The game starts with its own classes." % e)
            if os.path.isfile(dest):
                os.remove(dest)
    elif os.path.isfile(dest):
        os.remove(dest)
    try:
        with open(os.path.join(target, *DEBUG.split("/")), "w", encoding="utf-8") as f:
            f.write("\n".join(lines) + "\n")
    except OSError:
        pass
    return out


def start_tools_when_ready(target, is_running, wait=240, settle=20, popen=subprocess.Popen):
    """Creative: start the Practice Tool once the game is up (it attaches to the running game)."""
    exe = _find(target, TOOL_EXE)
    if not exe:
        return False

    def run():
        end = time.time() + wait
        while time.time() < end:
            if is_running(["eldenring.exe"]):
                time.sleep(settle)
                try:
                    popen([exe], cwd=os.path.dirname(exe))
                except OSError:
                    pass
                return
            time.sleep(3)
    threading.Thread(target=run, daemon=True).start()
    return True
