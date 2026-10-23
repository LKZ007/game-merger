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
    /// <summary>Frankenstein's own explosives, since neither Schedule I nor More Guns has a grenade launcher:
    /// frag grenades (T throws one; from the F6 menu) and a grenade launcher. The launcher works through the game's
    /// pump shotgun, with its own drum-launcher model (Looks): while that one gun is in your hand, every shot also fires a grenade
    /// that arcs, hits and explodes. Explosions hurt everyone near them except you (the game's own NPC health).
    /// Physics and lights are reached by name so the same mod works on both builds of the game.</summary>
    internal static class Boom
    {
        class Shell
        {
            public GameObject Go;
            public Vector3 V;
            public float Fuse, Power = 1f;  // Power: how big the blast is (the RPG's rocket 1.7, a grenade 1)
            public bool Stuck, Impact;
        }

        class Flash
        {
            public GameObject Go;
            public Light Light;
            public float Born, Size;
        }

        public static int Grenades;
        static bool _loaded;
        static readonly List<Shell> _live = new List<Shell>();
        static readonly List<Flash> _flashes = new List<Flash>();
        static int _launcherSlot = -1;
        static List<int> _before;
        static string _shotgun;
        static object _heldGun;
        static int _heldAmmo = -1;
        static Type _inv, _gun, _npc;
        static MethodInfo _linecast;
        static Material _shellMat, _fireMat;
        static bool _warned;
        const float Radius = 7f, Damage = 140f;

        static string Save => Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "explosives.txt");

        static float _nextTrack, _nextGunLook;
        static object _gunSeen;

        /// <summary>T, or Throw grenade on the controller wheel.</summary>
        public static void ThrowNow()
        {
            if (Grenades > 0 || MainMenu.Creative)
            {
                if (!MainMenu.Creative) { Grenades--; Store(); }
                Throw(false);
            }
            else ItemMenu.Notify("No grenades. Get them from the item menu (Weapons tab).");
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                var bits = File.ReadAllText(Save).Split(',');
                Grenades = int.Parse(bits[0]);
                if (bits.Length > 1) _launcherSlot = int.Parse(bits[1]);
            }
            catch (Exception) { }
        }

        static void Store()
        {
            try { File.WriteAllText(Save, Grenades + "," + _launcherSlot); } catch (Exception) { }
        }

        public static void GiveGrenades(int n)
        {
            Load();
            Grenades += n;
            Store();
            ItemMenu.Notify("Got " + n + " frag grenade" + (n == 1 ? "" : "s") + ". Press T to throw (" + Grenades + " now).");
        }

        /// <summary>The launcher: a pump shotgun, and Frankenstein remembers which hotbar slot it lands in.</summary>
        public static void GiveLauncher()
        {
            Load();
            _shotgun = _shotgun ?? Story.ItemId("pumpshotgun") ?? Story.ItemId("shotgun");
            var own = OwnItems.IdOf("launcher");
            OwnItems.CheckGiven("launcher", own);
            if (own != null)
            {
                S1API.Console.ConsoleHelper.AddItemToInventory(own, 1);
                ItemMenu.Notify("Got the Grenade Launcher (six-shot drum). Equip it and shoot: every shot is a grenade.");
                return;
            }
            if (_shotgun == null) { ItemMenu.Notify("This game has no shotgun to build the grenade launcher on."); return; }
            _before = Slots().Select((s, i) => Id(s) == _shotgun ? i : -1).Where(i => i >= 0).ToList();
            S1API.Console.ConsoleHelper.AddItemToInventory(_shotgun, 1);
            ItemMenu.Notify("Got the Grenade Launcher (six-shot drum). Equip it and shoot: every shot is a grenade.");
        }

        public static void Update()
        {
            Load();
            if (Time.unscaledTime >= _nextTrack)
            {
                _nextTrack = Time.unscaledTime + 0.12f;  // reading the hotbar every frame cost frames
                try { Track(); } catch (Exception e) { Warn("launcher", e); }
            }
            if (Input.GetKeyDown(KeyCode.T) && !ItemMenu.IsOpen && Time.timeScale > 0f) ThrowNow();
            float dt = Time.deltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                if (s.Go == null) { _live.RemoveAt(i); continue; }
                s.Fuse -= dt;
                if (!s.Stuck)
                {
                    var from = s.Go.transform.position;
                    s.V += Gravity * dt;
                    var to = from + s.V * dt;
                    if (Hits(from, to))
                    {
                        if (s.Impact) s.Fuse = 0f;  // launcher grenades go off on impact
                        else { s.Stuck = true; s.V = Vector3.zero; to = from; }
                    }
                    s.Go.transform.position = to;
                    s.Go.transform.Rotate(400f * dt, 120f * dt, 0f);
                }
                if (s.Fuse <= 0f)
                {
                    var at = s.Go.transform.position;
                    UnityEngine.Object.Destroy(s.Go);
                    _live.RemoveAt(i);
                    Explode(at, s.Power);
                }
            }
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var f = _flashes[i];
                float t = (Time.time - f.Born) / 0.55f;
                if (f.Go == null || t >= 1f)
                {
                    if (f.Go != null) UnityEngine.Object.Destroy(f.Go);
                    _flashes.RemoveAt(i);
                    continue;
                }
                f.Go.transform.localScale = Vector3.one * f.Size * (0.3f + 0.7f * Mathf.Sqrt(t));
                if (f.Light != null) f.Light.intensity = 30f * (1f - t);
            }
        }

        static readonly Guns.Spec LauncherSpec = new Guns.Spec { Name = "Grenade Launcher (Frankenstein)", Rate = 0.6, Spread = 0.5, Mag = 6, Reload = 1.6, Auto = false, AutoSet = true };

        static readonly Vector3 Gravity = new Vector3(0, -9.81f, 0);

        static void Warn(string what, Exception e)
        {
            if (_warned) return;
            _warned = true;
            Melon<Plugin>.Logger.Warning("Explosives (" + what + "): " + e.Message);
        }

        // ---------- the launcher: which slot it's in, and a grenade on every shot ----------

        internal static List<object> Slots()
        {
            if (_inv == null) _inv = Rt.FindByName("PlayerInventory");
            if (_inv == null) return new List<object>();
            var inv = Inventory();
            return Creative.Each(Rt.Get(inv, "hotbarSlots", "HotbarSlots")).ToList();
        }

        internal static string Id(object slot) => Rt.Get(Rt.Get(slot, "ItemInstance"), "ID") as string;

        static object _invFound;

        /// <summary>The player's inventory. Found once and kept: it's asked for several times a second, and
        /// searching every object in the game each time would cost frames.</summary>
        internal static object Inventory()
        {
            if (_invFound is UnityEngine.Object live && live != null) return _invFound;
            if (_inv == null) _inv = Rt.FindByName("PlayerInventory");
            if (_inv == null) return null;
            _invFound = Rt.Instance(_inv)
                        ?? Rt.All(_inv).FirstOrDefault();
            return _invFound;
        }

        static string _equipWay = "";
        static Type _hotbar;

        /// <summary>Which hotbar slot is in your hand (-1 for none). Builds differ, so every way is tried: the
        /// inventory's own index, its equipped slot, then each slot's IsEquipped (read as the hotbar slot it is).
        /// The way that worked is written to drop-debug.txt once.</summary>
        internal static int EquippedIndex(List<object> slots)
        {
            var inv = Inventory();
            if (inv == null || slots.Count == 0) return -1;
            var n = Rt.Get(inv, "EquippedSlotIndex", "equippedSlotIndex", "SelectedSlotIndex", "selectedSlotIndex", "CurrentSlotIndex");
            if (n != null)
            {
                int i;
                try { i = Convert.ToInt32(n); } catch (Exception) { i = -2; }
                if (i >= 0 && i < slots.Count) { Way("inventory index"); return i; }  // -1 may just be stale: the other ways still look
            }
            var eq = Rt.Get(inv, "equippedSlot", "EquippedSlot");
            if (eq != null)
            {
                var p = Ptr(eq);
                for (int i = 0; i < slots.Count; i++)
                    if (ReferenceEquals(slots[i], eq) || (p != IntPtr.Zero && Ptr(slots[i]) == p)) { Way("inventory's equipped slot"); return i; }
            }
            if (_hotbar == null) _hotbar = Rt.FindByName("HotbarSlot");
            for (int i = 0; i < slots.Count; i++)
            {
                var on = Rt.Get(slots[i], "IsEquipped", "isEquipped", "Equipped") ?? Rt.Get(As(slots[i], _hotbar), "IsEquipped", "isEquipped", "Equipped");
                if (on is bool b && b) { Way("slot IsEquipped"); return i; }
            }
            Way(eq == null && n == null ? "none found: the inventory has no equipped index or slot" : "nothing equipped");
            return -1;
        }

        internal static object EquippedSlot()
        {
            var slots = Slots();
            int i = EquippedIndex(slots);
            return i >= 0 ? slots[i] : null;
        }

        static void Way(string how)
        {
            if (_equipWay == how) return;
            _equipWay = how;
            Drop.Note("held slot found by: " + how);
        }

        static IntPtr Ptr(object o)
        {
            try { var v = o?.GetType().GetProperty("Pointer")?.GetValue(o, null); return v is IntPtr p ? p : IntPtr.Zero; } catch (Exception) { return IntPtr.Zero; }
        }

        /// <summary>An IL2CPP object read as one of its own subclasses (TryCast), or null.</summary>
        static object As(object o, Type t)
        {
            if (o == null || t == null || t.IsInstanceOfType(o)) return o;
            try
            {
                var m = o.GetType().GetMethods().FirstOrDefault(x => x.Name == "TryCast" && x.IsGenericMethodDefinition);
                return m?.MakeGenericMethod(t).Invoke(o, null);
            }
            catch (Exception) { return null; }
        }

        /// <summary>The grenade launcher is in your hand.</summary>
        public static bool LauncherHeld;

        static void Track()
        {
            LauncherHeld = false;
            var slots = Slots();
            if (_before != null && slots.Count > 0)
            {
                // the slot that just got a shotgun is the launcher's
                for (int i = 0; i < slots.Count; i++)
                    if (Id(slots[i]) == _shotgun && !_before.Contains(i)) { _launcherSlot = i; _before = null; Store(); break; }
            }
            // the launcher in your hand: its own item, or (given before 3.38) the shotgun in the slot it went into
            object slot = null;
            int held = EquippedIndex(slots);
            if (held >= 0 && OwnItems.KeyOf(Id(slots[held])) == "launcher") slot = slots[held];
            if (slot == null)
            {
                if (_launcherSlot < 0 || _launcherSlot >= slots.Count) { _heldGun = null; return; }
                slot = slots[_launcherSlot];
                var id = Id(slot);
                if (id == null || (_shotgun != null && id != _shotgun)) { if (id != null && _before == null) { _launcherSlot = -1; Store(); } return; }
                if (held != _launcherSlot) { _heldGun = null; return; }
            }
            LauncherHeld = true;
            if (_gun == null) _gun = Rt.FindByName("Equippable_RangedWeapon");
            // finding the gun searches every object in the game: only now and then, or when the old one is gone
            if (_gun != null && (_gunSeen == null || (_gunSeen is UnityEngine.Object g0 && g0 == null) || Time.unscaledTime >= _nextGunLook))
            {
                _nextGunLook = Time.unscaledTime + 1.5f;
                _gunSeen = Looks.HeldGun(_gun);
            }
            var gun = _gunSeen;
            var inst = Rt.Get(gun, "weaponItem", "itemInstance", "ItemInstance");
            if (gun == null || inst == null) return;
            try { Looks.Dress(Rt.GameObjectOf(gun), "launcher", "pumpshotgun"); } catch (Exception e) { Warn("launcher look", e); }
            int ammo = Convert.ToInt32(Rt.Get(inst, "Value") ?? 0);
            if (!ReferenceEquals(gun, _heldGun))
            {
                _heldGun = gun;
                _heldAmmo = ammo;
                // a slow six-shot drum, against the pump shotgun it's built on
                if (gun is UnityEngine.Object u && Guns._done.Add(u.GetInstanceID()))
                    try { Guns.Apply(gun, Rt.GameObjectOf(gun), LauncherSpec); } catch (Exception e) { Warn("launcher numbers", e); }
                return;
            }
            if (ammo < _heldAmmo) Throw(true);  // a shot went off: a grenade goes with it
            _heldAmmo = ammo;
        }

        // ---------- grenades in flight ----------

        internal static void Throw(bool launcher, float power = 1f)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Frankenstein grenade";
            var col = Rt.Comp(go, Rt.Find("UnityEngine.Collider")) as UnityEngine.Object;
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.localScale = launcher ? new Vector3(0.09f, 0.09f, 0.14f) : new Vector3(0.11f, 0.13f, 0.11f);
            go.transform.position = cam.transform.position + cam.transform.forward * 0.9f - cam.transform.up * 0.15f + cam.transform.right * 0.12f;
            Tint(go, ref _shellMat, launcher ? new Color(0.35f, 0.33f, 0.2f) : new Color(0.18f, 0.24f, 0.12f));
            _live.Add(new Shell
            {
                Go = go, Impact = launcher, Fuse = launcher ? 6f : 2.6f, Power = power,
                V = cam.transform.forward * (launcher ? 34f : 15f) + Vector3.up * (launcher ? 2.5f : 4f),
            });
        }

        static bool Hits(Vector3 from, Vector3 to)
        {
            if (_linecast == null)
            {
                var phys = Rt.Find("UnityEngine.Physics");
                _linecast = phys?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Linecast" && m.GetParameters().Length == 2);
                if (_linecast == null) return to.y < -50f;
            }
            try { return (bool)_linecast.Invoke(null, new object[] { from, to }); }
            catch (Exception) { return false; }
        }

        static void Tint(GameObject go, ref Material cache, Color c)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (r == null) return;
            if (cache == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                cache = sh != null ? new Material(sh) : new Material(r.sharedMaterial);
            }
            var m = new Material(cache);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            r.sharedMaterial = m;
        }

        // ---------- the explosion ----------

        static void Explode(Vector3 at, float power)
        {
            float radius = Radius * Mathf.Sqrt(power);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Frankenstein explosion";
            var col = Rt.Comp(go, Rt.Find("UnityEngine.Collider")) as UnityEngine.Object;
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.position = at;
            Tint(go, ref _fireMat, new Color(1f, 0.55f, 0.12f));
            Light light = null;
            try
            {
                light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.6f, 0.25f);
                light.range = radius * 3f;
                light.intensity = 30f * power;
            }
            catch (Exception) { }
            _flashes.Add(new Flash { Go = go, Light = light, Born = Time.time, Size = radius * 0.9f });
            int hurt = 0;
            try { hurt = HurtNear(at, radius, Damage * power); } catch (Exception e) { Warn("damage", e); }
            if (hurt > 0) Melon<Plugin>.Logger.Msg("Explosion hit " + hurt);
        }

        /// <summary>Everyone in the blast takes damage, the closest the most. You don't hurt yourself.</summary>
        static int HurtNear(Vector3 at, float radius, float damage)
        {
            if (_npc == null) _npc = Rt.Find("ScheduleOne.NPCs.NPC");
            if (_npc == null) return 0;
            int n = 0;
            foreach (var o in Rt.All(_npc))
            {
                var c = o as Component;
                if (c == null) continue;
                float d = Vector3.Distance(c.transform.position, at);
                if (d > radius) continue;
                float dmg = damage * (1f - d / radius * 0.75f);
                var health = Rt.Get(o, "Health", "health");
                if (Call(health, "TakeDamage", dmg, true) || Call(o, "TakeDamage", dmg, true)) n++;
            }
            return n;
        }

        internal static bool Call(object o, string method, params object[] args)
        {
            if (o == null) return false;
            var m = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                     .FirstOrDefault(x => x.Name == method && x.GetParameters().Length >= 1 &&
                                          x.GetParameters().Skip(args.Length).All(p => p.IsOptional));
            if (m == null) return false;
            var ps = m.GetParameters();
            var full = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
                full[i] = i < args.Length ? Convert.ChangeType(args[i], ps[i].ParameterType) : ps[i].DefaultValue;
            m.Invoke(o, full);
            return true;
        }

        public static void Draw()
        {
            if (Grenades <= 0 && _launcherSlot < 0 && !MainMenu.Creative) return;
            if (ItemMenu.IsOpen || Loading.Showing) return;
            var text = (MainMenu.Creative ? "Grenades: unlimited" : "Grenades: " + Grenades) + "  ·  T to throw" +
                       (_heldGun != null ? "  ·  Grenade launcher loaded" : "");
            if (Grenades <= 0 && !MainMenu.Creative && _heldGun == null) return;
            GUI.Label(new Rect(Screen.width - 330, Screen.height - 40, 320, 24), text);
        }
    }
}
