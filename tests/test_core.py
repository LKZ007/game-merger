import json
import os
import shutil
import tempfile
import time
import unittest
import types
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

    def test_logs_and_context_for_the_ai(self):
        from gamemerger import ai as franklin
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

    def test_ai_needs_a_key(self):
        from gamemerger import ai
        with self.assertRaises(ai.AIError):
            ai.AI("")


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
        from gamemerger import ai
        bot = ai.AI.__new__(ai.AI)
        bot.ask = lambda text, *a: 'Here: {"culprit": "Car pack", "why": "The car pack broke the tweaks."}'
        self.assertEqual(bot.diagnose("logs", ["Car pack", "HUD"])["culprit"], "Car pack")
        bot.ask = lambda text, *a: '{"culprit": "Something else", "why": "?"}'
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


class OwnedLibraryTests(unittest.TestCase):
    def setUp(self):
        os.environ["GAMEMERGER_HOME"] = tempfile.mkdtemp()

    def test_steam_account_and_list(self):
        from gamemerger import accounts
        vdf = '"users"\n{\n\t"76561197960287930"\n\t{\n\t\t"PersonaName"\t\t"levy"\n\t\t"MostRecent"\t\t"1"\n\t}\n}'
        self.assertEqual(accounts.parse_loginusers(vdf), ("76561197960287930", "levy"))
        xml = b"<gamesList><games><game><appID>1551360</appID><name><![CDATA[Forza Horizon 5]]></name></game></games></gamesList>"
        self.assertEqual(accounts.parse_steam_xml(xml), [{"id": "1551360", "name": "Forza Horizon 5"}])
        with self.assertRaises(accounts.AccountError):
            accounts.parse_steam_xml(b"<html>private</html>")
        self.assertEqual(accounts.steam_id_from("https://steamcommunity.com/profiles/76561197960287930/"), "76561197960287930")

    def test_not_on_this_pc_and_other_devices(self):
        from gamemerger import accounts
        cloud = tempfile.mkdtemp()
        os.makedirs(os.path.join(cloud, "Frankenstein devices"))
        with open(os.path.join(cloud, "Frankenstein devices", "BIG-PC.json"), "w") as f:
            json.dump({"device": "BIG-PC", "games": [{"name": "Forza Horizon 5", "source": "Xbox", "appid": ""}]}, f)
        here = [{"name": "Cyberpunk 2077", "source": "Steam", "appid": "1091500"}]
        owned = {"Steam": [{"id": "1091500", "name": "Cyberpunk 2077"}, {"id": "1551360", "name": "Forza Horizon 5"},
                           {"id": "413150", "name": "Stardew Valley"}]}
        out = accounts.merge_lists(here, owned, accounts.other_devices(cloud))
        self.assertEqual([g["name"] for g in out], ["Forza Horizon 5", "Stardew Valley"])
        self.assertEqual(out[0]["elsewhere"], "BIG-PC")
        self.assertEqual(out[1]["install"], "steam://install/413150")

    def test_gog_galaxy_list(self):
        import sqlite3
        from gamemerger import accounts
        db = os.path.join(tempfile.mkdtemp(), "galaxy-2.0.db")
        con = sqlite3.connect(db)
        con.executescript("""CREATE TABLE LibraryReleases (releaseKey TEXT); CREATE TABLE GamePieceTypes (id INTEGER, type TEXT);
            CREATE TABLE GamePieces (releaseKey TEXT, gamePieceTypeId INTEGER, value TEXT);
            INSERT INTO LibraryReleases VALUES ('gog_1207664643'), ('steam_10');
            INSERT INTO GamePieceTypes VALUES (1, 'originalTitle'), (2, 'originalImages');
            INSERT INTO GamePieces VALUES ('gog_1207664643', 1, '{"title": "The Witcher 3"}'), ('gog_1207664643', 2, '{"verticalCover": "https://x/c.jpg"}'),
                                          ('steam_10', 1, '{"title": "Counter-Strike"}');""")
        con.commit()
        con.close()
        self.assertEqual(accounts.gog_owned(db), [{"id": "1207664643", "name": "The Witcher 3", "image": "https://x/c.jpg",
                                                   "install": "goggalaxy://openGameView/1207664643"}])

    def test_epic_code_and_secrets(self):
        from gamemerger import accounts
        self.assertEqual(accounts.epic_code_from('{"redirectUrl":"x","authorizationCode":"0123456789abcdef0123456789abcdef"}'),
                         "0123456789abcdef0123456789abcdef")
        accounts.secret_set("steam_key", "abc")
        self.assertEqual(accounts.secret_get("steam_key"), "abc")
        accounts.secret_delete("steam_key")
        self.assertIsNone(accounts.secret_get("steam_key"))


class AccountsTests(unittest.TestCase):
    def setUp(self):
        os.environ["GAMEMERGER_HOME"] = tempfile.mkdtemp()

    def test_admin_keeps_settings_and_accounts_are_separate(self):
        from gamemerger import paths, profiles
        paths.save_settings({"claude_api_key": "k", "achievements": {"first_merge": 1}})
        data = profiles.ensure_admin(paths.load_settings(), "levy")
        admin = data["profiles"][0]
        self.assertTrue(admin["admin"])
        mine = profiles.Settings(paths.load_settings(), admin["id"])
        self.assertEqual((mine["claude_api_key"], mine["achievements"]), ("k", {"first_merge": 1}))
        kid = profiles.add("Sam", "1234", {"online": False})
        theirs = profiles.Settings(paths.load_settings(), kid["id"])
        self.assertEqual(theirs.get("claude_api_key"), "k")  # shared
        self.assertIsNone(theirs.get("achievements"))  # personal
        theirs["achievements"] = {"shared": 2}
        theirs["mode"] = "copy"
        paths.save_settings(theirs)
        self.assertEqual(profiles.Settings(paths.load_settings(), admin["id"])["achievements"], {"first_merge": 1})
        self.assertEqual(paths.load_settings()["mode"], "copy")
        self.assertTrue(profiles.check_pin(profiles.find(kid["id"]), "1234"))
        self.assertFalse(profiles.check_pin(profiles.find(kid["id"]), "0000"))
        self.assertFalse(profiles.public(profiles.find(kid["id"]))["perms"]["online"])
        with self.assertRaises(profiles.ProfileError):
            profiles.remove(admin["id"])
        with self.assertRaises(profiles.ProfileError):
            profiles.add("Bob", "12")

    def test_same_admin_on_two_pcs(self):
        from gamemerger import paths, profiles
        cloud = tempfile.mkdtemp()
        laptop = profiles.ensure_admin(paths.load_settings(), "Zokol")["profiles"][0]
        profiles.update(laptop["id"], pin="1234")
        profiles.save_personal(laptop["id"], {"achievements": {"first_merge": 5}, "steam_on": True})
        profiles.publish(cloud)
        code = profiles.link_code(laptop["id"])
        os.environ["GAMEMERGER_HOME"] = tempfile.mkdtemp()  # the desktop
        desk = profiles.ensure_admin(paths.load_settings(), "levy")["profiles"][0]
        profiles.save_personal(desk["id"], {"achievements": {"first_merge": 9, "party": 3}})
        profiles.pull([cloud])
        data = profiles.load()
        self.assertEqual(len(data["profiles"]), 1)  # linked, not a second admin
        me = data["profiles"][0]
        self.assertEqual((me["id"], me["name"], profiles.uid(me)), (desk["id"], "Zokol", laptop["id"]))
        self.assertTrue(profiles.check_pin(me, "1234"))
        mine = profiles.load_personal(me["id"])
        self.assertEqual(mine["achievements"], {"first_merge": 5, "party": 3})
        self.assertNotIn("steam_on", mine)  # per PC
        self.assertEqual(profiles.link(profiles.read_code(code)), me["id"])  # the code finds the same account
        profiles.set_remember(me["id"])
        self.assertEqual(profiles.remembered(), me["id"])
        with self.assertRaises(profiles.ProfileError):
            profiles.read_code("FRK1-abc")



