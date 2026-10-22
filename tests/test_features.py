"""The feature checklist: everything Frankenstein has shipped, checked on every release so nothing from an
earlier update goes missing. Add a line here whenever a release adds a feature."""
import json
import os
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
    "updates and self-check": ["check_update", "check_update_now", "install_update", "self_check_result", "repair_install"],
    "merge upgrades": ["merge_upgrade"],
    "settings": ["get_settings", "save_settings"],
    "trailer clips for preview slides": ["cover_clip", "merge_cover_clip"],
    "Merge plan before Merge it": ["merge_plan", "merge_plan_of"],
    "Carry over my save (character and abilities)": ["carry_read", "carry_save"],
}

# feature: something the page must still have (a function or element id in the web files)
WEB = {
    "who's playing and account linking": ["whoIsPlaying", "linkGet"],
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
        self.assertIn("switch.json".encode("utf-16-le"), dll, "Schedule I main menu panel")
        self.assertTrue(os.path.isfile(os.path.join(ROOT, "mods", "schedule1", "MainMenu.cs")))
        gta = read(PKG, "mods", "gta5", "FrankensteinMenu.3.cs")
        for x in ("Keys.F10", "Keys.F11", "switch.json", "ChangeModel", "BuildZone", "SET_WEATHER_TYPE_NOW_PERSIST", "Press Shift+F10 again to confirm", "carry-done.txt", "Keys.F6"):
            self.assertIn(x, gta)
        for x in ("Hold the block", "block.json", "Build the block here", "Switch to Creative (every item, its own saves)", "Yes, switch now", "carry-done.txt"):
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

if __name__ == "__main__":
    unittest.main()
