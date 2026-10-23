using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Milestone 1's test of the new world: a city block built from real models (the downloaded CC0 city
    /// packs), placed where you stand from the F8 menu and kept with your save. It shows the three things the big
    /// map needs: real models with collision you can walk and drive on, streaming (the whole district switches off
    /// when you're far away), and levels of detail (simple versions of the buildings far off, the full ones up
    /// close). It also checks whether the game's NPCs could walk there (their navigation mesh). Everything it did
    /// goes to districts-debug.txt.</summary>
    internal static class Districts
    {
        sealed class Lot { public GameObject Full, Far; public Vector3 At; }

        static GameObject _root;
        static readonly List<Lot> _lots = new List<Lot>();
        static Vector3 _spot;
        static float _yaw, _next, _scale = 1f;
        static bool _has, _building, _inTown;
        static string _file = "", _note = "";
        static int _step;
        static readonly List<string> _log = new List<string>();
        public static string Note => _note;
        public static bool Has => _has;

        const float Cell = 22f, Detail = 220f, Reach = 650f;

        public static void Scene(bool inTown)
        {
            _inTown = inTown;
            Clear();
            _file = "";
        }

        static string File_ => Mode.DataFile("district");

        static void Load()
        {
            if (_file == File_) return;
            _file = File_;
            _has = false;
            try
            {
                if (!File.Exists(_file)) return;
                var d = MiniJson.Parse(File.ReadAllText(_file));
                _spot = new Vector3((float)J.N(d, "x"), (float)J.N(d, "y"), (float)J.N(d, "z"));
                _yaw = (float)J.N(d, "yaw");
                _has = true;
                _building = true;
                _step = 0;
            }
            catch (Exception) { }
        }

        static void Store()
        {
            try { File.WriteAllText(File_, MiniJson.Write(new Dictionary<string, object> { ["x"] = (double)_spot.x, ["y"] = (double)_spot.y, ["z"] = (double)_spot.z, ["yaw"] = (double)_yaw })); }
            catch (Exception) { }
        }

        /// <summary>F8: put the test block where you stand, facing the way you look.</summary>
        public static void PlaceHere()
        {
            var me = Player.Local;
            if (me == null) return;
            _spot = me.Position + Quaternion.Euler(0, Camera.main != null ? Camera.main.transform.eulerAngles.y : 0, 0) * new Vector3(0, 0, Cell * 2.2f);
            _yaw = Camera.main != null ? Mathf.Round(Camera.main.transform.eulerAngles.y / 90f) * 90f : 0;
            _has = true;
            Store();
            Clear();
            _building = true;
            _step = 0;
            _note = "Building the test block...";
        }

        public static void Remove()
        {
            Clear();
            _has = false;
            try { if (File.Exists(File_)) File.Delete(File_); } catch (Exception) { }
            _note = "The test block is gone.";
        }

        static void Clear()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _lots.Clear();
            _building = false;
        }

        static readonly string[] Tall = { "skyscraper", "building-skyscraper" };

        /// <summary>One lot per frame, so building never stalls the game. Waits while models are still loading.</summary>
        public static void Update()
        {
            if (!_inTown || Player.Local == null) return;
            Load();
            if (_building) { Build(); return; }
            if (_root == null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Perf.Every(0.5f);
            Stream();
        }

        static void Build()
        {
            var buildings = global::Frankenstein.Models.Catalog.Where(e => e.Kind == "buildings" && !e.Name.StartsWith("low-detail", StringComparison.OrdinalIgnoreCase)).ToList();
            if (buildings.Count == 0)
            {
                _building = false;
                _note = "No city models on this PC yet: the app downloads them (Settings > Graphics and models), then press Play again.";
                Say(_note);
                return;
            }
            if (_root == null)
            {
                _root = new GameObject("Frankenstein district: test block");
                _root.transform.position = _spot;
                _root.transform.rotation = Quaternion.Euler(0, _yaw, 0);
                _scale = 0;
            }
            // the city kits' buildings are about a metre to a tile: one tile becomes most of a 22 m lot
            if (_scale == 0)
            {
                var probe = global::Frankenstein.Models.Template(buildings[0]);
                if (probe == null) { if (global::Frankenstein.Models.Failed(buildings[0])) { _building = false; _note = "The city models couldn't be read (models-debug.txt says why)."; Say(_note); } return; }
                var b = global::Frankenstein.Models.LocalBounds(probe, probe.transform);
                _scale = Mathf.Clamp(Cell * 0.8f / Mathf.Max(0.1f, Mathf.Max(b.size.x, b.size.z)), 0.5f, 40f);
                Say("city models: " + buildings.Count + " buildings, first " + buildings[0].Name + " is " + b.size.ToString("0.00") + " units, scaled x" + _scale.ToString("0.0"));
            }
            int n = 5;  // a 5 x 5 block: buildings round the edge, a crossroads through the middle
            if (_step >= n * n) { Finish(); return; }
            int i = _step % n, j = _step / n;
            if (i == 2 || j == 2) { _step++; return; }  // the streets
            var rnd = new System.Random(_step * 7919 + 13);
            bool centre = Math.Abs(i - 2) <= 1 && Math.Abs(j - 2) <= 1;
            var pool = centre ? buildings.Where(e => Tall.Any(t => e.Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)).ToList() : buildings;
            if (pool.Count == 0) pool = buildings;
            var pick = pool[rnd.Next(pool.Count)];
            var local = new Vector3((i - 2) * Cell, 0, (j - 2) * Cell);
            var at = _root.transform.TransformPoint(local);
            at.y = Ground(at);
            var rot = _root.transform.rotation * Quaternion.Euler(0, 90 * rnd.Next(4), 0);
            var full = global::Frankenstein.Models.Spawn(pick, at, rot, _root.transform);
            if (full == null) { if (global::Frankenstein.Models.Failed(pick)) _step++; return; }  // still loading: the same lot next frame
            full.transform.localScale = Vector3.one * _scale;
            full.transform.position = at;
            int solid = global::Frankenstein.Models.Solid(full);
            var farEntry = global::Frankenstein.Models.Pick("buildings", "low-detail-" + pick.Name);
            GameObject far = null;
            if (farEntry != null && farEntry.Name.StartsWith("low-detail", StringComparison.OrdinalIgnoreCase))
            {
                far = global::Frankenstein.Models.Spawn(farEntry, at, rot, _root.transform);
                if (far == null && !global::Frankenstein.Models.Failed(farEntry)) { UnityEngine.Object.Destroy(full); return; }
                far.transform.localScale = Vector3.one * _scale;
                far.transform.position = at;
                far.SetActive(false);
            }
            _lots.Add(new Lot { Full = full, Far = far, At = at });
            Say("lot " + i + "," + j + ": " + pick.Name + (far != null ? " (+ far version)" : "") + ", " + solid + " colliders");
            _step++;
        }

        static void Finish()
        {
            _building = false;
            _note = "Test block built: " + _lots.Count + " buildings from real models.";
            Say(_note + " Streaming: off beyond " + Reach + " m, simple versions beyond " + Detail + " m.");
            NavProbe();
        }

        /// <summary>Distance streaming: the district off when you're far, far versions of the buildings at mid range.</summary>
        static void Stream()
        {
            var me = Player.Local;
            if (me == null) return;
            float d = Vector3.Distance(me.Position, _spot);
            bool on = d < Reach;
            if (_root.activeSelf != on) { _root.SetActive(on); Say((on ? "streamed in" : "streamed out") + " at " + d.ToString("0") + " m"); }
            if (!on) return;
            float detail = Detail * (Perf.Level == 0 ? 1f : Perf.Level == 1 ? 0.7f : 0.45f);
            foreach (var lot in _lots)
            {
                if (lot.Full == null) continue;
                bool near = lot.Far == null || Vector3.Distance(me.Position, lot.At) < detail;
                if (lot.Full.activeSelf != near) lot.Full.SetActive(near);
                if (lot.Far != null && lot.Far.activeSelf == near) lot.Far.SetActive(!near);
            }
        }

        static float Ground(Vector3 p)
        {
            // straight down from above onto whatever's there (the town's ground or terrain)
            try
            {
                var phys = Rt.Find("UnityEngine.Physics");
                var ray = phys?.GetMethods().FirstOrDefault(m => m.Name == "Raycast" && m.GetParameters().Length == 4 && m.GetParameters()[1].ParameterType == typeof(Vector3) && m.GetParameters()[2].IsOut);
                if (ray != null)
                {
                    var args = new object[] { p + Vector3.up * 200f, Vector3.down, null, 500f };
                    if ((bool)ray.Invoke(null, args)) { var pt = Rt.Get(args[2], "point"); if (pt is Vector3 v) return v.y; }
                }
            }
            catch (Exception) { }
            return p.y;
        }

        /// <summary>Can the game's NPCs walk on the block? The answer decides how milestone 2 brings people to new land.</summary>
        static void NavProbe()
        {
            try
            {
                var nav = Rt.Find("UnityEngine.AI.NavMesh");
                var sample = nav?.GetMethods().FirstOrDefault(m => m.Name == "SamplePosition" && m.GetParameters().Length == 4);
                if (sample == null) { Say("NPC walking: no NavMesh in this build"); return; }
                int on = 0;
                foreach (var lot in _lots.Take(8))
                {
                    var street = lot.At + (_spot - lot.At).normalized * (Cell * 0.55f);
                    var args = new object[] { street, null, 3f, -1 };
                    if ((bool)sample.Invoke(null, args)) on++;
                }
                Say("NPC walking: " + on + " of " + Math.Min(8, _lots.Count) + " street spots are on the game's navigation mesh" +
                    (on == 0 ? " (new land needs its own navigation mesh for NPCs: milestone 2 builds it)" : ""));
            }
            catch (Exception e) { Say("NPC walking check failed: " + (e.InnerException ?? e).Message); }
        }

        static void Say(string line)
        {
            _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
            if (_log.Count > 300) _log.RemoveRange(0, _log.Count - 300);
            try
            {
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "districts-debug.txt"), new[] { "Frankenstein " + Plugin.Version }.Concat(_log).ToArray());
            }
            catch (Exception) { }
        }
    }
}
