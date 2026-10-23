using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Dropping what's in your hand with the controller: d-pad down (when no Frankenstein menu is open).
    /// Schedule I has no drop button of its own for the controller, so this calls the game's own drop, found by name
    /// on the hotbar (whatever this build calls it); what it found is written to the MelonLoader log once.</summary>
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
            if ((Pad.Pressed & Pad.Down) == 0 || ItemMenu.IsOpen || Wheel.Open || Pad.PanelsOpen) return;
            Now();
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
            if (_drop == null)
            {
                if (!_told) { _told = true; ItemMenu.Notify("This build of the game has no drop to call. Open the inventory and drag the item out."); }
                return;
            }
            try
            {
                var target = _on is UnityEngine.Object uo && uo == null ? Rt.All(Rt.FindByName("PlayerInventory")).FirstOrDefault() : _on;
                if (_drop.GetParameters().Length == 0) _drop.Invoke(_drop.DeclaringType.IsInstanceOfType(slot) ? slot : target, null);
                else _drop.Invoke(target, new[] { slot });
            }
            catch (Exception e) { ItemMenu.Notify("Couldn't drop it: " + (e.InnerException ?? e).Message); }
        }

        public static void Draw()
        {
            if (!Pad.InUse || !_has || ItemMenu.IsOpen || Wheel.Open || Pad.PanelsOpen) return;
            GUI.Box(new Rect(Screen.width - 236, Screen.height - 120, 220, 26), "D-pad down: drop");
        }
    }
}
