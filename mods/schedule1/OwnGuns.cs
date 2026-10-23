using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Frankenstein's own AK-47, Uzi, sniper and RPG, so they're there even when the More Guns mod isn't
    /// (it isn't made for every build of the game). Each works through one of the game's own guns (the M1911,
    /// revolver or pump shotgun) and gets the real gun's numbers: full auto, magazine, fire rate, damage; the RPG
    /// fires a rocket that explodes. Each is an item of its own (OwnItems: its own name and icon in the hotbar), and in
    /// your hands it has its own model (Looks), not the base gun's. Guns given before 3.38 were the base gun itself;
    /// the hotbar slot they went into still remembers which gun it is (UserData/Frankenstein/ownguns-&lt;save&gt;.json). Only offered when More Guns' own isn't loaded.</summary>
    internal static class OwnGuns
    {
        internal class Def
        {
            public string Key, Name, Base;
            public Guns.Spec Spec;
            public bool Rocket;
        }

        // Numbers are against the gun each is built on (the M1911 pistol, the revolver, the pump shotgun): the AK and
        // Uzi are fully automatic (the Uzi faster and weaker), the sniper slow with very high damage and reach, the
        // RPG one slow shot with a big blast. The grenade launcher's are in Boom.
        public static readonly Def[] Defs =
        {
            new Def { Key = "ak47", Name = "AK-47", Base = "m1911", Spec = new Guns.Spec { Name = "AK-47 (Frankenstein)", Damage = 1.6, Rate = 3.5, Mag = 30, Reload = 1.1, Range = 1.6, Spread = 0.8, Auto = true, AutoSet = true } },
            new Def { Key = "uzi", Name = "Uzi", Base = "m1911", Spec = new Guns.Spec { Name = "Uzi (Frankenstein)", Damage = 0.75, Rate = 5.5, Mag = 32, Reload = 0.8, Range = 0.9, Spread = 1.5, Auto = true, AutoSet = true } },
            new Def { Key = "sniper", Name = ".308 Sniper", Base = "revolver", Spec = new Guns.Spec { Name = ".308 Sniper (Frankenstein)", Damage = 4.5, Rate = 0.4, Mag = 5, Reload = 1.4, Range = 3, Spread = 0.15, Auto = false, AutoSet = true } },
            new Def { Key = "rpg", Name = "RPG", Base = "pumpshotgun", Rocket = true, Spec = new Guns.Spec { Name = "RPG (Frankenstein)", Damage = 1, Rate = 0.3, Mag = 1, Reload = 1.8, Range = 2, Spread = 0.3, Auto = false, AutoSet = true } },
        };

        /// <summary>True when More Guns' own guns loaded; then these aren't offered.</summary>
        public static bool MoreGunsLoaded;
        public static bool Holding;
        /// <summary>Which of these is in your hand (ak47, uzi, sniper, rpg), or null.</summary>
        public static string HeldKey;
        static readonly Dictionary<int, string> _slots = new Dictionary<int, string>();
        static Def _giving;
        static List<int> _before;
        static float _next, _nextGunLook;
        static object _held;
        static int _heldAmmo;
        static Type _gun;
        static string _file = "";

        static string File_ => Mode.DataFile("ownguns");

        public static string Source => MoreGunsLoaded ? "More Guns loaded: its AK, SMG, sniper and RPG are in the Weapons tab"
                                                      : "AK-47, Uzi, sniper, RPG and grenade launcher: Frankenstein's own, each with its own model";

        public static void Give(Def d)
        {
            var own = OwnItems.IdOf(d.Key);
            OwnItems.CheckGiven(d.Key, own);
            if (own != null)
            {
                // its own item: the hotbar shows the gun's own name and icon
                S1API.Console.ConsoleHelper.AddItemToInventory(own, 1);
                ItemMenu.Notify(d.Name + " added to your hotbar.");
                return;
            }
            var baseId = Story.ItemId(d.Base);
            if (baseId == null) { ItemMenu.Notify("This game has no " + d.Base + " to build the " + d.Name + " on."); return; }
            _giving = d;
            _before = Boom.Slots().Select((s, i) => Boom.Id(s) == baseId ? i : -1).Where(i => i >= 0).ToList();
            S1API.Console.ConsoleHelper.AddItemToInventory(baseId, 1);
            ItemMenu.Notify(d.Name + " added to your hotbar. Equip it: it's the " + d.Name + " in your hands.");
        }

        public static string NameOfSlot(int i) => _slots.TryGetValue(i, out var k) ? Defs.First(d => d.Key == k).Name : null;

        static void Load()
        {
            if (_file == File_) return;
            _file = File_;
            _slots.Clear();
            try
            {
                if (!File.Exists(_file)) return;
                foreach (var line in File.ReadAllLines(_file))
                {
                    var bits = line.Split('=');
                    if (bits.Length == 2 && int.TryParse(bits[0], out var i)) _slots[i] = bits[1];
                }
            }
            catch (Exception) { }
        }

        static void Store()
        {
            try { File.WriteAllLines(File_, _slots.Select(kv => kv.Key + "=" + kv.Value).ToArray()); } catch (Exception) { }
        }

        public static void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.15f;
            Load();
            var slots = Boom.Slots();
            if (slots.Count == 0) { Holding = false; HeldKey = null; return; }
            if (_giving != null && _before != null)
            {
                var baseId = Story.ItemId(_giving.Base);
                for (int i = 0; i < slots.Count; i++)
                    if (Boom.Id(slots[i]) == baseId && !_before.Contains(i)) { _slots[i] = _giving.Key; _giving = null; _before = null; Store(); break; }
            }
            // a slot that now holds something else isn't that gun any more
            foreach (var i in _slots.Keys.ToList())
            {
                var d = Defs.First(x => x.Key == _slots[i]);
                var id = i < slots.Count ? Boom.Id(slots[i]) : null;
                if (id != null && id != Story.ItemId(d.Base)) { _slots.Remove(i); Store(); }
            }
            Def held = null;
            int hi = Boom.EquippedIndex(slots);
            if (hi >= 0)
            {
                var key = OwnItems.KeyOf(Boom.Id(slots[hi]));  // its own item
                if (key == null && _slots.ContainsKey(hi)) key = _slots[hi];  // a base gun given before 3.38
                held = Defs.FirstOrDefault(x => x.Key == key);
            }
            Holding = held != null;
            HeldKey = held?.Key;
            if (!Holding) { _held = null; return; }
            if (_gun == null) _gun = Rt.FindByName("Equippable_RangedWeapon");
            if (_gun == null) return;
            if (_held == null || (_held is UnityEngine.Object h && h == null) || Time.unscaledTime >= _nextGunLook)
            {
                _nextGunLook = Time.unscaledTime + 1.5f;
                var gun = Looks.HeldGun(_gun);
                if (gun != null && !ReferenceEquals(gun, _held))
                {
                    _held = gun;
                    var uo = gun as UnityEngine.Object;
                    if (uo != null && Guns._done.Add(uo.GetInstanceID()))
                    {
                        try { Guns.Apply(gun, Rt.GameObjectOf(gun), held.Spec); }
                        catch (Exception e) { Melon<Plugin>.Logger.Warning("Own guns: " + held.Name + ": " + e.Message); }
                    }
                    _heldAmmo = Ammo(gun);
                    return;
                }
            }
            if (_held == null) return;
            var go = Rt.GameObjectOf(_held);
            try { Looks.Dress(go, held.Key, held.Base); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Looks: " + held.Name + ": " + e.Message); }
            if (!held.Rocket) return;
            int ammo = Ammo(_held);
            Looks.Warhead(go, ammo > 0);
            if (ammo < _heldAmmo) Boom.Throw(true, 1.7f);  // the RPG fired: a rocket goes with the shot, a bigger blast than a grenade
            _heldAmmo = ammo;
        }

        static int Ammo(object gun)
        {
            try { return Convert.ToInt32(Rt.Get(Rt.Get(gun, "weaponItem", "itemInstance", "ItemInstance"), "Value") ?? 0); }
            catch (Exception) { return 0; }
        }
    }
}