class AnyGameTests(unittest.TestCase):
    def setUp(self):
        os.environ["GAMEMERGER_HOME"] = tempfile.mkdtemp()

    def test_engines_are_told_apart(self):
        from gamemerger import autorecipe
        d = tempfile.mkdtemp(dir=TMP)
        mono = os.path.join(d, "Valheim")
        write(os.path.join(mono, "valheim.exe"), "x" * 100)
        write(os.path.join(mono, "UnityCrashHandler64.exe"), "x" * 500)
        write(os.path.join(mono, "UnityPlayer.dll"))
        write(os.path.join(mono, "valheim_Data", "app.info"), "IronGate\nValheim")
        eng = autorecipe.detect(mono)
        self.assertEqual((eng["engine"], eng["frameworks"][0]["id"]), ("unity-mono", "bepinex"))
        self.assertEqual(autorecipe.main_exe(mono)[0], "valheim.exe")  # the crash handler is bigger but skipped
        self.assertEqual(autorecipe.guess_saves(mono, eng), ["{LocalLow}/IronGate/Valheim"])
        il2 = os.path.join(d, "Il2")
        write(os.path.join(il2, "GameAssembly.dll"))
        write(os.path.join(il2, "UnityPlayer.dll"))
        os.makedirs(os.path.join(il2, "Il2_Data"))
        self.assertEqual(autorecipe.detect(il2)["frameworks"][0]["id"], "melonloader")
        ue = os.path.join(d, "Ue")
        os.makedirs(os.path.join(ue, "Pal", "Content", "Paks"))
        eng = autorecipe.detect(ue)
        self.assertEqual(eng["engine"], "unreal")
        self.assertEqual(eng["layout"]["loose_rules"][0]["dest"], "Pal/Content/Paks/~mods")
        beth = os.path.join(d, "Skyrim")
        write(os.path.join(beth, "Data", "Skyrim.esm"))
        self.assertEqual(autorecipe.detect(beth)["engine"], "bethesda")
        self.assertEqual(autorecipe.detect(tempfile.mkdtemp(dir=TMP))["moddable"], "no")

    def test_bethesda_mods_land_in_data(self):
        from gamemerger import autorecipe
        d = tempfile.mkdtemp(dir=TMP)
        beth = os.path.join(d, "Skyrim")
        write(os.path.join(beth, "Data", "Skyrim.esm"))
        layout = autorecipe.detect(beth)["layout"]
        loose = make_zip(os.path.join(d, "a.zip"), {"Cool Armor/meshes/armor.nif": "m", "Cool Armor/CoolArmor.esp": "p"})
        rels, _ = installer.preview(loose, layout)
        self.assertEqual(sorted(rels), ["Data/CoolArmor.esp", "Data/meshes/armor.nif"])
        wrapped = make_zip(os.path.join(d, "b.zip"), {"Data/textures/t.dds": "t"})
        self.assertEqual(installer.preview(wrapped, layout)[0], ["Data/textures/t.dds"])

    def test_any_game_gets_a_recipe_with_every_other_game_as_a_style(self):
        from gamemerger import autorecipe, detect
        d = tempfile.mkdtemp(dir=TMP)
        ue = os.path.join(d, "Palworld")
        os.makedirs(os.path.join(ue, "Pal", "Content", "Paks"))
        write(os.path.join(ue, "Palworld.exe"))
        g = detect.InstalledGame("Palworld", ue, "Steam", appid="1623730")
        r = autorecipe.build(g, ["Palworld", "Forza Horizon 5", "Schedule I"])
        self.assertEqual([t.from_game for t in r.themes], ["Forza Horizon 5", "Schedule I"])
        self.assertEqual(r.auto["engine"], "Unreal Engine")
        self.assertFalse(r.themes[0].filled)
        autorecipe.set_theme(r.id, r.themes[0].id, "Cars in Palpagos.", [{"name": "Cars", "search": "car"}])
        again = {x.id: x for x in autorecipe.load_all(["Forza Horizon 5"])}[r.id]
        self.assertEqual((again.themes[0].description, again.themes[0].slots[0].search, again.themes[0].filled),
                         ("Cars in Palpagos.", "car", True))
        hand = RECIPES["cyberpunk2077"]
        autorecipe.extend(hand, ["Forza Horizon 5", "Valheim", "Cyberpunk 2077"])
        ids = [t.id for t in hand.themes]
        self.assertIn("style-valheim", ids)
        self.assertNotIn("style-forza-horizon-5", ids)  # already a hand-made style
        self.assertNotIn("style-cyberpunk-2077", ids)

    def test_fun_guess_from_genres(self):
        from gamemerger import storeinfo
        self.assertEqual(storeinfo.guess_fun({"genres": ["Action", "Racing", "Indie"]}, {"genres": ["Racing", "Action"]})["rating"], "green")
        self.assertEqual(storeinfo.guess_fun({"genres": ["Action", "RPG"]}, {"genres": ["Action", "Sports"]})["rating"], "yellow")
        self.assertEqual(storeinfo.guess_fun({"genres": ["Strategy"]}, {"genres": ["Racing"]})["rating"], "red")
        self.assertEqual(storeinfo.guess_fun(None, {"genres": ["Racing"]})["rating"], "yellow")
        raw = {"5": {"success": True, "data": {"name": "X", "genres": [{"description": "Action"}], "categories": [{"id": 18}]}}}
        self.assertEqual(storeinfo.parse("5", raw)["controller"], "partial")



