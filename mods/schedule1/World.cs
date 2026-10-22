using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The Extension game built into Schedule I with Schedule I's own engine and props (F8 in game):
    /// the merge's zone (for Hood Warfare, "the Block": a street corner made of the town's own dumpsters, fences,
    /// crates and barriers, placed where you stand and rebuilt every session), the Extension's lighting, its game
    /// mode (Hold the block), its HUD and a merged title card when the town loads.</summary>
    internal static class World
    {
        public static string Style = "", Zone = "", Lighting = "", Title = "", Host = "Schedule I";
        public static List<string> Modes = new List<string>();
        static bool _open, _light, _built, _inTown;
        static string _confirm = "";
        static float _hintUntil;
        static Vector3 _spot;
        static bool _hasSpot;
        static readonly List<GameObject> _made = new List<GameObject>();
        static float _cardUntil, _rebuildAt = -1, _nextLight;
        static float _holdStart = -1, _offSince = -1;
        static string _note = "";
        static GUIStyle _big;

        const float HoldSeconds = 180, Radius = 20;
        static readonly string[] Props = { "dumpster", "trash", "bin", "fence", "barrier", "crate", "pallet", "cone", "bench", "barrel", "tire", "sign" };

        static string File_ => Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "block.json");

        public static void Load()
        {
            try
            {
                if (!File.Exists(File_)) return;
                var s = MiniJson.Parse(File.ReadAllText(File_));
                _spot = new Vector3((float)J.N(s, "x"), (float)J.N(s, "y"), (float)J.N(s, "z"));
                _hasSpot = true;
            }
            catch (Exception) { }
        }

        public static void Scene(string name)
        {
            _inTown = name != null && name.Length > 0 && name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) < 0;
            if (_inTown) _hintUntil = Time.unscaledTime + 25f;
            Carry.Scene(_inTown);
            Gangs.Scene(_inTown);
            if (!_inTown || Style.Length == 0) return;
            _cardUntil = Time.unscaledTime + 6f;  // the merged title card
            _made.Clear();
            _built = false;
            if (_hasSpot) _rebuildAt = Time.unscaledTime + 12f;  // the town needs a moment before its props exist
        }

        static void Save()
        {
            try { File.WriteAllText(File_, "{\"x\": " + _spot.x + ", \"y\": " + _spot.y + ", \"z\": " + _spot.z + "}"); }
            catch (Exception) { }
        }

        // ---------- the zone, from the town's own props ----------
        internal static List<GameObject> Kinds()
        {
            var seen = new HashSet<string>();
            var out_ = new List<GameObject>();
            foreach (var r in Rt.All<MeshRenderer>())
            {
                if (r == null || out_.Count >= 14) continue;
                var go = r.gameObject;
                var n = go.name.ToLowerInvariant();
                foreach (var k in Props)
                {
                    if (!n.Contains(k) || seen.Contains(k)) continue;
                    seen.Add(k);
                    out_.Add(go);
                    break;
                }
            }
            return out_;
        }

        static int Build()
        {
            foreach (var g in _made) if (g != null) UnityEngine.Object.Destroy(g);
            _made.Clear();
            var kinds = Kinds();
            if (kinds.Count == 0) return 0;
            // a square street corner: props along three sides, open toward the road
            int i = 0;
            for (float t = -Radius; t <= Radius; t += 4f)
            {
                foreach (var side in new[] { new Vector3(t, 0, Radius), new Vector3(-Radius, 0, t), new Vector3(Radius, 0, t) })
                {
                    var src = kinds[i++ % kinds.Count];
                    var copy = UnityEngine.Object.Instantiate(src, _spot + side, Quaternion.Euler(0, (i * 37) % 360, 0));
                    copy.name = "Frankenstein block";
                    _made.Add(copy);
                }
            }
            _built = true;
            return _made.Count;
        }

        // ---------- the Extension's light ----------
        static void Light()
        {
            if (!_light || Time.unscaledTime < _nextLight) return;
            _nextLight = Time.unscaledTime + 3f;  // the game's day cycle keeps changing the light, so keep it tinted
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.32f, 0.18f, 0.46f);
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.ambientLight = new Color(0.42f, 0.34f, 0.58f);
        }

        static bool OnBlock()
        {
            var me = Player.Local;
            return me != null && _hasSpot && Vector3.Distance(me.Position, _spot) < Radius + 4;
        }

        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8) && _inTown) { _open = !_open; _confirm = ""; }
            if (Style.Length == 0) return;
            if (_rebuildAt > 0 && Time.unscaledTime >= _rebuildAt) { _rebuildAt = -1; try { Build(); } catch (Exception e) { Warn(e); } }
            try { Light(); } catch (Exception e) { Warn(e); _light = false; }
            if (_holdStart < 0) return;
            if (OnBlock()) _offSince = -1;
            else if (_offSince < 0) _offSince = Time.unscaledTime;
            else if (Time.unscaledTime - _offSince > 10) { _holdStart = -1; _note = "You left the block. Hold the block failed."; return; }
            if (Time.unscaledTime - _holdStart >= HoldSeconds)
            {
                _holdStart = -1;
                try { S1API.Money.Money.ChangeCashBalance(1500, true, true); } catch (Exception) { }
                _note = "You held the block! +$1500";
                ItemMenu.Notify(_note);
            }
        }

        static void DrawPanel()
        {
            if (!_open) return;
            float pw = 380, px = 40, py = Screen.height * 0.25f;
            // Creative, from inside the game: confirm, then the game restarts into Creative's own saves
            string to = MainMenu.Creative ? "normal" : "creative";
            float top = Style.Length > 0 ? 300 : 110;
            GUI.Box(new Rect(px, py - top + 64, pw, top - 64 + (Style.Length > 0 ? 250 : 0)), "");
            float cy = py - top + 72;
            GUI.Label(new Rect(px + 12, cy, pw - 24, 22), "FRANKENSTEIN  ·  " + (MainMenu.Merge.Length > 0 ? MainMenu.Merge : "this merge") + (MainMenu.Creative ? "  ·  Creative" : ""));
            if (_confirm == to)
            {
                GUI.Label(new Rect(px + 12, cy + 22, pw - 24, 36), "Save first (sleep, or the pause menu). Then the game restarts in " + (to == "creative" ? "Creative, with its own saves." : "your normal game."));
                if (GUI.Button(new Rect(px + 12, cy + 58, (pw - 30) / 2, 28), "Yes, switch now")) { _confirm = ""; MainMenu.Switch(to); }
                if (GUI.Button(new Rect(px + 18 + (pw - 30) / 2, cy + 58, (pw - 30) / 2, 28), "Cancel")) _confirm = "";
            }
            else if (GUI.Button(new Rect(px + 12, cy + 26, pw - 24, 30), MainMenu.Creative ? "Back to my normal game" : "Switch to Creative (every item, its own saves)")) _confirm = to;
            Carry.Draw(px + pw + 12, py - top + 64, 420);
            if (Style.Length == 0) return;
            GUI.Label(new Rect(px + 12, py + 8, pw - 24, 22), Title + " in " + Host);
            GUI.Label(new Rect(px + 12, py + 32, pw - 24, 40), Zone);
            if (GUI.Button(new Rect(px + 12, py + 76, pw - 24, 30), _hasSpot ? "Move the block here" : "Build the block here"))
            {
                var me = Player.Local;
                if (me != null)
                {
                    _spot = me.Position; _hasSpot = true; Save();
                    try { _note = Build() > 0 ? "The block is built from the town's own props." : "Couldn't find props to build with here."; }
                    catch (Exception e) { Warn(e); _note = "Couldn't build here."; }
                }
            }
            if (GUI.Button(new Rect(px + 12, py + 112, pw - 24, 30), _light ? "Light: back to normal" : "Light: " + (Lighting.Length > 0 ? Lighting : "the Extension's look")))
            {
                _light = !_light;
                if (!_light) RenderSettings.fog = false;
            }
            if (GUI.Button(new Rect(px + 12, py + 148, pw - 24, 30), _holdStart >= 0 ? "Stop holding the block" : "Hold the block (3 minutes, $1500)"))
            {
                if (_holdStart >= 0) _holdStart = -1;
                else if (!_hasSpot) _note = "Build the block first.";
                else { _holdStart = Time.unscaledTime; _offSince = -1; _note = ""; }
            }
            GUI.Label(new Rect(px + 12, py + 186, pw - 24, 56), _built ? "Untested in game: tell Frankenstein's maker how it looks." : "F8 closes this.");
        }

        static void Warn(Exception e) => Melon<Plugin>.Logger.Warning("World: " + e.Message);

        public static void Draw()
        {
            if (!_inTown) return;
            if (Time.unscaledTime < _hintUntil)
                GUI.Box(new Rect(16, Screen.height - 70, 420, 28), MainMenu.Creative ? "Creative · F8: back to your normal game" : "F8: Frankenstein menu · switch to Creative");
            DrawPanel();
            if (Style.Length == 0) return;
            if (_big == null) _big = new GUIStyle(GUI.skin.box) { fontSize = 46 };  // the box style centres its text
            if (Time.unscaledTime < _cardUntil)
            {
                var a = Mathf.Clamp01((_cardUntil - Time.unscaledTime) / 1.5f);
                var old = GUI.color;
                GUI.color = new Color(1, 1, 1, a);
                GUI.Box(new Rect(0, Screen.height * 0.36f, Screen.width, 150), "");
                GUI.Label(new Rect(0, Screen.height * 0.36f + 10, Screen.width, 70), Host.ToUpperInvariant() + "  ×  " + Title.ToUpperInvariant(), _big);
                GUI.Label(new Rect(0, Screen.height * 0.36f + 90, Screen.width, 40), MainMenu.Merge + "   ·   F8: " + Title + " menu");
                GUI.color = old;
            }
            // the Extension's HUD: turf, mode, cash
            float w = 300, x = Screen.width - w - 16, y = 16;
            GUI.Box(new Rect(x, y, w, 74), "");
            GUI.Label(new Rect(x + 10, y + 4, w - 20, 22), Title.ToUpperInvariant() + "  ×  " + Host.ToUpperInvariant());
            string turf = !_hasSpot ? "No turf yet (F8: build the block)" : OnBlock() ? "On your block" : "Off turf";
            GUI.Label(new Rect(x + 10, y + 26, w - 20, 22), "Turf: " + turf);
            string mode = _holdStart >= 0 ? string.Format("Hold the block: {0:0}s left", HoldSeconds - (Time.unscaledTime - _holdStart)) : _note;
            GUI.Label(new Rect(x + 10, y + 48, w - 20, 22), mode);
        }

    }
}
