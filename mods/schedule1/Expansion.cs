using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>EXPERIMENTAL: more map. New districts built past the edge of Schedule I's town, at the town's own
    /// scale, from the town's own pieces: solid ground, crossing roads as wide as the town's, copies of its
    /// buildings along them and its props on the corners, one district for each gang, in its colours. Built again
    /// the same way every session (the layout comes from a fixed seed), so nothing is saved into the game.
    /// Getting there: F8 > Travel. Limits, said plainly: the copied buildings are shells (no doors or insides),
    /// the game's map and its people don't know these places (they have no walking paths there), and the gangs'
    /// people stay in their old districts.</summary>
    internal static class Expansion
    {
        class District
        {
            public string Gang, Name;
            public Vector3 Centre;
            public Color Color;
        }

        static readonly List<District> _districts = new List<District>();
        static readonly List<GameObject> _made = new List<GameObject>();
        static float _buildAt = -1;
        static bool _built, _warned;
        static string _here = "";
        static float _hereUntil;
        public static string Note = "";
        const float Size = 150f, Gap = 30f;

        public static bool On => Gangs.On && Gangs.Ids.Any(g => Gangs.NewDistrictOf(g).Length > 0);
        public static int Count => _districts.Count;

        public static void Scene(bool inTown)
        {
            _built = false;
            _districts.Clear();
            _made.Clear();
            _buildAt = inTown && On ? Time.unscaledTime + 25f : -1;  // after the town and its props exist
        }

        public static void Update()
        {
            if (_buildAt > 0 && Time.unscaledTime >= _buildAt)
            {
                _buildAt = -1;
                try { Build(); }
                catch (Exception e) { Note = "The new districts couldn't be built: " + e.Message; Melon<Plugin>.Logger.Warning("Expansion: " + e); }
            }
            if (!_built) return;
            var me = Player.Local;
            if (me == null) return;
            var d = _districts.FirstOrDefault(x => Mathf.Abs(me.Position.x - x.Centre.x) < Size / 2 && Mathf.Abs(me.Position.z - x.Centre.z) < Size / 2);
            var name = d != null ? d.Name : "";
            if (name != _here)
            {
                _here = name;
                if (d != null) { _hereUntil = Time.unscaledTime + 5f; ItemMenu.Notify("Entering " + d.Name + ": " + Gangs.NameOf(d.Gang) + " turf"); }
            }
        }

        // ---------- the town's own measurements and pieces ----------

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

        static void Build()
        {
            var all = Rt.All<MeshRenderer>().Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy).ToList();
            if (all.Count < 50) { _buildAt = Time.unscaledTime + 10f; return; }  // not loaded yet
            var town = new Bounds();
            bool first = true;
            var roads = new List<MeshRenderer>();
            foreach (var r in all)
            {
                var b = r.bounds;
                if (Huge(b)) continue;
                if (first) { town = b; first = false; } else town.Encapsulate(b);
                if (r.gameObject.name.ToLowerInvariant().Contains("road")) roads.Add(r);
            }
            // ground height: where the town's roads are (or where you stand)
            float ground = roads.Count > 0 ? roads.Select(r => r.bounds.max.y).OrderBy(y => y).ElementAt(roads.Count / 2)
                                           : (Player.Local != null ? Player.Local.Position.y - 1f : town.min.y);
            float roadWidth = roads.Count > 0 ? Mathf.Clamp(roads.Select(r => Mathf.Min(r.bounds.size.x, r.bounds.size.z)).OrderBy(w => w).ElementAt(roads.Count / 2), 6f, 14f) : 9f;
            var groundMat = Pick(all, "grass", "ground", "sidewalk", "concrete", "pavement");
            var roadMat = Pick(roads.Count > 0 ? roads : all, "road", "asphalt", "tarmac");
            var buildings = Shells(all);
            var props = World.Kinds();
            var gangs = Gangs.Ids.Where(g => Gangs.NewDistrictOf(g).Length > 0).ToList();
            // a row of districts past the town's east edge, two deep
            var rnd = new System.Random(1337);
            float x0 = town.max.x + 120f + Size / 2, z0 = town.center.z - Size - Gap / 2;
            for (int i = 0; i < gangs.Count; i++)
            {
                var d = new District
                {
                    Gang = gangs[i], Name = Gangs.NewDistrictOf(gangs[i]), Color = Gangs.ColorOf(gangs[i]),
                    Centre = new Vector3(x0 + (i / 2) * (Size + Gap), ground, z0 + (i % 2) * (Size + Gap)),
                };
                _districts.Add(d);
                Lay(d, groundMat, roadMat, roadWidth, buildings, props, rnd);
            }
            // a road that ties the districts together, the length of the row
            float len = ((gangs.Count + 1) / 2) * (Size + Gap) + 120f;
            Slab("Frankenstein road", new Vector3(x0 - 60f + len / 2 - Size / 2, ground - 0.05f, town.center.z), new Vector3(len, 0.2f, roadWidth), roadMat, new Color(0.25f, 0.25f, 0.27f));
            _built = true;
            Note = _districts.Count + " new districts east of town, " + _made.Count + " pieces. F8 > Travel to go there.";
            Melon<Plugin>.Logger.Msg("Expansion: " + Note + " (" + buildings.Count + " building shapes, roads " + roadWidth.ToString("0.#") + " m wide, ground at " + ground.ToString("0.#") + ")");
        }

        /// <summary>The town's buildings as plain shells: big meshes the size of a house or a shop.</summary>
        static List<GameObject> Shells(List<MeshRenderer> all)
        {
            var seen = new HashSet<string>();
            var out_ = new List<GameObject>();
            foreach (var r in all)
            {
                var b = r.bounds;
                if (b.size.y < 5 || b.size.y > 35 || b.size.x < 8 || b.size.z < 8 || b.size.x > 45 || b.size.z > 45) continue;
                var n = r.gameObject.name.ToLowerInvariant();
                if (n.Contains("road") || n.Contains("terrain") || n.Contains("water") || n.Contains("frankenstein")) continue;
                var key = (r.GetComponent<MeshFilter>()?.sharedMesh?.name ?? n);
                if (!seen.Add(key)) continue;
                out_.Add(r.gameObject);
                if (out_.Count >= 40) break;
            }
            return out_;
        }

        static GameObject Slab(string name, Vector3 at, Vector3 size, Material mat, Color tint)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);  // its box collider stays: you walk and drive on it
            g.name = name;
            g.transform.position = at;
            g.transform.localScale = size;
            var r = g.GetComponent<MeshRenderer>();
            if (r != null && mat != null)
            {
                var m = new Material(mat);
                if (tint.a > 0) { m.color = tint; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); }
                r.sharedMaterial = m;
            }
            _made.Add(g);
            return g;
        }

        static GameObject Copy(GameObject src, Vector3 at, float yaw)
        {
            var c = UnityEngine.Object.Instantiate(src);
            c.name = "Frankenstein " + src.name;
            // only what you see and bump into: the game's scripts, network parts and doors don't come along
            foreach (var mb in Rt.Comps(c, typeof(MonoBehaviour), true))
                if (mb is UnityEngine.Object o) UnityEngine.Object.DestroyImmediate(o);
            c.transform.rotation = Quaternion.Euler(0, yaw, 0);
            var r = c.GetComponentInChildren<MeshRenderer>();
            // stand it on the ground at the spot, whatever its pivot
            c.transform.position = at;
            if (r != null)
            {
                var b = r.bounds;
                c.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            }
            c.SetActive(true);
            _made.Add(c);
            return c;
        }

        static void Lay(District d, Material groundMat, Material roadMat, float roadWidth, List<GameObject> shells, List<GameObject> props, System.Random rnd)
        {
            var c = d.Centre;
            Slab("Frankenstein ground " + d.Name, c + new Vector3(0, -0.6f, 0), new Vector3(Size, 1f, Size), groundMat, new Color(0, 0, 0, 0));
            // a crossroads, the town's width
            Slab("Frankenstein road", c + new Vector3(0, -0.05f, 0), new Vector3(Size, 0.2f, roadWidth), roadMat, new Color(0.25f, 0.25f, 0.27f));
            Slab("Frankenstein road", c + new Vector3(0, -0.05f, 0), new Vector3(roadWidth, 0.2f, Size), roadMat, new Color(0.25f, 0.25f, 0.27f));
            // buildings along both roads, facing them
            if (shells.Count > 0)
            {
                float set = roadWidth / 2 + 16f;
                foreach (var along in new[] { -55f, -25f, 25f, 55f })
                {
                    Copy(shells[rnd.Next(shells.Count)], c + new Vector3(along, 0, set), 180f);
                    Copy(shells[rnd.Next(shells.Count)], c + new Vector3(along, 0, -set), 0f);
                    Copy(shells[rnd.Next(shells.Count)], c + new Vector3(set, 0, along), 270f);
                    Copy(shells[rnd.Next(shells.Count)], c + new Vector3(-set, 0, along), 90f);
                }
            }
            // the gang's colours on the corners, and the town's props around them
            foreach (var corner in new[] { new Vector3(1, 0, 1), new Vector3(-1, 0, 1), new Vector3(1, 0, -1), new Vector3(-1, 0, -1) })
            {
                var at = c + corner * (roadWidth / 2 + 3f);
                Slab("Frankenstein flag", at + new Vector3(0, 2.5f, 0), new Vector3(0.15f, 5f, 0.15f), groundMat, new Color(0.15f, 0.15f, 0.15f));
                Slab("Frankenstein flag", at + new Vector3(0.6f, 4.4f, 0), new Vector3(1.2f, 0.8f, 0.05f), groundMat, d.Color);
                if (props.Count > 0) Copy(props[rnd.Next(props.Count)], at + corner * 4f, rnd.Next(360));
            }
        }

        // ---------- getting there ----------
        public static void Travel(int i)
        {
            var me = Player.Local;
            if (me == null || i < 0 || i >= _districts.Count) return;
            try
            {
                if (me.IsInVehicle) { ItemMenu.Notify("Get out of the car first."); return; }
                me.Position = _districts[i].Centre + new Vector3(8f, 1.5f, 8f);
                ItemMenu.Notify("Welcome to " + _districts[i].Name + ".");
            }
            catch (Exception e) { if (!_warned) { _warned = true; Melon<Plugin>.Logger.Warning("Expansion travel: " + e.Message); } }
        }

        public static string NameAt(int i) => i >= 0 && i < _districts.Count ? _districts[i].Name + " (" + Gangs.NameOf(_districts[i].Gang) + ")" : "";

        public static void Draw()
        {
            if (_here.Length > 0 && Time.unscaledTime < _hereUntil)
                GUI.Box(new Rect(Screen.width / 2f - 200, 80, 400, 30), _here.ToUpperInvariant());
        }
    }
}