class ModSourceAndControllerTests(unittest.TestCase):
    def setUp(self):
        os.environ["GAMEMERGER_HOME"] = tempfile.mkdtemp()

    def test_modrinth_pulls_in_required_mods_once(self):
        from gamemerger import modsources
        versions = {
            "sodium": {"name": "Sodium 1", "files": [{"primary": True, "url": "u1", "filename": "sodium.jar"}],
                       "dependencies": [{"project_id": "fabric-api", "dependency_type": "required"},
                                        {"project_id": "iris", "dependency_type": "optional"}]},
            "fabric-api": {"name": "Fabric API", "files": [{"primary": True, "url": "u2", "filename": "fabric-api.jar"}],
                           "dependencies": [{"project_id": "sodium", "dependency_type": "required"}]},
        }
        old_v, old_d = modsources.modrinth_version, modsources.download
        modsources.modrinth_version = lambda p, l=None, g=None: versions[p]
        modsources.download = lambda url, folder, name=None, progress=None: "/dl/" + name
        try:
            got = modsources.modrinth_get("sodium", "fabric", "1.21.1")
        finally:
            modsources.modrinth_version, modsources.download = old_v, old_d
        self.assertEqual([p for _n, p in got], ["/dl/sodium.jar", "/dl/fabric-api.jar"])

    def test_link_must_be_a_web_address(self):
        from gamemerger import modsources
        with self.assertRaises(modsources.SourceError):
            modsources.from_link("C:/mods/thing.zip")

    def test_minecraft_profile_points_at_the_merge(self):
        from gamemerger import minecraft, autorecipe
        root = tempfile.mkdtemp(dir=TMP)
        write(os.path.join(root, "launcher_profiles.json"), json.dumps({"profiles": {"x": {"name": "Latest"}}}))
        self.assertEqual(autorecipe.detect(root)["engine"], "minecraft")
        minecraft.add_profile(root, "m1", "Blocky Night City", "D:/Frankenstein/Blocky", "fabric-loader-0.16-1.21.1")
        with open(os.path.join(root, "launcher_profiles.json")) as f:
            prof = json.load(f)["profiles"]
        self.assertEqual(prof["frankenstein-m1"]["gameDir"], "D:/Frankenstein/Blocky")
        self.assertIn("x", prof)
        minecraft.select_profile(root, "m1")
        minecraft.remove_profile(root, "m1")
        with open(os.path.join(root, "launcher_profiles.json")) as f:
            self.assertNotIn("frankenstein-m1", json.load(f)["profiles"])
        d = tempfile.mkdtemp(dir=TMP)
        jar = os.path.join(d, "sodium.jar")
        write(jar, "jar")
        rels, _ = installer.preview(jar, autorecipe.detect(root)["layout"])
        self.assertEqual(rels, ["mods/sodium.jar"])

    def test_controller_map_and_badges(self):
        from gamemerger import controller
        m = controller.merged_map({"a": "e", "bogus": "x"})
        self.assertEqual((m["a"], m["b"]), ("e", "ctrl"))
        self.assertNotIn("bogus", m)
        self.assertEqual(controller.badge("full")[0], "native")
        self.assertEqual(controller.badge("none")[0], "mapped")
        self.assertTrue(all(v in controller.KEYS for k, v in controller.DEFAULT.items()
                            if k not in ("left_stick", "right_stick", "sensitivity")))

    def test_creative_mode_on_every_engine(self):
        from gamemerger import autorecipe
        for eng in ("unity-mono", "unity-il2cpp", "unreal", "bethesda", "source", "godot", "unknown"):
            sb = autorecipe.sandbox_for({"engine": eng})
            self.assertTrue(sb.get("note"), eng)
        recipes = {r.id: r for r in load_recipes()}
        for rid in ("schedule1", "gta5", "cyberpunk2077"):
            self.assertTrue(recipes[rid].sandbox.get("slots"), rid)

    def test_every_game_gets_a_colour_before_a_main_game_is_picked(self):
        import types
        from gamemerger import api, storeinfo
        from gamemerger.recipes import Recipe
        auto = Recipe("auto-x", "Palworld", [], [], "", {}, {}, [])
        auto.auto = {"moddable": "limited", "note": ""}
        fake = types.SimpleNamespace(
            _games=[types.SimpleNamespace(name="Palworld", recipe=auto), types.SimpleNamespace(name="Odd", recipe=None)],
            _hand=[RECIPES["gta5"]], _appid_for_name=lambda n: {"Stardew Valley": "413150"}.get(n),
            _store_info=lambda n, a=None: None)
        with open(storeinfo._file("413150"), "w") as f:
            json.dump({"appid": "413150", "genres": [], "workshop": True}, f)
        r = api.Api.game_ratings(fake, ["Palworld", "Odd", "Grand Theft Auto V", "Stardew Valley", "Never Seen"])
        got = {n: v["rating"] for n, v in r["scores"].items()}
        self.assertEqual(got, {"Palworld": "yellow", "Odd": "red", "Grand Theft Auto V": "green",
                               "Stardew Valley": "green"})
        self.assertTrue(r["pending"])

    def test_thunderstore_search_and_zip_cleanup(self):
        from gamemerger import modsources, paths
        pk = [{"full": "Gunsmith-MoreGuns", "name": "More_Guns", "owner": "Gunsmith", "page": "p", "cats": ["Mods"], "nsfw": False,
               "v": [{"full": "Gunsmith-MoreGuns-1.0.0", "desc": "Adds pistols and rifles", "icon": "", "url": "u", "deps": [], "dl": 9}], "dl": 900},
              {"full": "Chef-Recipes", "name": "Recipes", "owner": "Chef", "page": "p", "cats": [], "nsfw": False,
               "v": [{"full": "Chef-Recipes-1.0.0", "desc": "Food", "icon": "", "url": "u", "deps": [], "dl": 1}], "dl": 5}]
        with open(os.path.join(paths.sub(os.path.join("library", "thunderstore")), "schedule-i.json"), "w") as f:
            json.dump(pk, f)
        hits = modsources.thunderstore_search("schedule-i", "guns")
        self.assertEqual([h["name"] for h in hits], ["More Guns"])
        z = make_zip(os.path.join(tempfile.mkdtemp(dir=TMP), "m.zip"), {"manifest.json": "{}", "icon.png": "x", "Mods/MoreGuns.dll": "d"})
        modsources._strip_ts(z)
        with zipfile.ZipFile(z) as zz:
            self.assertEqual(zz.namelist(), ["Mods/MoreGuns.dll"])

    def test_story_pack_is_cleaned_for_the_game(self):
        from gamemerger import storymode
        raw = {"title": "Hood Point", "kit": ["M1911*1"], "characters": [{"id": "c0", "first": "Dre", "last": "Vance", "role": "Weapons dealer",
               "bio": "Sells iron.", "shop": [{"item": "Revolver", "price": "900", "after_quest": 0}]}],
               "quests": [{"title": "Strapped", "giver": "c0", "steps": [{"type": "text", "text": "Answer Dre"},
                           {"type": "takedown", "text": "Drop 3 cops", "amount": 3, "target": "police"},
                           {"type": "fly", "text": "nonsense"}], "reward": {"cash": 500, "items": [{"item": "M1911"}]}},
                          {"title": "Empty", "steps": []}]}
        pack = storymode.clean(raw, "X")
        self.assertEqual(len(pack["characters"]), 6)
        self.assertEqual([s["type"] for s in pack["quests"][0]["steps"]], ["text", "takedown"])
        self.assertEqual(len(pack["quests"]), 1)
        self.assertEqual(pack["characters"][0]["shop"][0]["price"], 900)
        q = storymode.as_questline(pack)
        self.assertEqual(q["quests"][0]["giver"], "Dre Vance")
        self.assertEqual(storymode.depth(RECIPES["schedule1"])["level"], "in-game")
        self.assertEqual(storymode.depth(None)["level"], "light")



