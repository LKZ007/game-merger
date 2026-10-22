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
    "moving covers (trailer loops in the box)": ["cover_clip", "merge_cover_clip"],
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
    "moving pictures inside the covers": ["clipVideo", "face-clip", "watchClips", "cover-pan"],
    "roomy merge card: big Play, the rest under More": ["play-big", "data-more", "more-menu", "closeMore"],
    "what's inside the game itself": ["In the game:"],
    "animated preview slides with named guns, items, characters": ["pv-items", "pvInset", "pv-bg"],
    "Play as, with how close it gets": ["How close"],
}

# Frankenstein's own mods for each game, bundled with the app
BUNDLED = ["mods/schedule1/Frankenstein.dll", "mods/cyberpunk2077/frankenstein_items/init.lua", "mods/gta5/Frankenstein.3.cs",
           "mods/gta5/FrankensteinStory.3.cs", "mods/gta5/FrankensteinMenu.3.cs"]
LUA = ["Garage", "Tuning", "Races", "drawHud", "bridge", "Cheats", "Fly", "Cars", "storyTick", "Story",
       "This merge", "drawMerge", "switchTo", "playAs"]


class FeatureChecklist(unittest.TestCase):
    def test_app_functions(self):
        from gamemerger.api import Api
        self.assertTrue(Api.NOT_GAMES.match("Steamworks Common Redistributables"))  # tools never show as games
        gone = ["%s: %s" % (f, n) for f, names in API.items() for n in names if not callable(getattr(Api, n, None))]
        self.assertEqual(gone, [], "features missing from the app")

    def test_page(self):
        web = "".join(read(PKG, "web", f) for f in ("index.html", "app.js", "app.css"))
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
        for x in ("Keys.F10", "Keys.F11", "switch.json", "ChangeModel"):
            self.assertIn(x, gta)

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


if __name__ == "__main__":
    unittest.main()
