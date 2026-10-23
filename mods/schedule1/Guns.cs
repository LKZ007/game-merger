using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The Extension game's guns, rebuilt on Schedule I's: when you hold a gun, its numbers are set to the
    /// replica's (damage, magazine, fire rate, reload, full auto) and its model is sized to match (a little
    /// bigger or smaller, never stretched, so it stays in the hand).
    /// The list comes from Frankenstein's settings ("guns"); what each gun got is written to the MelonLoader log
    /// and to UserData/Frankenstein/guns-report.txt so it can be checked.</summary>
    internal static class Guns
    {
        internal class Spec
        {
            public string Match, Name;
            public double Damage = 1, Rate = 1, Reload = 1, Size = 1;
            public int Mag;
            public bool Auto, AutoSet;
        }

        static readonly List<Spec> _specs = new List<Spec>();
        internal static readonly HashSet<int> _done = new HashSet<int>();
        static readonly List<string> _report = new List<string>();
        static Type _weapon;
        static float _next;
        static bool _looked;

        public static void Read(object list)
        {
            foreach (var o in J.L(new Dictionary<string, object> { { "g", list } }, "g"))
            {
                var s = new Spec
                {
                    Match = J.S(o, "match").ToLowerInvariant(), Name = J.S(o, "name"),
                    Damage = J.N(o, "damage", 1), Rate = J.N(o, "rate", 1), Reload = J.N(o, "reload", 1),
                    Size = J.N(o, "size", 1), Mag = (int)J.N(o, "mag", 0),
                };
                var auto = J.S(o, "auto");
                if (auto.Length > 0) { s.AutoSet = true; s.Auto = auto.ToLowerInvariant() == "true"; }
                if (s.Match.Length > 0) _specs.Add(s);
            }
        }

        public static void Update()
        {
            if (OwnGuns.Holding) return;  // Frankenstein's own gun is in your hand: it has its own numbers
            if (_specs.Count == 0 || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;  // a full search of the game's objects: not too often
            if (_weapon == null)
            {
                if (_looked) return;
                _weapon = Rt.FindByName("Equippable_RangedWeapon");
                if (_weapon == null) { _looked = Time.unscaledTime > 120f; return; }
            }
            foreach (var w in Rt.All(_weapon))
            {
                var uo = w as UnityEngine.Object;
                if (uo == null) continue;
                int id = uo.GetInstanceID();
                if (_done.Contains(id)) continue;
                _done.Add(id);
                var go = Rt.GameObjectOf(w);
                var name = (go != null ? go.name : "").ToLowerInvariant().Replace(" ", "").Replace("_", "");
                var spec = _specs.FirstOrDefault(s => name.Contains(s.Match.Replace(" ", "").Replace("_", "")));
                if (spec == null) continue;
                try { Apply(w, go, spec); }
                catch (Exception e) { Melon<Plugin>.Logger.Warning("Guns: " + spec.Name + ": " + e.Message); }
            }
        }

        static double Num(object o) { try { return Convert.ToDouble(o); } catch (Exception) { return double.NaN; } }

        static string Scale(object w, double by, params string[] names)
        {
            foreach (var n in names)
            {
                var v = Num(Rt.Get(w, n));
                if (double.IsNaN(v) || v <= 0) continue;
                var set = Rt.Set(w, (float)(v * by), n);
                if (set != null) return set + " " + v.ToString("0.##") + "→" + (v * by).ToString("0.##");
            }
            return null;
        }

        internal static void Apply(object w, GameObject go, Spec s)
        {
            var did = new List<string>();
            if (Math.Abs(s.Damage - 1) > 0.01) did.Add(Scale(w, s.Damage, "Damage"));
            if (s.Mag > 0 && Rt.Set(w, s.Mag, "MagazineSize") != null) did.Add("MagazineSize " + s.Mag);
            // fire rate: the time between shots gets shorter as the rate goes up
            if (Math.Abs(s.Rate - 1) > 0.01)
                did.Add(Scale(w, 1 / s.Rate, "FireCooldown", "TimeBetweenShots", "ShotCooldown", "FireInterval", "Cooldown") ??
                        Scale(w, s.Rate, "FireRate", "RateOfFire", "RoundsPerMinute"));
            if (Math.Abs(s.Reload - 1) > 0.01) did.Add(Scale(w, s.Reload, "ReloadTime", "ReloadDuration", "ReloadLength"));
            if (s.AutoSet)
            {
                var n = Rt.Set(w, s.Auto, "IsAutomatic", "Automatic", "FullAuto", "isAutomatic");
                if (n != null) did.Add(n + " " + s.Auto);
            }
            if (go != null) did.Add(Shape(go, s));
            var line = s.Name + ": " + string.Join(", ", did.Where(x => x != null).ToArray());
            if (did.All(x => x == null)) line = s.Name + ": none of its numbers could be changed in this build of the game";
            Melon<Plugin>.Logger.Msg("Guns: " + line);
            if (!_report.Contains(line))
            {
                _report.Add(line);
                try { File.WriteAllLines(Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "guns-report.txt"), _report.ToArray()); }
                catch (Exception) { }
            }
        }

        /// <summary>The replica's size. Only the gun's own model is scaled, evenly and by a little (85% to 115%), around
        /// its own centre: the gun stays in the hand, the grip where the game put it. (Until 3.28 the whole held
        /// object was stretched along the barrel and blocks were stuck on in world space, which bent the grip out
        /// of the hand and left a part floating beside the gun.) Any blocks an older version added are removed.</summary>
        static string Shape(GameObject go, Spec s)
        {
            int removed = 0;
            foreach (var t in Rt.Comps(go, typeof(Transform), true).OfType<Transform>().ToList())
                if (t != null && t.name == "Frankenstein part") { UnityEngine.Object.Destroy(t.gameObject); removed++; }
            float k = Mathf.Clamp((float)s.Size, 0.85f, 1.15f);
            if (Math.Abs(k - 1f) < 0.01f) return removed > 0 ? "removed " + removed + " old part(s)" : null;
            int n = 0;
            foreach (var r in Rt.Comps(go, typeof(MeshRenderer), true).OfType<MeshRenderer>())
            {
                if (r == null || r.transform == go.transform) continue;
                var t = r.transform;
                var before = r.bounds.center;
                t.localScale = t.localScale * k;
                t.position += before - r.bounds.center;  // grows or shrinks around its own centre, not its pivot
                n++;
            }
            return n > 0 ? "model x" + k.ToString("0.##") + " size" : null;
        }
    }
}
