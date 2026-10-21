import json
import os
import tempfile
import unittest
import zipfile

TMP = tempfile.mkdtemp()
os.environ["GAMEMERGER_HOME"] = os.path.join(TMP, "home")

from gamemerger import detect, installer  # noqa: E402
from gamemerger.recipes import load_recipes  # noqa: E402

RECIPES = {r.id: r for r in load_recipes()}


def write(path, text="x"):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w") as f:
        f.write(text)


def make_zip(path, files):
    with zipfile.ZipFile(path, "w") as z:
        for name, text in files.items():
            z.writestr(name, text)
    return path


def fake_cyberpunk(root):
    write(os.path.join(root, "bin", "x64", "Cyberpunk2077.exe"), "exe")
    write(os.path.join(root, "archive", "pc", "content", "basegame.archive"), "base")
    write(os.path.join(root, "r6", "config", "settings.json"), "original")
    return root


class LayoutTests(unittest.TestCase):
    def setUp(self):
        self.d = tempfile.mkdtemp(dir=TMP)

    def plan(self, files, recipe="cyberpunk2077", **kw):
        src = os.path.join(self.d, "src")
        for name, text in files.items():
            write(os.path.join(src, name), text)
        plan, unknown = installer.plan_files(src, RECIPES[recipe].layout, **kw)
        return sorted(rel.replace(os.sep, "/") for _s, rel in plan), unknown

    def test_root_structured_mod_inside_wrapper_folder(self):
        rels, _ = self.plan({"CoolCar-1.2/archive/pc/mod/car.archive": "a",
                             "CoolCar-1.2/r6/tweaks/car.yaml": "b", "CoolCar-1.2/readme.txt": "c"})
        self.assertEqual(rels, ["archive/pc/mod/car.archive", "r6/tweaks/car.yaml"])

    def test_loose_archive_file(self):
        rels, _ = self.plan({"car.archive": "a", "preview.png": "p"})
        self.assertEqual(rels, ["archive/pc/mod/car.archive"])

    def test_cet_lua_mod_keeps_its_folder(self):
        rels, _ = self.plan({"SpeedHud/init.lua": "a", "SpeedHud/ui.lua": "b"})
        self.assertEqual(rels, ["bin/x64/plugins/cyber_engine_tweaks/mods/SpeedHud/init.lua",
                                "bin/x64/plugins/cyber_engine_tweaks/mods/SpeedHud/ui.lua"])

    def test_schedule1_dll_goes_to_mods(self):
        rels, _ = self.plan({"MoreGuns.dll": "a"}, recipe="schedule1")
        self.assertEqual(rels, ["Mods/MoreGuns.dll"])

    def test_melonloader_zip(self):
        rels, _ = self.plan({"MelonLoader/net6/MelonLoader.dll": "a", "version.dll": "b"}, recipe="schedule1")
        self.assertEqual(rels, ["MelonLoader/net6/MelonLoader.dll", "version.dll"])

    def test_scripthookv_bin_subdir(self):
        rels, _ = self.plan({"bin/ScriptHookV.dll": "a", "bin/dinput8.dll": "b", "readme.txt": "r"},
                            recipe="gta5", flat_root=True, subdir="bin")
        self.assertEqual(rels, ["ScriptHookV.dll", "dinput8.dll"])

    def test_unknown_files_reported(self):
        rels, unknown = self.plan({"weird.bin": "a"})
        self.assertEqual(rels, [])
        self.assertEqual(unknown, ["weird.bin"])

    def test_zip_slip_refused(self):
        bad = make_zip(os.path.join(self.d, "bad.zip"), {"../../evil.dll": "x"})
        with self.assertRaises(installer.MergeError):
            installer.extract(bad, os.path.join(self.d, "out"))


