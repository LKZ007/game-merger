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
    /// <summary>Schedule I's main menu, redrawn: big cards (Continue "Load your world", New Game "Start a new world",
    /// Creative "Unlimited resources", Settings, Quit) with a picture of the merge on the right, like the mockup.
    /// Drawn by Frankenstein itself over the game's menu, so it doesn't depend on copying the game's own rows (which
    /// this build of the game wouldn't take). The game's own list is shrunk out of sight, and each card presses the
    /// game's own button, so Continue, New Game, Settings and Quit do exactly what the game does. Creative starts a
    /// new world in Creative (see Mode). Mouse, keyboard (arrows, Enter) and controller (d-pad or stick, A).</summary>
    internal static class MenuSkin
    {
        class Card
        {
            public string Key, Title, Sub, Icon;
            public object Button;   // the game's own button this card presses
        }

        static readonly List<Card> _cards = new List<Card>();
        static Transform _list;
        static Vector3 _listScale;
        static GameObject _anchor;   // the game's New Game text: while it's on screen, the menu is
        static float _scanAt, _until;
        static int _sel;
        static bool _found, _navOff;
        static object _eventSystem;
        static Texture2D _art;
        static readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        static GUIStyle _title, _sub, _chev, _cap, _capSub, _small;
        static string _note = "";

        public static bool Active => _found && Showing;
        public static bool Found => _found;
        static bool Showing => _anchor != null && _anchor.activeInHierarchy && !Loading.Showing;

        public static void Scene(bool inMenu)
        {
            Restore();
            _cards.Clear();
            _found = false;
            _anchor = null;
            _list = null;
            _sel = 0;
            _note = "";
            _scanAt = inMenu ? Time.unscaledTime + 0.5f : -1;
            _until = inMenu ? Time.unscaledTime + 30f : -1;
        }

        static string Clean(object t) => System.Text.RegularExpressions.Regex.Replace(Rt.Text(t) ?? "", "<[^>]+>", "").Trim().ToLowerInvariant();

        static object ButtonOf(GameObject go)
        {
            var bt = Rt.Find("UnityEngine.UI.Button");
            for (var t = go != null ? go.transform : null; t != null && bt != null; t = t.parent)
            {
                var b = Rt.Comp(t.gameObject, bt);
                if (b != null) return b;
            }
            return null;
        }

        static void Scan()
        {
            var texts = Rt.Texts().Select(t => new { t, s = Clean(t) }).Where(x => x.s.Length > 0).ToList();
            var want = new[]
            {
                new[] { "continue", "Continue", "Load your world", "play" },
                new[] { "new game", "New Game", "Start a new world", "cube" },
                new[] { "", "Creative", "Unlimited resources", "brush" },
                new[] { "settings", "Settings", "Adjust your game", "gear" },
                new[] { "credits", "Credits", "Who made it", "" },
                new[] { "quit", "Quit", "Exit to desktop", "exit" },
            };
            var made = new List<Card>();
            GameObject anchor = null;
            foreach (var w in want)
            {
                if (w[0].Length == 0) { made.Add(new Card { Key = "creative", Title = w[1], Sub = w[2], Icon = w[3] }); continue; }
                var hit = texts.FirstOrDefault(x => x.s == w[0] || x.s.Replace(" ", "") == w[0].Replace(" ", ""));
                if (hit == null) continue;
                var go = Rt.GameObjectOf(hit.t);
                if (w[0] == "new game") anchor = go;
                made.Add(new Card { Key = w[0], Title = w[1], Sub = w[2], Icon = w[3], Button = ButtonOf(go) });
            }
            if (anchor == null) return;  // not the main menu yet
            _anchor = anchor;
            _cards.Clear();
            _cards.AddRange(made);
            // the game's own list: the parent that holds New Game and Settings (or Quit)
            var other = made.Where(c => c.Key == "settings" || c.Key == "quit").Select(c => c.Button as Component).FirstOrDefault(c => c != null);
            var row = anchor.transform;
            if (other != null) while (row.parent != null && !other.transform.IsChildOf(row.parent)) row = row.parent;
            _list = row.parent;
            if (_list != null) { _listScale = _list.localScale; _list.localScale = Vector3.zero; }  // out of sight, still there to press
            NavOff(true);
            _found = true;
            _sel = 0;
            Melon<Plugin>.Logger.Msg("Main menu: Frankenstein's menu is up (" + string.Join(", ", _cards.Select(c => c.Title + (c.Button != null || c.Key == "creative" ? "" : " (no button)")).ToArray()) + ").");
            try
            {
                var lines = new List<string>();
                for (var t = anchor.transform; t != null; t = t.parent) lines.Add(t.name + " children=" + t.childCount);
                File.WriteAllText(Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "menu-debug.txt"),
                                  "cards: " + string.Join(", ", _cards.Select(c => c.Title + (c.Button != null ? " (button)" : "")).ToArray()) + "\n" + string.Join("\n", lines.ToArray()));
            }
            catch (Exception) { }
        }

        /// <summary>The game's own menu keys off while Frankenstein's is up, so A or Enter can't also press a hidden row.</summary>
        static void NavOff(bool off)
        {
            try
            {
                if (_eventSystem == null || (_eventSystem is UnityEngine.Object o && o == null))
                    _eventSystem = Rt.All(Rt.Find("UnityEngine.EventSystems.EventSystem")).FirstOrDefault();
                if (_eventSystem == null) return;
                Rt.Set(_eventSystem, !off, "sendNavigationEvents");
                _navOff = off;
            }
            catch (Exception) { }
        }

        static void Restore()
        {
            if (_list != null) { try { _list.localScale = _listScale; } catch (Exception) { } }
            if (_navOff) NavOff(false);
        }

        static bool Press(object button)
        {
            if (button == null) return false;
            try
            {
                var ev = Rt.Get(button, "onClick");
                var inv = ev?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(m => m.Name == "Invoke" && m.GetParameters().Length == 0);
                if (inv != null) { inv.Invoke(ev, null); return true; }
            }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Main menu: pressing the game's button failed: " + e.Message); }
            return false;
        }

        static void Choose(Card c)
        {
            if (c.Key == "creative")
            {
                // a new world in Creative: the next game you start gets it, then the game's own New Game screen
                Mode.Pending = "creative";
                var ng = _cards.FirstOrDefault(x => x.Key == "new game");
                _note = "Creative: the new world you start now is Creative.";
                if (ng == null || !Press(ng.Button)) _note = "Creative is on for the next game you start or load.";
                return;
            }
            if (c.Key == "new game" && Mode.Pending == "creative") Mode.Pending = "";  // New Game itself is Survival
            if (!Press(c.Button))
            {
                // the button couldn't be pressed from here: show the game's own list again
                _note = "Couldn't press " + c.Title + " from here, so the game's own menu is back.";
                Restore();
                _found = false;
                _until = -1;
            }
        }

        public static void Update()
        {
            if (_scanAt > 0 && !_found && Time.unscaledTime >= _scanAt)
            {
                _scanAt = Time.unscaledTime + 0.5f;
                try { Scan(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Main menu: " + e.Message); }
                if (!_found && Time.unscaledTime > _until) { _scanAt = -1; Melon<Plugin>.Logger.Msg("Main menu: couldn't find New Game in 30 seconds; Frankenstein's corner panel shows instead."); }
            }
            if (!_found) return;
            if (_anchor == null) { Scene(true); return; }  // the menu was rebuilt
            bool showing = Showing;
            if (showing != !_navOff) NavOff(showing);
            if (!showing || _cards.Count == 0) return;
            int n = _cards.Count;
            if ((Pad.Pressed & Pad.Down) != 0 || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) _sel = (_sel + 1) % n;
            if ((Pad.Pressed & Pad.Up) != 0 || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) _sel = (_sel + n - 1) % n;
            if (_stickReady && Mathf.Abs(Pad.Stick.y) > 0.6f) { _sel = (_sel + (Pad.Stick.y < 0 ? 1 : n - 1)) % n; _stickReady = false; }
            if (Mathf.Abs(Pad.Stick.y) < 0.3f) _stickReady = true;
            if ((Pad.Pressed & Pad.A) != 0 || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Choose(_cards[_sel]);
        }

        static bool _stickReady = true;
        static Vector2 _lastMouse;

        // ---------- drawing ----------

        static Texture2D Icon(string kind)
        {
            if (kind.Length == 0) return null;
            Texture2D t;
            if (_icons.TryGetValue(kind, out t)) return t;
            const int S = 64;
            t = new Texture2D(S, S);
            var clear = new Color(0, 0, 0, 0);
            var w = new Color(0.92f, 0.95f, 0.96f, 1);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float fx = x - 31.5f, fy = y - 31.5f, r = Mathf.Sqrt(fx * fx + fy * fy);
                    bool on = false;
                    switch (kind)
                    {
                        case "play": on = x > 18 && x < 50 && Mathf.Abs(fy) < (50 - x) * 0.62f; break;
                        case "cube":
                            on = Mathf.Abs(fx) + Mathf.Abs(fy) * 1.7f < 30 && (Mathf.Abs(fx) > 2 || fy > 0) && !(fy > -1 && fy < 2 && Mathf.Abs(fx) < 26);
                            break;
                        case "brush": { float d = Mathf.Abs(fx + fy) / 1.414f; on = d < 5 && fx - fy > -40 && fx - fy < 30 || (r < 9 && fx < -14 && fy < -14); } break;
                        case "gear": { float a = Mathf.Atan2(fy, fx); float teeth = Mathf.Cos(a * 8) > 0.3f ? 29 : 23; on = r < teeth && r > 10; } break;
                        case "exit": on = (x > 10 && x < 40 && (y < 16 || y > 48 || x < 16) && y > 10 && y < 54) || (Mathf.Abs(fy) < 3 && x > 24 && x < 56) || (x > 44 && x < 56 && Mathf.Abs(fy) < (56 - x)); break;
                    }
                    t.SetPixel(x, y, on ? w : clear);
                }
            t.Apply();
            _icons[kind] = t;
            return t;
        }

        static void Styles(float k)
        {
            if (_title != null && _title.fontSize == (int)(30 * k)) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = (int)(30 * k) };
            Enum(_title, "fontStyle", 1);  // bold (the enum's assembly isn't one Frankenstein builds against)
            _title.normal.textColor = Color.white;
            _sub = new GUIStyle(GUI.skin.label) { fontSize = (int)(16 * k) };
            _sub.normal.textColor = new Color(0.68f, 0.78f, 0.82f);
            _chev = new GUIStyle(GUI.skin.label) { fontSize = (int)(30 * k) };
            Enum(_chev, "alignment", 4);  // middle centre
            _chev.normal.textColor = new Color(0.75f, 0.8f, 0.85f);
            _cap = new GUIStyle(GUI.skin.label) { fontSize = (int)(22 * k), wordWrap = true };
            Enum(_cap, "fontStyle", 1);
            _cap.normal.textColor = Color.white;
            _capSub = new GUIStyle(GUI.skin.label) { fontSize = (int)(15 * k), wordWrap = true };
            _capSub.normal.textColor = new Color(0.68f, 0.78f, 0.82f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = (int)(14 * k), wordWrap = true };
            _small.normal.textColor = new Color(0.75f, 0.85f, 0.8f);
        }

        static void Enum(object o, string prop, int value)
        {
            try
            {
                var p = o.GetType().GetProperty(prop);
                p?.SetValue(o, System.Enum.ToObject(p.PropertyType, value), null);
            }
            catch (Exception) { }
        }

        static void Frame(Rect r, Color line, float th)
        {
            Paint.Fill(new Rect(r.x, r.y, r.width, th), line);
            Paint.Fill(new Rect(r.x, r.yMax - th, r.width, th), line);
            Paint.Fill(new Rect(r.x, r.y, th, r.height), line);
            Paint.Fill(new Rect(r.xMax - th, r.y, th, r.height), line);
        }

        public static void Draw()
        {
            if (!Active) return;
            float k = Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);
            Styles(k);
            var green = new Color(0.25f, 0.86f, 0.3f);
            float x = Screen.width * 0.06f, y = Screen.height * 0.33f, w = 560 * k, h = 82 * k, gap = 14 * k;
            var m = Event.current.mousePosition;
            bool moved = (m - _lastMouse).sqrMagnitude > 1f;
            if (Event.current.type == EventType.Repaint) _lastMouse = m;
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                bool small = c.Key == "credits";
                var r = new Rect(x, y, small ? w * 0.5f : w, small ? h * 0.55f : h);
                y += r.height + gap;
                if (moved && r.Contains(m)) _sel = i;
                bool on = i == _sel;
                Paint.Fill(r, on ? new Color(0.1f, 0.42f, 0.14f, 0.92f) : new Color(0.04f, 0.07f, 0.09f, 0.86f));
                if (on) Paint.Fill(new Rect(r.x, r.y, r.width * 0.22f, r.height), new Color(0.02f, 0.05f, 0.03f, 0.55f));
                Frame(r, on ? green : new Color(0.32f, 0.4f, 0.46f, 0.9f), on ? 3 * k : 2 * k);
                if (small)
                {
                    GUI.Label(new Rect(r.x + 18 * k, r.y + 6 * k, r.width, r.height), c.Title, _sub);
                }
                else
                {
                    var ic = Icon(c.Icon);
                    if (ic != null)
                    {
                        var old = GUI.color;
                        GUI.color = on ? green : Color.white;
                        Paint.Picture(new Rect(r.x + 26 * k, r.y + (h - 46 * k) / 2, 46 * k, 46 * k), ic);
                        GUI.color = old;
                    }
                    string sub = c.Sub;
                    if (c.Key == "creative" && Mode.Pending == "creative") sub = "Next game: Creative";
                    GUI.Label(new Rect(r.x + 100 * k, r.y + 8 * k, w - 160 * k, 40 * k), c.Title, _title);
                    GUI.Label(new Rect(r.x + 102 * k, r.y + 46 * k, w - 160 * k, 26 * k), sub, _sub);
                    GUI.Label(new Rect(r.xMax - 60 * k, r.y, 50 * k, h), on ? "" : ">", _chev);
                    if (on && Pad.InUse) GUI.Label(new Rect(r.xMax - 60 * k, r.y, 50 * k, h), "A", _chev);
                }
                if (GUI.Button(r, "", GUIStyle.none)) { _sel = i; Choose(c); }
            }
            if (_note.Length > 0) GUI.Label(new Rect(x, y + 4 * k, w, 40 * k), _note, _small);
            DrawPreview(k, green);
        }

        static void DrawPreview(float k, Color green)
        {
            float pw = Mathf.Min(560 * k, Screen.width * 0.36f), px = Screen.width - pw - Screen.width * 0.06f, py = Screen.height * 0.38f;
            float ph = pw * 0.52f;
            if (_art == null)
                foreach (var n in new[] { "loading.jpg", "loading.png" })
                {
                    var p = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", n);
                    if (_art == null && File.Exists(p)) try { _art = Rt.LoadImage(File.ReadAllBytes(p)); } catch (Exception) { }
                }
            var box = new Rect(px, py, pw, ph + 130 * k);
            Paint.Fill(box, new Color(0.03f, 0.06f, 0.07f, 0.9f));
            Frame(box, new Color(0.2f, 0.55f, 0.45f, 0.9f), 2 * k);
            if (_art != null) Paint.Picture(new Rect(px + 3, py + 3, pw - 6, ph - 3), _art);
            else Paint.Fill(new Rect(px + 3, py + 3, pw - 6, ph - 3), new Color(0.1f, 0.2f, 0.16f));
            Paint.Fill(new Rect(px + 18 * k, py + ph + 20 * k, 52 * k, 52 * k), green);
            Paint.Fill(new Rect(px + 18 * k, py + ph + 44 * k, 52 * k, 28 * k), new Color(0.45f, 0.3f, 0.18f));
            string name = MainMenu.Merge.Length > 0 ? MainMenu.Merge : "Your world awaits";
            GUI.Label(new Rect(px + 86 * k, py + ph + 12 * k, pw - 100 * k, 30 * k), name, _cap);
            string line = MainMenu.RundownLine();
            GUI.Label(new Rect(px + 86 * k, py + ph + 44 * k, pw - 100 * k, 80 * k),
                      (line.Length > 0 ? line : "Build, explore, survive or just create. The choice is yours.") + "\n" + MainMenu.SavesLine(), _capSub);
        }
    }
}
