using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Dropping with the controller. While you play: d-pad down drops what's in your hand. With the
    /// inventory or a storage rack open: LB and RB pick a hotbar slot (shown at the bottom), d-pad down drops that
    /// slot's items on the ground. Schedule I has no drop button of its own for the controller, so this calls the
    /// game's own drop, found by name on the inventory or the slot; what it found goes to the MelonLoader log once.
    /// A drop is a move: Creative doesn't put dropped items back.</summary>
    internal static class Drop
    {
        static MethodInfo _drop;
        static object _on;
        static bool _looked, _told;

        static bool _has;
        static float _next;

        public static void Update()
        {
            if (Pad.InUse && Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + 0.3f;
                try { var s = Equipped(out _); _has = s != null && Boom.Id(s) != null; } catch (Exception) { _has = false; }
            }
            if (S1API.Entities.Player.Local == null || MenuSkin.Active) return;  // only while you play
            if (ItemMenu.IsOpen || Wheel.Open || Pad.PanelsOpen) return;
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
                if ((Pad.Pressed & Pad.Down) != 0) Slot(_pick);
                return;
            }
            if ((Pad.Pressed & Pad.Down) == 0) return;
            Now();
        }

        static bool _menu;
        static int _pick;

        /// <summary>Drops one hotbar slot's items (with the inventory open).</summary>
        public static void Slot(int index)
        {
            var slots = Boom.Slots();
            if (index < 0 || index >= slots.Count || Boom.Id(slots[index]) == null) { ItemMenu.Notify("That slot is empty."); return; }
            Find(slots[index]);
            var slot = slots[index];
            Creative.Forget();
            try
            {
                if (_drop != null && _drop.GetParameters().Length == 1) { _drop.Invoke(Target(), new[] { slot }); return; }
                if (_drop != null && _drop.DeclaringType.IsInstanceOfType(slot)) { _drop.Invoke(slot, null); return; }
                // only "drop what's in your hand": hold that slot first, then drop it
                var inv = Target();
                var equip = inv?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => (m.Name == "Equip" || m.Name == "EquipSlot" || m.Name == "SetEquippedSlotIndex" || m.Name == "SetEquippedSlot") && m.GetParameters().Length == 1);
                if (_drop != null && equip != null)
                {
                    var p = equip.GetParameters()[0].ParameterType;
                    equip.Invoke(inv, new[] { p == typeof(int) ? (object)index : slot });
                    _drop.Invoke(_drop.DeclaringType.IsInstanceOfType(slot) ? slot : inv, null);
                    return;
                }
                ItemMenu.Notify("This build can't drop from the inventory with a controller. Hold the item and press d-pad down instead.");
            }
            catch (Exception e) { ItemMenu.Notify("Couldn't drop it: " + (e.InnerException ?? e).Message); }
        }

        static object Target()
        {
            var inv = Rt.All(Rt.FindByName("PlayerInventory")).FirstOrDefault();
            return _on is UnityEngine.Object uo && uo == null ? inv : (_on ?? inv);
        }

        static object Equipped(out int index)
        {
            index = -1;
            var slots = Boom.Slots();
            for (int i = 0; i < slots.Count; i++)
                if (Rt.Get(slots[i], "IsEquipped", "isEquipped") is bool on && on) { index = i; return slots[i]; }
            return null;
        }

        public static void Now()
        {
            var slot = Equipped(out var index);
            if (slot == null || Boom.Id(slot) == null) { ItemMenu.Notify("Nothing in your hand to drop."); return; }
            Find(slot);
            Creative.Forget();  // dropped on purpose: Creative doesn't put it back
            if (_drop == null)
            {
                if (!_told) { _told = true; ItemMenu.Notify("This build of the game has no drop to call. Open the inventory and drag the item out."); }
                return;
            }
            try
            {
                var target = Target();
                if (_drop.GetParameters().Length == 0) _drop.Invoke(_drop.DeclaringType.IsInstanceOfType(slot) ? slot : target, null);
                else _drop.Invoke(target, new[] { slot });
            }
            catch (Exception e) { ItemMenu.Notify("Couldn't drop it: " + (e.InnerException ?? e).Message); }
        }

        static void Find(object slot)
        {
            if (!_looked)
            {
                _looked = true;
                var inv = Rt.All(Rt.FindByName("PlayerInventory")).FirstOrDefault();
                foreach (var o in new[] { inv, slot })
                {
                    if (o == null || _drop != null) continue;
                    var ms = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                               .Where(m => m.Name.IndexOf("Drop", StringComparison.OrdinalIgnoreCase) >= 0 && !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                               .ToList();
                    Melon<Plugin>.Logger.Msg("Drop: " + o.GetType().Name + " has " + (ms.Count == 0 ? "no drop" : string.Join(", ", ms.Select(m => m.Name + "(" + m.GetParameters().Length + ")").ToArray())));
                    _drop = ms.FirstOrDefault(m => m.GetParameters().Length == 0) ??
                            ms.FirstOrDefault(m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(slot));
                    if (_drop != null) _on = o;
                }
            }
        }

        public static void Draw()
        {
            if (!Pad.InUse || ItemMenu.IsOpen || Wheel.Open || Pad.PanelsOpen || MenuSkin.Active || S1API.Entities.Player.Local == null) return;
            if (_menu)
            {
                var slots = Boom.Slots();
                if (slots.Count == 0) return;
                var slot = slots[Math.Min(_pick, slots.Count - 1)];
                var id = Boom.Id(slot);
                var name = id == null ? "empty" : (Rt.Get(Rt.Get(slot, "ItemInstance"), "Name") as string ?? id) + " x" + Convert.ToInt32(Rt.Get(slot, "Quantity") ?? 1);
                float w = 520, x = Screen.width / 2f - w / 2, y = Screen.height - 150;
                Paint.Fill(new Rect(x, y, w, 34), new Color(0.07f, 0.04f, 0.12f, 0.85f));
                Paint.Fill(new Rect(x, y, 4, 34), new Color(0.27f, 0.72f, 0.29f));
                GUI.Label(new Rect(x + 14, y + 7, w - 20, 24), "Slot " + (_pick + 1) + ": " + name + "     LB / RB pick a slot  ·  D-pad down: drop it");
                return;
            }
            if (!_has) return;
            GUI.Box(new Rect(Screen.width - 236, Screen.height - 120, 220, 26), "D-pad down: drop");
        }
    }
}
