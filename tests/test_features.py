"""The feature checklist: everything Frankenstein has shipped, checked on every release so nothing from an
earlier update goes missing. Add a line here whenever a release adds a feature."""
import json
import os
import tempfile
import re
import unittest


def read(*parts):
    with open(os.path.join(*parts), encoding="utf-8") as f:
        return f.read()

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PKG = os.path.join(ROOT, "gamemerger")

# feature: the app functions it needs (Python side)
API = {
    "accounts (admin, PINs, stay signed in, linked PCs)": ["profiles_state", "profile_switch", "profile_link_code", "profile_link"],
    "library and owned games": ["scan", "library", "library_owned", "steam_connect", "epic_connect", "gog_connect", "add_game_folder"],
    "fun colours on every game": ["fun_scores", "game_ratings"],
    "merge preview and names": ["merge_preview", "merge_identity", "fusion_art"],
    "mod sources (Nexus, CurseForge, Modrinth, Thunderstore, links)": ["mod_search", "mod_get", "mod_link", "nexus_link", "curseforge_set_key"],
    "making merges": ["merge_options", "start_merge", "check_mods"],
    "auto-merge and What it adds": ["merge_autofill", "merge_adds"],
    "Spawner": ["spawner_catalog", "spawner_send"],
    "creative and sandbox tools": ["creative_info", "creative_auto", "creative_add"],
    "play, saves and rundown": ["play", "merge_rundown", "rundown_pref"],
    "story mode and questlines": ["questlines", "questline_new", "questline_tick"],
    "crash repair and save repair": ["fix_crash"],
    "mods and profiles": ["add_mods", "merge_mods", "set_mods", "delete_profile", "undo", "delete"],
    "play together": ["party", "party_setup", "party_host", "party_join", "party_fix", "party_share", "party_fetch"],
    "sharing merges": ["share_merge", "import_merge", "import_info"],
    "gallery": ["gallery", "gallery_thumb", "shots_taken"],
    "achievements": ["achievements", "achievements_new"],
    "controller on every game": ["controller_info", "pad_settings", "pad_save"],
    "Discord": ["discord_set"],
    "save space: merges packed away after playing, rebuilt on Play (3.30)": ["pack_merge", "unpack_merge", "_auto_pack", "_pack_info"],
    "startup errors are logged (3.28.1)": ["report_js_error", "_stale_web"],
    "updates and self-check": ["check_update", "check_update_now", "install_update", "self_check_result", "repair_install"],
    "merge upgrades": ["merge_upgrade"],
    "settings": ["get_settings", "save_settings"],
    "trailer clips for preview slides": ["cover_clip", "merge_cover_clip"],
    "Merge plan before Merge it": ["merge_plan", "merge_plan_of"],
    "Carry over my save (character and abilities)": ["carry_read", "carry_save"],
    "your devices on one account, joining over Wi-Fi (3.33)": ["devices_state", "device_join", "device_join_result", "device_join_answer", "_start_devices"],
    "pairing a PC with a code, firewall fix (3.35)": ["device_pair_start", "device_pair_join", "device_pair_result", "device_firewall"],
}