class ScheduleOneFixTests(unittest.TestCase):
    def test_manifest_write_survives_a_locked_or_read_only_file(self):
        import stat
        d = tempfile.mkdtemp()
        p = os.path.join(d, "manifest.json")
        installer.write_safely(p, "{}")
        os.chmod(p, stat.S_IREAD)  # read-only, as a copy or restore can leave it
        installer.write_safely(p, '{"a": 1}')
        with open(p) as f:
            self.assertEqual(json.load(f), {"a": 1})
        self.assertEqual([x for x in os.listdir(d) if x.endswith(".tmp")], [])
        # many threads saving at once never clash on one temporary file
        import threading
        ts = [threading.Thread(target=installer.write_safely, args=(p, json.dumps({"n": i}))) for i in range(12)]
        [t.start() for t in ts]
        [t.join() for t in ts]
        with open(p) as f:
            self.assertIn("n", json.load(f))

    def test_mod_check_reads_melonloader_log(self):
        from gamemerger import api
        d = tempfile.mkdtemp()
        m = types.SimpleNamespace(data={"recipe_id": "schedule1"}, target_path=d)
        self.assertFalse(api.Api._mod_check(m)["loaded"])  # no log: MelonLoader didn't run
        os.makedirs(os.path.join(d, "MelonLoader"))
        log = os.path.join(d, "MelonLoader", "Latest.log")
        with open(log, "w") as f:
            f.write("[12:00:01] Loading Mods...\n[12:00:02] [Frankenstein] Frankenstein loaded for X.\n")
        self.assertTrue(api.Api._mod_check(m)["loaded"])
        with open(log, "w") as f:  # an old mod ran: the card says so
            f.write("[12:00:01] Frankenstein v1.0.0\n[12:00:02] [Frankenstein] Frankenstein loaded for X.\n")
        c = api.Api._mod_check(m)
        self.assertFalse(c["loaded"])
        self.assertIn("older Frankenstein mod", c["why"])
        with open(log, "w") as f:
            f.write("[12:00:01] Frankenstein v%s\n[12:00:02] [Frankenstein] Frankenstein mod %s loaded for X.\n" % (api.VERSION, api.VERSION))
        c = api.Api._mod_check(m)
        self.assertTrue(c["loaded"])
        self.assertEqual(c["version"], api.VERSION)
        with open(log, "w") as f:
            f.write("[12:00:01] Failed to load Melon Assembly from Frankenstein.dll: missing dependency S1API\n")
        c = api.Api._mod_check(m)
        self.assertFalse(c["loaded"])
        self.assertIn("S1API", c["why"])

    def test_menu_text_never_says_no_menu_for_schedule1(self):
        from gamemerger import api
        m = types.SimpleNamespace(data={"recipe_id": "schedule1", "installs": []})
        self.assertIn("next time you press Play", api.Api._ingame(m))

    def test_replica_guns_and_loading_screen(self):
        from gamemerger.api import REPLICAS, LOADING_TIPS, MOREGUNS, Api
        names = {g["name"] for g in REPLICAS["hoodwarfare"]}
        self.assertTrue({"Choppa", "Uzi", "Glock", ".308 Sniper"} <= names)
        for g in REPLICAS["hoodwarfare"]:
            self.assertTrue(g["match"] and 0.3 < g.get("damage", 1) < 3)
        self.assertTrue(LOADING_TIPS["hoodwarfare"])
        d = tempfile.mkdtemp()
        os.makedirs(os.path.join(d, "UserData"))
        cfg = os.path.join(d, "UserData", "MoreGuns.cfg")
        with open(cfg, "w") as f:
            f.write('["MoreGuns-ak47 Settings"]\n"ak47 Damage" = 70.0\n"ak47 Display Name" = "AK47"\n"ak47 Magazine Size" = 30\n')
        Api._moreguns_cfg(types.SimpleNamespace(target_path=d), "hoodwarfare")
        text = open(cfg).read()
        self.assertIn('"ak47 Display Name" = "Choppa"', text)
        self.assertIn('"ak47 Damage" = %.1f' % MOREGUNS["hoodwarfare"]["ak47"]["Damage"], text)


if __name__ == "__main__":
    unittest.main()


class SafetyTests(unittest.TestCase):
    def setUp(self):
        os.environ["GAMEMERGER_HOME"] = tempfile.mkdtemp()

    def test_damaged_save_goes_back_to_last_good_copy(self):
        from gamemerger import saves
        slot = os.path.join(saves.slot_root("m1"), "0", "ManualSave-1")
        os.makedirs(slot)
        with open(os.path.join(slot, "sav.dat"), "wb") as f:
            f.write(b"CSAV" + b"x" * 4000)
        saves.snapshot("m1")
        with open(os.path.join(slot, "sav.dat"), "wb") as f:
            f.write(b"\0" * 10)  # cut off mid-write
        self.assertEqual(len(saves.damaged("m1")), 1)
        done = saves.repair("m1")
        self.assertEqual(done["fixed"], ["ManualSave-1"])
        with open(os.path.join(slot, "sav.dat"), "rb") as f:
            self.assertEqual(f.read(4), b"CSAV")
        self.assertEqual(saves.damaged("m1"), [])

    def test_original_saves_put_back(self):
        from gamemerger import saves
        orig = tempfile.mkdtemp()
        with open(os.path.join(orig, "a.sav"), "w") as f:
            f.write("mine")
        r = types.SimpleNamespace(id="g", saves=[orig])
        saves.swap_in("m2", r)
        with open(os.path.join(orig, "merge.sav"), "w") as f:
            f.write("merge")
        os.rename(orig + saves.ORIGINAL_SUFFIX + "", orig + ".tmp")  # simulate cloud writing into the originals
        with open(os.path.join(orig + ".tmp", "a.sav"), "w") as f:
            f.write("clobbered!")
        os.rename(orig + ".tmp", orig + saves.ORIGINAL_SUFFIX)
        saves.swap_out()
        self.assertEqual(saves.check_originals("m2"), ["a.sav"])
        with open(os.path.join(orig, "a.sav")) as f:
            self.assertEqual(f.read(), "mine")

    def test_space_saver_never_shares_writable_files(self):
        from gamemerger import installer
        base, target = tempfile.mkdtemp(), tempfile.mkdtemp()
        os.makedirs(os.path.join(base, "r6", "cache"))
        with open(os.path.join(base, "r6", "cache", "final.redscripts"), "w") as f:
            f.write("vanilla")
        installer.clone_tree(base, os.path.join(target, "copy"), hardlink=True)
        self.assertEqual(installer.harden(os.path.join(target, "copy")), 1)
        with open(os.path.join(target, "copy", "r6", "cache", "final.redscripts"), "w") as f:
            f.write("modded")
        with open(os.path.join(base, "r6", "cache", "final.redscripts")) as f:
            self.assertEqual(f.read(), "vanilla")


