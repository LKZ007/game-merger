using System;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Creative is a game mode on each save, like Minecraft's: one set of saves, and any save can be Survival
    /// or Creative. The mode is kept with the save itself (SaveGame_N/Modded/Frankenstein/gamemode.txt, the folder
    /// S1API uses for mod data), so it travels with backups and packing. Pick it on the main menu before you start
    /// or load a game, or switch the save you're in from F8 (or the controller wheel): no restart. The first time
    /// a save changes mode, a copy of it is put aside in UserData/Frankenstein/save-backups.</summary>
    internal static class Mode
    {
        /// <summary>Chosen on the main menu (or by Frankenstein's app): the mode the next game you start or load
        /// gets. "" keeps each save's own mode.</summary>
        public static string Pending = "";
        public static string SavePath = "";
        static float _next;
        static object _lm;

        public static string Key => SavePath.Length > 0 ? new DirectoryInfo(SavePath).Name : "";

        static string ModeFile(string save) => Path.Combine(save, "Modded", "Frankenstein", "gamemode.txt");

        public static string Of(string save)
        {
            try { return File.Exists(ModeFile(save)) && File.ReadAllText(ModeFile(save)).Trim() == "creative" ? "creative" : "survival"; }
            catch (Exception) { return "survival"; }
        }

        /// <summary>The game's own saves on this PC (SaveGame_1..5 under the Steam account's folder), with their mode.</summary>
        public static string[][] Saves()
        {
            try
            {
                var saves = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "TVGS", "Schedule I", "Saves");
                if (!Directory.Exists(saves)) return new string[0][];
                return Directory.GetDirectories(saves).SelectMany(Directory.GetDirectories)
                    .Where(d => Path.GetFileName(d).StartsWith("SaveGame", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => Path.GetFileName(d))
                    .Select(d => new[] { Path.GetFileName(d).Replace("SaveGame_", "Save "), Of(d) }).ToArray();
            }
            catch (Exception) { return new string[0][]; }
        }

        static string Loaded()
        {
            if (_lm == null) _lm = Rt.All(Rt.FindByName("LoadManager")).FirstOrDefault();
            return Rt.Get(_lm, "LoadedGameFolderPath") as string ?? "";
        }

        /// <summary>The town scene starts after the game has picked its save, so the save (and its mode) is known
        /// before the gangs and crews load their data for it.</summary>
        public static void Scene(bool inTown)
        {
            _inTown = inTown;
            _lm = null;
            if (!inTown) { SavePath = ""; return; }
            _next = 0;
            Update(true);
        }

        static bool _inTown;

        public static void Tick() => Update(_inTown);

        /// <summary>Watches which save is loaded; when it changes, takes that save's mode (or the one picked on
        /// the main menu).</summary>
        public static void Update(bool inTown)
        {
            if (!inTown) { _lm = null; return; }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            string p;
            try { p = Loaded(); } catch (Exception) { _lm = null; return; }
            if (string.IsNullOrEmpty(p) || p == SavePath) return;
            SavePath = p;
            if (Pending.Length > 0)
            {
                if (Pending == "creative" && Of(SavePath) != "creative") Creative.StartEmpty = true;  // a new Creative world: empty hotbar
                Set(Pending, false);
                Pending = "";
            }
            else MainMenu.Creative = Of(SavePath) == "creative";
            Melon<Plugin>.Logger.Msg("Game mode: " + Path.GetFileName(SavePath) + " is " + (MainMenu.Creative ? "Creative" : "Survival") + ".");
        }

        /// <summary>Sets the loaded save's mode. A copy of the save is put aside the first time it changes.</summary>
        public static void Set(string mode, bool tell = true)
        {
            bool creative = mode == "creative";
            if (SavePath.Length > 0 && Of(SavePath) != mode)
            {
                Backup(SavePath);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ModeFile(SavePath)));
                    File.WriteAllText(ModeFile(SavePath), mode);
                }
                catch (Exception e) { Melon<Plugin>.Logger.Warning("Game mode: couldn't write it to the save: " + e.Message); }
            }
            MainMenu.Creative = creative;
            Creative.Forget();
            if (tell) ItemMenu.Notify(creative ? "Creative: every item, unlimited. Saved with this game." : "Survival: back to the normal rules.");
        }

        static void Backup(string save)
        {
            try
            {
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "save-backups");
                var name = Path.GetFileName(save);
                if (Directory.Exists(dir) && Directory.GetDirectories(dir).Any(d => Path.GetFileName(d).StartsWith(name + " "))) return;  // only the first time
                Copy(save, Path.Combine(dir, name + " " + DateTime.Now.ToString("yyyy-MM-dd HHmm")));
            }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Game mode: save backup failed: " + e.Message); }
        }

        static void Copy(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(from)) Copy(d, Path.Combine(to, Path.GetFileName(d)));
        }

        /// <summary>Frankenstein's own data for this save (gangs, crews): one file per save, started from the
        /// older shared file the first time.</summary>
        public static string DataFile(string stem)
        {
            var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
            if (Key.Length == 0) return Path.Combine(dir, stem + ".json");
            var keyed = Path.Combine(dir, stem + "-" + Key + ".json");
            if (!File.Exists(keyed))
            {
                var old = Path.Combine(dir, stem + (MainMenu.Creative ? "-creative" : "") + ".json");
                try { if (File.Exists(old)) File.Copy(old, keyed); } catch (Exception) { }
            }
            return keyed;
        }
    }
}
