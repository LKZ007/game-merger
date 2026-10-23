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
            public string Key, Name, Base, Kind, Kit, Category = "Custom";
            public Color Paint, Trim, Lights, Glow;
            public S1API.Vehicles.VehicleColor Fallback;
            public float Wing = 1f, Mph = 120f, ZeroSixty = 6f;
            public string[] Models = new string[0];  // the 3D models it wears, best first (names in the model catalogue)  // top speed and 0-60, as on levy's sheet (Drive keeps them)
        }

        static Style S(string key, string name, string bas, string kit, string cat, Color paint, S1API.Vehicles.VehicleColor fb, float mph, float z60, float glowA = 0)
        {
            bool box = kit == "gwagon" || kit == "suv";
            return new Style
            {
                Key = key, Name = name, Base = bas, Kit = kit, Kind = box ? "gwagon" : "super", Category = cat, Paint = paint, Fallback = fb, Mph = mph, ZeroSixty = z60,
                Trim = box ? new Color(0.75f, 0.76f, 0.8f) : new Color(0.05f, 0.05f, 0.06f), Lights = new Color(0.88f, 0.93f, 1f),
                Glow = glowA > 0 ? new Color(paint.r, paint.g, paint.b, glowA) : new Color(0, 0, 0, 0),
                Models = cat == "Supercars" ? new[] { "race-future", "race", "sedan-sports" }
                       : cat == "Sports Cars" ? new[] { "sedan-sports", "hatchback-sports", "race" }
                       : box ? (kit == "gwagon" ? new[] { "suv", "suv-luxury" } : new[] { "suv-luxury", "suv" })
                       : new[] { "sedan-sports", "sedan" },
            };
        }

        /// <summary>levy's twelve (unbranded lookalikes, made-up names) and the two earlier extras.</summary>
        public static readonly Style[] Styles =
        {
            S("velocci", "Velocci P-One", "cheetah", "super", "Supercars", new Color(1f, 0.42f, 0.04f), S1API.Vehicles.VehicleColor.Orange, 240, 2.4f, 1),
            S("toro", "Toro Furioso", "cheetah", "wedge", "Supercars", new Color(0.38f, 0.82f, 0.08f), S1API.Vehicles.VehicleColor.LightGreen, 217, 2.9f),
            S("rosso", "Rosso Novanta", "cheetah", "super", "Supercars", new Color(0.86f, 0.05f, 0.05f), S1API.Vehicles.VehicleColor.Red, 211, 2.5f),
            S("ventara", "Ventara Aria", "hounddog", "gt", "Supercars", new Color(0.5f, 0.12f, 0.78f), S1API.Vehicles.VehicleColor.Purple, 230, 3f, 1),
            S("uragano", "Toro Uragano", "cheetah", "wedge", "Supercars", new Color(0.08f, 0.32f, 0.92f), S1API.Vehicles.VehicleColor.DarkBlue, 202, 3.2f),
            S("vitesse", "Vitesse Centaur", "hounddog", "gt", "Supercars", new Color(0.06f, 0.14f, 0.38f), S1API.Vehicles.VehicleColor.Navy, 261, 2.3f, 1),
            S("stoccarda", "Stoccarda 9 Turbo", "hounddog", "sports", "Sports Cars", new Color(0.72f, 0.73f, 0.75f), S1API.Vehicles.VehicleColor.LightGrey, 205, 2.7f),
            S("ingolf", "Ingolf R-Ten", "cheetah", "super", "Sports Cars", new Color(0.92f, 0.92f, 0.93f), S1API.Vehicles.VehicleColor.White, 205, 3.1f),
            S("kentucky", "Kentucky Eight", "cheetah", "super", "Sports Cars", new Color(0.98f, 0.82f, 0.05f), S1API.Vehicles.VehicleColor.Yellow, 194, 2.9f),
            S("gelande", "Gelände G63", "bruiser", "gwagon", "SUVs", new Color(0.04f, 0.04f, 0.05f), S1API.Vehicles.VehicleColor.Black, 137, 4.5f),
            S("royale", "Spirit Royale", "bruiser", "suv", "SUVs", new Color(0.03f, 0.03f, 0.04f), S1API.Vehicles.VehicleColor.Black, 155, 4.8f),
            S("highland", "Highland Sport", "bruiser", "suv", "SUVs", new Color(0.9f, 0.9f, 0.9f), S1API.Vehicles.VehicleColor.White, 150, 5f),
            S("notte", "Notte GT", "hounddog", "gt", "Custom", new Color(0.32f, 0.1f, 0.56f), S1API.Vehicles.VehicleColor.Purple, 190, 3.6f, 1),
            S("gelande_desert", "Gelände G Desert", "bruiser", "gwagon", "Custom", new Color(0.7f, 0.6f, 0.43f), S1API.Vehicles.VehicleColor.LightGrey, 130, 5.2f),
        };

        /// <summary>The game counts top speed in km/h; the car must be allowed at least this one's own.</summary>
        static void Speed(S1API.Vehicles.LandVehicle v, Style s)
        {
            try
            {
                float was = v.TopSpeed;
                v.TopSpeed = Mathf.Max(was, s.Mph * 1.609f);
                Debug(new List<string> { s.Name + ": top speed " + was.ToString("0") + " -> " + v.TopSpeed.ToString("0") + " (" + s.Mph + " mph, 0-60 in " + s.ZeroSixty + " s)" });
            }
            catch (Exception) { }
        }

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
            Speed(v, s);
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
            _next = Time.unscaledTime + Perf.Every(1f);
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
                Speed(v, s);
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
            // a real 3D model over the base car: its body (and its wheels, when the model has its own) hidden,
            // its physics, seats and driving kept. Until the model packs are on this PC the car keeps its own body, painted.
            var entry = global::Frankenstein.Models.Pick("cars", s.Models);
            var model = global::Frankenstein.Models.Spawn(entry, car.transform.position, car.transform.rotation, root.transform);
            if (model == null)
            {
                UnityEngine.Object.Destroy(root);
                Tint(car, s.Paint);
                // while the model is still loading this runs again every second (Update), until it's on
                if (entry == null && _told.Add(s.Key)) Debug(new List<string> { s.Name + ": no car model on this PC yet (the app downloads the model packs), so it keeps the " + s.Base + "'s own body, painted" });
                return;
            }
            var log = new List<string> { s.Name + " on the " + s.Base + " (" + car.name + "): model " + entry.Name + " from " + entry.Pack + "; wheels at " + string.Join(" ", fit.Wheels.Select(w => w.ToString("0.00")).ToArray()) };
            int fitted = FitModel(model, car.transform, fit, log);
            bool ownWheels = Rt.Comps(model, typeof(Transform), true).OfType<Transform>().Any(t => t.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0);
            var hide = new List<Renderer>();
            foreach (var r in renderers)
            {
                bool keep = !ownWheels && IsWheel(r, fit, car.transform);
                log.Add((keep ? "  kept   " : "  hidden ") + Path_(r.transform, car.transform));
                if (!keep) hide.Add(r);
            }
            foreach (var bm in Creative.Each(Rt.Get(car, "BodyMeshes", "bodyMeshes")))
            {
                var r = Rt.Get(bm, "Renderer", "renderer", "Mesh") as Renderer;
                if (r != null && !hide.Contains(r)) hide.Add(r);
            }
            foreach (var r in hide) r.enabled = false;
            _hidden[car.GetInstanceID()] = new KeyValuePair<Component, List<Renderer>>(car, hide);
            int hidden = hide.Count;
            if (ownWheels) _spin[car.GetInstanceID()] = new KeyValuePair<Component, List<Transform>>(car, Rt.Comps(model, typeof(Transform), true).OfType<Transform>()
                .Where(t => t.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 && t.GetComponent<MeshFilter>() != null).ToList());
            Debug(log);
            Lights(root.transform, car.transform, fit);
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
            Melon<Plugin>.Logger.Msg("Garage: " + s.Name + (loaded ? " (from your save)" : "") + ": wears the " + entry.Name + " model (" + fitted + " meshes) on the " + s.Base + ", " + hidden + " of its parts hidden" + (ownWheels ? ", the model's own wheels turn" : ", its wheels kept") + ".");
        }

        static readonly HashSet<string> _told = new HashSet<string>();
        static readonly Dictionary<int, KeyValuePair<Component, List<Transform>>> _spin = new Dictionary<int, KeyValuePair<Component, List<Transform>>>();

        /// <summary>The model sized to the base car: as long as its wheelbase allows, centred on its wheels, standing on
        /// the ground under them, and turned to face the way the car does.</summary>
        static int FitModel(GameObject model, Transform car, Body.Fit fit, List<string> log)
        {
            var t = model.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            var b = global::Frankenstein.Models.LocalBounds(model, car);
            if (b.size.x > b.size.z * 1.2f) { t.localRotation = Quaternion.Euler(0, 90, 0); b = global::Frankenstein.Models.LocalBounds(model, car); }
            float wheelbase = fit.FrontZ - fit.RearZ;
            float want = wheelbase * 1.62f;  // overall length is about 1.6 wheelbases on most cars
            float k = b.size.z > 0.01f ? want / b.size.z : 1f;
            t.localScale = Vector3.one * k;
            b = global::Frankenstein.Models.LocalBounds(model, car);
            float midZ = (fit.FrontZ + fit.RearZ) / 2, midX = fit.Wheels.Average(w => w.x);
            t.localPosition += new Vector3(midX - b.center.x, fit.Ground - b.min.y, midZ - b.center.z);
            log.Add("  model scaled x" + k.ToString("0.00") + " to " + (b.size.z).ToString("0.00") + " m long (wheelbase " + wheelbase.ToString("0.00") + ")");
            return Rt.Comps(model, typeof(MeshFilter), true).Count;
        }

        /// <summary>Head and tail lights that really light the road, on any model.</summary>
        static void Lights(Transform root, Transform car, Body.Fit fit)
        {
            try
            {
                foreach (var front in new[] { true, false })
                {
                    var go = new GameObject(front ? "headlights" : "taillights");
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3(0, fit.Ground + fit.R * 1.6f, front ? fit.FrontZ + fit.R * 2.2f : fit.RearZ - fit.R * 2.2f);
                    go.transform.localRotation = Quaternion.Euler(8, front ? 0 : 180, 0);
                    var l = go.AddComponent<Light>();
                    l.type = front ? LightType.Spot : LightType.Point;
                    l.color = front ? new Color(1f, 0.96f, 0.88f) : new Color(1f, 0.08f, 0.05f);
                    l.range = front ? 32f : 3f;
                    if (front) l.spotAngle = 70f;
                    l.intensity = front ? 2.2f : 1.4f;
                }
            }
            catch (Exception) { }
        }

        /// <summary>A model's own wheels turn with the car's speed.</summary>
        public static void Spin()
        {
            foreach (var id in _spin.Keys.ToList())
            {
                var kv = _spin[id];
                if (kv.Key == null) { _spin.Remove(id); continue; }
                var rb = Rt.Comp(kv.Key.gameObject, Rt.Find("UnityEngine.Rigidbody"));
                var vel = Rt.Get(rb, "linearVelocity", "velocity");
                if (!(vel is Vector3 v)) continue;
                float speed = Vector3.Dot(v, kv.Key.transform.forward);
                float deg = speed / 0.35f * Mathf.Rad2Deg * Time.deltaTime;
                foreach (var w in kv.Value) if (w != null) w.Rotate(deg, 0, 0, Space.Self);
            }
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
            if (s.Kind != "gwagon")
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
                if (s.Kit == "gwagon")  // the off-roader's own: the luxury SUVs have neither
                {
                    parts.Add(P('z', 0, belt + 0.05f * k, tail - 0.16f * k, 0.74f * k, 0.74f * k, 0.24f * k, black, 0.2f, false));                 // spare wheel
                    parts.Add(P('z', 0, belt + 0.05f * k, tail - 0.285f * k, 0.44f * k, 0.44f * k, 0.02f * k, s.Trim, 0.9f, false));
                    parts.Add(P('c', 0, roof + 0.07f * k, f.Z(0.66f), wN * 1.5f, 0.07f * k, 0.12f * k, black, 0.5f, false));                        // roof light bar
                    parts.Add(P('c', 0, roof + 0.07f * k, f.Z(0.66f) + 0.065f * k, wN * 1.4f, 0.04f * k, 0.01f, s.Lights, 1f, true));
                }
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
            new Vehicles.Car { Code = s.Base, Name = s.Name, Kind = s.Kit == "gwagon" ? "gwagon" : s.Kit == "suv" ? "suv" : s.Kit == "gt" || s.Kit == "sports" ? "sports" : "super", Color = s.Paint, Style = s };
    }
}