class StandardTests(unittest.TestCase):
    def test_every_part_has_something(self):
        from gamemerger import api
        self.assertEqual(api.standin_for("cyberpunk2077", "Speedometer and HUD")[1], "builtin")
        self.assertEqual(api.standin_for("cyberpunk2077", "Radio stations")[1], "story")
        self.assertEqual(api.standin_for("whatever", "Dragons")[0], api.STORY_STANDIN)
        self.assertEqual(api.standin_for("x", "y", "Own thing"), ("Own thing", "builtin"))


class PlayAsAndInGameTests(unittest.TestCase):
    def test_the_other_games_character(self):
        from gamemerger import playas
        self.assertEqual(playas.style("Gang Beasts")[0], "wobbly")
        self.assertEqual(playas.style("Batman: Arkham Knight")[0], "superhero")
        self.assertEqual(playas.style("Stick Fight: The Game")[0], "stick")
        self.assertEqual(playas.style("ELDEN RING")[0], "knight")
        self.assertEqual(playas.source_of(playas.slot_name("Gang Beasts")), "Gang Beasts")
        self.assertTrue(playas.must_match(playas.slot_name("Gang Beasts")))
        self.assertIn("Impotent Rage", playas.host_text("gta5", "Injustice 2"))
        self.assertEqual(playas.host_text("eldenring", "Gang Beasts"), "")  # no own mod there: honest stand-in

    def test_character_games_make_play_as_the_hero(self):
        from gamemerger import playas, recipes
        r = types.SimpleNamespace(id="gta5", themes=[
            types.SimpleNamespace(from_game="Gang Beasts", name="Gang Beasts style", slots=[recipes.Slot("Ragdoll", "ragdoll", hero=True)]),
            types.SimpleNamespace(from_game="Forza Horizon 5", name="Forza", slots=[recipes.Slot("Cars", "cars", hero=True)])])
        playas.add_to(r)
        playas.add_to(r)  # twice changes nothing
        self.assertTrue(playas.is_play_as(r.themes[0].slots[0].name) and r.themes[0].slots[0].hero)
        self.assertFalse(r.themes[0].slots[1].hero)
        self.assertEqual([s.hero for s in r.themes[1].slots], [True, False])
        self.assertEqual(len(r.themes[1].slots), 2)

    def test_old_merges_get_play_as(self):
        from gamemerger import api, playas, recipes
        a = api.Api.__new__(api.Api)
        t = types.SimpleNamespace(id="t", from_game="Gang Beasts", name="GB", slots=[recipes.Slot("Ragdoll", "ragdoll", hero=True)])
        r = types.SimpleNamespace(id="gta5", themes=[t], nexus_domain="gta5", theme=lambda i: t)
        playas.add_to(r)
        m = types.SimpleNamespace(data={"theme_id": "t", "adds": [{"slot": "Ragdoll", "ok": True, "hero": True}]}, save=lambda: None)
        self.assertTrue(a._add_new_parts(m, r))
        self.assertFalse(a._add_new_parts(m, r))
        first = m.data["adds"][0]
        self.assertTrue(playas.is_play_as(first["slot"]) and first["hero"] and first["standin"] == "builtin")
        self.assertTrue(first["close"] and first["picks"])
        self.assertFalse(m.data["adds"][1]["hero"])

    def test_creative_switch_from_inside_the_game(self):
        import time as _t
        from gamemerger import api
        folder = tempfile.mkdtemp()
        sub = os.path.join(folder, "UserData", "Frankenstein")
        os.makedirs(sub)
        with open(os.path.join(sub, "switch.json"), "w") as f:
            f.write('{"to": "creative"}')
        a = api.Api.__new__(api.Api)
        played = []
        a.play = lambda mid, creative=False: played.append((mid, creative))
        old_load, old_sleep = api.Merge.load, api.time.sleep
        api.Merge.load = staticmethod(lambda mid: types.SimpleNamespace(target_path=folder, data={"recipe_id": "schedule1"}))
        api.time.sleep = lambda s: None
        try:
            a._switch_from_game("m9")
            for _ in range(50):
                if played:
                    break
                _t.sleep(0.02)
        finally:
            api.Merge.load, api.time.sleep = old_load, old_sleep
        self.assertEqual(played, [("m9", True)])
        self.assertFalse(os.path.exists(os.path.join(sub, "switch.json")))


class NetworkSetupTests(unittest.TestCase):
    def test_setup_failures_say_why(self):
        from gamemerger import network
        d = tempfile.mkdtemp()
        log = os.path.join(d, "x.log")
        self.assertIn("Yes/No prompt", network._why(False, log))
        for text, want in (("started\nmsiexec 1618 \n", "Another installer"), ("started\nmsiexec 1603 \n", "zerotier.com"),
                           ("started\nno-token\n", "service didn't start"), ("started\ncopy-failed\n", "copy")):
            with open(log, "w") as f:
                f.write(text)
            self.assertIn(want, network._why(True, log))

    def test_setup_waits_with_ping_not_timeout(self):
        import inspect
        from gamemerger import network
        src = inspect.getsource(network.setup)
        self.assertNotIn("timeout /t", src)  # timeout quits at once in a hidden window, so it never waited
        self.assertIn("ping -n 2", src)


class CarryOverTests(unittest.TestCase):
    def test_schedule1_save_is_read_and_never_changed(self):
        from gamemerger import carryover
        root = tempfile.mkdtemp()
        save = os.path.join(root, "765", "SaveGame_1")
        os.makedirs(os.path.join(save, "Players", "Player_0"))
        with open(os.path.join(save, "Money.json"), "w") as f:
            json.dump({"DataType": "MoneyData", "OnlineBalance": 5200.0, "Networth": 9000}, f)
        with open(os.path.join(save, "Rank.json"), "w") as f:
            json.dump({"Rank": 12, "Tier": 3, "XP": 450}, f)
        with open(os.path.join(save, "Players", "Player_0", "Inventory.json"), "w") as f:
            json.dump({"Items": json.dumps({"CashBalance": 300.0})}, f)  # JSON inside a string, like the game
        before = {p: os.path.getmtime(os.path.join(dp, p)) for dp, _d, fs in os.walk(save) for p in fs}
        card = carryover.read("Schedule I", roots=[os.path.join(root, "*", "SaveGame_*")])
        self.assertEqual(card["money"], 5500)
        self.assertEqual(card["level"], 12)
        self.assertIn("rich", card["abilities"])
        self.assertIn("tough", card["abilities"])
        after = {p: os.path.getmtime(os.path.join(dp, p)) for dp, _d, fs in os.walk(save) for p in fs}
        self.assertEqual(before, after)

    def test_cyberpunk_summary_and_abilities(self):
        from gamemerger import carryover
        root = tempfile.mkdtemp()
        save = os.path.join(root, "ManualSave-3")
        os.makedirs(save)
        with open(os.path.join(save, "metadata.9.json"), "w") as f:
            json.dump({"Data": {"metadata": {"level": 37, "streetCred": 50, "strength": 4, "reflexes": 18,
                                             "technicalAbility": 6, "intelligence": 3, "cool": 12, "lifePath": "StreetKid"}}}, f)
        card = carryover.read("Cyberpunk 2077", roots=[os.path.join(root, "*")])
        self.assertEqual(card["level"], 37)
        self.assertEqual(card["abilities"][:2], ["agile", "stealth"])
        gta = {p["id"]: p for p in carryover.powers_for("gta5", card["abilities"])}
        self.assertEqual(gta["agile"]["level"], "built")
        s1 = {p["id"]: p for p in carryover.powers_for("schedule1", card["abilities"])}
        self.assertEqual(s1["agile"]["level"], "can't")  # honest: Schedule I can't do it yet

    def test_locked_saves_are_filled_by_hand(self):
        from gamemerger import carryover
        card = carryover.read("ELDEN RING")
        self.assertIn("by hand", card["how"])
        st = carryover.settings("gta5", {"saved": 5, "name": "Tarnished", "money": 900, "abilities": ["magic", "rich"]})
        self.assertEqual(st["id"], "5")
        self.assertEqual(st["powers"], ["magic", "money"])


