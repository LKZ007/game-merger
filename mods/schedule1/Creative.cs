using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Creative is unlimited, like Minecraft's: the gun in your hand never runs dry, and whatever you
    /// place or use from your hotbar comes straight back. Moving items into storage, selling them or dropping them
    /// moves them (nothing comes back while a menu is open, or after a drop). Only in Creative saves.
    /// Reads the game's own hotbar and gun by name on either build; what it can't reach is logged once.</summary>
    internal static class Creative
    {
        static float _next, _nextGuns;
        static List<object> _guns = new List<object>();
        static readonly Dictionary<string, MethodInfo> _methods = new Dictionary<string, MethodInfo>();
        static Type _inv, _gun;
        static readonly Dictionary<int, KeyValuePair<string, int>> _had = new Dictionary<int, KeyValuePair<string, int>>();
        static bool _warned;

        /// <summary>Set when a new world starts in Creative: its hotbar is emptied once, so you start with nothing.</summary>
        public static bool StartEmpty;

        public static void Update()
        {
            if (!MainMenu.Creative || Time.unscaledTime < _next) return;
            if (StartEmpty) Empty();
            _next = Time.unscaledTime + Perf.Every(0.25f);
            try { Ammo(); } catch (Exception e) { Warn("ammo", e); }
            try { Items(); } catch (Exception e) { Warn("items", e); }
        }

        static void Warn(string what, Exception e)
        {
            if (_warned) return;
            _warned = true;
            Melon<Plugin>.Logger.Warning("Creative unlimited " + what + ": " + e.Message);
        }

        static void Ammo()
        {
            if (_gun == null) _gun = Rt.FindByName("Equippable_RangedWeapon");
            if (_gun == null) return;
            if (Time.unscaledTime >= _nextGuns) { _nextGuns = Time.unscaledTime + Perf.Every(2f); _guns = Rt.All(_gun); }
            foreach (var w in _guns)
            {
                if (w is UnityEngine.Object uo && uo == null) continue;
                var size = Rt.Get(w, "MagazineSize");
                var inst = Rt.Get(w, "weaponItem", "itemInstance", "ItemInstance");
                if (size == null || inst == null) continue;
                int full = Convert.ToInt32(size), now = Convert.ToInt32(Rt.Get(inst, "Value") ?? full);
                if (now >= full) continue;
                if (!Call(inst, "SetValue", full)) Rt.Set(inst, full, "Value");
            }
        }

        static bool Call(object o, string method, params object[] args)
        {
            if (o == null) return false;
            var key = o.GetType().FullName + "|" + method + "|" + args.Length;
            MethodInfo m;
            if (!_methods.TryGetValue(key, out m))
                _methods[key] = m = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .FirstOrDefault(x => x.Name == method && x.GetParameters().Length >= args.Length &&
                                          x.GetParameters().Skip(args.Length).All(p => p.IsOptional));
            if (m == null) return false;
            var ps = m.GetParameters();
            var full = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
                full[i] = i < args.Length ? Convert.ChangeType(args[i], ps[i].ParameterType) : ps[i].DefaultValue;
            m.Invoke(o, full);
            return true;
        }

        /// <summary>Lists from either build: a normal list, or IL2CPP's own (Count and an indexer).</summary>
        internal static IEnumerable<object> Each(object list)
        {
            if (list == null) yield break;
            if (list is System.Collections.IEnumerable e)
            {
                foreach (var x in e) yield return x;
                yield break;
            }
            var count = Rt.Get(list, "Count");
            var item = list.GetType().GetMethod("get_Item");
            if (count == null || item == null) yield break;
            for (int i = 0; i < Convert.ToInt32(count); i++) yield return item.Invoke(list, new object[] { i });
        }

        static void Items()
        {
            var inv = Boom.Inventory();
            if (inv == null) return;
            var slots = Each(Rt.Get(inv, "hotbarSlots", "HotbarSlots")).ToList();
            // a menu is open (inventory, a storage rack, a shop, the phone): items moved there are moved, not used,
            // so nothing comes back; the hotbar is read afresh once you're playing again
            if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible) { _had.Clear(); return; }
            for (int i = 0; i < slots.Count; i++)
            {
                var item = Rt.Get(slots[i], "ItemInstance");
                string id = item != null ? Rt.Get(item, "ID") as string : null;
                int qty = Convert.ToInt32(Rt.Get(slots[i], "Quantity") ?? 0);
                KeyValuePair<string, int> was;
                bool had = _had.TryGetValue(i, out was);
                if (had && was.Key != null && was.Value > 0)
                {
                    if (id == was.Key && qty < was.Value)
                    {
                        // used, placed or sold: put the same amount back in the same slot
                        if (!Call(slots[i], "ChangeQuantity", was.Value - qty)) S1API.Console.ConsoleHelper.AddItemToInventory(id, was.Value - qty);
                        continue;
                    }
                    if (id == null)
                    {
                        // the last one went: it comes back (to the first free slot)
                        S1API.Console.ConsoleHelper.AddItemToInventory(was.Key, was.Value);
                        _had.Remove(i);
                        continue;
                    }
                }
                if (id != null) _had[i] = new KeyValuePair<string, int>(id, Math.Max(qty, 1));
                else _had.Remove(i);
            }
        }

        static void Empty()
        {
            var slots = Boom.Slots();
            if (slots.Count == 0) return;  // not loaded yet: next time
            StartEmpty = false;
            int n = 0;
            foreach (var slot in slots)
                if (Boom.Id(slot) != null && !Boom.Id(slot).ToLowerInvariant().Contains("cash") && Call(slot, "ClearStoredInstance")) n++;
            _had.Clear();
            Melon<Plugin>.Logger.Msg("Creative: started empty (" + n + " slot(s) cleared).");
        }

        /// <summary>Dropping something on purpose shouldn't make it come back: Frankenstein forgets the hotbar
        /// when you open the item menu or leave the town.</summary>
        public static void Forget() => _had.Clear();
    }
}
