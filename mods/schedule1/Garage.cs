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
    /// Each is one of the game's own drivable cars underneath (Cheetah, Hounddog, Bruiser), so it drives, parks and
    /// saves like any car, with its own paint, a body kit (splitter, skirts, wing, diffuser or spare wheel, light
    /// bar, grille, step bars), its own lights and more top speed. Which of your cars is which is kept per save
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
            new Style { Key = "toro", Name = "Toro Furioso", Base = "cheetah", Kind = "super", Kit = "super", Paint = new Color(0.98f, 0.78f, 0.02f),
                        Trim = new Color(0.05f, 0.05f, 0.05f), Lights = new Color(1f, 0.85f, 0.55f), Fallback = S1API.Vehicles.VehicleColor.Yellow,
                        Speed = 1.55f, Wing = 1.35f },
            new Style { Key = "notte", Name = "Notte GT", Base = "hounddog", Kind = "super", Kit = "super", Paint = new Color(0.32f, 0.1f, 0.56f),
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
            _next = Time.unscaledTime + 2f;
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

        /// <summary>The body kit, fitted to the car's own size (its model's bounds, in the car's own space).</summary>
        static void Kit(Component car, Style s, bool loaded)
        {
            if (StyleOf(car) != null) return;
            var go = car.gameObject;
            var filters = Rt.Comps(go, typeof(MeshFilter), true).OfType<MeshFilter>().Where(f => f != null && f.sharedMesh != null).ToList();
            if (filters.Count == 0) { Melon<Plugin>.Logger.Warning("Garage: the " + s.Base + " has no model to fit a kit to."); return; }
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = -min;
            foreach (var f in filters)
            {
                var b = f.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = car.transform.InverseTransformPoint(f.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
            var size = max - min;
            if (size.z < 1.5f || size.z > 12f) { Melon<Plugin>.Logger.Warning("Garage: the " + s.Base + "'s size looks wrong (" + size + "), no kit."); return; }
            var root = new GameObject(Marker + s.Key);
            root.transform.SetParent(car.transform, false);
            root.layer = go.layer;
            var body = Rt.Comps(go, typeof(MeshRenderer), true).OfType<MeshRenderer>().Where(r => r != null)
                         .OrderByDescending(r => r.bounds.size.sqrMagnitude).FirstOrDefault();
            var template = body != null ? body.sharedMaterial : null;
            float W = size.x, H = size.y, L = size.z, cx = (min.x + max.x) / 2, cz = (min.z + max.z) / 2, y0 = min.y;
            var parts = new List<Looks.Part>();
            var red = new Color(1f, 0.08f, 0.05f);
            if (s.Kit == "super")
            {
                float wing = s.Wing;
                parts.Add(P('c', cx, y0 + 0.14f * H, max.z - 0.04f, W * 0.96f, 0.035f, 0.18f, s.Trim, 0.8f));             // front splitter
                foreach (var side in new[] { -1f, 1f })
                {
                    parts.Add(P('c', cx + side * (W / 2 - 0.03f), y0 + 0.2f * H, cz, 0.07f, 0.07f, L * 0.52f, s.Trim, 0.8f));     // side skirt
                    parts.Add(P('c', cx + side * W * 0.3f, y0 + 0.66f * H, min.z + 0.1f * L, 0.04f, 0.16f * wing, 0.07f, s.Trim, 0.7f));  // wing post
                    parts.Add(P('c', cx + side * W * 0.3f, y0 + 0.46f * H, max.z - 0.015f, W * 0.24f, 0.035f, 0.03f, s.Lights, 1f, true));  // LED headlight
                    parts.Add(P('c', cx + side * W * 0.42f, y0 + 0.3f * H, max.z - 0.06f * L, 0.05f, 0.12f, 0.04f, s.Trim, 0.6f));     // intake
                }
                parts.Add(P('c', cx, y0 + 0.66f * H + 0.09f * wing, min.z + 0.1f * L, W * (0.82f + 0.08f * wing), 0.03f, 0.26f * wing, wing > 1.2f ? s.Trim : s.Paint, 0.9f));  // rear wing
                parts.Add(P('c', cx, y0 + 0.17f * H, min.z + 0.03f, W * 0.82f, 0.1f, 0.14f, s.Trim, 0.8f));               // diffuser
                for (int i = -2; i <= 2; i++) parts.Add(P('c', cx + i * W * 0.15f, y0 + 0.13f * H, min.z + 0.05f, 0.015f, 0.09f, 0.16f, s.Trim, 0.8f));  // its fins
                parts.Add(P('c', cx, y0 + 0.54f * H, min.z + 0.012f, W * 0.86f, 0.035f, 0.03f, red, 1f, true));             // tail light bar
                parts.Add(P('z', cx - W * 0.12f, y0 + 0.24f * H, min.z + 0.02f, 0.08f, 0.08f, 0.06f, new Color(0.7f, 0.7f, 0.72f), 1f));  // exhausts
                parts.Add(P('z', cx + W * 0.12f, y0 + 0.24f * H, min.z + 0.02f, 0.08f, 0.08f, 0.06f, new Color(0.7f, 0.7f, 0.72f), 1f));
            }
            else
            {
                parts.Add(P('z', cx, y0 + 0.5f * H, min.z - 0.1f, 0.72f, 0.72f, 0.24f, new Color(0.06f, 0.06f, 0.06f), 0.2f));  // spare wheel on the back
                parts.Add(P('z', cx, y0 + 0.5f * H, min.z - 0.225f, 0.42f, 0.42f, 0.02f, s.Trim, 0.9f));                    // its cover
                parts.Add(P('c', cx, max.y + 0.05f, max.z - 0.3f * L, W * 0.78f, 0.08f, 0.12f, new Color(0.05f, 0.05f, 0.05f), 0.5f));  // roof light bar
                parts.Add(P('c', cx, max.y + 0.05f, max.z - 0.3f * L + 0.065f, W * 0.72f, 0.04f, 0.01f, s.Lights, 1f, true));
                foreach (var side in new[] { -1f, 1f })
                {
                    parts.Add(P('c', cx + side * W * 0.38f, max.y + 0.03f, cz - 0.05f * L, 0.04f, 0.04f, L * 0.5f, s.Trim, 0.8f));     // roof rails
                    parts.Add(P('c', cx + side * (W / 2 + 0.02f), y0 + 0.17f * H, cz, 0.14f, 0.04f, L * 0.42f, s.Trim, 0.8f));     // step bars
                    parts.Add(P('z', cx + side * W * 0.37f, y0 + 0.5f * H, max.z + 0.005f, 0.22f, 0.22f, 0.04f, new Color(0.85f, 0.86f, 0.9f), 1f));  // round headlight ring
                    parts.Add(P('z', cx + side * W * 0.37f, y0 + 0.5f * H, max.z + 0.03f, 0.17f, 0.17f, 0.02f, s.Lights, 1f, true));  // and lamp
                    parts.Add(P('c', cx + side * W * 0.37f, y0 + 0.38f * H, max.z + 0.01f, 0.2f, 0.025f, 0.02f, new Color(1f, 0.6f, 0.1f), 1f, true));  // indicator
                    parts.Add(P('c', cx + side * (W / 2 - 0.02f), y0 + 0.62f * H, min.z + 0.01f, 0.08f, 0.22f, 0.03f, red, 1f, true));  // tail lights
                }
                for (int i = 0; i < 3; i++)
                    parts.Add(P('c', cx, y0 + (0.44f + i * 0.05f) * H, max.z + 0.01f, W * 0.42f, 0.025f, 0.03f, s.Trim, 1f));  // grille bars
                parts.Add(P('c', cx, y0 + 0.22f * H, max.z + 0.06f, W * 0.9f, 0.1f, 0.1f, new Color(0.07f, 0.07f, 0.07f), 0.4f));  // front bumper
            }
            int made = 0;
            foreach (var p in parts)
                if (Looks.Build(p, root.transform, template) != null) made++;
            if (s.Glow.a > 0)
            {
                try
                {
                    var lgo = new GameObject("underglow");
                    lgo.transform.SetParent(root.transform, false);
                    lgo.transform.localPosition = new Vector3(cx, y0 + 0.05f, cz);
                    var light = lgo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = s.Glow;
                    light.range = 4.5f;
                    light.intensity = 3f;
                }
                catch (Exception) { }
            }
            Tint(car, s.Paint);
            Melon<Plugin>.Logger.Msg("Garage: " + s.Name + (loaded ? " (from your save)" : "") + ": " + made + " kit parts on the " + s.Base + ", size " + size.ToString("0.0"));
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