class StartupTests(unittest.TestCase):
    def test_page_and_script_carry_this_version(self):
        from gamemerger.api import Api
        from gamemerger.version import VERSION
        here = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "gamemerger")
        self.assertEqual(Api._stale_web(here), [], "index.html PAGE_BUILD and app.js APP_BUILD must equal VERSION " + VERSION)


class DeviceTests(unittest.TestCase):
    def _lan(self, accounts_, inv):
        from gamemerger import devices
        lan = devices.Lan(lambda: {"accounts": accounts_, "current": accounts_[0]["uid"] if accounts_ else "", "inventory": inv})
        lan.sent = []
        lan._send = lambda msg, ip: lan.sent.append((msg, ip))
        return lan

    def test_inventory_only_goes_to_the_same_account(self):
        from gamemerger import devices
        inv = devices.inventory([{"name": "Schedule I", "source": "Steam"}], [{"name": "Hood", "host": "Schedule I", "extension": "Hood Warfare 2"}], "3.33.0")
        pc = self._lan([{"uid": "levy-uid", "name": "levy"}], inv)
        pc.handle({"t": "inv?", "device": "STRANGER", "uids": ["someone-else"]}, "10.0.0.9")
        self.assertEqual(pc.sent, [])  # a different account gets nothing
        pc.handle({"t": "inv?", "device": "LEVYS-LAPTOP", "uids": ["levy-uid"]}, "10.0.0.5")
        msg, ip = pc.sent[-1]
        self.assertEqual((msg["t"], ip), ("inv", "10.0.0.5"))
        self.assertEqual(msg["merges"][0]["name"], "Hood")
        laptop = self._lan([{"uid": "levy-uid", "name": "levy"}], devices.inventory([], [], "3.33.0"))
        laptop.handle(dict(msg, device="DESKTOP-1T0410G"), "10.0.0.4")
        self.assertEqual(laptop.inventories["desktop-1t0410g"]["merges"][0]["host"], "Schedule I")

    def test_joining_needs_a_yes_and_a_known_account(self):
        pc = self._lan([{"uid": "levy-uid", "name": "levy"}], {"games": [], "merges": []})
        pc.handle({"t": "join?", "device": "LEVYS-LAPTOP", "uid": "not-ours"}, "10.0.0.5")
        self.assertEqual(pc.requests, {})
        pc.handle({"t": "join?", "device": "LEVYS-LAPTOP", "uid": "levy-uid"}, "10.0.0.5")
        (rid, req), = pc.requests.items()
        self.assertEqual(req["account"], "levy")
        self.assertEqual(pc.sent, [])  # nothing is sent until someone says yes
        pc.answer(rid, True, "FRANK-code")
        self.assertEqual(pc.sent[-1][0]["code"], "FRANK-code")
        laptop = self._lan([], {"games": [], "merges": []})
        laptop.handle(dict(pc.sent[-1][0], device="DESKTOP-1T0410G"), "10.0.0.4")
        self.assertTrue(laptop.answers["desktop-1t0410g"]["ok"])

    def test_pairing_with_a_code(self):
        from gamemerger import devices
        laptop = self._lan([{"uid": "levy-uid", "name": "levy"}], {"games": [], "merges": []})
        laptop._broadcast = lambda msg: laptop.sent.append((msg, "bcast"))
        code = devices.new_pair_code()
        laptop.offer = (devices.pair_tag(code), devices.seal(code, "FRK1-levy"), time.time() + 60)
        laptop._broadcast({"t": "pair", "device": "LEVYS-LAPTOP", "tag": laptop.offer[0], "blob": laptop.offer[1]})
        pc = self._lan([{"uid": "pc-uid", "name": "levy"}], {"games": [], "merges": []})
        stranger = self._lan([], {"games": [], "merges": []})
        stranger.seek(devices.new_pair_code())
        stranger.handle(laptop.sent[-1][0], "10.0.0.5")
        self.assertIsNone(stranger.paired)          # a different code gets nothing
        pc.seek(code.lower().replace("-", " "))     # typed any old way
        pc.handle(laptop.sent[-1][0], "10.0.0.5")
        self.assertEqual(pc.paired, {"device": "LEVYS-LAPTOP", "code": "FRK1-levy"})
        self.assertNotIn("FRK1-levy", json.dumps(laptop.sent))  # only ever sent locked
        with self.assertRaises(devices.PairError):
            devices.clean_code("1234")

    def test_hello_goes_to_every_network(self):
        from gamemerger import devices
        self.assertEqual(devices.broadcast_addrs(["192.168.1.23", "172.20.64.1"]), ["255.255.255.255", "192.168.1.255", "172.20.64.255"])
        self.assertIn("localport=47811", devices.firewall_command("C:\\x\\Frankenstein.exe"))

    def test_cloud_sync_doesnt_swap_two_admins(self):
        from gamemerger import profiles, paths
        old = paths.data_dir
        roots = [tempfile.mkdtemp(), tempfile.mkdtemp()]
        try:
            recs = []
            for i, r in enumerate(roots):
                paths.data_dir = lambda r=r: r
                profiles.ensure_admin({}, "levy")
                data = profiles.load()
                data["profiles"][0]["id"] = ["paaaa", "pbbbb"][i]
                profiles.save(data)
                recs.append(profiles.export(data["profiles"][0]["id"]))
            for i, r in enumerate(roots):  # each reads the other's at once
                paths.data_dir = lambda r=r: r
                profiles.link(recs[1 - i], newer_only=True, seen={"paaaa", "pbbbb"})
            uids = []
            for r in roots:
                paths.data_dir = lambda r=r: r
                uids.append([profiles.uid(p) for p in profiles.load()["profiles"]])
            self.assertEqual(uids, [["paaaa"], ["paaaa"]])
        finally:
            paths.data_dir = old

    def test_cloud_folder_round_trip(self):
        from gamemerger import devices
        root = tempfile.mkdtemp()
        inv = devices.inventory([], [{"name": "Hood", "host": "Schedule I"}], "3.33.0")
        inv["device"] = "OTHER-PC"
        d = os.path.join(root, "Frankenstein devices", "levy-uid")
        os.makedirs(d)
        with open(os.path.join(d, "OTHER-PC.json"), "w") as f:
            json.dump(inv, f)
        got = devices.read_cloud([root], "levy-uid")
        self.assertEqual(got["other-pc"]["merges"][0]["name"], "Hood")
        self.assertEqual(devices.read_cloud([root], "someone-else"), {})


