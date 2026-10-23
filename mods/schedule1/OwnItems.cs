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
            Checks();
            if (_done || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            try { Register(); }
            catch (Exception e) { Say("registering failed: " + (e.InnerException ?? e).Message); }
            if (++_tries >= 300 && !_done) { _done = true; Say("gave up after 5 minutes: the guns stay on their base gun's item (see the lines above)"); }
        }

        static readonly HashSet<string> _said = new HashSet<string>();

        /// <summary>Says a line once (the registration retries every second until the game's items are in).</summary>
        static void Once(string line) { if (_said.Add(line)) Say(line); }

        static Type RegistryType()
        {
            var t = Rt.Find("ScheduleOne.Registry");
            if (t != null) return t;
            // any game type called Registry that has GetItem(string)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name;
                if (an != "Assembly-CSharp" && an != "Il2CppAssembly-CSharp") continue;
                Type[] ts;
                try { ts = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { ts = e.Types.Where(x => x != null).ToArray(); }
                t = ts.FirstOrDefault(x => x.Name == "Registry" && x.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).Any(m => m.Name == "GetItem"));
                if (t != null) return t;
            }
            return null;
        }

        static void Register()
        {
            var regT = RegistryType();
            if (regT == null) { Once("waiting: the game's Registry type isn't found"); return; }
            Once("registry type: " + regT.FullName);
            const BindingFlags st = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            object reg = null;
            try { reg = Rt.Instance(regT); } catch (Exception e) { Once("Registry.Instance failed: " + (e.InnerException ?? e).Message); }
            if (reg == null || (reg is UnityEngine.Object r0 && r0 == null)) reg = Rt.All(regT).FirstOrDefault();
            if (reg == null || (reg is UnityEngine.Object ro && ro == null)) { Once("waiting: the Registry isn't up yet"); return; }
            var get = regT.GetMethods(st).FirstOrDefault(m => m.Name == "GetItem" && !m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string))
                      ?? regT.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => m.Name == "GetItem" && !m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string));
            var add = regT.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).FirstOrDefault(m => m.Name == "AddToRegistry" && m.GetParameters().Length == 1);
            if (get == null || add == null)
            {
                Say("the registry has no GetItem/AddToRegistry. It has: " + string.Join(", ", regT.GetMethods().Select(m => m.Name).Distinct().Take(60).ToArray()));
                _done = true;
                return;
            }
            Once("GetItem returns " + get.ReturnType.FullName + (get.IsStatic ? " (static)" : "") + "; AddToRegistry takes " + add.GetParameters()[0].ParameterType.FullName);
            var copy = Rt.Generic(typeof(UnityEngine.Object), "Instantiate", 1);
            if (copy == null) { Say("no Object.Instantiate<T> in this build"); _done = true; return; }
            bool all = true;
            foreach (var it in Items)
            {
                var id = Prefix + it.Key;
                if (_ids.ContainsKey(it.Key)) continue;
                if (Lookup(get, reg, id) != null) { _ids[it.Key] = id; Once(it.Name + ": already in the registry as " + id); continue; }
                string baseId = null;
                try { baseId = Story.ItemId(it.Base) ?? (it.Base == "pumpshotgun" ? Story.ItemId("shotgun") : null); }
                catch (Exception e) { Once(it.Name + ": finding the " + it.Base + " failed: " + e.Message); }
                if (baseId == null) { Once("waiting: no " + it.Base + " in the game's item list yet"); all = false; continue; }
                var src = Lookup(get, reg, baseId);
                if (src == null) { Once("waiting: the registry doesn't have " + baseId + " yet"); all = false; continue; }
                try
                {
                    var made = copy.MakeGenericMethod(add.GetParameters()[0].ParameterType.IsInstanceOfType(src) ? src.GetType() : get.ReturnType).Invoke(null, new[] { src });
                    if (made == null) { Once(it.Name + ": copying the " + baseId + " gave nothing"); all = false; continue; }
                    var did = new List<string>();
                    did.Add(Rt.Set(made, id, "ID", "id") ?? "ID NOT SET");
                    did.Add(Rt.Set(made, it.Name, "Name", "name") ?? "Name NOT SET");
                    did.Add(Rt.Set(made, it.Description, "Description", "description") ?? "Description NOT SET");
                    var icon = Looks.Sprite(it.Key);
                    did.Add(icon == null ? "no icon made" : Rt.Set(made, icon, "Icon", "icon") ?? "Icon NOT SET");
                    var uo = made as UnityEngine.Object;
                    if (uo != null) { uo.name = id; uo.hideFlags = HideFlags.DontUnloadUnusedAsset; }
                    add.Invoke(add.IsStatic ? null : reg, new[] { made });
                    var back = Lookup(get, reg, id);
                    Say(it.Name + ": copied " + baseId + " (" + made.GetType().Name + "), set " + string.Join(", ", did.ToArray()) +
                        (back == null ? "; added, but the registry doesn't find " + id : "; registered as " + id + " (ID reads back " + (Rt.Get(back, "ID") ?? "?") + ")"));
                    if (back == null) { all = false; continue; }
                    _ids[it.Key] = id;
                }
                catch (Exception e)
                {
                    Say(it.Name + ": registering failed: " + (e.InnerException ?? e).GetType().Name + ": " + (e.InnerException ?? e).Message);
                    all = false;
                }
            }
            if (all) { _done = true; Say("all " + Items.Length + " weapons are their own items"); }
        }

        /// <summary>After F6 gives one of these: which item really landed in the hotbar, with its name and icon.</summary>
        public static void CheckGiven(string key, string want)
        {
            _checks.Add(new KeyValuePair<float, string[]>(Time.unscaledTime + 1.5f, new[] { key, want ?? "(not registered: the base gun was given)" }));
        }

        static readonly List<KeyValuePair<float, string[]>> _checks = new List<KeyValuePair<float, string[]>>();

        public static void Checks()
        {
            for (int i = _checks.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < _checks[i].Key) continue;
                var c = _checks[i].Value;
                _checks.RemoveAt(i);
                var slots = Boom.Slots();
                var line = "self-test " + c[0] + ": asked for " + c[1] + "; hotbar now: " +
                           string.Join(" | ", slots.Select((s, n) =>
                           {
                               var inst = Rt.Get(s, "ItemInstance");
                               if (inst == null) return (n + 1) + " empty";
                               var def = Rt.Get(inst, "Definition", "definition");
                               var icon = Rt.Get(def, "Icon", "icon") as UnityEngine.Object;
                               return (n + 1) + " " + Boom.Id(s) + " \"" + (Rt.Get(inst, "Name") as string) + "\" icon " + (icon != null ? icon.name : "none");
                           }).ToArray());
                Say(line);
            }
        }

        static object Lookup(MethodInfo get, object reg, string id)
        {
            try
            {
                var o = get.Invoke(get.IsStatic ? null : reg, new object[] { id });
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