class MergeTests(unittest.TestCase):
    def setUp(self):
        self.d = tempfile.mkdtemp(dir=TMP)
        self.base = fake_cyberpunk(os.path.join(self.d, "Cyberpunk 2077"))
        self.recipe = RECIPES["cyberpunk2077"]
        self.theme = self.recipe.theme("forza")
        self.mod = make_zip(os.path.join(self.d, "mod.zip"), {
            "archive/pc/mod/car.archive": "car", "r6/config/settings.json": "modded"})

    def read(self, *p):
        with open(os.path.join(*p)) as f:
            return f.read()

    def check_copy_mode(self, mode):
        m = installer.Merge.create(self.recipe, self.theme, self.base, os.path.join(self.d, "out"),
                                   mode, "Cyberpunk - Forza", appid="1091500")
        t = m.target_path
        self.assertTrue(os.path.isfile(os.path.join(t, "bin", "x64", "Cyberpunk2077.exe")))
        self.assertEqual(self.read(t, "bin", "x64", "steam_appid.txt"), "1091500")
        m.install(self.mod, "mod", self.recipe.layout)
        self.assertEqual(self.read(t, "r6", "config", "settings.json"), "modded")
        # the original install is never changed, even when files are hard-linked
        self.assertEqual(self.read(self.base, "r6", "config", "settings.json"), "original")
        self.assertFalse(os.path.exists(os.path.join(self.base, "archive", "pc", "mod")))
        m = installer.Merge.load(m.id)
        m.undo()
        self.assertEqual(self.read(t, "r6", "config", "settings.json"), "original")
        self.assertFalse(os.path.exists(os.path.join(t, "archive", "pc", "mod")))
        self.assertFalse(os.path.exists(os.path.join(t, "bin", "x64", "steam_appid.txt")))
        m.delete()
        self.assertFalse(os.path.exists(t))
        self.assertTrue(os.path.isfile(os.path.join(self.base, "bin", "x64", "Cyberpunk2077.exe")))

    def test_hardlink_copy(self):
        self.check_copy_mode(installer.MODE_HARDLINK)

    def test_full_copy(self):
        self.check_copy_mode(installer.MODE_COPY)

    def test_in_place_with_backup(self):
        m = installer.Merge.create(self.recipe, self.theme, self.base, "", installer.MODE_INPLACE, "inplace")
        m.install(self.mod, "mod", self.recipe.layout)
        self.assertEqual(self.read(self.base, "r6", "config", "settings.json"), "modded")
        m.undo()
        self.assertEqual(self.read(self.base, "r6", "config", "settings.json"), "original")
        self.assertFalse(os.path.exists(os.path.join(self.base, "archive", "pc", "mod")))
        m.delete()  # in-place delete must never remove the original game
        self.assertTrue(os.path.isfile(os.path.join(self.base, "bin", "x64", "Cyberpunk2077.exe")))

    def test_two_mods_same_file_then_undo(self):
        m = installer.Merge.create(self.recipe, self.theme, self.base, "", installer.MODE_INPLACE, "twice")
        m.install(self.mod, "mod1", self.recipe.layout)
        other = make_zip(os.path.join(self.d, "mod2.zip"), {"r6/config/settings.json": "second"})
        m.install(other, "mod2", self.recipe.layout)
        self.assertEqual(self.read(self.base, "r6", "config", "settings.json"), "second")
        m.undo()
        self.assertEqual(self.read(self.base, "r6", "config", "settings.json"), "original")

    def test_refuses_to_delete_non_copy_folder(self):
        m = installer.Merge.create(self.recipe, self.theme, self.base, os.path.join(self.d, "out"),
                                   installer.MODE_COPY, "x")
        os.remove(os.path.join(m.target_path, installer.COPY_MARKER))
        with self.assertRaises(installer.MergeError):
            m.delete()
        self.assertTrue(os.path.isdir(m.target_path))


class DetectTests(unittest.TestCase):
    def test_steam_libraries_and_recipe_match(self):
        d = tempfile.mkdtemp(dir=TMP)
        steam, lib2 = os.path.join(d, "Steam"), os.path.join(d, "Games")
        write(os.path.join(steam, "steamapps", "libraryfolders.vdf"),
              '"libraryfolders"\n{\n "0" { "path" "%s" }\n "1" { "path" "%s" }\n}' % (steam, lib2))
        write(os.path.join(lib2, "steamapps", "appmanifest_1091500.acf"),
              '"AppState"\n{\n "appid" "1091500"\n "name" "Cyberpunk 2077"\n "installdir" "Cyberpunk 2077"\n}')
        fake_cyberpunk(os.path.join(lib2, "steamapps", "common", "Cyberpunk 2077"))
        write(os.path.join(steam, "steamapps", "appmanifest_1551360.acf"),
              '"AppState"\n{\n "appid" "1551360"\n "name" "Forza Horizon 5"\n "installdir" "ForzaHorizon5"\n}')
        os.makedirs(os.path.join(steam, "steamapps", "common", "ForzaHorizon5"))
        games = detect.match_recipes(detect.find_steam(steam), list(RECIPES.values()))
        by_name = {g.name: g for g in games}
        self.assertEqual(by_name["Cyberpunk 2077"].recipe.id, "cyberpunk2077")
        self.assertEqual(by_name["Cyberpunk 2077"].appid, "1091500")
        self.assertIsNone(by_name["Forza Horizon 5"].recipe)

    def test_epic_and_xbox(self):
        d = tempfile.mkdtemp(dir=TMP)
        game = fake_cyberpunk(os.path.join(d, "Epic", "Cyberpunk2077"))
        write(os.path.join(d, "manifests", "abc.item"),
              json.dumps({"DisplayName": "Cyberpunk 2077", "InstallLocation": game}))
        os.makedirs(os.path.join(d, "XboxGames", "Forza Horizon 5", "Content"))
        epic = detect.find_epic(os.path.join(d, "manifests"))
        xbox = detect.find_xbox([d])
        self.assertEqual([g.name for g in epic], ["Cyberpunk 2077"])
        self.assertEqual([g.name for g in xbox], ["Forza Horizon 5"])
        self.assertTrue(xbox[0].path.endswith("Content"))