class CreativeFoldTests(unittest.TestCase):
    def test_creative_saves_join_the_others_marked_creative(self):
        from gamemerger import saves, paths
        old = paths.data_dir
        root = tempfile.mkdtemp()
        paths.data_dir = lambda: root
        try:
            main = os.path.join(saves.slot_root("m1"), "0", "7656")
            cre = os.path.join(saves.slot_root("m1~creative"), "0", "7656")
            for d, n in ((main, 1), (cre, 1), (cre, 2)):
                os.makedirs(os.path.join(d, "SaveGame_%d" % n))
                with open(os.path.join(d, "SaveGame_%d" % n, "Game.json"), "w") as f:
                    f.write("%s-%d" % ("main" if d == main else "creative", n))
            out = saves.fold_creative("m1", "schedule1")
            self.assertEqual(out, {"moved": 2, "kept": 0})
            self.assertEqual(open(os.path.join(main, "SaveGame_1", "Game.json")).read(), "main-1")  # untouched
            got = sorted(open(os.path.join(main, n, "Game.json")).read() for n in ("SaveGame_2", "SaveGame_3"))
            self.assertEqual(got, ["creative-1", "creative-2"])
            for n in ("SaveGame_2", "SaveGame_3"):
                self.assertEqual(open(os.path.join(main, n, "Modded", "Frankenstein", "gamemode.txt")).read(), "creative")
            self.assertFalse(os.path.exists(saves.slot_root("m1~creative")))
        finally:
            paths.data_dir = old


class BuiltinModTests(unittest.TestCase):
    def test_old_mod_in_game_copy_is_noticed(self):
        from gamemerger.api import Api
        root = tempfile.mkdtemp()
        src, game = os.path.join(root, "src"), os.path.join(root, "game")
        os.makedirs(src)
        os.makedirs(os.path.join(game, "Mods"))
        with open(os.path.join(src, "Frankenstein.dll"), "wb") as f:
            f.write(b"new build")
        self.assertTrue(Api._builtin_differs(src, game, "Mods"))  # missing
        with open(os.path.join(game, "Mods", "Frankenstein.dll"), "wb") as f:
            f.write(b"old build")  # same size, put back by a rebuild from the kept copy
        self.assertTrue(Api._builtin_differs(src, game, "Mods"))
        Api._builtin_zip(src, "Mods", os.path.join(root, "kept.zip"))
        import zipfile
        with zipfile.ZipFile(os.path.join(root, "kept.zip")) as z:
            self.assertEqual(z.read("Mods/Frankenstein.dll"), b"new build")
        with open(os.path.join(game, "Mods", "Frankenstein.dll"), "wb") as f:
            f.write(b"new build")
        self.assertFalse(Api._builtin_differs(src, game, "Mods"))


