using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Frankenstein's own weapons as real items of their own: the AK-47, Uzi, .308 sniper, RPG and grenade
    /// launcher each get an item in the game's registry (frankenstein_ak47 and so on), copied from the gun it fires
    /// like (so it shoots, reloads and saves like that one) but with its own name, description and icon. So the
    /// hotbar, the inventory and your hands show the sniper, not a revolver. Registered as soon as the game's
    /// registry is up, before any save loads, so saves that hold them keep them. What happened is in
    /// UserData/Frankenstein/ownitems-debug.txt.</summary>
    internal static class OwnItems
    {
        public const string Prefix = "frankenstein_";

        internal class Item
        {
            public string Key, Name, Description, Base;
        }

        public static readonly Item[] Items =
        {
            new Item { Key = "ak47", Name = "AK-47", Base = "m1911", Description = "Full auto, 30 rounds. Frankenstein's own." },
            new Item { Key = "uzi", Name = "Uzi", Base = "m1911", Description = "Fast and small, 32 rounds. Frankenstein's own." },
            new Item { Key = "sniper", Name = ".308 Sniper", Base = "revolver", Description = "Bolt action with a scope, 5 rounds, hits hard. Frankenstein's own." },
            new Item { Key = "rpg", Name = "RPG", Base = "pumpshotgun", Description = "One rocket at a time, and it explodes. Frankenstein's own." },
            new Item { Key = "launcher", Name = "Grenade Launcher", Base = "pumpshotgun", Description = "Six-shot drum: every shot is a grenade. Frankenstein's own." },
        };

        static readonly Dictionary<string, string> _ids = new Dictionary<string, string>();
        static readonly List<string> _log = new List<string>();
        static bool _done;
        static float _next;
        static int _tries;

        /// <summary>The registered item's ID for a weapon (ak47, sniper...), or null if it couldn't be made.</summary>
        public static string IdOf(string key) { string id; return _ids.TryGetValue(key, out id) ? id : null; }

        /// <summary>Which of Frankenstein's weapons this item ID is, or null.</summary>
        public static string KeyOf(string id)
        {
            if (id == null || !id.StartsWith(Prefix)) return null;
            var key = id.Substring(Prefix.Length);
            return Items.Any(i => i.Key == key) ? key : null;
        }

        public static void Update()
        {
            if (_done || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            try { Register(); }
            catch (Exception e) { Say("registering failed: " + (e.InnerException ?? e).Message); }
            if (++_tries >= 120 && !_done) { _done = true; Say("gave up after 2 minutes: the guns stay on their base gun's item"); }
        }

        static void Register()
        {
            var regT = Rt.FindByName("Registry");
            if (regT == null) return;
            const BindingFlags st = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            var reg = regT.GetProperty("Instance", st)?.GetValue(null, null) ?? Rt.All(regT).FirstOrDefault();
            if (reg == null || (reg is UnityEngine.Object ro && ro == null)) return;
            var get = regT.GetMethods(st).FirstOrDefault(m => m.Name == "GetItem" && !m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            var add = regT.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => m.Name == "AddToRegistry" && m.GetParameters().Length == 1);
            if (get == null || add == null) { Say("the registry has no GetItem/AddToRegistry"); _done = true; return; }
            var copy = Rt.Generic(typeof(UnityEngine.Object), "Instantiate", 1);
            bool all = true;
            foreach (var it in Items)
            {
                var id = Prefix + it.Key;
                if (_ids.ContainsKey(it.Key)) continue;
                if (Lookup(get, id) != null) { _ids[it.Key] = id; continue; }
                var baseId = Story.ItemId(it.Base) ?? (it.Base == "pumpshotgun" ? Story.ItemId("shotgun") : null);
                var src = baseId == null ? null : Lookup(get, baseId);
                if (src == null) { all = false; continue; }  // the game's items aren't in yet
                var made = copy.MakeGenericMethod(get.ReturnType).Invoke(null, new[] { src });
                if (made == null) { all = false; continue; }
                Rt.Set(made, id, "ID");
                Rt.Set(made, it.Name, "Name");
                Rt.Set(made, it.Description, "Description");
                var icon = Looks.Sprite(it.Key);
                if (icon != null) Rt.Set(made, icon, "Icon");
                var uo = made as UnityEngine.Object;
                if (uo != null) { uo.name = id; uo.hideFlags = HideFlags.DontUnloadUnusedAsset; }
                add.Invoke(reg, new[] { made });
                if (Lookup(get, id) == null) { Say(it.Name + ": added, but the registry doesn't find it"); all = false; continue; }
                _ids[it.Key] = id;
                Say(it.Name + ": registered as " + id + " (copied from " + baseId + (icon != null ? ", own icon" : "") + ")");
            }
            if (all) { _done = true; Say("all " + Items.Length + " weapons are their own items"); }
        }

        static object Lookup(MethodInfo get, string id)
        {
            try
            {
                var o = get.Invoke(null, new object[] { id });
                return o is UnityEngine.Object u && u == null ? null : o;
            }
            catch (Exception) { return null; }
        }

        static void Say(string line)
        {
            Melon<Plugin>.Logger.Msg("Own items: " + line);
            _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
            try
            {
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "ownitems-debug.txt"), new[] { "Frankenstein " + Plugin.Version }.Concat(_log.Skip(Math.Max(0, _log.Count - 100))).ToArray());
            }
            catch (Exception) { }
        }
    }
}