class AssistantAndCheckTests(unittest.TestCase):
    def test_clashes_between_mods(self):
        clashes = installer.find_clashes({"a.zip": ["r6/x.yaml", "r6/y.yaml"], "b.zip": ["r6/x.yaml", "r6/y.yaml"],
                                          "c.zip": ["archive/pc/mod/c.archive"]})
        self.assertEqual(len(clashes), 1)
        self.assertIn("a.zip and b.zip", clashes[0])
        self.assertIn("+1 more", clashes[0])

    def test_preview_does_not_install(self):
        d = tempfile.mkdtemp(dir=TMP)
        z = make_zip(os.path.join(d, "car.zip"), {"Car/archive/pc/mod/car.archive": "a"})
        rels, unknown = installer.preview(z, RECIPES["cyberpunk2077"].layout)
        self.assertEqual(rels, ["archive/pc/mod/car.archive"])

    def test_logs_and_context_for_franklin(self):
        from gamemerger import franklin
        d = tempfile.mkdtemp(dir=TMP)
        write(os.path.join(d, "MelonLoader", "Latest.log"), "\n".join("line %d" % i for i in range(400)))
        logs = franklin.read_logs(d, RECIPES["schedule1"].logs)
        self.assertEqual(logs[0][0].replace(os.sep, "/"), "MelonLoader/Latest.log")
        self.assertTrue(logs[0][1].endswith("line 399"))
        self.assertEqual(len(logs[0][1].splitlines()), franklin.LOG_TAIL_LINES)
        ctx = franklin.build_context({"base_game": "Schedule I", "theme": "Hood Warfare style",
                                      "mods": [{"file": "guns.zip", "slot": "Weapons", "paths": ["Mods/Guns.dll"]}],
                                      "logs": logs})
        self.assertIn("Base game: Schedule I", ctx)
        self.assertIn("guns.zip", ctx)
        self.assertIn("line 399", ctx)

    def test_franklin_needs_a_key(self):
        from gamemerger import franklin
        with self.assertRaises(franklin.FranklinError):
            franklin.Franklin("")


class SavesTests(unittest.TestCase):
    def test_merge_gets_its_own_saves_and_originals_come_back(self):
        from types import SimpleNamespace
        from gamemerger import saves
        d = tempfile.mkdtemp(dir=TMP)
        game_saves = os.path.join(d, "Profiles")
        write(os.path.join(game_saves, "slot1.sav"), "original")
        recipe = SimpleNamespace(saves=[game_saves])
        saves.create_slot("m1", recipe)  # starts as a copy of the current saves
        saves.swap_in("m1", recipe)
        self.assertTrue(os.path.isdir(game_saves + saves.ORIGINAL_SUFFIX))
        write(os.path.join(game_saves, "slot1.sav"), "merged progress")
        self.assertEqual(saves.active()["merge_id"], "m1")
        self.assertTrue(saves.swap_out())
        with open(os.path.join(game_saves, "slot1.sav")) as f:
            self.assertEqual(f.read(), "original")
        with open(os.path.join(saves.slot_root("m1"), "0", "slot1.sav")) as f:
            self.assertEqual(f.read(), "merged progress")
        self.assertIsNone(saves.active())
        self.assertFalse(os.path.exists(game_saves + saves.ORIGINAL_SUFFIX))

    def test_watch_calls_back_when_game_closes(self):
        from gamemerger import saves
        states = iter([False, True, True, False])
        closed = []
        t = saves.watch_until_closed(["Game.exe"], lambda: closed.append(1), grace=5, poll=0.01,
                                     is_running=lambda _n: next(states, False))
        t.join(2)
        self.assertEqual(closed, [1])