class PackTests(unittest.TestCase):
    def test_pack_keeps_only_changes_and_unpack_rebuilds(self):
        from gamemerger import saves
        root = tempfile.mkdtemp()
        base = os.path.join(root, "Game")
        os.makedirs(os.path.join(base, "Data"))
        for rel, text in (("Game.exe", "exe"), ("Data/big.pak", "x" * 5000), ("Data/old.cfg", "a=1"), ("gone.txt", "bye")):
            with open(os.path.join(base, rel), "w") as f:
                f.write(text)
        before = {r: open(os.path.join(base, r)).read() for r in ("Game.exe", "Data/big.pak", "Data/old.cfg", "gone.txt")}
        r = types.SimpleNamespace(id="t", name="Test", launch_exe=lambda t: None)
        t = types.SimpleNamespace(id="th", name="Theme")
        m = installer.Merge.create(r, t, base, os.path.join(root, "out"), installer.MODE_COPY, "Packed")
        with open(os.path.join(m.target_path, "Data", "old.cfg"), "w") as f:
            f.write("a=2")  # a mod's setting
        os.makedirs(os.path.join(m.target_path, "Mods"))
        with open(os.path.join(m.target_path, "Mods", "mod.dll"), "w") as f:
            f.write("mod")
        os.remove(os.path.join(m.target_path, "gone.txt"))
        slot = saves.slot_root(m.id)
        os.makedirs(slot)
        with open(os.path.join(slot, "save1.json"), "w") as f:
            f.write("{}")
        info = m.pack()
        saves.zip_slots(m.id)
        self.assertFalse(os.path.exists(m.target_path))
        self.assertTrue(m.packed)
        self.assertLess(info["zip_bytes"], info["copy_bytes"])
        self.assertFalse(os.path.isdir(slot))
        with self.assertRaises(installer.MergeError):
            m.put_file(os.path.join(base, "Game.exe"), "x.exe")  # packed: nothing can change it until unpacked
        m = installer.Merge.load(m.id)
        m.unpack()
        saves.unzip_slots(m.id)
        read = lambda rel: open(os.path.join(m.target_path, rel)).read()
        self.assertEqual(read("Data/old.cfg"), "a=2")
        self.assertEqual(read("Mods/mod.dll"), "mod")
        self.assertEqual(read("Data/big.pak"), "x" * 5000)
        self.assertFalse(os.path.exists(os.path.join(m.target_path, "gone.txt")))
        self.assertTrue(os.path.isfile(os.path.join(slot, "save1.json")))
        self.assertFalse(m.packed)
        # the original game was only read
        self.assertEqual(before, {r: open(os.path.join(base, r)).read() for r in before})
        self.assertFalse(os.path.exists(os.path.join(base, "Mods")))

    def test_short_on_room_rebuilds_elsewhere_and_leaves_no_half_copy(self):
        root = tempfile.mkdtemp()
        base = os.path.join(root, "Game")
        os.makedirs(base)
        with open(os.path.join(base, "Game.exe"), "w") as f:
            f.write("exe" * 1000)
        r = types.SimpleNamespace(id="t", name="Test", launch_exe=lambda t: None)
        t = types.SimpleNamespace(id="th", name="Theme")
        m = installer.Merge.create(r, t, base, os.path.join(root, "out"), installer.MODE_COPY, "Roomy")
        with open(os.path.join(m.target_path, "mod.txt"), "w") as f:
            f.write("mine")
        m.pack()
        room = m.unpack_room()
        self.assertGreaterEqual(room["need"], 3000 + installer.HEADROOM)
        real = shutil.disk_usage
        try:  # the drive is too full: the copy is refused and nothing is left half made
            shutil.disk_usage = lambda p: types.SimpleNamespace(free=1000, total=1, used=0)
            with self.assertRaises(installer.MergeError) as e:
                installer.Merge.load(m.id).unpack()
            self.assertIn("Free up", str(e.exception))
        finally:
            shutil.disk_usage = real
        m = installer.Merge.load(m.id)
        self.assertTrue(m.packed)
        self.assertFalse(os.path.exists(m.target_path))
        with self.assertRaises(installer.MergeError):
            m.move_copy_to(base)  # never inside the original
        new = m.move_copy_to(os.path.join(root, "other drive"))
        installer.Merge.load(m.id).unpack()
        self.assertEqual(open(os.path.join(new, "mod.txt")).read(), "mine")
        self.assertTrue(os.path.isfile(os.path.join(new, "Game.exe")))
        self.assertEqual(open(os.path.join(base, "Game.exe")).read(), "exe" * 1000)  # original untouched

    def test_unpack_clears_its_own_cut_off_rebuild_and_moves_strangers_aside(self):
        root = tempfile.mkdtemp()
        base = os.path.join(root, "Game")
        os.makedirs(os.path.join(base, "Data"))
        for rel in ("Game.exe", "Data/a.pak"):
            with open(os.path.join(base, rel), "w") as f:
                f.write("orig")
        r = types.SimpleNamespace(id="t", name="Test", launch_exe=lambda t: None)
        t = types.SimpleNamespace(id="th", name="Theme")
        m = installer.Merge.create(r, t, base, os.path.join(root, "out"), installer.MODE_COPY, "Cut")
        with open(os.path.join(m.target_path, "mod.txt"), "w") as f:
            f.write("mine")
        m.pack()
        # an older rebuild cut off half way: no marker, only the game's files
        os.makedirs(os.path.join(m.target_path, "Data"))
        with open(os.path.join(m.target_path, "Data", "a.pak"), "w") as f:
            f.write("or")
        installer.Merge.load(m.id).unpack()
        self.assertEqual(open(os.path.join(m.target_path, "mod.txt")).read(), "mine")
        self.assertTrue(os.path.isfile(os.path.join(m.target_path, installer.COPY_MARKER)))
        self.assertEqual(os.listdir(os.path.join(root, "out")), [os.path.basename(m.target_path)])
        # someone else's files in the way: moved aside, never deleted
        m = installer.Merge.load(m.id)
        m.pack()
        os.makedirs(m.target_path)
        with open(os.path.join(m.target_path, "holiday.jpg"), "w") as f:
            f.write("photo")
        installer.Merge.load(m.id).unpack()
        aside = [d for d in os.listdir(os.path.join(root, "out")) if "moved aside" in d]
        self.assertEqual(len(aside), 1)
        self.assertEqual(open(os.path.join(root, "out", aside[0], "holiday.jpg")).read(), "photo")
        self.assertEqual(open(os.path.join(m.target_path, "mod.txt")).read(), "mine")
        self.assertEqual(open(os.path.join(base, "Game.exe")).read(), "orig")  # original untouched

    def test_delete_all_merges_keeps_originals(self):
        from gamemerger import api, saves
        root = tempfile.mkdtemp()
        base = os.path.join(root, "Game")
        os.makedirs(base)
        with open(os.path.join(base, "Game.exe"), "w") as f:
            f.write("exe")
        r = types.SimpleNamespace(id="t", name="Test", launch_exe=lambda t: None)
        t = types.SimpleNamespace(id="th", name="Theme")
        made = [installer.Merge.create(r, t, base, os.path.join(root, "out"), installer.MODE_COPY, "All %d" % i) for i in range(2)]
        made[1].pack()
        os.makedirs(saves.slot_root(made[0].id), exist_ok=True)
        jobs = []
        fake = self._fake_api(jobs)
        api.Api.delete_all_merges(fake)
        mine = {m.id for m in made}
        self.assertFalse(any(m.id in mine for m in installer.Merge.all()))
        self.assertGreaterEqual(jobs[0]["deleted"], 2)
        self.assertGreater(jobs[0]["freed"], 0)
        for m in made:
            self.assertFalse(os.path.exists(m.target_path))
            self.assertFalse(os.path.exists(installer.Merge.record_dir(m.id)))
            self.assertFalse(os.path.exists(saves.slot_root(m.id)))
        self.assertEqual(open(os.path.join(base, "Game.exe")).read(), "exe")  # the installed game stays

    def _fake_api(self, jobs, games=()):
        from gamemerger import api
        fake = types.SimpleNamespace(_playing=None, _me=lambda: {"id": "me", "admin": True}, _games=list(games),
                                     _run=lambda fn: jobs.append(fn(types.SimpleNamespace(write=lambda *_: None, progress=None))))
        fake._protected = lambda: api.Api._protected(fake)
        fake._my_merges = lambda: api.Api._my_merges(fake)
        return fake

    def test_unmarked_old_copies_delete_only_when_asked_and_safe(self):
        from gamemerger import api
        root = tempfile.mkdtemp()
        base = os.path.join(root, "Steam", "Game")
        os.makedirs(base)
        with open(os.path.join(base, "Game.exe"), "w") as f:
            f.write("exe")
        r = types.SimpleNamespace(id="t", name="Test", launch_exe=lambda t: None)
        t = types.SimpleNamespace(id="th", name="Theme")
        m = installer.Merge.create(r, t, base, os.path.join(root, "out"), installer.MODE_COPY, "Old copy")
        os.remove(os.path.join(m.target_path, installer.COPY_MARKER))  # adopted from an older version: no marker
        fake = self._fake_api([])
        self.assertEqual([x["id"] for x in api.Api.delete_check(fake, m.id)], [m.id])
        with self.assertRaises(installer.MergeError):
            m.delete()  # not without asking
        self.assertTrue(os.path.isdir(m.target_path))
        # never the original, an installed game, or a folder around one, even when asked
        for target in (base, os.path.dirname(base)):
            bad = installer.Merge(dict(m.data, target_path=target))
            self.assertTrue(bad.why_not_delete_anyway())
            with self.assertRaises(installer.MergeError):
                bad.delete(force=True)
        other = os.path.join(root, "out", "Other game")
        os.makedirs(other)
        self.assertIn("installed game", installer.Merge(dict(m.data, target_path=os.path.join(root, "out"))).why_not_delete_anyway([other]))
        steam = os.path.join(root, "steamapps", "common", "X")
        os.makedirs(steam)
        self.assertIn("Steam", installer.Merge(dict(m.data, target_path=steam)).why_not_delete_anyway())
        self.assertEqual(open(os.path.join(base, "Game.exe")).read(), "exe")
        m.delete(force=True)  # asked, and safe
        self.assertFalse(os.path.exists(m.target_path))
        self.assertEqual(open(os.path.join(base, "Game.exe")).read(), "exe")

    def test_inplace_merges_are_never_packed(self):
        m = installer.Merge({"id": "x", "name": "X", "mode": installer.MODE_INPLACE, "base_path": "/", "target_path": "/"})
        self.assertIn("original", m.can_pack())
