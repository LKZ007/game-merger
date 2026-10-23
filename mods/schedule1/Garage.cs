using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Frankenstein's own cars in the F6 Vehicles tab: two supercars and a GT in the style of McLaren and
    /// Lamborghini, and a boxy luxury off-roader in the style of the G-Class (unbranded: the names are made up).
    /// Each has a whole new body (Body: a lofted mesh with its own profile, glass cabin, wheel arches) over one of
    /// the game's own drivable cars (Cheetah, Hounddog, Bruiser), whose body is hidden; its wheels, seats and driving
    /// stay, so it drives, parks and saves like any car. Plus its own lights, details and more top speed. Which of your cars is which is kept per save
    /// (UserData/Frankenstein/cars-&lt;save&gt;.json, by the car's id), so the kit comes back when you load.</summary>
    internal static class Garage
    {
        internal class Style
        {
            public string Key, Name, Base, Kind, Kit;
            public Color Paint, Trim, Lights, Glow;
            public S1API.Vehicles.VehicleColor Fallback;
            public float Speed = 1f, Wing = 1f;
        }

        public static readonly Style[] Styles =
        {
            new Style { Key = "velocci", Name = "Velocci 720", Base = "cheetah", Kind = "super", Kit = "super", Paint = new Color(1f, 0.42f, 0.04f),
                        Trim = new Color(0.06f, 0.06f, 0.07f), Lights = new Color(0.85f, 0.92f, 1f), Glow = new Color(1f, 0.45f, 0.1f),
                        Fallback = S1API.Vehicles.VehicleColor.Orange, Speed = 1.45f },
            new Style { Key = "toro", Name = "Toro Furioso", Base = "cheetah", Kind = "super", Kit = "wedge", Paint = new Color(0.98f, 0.78f, 0.02f),
                        Trim = new Color(0.05f, 0.05f, 0.05f), Lights = new Color(1f, 0.85f, 0.55f), Fallback = S1API.Vehicles.VehicleColor.Yellow,
                        Speed = 1.55f, Wing = 1.35f },
            new Style { Key = "notte", Name = "Notte GT", Base = "hounddog", Kind = "super", Kit = "gt", Paint = new Color(0.32f, 0.1f, 0.56f),
                        Trim = new Color(0.07f, 0.06f, 0.09f), Lights = new Color(0.8f, 0.7f, 1f), Glow = new Color(0.6f, 0.3f, 1f),
                        Fallback = S1API.Vehicles.VehicleColor.Purple, Speed = 1.4f, Wing = 0.8f },
            new Style { Key = "gelande", Name = "Gelände G63", Base = "bruiser", Kind = "gwagon", Kit = "gwagon", Paint = new Color(0.04f, 0.04f, 0.05f),
                        Trim = new Color(0.75f, 0.76f, 0.8f), Lights = new Color(0.9f, 0.95f, 1f), Fallback = S1API.Vehicles.VehicleColor.Black, Speed = 1.25f },
            new Style { Key = "gelande_desert", Name = "Gelände G Desert", Base = "bruiser", Kind = "gwagon", Kit = "gwagon", Paint = new Color(0.7f, 0.6f, 0.43f),
                        Trim = new Color(0.08f, 0.08f, 0.08f), Lights = new Color(1f, 0.9f, 0.7f), Fallback = S1API.Vehicles.VehicleColor.LightGrey, Speed = 1.2f },
        };

        const string Marker = "Frankenstein kit:";
        static readonly Dictionary<string, string> _cars = new Dictionary<string, string>();  // car id -> style
        static readonly List<KeyValuePair<object, Style>> _pending = new List<KeyValuePair<object, Style>>();
        static string _file = "";
        static float _next;

        static string File_ => Mode.DataFile("cars");

        public static Style StyleOf(Component car)
        {
            if (car == null) return null;
            for (int i = 0; i < car.transform.childCount; i++)
            {
                var n = car.transform.GetChild(i).name;
                if (n.StartsWith(Marker)) return Styles.FirstOrDefault(s => s.Key == n.Substring(Marker.Length));
            }
            return null;
        }

        /// <summary>Right after spawning: the paint and speed now, the body kit as soon as the car is in the world.</summary>
        public static void Restyle(S1API.Vehicles.LandVehicle v, Style s)
        {
            try { v.Color = s.Fallback; } catch (Exception e) { Melon<Plugin>.Logger.Warning("Garage: paint: " + e.Message); }
            try { v.TopSpeed = v.TopSpeed * s.Speed; } catch (Exception) { }
            _pending.Add(new KeyValuePair<object, Style>(v, s));
            _next = 0;
        }

        static void Load()
        {
            if (_file == File_) return;
            _file = File_;
            _cars.Clear();
            try
            {
                if (File.Exists(_file))
                    foreach (var line in File.ReadAllLines(_file))
                    {
                        var bits = line.Split('=');
                        if (bits.Length == 2) _cars[bits[0]] = bits[1];
                    }
            }
            catch (Exception) { }
        }

        static void Store()
        {
            try { File.WriteAllLines(File_, _cars.Select(kv => kv.Key + "=" + kv.Value).ToArray()); } catch (Exception) { }
        }

        static string Id(object v)
        {
            try { return (v as S1API.Vehicles.LandVehicle)?.GUID?.ToString() ?? ""; } catch (Exception) { return ""; }
        }

        /// <summary>Every couple of seconds: new cars get their kit, and your saved Frankenstein cars get theirs back.</summary>
        public static void Update()
        {
            if (Time.unscaledTime < _next || Player_() == null) return;
            _next = Time.unscaledTime + 1f;
            KeepHidden();
            Load();
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var id = Id(_pending[i].Key);
                var car = Rt.Get(_pending[i].Key, "S1LandVehicle") as Component;
                if (car == null) continue;
                Kit(car, _pending[i].Value, false);
                if (id.Length > 0) { _cars[id] = _pending[i].Value.Key; Store(); }
                _pending.RemoveAt(i);
            }
            if (_cars.Count == 0) return;
            S1API.Vehicles.LandVehicle[] all;
            try { all = S1API.Vehicles.VehicleRegistry.GetAll(); } catch (Exception) { return; }
            foreach (var v in all)
            {
                string key;
                if (v == null || !_cars.TryGetValue(Id(v), out key)) continue;
                var car = Rt.Get(v, "S1LandVehicle") as Component;
                var s = Styles.FirstOrDefault(x => x.Key == key);
                if (car == null || s == null || StyleOf(car) != null) continue;
                try { v.TopSpeed = v.TopSpeed * s.Speed; } catch (Exception) { }
                Kit(car, s, true);
            }
        }

        static object Player_() { try { return S1API.Entities.Player.Local; } catch (Exception) { return null; } }

        static Looks.Part P(char shape, float x, float y, float z, float sx, float sy, float sz, Color c, float shine = 0.5f, bool glow = false) =>
            new Looks.Part { Shape = shape, Pos = new Vector3(x, y, z), Size = new Vector3(sx, sy, sz), Color = c, Shine = shine, Glow = glow };

        /// <summary>The new body over the base car: its body hidden, its wheels kept, the new one fitted to them.</summary>
        static void Kit(Component car, Style s, bool loaded)
        {
            if (StyleOf(car) != null) return;
            var fit = Body.Measure(car);
            if (fit == null) { Melon<Plugin>.Logger.Warning("Garage: couldn't find the " + s.Base + "'s wheels, so it keeps its own body (painted)."); Tint(car, s.Paint); return; }
            var go = car.gameObject;
            var root = new GameObject(Marker + s.Key);
            root.transform.SetParent(car.transform, false);
            root.layer = go.layer;
            var renderers = Rt.Comps(go, typeof(MeshRenderer), true).OfType<Renderer>()
                              .Concat(Rt.Comps(go, typeof(SkinnedMeshRenderer), true).OfType<Renderer>())
                              .Where(r => r != null && r.enabled && !r.transform.IsChildOf(root.transform)).ToList();
            var template = renderers.OrderByDescending(r => r.bounds.size.sqrMagnitude).Select(r => r.sharedMaterial).FirstOrDefault();
            var glass = new Color(0.04f, 0.05f, 0.07f);
            var b = Body.Make(root.transform, fit, Body.Shapes[s.Kit], template, s.Paint, glass);
            if (b.Parts == 0) { UnityEngine.Object.Destroy(root); Tint(car, s.Paint); return; }
            // the base body goes; its wheels (and anything on them) stay. The game's own list of body panels goes
            // too, whatever their names, and they're hidden again if the game shows them (Update)
            var hide = new List<Renderer>();
            var log = new List<string> { s.Name + " on the " + s.Base + " (" + car.name + "): wheels at " + string.Join(" ", fit.Wheels.Select(w => w.ToString("0.00")).ToArray()) + ", r " + fit.R.ToString("0.00") };
            foreach (var r in renderers)
            {
                bool wheel = IsWheel(r, fit, car.transform);
                log.Add((wheel ? "  kept   " : "  hidden ") + Path_(r.transform, car.transform) + "  size " + r.bounds.size.ToString("0.00"));
                if (!wheel) hide.Add(r);
            }
            foreach (var bm in Creative.Each(Rt.Get(car, "BodyMeshes", "bodyMeshes")))
            {
                var r = Rt.Get(bm, "Renderer", "renderer", "Mesh") as Renderer;
                if (r != null && !hide.Contains(r)) { hide.Add(r); log.Add("  hidden (body panel) " + Path_(r.transform, car.transform)); }
            }
            foreach (var r in hide) r.enabled = false;
            _hidden[car.GetInstanceID()] = new KeyValuePair<Component, List<Renderer>>(car, hide);
            int hidden = hide.Count;
            Debug(log);
            var parts = Details(s, fit, b);
            foreach (var p in parts) Looks.Build(p, root.transform, template);
            if (s.Glow.a > 0)
            {
                try
                {
                    var lgo = new GameObject("underglow");
                    lgo.transform.SetParent(root.transform, false);
                    lgo.transform.localPosition = new Vector3(0, fit.Ground + 0.1f, (fit.FrontZ + fit.RearZ) / 2);
                    var light = lgo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = s.Glow;
                    light.range = 4.5f;
                    light.intensity = 3f;
                }
                catch (Exception) { }
            }
            Melon<Plugin>.Logger.Msg("Garage: " + s.Name + (loaded ? " (from your save)" : "") + ": new body (" + b.Parts + " meshes, " + parts.Count +
                                     " details) on the " + s.Base + ", " + hidden + " of its parts hidden, wheels kept.");
        }

        static readonly Dictionary<int, KeyValuePair<Component, List<Renderer>>> _hidden = new Dictionary<int, KeyValuePair<Component, List<Renderer>>>();
        static readonly List<string> _log = new List<string>();

        static string Path_(Transform t, Transform car)
        {
            var bits = new List<string>();
            for (; t != null && t != car; t = t.parent) bits.Insert(0, t.name);
            return string.Join("/", bits.ToArray());
        }

        static void Debug(List<string> lines)
        {
            _log.AddRange(lines);
            try
            {
                var dir = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Frankenstein");
                File.WriteAllLines(System.IO.Path.Combine(dir, "garage-debug.txt"), new[] { "Frankenstein " + Plugin.Version }.Concat(_log.Skip(Math.Max(0, _log.Count - 400))).ToArray());
            }
            catch (Exception) { }
        }

        /// <summary>A hidden base body stays hidden: some of the game's own code (paint, damage) shows it again.</summary>
        static void KeepHidden()
        {
            foreach (var id in _hidden.Keys.ToList())
            {
                var kv = _hidden[id];
                if (kv.Key == null) { _hidden.Remove(id); continue; }
                foreach (var r in kv.Value)
                    if (r != null && r.enabled) r.enabled = false;
            }
        }

        static readonly string[] WheelWords = { "wheel", "wheels", "tire", "tires", "tyre", "tyres", "rim", "rims", "caliper", "hub", "brake" };

        /// <summary>A wheel by its name (whole words only: "Trim" or "Primary" aren't rims) or by sitting on a wheel.</summary>
        static bool IsWheel(Renderer r, Body.Fit fit, Transform car)
        {
            for (var t = r.transform; t != null && t != car; t = t.parent)
            {
                var words = System.Text.RegularExpressions.Regex.Split(System.Text.RegularExpressions.Regex.Replace(t.name, "([a-z])([A-Z])", "$1 $2").ToLowerInvariant(), "[^a-z]+");
                if (words.Any(w => WheelWords.Contains(w) || w.StartsWith("wheel"))) return true;
            }
            var c = car.InverseTransformPoint(r.bounds.center);
            return fit.Wheels.Any(w => (w - c).magnitude < fit.R * 0.9f) && r.bounds.size.magnitude < fit.R * 4f;
        }

        /// <summary>Lights, intakes and the rest, placed on the new body's own surface.</summary>
        static List<Looks.Part> Details(Style s, Body.Fit f, Body.Built b)
        {
            var parts = new List<Looks.Part>();
            float k = f.K, nose = b.NoseZ, tail = b.TailZ;
            float wN = b.WidthAtZ(nose - 0.12f * k) / 2, wT = b.WidthAtZ(tail + 0.1f * k) / 2;
            var red = new Color(1f, 0.08f, 0.05f);
            var black = new Color(0.04f, 0.04f, 0.05f);
            Func<char, float, float, float, float, float, float, Color, float, bool, Looks.Part> P = (shape, x, y, z, sx, sy, sz, c, shine, glow) =>
                new Looks.Part { Shape = shape, Pos = new Vector3(x, y, z), Size = new Vector3(sx, sy, sz), Color = c, Shine = shine, Glow = glow };
            if (s.Kit != "gwagon")
            {
                float yN = b.BeltAtZ(nose - 0.2f * k) - 0.005f, yT = b.BeltAtZ(tail) - 0.1f * k;
                foreach (var side in new[] { -1f, 1f })
                {
                    // slim LED headlights swept back along the nose, and the tail lights
                    parts.Add(P('c', side * wN * 0.62f, yN, nose - 0.2f * k, wN * 0.55f, 0.035f * k, 0.22f * k, s.Lights, 1f, true));
                    parts.Add(P('c', side * wT * 0.6f, yT, tail - 0.005f, wT * 0.6f, 0.04f * k, 0.03f * k, red, 1f, true));
                    // side intake behind the door
                    float zi = f.Z(0.08f);
                    parts.Add(P('c', side * (b.WidthAtZ(zi) / 2 - 0.01f), f.Ground + (b.BeltAtZ(zi) - f.Ground) * 0.6f, zi, 0.04f, 0.18f * k, 0.5f * k, black, 0.4f, false));
                    // mirror
                    float zm = f.Z(Body.Shapes[s.Kit].C3 - 0.05f);
                    parts.Add(P('c', side * (b.WidthAtZ(zm) / 2 + 0.06f * k), b.BeltAtZ(zm) + 0.12f * k, zm, 0.14f * k, 0.07f * k, 0.1f * k, s.Paint, 0.85f, false));
                }
                parts.Add(P('c', 0, f.Ground + 0.12f * k, nose - 0.08f * k, wN * 1.7f, 0.03f * k, 0.2f * k, black, 0.6f, false));      // splitter
                parts.Add(P('c', 0, f.Ground + 0.2f * k, tail + 0.02f * k, wT * 1.5f, 0.1f * k, 0.18f * k, black, 0.6f, false));      // diffuser
                parts.Add(P('c', 0, yT, tail - 0.005f, wT * 1.2f, 0.025f * k, 0.03f * k, red, 1f, true));                          // light bar
                parts.Add(P('z', -0.12f * k, f.Ground + 0.26f * k, tail + 0.03f * k, 0.09f * k, 0.09f * k, 0.08f * k, new Color(0.7f, 0.7f, 0.72f), 1f, false));
                parts.Add(P('z', 0.12f * k, f.Ground + 0.26f * k, tail + 0.03f * k, 0.09f * k, 0.09f * k, 0.08f * k, new Color(0.7f, 0.7f, 0.72f), 1f, false));
                if (s.Kit == "super")  // a small ducktail lip in the body colour
                    parts.Add(P('c', 0, b.BeltAtZ(tail + 0.15f * k) + 0.03f * k, tail + 0.15f * k, wT * 1.6f, 0.03f * k, 0.18f * k, s.Paint, 0.85f, false));
            }
            else
            {
                float belt = b.BeltAtZ(nose), roof = b.RoofAtZ(f.Z(0.3f));
                foreach (var side in new[] { -1f, 1f })
                {
                    parts.Add(P('z', side * wN * 0.72f, belt - 0.16f * k, nose + 0.01f, 0.22f * k, 0.22f * k, 0.05f * k, new Color(0.85f, 0.86f, 0.9f), 1f, false));  // headlight ring
                    parts.Add(P('z', side * wN * 0.72f, belt - 0.16f * k, nose + 0.035f, 0.17f * k, 0.17f * k, 0.02f * k, s.Lights, 1f, true));                    // lamp
                    parts.Add(P('c', side * wN * 0.9f, belt - 0.03f * k, nose - 0.25f * k, 0.08f * k, 0.05f * k, 0.14f * k, new Color(1f, 0.6f, 0.1f), 1f, true));  // indicators on the wings
                    parts.Add(P('c', side * (wT - 0.03f), belt - 0.25f * k, tail - 0.005f, 0.1f * k, 0.3f * k, 0.03f * k, red, 1f, true));                       // tail lights
                    parts.Add(P('c', side * (b.WidthAtZ(f.Z(0.5f)) / 2 + 0.06f * k), f.Ground + 0.32f * k, f.Z(0.5f), 0.14f * k, 0.04f * k, (f.FrontZ - f.RearZ) * 0.6f, s.Trim, 0.8f, false));  // step bars
                    // roof rails: on the roof itself, inside its edges, and only as long as it is
                    var sh = Body.Shapes[s.Kit];
                    float rz0 = f.Z(sh.C1 + 0.06f), rz1 = f.Z(sh.C2 - 0.06f);
                    parts.Add(P('c', side * b.WidthAtZ((rz0 + rz1) / 2) * sh.CabinWidth * 0.42f, roof + 0.02f * k, (rz0 + rz1) / 2, 0.04f * k, 0.04f * k, rz1 - rz0, s.Trim, 0.8f, false));
                    parts.Add(P('c', side * (b.WidthAtZ(f.Z(0.45f)) / 2 + 0.02f), belt - 0.02f * k, f.Z(0.45f), 0.025f, 0.03f * k, (f.FrontZ - f.RearZ) * 1.1f, s.Trim, 0.9f, false));  // chrome belt strip
                }
                for (int i = 0; i < 3; i++)
                    parts.Add(P('c', 0, belt - (0.1f + i * 0.07f) * k, nose + 0.012f, wN * 0.8f, 0.025f * k, 0.03f * k, s.Trim, 1f, false));  // grille bars
                parts.Add(P('c', 0, f.Ground + 0.42f * k, nose + 0.06f, wN * 1.9f, 0.14f * k, 0.12f * k, black, 0.4f, false));                   // bumper
                parts.Add(P('c', 0, f.Ground + 0.42f * k, tail - 0.05f, wT * 1.9f, 0.14f * k, 0.1f * k, black, 0.4f, false));
                parts.Add(P('z', 0, belt + 0.05f * k, tail - 0.16f * k, 0.74f * k, 0.74f * k, 0.24f * k, black, 0.2f, false));                 // spare wheel
                parts.Add(P('z', 0, belt + 0.05f * k, tail - 0.285f * k, 0.44f * k, 0.44f * k, 0.02f * k, s.Trim, 0.9f, false));
                parts.Add(P('c', 0, roof + 0.07f * k, f.Z(0.66f), wN * 1.5f, 0.07f * k, 0.12f * k, black, 0.5f, false));                        // roof light bar
                parts.Add(P('c', 0, roof + 0.07f * k, f.Z(0.66f) + 0.065f * k, wN * 1.4f, 0.04f * k, 0.01f, s.Lights, 1f, true));
            }
            return parts;
        }

        /// <summary>The exact paint, over the closest of the game's own colours: the car's body panels only.</summary>
        static void Tint(Component car, Color c)
        {
            try
            {
                int n = 0;
                foreach (var bm in Creative.Each(Rt.Get(car, "BodyMeshes", "bodyMeshes")))
                {
                    var r = Rt.Get(bm, "Renderer", "renderer", "Mesh") as Renderer;
                    var idx = Convert.ToInt32(Rt.Get(bm, "MaterialIndex", "materialIndex") ?? 0);
                    if (r == null || idx != 0) continue;
                    var m = r.material;
                    m.color = c;
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
                    n++;
                }
                if (n == 0) Melon<Plugin>.Logger.Msg("Garage: body panels not found by name; the game's nearest paint stays.");
            }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Garage: paint: " + e.Message); }
        }

        public static Vehicles.Car Car(Style s) =>
            new Vehicles.Car { Code = s.Base, Name = s.Name, Kind = s.Kind, Color = s.Paint, Style = s };
    }
}
