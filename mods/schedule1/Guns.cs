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
    /// replica's (damage, magazine, fire rate, reload, full auto) and its shape is changed to match (longer or
    /// shorter, bigger or smaller, and simple parts added: a curved mag, an extended mag, a scope, a drum).
    /// The list comes from Frankenstein's settings ("guns"); what each gun got is written to the MelonLoader log
    /// and to UserData/Frankenstein/guns-report.txt so it can be checked.</summary>
    internal static class Guns
    {
        class Spec
        {
            public string Match, Name, Parts = "";
            public double Damage = 1, Rate = 1, Reload = 1, Length = 1, Size = 1;
            public int Mag;
            public bool Auto, AutoSet;
        }

        static readonly List<Spec> _specs = new List<Spec>();
        static readonly HashSet<int> _done = new HashSet<int>();
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
                    Match = J.S(o, "match").ToLowerInvariant(), Name = J.S(o, "name"), Parts = J.S(o, "parts"),
                    Damage = J.N(o, "damage", 1), Rate = J.N(o, "rate", 1), Reload = J.N(o, "reload", 1),
                    Length = J.N(o, "length", 1), Size = J.N(o, "size", 1), Mag = (int)J.N(o, "mag", 0),
                };
                var auto = J.S(o, "auto");
                if (auto.Length > 0) { s.AutoSet = true; s.Auto = auto.ToLowerInvariant() == "true"; }
                if (s.Match.Length > 0) _specs.Add(s);
            }
        }

        public static void Update()
        {
            if (_specs.Count == 0 || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.4f;
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

        static void Apply(object w, GameObject go, Spec s)
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
            if (go != null && (Math.Abs(s.Length - 1) > 0.01 || Math.Abs(s.Size - 1) > 0.01 || s.Parts.Length > 0))
                did.Add(Shape(go, s));
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

        /// <summary>The replica's shape: stretched along the barrel (Length) and scaled overall (Size), then simple
        /// parts made from blocks in the gun's own material.</summary>
        static string Shape(GameObject go, Spec s)
        {
            var cam = Camera.main;
            var fwd = cam != null ? cam.transform.forward : go.transform.forward;
            var local = go.transform.InverseTransformDirection(fwd);
            int axis = Math.Abs(local.x) > Math.Abs(local.y) ? (Math.Abs(local.x) > Math.Abs(local.z) ? 0 : 2) : (Math.Abs(local.y) > Math.Abs(local.z) ? 1 : 2);
            var sc = go.transform.localScale * (float)s.Size;
            sc[axis] *= (float)s.Length;
            go.transform.localScale = sc;
            var rends = Rt.Comps(go, typeof(MeshRenderer), true).OfType<MeshRenderer>().Where(r => r != null).ToList();
            if (rends.Count == 0 || s.Parts.Length == 0) return "shape x" + s.Length.ToString("0.##") + " long, x" + s.Size.ToString("0.##") + " size";
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            var up = cam != null ? cam.transform.up : go.transform.up;
            var right = cam != null ? cam.transform.right : go.transform.right;
            float len = Math.Max(0.05f, Math.Abs(Vector3.Dot(b.size, new Vector3(Math.Abs(fwd.x), Math.Abs(fwd.y), Math.Abs(fwd.z)))));
            var mat = rends[0].sharedMaterial;
            var made = new List<string>();
            foreach (var part in s.Parts.Split(','))
            {
                var p = part.Trim().ToLowerInvariant();
                switch (p)
                {
                    case "curved_mag":
                        Block(go, mat, PrimitiveType.Cube, b.center + fwd * len * 0.12f - up * len * 0.16f, new Vector3(len * 0.07f, len * 0.3f, len * 0.11f),
                              Quaternion.LookRotation(fwd, up) * Quaternion.Euler(-18, 0, 0));
                        break;
                    case "ext_mag":
                        Block(go, mat, PrimitiveType.Cube, b.center - fwd * len * 0.18f - up * len * 0.42f, new Vector3(len * 0.12f, len * 0.36f, len * 0.16f),
                              Quaternion.LookRotation(fwd, up));
                        break;
                    case "scope":
                        Block(go, mat, PrimitiveType.Cylinder, b.center + up * (b.extents.y + len * 0.05f), new Vector3(len * 0.07f, len * 0.2f, len * 0.07f),
                              Quaternion.LookRotation(up, -fwd));
                        break;
                    case "drum":
                        Block(go, mat, PrimitiveType.Cylinder, b.center - up * len * 0.2f, new Vector3(len * 0.22f, len * 0.05f, len * 0.22f),
                              Quaternion.LookRotation(fwd, right));
                        break;
                    case "suppressor":
                        Block(go, mat, PrimitiveType.Cylinder, b.center + fwd * (len * 0.5f + len * 0.12f), new Vector3(len * 0.06f, len * 0.12f, len * 0.06f),
                              Quaternion.LookRotation(up, -fwd));
                        break;
                    default:
                        continue;
                }
                made.Add(p.Replace('_', ' '));
            }
            return "shape x" + s.Length.ToString("0.##") + " long, x" + s.Size.ToString("0.##") + " size" + (made.Count > 0 ? ", added " + string.Join(", ", made.ToArray()) : "");
        }

        static void Block(GameObject gun, Material mat, PrimitiveType shape, Vector3 at, Vector3 size, Quaternion rot)
        {
            var g = GameObject.CreatePrimitive(shape);
            g.name = "Frankenstein part";
            var col = Rt.Comp(g, Rt.Find("UnityEngine.Collider")) as UnityEngine.Object;
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            g.layer = gun.layer;
            g.transform.position = at;
            g.transform.rotation = rot;
            g.transform.localScale = size;
            g.transform.SetParent(gun.transform, true);
            var r = g.GetComponent<MeshRenderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
        }
    }
}