class CloudSaveTests(unittest.TestCase):
    def test_push_then_pull_newer_on_other_pc(self):
        import time
        from gamemerger import saves
        cloud = tempfile.mkdtemp(dir=TMP)
        write(os.path.join(saves.slot_root("pc1"), "0", "a.sav"), "day 1")
        self.assertTrue(saves.push_cloud("pc1", cloud, "Night City"))
        write(os.path.join(saves.slot_root("laptop"), "0", "a.sav"), "old")
        old = time.time() - 3600
        os.utime(os.path.join(saves.slot_root("laptop"), "0", "a.sav"), (old, old))
        self.assertTrue(saves.pull_cloud("laptop", cloud, "Night City"))
        with open(os.path.join(saves.slot_root("laptop"), "0", "a.sav")) as f:
            self.assertEqual(f.read(), "day 1")
        self.assertFalse(saves.pull_cloud("laptop", cloud, "Night City"))  # already up to date


class PermissionAndCoverTests(unittest.TestCase):
    def test_library_root_covers_whole_steam_library(self):
        from gamemerger import permissions
        p = os.path.join("C:", "Steam", "steamapps", "common", "Cyberpunk 2077")
        self.assertEqual(permissions.library_root(p), os.path.join("C:", "Steam", "steamapps", "common"))
        self.assertEqual(permissions.library_root(os.path.join("D:", "Games", "GTAV")), os.path.join("D:", "Games", "GTAV"))

    def test_writable_folders_need_nothing(self):
        from gamemerger import permissions
        self.assertEqual(permissions.folders_needing_access([tempfile.mkdtemp(dir=TMP)]), [])

    def test_made_and_merged_covers(self):
        from gamemerger import covers
        base = covers.made_cover("Schedule I", tempfile.mkdtemp(dir=TMP))
        self.assertTrue(base and os.path.isfile(base))
        merged = covers.merged_cover(base, None, "Hood Warfare 2", "test")
        self.assertTrue(os.path.isfile(merged))
        self.assertTrue(covers.as_data_uri(merged).startswith("data:image/png;base64,"))
        self.assertEqual(covers.appid_for(type("G", (), {"name": "Cyberpunk 2077®", "appid": ""})()), "1091500")

    def test_scraped_download_link(self):
        from unittest import mock
        from gamemerger import downloader
        page = b'<a href="/files/ScriptHookV_1.0.3407.0.zip">Download</a>'
        resp = mock.MagicMock()
        resp.__enter__.return_value.read.return_value = page
        with mock.patch("urllib.request.urlopen", return_value=resp):
            tag, name, url = downloader.scraped_asset("http://www.dev-c.com/gtav/scripthookv/",
                                                      RECIPES["gta5"].frameworks["scripthookv"].scrape["pattern"])
        self.assertEqual(name, "ScriptHookV_1.0.3407.0.zip")
        self.assertEqual(url, "http://www.dev-c.com/files/ScriptHookV_1.0.3407.0.zip")


class ProfileTests(unittest.TestCase):
    def test_switch_mods_off_and_on(self):
        d = tempfile.mkdtemp(dir=TMP)
        base = fake_cyberpunk(os.path.join(d, "Cyberpunk 2077"))
        r = RECIPES["cyberpunk2077"]
        a = make_zip(os.path.join(d, "a.zip"), {"r6/config/settings.json": "chaos"})
        b = make_zip(os.path.join(d, "b.zip"), {"archive/pc/mod/car.archive": "car"})
        m = installer.Merge.create(r, r.theme("forza"), base, os.path.join(d, "out"), installer.MODE_COPY, "P")
        m.install(a, "Chaos", r.layout)
        m.install(b, "Car", r.layout)
        m.rebuild(r.layout, ["Car"])
        t = m.target_path
        with open(os.path.join(t, "r6", "config", "settings.json")) as f:
            self.assertEqual(f.read(), "original")
        self.assertTrue(os.path.isfile(os.path.join(t, "archive", "pc", "mod", "car.archive")))
        self.assertEqual([(x["label"], x["on"]) for x in m.all_mods()], [("Chaos", False), ("Car", True)])
        m.rebuild(r.layout, ["Car", "Chaos"])
        with open(os.path.join(t, "r6", "config", "settings.json")) as f:
            self.assertEqual(f.read(), "chaos")
        self.assertEqual(m.data["parked"], [])
        self.assertEqual([x["label"] for x in m.all_mods()], ["Chaos", "Car"])  # original order kept