# feature: something the page must still have (a function or element id in the web files)
WEB = {
    "who's playing and account linking": ["whoIsPlaying", "linkGet"],
    "your devices card and join requests (3.33)": ["renderDevices", "watchJoinRequests", "Use ${esc(a.name)}'s account on this PC"],
    "pairing code box (3.35)": ["showPairCode", "pairWithCode", "Let Frankenstein through the firewall"],
    "library halo, Make host and this-merge panel": ["mergePairHtml", "make-host", "picked"],
    "merge step picks mods by itself": ["What it brings over"],
    "What it adds with built-ins and stand-ins": ["addsHtml", "story stand-in"],
    "Spawner panel": ["openSpawner"],
    "check for updates in the sidebar": ["navUpdate", "checkUpdateNow"],
    "self-check after updates": ["watchSelfCheck", "selfCheck"],
    "originals untouched tag": ["Originals untouched"],
    "creative dialog": ["creativeSetup"],
    "hero item first": ["★ Hero"],
    "preview slideshow with both games fused": ["pvShow", "pv-fused"],
    "no blank covers, 3D moving covers": ["genCover", "cover-float", "cover-sweep", "prefers-reduced-motion"],
    "moving pictures in the preview slides": ["clipVideo", "face-clip", "watchClips"],
    "moving covers: the original cover art, living (3.28)": ["cover-live", "live-fx", "clipStart"],
    "new logo and startup: covers over each palm, clap, dotted burst (3.28)": ["logo/base.png", "logo/hand_l.png", "splashProgress", "splash-burst", "front: true"],
    "startup never blocks: Skip, 30 s timeout, fresh scripts, random covers (3.28.1)": ["splash-skip", "skipSplash", "ASSET_V", "APP_BUILD", "atMost", "splashGames"],
    "save space: Pack away / Unpack, packed tag with GB saved, setting (3.30)": ["ensureUnpacked", "packAfterPlay", "Pack away to save space", "📦 Packed"],
    "full-screen purple explosion at the end of startup (3.29)": ["far * 1.15", "length: 2400"],
    "roomy merge card: big Play, the rest under More": ["play-big", "data-more", "more-menu", "closeMore"],
    "what's inside the game itself": ["In the game:"],
    "animated preview slides with named guns, items, characters": ["pv-items", "pv-bg"],
    "each preview slide is one merged picture (mod screenshot or blended frame)": ["pv-real", "pv-mixin", "pv-grade", "Mod screenshot"],
    "Play as, with how close it gets": ["How close"],
    "Merge plan, honest per engine": ["planHtml", "loadPlan", "Merge plan", "Not possible yet", "Built by Frankenstein"],
    "merged loading screen while the Host starts": ["loadingScreen", "load-screen"],
    "Host game and Extension game naming": ["Host game", "Extension game"],
    "Carry over my save dialog": ["carryDialog", "Carry over my save", "Bring them over"],
}

# Frankenstein's own mods for each game, bundled with the app
BUNDLED = ["mods/schedule1/Frankenstein.dll", "mods/cyberpunk2077/frankenstein_items/init.lua", "mods/gta5/Frankenstein.3.cs",
           "mods/gta5/FrankensteinStory.3.cs", "mods/gta5/FrankensteinMenu.3.cs"]
LUA = ["Garage", "Tuning", "Races", "drawHud", "bridge", "Cheats", "Fly", "Cars", "storyTick", "Story",
       "This merge", "drawMerge", "switchTo", "playAs", "drawWorld", "buildZoneHere", "startEvent", "Speed trap",
       "Drift zone", "Circuit", "worldTick", "Yes, switch to Creative now", "Press ~ > This merge to switch to Creative", "drawCarry", "carryTick"]


