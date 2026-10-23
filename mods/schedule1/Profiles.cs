using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The menu's Profiles: one card per save slot, read from the save's own files (your organisation's
    /// name, cash, rank, properties owned, its game mode). Any number the save doesn't have shows as "-". Play loads
    /// that save through the game's own loader; if this build hides it, the game's own Continue screen opens.</summary>
    internal static class Profiles
    {
        internal class Slot
        {
            public int Number;
            public string Path = "", Name = "", Mode = "survival";
            public double Cash = double.NaN, Rank = double.NaN, Properties = double.NaN, Xp = double.NaN, Empire = double.NaN;
            public DateTime Played;
            public bool Empty => Path.Length == 0;
        }

        public const int Slots = 5;
        static List<Slot> _slots;
        static float _readAt;

        public static void Forget() { _slots = null; }

        public static List<Slot> All()
        {
            if (_slots != null && Time.unscaledTime < _readAt) return _slots;
            _readAt = Time.unscaledTime + 5f;
            var list = Enumerable.Range(1, Slots).Select(i => new Slot { Number = i }).ToList();
            try
            {
                var root = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "TVGS", "Schedule I", "Saves");
                if (Directory.Exists(root))
                    foreach (var d in Directory.GetDirectories(root).SelectMany(Directory.GetDirectories)
                                               .Where(d => System.IO.Path.GetFileName(d).StartsWith("SaveGame_", StringComparison.OrdinalIgnoreCase))
                                               .OrderBy(d => Directory.GetLastWriteTime(d)))
                    {
                        int n;
                        if (!int.TryParse(System.IO.Path.GetFileName(d).Substring(9), out n) || n < 1 || n > Slots) continue;
                        list[n - 1] = Read(d, n);
                    }
            }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Profiles: " + e.Message); }
            _slots = list;
            return list;
        }

        public static Slot Latest() => All().Where(s => !s.Empty).OrderByDescending(s => s.Played).FirstOrDefault();

        static Slot Read(string dir, int n)
        {
            var s = new Slot { Number = n, Path = dir, Mode = Mode.Of(dir), Played = Directory.GetLastWriteTime(dir) };
            var game = Json(dir, "Game.json");
            s.Name = Find(game, "OrganisationName", "OrganizationName", "OrgName") as string ?? "";
            if (s.Name.Length == 0) s.Name = "Save " + n;
            var money = Json(dir, "Money.json");
            s.Cash = Sum(money, "OnlineBalance", "Balance", "CashBalance");
            var rank = Json(dir, "Rank.json");
            var r = Num(Find(rank, "Rank", "CurrentRank"));
            var tier = Num(Find(rank, "Tier", "CurrentTier"));
            if (!double.IsNaN(r)) { s.Rank = r * 5 + (double.IsNaN(tier) ? 0 : tier) + 1; s.Empire = r + 1; }  // Drug Empire: the rank's own level
            s.Xp = Num(Find(rank, "TotalXP", "XP", "Xp", "CurrentXP"));  // Hood Reputation
            try
            {
                var props = System.IO.Path.Combine(dir, "Properties");
                if (Directory.Exists(props))
                    s.Properties = Directory.GetFiles(props, "*.json", SearchOption.AllDirectories)
                        .Count(f => { var j = Json(System.IO.Path.GetDirectoryName(f), System.IO.Path.GetFileName(f)); return Find(j, "IsOwned", "Owned") is bool b && b; });
            }
            catch (Exception) { }
            return s;
        }

        static object Json(string dir, string name)
        {
            try
            {
                var p = System.IO.Path.Combine(dir, name);
                return File.Exists(p) ? MiniJson.Parse(File.ReadAllText(p)) : null;
            }
            catch (Exception) { return null; }
        }

        /// <summary>The first value under any of these keys, at any depth.</summary>
        static object Find(object o, params string[] keys)
        {
            var d = o as Dictionary<string, object>;
            if (d != null)
            {
                foreach (var k in keys)
                    foreach (var kv in d)
                        if (string.Equals(kv.Key, k, StringComparison.OrdinalIgnoreCase)) return kv.Value;
                foreach (var v in d.Values) { var f = Find(v, keys); if (f != null) return f; }
            }
            var l = o as List<object>;
            if (l != null) foreach (var v in l) { var f = Find(v, keys); if (f != null) return f; }
            return null;
        }

        static double Num(object o) => o is double d ? d : double.NaN;

        static double Sum(object o, params string[] keys) => Num(Find(o, keys));

        public static string Money(double v) => double.IsNaN(v) ? "-" : "$" + Math.Round(v).ToString("#,0");
        public static string Count(double v) => double.IsNaN(v) ? "-" : ((int)v).ToString();

        /// <summary>Loads this save the way the game's own Continue screen does.</summary>
        public static bool Play(Slot s)
        {
            try
            {
                var lmType = Rt.FindByName("LoadManager");
                var lm = lmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null, null)
                         ?? Rt.All(lmType).FirstOrDefault();
                if (lm == null) return false;
                var start = lm.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "StartGame" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType.Name.Contains("SaveInfo"));
                if (start == null) return false;
                object info = null;
                foreach (var name in new[] { "SaveGames", "saveGames", "SaveInfos" })
                {
                    foreach (var i in Creative.Each(Rt.Get(lm, name) ?? StaticGet(lmType, name)))
                        if (Same(Rt.Get(i, "SavePath", "savePath") as string, s.Path)) info = i;
                    if (info != null) break;
                }
                if (info == null) { Melon<Plugin>.Logger.Msg("Profiles: the game's list of saves doesn't have " + s.Path); return false; }
                var ps = start.GetParameters();
                start.Invoke(lm, new[] { info }.Concat(ps.Skip(1).Select(p => p.IsOptional ? p.DefaultValue : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null))).ToArray());
                return true;
            }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Profiles: couldn't load the save: " + (e.InnerException ?? e).Message); return false; }
        }

        static object StaticGet(Type t, string name)
        {
            const BindingFlags f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            try { return t?.GetProperty(name, f)?.GetValue(null, null) ?? t?.GetField(name, f)?.GetValue(null); } catch (Exception) { return null; }
        }

        static bool Same(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            return string.Equals(System.IO.Path.GetFullPath(a).TrimEnd('\\', '/'), System.IO.Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
    }
}
