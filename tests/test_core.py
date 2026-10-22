import json
import os
import tempfile
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