class CrashRepairTests(unittest.TestCase):
    def test_diagnose_picks_a_listed_mod_only(self):
        from gamemerger import franklin
        bot = franklin.Franklin.__new__(franklin.Franklin)
        bot.stream_reply = lambda h: iter(['Here: {"culprit": "Car pack", "why": "Fuck, the car pack broke the tweaks."}'])
        self.assertEqual(bot.diagnose("logs", ["Car pack", "HUD"])["culprit"], "Car pack")
        bot.stream_reply = lambda h: iter(['{"culprit": "Something else", "why": "?"}'])
        self.assertIsNone(bot.diagnose("logs", ["Car pack"])["culprit"])


class SharingTests(MergeTests):
    def test_share_and_rebuild_on_a_friends_pc(self):
        from gamemerger import sharing
        m = installer.Merge.create(self.recipe, self.theme, self.base, os.path.join(self.d, "out"),
                                   installer.MODE_COPY, "Night City Racing")
        m.install(self.mod, "Car pack", self.recipe.layout)
        os.remove(self.mod)  # the download is gone, the merge kept its own copy
        out = sharing.export(m, self.recipe, os.path.join(self.d, "share"))
        self.assertTrue(out.endswith("Night City Racing.gmerge"))
        info = sharing.read(out)
        self.assertEqual((info["recipe_id"], info["theme_id"]), ("cyberpunk2077", "forza"))
        self.assertEqual(sharing.version_note(info, self.base, self.recipe.exe), "")
        info, folder, items = sharing.unpack(out)
        friend = installer.Merge.create(self.recipe, self.theme, self.base, os.path.join(self.d, "friend"),
                                        installer.MODE_COPY, info["name"])
        for inst, archive in items:
            friend.install(archive, inst["label"], self.recipe.layout)
        self.assertEqual(self.read(friend.target_path, "r6", "config", "settings.json"), "modded")

    def test_not_a_share_file(self):
        from gamemerger import sharing
        bad = make_zip(os.path.join(self.d, "x.gmerge"), {"hello.txt": "x"})
        with self.assertRaises(installer.MergeError):
            sharing.read(bad)


class RenameAndNexusTests(unittest.TestCase):
    def test_updater_finds_new_then_old_installer(self):
        from unittest import mock
        from gamemerger import updater

        def latest(assets):
            body = json.dumps({"tag_name": "v99.0.0", "assets": [
                {"name": n, "browser_download_url": "https://x/" + n, "size": 1} for n in assets]}).encode()
            r = mock.MagicMock()
            r.__enter__.return_value.read.return_value = body
            return r

        with mock.patch("urllib.request.urlopen", return_value=latest(["GameMergerSetup.exe", "FrankensteinSetup.exe"])), \
                mock.patch("json.load", lambda f: json.loads(f.read())):
            self.assertTrue(updater.latest()["url"].endswith("FrankensteinSetup.exe"))
        with mock.patch("urllib.request.urlopen", return_value=latest(["GameMergerSetup.exe"])), \
                mock.patch("json.load", lambda f: json.loads(f.read())):
            self.assertTrue(updater.latest()["url"].endswith("GameMergerSetup.exe"))

    def test_old_cloud_folder_is_renamed(self):
        from gamemerger import saves
        d = tempfile.mkdtemp()
        os.makedirs(os.path.join(d, "Game Merger saves", "Night City"))
        self.assertEqual(saves.cloud_slot(d, "Night City"), os.path.join(d, "Frankenstein saves", "Night City"))
        self.assertTrue(os.path.isdir(os.path.join(d, "Frankenstein saves", "Night City")))

    def test_parse_nxm(self):
        from gamemerger import nexus
        got = nexus.parse_nxm("nxm://Cyberpunk2077/mods/107/files/12345?key=abc&expires=1700000000&user_id=9")
        self.assertEqual(got, {"domain": "cyberpunk2077", "mod": "107", "file": "12345", "key": "abc", "expires": "1700000000"})
        self.assertIsNone(nexus.parse_nxm("https://www.nexusmods.com/"))


if __name__ == "__main__":
    unittest.main()
