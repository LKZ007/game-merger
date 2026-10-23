using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>EXPERIMENTAL: a whole city island off the east coast of Schedule I's town, laid out from levy's map
    /// (mods/_art/hoodwarfare/ref-map.jpg, 1448 x 1086 px, 0.9 m a pixel, so about 1.3 x 1 km). A highway bridge
    /// runs from the edge of town over the water to it. On it: Downtown's towers, Uptown Hills' mansions, Lake
    /// Park, the Suburbs, the Airport, Shopping &amp; Nightlife, the Construction Zone, the Trailer Park, the Farms,
    /// the Port and the docks island, and every gang's HQ where the map has it (London, Mafia, Crips, Bloods,
    /// Yakuza, the Cartel's point with its tanks, and the Outlaw MC up on the cliff with a lighthouse).
    ///
    /// Built from the town's own buildings, props and materials where they fit, and from simple shapes where the
    /// game has nothing (towers, cranes, planes, ships, containers). Built again the same way every session (fixed
    /// seeds), so nothing goes into the save. To keep the game smooth it's built one district a frame, each
    /// district's pieces are batched together, and districts far from you are switched off (nearer still when
    /// Perf turns extras down). Fall in the water and you're put back on the nearest road.
    ///
    /// Limits, said plainly: there are no real skyscraper models in Schedule I, so the towers are blocks with
    /// lit windows; copied buildings are shells with no insides; and the game's townspeople have no walking paths
    /// there, so the only people on the island are the gangs' own crews, who hold their HQs.
    /// F4 (or F8 > City map) opens the map: you on it, click a district to go there.</summary>
    internal static class Expansion
    {
        public const float PW = 1448f, PH = 1086f, S = 0.9f;  // the map picture, and metres a pixel

        public class District
        {
            public string Name, Kind, Gang = "";
            public Rect Px;  // on the map picture
            public float Lift;
            public GameObject Root;
            public bool Done;
            public Vector3 Centre;
            public District(string name, string kind, string gang, float x, float y, float w, float h, float lift = 0)
            {
                Name = name; Kind = kind; Gang = gang; Px = new Rect(x, y, w, h); Lift = lift;
            }
        }

        /// <summary>The districts as levy's map has them (pixels on the picture).</summary>
        public static readonly District[] Plan =
        {
            new District("Uptown Hills", "hills", "", 60, 60, 330, 200),
            new District("Downtown", "towers", "", 450, 60, 280, 230),
            new District("Lake Park", "park", "", 730, 110, 190, 140),
            new District("Suburbs", "suburbs", "", 930, 60, 300, 190),
            new District("Airport", "airport", "", 1100, 170, 330, 220),
            new District("London HQ", "hq", "london", 290, 280, 140, 90),
            new District("Shopping & Nightlife", "nightlife", "", 610, 300, 200, 90),
            new District("Mafia HQ", "hq", "mafia", 420, 390, 170, 130),
            new District("Construction Zone", "construction", "", 720, 390, 200, 150),
            new District("Trailer Park", "trailers", "eastlondon", 960, 390, 190, 150),
            new District("Farms", "farms", "", 1230, 450, 200, 220),
            new District("Port", "port", "", 30, 450, 340, 380),
            new District("South Docks", "port", "", 160, 830, 280, 180),
            new District("Crips HQ", "hq", "crips", 510, 570, 150, 110),
            new District("Yakuza HQ", "hq", "yakuza", 760, 610, 160, 120),
            new District("Bloods HQ", "hq", "bloods", 580, 690, 160, 110),
            new District("Cartel Point", "cartel", "cartel", 780, 800, 260, 170),
            new District("Outlaw Bluff", "cliff", "outlaw", 1110, 760, 300, 260, 10f),
        };

        /// <summary>The land, as rectangles on the picture (lift: metres above the town's ground).</summary>
        static readonly float[][] Land =
        {
            new float[] { 20, 20, 1410, 400, 0 },     // the north: hills, downtown, park, suburbs, airport
            new float[] { 400, 380, 1040, 330, 0 },   // the middle
            new float[] { 470, 680, 580, 300, 0 },    // the south and the Cartel's point
            new float[] { 20, 450, 350, 380, 0 },     // the port
            new float[] { 150, 830, 290, 180, 0 },    // the docks island
            new float[] { 1090, 700, 350, 340, 10 },  // the Outlaw's cliff
        };

        /// <summary>Roads on the picture: x1, y1, x2, y2, width in metres (16 is the highway).</summary>
        static readonly float[][] Roads =
        {
            new float[] { 20, 455, 420, 430, 16 }, new float[] { 420, 430, 900, 350, 16 }, new float[] { 900, 350, 1100, 300, 16 },
            new float[] { 1100, 300, 1420, 200, 16 }, new float[] { 700, 250, 740, 600, 16 }, new float[] { 740, 600, 735, 1000, 16 },
            new float[] { 230, 300, 420, 430, 10 }, new float[] { 120, 160, 230, 300, 10 }, new float[] { 900, 350, 1000, 250, 10 },
            new float[] { 1000, 250, 1100, 150, 10 }, new float[] { 300, 600, 560, 600, 10 }, new float[] { 300, 720, 560, 710, 10 },
            new float[] { 320, 900, 620, 830, 10 }, new float[] { 620, 830, 735, 820, 10 }, new float[] { 740, 600, 960, 560, 10 },
            new float[] { 960, 560, 1180, 620, 10 }, new float[] { 1180, 620, 1250, 760, 10 }, new float[] { 1250, 760, 1260, 880, 10 },
            new float[] { 735, 820, 900, 880, 10 }, new float[] { 420, 430, 500, 560, 10 }, new float[] { 500, 560, 740, 600, 10 },
            new float[] { 900, 350, 940, 470, 10 }, new float[] { 940, 470, 960, 560, 10 }, new float[] { 1180, 620, 1320, 560, 10 },
            new float[] { 560, 150, 700, 250, 10 }, new float[] { 700, 250, 820, 300, 10 },
        };

        static readonly List<GameObject> _made = new List<GameObject>();
        static readonly List<Vector3> _roadPoints = new List<Vector3>();
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        static GameObject _base;
        static Transform _parent;
        static Vector3 _origin;
        static float _ground, _water, _buildAt = -1, _nextStream, _nextRescue;
        static int _step = -1, _pieces;
        static bool _built, _warned, _ownSea;
        static string _here = "";
        static float _hereUntil;
        static Material _baseMat, _groundMat, _roadMat;
        static List<GameObject> _shells = new List<GameObject>(), _small = new List<GameObject>(), _trees = new List<GameObject>(), _props = new List<GameObject>();
        static Texture2D _windows;
        static Vector3 _townEdge;
        public static string Note = "";

        static readonly List<string> _log = new List<string>();

        /// <summary>What the island did, for island-debug.txt (and the town's measured size, to compare).</summary>
        static void Say(string line)
        {
            Melon<Plugin>.Logger.Msg("Expansion: " + line);
            _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
            try
            {
                var dir = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Frankenstein");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "island-debug.txt"), new[] { "Frankenstein " + Plugin.Version }.Concat(_log.Skip(Math.Max(0, _log.Count - 200))).ToArray());
            }
            catch (Exception) { }
        }

        /// <summary>How far along the island is, for the menus.</summary>
        public static string Status => _built ? "ready" : _step >= 0 ? "being built (" + (_step * 100 / Plan.Length) + "%)" : Note.Length > 0 ? Note : _buildAt > 0 ? "starts building a few seconds after the town loads" : "not started";

        /// <summary>Straight to the island (Downtown), from the wheel, F6 or F8.</summary>
        public static void GoToIsland()
        {
            if (!On) { ItemMenu.Notify("This merge has no city island."); return; }
            if (!_built) { ItemMenu.Notify("The city island is " + Status + "."); return; }
            Travel(1);
        }

        public static bool On => Gangs.On;
        public static bool Built => _built;
        public static int Count => _built ? Plan.Length : 0;
        public static float Water => _water;

        public static void Scene(bool inTown)
        {
            _built = false;
            _step = -1;
            _made.Clear();
            _roadPoints.Clear();
            _mats.Clear();
            _base = null;
            _jobs.Clear();
            _pieces = 0;
            foreach (var d in Plan) { d.Root = null; d.Done = false; }
            _buildAt = inTown && On ? Time.unscaledTime + 25f : -1;  // after the town and its props exist
        }

        // ---------- the picture and the world ----------

        public static Vector3 ToWorld(float px, float py, float h) => new Vector3(_origin.x + px * S, h, _origin.z - py * S);
        public static Vector2 ToPx(Vector3 w) => new Vector2((w.x - _origin.x) / S, (_origin.z - w.z) / S);

        static bool OnLand(Vector2 p, out float lift)
        {
            lift = 0;
            bool any = false;
            foreach (var l in Land)
                if (p.x >= l[0] && p.x <= l[0] + l[2] && p.y >= l[1] && p.y <= l[1] + l[3]) { lift = Mathf.Max(lift, l[4]); any = true; }
            return any;
        }

        static float HeightAt(Vector3 w)
        {
            if (w.x < _origin.x) return _ground;  // the town's side
            float lift;
            OnLand(ToPx(w), out lift);
            return _ground + lift;
        }

        static bool OverWater(Vector3 w)
        {
            if (w.x < _origin.x + 10f) return w.x > _townEdge.x + 15f;
            float lift;
            return !OnLand(ToPx(w), out lift);
        }

        /// <summary>The district you're in on the island, or null.</summary>
        public static District At(Vector3 w)
        {
            if (!_built) return null;
            var p = ToPx(w);
            District best = null;
            foreach (var d in Plan)
                if (d.Px.Contains(p) && (best == null || d.Px.width * d.Px.height < best.Px.width * best.Px.height)) best = d;
            return best;
        }

        /// <summary>For the gangs: the island gang whose own district this is (the Cartel's point, the Outlaw's bluff).</summary>
        public static string RegionAt(Vector3 w)
        {
            var d = At(w);
            return d != null && d.Gang.Length > 0 && Gangs.RegionOf(d.Gang) == d.Name ? d.Name : "";
        }

        // ---------- every frame ----------

        public static void Update()
        {
            if (_buildAt > 0 && Time.unscaledTime >= _buildAt)
            {
                _buildAt = -1;
                try { Begin(); }
                catch (Exception e) { Note = "The island couldn't be built: " + e.Message; Say(Note + "\n" + e); }
            }
            if (_step >= 0)
            {
                // one district a frame, so building it never freezes the game
                try { Next(); }
                catch (Exception e) { Melon<Plugin>.Logger.Warning("Expansion (" + (_step < Plan.Length ? Plan[_step].Name : "?") + "): " + e.Message); _parent = null; _step++; }
            }
            if (!_built) return;
            var me = Player.Local;
            if (me == null) return;
            Stream(me.Position);
            Rescue(me);
            var d = At(me.Position);
            var name = d != null ? d.Name : "";
            if (name != _here)
            {
                _here = name;
                if (d != null)
                {
                    _hereUntil = Time.unscaledTime + 4f;
                    ItemMenu.Notify("Entering " + d.Name + (d.Gang.Length > 0 ? ": " + Gangs.NameOf(d.Gang) + " turf" : ""));
                }
            }
        }

        /// <summary>Districts far from you are switched off: nothing there is drawn or checked.</summary>
        static void Stream(Vector3 me)
        {
            if (Time.unscaledTime < _nextStream) return;
            _nextStream = Time.unscaledTime + Perf.Every(1f);
            float reach = Perf.Level == 0 ? 450f : Perf.Level == 1 ? 330f : 220f;
            foreach (var d in Plan)
            {
                if (d.Root == null) continue;
                var c = d.Centre;
                float half = Mathf.Max(d.Px.width, d.Px.height) * S / 2;
                float dist = Vector2.Distance(new Vector2(me.x, me.z), new Vector2(c.x, c.z)) - half;
                // Downtown's towers are the skyline: seen from further away
                bool on = dist < (d.Kind == "towers" ? reach * 2f : reach);
                if (d.Root.activeSelf != on) d.Root.SetActive(on);
            }
        }

        /// <summary>In the water off the island: back on the nearest road.</summary>
        static void Rescue(Player me)
        {
            if (Time.unscaledTime < _nextRescue || _roadPoints.Count == 0) return;
            _nextRescue = Time.unscaledTime + 0.5f;
            var p = me.Position;
            if (p.x < _townEdge.x || p.y > _water - 1.2f) return;
            try { if (me.IsInVehicle) return; } catch (Exception) { }
            var best = _roadPoints.OrderBy(r => (r - p).sqrMagnitude).First();
            try { me.Position = best + new Vector3(0, 1.5f, 0); ItemMenu.Notify("Out of the water: back on the road."); } catch (Exception) { }
        }

        // ---------- what the town gives us ----------

        static bool Huge(Bounds b) => b.size.x > 600 || b.size.z > 600;

        static Material Pick(List<MeshRenderer> all, params string[] words)
        {
            foreach (var w in words)
                foreach (var r in all)
                {
                    var m = r.sharedMaterial;
                    if (m != null && (m.name.ToLowerInvariant().Contains(w) || r.gameObject.name.ToLowerInvariant().Contains(w))) return m;
                }
            return all.Count > 0 ? all[0].sharedMaterial : null;
        }

        static void Begin()
        {
            var all = Rt.All<MeshRenderer>().Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy).ToList();
            if (all.Count < 50) { _buildAt = Time.unscaledTime + 10f; return; }  // not loaded yet
            var town = new Bounds();
            bool first = true;
            var roads = new List<MeshRenderer>();
            float sea = float.NaN;
            var seaBounds = new Bounds();
            foreach (var r in all)
            {
                var b = r.bounds;
                var n = r.gameObject.name.ToLowerInvariant();
                if (Huge(b))
                {
                    if (n.Contains("ocean") || n.Contains("water") || n.Contains("sea")) { sea = b.max.y; seaBounds = b; }
                    continue;
                }
                if (first) { town = b; first = false; } else town.Encapsulate(b);
                if (n.Contains("road")) roads.Add(r);
            }
            _ground = roads.Count > 0 ? roads.Select(r => r.bounds.max.y).OrderBy(y => y).ElementAt(roads.Count / 2)
                                      : (Player.Local != null ? Player.Local.Position.y - 1f : town.min.y);
            _ownSea = float.IsNaN(sea) || sea > _ground - 0.5f;
            _water = _ownSea ? _ground - 2.5f : sea;
            _groundMat = Pick(all, "grass", "ground", "sidewalk", "concrete", "pavement");
            _roadMat = Pick(roads.Count > 0 ? roads : all, "road", "asphalt", "tarmac");
            _baseMat = Pick(all, "concrete", "plaster", "wall", "metal") ?? _groundMat;
            Shells(all);
            _props = World.Kinds();
            _trees = all.Where(r => { var n = r.gameObject.name.ToLowerInvariant(); var b = r.bounds; return (n.Contains("tree") || n.Contains("pine") || n.Contains("oak")) && b.size.y > 3 && b.size.y < 25; })
                        .GroupBy(r => r.gameObject.name).Select(g => g.First().gameObject).Take(6).ToList();
            // the island starts across a channel east of town, its highway in line with the town's middle
            _townEdge = new Vector3(town.max.x, _ground, town.center.z);
            Say("the town measures " + town.size.x.ToString("0") + " x " + town.size.z.ToString("0") + " m (" + (town.size.x * town.size.z / 1e6f).ToString("0.00") +
                " km2 as a rectangle); the island's land is about 1.07 km2. Its west shore is at x " + (town.max.x + 90f + 20f * S).ToString("0") + ", the town's east edge at x " + town.max.x.ToString("0"));
            _origin = new Vector3(town.max.x + 90f, _ground, town.center.z + 455f * S);
            Clear(seaBounds, sea);
            foreach (var d in Plan) d.Centre = ToWorld(d.Px.center.x, d.Px.center.y, _ground + d.Lift);
            _windows = Windows();
            BuildBase();
            _step = 0;
            Say("building the island (" + _shells.Count + " building shapes, " + _trees.Count + " trees, ground at " + _ground.ToString("0.#") +
                                     ", water at " + _water.ToString("0.#") + (_ownSea ? " (Frankenstein's)" : " (the game's)") + ").");
        }

        /// <summary>Schedule I's town sits in hills and mountains (Unity terrain, which isn't counted in the town's
        /// size). If the terrain rises over where the island would be, the island goes past the terrain's edge so it
        /// isn't buried; the bridge is longer then. Where the game's own sea doesn't reach, Frankenstein lays its own.</summary>
        static void Clear(Bounds seaBounds, float sea)
        {
            try
            {
                var tt = Rt.Find("UnityEngine.Terrain");
                var sample = tt?.GetMethods().FirstOrDefault(m => m.Name == "SampleHeight" && m.GetParameters().Length == 1);
                var terrains = tt != null ? Rt.All(tt) : new List<object>();
                float edge = float.MinValue, highest = float.MinValue;
                foreach (var t in terrains)
                {
                    var c = t as Component;
                    var size = Rt.Get(Rt.Get(t, "terrainData"), "size");
                    if (c == null || !(size is Vector3 sz)) continue;
                    var pos = c.transform.position;
                    Say("terrain " + c.gameObject.name + ": x " + pos.x.ToString("0") + ".." + (pos.x + sz.x).ToString("0") + ", z " + pos.z.ToString("0") + ".." + (pos.z + sz.z).ToString("0") + ", up to " + (pos.y + sz.y).ToString("0") + " m");
                    edge = Mathf.Max(edge, pos.x + sz.x);
                    for (float px = 0; px <= PW; px += 120f)
                        for (float py = 0; py <= PH; py += 120f)
                        {
                            var w = ToWorld(px, py, 0);
                            if (w.x < pos.x || w.x > pos.x + sz.x || w.z < pos.z || w.z > pos.z + sz.z || sample == null) continue;
                            var h = (float)sample.Invoke(t, new object[] { w }) + pos.y;
                            highest = Mathf.Max(highest, h);
                        }
                }
                if (highest > _ground + 0.5f)
                {
                    Say("the terrain rises to " + highest.ToString("0") + " m where the island would go (its ground is " + _ground.ToString("0") + "): moving it past the terrain's east edge at x " + edge.ToString("0"));
                    _origin.x = edge + 120f;
                }
                else Say(terrains.Count + " terrain(s); the island's spot is clear");
            }
            catch (Exception e) { Say("couldn't check the terrain: " + (e.InnerException ?? e).Message); }
            var west = ToWorld(0, 0, 0);
            var east = ToWorld(PW, PH, 0);
            bool inSea = !float.IsNaN(sea) && seaBounds.min.x <= west.x && seaBounds.max.x >= east.x && seaBounds.min.z <= east.z && seaBounds.max.z >= west.z;
            if (!_ownSea && !inSea) { _ownSea = true; Say("the game's sea doesn't reach the island: Frankenstein lays its own at the same level"); }
            Say("the island: x " + west.x.ToString("0") + ".." + east.x.ToString("0") + ", z " + east.z.ToString("0") + ".." + west.z.ToString("0") + ", ground " + _ground.ToString("0.#") + ", water " + _water.ToString("0.#"));
        }

        static void Shells(List<MeshRenderer> all)
        {
            var seen = new HashSet<string>();
            _shells.Clear();
            _small.Clear();
            foreach (var r in all)
            {
                var b = r.bounds;
                if (b.size.y < 4 || b.size.y > 35 || b.size.x < 7 || b.size.z < 7 || b.size.x > 45 || b.size.z > 45) continue;
                var n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("road") || n.Contains("terrain") || n.Contains("water") || n.Contains("frankenstein")) continue;
                var mf = Rt.Comp(r.gameObject, typeof(MeshFilter)) as MeshFilter;
                var key = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : n;
                if (!seen.Add(key)) continue;
                if (b.size.x < 18 && b.size.z < 18) _small.Add(r.gameObject); else _shells.Add(r.gameObject);
                if (_shells.Count + _small.Count >= 60) break;
            }
            if (_small.Count == 0) _small.AddRange(_shells);
            if (_shells.Count == 0) _shells.AddRange(_small);
        }

        /// <summary>A window grid for the towers: dark glass, some windows lit.</summary>
        static Texture2D Windows()
        {
            var t = new Texture2D(64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    bool frame = x % 8 == 0 || y % 8 == 0 || y % 8 == 1;
                    Color c;
                    if (frame) c = new Color(0.16f, 0.17f, 0.2f);
                    else
                    {
                        int cell = (x / 8) * 8 + y / 8;
                        c = new System.Random(cell * 31 + 7).Next(5) == 0 ? new Color(0.95f, 0.82f, 0.55f) : new Color(0.2f, 0.3f, 0.4f);
                    }
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        // ---------- materials ----------

        static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            { "concrete", new Color(0.56f, 0.56f, 0.55f) }, { "asphalt", new Color(0.16f, 0.16f, 0.17f) }, { "line", new Color(0.95f, 0.8f, 0.2f) },
            { "white", new Color(0.9f, 0.9f, 0.9f) }, { "glass", new Color(0.22f, 0.32f, 0.42f) }, { "water", new Color(0.07f, 0.2f, 0.3f) },
            { "dirt", new Color(0.46f, 0.37f, 0.26f) }, { "wheat", new Color(0.78f, 0.68f, 0.3f) }, { "crop", new Color(0.28f, 0.48f, 0.2f) },
            { "barn", new Color(0.55f, 0.13f, 0.1f) }, { "roof", new Color(0.24f, 0.21f, 0.2f) }, { "steel", new Color(0.4f, 0.42f, 0.45f) },
            { "crane", new Color(0.95f, 0.7f, 0.1f) }, { "orange", new Color(0.95f, 0.45f, 0.1f) }, { "trailer", new Color(0.85f, 0.82f, 0.74f) },
            { "leaf", new Color(0.17f, 0.34f, 0.15f) }, { "trunk", new Color(0.3f, 0.2f, 0.12f) }, { "rock", new Color(0.42f, 0.4f, 0.36f) },
            { "hill", new Color(0.26f, 0.38f, 0.2f) }, { "path", new Color(0.7f, 0.66f, 0.56f) }, { "tarmac", new Color(0.3f, 0.3f, 0.31f) },
            { "hull_red", new Color(0.5f, 0.1f, 0.08f) }, { "hull_blue", new Color(0.1f, 0.18f, 0.35f) }, { "c_red", new Color(0.62f, 0.14f, 0.1f) },
            { "c_blue", new Color(0.12f, 0.3f, 0.6f) }, { "c_green", new Color(0.15f, 0.45f, 0.25f) }, { "c_orange", new Color(0.9f, 0.5f, 0.12f) },
            { "c_white", new Color(0.85f, 0.85f, 0.82f) }, { "tank", new Color(0.82f, 0.82f, 0.78f) }, { "wall", new Color(0.35f, 0.33f, 0.32f) },
        };

        static readonly Dictionary<string, Color> Glow = new Dictionary<string, Color>
        {
            { "neon_pink", new Color(1f, 0.2f, 0.75f) }, { "neon_cyan", new Color(0.2f, 0.9f, 1f) }, { "neon_purple", new Color(0.6f, 0.3f, 1f) },
            { "lamp", new Color(1f, 0.85f, 0.5f) }, { "beacon", new Color(1f, 0.25f, 0.2f) },
        };

        static Material Mat(string key)
        {
            Material m;
            if (_mats.TryGetValue(key, out m)) return m;
            if (key == "ground") m = _groundMat;
            else if (key.StartsWith("land:"))
            {
                // the town's own ground, its texture repeated across the island instead of stretched over it
                m = new Material(_groundMat ?? _baseMat);
                var p = key.Substring(5).Split('x');
                var scale = new Vector2(float.Parse(p[0]), float.Parse(p[1]));
                m.mainTextureScale = scale;
                try { if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", scale); } catch (Exception) { }
            }
            else if (key == "road") { m = new Material(_roadMat ?? _baseMat); Tint(m, new Color(0.25f, 0.25f, 0.27f)); }
            else
            {
                m = new Material(_baseMat ?? _groundMat);
                m.name = "Frankenstein " + key;
                Plain(m);
                Color c;
                if (key.StartsWith("gang:")) Tint(m, Gangs.ColorOf(key.Substring(5)));
                else if (key.StartsWith("gangglow:")) { var gc = Gangs.ColorOf(key.Substring(9)); Tint(m, gc); Emit(m, gc * 2f); }
                else if (key.StartsWith("tower:"))
                {
                    var p = key.Substring(6).Split('x');
                    Tint(m, Color.white);
                    m.mainTexture = _windows;
                    try { m.SetTexture("_BaseMap", _windows); } catch (Exception) { }
                    var scale = new Vector2(float.Parse(p[0]), float.Parse(p[1]));
                    m.mainTextureScale = scale;
                    try { m.SetTextureScale("_BaseMap", scale); } catch (Exception) { }
                    Emit(m, new Color(0.35f, 0.3f, 0.22f), _windows);
                }
                else if (Glow.TryGetValue(key, out c)) { Tint(m, c); Emit(m, c * 2.5f); }
                else Tint(m, Colors.TryGetValue(key, out c) ? c : Color.gray);
            }
            _mats[key] = m;
            return m;
        }

        static void Plain(Material m)
        {
            m.mainTexture = null;
            try { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null); } catch (Exception) { }
            try { if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null); } catch (Exception) { }
        }

        static void Tint(Material m, Color c)
        {
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        static void Emit(Material m, Color c, Texture t = null)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c);
            if (t != null && m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", t);
        }

        // ---------- shapes ----------

        static Type _collider;
        static readonly Queue<Action> _jobs = new Queue<Action>();
        static readonly System.Diagnostics.Stopwatch _sw = new System.Diagnostics.Stopwatch();

        /// <summary>Every piece is queued, and Next makes them a few milliseconds a frame, so the game never stalls.</summary>
        static void Shape(PrimitiveType kind, Vector3 at, Vector3 size, Quaternion rot, string mat, bool solid)
        {
            var parent = _parent;
            var m = Mat(mat);
            _pieces++;
            _jobs.Enqueue(() => Make(kind, at, size, rot, m, solid, parent));
        }

        static void Make(PrimitiveType kind, Vector3 at, Vector3 size, Quaternion rot, Material mat, bool solid, Transform parent)
        {
            var g = GameObject.CreatePrimitive(kind);  // its collider stays when solid: you walk and drive on it
            g.name = "Frankenstein " + (mat != null ? mat.name : "piece");
            if (parent != null) g.transform.SetParent(parent, false);
            g.transform.position = at;
            g.transform.rotation = rot;
            g.transform.localScale = size;
            var r = Rt.Comp(g, typeof(MeshRenderer)) as MeshRenderer;
            if (r != null) r.sharedMaterial = mat;
            if (!solid)
            {
                if (_collider == null) _collider = Rt.Find("UnityEngine.Collider");
                if (Rt.Comp(g, _collider) is UnityEngine.Object c) UnityEngine.Object.Destroy(c);
            }
            if (parent == null) _made.Add(g);
        }

        static void Box(Vector3 at, Vector3 size, float yaw, string mat, bool solid = true, float pitch = 0) =>
            Shape(PrimitiveType.Cube, at, size, Quaternion.Euler(pitch, yaw, 0), mat, solid);

        /// <summary>A box standing on y (its bottom at y).</summary>
        static void Block(Vector3 foot, Vector3 size, float yaw, string mat, bool solid = true) =>
            Box(foot + new Vector3(0, size.y / 2, 0), size, yaw, mat, solid);

        static void Cyl(Vector3 foot, float radius, float height, string mat, bool solid = true) =>
            Shape(PrimitiveType.Cylinder, foot + new Vector3(0, height / 2, 0), new Vector3(radius * 2, height / 2, radius * 2), Quaternion.identity, mat, solid);

        static void Ball(Vector3 at, Vector3 size, string mat, bool solid = true) =>
            Shape(PrimitiveType.Sphere, at, size, Quaternion.identity, mat, solid);

        static void Copy(GameObject src, Vector3 at, float yaw, float scale = 1f)
        {
            var parent = _parent;
            _pieces++;
            _jobs.Enqueue(() => Clone(src, at, yaw, scale, parent));
        }

        static void Clone(GameObject src, Vector3 at, float yaw, float scale, Transform parent)
        {
            if (src == null) return;
            var c = UnityEngine.Object.Instantiate(src);
            c.name = "Frankenstein " + src.name;
            // only what you see and bump into: the game's scripts, network parts and doors don't come along
            foreach (var mb in Rt.Comps(c, typeof(MonoBehaviour), true))
                if (mb is UnityEngine.Object o) UnityEngine.Object.DestroyImmediate(o);
            if (parent != null) c.transform.SetParent(parent, true);
            c.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (scale != 1f) c.transform.localScale = c.transform.localScale * scale;
            c.transform.position = at;
            var r = Rt.Comp(c, typeof(MeshRenderer)) as MeshRenderer;
            if (r != null)
            {
                var b = r.bounds;  // stand it on the ground at the spot, whatever its pivot
                c.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            }
            c.SetActive(true);
        }

        static void Tree(Vector3 foot, System.Random rnd)
        {
            if (_trees.Count > 0) { Copy(_trees[rnd.Next(_trees.Count)], foot, rnd.Next(360)); return; }
            float h = 4f + (float)rnd.NextDouble() * 3f;
            Cyl(foot, 0.25f, h * 0.5f, "trunk");
            Ball(foot + new Vector3(0, h * 0.7f, 0), new Vector3(h * 0.6f, h * 0.55f, h * 0.6f), "leaf", false);
        }

        // ---------- the land, the sea, the roads and bridges ----------

        static void BuildBase()
        {
            _base = new GameObject("Frankenstein island");
            _made.Add(_base);
            _parent = _base.transform;
            if (_ownSea)
            {
                var mid = ToWorld(PW / 2, PH / 2, _water - 0.1f);
                Box(mid, new Vector3(PW * S + 900f, 0.2f, PH * S + 900f), 0, "water", false);
            }
            foreach (var l in Land)
            {
                float top = _ground + l[4], depth = top - _water + 6f;
                var c = ToWorld(l[0] + l[2] / 2, l[1] + l[3] / 2, top - depth / 2);
                Box(c, new Vector3(l[2] * S, depth, l[3] * S), 0, "land:" + Mathf.Round(l[2] * S / 12f) + "x" + Mathf.Round(l[3] * S / 12f));
                // a rocky shore where the land meets the water
                Box(new Vector3(c.x, _water + 0.8f, c.z), new Vector3(l[2] * S + 2f, 3f, l[3] * S + 2f), 0, "rock", false);
            }
            // the highway bridge from the edge of town
            Road(new Vector3(_townEdge.x - 30f, _ground, _townEdge.z), ToWorld(Roads[0][0], Roads[0][1], _ground), 16f);
            foreach (var r in Roads)
                Road(ToWorld(r[0], r[1], 0), ToWorld(r[2], r[3], 0), r[4]);
            var b = _base;
            _jobs.Enqueue(() => StaticBatchingUtility.Combine(b));
            _parent = null;
        }

        static void Road(Vector3 a, Vector3 b, float width)
        {
            a.y = HeightAt(a);
            b.y = HeightAt(b);
            var flat = new Vector3(b.x - a.x, 0, b.z - a.z);
            float len = flat.magnitude, rise = b.y - a.y;
            if (len < 1f) return;
            float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, pitch = -Mathf.Atan2(rise, len) * Mathf.Rad2Deg;
            float slope = Mathf.Sqrt(len * len + rise * rise);
            var mid = (a + b) / 2 + new Vector3(0, 0.05f - 0.2f, 0);
            Box(mid, new Vector3(width, 0.4f, slope + width * 0.5f), yaw, "road", true, pitch);
            if (width >= 14f) Box(mid + new Vector3(0, 0.21f, 0), new Vector3(0.3f, 0.02f, slope), yaw, "line", false, pitch);
            var right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
            bool bridge = false;
            for (float t = 0; t <= len; t += 15f)
            {
                var p = Vector3.Lerp(a, b, t / len);
                _roadPoints.Add(p);
                if (!OverWater(p)) continue;
                bridge = true;
                Cyl(new Vector3(p.x, _water - 6f, p.z) + right * (width / 2 - 1f), 0.8f, p.y - _water + 5.6f, "concrete");
                Cyl(new Vector3(p.x, _water - 6f, p.z) - right * (width / 2 - 1f), 0.8f, p.y - _water + 5.6f, "concrete");
            }
            if (bridge)
                foreach (var side in new[] { 1f, -1f })
                    Box(mid + right * side * (width / 2 + 0.15f) + new Vector3(0, 0.7f, 0), new Vector3(0.3f, 1.1f, slope), yaw, "concrete", true, pitch);
            // street lamps every 40 m
            for (float t = 20f; t < len; t += 40f)
            {
                var p = Vector3.Lerp(a, b, t / len) + right * (width / 2 + 0.6f);
                if (OverWater(p)) continue;
                Cyl(p, 0.12f, 6f, "steel");
                Box(p + new Vector3(0, 6f, 0) - right * 0.8f, new Vector3(0.4f, 0.2f, 1.8f), yaw + 90f, "lamp", false);
            }
        }

        // ---------- the districts ----------

        /// <summary>A few milliseconds of building a frame: the queued pieces first, then the next district's plan.</summary>
        static void Next()
        {
            _sw.Reset();
            _sw.Start();
            while (_sw.Elapsed.TotalMilliseconds < 3.0)
            {
                if (_jobs.Count > 0)
                {
                    var job = _jobs.Dequeue();
                    try { job(); } catch (Exception e) { if (!_warned) { _warned = true; Melon<Plugin>.Logger.Warning("Expansion piece: " + e.Message); } }
                    continue;
                }
                if (_step >= Plan.Length) { Finish(); return; }
                Lay(Plan[_step]);
                _step++;
            }
        }

        static void Lay(District d)
        {
            d.Root = new GameObject("Frankenstein " + d.Name);
            _made.Add(d.Root);
            _parent = d.Root.transform;
            var rnd = new System.Random(d.Name.Aggregate(17, (h, ch) => h * 31 + ch));
            switch (d.Kind)
            {
                case "towers": Towers(d, rnd); break;
                case "hills": Hills(d, rnd); break;
                case "park": Park(d, rnd); break;
                case "suburbs": Houses(d, rnd, 34f); break;
                case "airport": Airport(d, rnd); break;
                case "nightlife": Nightlife(d, rnd); break;
                case "construction": Construction(d, rnd); break;
                case "trailers": Trailers(d, rnd); break;
                case "farms": Farms(d, rnd); break;
                case "port": Port(d, rnd); break;
                case "cartel": Port(d, rnd, true); break;
                case "cliff": Cliff(d, rnd); break;
            }
            if (d.Gang.Length > 0 && Gangs.ColorOf(d.Gang) != Color.gray) Hq(d, rnd);
            var root = d.Root;
            _jobs.Enqueue(() => { StaticBatchingUtility.Combine(root); d.Done = true; });
            _parent = null;
        }

        static void Finish()
        {
            if (_jobs.Count > 0) return;
            _step = -1;
            _built = true;
            Note = "The island: " + Plan.Count(p => p.Done) + " districts, " + _pieces + " pieces. F4 for the map.";
            Say(Note);
        }

        static Vector3 Spot(District d, float fx, float fy) => ToWorld(d.Px.x + d.Px.width * fx, d.Px.y + d.Px.height * fy, _ground + d.Lift);
        static float Wm(District d) => d.Px.width * S;
        static float Hm(District d) => d.Px.height * S;
        static float R(System.Random rnd, float a, float b) => a + (float)rnd.NextDouble() * (b - a);

        /// <summary>A grid of streets through the district, every `block` metres.</summary>
        static void Streets(District d, float block, string mat = "road")
        {
            var c = d.Centre;
            for (float x = -Wm(d) / 2 + block; x < Wm(d) / 2 - 5; x += block)
                Box(c + new Vector3(x, 0.02f, 0), new Vector3(9f, 0.06f, Hm(d)), 0, mat, false);
            for (float z = -Hm(d) / 2 + block; z < Hm(d) / 2 - 5; z += block)
                Box(c + new Vector3(0, 0.02f, z), new Vector3(Wm(d), 0.06f, 9f), 0, mat, false);
        }

        static IEnumerable<Vector3> Lots(District d, float block, float margin)
        {
            var c = d.Centre;
            for (float x = -Wm(d) / 2 + margin; x < Wm(d) / 2 - margin; x += block)
                for (float z = -Hm(d) / 2 + margin; z < Hm(d) / 2 - margin; z += block)
                    yield return c + new Vector3(x + block / 2 - margin / 2, 0, z + block / 2 - margin / 2);
        }

        static void Towers(District d, System.Random rnd)
        {
            Streets(d, 46f);
            var c = d.Centre;
            float maxR = Mathf.Max(Wm(d), Hm(d)) / 2;
            foreach (var lot in Lots(d, 46f, 6f))
            {
                // taller towards the middle, like a real downtown
                float near = 1f - Mathf.Clamp01(Vector3.Distance(lot, c) / maxR);
                float h = Mathf.Round(R(rnd, 30f, 60f) + near * R(rnd, 40f, 90f));
                float w = R(rnd, 18f, 28f), dd = R(rnd, 18f, 28f);
                var key = "tower:" + Mathf.Max(1, Mathf.Round(w / 4)) + "x" + Mathf.Max(1, Mathf.Round(h / 4));
                Block(lot, new Vector3(w, h, dd), 0, key);
                Block(lot + new Vector3(0, h, 0), new Vector3(w * 0.6f, R(rnd, 2f, 5f), dd * 0.6f), 0, "steel");
                if (h > 100) Cyl(lot + new Vector3(0, h + 3f, 0), 0.3f, 18f, "steel", false);
                if (h > 100) Ball(lot + new Vector3(0, h + 21.5f, 0), Vector3.one * 1f, "beacon", false);
                Block(lot + new Vector3(0, 0, -dd / 2 - 1.5f), new Vector3(w * 0.5f, 4f, 3f), 0, "glass");  // the lobby
            }
        }

        static void Houses(District d, System.Random rnd, float block, bool big = false)
        {
            Streets(d, block);
            var list = big ? _shells : _small;
            if (list.Count == 0) return;
            foreach (var lot in Lots(d, block, 5f))
            {
                foreach (var side in new[] { -1f, 1f })
                {
                    var at = lot + new Vector3(side * block * 0.22f, 0, 0);
                    Copy(list[rnd.Next(list.Count)], at, side > 0 ? 270f : 90f);
                    Tree(at + new Vector3(0, 0, block * 0.3f), rnd);
                }
            }
        }

        static void Hills(District d, System.Random rnd)
        {
            // green hills you can walk over, with the mansions between them
            for (int i = 0; i < 7; i++)
            {
                var at = Spot(d, R(rnd, 0.1f, 0.9f), R(rnd, 0.1f, 0.9f));
                float r = R(rnd, 25f, 50f);
                Ball(at - new Vector3(0, r * 0.25f, 0), new Vector3(r * 2f, r * 0.7f, r * 2f), "hill");
            }
            var list = _shells.Count > 0 ? _shells : _small;
            for (int i = 0; i < 10 && list.Count > 0; i++)
            {
                var at = Spot(d, R(rnd, 0.1f, 0.9f), R(rnd, 0.15f, 0.85f));
                Block(at, new Vector3(30f, 0.6f, 30f), 0, "path");  // its own flat lot
                Copy(list[rnd.Next(list.Count)], at + new Vector3(0, 0.6f, 0), rnd.Next(4) * 90f, 1.15f);
                Block(at + new Vector3(10f, 0.6f, 10f), new Vector3(6f, 0.1f, 10f), 0, "water", false);  // the pool
            }
            for (int i = 0; i < 60; i++) Tree(Spot(d, R(rnd, 0.02f, 0.98f), R(rnd, 0.02f, 0.98f)), rnd);
        }

        static void Park(District d, System.Random rnd)
        {
            var c = d.Centre;
            Block(c, new Vector3(Wm(d) * 0.5f, 0.12f, Hm(d) * 0.45f), 0, "water", false);
            Block(c + new Vector3(Wm(d) * 0.22f, 0, -Hm(d) * 0.1f), new Vector3(Wm(d) * 0.22f, 0.13f, Hm(d) * 0.3f), 20f, "water", false);
            Block(c + new Vector3(0, 0, Hm(d) * 0.3f), new Vector3(Wm(d) * 0.9f, 0.08f, 4f), 0, "path", false);
            Block(c + new Vector3(0, 0, -Hm(d) * 0.32f), new Vector3(Wm(d) * 0.9f, 0.08f, 4f), 0, "path", false);
            for (int i = 0; i < 70; i++)
            {
                var at = Spot(d, R(rnd, 0.03f, 0.97f), R(rnd, 0.03f, 0.97f));
                if (Mathf.Abs(at.x - c.x) < Wm(d) * 0.3f && Mathf.Abs(at.z - c.z) < Hm(d) * 0.26f) continue;  // not in the lake
                Tree(at, rnd);
            }
            for (int i = 0; i < 8 && _props.Count > 0; i++) Copy(_props[rnd.Next(_props.Count)], Spot(d, R(rnd, 0.1f, 0.9f), rnd.Next(2) == 0 ? 0.2f : 0.82f), rnd.Next(360));
        }

        static void Airport(District d, System.Random rnd)
        {
            var c = d.Centre;
            Block(c - new Vector3(0, 0.02f, 0), new Vector3(Wm(d), 0.1f, Hm(d)), 0, "tarmac", false);
            float yaw = 72f;  // the runway runs along the island's north-east coast, as on the map
            var run = c + new Vector3(Wm(d) * 0.12f, 0, 0);
            Block(run, new Vector3(45f, 0.12f, Hm(d) * 1.25f), yaw, "asphalt", false);
            var along = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            for (float t = -Hm(d) * 0.55f; t < Hm(d) * 0.55f; t += 24f)
                Block(run + along * t + new Vector3(0, 0.01f, 0), new Vector3(1f, 0.12f, 10f), yaw, "white", false);
            var term = c + new Vector3(-Wm(d) * 0.28f, 0, Hm(d) * 0.05f);
            Block(term, new Vector3(30f, 12f, 110f), yaw, "glass");
            Block(term + new Vector3(0, 12f, 0), new Vector3(34f, 1.5f, 114f), yaw, "white");
            var tower = c + new Vector3(-Wm(d) * 0.38f, 0, -Hm(d) * 0.3f);
            Cyl(tower, 3f, 34f, "concrete");
            Block(tower + new Vector3(0, 34f, 0), new Vector3(10f, 5f, 10f), 45f, "glass");
            Block(tower + new Vector3(0, 39f, 0), new Vector3(11f, 0.6f, 11f), 45f, "steel");
            for (int i = 0; i < 2; i++) Block(c + new Vector3(-Wm(d) * 0.1f + i * 50f, 0, -Hm(d) * 0.38f), new Vector3(40f, 14f, 34f), 0, "steel");
            for (int i = 0; i < 4; i++) Plane(term + new Vector3(30f + (i % 2) * 34f, 0, -40f + i * 26f), yaw + 90f);
        }

        static void Plane(Vector3 foot, float yaw)
        {
            var rot = Quaternion.Euler(0, yaw, 0);
            var f = rot * Vector3.forward;
            Shape(PrimitiveType.Capsule, foot + new Vector3(0, 3.2f, 0), new Vector3(3.6f, 16f, 3.6f), rot * Quaternion.Euler(90, 0, 0), "white", true);
            Box(foot + new Vector3(0, 2.8f, 0) + f * 1f, new Vector3(30f, 0.4f, 5f), yaw, "white");
            Box(foot + new Vector3(0, 6f, 0) - f * 13.5f, new Vector3(0.4f, 5f, 3.5f), yaw, "c_blue");
            Box(foot + new Vector3(0, 3.8f, 0) - f * 14f, new Vector3(10f, 0.3f, 2.5f), yaw, "white");
        }

        static void Nightlife(District d, System.Random rnd)
        {
            var c = d.Centre;
            Block(c - new Vector3(0, 0.02f, 0), new Vector3(Wm(d), 0.08f, Hm(d)), 0, "neon_purple", false);
            Block(c, new Vector3(Wm(d) * 0.9f, 0.1f, 12f), 0, "path", false);  // the strip
            var list = _shells.Count > 0 ? _shells : _small;
            string[] neon = { "neon_pink", "neon_cyan", "neon_purple" };
            for (float x = -Wm(d) / 2 + 14f; x < Wm(d) / 2 - 10f; x += 26f)
                foreach (var side in new[] { -1f, 1f })
                {
                    var at = c + new Vector3(x, 0, side * 24f);
                    if (list.Count > 0) Copy(list[rnd.Next(list.Count)], at, side > 0 ? 180f : 0f);
                    else Block(at, new Vector3(20f, R(rnd, 8f, 16f), 20f), 0, "wall");
                    var face = at - new Vector3(0, 0, side * 11f);
                    Block(face + new Vector3(0, R(rnd, 4f, 7f), 0), new Vector3(R(rnd, 8f, 16f), 1.4f, 0.3f), 0, neon[rnd.Next(3)], false);
                    Block(face + new Vector3(R(rnd, -8f, 8f), 0, 0), new Vector3(0.3f, R(rnd, 6f, 12f), 0.3f), 0, neon[rnd.Next(3)], false);
                }
        }

        static void Construction(District d, System.Random rnd)
        {
            var c = d.Centre;
            Block(c - new Vector3(0, 0.02f, 0), new Vector3(Wm(d), 0.15f, Hm(d)), 0, "dirt", false);
            for (int b = 0; b < 3; b++)
            {
                var at = c + new Vector3((b - 1) * Wm(d) * 0.3f, 0, (b % 2 == 0 ? -1 : 1) * Hm(d) * 0.15f);
                int floors = 3 + rnd.Next(5);
                for (int fl = 0; fl < floors; fl++)
                {
                    Block(at + new Vector3(0, fl * 4f, 0), new Vector3(28f, 0.4f, 22f), 0, "concrete");
                    if (fl == floors - 1) break;
                    for (int ix = 0; ix < 4; ix++)
                        for (int iz = 0; iz < 3; iz++)
                            Block(at + new Vector3(-12f + ix * 8f, fl * 4f + 0.4f, -9f + iz * 9f), new Vector3(0.6f, 3.6f, 0.6f), 0, "concrete");
                }
            }
            for (int k = 0; k < 2; k++)
            {
                var at = c + new Vector3((k == 0 ? -1 : 1) * Wm(d) * 0.18f, 0, 0);
                float h = 44f + k * 8f;
                Block(at, new Vector3(2.2f, h, 2.2f), 0, "crane");
                Block(at + new Vector3(12f, h, 0), new Vector3(44f, 1.6f, 1.6f), k * 60f, "crane");
                Block(at + new Vector3(-8f, h - 2f, 0), new Vector3(5f, 3f, 3f), k * 60f, "concrete");
                Block(at + new Vector3(0, h + 1.6f, 0), new Vector3(3f, 2.5f, 3f), 0, "glass");
            }
            for (int i = 0; i < 14; i++) Block(Spot(d, R(rnd, 0.05f, 0.95f), R(rnd, 0.05f, 0.95f)), new Vector3(R(rnd, 2f, 5f), R(rnd, 1f, 2.5f), R(rnd, 2f, 5f)), rnd.Next(90), rnd.Next(2) == 0 ? "steel" : "c_orange");
            foreach (var side in new[] { -1f, 1f })
            {
                Block(c + new Vector3(0, 0, side * Hm(d) / 2), new Vector3(Wm(d), 2.2f, 0.15f), 0, "orange");
                Block(c + new Vector3(side * Wm(d) / 2, 0, 0), new Vector3(0.15f, 2.2f, Hm(d)), 0, "orange");
            }
        }

        static void Trailers(District d, System.Random rnd)
        {
            var c = d.Centre;
            Block(c - new Vector3(0, 0.02f, 0), new Vector3(Wm(d), 0.08f, Hm(d)), 0, "dirt", false);
            for (float x = -Wm(d) / 2 + 10f; x < Wm(d) / 2 - 8f; x += 16f)
                for (float z = -Hm(d) / 2 + 12f; z < Hm(d) / 2 - 10f; z += 18f)
                {
                    if (rnd.Next(6) == 0) continue;
                    if (d.Gang.Length > 0 && Mathf.Abs(x) < 50f && Mathf.Abs(z) < 40f) continue;  // the gang's compound is in the middle
                    var at = c + new Vector3(x + R(rnd, -1.5f, 1.5f), 0.4f, z);
                    float yaw = rnd.Next(2) * 180f + R(rnd, -6f, 6f);
                    Block(at, new Vector3(3.6f, 3f, 11f), yaw, rnd.Next(3) == 0 ? "c_white" : "trailer");
                    Block(at + new Vector3(0, 3f, 0), new Vector3(3.9f, 0.25f, 11.3f), yaw, "roof");
                    if (rnd.Next(3) == 0 && _props.Count > 0) Copy(_props[rnd.Next(_props.Count)], at + new Vector3(4f, -0.4f, 0), rnd.Next(360));
                }
            for (int i = 0; i < 25; i++) Tree(Spot(d, R(rnd, 0.02f, 0.98f), rnd.Next(2) == 0 ? 0.03f : 0.97f), rnd);
        }

        static void Farms(District d, System.Random rnd)
        {
            var c = d.Centre;
            string[] crops = { "crop", "wheat", "dirt", "crop", "wheat" };
            float fw = Wm(d) / 4, fh = Hm(d) / 4;
            for (int ix = 0; ix < 4; ix++)
                for (int iz = 0; iz < 4; iz++)
                {
                    if (ix == 1 && iz == 2) continue;  // the farmyard
                    var at = c + new Vector3(-Wm(d) / 2 + fw * (ix + 0.5f), 0, -Hm(d) / 2 + fh * (iz + 0.5f));
                    Block(at, new Vector3(fw - 3f, 0.25f, fh - 3f), 0, crops[rnd.Next(crops.Length)], false);
                }
            var yard = c + new Vector3(-Wm(d) / 2 + fw * 1.5f, 0, -Hm(d) / 2 + fh * 2.5f);
            Block(yard, new Vector3(14f, 7f, 22f), 0, "barn");
            Shape(PrimitiveType.Cube, yard + new Vector3(-3.5f, 8.7f, 0), new Vector3(8.5f, 0.4f, 23f), Quaternion.Euler(0, 0, 35f), "roof", true);
            Shape(PrimitiveType.Cube, yard + new Vector3(3.5f, 8.7f, 0), new Vector3(8.5f, 0.4f, 23f), Quaternion.Euler(0, 0, -35f), "roof", true);
            Cyl(yard + new Vector3(14f, 0, 6f), 3f, 16f, "steel");
            Ball(yard + new Vector3(14f, 16f, 6f), new Vector3(6f, 3f, 6f), "steel", false);
            Block(yard + new Vector3(-16f, 0, -10f), new Vector3(10f, 5f, 8f), 0, "c_white");  // the farmhouse
            Block(yard + new Vector3(30f, 0, -24f), new Vector3(26f, 0.1f, 18f), 0, "water", false);  // the pond
            for (int i = 0; i < 30; i++) Tree(Spot(d, rnd.Next(2) == 0 ? 0.02f : 0.98f, R(rnd, 0.02f, 0.98f)), rnd);
        }

        static void Port(District d, System.Random rnd, bool tanks = false)
        {
            var c = d.Centre;
            Block(c - new Vector3(0, 0.02f, 0), new Vector3(Wm(d), 0.1f, Hm(d)), 0, "concrete", false);
            string[] boxes = { "c_red", "c_blue", "c_green", "c_orange", "c_white" };
            // warehouses down the middle
            for (float z = -Hm(d) / 2 + 30f; z < Hm(d) / 2 - 25f; z += 60f)
                Block(c + new Vector3(Wm(d) * 0.12f, 0, z), new Vector3(36f, 12f, 44f), 0, rnd.Next(2) == 0 ? "steel" : "wall");
            // container stacks along the quay
            for (float z = -Hm(d) / 2 + 12f; z < Hm(d) / 2 - 10f; z += 7f)
                for (int row = 0; row < 3; row++)
                {
                    int high = 1 + rnd.Next(4);
                    var at = c + new Vector3(-Wm(d) / 2 + 20f + row * 14f, 0, z);
                    for (int k = 0; k < high; k++) Block(at + new Vector3(0, k * 2.6f, 0), new Vector3(12f, 2.6f, 2.5f), 0, boxes[rnd.Next(boxes.Length)]);
                }
            if (!tanks) Quay(d, rnd, boxes);
            else  // the Cartel's point: its tanks instead of the quay
                for (int i = 0; i < 6; i++)
                {
                    var at = c + new Vector3(Wm(d) * (0.12f + (i % 3) * 0.12f), 0, Hm(d) * (i < 3 ? 0.25f : 0.4f) - Hm(d) * 0.1f);
                    Cyl(at, 9f, 14f, "tank");
                    Cyl(at + new Vector3(0, 14f, 0), 9.2f, 0.4f, "steel");
                }
        }

        static void Quay(District d, System.Random rnd, string[] boxes)
        {
            var c = d.Centre;
            // gantry cranes on the quay edge
            for (float z = -Hm(d) / 2 + 40f; z < Hm(d) / 2 - 30f; z += 90f)
            {
                var at = c + new Vector3(-Wm(d) / 2 + 6f, 0, z);
                foreach (var dz in new[] { -6f, 6f }) { Block(at + new Vector3(4f, 0, dz), new Vector3(1.2f, 34f, 1.2f), 0, "orange"); Block(at + new Vector3(-12f, 0, dz), new Vector3(1.2f, 34f, 1.2f), 0, "orange"); }
                Block(at + new Vector3(-14f, 34f, 0), new Vector3(52f, 2.2f, 14f), 0, "orange");
            }
            // piers and ships off the west side
            for (float z = -Hm(d) / 2 + 30f; z < Hm(d) / 2 - 20f; z += 80f)
            {
                var pier = c + new Vector3(-Wm(d) / 2 - 30f, _ground - 0.3f - c.y, z);
                Block(pier, new Vector3(60f, 0.6f, 12f), 0, "concrete");
                Ship(pier + new Vector3(-6f, 0, 22f), rnd, boxes);
            }
        }

        static void Ship(Vector3 at, System.Random rnd, string[] boxes)
        {
            float y = _water;
            var hull = new Vector3(at.x, y - 4f, at.z);
            Block(hull, new Vector3(70f, 10f, 13f), 0, rnd.Next(2) == 0 ? "hull_red" : "hull_blue");
            Block(hull + new Vector3(-28f, 10f, 0), new Vector3(10f, 12f, 12f), 0, "c_white");
            Block(hull + new Vector3(-28f, 22f, 0), new Vector3(11f, 1f, 13f), 0, "steel");
            for (float x = -18f; x < 30f; x += 12.5f)
                for (int k = 0; k < 1 + rnd.Next(3); k++)
                    Block(hull + new Vector3(x, 10f + k * 2.6f, rnd.Next(2) == 0 ? -3f : 3f), new Vector3(12f, 2.6f, 5f), 0, boxes[rnd.Next(boxes.Length)]);
        }

        static void Cliff(District d, System.Random rnd)
        {
            for (int i = 0; i < 9; i++)
            {
                var at = Spot(d, R(rnd, 0.05f, 0.95f), R(rnd, 0.05f, 0.95f));
                float r = R(rnd, 6f, 16f);
                Ball(at, new Vector3(r * 2, r * 0.8f, r * 2), "rock");
            }
            for (int i = 0; i < 40; i++) Tree(Spot(d, R(rnd, 0.02f, 0.98f), R(rnd, 0.02f, 0.98f)), rnd);
            // the lighthouse on the far point
            var lh = Spot(d, 0.92f, 0.95f);
            for (int k = 0; k < 6; k++) Cyl(lh + new Vector3(0, k * 4f, 0), 2.6f - k * 0.15f, 4f, k % 2 == 0 ? "c_white" : "c_red");
            Cyl(lh + new Vector3(0, 24f, 0), 2f, 3f, "glass");
            Ball(lh + new Vector3(0, 25.5f, 0), Vector3.one * 2f, "lamp", false);
            Cyl(lh + new Vector3(0, 27f, 0), 2.4f, 0.8f, "c_red");
        }

        /// <summary>A gang's HQ: a walled compound in its colours, the main building, flags and a crown sign that
        /// glows, the gate on the road side. Its crew hold it.</summary>
        static void Hq(District d, System.Random rnd)
        {
            var c = d.Centre;
            string gang = "gang:" + d.Gang;
            float w = Mathf.Min(Wm(d), 90f), h = Mathf.Min(Hm(d), 70f);
            Block(c - new Vector3(0, 0.02f, 0), new Vector3(w, 0.12f, h), 0, "concrete", false);
            foreach (var side in new[] { -1f, 1f })
            {
                Block(c + new Vector3(0, 0, side * h / 2), new Vector3(w, 3f, 0.5f), 0, "wall");
                Block(c + new Vector3(0, 3f, side * h / 2), new Vector3(w, 0.4f, 0.6f), 0, gang);
                // the west wall has the gate in its middle
                Block(c + new Vector3(-w / 2, 0, side * h * 0.3f), new Vector3(0.5f, 3f, h * 0.4f), 0, "wall");
            }
            Block(c + new Vector3(w / 2, 0, 0), new Vector3(0.5f, 3f, h), 0, "wall");
            var list = _shells.Count > 0 ? _shells : _small;
            if (list.Count > 0) Copy(list[rnd.Next(list.Count)], c + new Vector3(w * 0.12f, 0, 0), 270f, 1.2f);
            else Block(c + new Vector3(w * 0.12f, 0, 0), new Vector3(24f, 12f, 20f), 0, "wall");
            foreach (var corner in new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) })
            {
                var at = c + new Vector3(corner.x * (w / 2 - 3f), 0, corner.z * (h / 2 - 3f));
                Cyl(at, 0.12f, 9f, "steel");
                Box(at + new Vector3(0.9f, 8.2f, 0), new Vector3(1.8f, 1.2f, 0.05f), 0, gang, false);
            }
            // the crown sign, as on the map
            var sign = c + new Vector3(-w * 0.2f, 0, 0);
            Cyl(sign, 0.25f, 10f, "steel");
            Block(sign + new Vector3(0, 10f, 0), new Vector3(6f, 1.4f, 0.4f), 0, "gangglow:" + d.Gang, false);
            for (int k = -1; k <= 1; k++) Box(sign + new Vector3(k * 2.2f, 12.2f + (k == 0 ? 0.6f : 0), 0), new Vector3(1f, 2.2f + (k == 0 ? 1.2f : 0), 0.4f), 0, "gangglow:" + d.Gang, false);
            // the crew hold it: their routes start here
            var pts = new List<Vector3> { c + new Vector3(-w * 0.3f, 0.2f, 0), c + new Vector3(-w * 0.35f, 0.2f, h * 0.25f), c + new Vector3(-w * 0.35f, 0.2f, -h * 0.25f), c + new Vector3(w * 0.3f, 0.2f, h * 0.3f) };
            Crew.Island(Gangs.RegionOf(d.Gang) == d.Name ? d.Name : "", pts);
        }

        // ---------- getting there ----------

        public static void Travel(int i)
        {
            var me = Player.Local;
            if (me == null || i < 0 || i >= Plan.Length || !_built) return;
            try
            {
                if (me.IsInVehicle) { ItemMenu.Notify("Get out of the car first."); return; }
                var c = Plan[i].Centre;
                var best = _roadPoints.Count > 0 ? _roadPoints.OrderBy(r => (r - c).sqrMagnitude).First() : c;
                if (Vector3.Distance(best, c) > 140f) best = c;
                me.Position = best + new Vector3(0, 1.5f, 0);
                ItemMenu.Notify("Welcome to " + Plan[i].Name + ".");
            }
            catch (Exception e) { if (!_warned) { _warned = true; Melon<Plugin>.Logger.Warning("Expansion travel: " + e.Message); } }
        }

        public static string NameAt(int i) => i >= 0 && i < Plan.Length ? Plan[i].Name + (Plan[i].Gang.Length > 0 ? " (" + Gangs.NameOf(Plan[i].Gang) + ")" : "") : "";


        /// <summary>The district's name when you come in, and the gangs' HQ names over their compounds when you're near.</summary>
        public static void Draw()
        {
            if (!_built) return;
            if (_here.Length > 0 && Time.unscaledTime < _hereUntil)
                GUI.Box(new Rect(Screen.width / 2f - 200, 80, 400, 30), _here.ToUpperInvariant());
            var cam = Camera.main;
            var me = Player.Local;
            if (cam == null || me == null || Plugin.MenuUp) return;
            foreach (var d in Plan)
            {
                if (d.Gang.Length == 0 || d.Root == null || !d.Root.activeSelf) continue;
                var at = d.Centre + new Vector3(-Mathf.Min(Wm(d), 90f) * 0.2f, 16f, 0);
                if (Vector3.Distance(at, me.Position) > 220f) continue;
                var s = cam.WorldToScreenPoint(at);
                if (s.z <= 0) continue;
                var col = Gangs.ColorOf(d.Gang);
                var r = new Rect(s.x - 150, Screen.height - s.y - 14, 300, 28);
                var text = Gangs.NameOf(d.Gang).ToUpperInvariant() + " HQ";
                GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, RefArt.Style(20, Color.black, 4, true));
                GUI.Label(r, text, RefArt.Style(20, col, 4, true));
            }
        }
    }
}