class FeatureChecklist(unittest.TestCase):
    def test_app_functions(self):
        from gamemerger.api import Api
        self.assertTrue(Api.NOT_GAMES.match("Steamworks Common Redistributables"))  # tools never show as games
        gone = ["%s: %s" % (f, n) for f, names in API.items() for n in names if not callable(getattr(Api, n, None))]
        self.assertEqual(gone, [], "features missing from the app")

    def test_page(self):
        web = "".join(read(PKG, "web", f) for f in ("index.html", "app.js", "app.css"))
        self.assertNotIn("pv-half", read(PKG, "web", "app.js"), "preview slides are never two pictures side by side")
        gone = ["%s: %s" % (f, n) for f, names in WEB.items() for n in names if n not in web]
        self.assertEqual(gone, [], "features missing from the page")

    def test_bundled_mods(self):
        from gamemerger.api import Api, BUILTIN
        for f in BUNDLED + Api.REQUIRED:
            self.assertTrue(os.path.isfile(os.path.join(PKG, *f.split("/"))), f)
        self.assertEqual(set(BUILTIN), {"schedule1", "cyberpunk2077", "gta5"})
        lua = read(PKG, *BUNDLED[1].split("/"))
        self.assertEqual([x for x in LUA if x not in lua], [])

    def test_menus_inside_the_games(self):
        from gamemerger.api import BUILTIN
        for g, b in BUILTIN.items():
            self.assertTrue(b.get("ingame"), g)
        with open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb") as f:
            dll = f.read()
        self.assertIn("Game Mode: ".encode("utf-16-le"), dll, "Schedule I main menu: Game Mode entry")
        self.assertTrue(os.path.isfile(os.path.join(ROOT, "mods", "schedule1", "MainMenu.cs")))
        gta = read(PKG, "mods", "gta5", "FrankensteinMenu.3.cs")
        for x in ("Keys.F10", "Keys.F11", "switch.json", "ChangeModel", "BuildZone", "SET_WEATHER_TYPE_NOW_PERSIST", "Press Shift+F10 again to confirm", "carry-done.txt", "Keys.F6"):
            self.assertIn(x, gta)
        for x in ("Hold the block", "block.json", "Build the block here", "Game Mode: ", "Yes, switch", "carry-done.txt"):
            self.assertIn(x.encode("utf-16-le"), dll, "Schedule I World: " + x)
        self.assertTrue(os.path.isfile(os.path.join(ROOT, "mods", "schedule1", "World.cs")))

    def test_merge_plan_and_world_on_every_merge(self):
        from gamemerger import mergeplan
        from gamemerger.recipes import load_recipes
        for r in load_recipes():
            mergeplan.add_world(r)
            for t in r.themes:
                self.assertEqual(sum(mergeplan.is_world(x.name) for x in t.slots), 1)
                p = mergeplan.build(r, t)
                self.assertEqual([x["name"] for x in p["sections"]], list(mergeplan.SECTIONS))
                for sec in p["sections"]:
                    for row in sec["rows"]:
                        self.assertIn(row["level"], mergeplan.LEVELS)
                        self.assertTrue(row["how"])
        cp = {r.id: r for r in load_recipes()}
        for rid, tid in (("cyberpunk2077", "forza"), ("schedule1", "hoodwarfare")):
            p = mergeplan.build(cp[rid], cp[rid].theme(tid))
            self.assertTrue(p["showcase"])
            terrain = [row for sec in p["sections"] for row in sec["rows"] if row["part"] == "Terrain and foliage"][0]
            self.assertEqual(terrain["level"], "can't", "honest: terrain can't be reshaped there")

    def test_host_and_extension_names(self):
        js = read(PKG, "web", "app.js")
        self.assertNotIn("Merging in", js, "the second game is the Extension game")
        self.assertNotRegex(js, r"(?i)\bmain game\b")

    def test_play_as_on_every_merge(self):
        from gamemerger import playas
        from gamemerger.recipes import load_recipes
        for r in load_recipes():
            playas.add_to(r)
            for t in r.themes:
                self.assertEqual(sum(playas.is_play_as(s.name) for s in t.slots), 1, "%s %s" % (r.id, t.id))
                self.assertEqual(sum(bool(s.hero) for s in t.slots), 1)

    def test_mod_sources_stay_in_the_build(self):
        wf = read(ROOT, ".github", "workflows", "release.yml")
        self.assertIn("gamemerger\\mods", wf)
        self.assertIn("gamemerger\\web", wf)

    def test_recipes_load(self):
        from gamemerger.recipes import load_recipes
        ids = {r.id for r in load_recipes()}
        self.assertTrue({"schedule1", "cyberpunk2077", "gta5"} <= ids)
        for f in os.listdir(os.path.join(PKG, "recipes")):
            json.loads(read(PKG, "recipes", f))

    def test_hood_warfare_kit_and_shop(self):
        from gamemerger.api import KITS, ARSENALS
        self.assertTrue(KITS["hoodwarfare"]["items"] and KITS["hoodwarfare"]["shop"])
        self.assertTrue(ARSENALS["hoodwarfare"])

    def test_in_game_stories_and_heroes(self):
        from gamemerger import storymode
        from gamemerger.recipes import load_recipes
        for g in ("schedule1", "gta5", "cyberpunk2077"):
            self.assertEqual(storymode.DEPTH[g]["level"], "in-game")
        for r in load_recipes():
            for t in r.themes:
                self.assertTrue(t.slots[0].hero, "%s %s: the hero comes first" % (r.id, t.id))

    def test_version_moves_forward(self):
        from gamemerger.version import VERSION
        log = read(ROOT, "CHANGELOG.md")
        self.assertIn("## v%s" % VERSION, log, "CHANGELOG has no entry for this version")
        self.assertIn(VERSION, read(ROOT, "installer", "release-notes.md"))


    def test_schedule1_mod_reaches_the_game(self):
        # 3.27: the mod is added on every Play, saves never clash, and the card says when it didn't load
        api = read(PKG, "api.py")
        self.assertIn("_mod_check", api)
        self.assertIn("builtin_error", api)
        self.assertIn("write_safely", read(PKG, "installer.py"))
        self.assertNotIn('"manifest.json.tmp"', read(PKG, "installer.py"))
        js = read(PKG, "web", "app.js")
        self.assertIn("mod_check", js)

    def test_schedule1_menu_loading_screen_and_replica_guns(self):
        src = os.path.join(ROOT, "mods", "schedule1")
        for f in ("Rt.cs", "Loading.cs", "Guns.cs"):
            self.assertTrue(os.path.isfile(os.path.join(src, f)), f)
        menu = read(ROOT, "mods", "schedule1", "MainMenu.cs")
        self.assertIn("Inject", menu)
        self.assertIn("new game", menu)
        self.assertNotIn("ButtonClick", menu, "the copied row never keeps a button that could start a new game")
        self.assertIn("GetMouseButtonDown", menu)  # Frankenstein sees the click itself, on both builds
        self.assertIn("Screen.width - w - 24", menu)  # the fallback panel sits in the corner, off the title
        self.assertNotIn("FindObjectsOfType<", read(ROOT, "mods", "schedule1", "World.cs"))  # both builds of the game
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("HOOD TIP", "Equippable_RangedWeapon", "Creative"):
            self.assertIn(word.encode("utf-16-le"), dll, word)

    def test_schedule1_gangs(self):
        # 3.27: gangs with their own districts, initiation and ranks, your own gang, turf wars, robbing and trading
        from gamemerger.api import GANGS
        gangs = GANGS["hoodwarfare"]
        names = " ".join(g["name"] for g in gangs)
        for want in ("Crips", "Bloods", "Mafia", "Yakuza", "London"):
            self.assertIn(want, names)
        self.assertEqual(len({g["region"] for g in gangs}), len(gangs))
        ids = {g["id"] for g in gangs}
        for g in gangs:
            self.assertIn(g["rival"], ids)
            self.assertEqual(len(g["ranks"]), 6)
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("GANGS", "Initiation", "Turf war", "Recruit", "Sell a brick", "Drugs and guns"):
            self.assertIn(word.encode("utf-16-le"), dll, word)

    def test_328_icons_unlimited_creative_covers(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("Storage", "hotbarSlots", "Equippable_RangedWeapon"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        self.assertIn("class Paint", read(ROOT, "mods", "schedule1", "Rt.cs"))  # icons drawn the way IL2CPP keeps
        self.assertIn("SET_PED_INFINITE_AMMO_CLIP", read(PKG, "mods", "gta5", "FrankensteinMenu.3.cs"))
        self.assertIn("creativeTick", read(PKG, "mods", "cyberpunk2077", "frankenstein_items", "init.lua"))
        self.assertIn("def _scene", read(PKG, "covers.py"))  # no blank covers: painted art for games without any
        for f in ("base.png", "hand_l.png", "hand_r.png", "icon.png"):
            self.assertTrue(os.path.isfile(os.path.join(PKG, "web", "logo", f)), f)

    def test_329_guns_held_right_every_gun_and_cars(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("Weapons", "Vehicles", "Grenade Launcher", "Frag Grenade", "VehiclePrefabs", "Frankenstein part", "TakeDamage"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        guns = read(ROOT, "mods", "schedule1", "Guns.cs")
        self.assertNotIn("SetParent", guns, "no blocks stuck on in world space (the floating part)")
        self.assertNotIn("go.transform.localScale =", guns, "the held gun is never stretched (the bent grip)")
        from gamemerger.api import REPLICAS
        for g in REPLICAS["hoodwarfare"]:
            self.assertNotIn("length", g)
            self.assertNotIn("parts", g)
            self.assertTrue(0.85 <= g.get("size", 1) <= 1.15)
        for gun in ("ak47", "smg", "sniper", "minigun", "rpg", "pumpshotgun", "m1911", "revolver"):
            self.assertTrue(any(g["match"] == gun for g in REPLICAS["hoodwarfare"]), gun)

    def test_331_living_gangs_and_menu_creative(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("frankenstein_", "Avatar/Layers/Top/T-Shirt", "patrolling ", "THE STREETS", "_gameBuilding", "couldn't find New Game in 30 seconds"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        people = read(ROOT, "mods", "schedule1", "CrewPeople.cs")
        for gang in ("crips", "bloods", "london", "yakuza", "mafia", "eastlondon"):
            for role in ("boss", "lieutenant", "enforcer"):
                self.assertIn("class Crew_%s_%s" % (gang, role), people)
        self.assertIn("SetAndAttackTarget", read(ROOT, "mods", "schedule1", "Crew.cs"))  # real fights, not the cartel's

    def test_3311_mod_always_current_and_shows_its_version(self):
        from gamemerger.version import VERSION
        plugin = read(ROOT, "mods", "schedule1", "Plugin.cs")
        self.assertIn('public const string Version = "%s";' % VERSION, plugin, "bump Plugin.Version with every release")
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        self.assertIn(VERSION.encode("utf-16-le"), dll, "rebuild the Schedule I dll for this release")
        api = read(PKG, "api.py")
        self.assertIn("_builtin_differs", api)   # files compared, so a rebuild from the old kept copy is fixed on Play
        self.assertIn("_builtin_zip(src, b[\"dest\"], kept)", api)  # and the kept copy is refreshed
        self.assertIn("mod_check.version", read(PKG, "web", "app.js"))

    def test_3312_room_to_rebuild_and_creative_only_in_game(self):
        api = read(PKG, "api.py")
        for x in ("def unpack_room", "AUTO_PACK_MIN_FREE", "move_copy_to"):
            self.assertIn(x, api)
        js = read(PKG, "web", "app.js")
        self.assertIn("roomToUnpack", js)
        self.assertNotIn("Creative (own saves)</button>", js, "Creative lives in the game's own menu, not the app's More menu")

    def test_332_game_mode_controller_guns_fly_cars(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("gamemode.txt", "save-backups", "LoadedGameFolderPath",         # Creative is a mode on each save
                     "Point the stick, let go of View",              # the controller wheel
                     "AK-47 (Frankenstein)", "Uzi (Frankenstein)", "RPG (Frankenstein)", "Guns source: ",  # own guns
                     "Flying. Jump goes up", "Speed: ",                               # fly and speed in Creative
                     "Get in the ", "EnterVehicle", "menu-debug.txt"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        for f in ("MainMenu.cs", "World.cs", "Gangs.cs", "Carry.cs"):
            self.assertNotIn("GUI.Button(", read(ROOT, "mods", "schedule1", f), f + ": every button reachable with the controller (Pad.Button)")
        self.assertNotIn("Application.Quit", read(ROOT, "mods", "schedule1", "MainMenu.cs"), "no restart to change mode")
        self.assertIn("IsPlayerOwned = true", read(ROOT, "mods", "schedule1", "Vehicles.cs"))
        api = read(PKG, "api.py")
        self.assertIn("fold_creative", api)
        self.assertNotIn('merge_id + "~creative" if creative', api, "one set of saves")
        self.assertIn("_members", read(ROOT, "mods", "schedule1", "Rt.cs"))  # reflection cached: less lag

    def test_338_reference_screens_own_guns_aim(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("ref-menu.jpg", "ref-profiles.jpg", "ref-loading.jpg", "ref-avatar.jpg", "Hood Reputation", "Drug Empire",
                     "Empty Profile", "frankenstein_", "AddToRegistry", "ownitems-debug.txt"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        art = os.path.join(PKG, "mods", "_art", "hoodwarfare")
        for f in ("ref-menu.jpg", "ref-profiles.jpg", "ref-loading.jpg", "ref-avatar.jpg", "ref-icon-rep.png", "ref-icon-cash.png",
                  "ref-icon-empire.png", "ref-icon-house.png", "ref-icon-money.png", "ref-icon-crown.png"):
            self.assertTrue(os.path.isfile(os.path.join(art, f)), f)
        self.assertFalse(os.path.isdir(os.path.join(PKG, "mods", "schedule1", "art")))  # pictures don't go into the game's Mods folder
        import tempfile
        from gamemerger.api import Api
        with tempfile.TemporaryDirectory() as d:
            self.assertEqual(Api._ref_art(d, "hoodwarfare"), 10)   # a Hood Warfare merge gets the reference screens
            self.assertEqual(Api._ref_art(d, "hoodwarfare"), 0)    # once
            self.assertEqual(Api._ref_art(d, "other"), 0)          # other merges keep the painted menu
        own = read(ROOT, "mods", "schedule1", "OwnItems.cs")
        for key in ("ak47", "uzi", "sniper", "rpg", "launcher"):
            self.assertIn('Key = "%s"' % key, own)                 # every own weapon is an item of its own
        self.assertIn("OwnItems.IdOf(d.Key)", read(ROOT, "mods", "schedule1", "OwnGuns.cs"))      # F6 gives that item
        self.assertIn('OwnItems.IdOf("launcher")', read(ROOT, "mods", "schedule1", "Boom.cs"))
        self.assertIn("SkinnedMeshRenderer", read(ROOT, "mods", "schedule1", "Looks.cs"))          # the held model is swapped
        guns = read(ROOT, "mods", "schedule1", "OwnGuns.cs")
        self.assertIn("Spread = 0.15", guns)                    # the sniper is tight
        self.assertIn("Rate = 5.5", guns)                       # the Uzi fires fastest
        aim = read(ROOT, "mods", "schedule1", "Aim.cs")
        for kind in ('"sniper"', '"ak47"', '"uzi"', '"shotgun"', '"launcher"', "fieldOfView", "Arc("):
            self.assertIn(kind, aim)

    def test_3371_drop_weapons_and_empty_creative(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("drop-debug.txt", "ClearStoredInstance", "started empty"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        drop = read(ROOT, "mods", "schedule1", "Drop.cs")
        self.assertIn("KeyCode.G", drop)                      # keyboard too
        self.assertIn("Boom.Id(slot) != id", drop)            # checks the item really left
        self.assertIn("MainMenu.Creative) return;", read(ROOT, "mods", "schedule1", "Story.cs"))  # no starter kit in Creative
        self.assertIn("Creative.StartEmpty = true", read(ROOT, "mods", "schedule1", "Mode.cs"))

    def test_337_cars_fully_remodelled(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("UnityEngine.WheelCollider", "new body (", "wheels kept"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        body = read(ROOT, "mods", "schedule1", "Body.cs")
        for shape in ("super", "wedge", "gt", "gwagon"):
            self.assertIn('["%s"] = new Shape' % shape, body)   # every car has its own body
        self.assertIn('Rt.SetArray(mesh, "vertices"', body)      # a real mesh, built on either build of the game
        garage = read(ROOT, "mods", "schedule1", "Garage.cs")
        self.assertIn("r.enabled = false", garage)               # the base car's body is hidden
        self.assertIn("IsWheel(", garage)                        # its wheels aren't
        self.assertIn("CalcSize", read(ROOT, "mods", "schedule1", "Vehicles.cs"))  # the prompt fits the name

    def test_336_menu_like_reference_profiles_skip_tutorial(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("menu-backdrop.jpg", "title.png", "PROFILES", "Multiplayer", "Build your empire. Run your block.",
                     "tutorial-debug.txt", "SkipTutorial", "StartGame"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        skin = read(ROOT, "mods", "schedule1", "MenuSkin.cs")
        self.assertIn("Paint.Fill(new Rect(0, 0, W, H)", skin)   # one scene over the game's own menu, nothing shows through
        self.assertIn("Pad.Pressed & Pad.B", skin)                # controller on the profiles screen
        self.assertIn("Skip.Scene(sceneName)", read(ROOT, "mods", "schedule1", "Plugin.cs"))
        self.assertIn("Cursor.lockState != CursorLockMode.Locked", read(ROOT, "mods", "schedule1", "Creative.cs"))  # storage moves, doesn't copy
        drop = read(ROOT, "mods", "schedule1", "Drop.cs")
        self.assertIn("Pad.RB", drop)                              # pick a slot with the inventory open
        self.assertIn("Creative.Forget()", drop)                   # a drop isn't put back
        self.assertIn("LB / RB pick a slot".encode("utf-16-le"), dll)
        from gamemerger import menuart
        from PIL import Image
        d = tempfile.mkdtemp()
        menuart.backdrop(os.path.join(d, "b.jpg"), size=(480, 270))
        menuart.title(os.path.join(d, "t.png"), "Schedule I", "Hood Warfare", height=120)
        self.assertEqual(Image.open(os.path.join(d, "b.jpg")).size, (480, 270))
        t = Image.open(os.path.join(d, "t.png"))
        self.assertEqual(t.mode, "RGBA")
        self.assertGreater(t.width, t.height * 2)
        self.assertIn("_menu_art", read(PKG, "api.py"))

    def test_335_own_weapon_looks_cars_and_car_entry(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("Frankenstein model", "Frankenstein kit:", "Velocci 720", "Toro Furioso", "Notte GT", "Gelände G63",
                     "vehicles-debug.txt", "Get in the ", "Car: in or out", "StartInteract"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        self.assertNotIn("More Guns isn't loaded".encode("utf-16-le"), dll)
        looks = read(ROOT, "mods", "schedule1", "Looks.cs")
        for key in ("ak47", "uzi", "sniper", "rpg", "launcher"):
            self.assertIn('["%s"] = new[]' % key, looks)   # every added weapon has its own model
        self.assertIn("r.enabled = false", looks)            # the base gun is hidden
        self.assertIn("Looks.Icon(", read(ROOT, "mods", "schedule1", "ItemMenu.cs"))
        self.assertIn("Looks.Dress(", read(ROOT, "mods", "schedule1", "Boom.cs"))
        self.assertIn("Pad.X", read(ROOT, "mods", "schedule1", "Vehicles.cs"))
        self.assertIn("Garage.Update()", read(ROOT, "mods", "schedule1", "Plugin.cs"))

    def test_334_new_main_menu_and_drop(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("Load your world", "Start a new world", "Unlimited resources", "Adjust your game", "Exit to desktop",
                     "Frankenstein's menu is up", "sendNavigationEvents", "D-pad down: drop"):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        skin = read(ROOT, "mods", "schedule1", "MenuSkin.cs")
        self.assertIn("localScale = Vector3.zero", skin)   # the game's list is hidden, its buttons still pressed
        self.assertIn('"Invoke"', skin)
        self.assertIn("Pad.Pressed & Pad.A", skin)          # controller
        self.assertIn("MenuSkin.Update()", read(ROOT, "mods", "schedule1", "MainMenu.cs"))

    def test_3321_heavier_swing(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        self.assertIn("Heavy hit!".encode("utf-16-le"), dll)
        melee = read(ROOT, "mods", "schedule1", "Melee.cs")
        for x in ("timeScale", "RightTrigger", "Warp", "_shake"):
            self.assertIn(x, melee)

    def test_3313_new_logo_on_shortcuts(self):
        from PIL import Image
        ico = Image.open(os.path.join(ROOT, "installer", "Frankenstein.ico"))
        self.assertIn((256, 256), ico.info["sizes"])
        ico.size = (256, 256)
        ico.load()
        logo = Image.open(os.path.join(PKG, "web", "logo", "icon.png")).convert("RGBA").resize((256, 256))
        diff = sum(abs(a - b) for p, q in zip(ico.convert("RGBA").getdata(), logo.getdata()) for a, b in zip(p, q))
        self.assertLess(diff / (256 * 256 * 4), 8, "the .ico is the hooded-figure logo")
        nsi = read(ROOT, "installer", "Frankenstein.nsi")
        self.assertIn("Frankenstein-${VER}.ico", nsi)  # a new path per version, so Windows' icon cache can't keep the old one
        self.assertIn("SHChangeNotify", nsi)

    def test_3311_new_districts(self):
        dll = open(os.path.join(PKG, "mods", "schedule1", "Frankenstein.dll"), "rb").read()
        for word in ("Frankenstein ground ", "TRAVEL", "Entering "):
            self.assertIn(word.encode("utf-16-le"), dll, word)
        from gamemerger.api import GANGS
        for g in GANGS["hoodwarfare"]:
            self.assertTrue(g.get("district"), "every gang gets a new district")

if __name__ == "__main__":
    unittest.main()
