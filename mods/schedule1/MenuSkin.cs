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
    /// <summary>Schedule I's main menu, redrawn like levy's reference: a night city backdrop over the whole screen
    /// (the game's own title, credits and 3D scene are covered), the merge's title lockup at the top left, a flat
    /// list with icons (Continue, New Game, Creative, Multiplayer, Settings, Profiles, Quit) where the chosen row
    /// gets a green glow, your latest save's cash and rank at the top right, and the version at the bottom right.
    /// Profiles shows a card per save slot. The art is painted by the app (menu-backdrop.jpg, title.png). Each row
    /// presses the game's own button, so everything does what the game does; the game's list is shrunk out of sight.
    /// Mouse, keyboard (arrows, Enter, Esc) and controller (d-pad or stick, A, B).</summary>
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
        static readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        static GUIStyle _small;
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
            _profiles = false;
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
                new[] { "new game", "New Game", "Start a new world", "plus" },
                new[] { "", "Creative", "Unlimited resources", "brush" },
                new[] { "invite friends", "Multiplayer", "Invite friends to your game", "people" },
                new[] { "settings", "Settings", "Adjust your game", "gear" },
                new[] { "", "Profiles", "Your saves", "person" },
                new[] { "quit", "Quit", "Exit to desktop", "exit" },
            };
            var made = new List<Card>();
            GameObject anchor = null;
            foreach (var w in want)
            {
                if (w[0].Length == 0) { made.Add(new Card { Key = w[1].ToLowerInvariant(), Title = w[1], Sub = w[2], Icon = w[3] }); continue; }
                var hit = texts.FirstOrDefault(x => x.s == w[0] || x.s.Replace(" ", "") == w[0].Replace(" ", "") || (w[0] == "invite friends" && x.s.StartsWith("invite")));
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
            if (c.Key == "profiles") { _profiles = true; _card = 0; Profiles.Forget(); return; }
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
            if (!_profiles && ((Pad.Pressed & Pad.Down) != 0 || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))) _sel = (_sel + 1) % n;
            if (!_profiles && ((Pad.Pressed & Pad.Up) != 0 || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))) _sel = (_sel + n - 1) % n;
            if (!_profiles && _stickReady && Mathf.Abs(Pad.Stick.y) > 0.6f) { _sel = (_sel + (Pad.Stick.y < 0 ? 1 : n - 1)) % n; _stickReady = false; }
            if (Mathf.Abs(Pad.Stick.y) < 0.3f) _stickReady = true;
            bool ok = (Pad.Pressed & Pad.A) != 0 || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (_profiles)
            {
                // the cards go left and right; B or Esc goes back to the list
                if ((Pad.Pressed & Pad.Right) != 0 || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) _card = Math.Min(Profiles.Slots - 1, _card + 1);
                if ((Pad.Pressed & Pad.Left) != 0 || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) _card = Math.Max(0, _card - 1);
                if (_stickX && Mathf.Abs(Pad.Stick.x) > 0.6f) { _card = Mathf.Clamp(_card + (Pad.Stick.x > 0 ? 1 : -1), 0, Profiles.Slots - 1); _stickX = false; }
                if (Mathf.Abs(Pad.Stick.x) < 0.3f) _stickX = true;
                if ((Pad.Pressed & Pad.B) != 0 || Input.GetKeyDown(KeyCode.Escape)) _profiles = false;
                else if (ok) PlaySlot(Profiles.All()[_card]);
                return;
            }
            if (ok) Choose(_cards[_sel]);
        }

        static bool _profiles, _stickX = true;
        static int _card;

        static void PlaySlot(Profiles.Slot s)
        {
            if (s.Empty)
            {
                var ng = _cards.FirstOrDefault(x => x.Key == "new game");
                if (ng != null) Choose(ng);
                return;
            }
            _note = "Loading " + s.Name + "...";
            if (Profiles.Play(s)) return;
            var cont = _cards.FirstOrDefault(x => x.Key == "continue");
            _note = "Pick " + s.Name + " on the game's own screen.";
            _profiles = false;
            if (cont != null) Choose(cont);
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
                        case "plus": on = (Mathf.Abs(fx) < 4 && Mathf.Abs(fy) < 24) || (Mathf.Abs(fy) < 4 && Mathf.Abs(fx) < 24); break;
                        case "person": on = Mathf.Sqrt(fx * fx + (fy - 12) * (fy - 12)) < 11 || (fy < -4 && fy > -28 && Mathf.Abs(fx) < 20 - (fy + 28) * 0.15f && !(fy > -8 && Mathf.Abs(fx) > 14)); break;
                        case "people":
                        {
                            float bx = fx + 9, cx2 = fx - 12;
                            on = Mathf.Sqrt(bx * bx + (fy - 10) * (fy - 10)) < 9 || (fy < -2 && fy > -24 && Mathf.Abs(bx) < 16) ||
                                 Mathf.Sqrt(cx2 * cx2 + (fy - 13) * (fy - 13)) < 7 || (fy < 2 && fy > -18 && cx2 > -4 && cx2 < 13);
                        }
                        break;
                        case "gear": { float a = Mathf.Atan2(fy, fx); float teeth = Mathf.Cos(a * 8) > 0.3f ? 29 : 23; on = r < teeth && r > 10; } break;
                        case "exit": on = (x > 10 && x < 40 && (y < 16 || y > 48 || x < 16) && y > 10 && y < 54) || (Mathf.Abs(fy) < 3 && x > 24 && x < 56) || (x > 44 && x < 56 && Mathf.Abs(fy) < (56 - x)); break;
                    }
                    t.SetPixel(x, y, on ? w : clear);
                }
            t.Apply();
            _icons[kind] = t;
            return t;
        }

        static GUIStyle _row, _top, _ver, _cardName, _cardSmall, _cardBig, _btn;
        static Texture2D _back, _logo;
        static bool _artTried;

        static void Styles(float k)
        {
            if (_row != null && _row.fontSize == (int)(26 * k)) return;
            Func<int, Color, bool, GUIStyle> make = (size, c, bold) =>
            {
                var st = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = false };
                st.normal.textColor = c;
                if (bold) Enum(st, "fontStyle", 1);
                return st;
            };
            var white = new Color(0.94f, 0.95f, 0.96f);
            _row = make((int)(26 * k), white, false);
            Enum(_row, "alignment", 3);  // middle left
            _top = make((int)(18 * k), white, true);
            Enum(_top, "alignment", 4);
            _ver = make((int)(13 * k), new Color(0.65f, 0.67f, 0.72f), false);
            Enum(_ver, "alignment", 5);  // middle right
            _cardName = make((int)(24 * k), white, true);
            Enum(_cardName, "alignment", 4);
            _cardSmall = make((int)(15 * k), new Color(0.7f, 0.73f, 0.78f), false);
            Enum(_cardSmall, "alignment", 4);
            _cardBig = make((int)(46 * k), white, true);
            Enum(_cardBig, "fontStyle", 3);  // bold italic, like the reference's PROFILES
            _btn = make((int)(20 * k), white, true);
            Enum(_btn, "alignment", 4);
            _small = make((int)(15 * k), new Color(0.75f, 0.85f, 0.8f), false);
            _small.wordWrap = true;
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

        static Texture2D Load(params string[] names)
        {
            foreach (var n in names)
            {
                var p = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", n);
                if (File.Exists(p)) try { var t = Rt.LoadImage(File.ReadAllBytes(p)); if (t != null) return t; } catch (Exception) { }
            }
            return null;
        }

        internal static Texture2D Backdrop() { Art(); return _back; }
        internal static Texture2D Logo() { Art(); return _logo; }

        static void Art()
        {
            if (_artTried) return;
            _artTried = true;
            _back = Load("menu-backdrop.jpg", "menu-backdrop.png");
            _logo = Load("title.png");
        }

        static readonly Color Green = new Color(0.27f, 0.72f, 0.29f);

        /// <summary>A green bar that fades out to the right, like the reference's chosen row.</summary>
        static void Glow(Rect r)
        {
            const int steps = 24;
            for (int i = 0; i < steps; i++)
            {
                float f = i / (float)steps;
                Paint.Fill(new Rect(r.x + r.width * f, r.y, r.width / steps + 1, r.height), new Color(Green.r, Green.g, Green.b, 0.95f * (1f - f) * (1f - f) + 0.04f));
            }
        }

        public static void Draw()
        {
            if (!Active) return;
            Art();
            float W = Screen.width, H = Screen.height, k = Mathf.Clamp(H / 1080f, 0.6f, 2f);
            Styles(k);
            // one scene over everything: the game's title, credits and 3D menu are covered
            Paint.Fill(new Rect(0, 0, W, H), new Color(0.03f, 0.03f, 0.06f));
            if (_back != null)
            {
                float a = _back.width / (float)_back.height, bw = Mathf.Max(W, H * a), bh = bw / a;
                Paint.Picture(new Rect((W - bw) / 2, (H - bh) / 2, bw, bh), _back);
            }
            var m = Event.current.mousePosition;
            bool moved = (m - _lastMouse).sqrMagnitude > 1f;
            if (Event.current.type == EventType.Repaint) _lastMouse = m;
            // the title lockup, top left
            float x = W * 0.045f, top = H * 0.06f;
            if (_logo != null)
            {
                float lh = H * 0.16f, lw = lh * _logo.width / _logo.height;
                if (lw > W * 0.42f) { lw = W * 0.42f; lh = lw * _logo.height / _logo.width; }
                Paint.Picture(new Rect(x, top, lw, lh), _logo);
            }
            else GUI.Label(new Rect(x, top, W * 0.5f, 60 * k), (World.Host.Length > 0 ? World.Host : "Schedule I").ToUpperInvariant() + "  ×  " + (MainMenu.Merge.Length > 0 ? MainMenu.Merge : "Frankenstein").ToUpperInvariant(), _cardBig);
            if (_profiles) DrawProfiles(W, H, k, m, moved);
            else DrawList(W, H, k, m, moved);
            DrawTopBar(W, H, k);
            string ver = "v" + Application.version + "  ·  Frankenstein " + Plugin.Version;
            GUI.Label(new Rect(W - 420 * k, H - 40 * k, 400 * k, 28 * k), ver, _ver);
        }

        static void DrawList(float W, float H, float k, Vector2 m, bool moved)
        {
            float x = W * 0.045f, y = H * 0.3f, w = 640 * k, h = 62 * k;
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                var r = new Rect(x, y, w, h);
                y += h;
                if (moved && r.Contains(m)) _sel = i;
                bool on = i == _sel;
                if (on) Glow(r);
                else Paint.Fill(new Rect(r.x, r.yMax - 1, r.width * 0.8f, 1), new Color(1, 1, 1, 0.1f));
                var ic = Icon(c.Icon);
                if (ic != null) Paint.Picture(new Rect(r.x + 20 * k, r.y + (h - 30 * k) / 2, 30 * k, 30 * k), ic);
                string title = c.Title;
                if (c.Key == "creative" && Mode.Pending == "creative") title += "  ·  next game";
                GUI.Label(new Rect(r.x + 72 * k, r.y, w - 90 * k, h), title, _row);
                if (GUI.Button(r, "", GUIStyle.none)) { _sel = i; Choose(c); }
            }
            string hint = _sel < _cards.Count ? _cards[_sel].Sub : "";
            if (_note.Length > 0) hint = _note;
            GUI.Label(new Rect(x + 20 * k, y + 16 * k, w, 40 * k), hint, _small);
            if (Pad.InUse) GUI.Label(new Rect(x + 20 * k, H - 50 * k, w, 30 * k), "A  select   ·   d-pad  move", _small);
        }

        static void DrawTopBar(float W, float H, float k)
        {
            var last = Profiles.Latest();
            float bh = 46 * k, y = 26 * k, gear = 46 * k;
            float x = W - 30 * k - gear;
            var g = new Rect(x, y, gear, bh);
            Paint.Fill(g, new Color(0.08f, 0.09f, 0.12f, 0.85f));
            Paint.Picture(new Rect(g.x + 10 * k, g.y + 9 * k, 28 * k, 28 * k), Icon("gear"));
            if (GUI.Button(g, "", GUIStyle.none)) { var st = _cards.FirstOrDefault(c => c.Key == "settings"); if (st != null) Choose(st); }
            if (last == null) return;
            string cash = Profiles.Money(last.Cash), rank = double.IsNaN(last.Rank) ? (last.Mode == "creative" ? "CREATIVE" : "SURVIVAL") : "LV " + (int)last.Rank;
            float bw = 330 * k;
            var bar = new Rect(x - 12 * k - bw, y, bw, bh);
            Paint.Fill(bar, new Color(0.08f, 0.09f, 0.12f, 0.85f));
            GUI.Label(new Rect(bar.x, bar.y, bw * 0.55f, bh), cash, _top);
            Paint.Fill(new Rect(bar.x + bw * 0.55f, bar.y + 10 * k, 1, bh - 20 * k), new Color(1, 1, 1, 0.2f));
            GUI.Label(new Rect(bar.x + bw * 0.55f, bar.y, bw * 0.45f, bh), rank, _top);
        }

        static void DrawProfiles(float W, float H, float k, Vector2 m, bool moved)
        {
            float x0 = W * 0.045f, y0 = H * 0.26f;
            GUI.Label(new Rect(x0, y0, W * 0.5f, 70 * k), "PROFILES", _cardBig);
            var slots = Profiles.All();
            float gap = 22 * k, cw = Mathf.Min(300 * k, (W - x0 * 2 - gap * (slots.Count - 1)) / slots.Count), ch = Mathf.Min(430 * k, H * 0.52f);
            float y = y0 + 90 * k;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var r = new Rect(x0 + i * (cw + gap), y, cw, ch);
                if (moved && r.Contains(m)) _card = i;
                bool on = i == _card;
                Paint.Fill(r, new Color(0.07f, 0.08f, 0.1f, 0.92f));
                Frame(r, on ? Green : new Color(1, 1, 1, 0.12f), on ? 3 * k : 1);
                // the avatar: a hooded figure in the save's colour (Creative violet, Survival green)
                var tint = s.Empty ? new Color(0.24f, 0.26f, 0.3f) : s.Mode == "creative" ? new Color(0.55f, 0.36f, 0.95f) : Green;
                float ax = r.x + cw / 2, ay = r.y + 40 * k;
                var oldc = GUI.color;
                GUI.color = tint;
                Paint.Picture(new Rect(ax - 64 * k, ay, 128 * k, 128 * k), Icon("person"));
                GUI.color = oldc;
                if (s.Empty)
                {
                    GUI.Label(new Rect(r.x, ay + 150 * k, cw, 34 * k), "Empty slot " + s.Number, _cardName);
                    GUI.Label(new Rect(r.x, ay + 190 * k, cw, 50 * k), "Start a new world here", _cardSmall);
                    var plus = new Rect(ax - 22 * k, r.yMax - 70 * k, 44 * k, 44 * k);
                    Frame(plus, new Color(1, 1, 1, 0.6f), 2);
                    Paint.Picture(new Rect(plus.x + 8 * k, plus.y + 8 * k, 28 * k, 28 * k), Icon("plus"));
                }
                else
                {
                    GUI.Label(new Rect(r.x, ay + 136 * k, cw, 34 * k), s.Name.ToUpperInvariant(), _cardName);
                    GUI.Label(new Rect(r.x, ay + 170 * k, cw, 24 * k), (s.Mode == "creative" ? "Creative" : "Survival") + (double.IsNaN(s.Rank) ? "" : "  ·  Level " + (int)s.Rank), _cardSmall);
                    float ly = ay + 210 * k;
                    foreach (var row in new[] { new[] { "Cash", Profiles.Money(s.Cash) }, new[] { "Properties", Profiles.Count(s.Properties) },
                                                new[] { "Last played", s.Played.ToString("d MMM") } })
                    {
                        GUI.Label(new Rect(r.x + 18 * k, ly, cw * 0.6f, 24 * k), row[0], _small);
                        var right = new GUIStyle(_small);
                        Enum(right, "alignment", 5);
                        GUI.Label(new Rect(r.x + cw * 0.4f - 18 * k, ly, cw * 0.6f, 24 * k), row[1], right);
                        ly += 28 * k;
                    }
                    var play = new Rect(r.x + 18 * k, r.yMax - 62 * k, cw - 36 * k, 42 * k);
                    Paint.Fill(play, on ? new Color(0.3f, 0.78f, 0.32f) : new Color(0.2f, 0.55f, 0.22f));
                    GUI.Label(play, "Play", _btn);
                }
                if (GUI.Button(r, "", GUIStyle.none)) { _card = i; PlaySlot(s); }
            }
            var back = new Rect(x0, y + ch + 30 * k, 200 * k, 44 * k);
            Paint.Fill(back, new Color(0.08f, 0.09f, 0.12f, 0.85f));
            GUI.Label(back, Pad.InUse ? "B  Back" : "Esc  Back", _btn);
            if (GUI.Button(back, "", GUIStyle.none)) _profiles = false;
            if (_note.Length > 0) GUI.Label(new Rect(x0 + 230 * k, y + ch + 38 * k, W * 0.6f, 30 * k), _note, _small);
        }
    }
}
