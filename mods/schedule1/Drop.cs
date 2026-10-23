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
    /// <summary>Dropping, weapons included. While you play: G or d-pad down drops what's in your hand. With the
    /// inventory or a storage rack open: LB and RB pick a hotbar slot (shown at the bottom), d-pad down (or G)
    /// drops that slot. Schedule I has no drop button for the controller and won't let some items (guns) be dropped,
    /// so Frankenstein first tries the game's own drop and checks the item really left the slot; if it didn't, the
    /// slot is emptied itself. Everything it found and tried is in UserData/Frankenstein/drop-debug.txt.
    /// A drop is a move: Creative doesn't put dropped items back.</summary>
    internal static class Drop
    {
        static readonly List<KeyValuePair<object, MethodInfo>> _ways = new List<KeyValuePair<object, MethodInfo>>();
        static bool _looked;
        static readonly List<string> _log = new List<string>();

        static bool _has, _menu;
        static float _next;
        static int _pick;

        public static void Update()
        {
            if (Pad.InUse && Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.3f;
                try { var s = Equipped(out _); _has = s != null && Boom.Id(s) != null; } catch (Exception) { _has = false; }
            }
            if (S1API.Entities.Player.Local == null || MenuSkin.Active) return;  // only while you play
            if (ItemMenu.IsOpen || Wheel.Open || Pad.PanelsOpen) return;
            bool press = (Pad.Pressed & Pad.Down) != 0 || Input.GetKeyDown(KeyCode.G);
            _menu = Cursor.lockState != CursorLockMode.Locked || Cursor.visible;  // the inventory or a storage rack is open
            if (_menu)
            {
                int n = Boom.Slots().Count;
                if (n > 0)
                {
                    if ((Pad.Pressed & Pad.RB) != 0) _pick = (_pick + 1) % n;
                    if ((Pad.Pressed & Pad.LB) != 0) _pick = (_pick + n - 1) % n;
                    _pick = Math.Min(_pick, n - 1);
                }
                if (press) Slot(_pick);
                return;
            }
            if (press) Now();
        }

        static object Equipped(out int index)
        {
            var slots = Boom.Slots();
            index = Boom.EquippedIndex(slots);
            return index >= 0 ? slots[index] : null;
        }

        /// <summary>A line for drop-debug.txt from elsewhere (how the held slot was found).</summary>
        public static void Note(string line) => Say(line);

        public static void Now()
        {
            var slot = Equipped(out var index);
            if (slot == null || Boom.Id(slot) == null)
            {
                Say("drop pressed: no held slot found (index " + index + ")" + (slot != null ? ", the slot reads empty" : ""));
                ItemMenu.Notify("Couldn't tell what's in your hand. Open the inventory and use LB/RB to pick the slot, then drop it. drop-debug.txt has why.");
                return;
            }
            Slot(index);
        }

        /// <summary>Drops one hotbar slot: the game's own drop if it takes it, otherwise the slot is emptied.</summary>
        public static void Slot(int index)
        {
            var slots = Boom.Slots();
            if (index < 0 || index >= slots.Count || Boom.Id(slots[index]) == null) { ItemMenu.Notify("That slot is empty."); return; }
            var slot = slots[index];
            var id = Boom.Id(slot);
            var name = Rt.Get(Rt.Get(slot, "ItemInstance"), "Name") as string ?? id;
            Creative.Forget();  // dropped on purpose: Creative doesn't put it back
            Find(slot);
            foreach (var w in _ways)
            {
                try
                {
                    var target = w.Key is Type ? null : (w.Key is UnityEngine.Object uo && uo == null ? null : w.Key);
                    var ps = w.Value.GetParameters();
                    var args = ps.Select((p, i) => i == 0 && p.ParameterType.IsInstanceOfType(slot) ? slot :
                                                   i == 0 && p.ParameterType == typeof(int) ? (object)index :
                                                   i == 0 && p.ParameterType.IsInstanceOfType(Rt.Get(slot, "ItemInstance")) ? Rt.Get(slot, "ItemInstance") :
                                                   p.IsOptional ? p.DefaultValue : (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)).ToArray();
                    if (ps.Length > 0 && args[0] == null) continue;
                    if (ps.Length == 0 && !w.Value.DeclaringType.IsInstanceOfType(slot)) { Equipped(out var held); if (held != index) continue; }  // drops what's held: only for that slot
                    w.Value.Invoke(w.Value.DeclaringType.IsInstanceOfType(slot) ? slot : target, args);
                    if (Boom.Id(slot) != id) { Say("dropped " + name + " with " + w.Value.DeclaringType.Name + "." + w.Value.Name); ItemMenu.Notify("Dropped " + name + "."); return; }
                    Say(w.Value.DeclaringType.Name + "." + w.Value.Name + " ran but " + name + " stayed in the slot");
                }
                catch (Exception e) { Say(w.Value.DeclaringType.Name + "." + w.Value.Name + " failed: " + (e.InnerException ?? e).Message); }
            }
            // the game wouldn't drop it (it doesn't let guns go): the slot is emptied
            try
            {
                if (!Call(slot, "ClearStoredInstance")) Call(slot, "ChangeQuantity", -Convert.ToInt32(Rt.Get(slot, "Quantity") ?? 1));
            }
            catch (Exception e) { Say("emptying the slot failed: " + (e.InnerException ?? e).Message); }
            if (Boom.Id(slot) != id) { Say("emptied the slot for " + name); ItemMenu.Notify("Dropped " + name + " (the game has no drop for it here, so it's gone: F6 has it again)."); }
            else ItemMenu.Notify("Couldn't drop " + name + ". drop-debug.txt has what was tried.");
        }

        static bool Call(object o, string name, params object[] args)
        {
            var m = o?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(x => x.Name == name && x.GetParameters().Length >= args.Length &&
                                                                                                         x.GetParameters().Skip(args.Length).All(p => p.IsOptional));
            if (m == null) return false;
            var ps = m.GetParameters();
            m.Invoke(o, ps.Select((p, i) => i < args.Length ? Convert.ChangeType(args[i], p.ParameterType) : p.DefaultValue).ToArray());
            return true;
        }

        static void Say(string line)
        {
            Melon<Plugin>.Logger.Msg("Drop: " + line);
            _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
            Write();
        }

        static void Write()
        {
            try
            {
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "drop-debug.txt"), new[] { "Frankenstein " + Plugin.Version }.Concat(_log.Skip(Math.Max(0, _log.Count - 200))).ToArray());
            }
            catch (Exception) { }
        }

        /// <summary>The game's own drop calls: on the inventory, the slot, and the inventory screen's managers.</summary>
        static void Find(object slot)
        {
            if (_looked) return;
            _looked = true;
            var owners = new List<object> { Rt.All(Rt.FindByName("PlayerInventory")).FirstOrDefault(), slot };
            foreach (var n in new[] { "ItemUIManager", "InventoryUI", "PlayerInventoryUI" })
            {
                var t = Rt.FindByName(n);
                if (t != null) owners.Add(t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null, null) ?? Rt.All(t).FirstOrDefault());
            }
            foreach (var o in owners.Where(o => o != null))
            {
                var ms = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                           .Where(m => (m.Name.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Discard", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                       !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_") && !m.Name.StartsWith("On") && !m.Name.Contains("Dropdown"))
                           .ToList();
                _log.Add(o.GetType().Name + " has " + (ms.Count == 0 ? "no drop" : string.Join(", ", ms.Select(m => m.Name + "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name).ToArray()) + ")").ToArray())));
                // the ones that take the slot, its item or its number first, then the ones that drop what's held
                foreach (var m in ms.Where(m => m.GetParameters().Length >= 1).Concat(ms.Where(m => m.GetParameters().Length == 0)))
                    if (m.GetParameters().Skip(1).All(p => p.IsOptional || p.ParameterType == typeof(bool) || p.ParameterType == typeof(int)))
                        _ways.Add(new KeyValuePair<object, MethodInfo>(o, m));
            }
            Write();
        }

        public static void Draw()
        {
            if (ItemMenu.IsOpen || Wheel.Open || Pad.PanelsOpen || MenuSkin.Active || S1API.Entities.Player.Local == null) return;
            if (_menu && Pad.InUse)
            {
                var slots = Boom.Slots();
                if (slots.Count == 0) return;
                var slot = slots[Math.Min(_pick, slots.Count - 1)];
                var id = Boom.Id(slot);
                var name = id == null ? "empty" : (Rt.Get(Rt.Get(slot, "ItemInstance"), "Name") as string ?? id) + " x" + Convert.ToInt32(Rt.Get(slot, "Quantity") ?? 1);
                float w = 560, x = Screen.width / 2f - w / 2, y = Screen.height - 150;
                Paint.Fill(new Rect(x, y, w, 34), new Color(0.07f, 0.04f, 0.12f, 0.85f));
                Paint.Fill(new Rect(x, y, 4, 34), new Color(0.27f, 0.72f, 0.29f));
                GUI.Label(new Rect(x + 14, y + 7, w - 20, 24), "Slot " + (_pick + 1) + ": " + name + "     LB / RB pick a slot  ·  D-pad down: drop it");
                return;
            }
            if (_menu || !_has || !Pad.InUse) return;
            GUI.Box(new Rect(Screen.width - 236, Screen.height - 120, 220, 26), "D-pad down (G): drop");
        }
    }
}
