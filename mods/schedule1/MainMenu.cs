using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Frankenstein inside the game's own main menu, under Continue and New game: Creative (its own
    /// saves), back to the normal game, and the merge's rundown. Creative needs the game to start again with other
    /// saves, so the button leaves UserData/Frankenstein/switch.json and closes the game; Frankenstein starts it
    /// again in that mode. While you play, the corner shows who you play as.</summary>
    internal static class MainMenu
    {
        public static string Merge = "";
        public static bool Creative;
        public static List<string> Rundown = new List<string>();
        public static string Who = "", Moves = "", Close = "";
        static bool _inMenu, _showRundown;
        static float _quitAt = -1;
        static string _note = "";

        // the Creative entry copied into the game's own list, under Continue and New game
        static GameObject _entry;
        static object _entryText;
        static float _lookUntil, _nextLook;
        static bool _injected;

        public static void Scene(string name)
        {
            _inMenu = name != null && name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;
            _injected = false;
            _entry = null;
            _lookUntil = _inMenu ? Time.unscaledTime + 30f : 0;
        }

        static string Label() => Creative ? "Back to normal game" : "Creative";

        static int _tries;
        static readonly HashSet<string> _said = new HashSet<string>();
        static object _bg;         // the row's own background picture: lit up on hover, like the game's rows
        static Color _bgColor;
        static bool _hover;

        /// <summary>Why the Creative entry isn't in yet, written to the MelonLoader log once per reason, so a failed
        /// attempt can be traced.</summary>
        static void Say(string why)
        {
            if (_said.Add(why)) Melon<Plugin>.Logger.Msg("Main menu: " + why);
        }

        /// <summary>Copies the menu's own New Game row, so Creative looks like the rest of the list (same font,
        /// size and place), and puts it between New Game and Settings. Every script on the copy is taken off,
        /// the button too, so the copy can never start a new game: Frankenstein sees the click itself.</summary>
        static void Inject()
        {
            _tries++;
            object newText = null, nearText = null;
            var seen = new List<string>();
            foreach (var t in Rt.Texts())
            {
                var s = (Rt.Text(t) ?? "").Trim();
                var low = System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", "").Trim().ToLowerInvariant();
                if (low.Length > 0 && seen.Count < 30) seen.Add(low);
                if ((low == "new game" || low == "newgame") && newText == null) newText = t;
                else if ((low == "settings" || low == "continue" || low == "options" || low == "load game") && nearText == null) nearText = t;
            }
            if (newText == null)
            {
                if (_tries == 6) Say("no \"New Game\" text yet; texts on screen: " + string.Join(" | ", seen.ToArray()));
                return;
            }
            var newGo = Rt.GameObjectOf(newText);
            var near = Rt.GameObjectOf(nearText);
            if (newGo == null) { Say("New Game text has no object"); return; }
            // the New Game row: the ancestor whose parent also holds the other rows
            var row = newGo.transform;
            if (near != null)
            {
                while (row.parent != null && !near.transform.IsChildOf(row.parent)) row = row.parent;
            }
            else if (row.parent != null) row = row.parent;
            var list = row.parent;
            if (list == null) { Say("New Game row has no list"); return; }
            _entry = UnityEngine.Object.Instantiate(row.gameObject, list);
            _entry.name = "Frankenstein Creative";
            _entry.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
            // only the pictures and the words stay: buttons, triggers and the game's scripts all go
            int removed = 0;
            foreach (var c in Rt.Comps(_entry, typeof(MonoBehaviour), true))
            {
                var n = Rt.TypeName(c);
                bool keep = n.EndsWith(".Image") || n.EndsWith(".RawImage") || n.Contains("TMP_Text") || n.Contains("TextMeshPro") ||
                            n.EndsWith("UI.Text") || n.EndsWith("Outline") || n.EndsWith("Shadow") || n.Contains("LayoutElement") || n.Contains("ContentSizeFitter");
                if (keep) continue;
                if (c is UnityEngine.Object o) { UnityEngine.Object.DestroyImmediate(o); removed++; }
            }
            _entryText = null;
            foreach (var n in new[] { "TMPro.TMP_Text", "UnityEngine.UI.Text" })
                if (_entryText == null) _entryText = Rt.CompInChildren(_entry, Rt.Find(n));
            if (_entryText == null)
            {
                Say("the copied row has no text component");
                UnityEngine.Object.Destroy(_entry);
                _entry = null;
                return;
            }
            Rt.Set(_entryText, Label(), "text");
            _bg = Rt.Comp(_entry, Rt.Find("UnityEngine.UI.Image"));
            if (_bg != null && Rt.Get(_bg, "color") is Color bc) _bgColor = bc;
            // no layout group on the list: move the rows below down by one row's height
            var layout = Rt.Comps(list.gameObject, typeof(Component), false).Any(c => Rt.TypeName(c).Contains("LayoutGroup"));
            var rt = _entry.transform as RectTransform;
            var rowRt = row as RectTransform;
            if (!layout && rt != null && rowRt != null)
            {
                float step = rowRt.rect.height + 6;
                if (near != null)
                {
                    var nearRow = near.transform;
                    while (nearRow.parent != null && nearRow.parent != list) nearRow = nearRow.parent;
                    if (nearRow is RectTransform nr && Math.Abs(nr.anchoredPosition.y - rowRt.anchoredPosition.y) > 1)
                        step = Math.Abs(nr.anchoredPosition.y - rowRt.anchoredPosition.y);
                }
                var y = rowRt.anchoredPosition.y;
                for (int i = 0; i < list.childCount; i++)
                {
                    var other = list.GetChild(i) as RectTransform;
                    if (other != null && other != rt && other.anchoredPosition.y < y - 1)
                        other.anchoredPosition = new Vector2(other.anchoredPosition.x, other.anchoredPosition.y - step);
                }
                rt.anchoredPosition = new Vector2(rowRt.anchoredPosition.x, y - step);
            }
            _injected = true;
            Melon<Plugin>.Logger.Msg("Main menu: " + Label() + " added under New Game (" + removed + " script(s) taken off the copy, " +
                                     (layout ? "list lays itself out" : "rows moved down") + ").");
        }

        /// <summary>The entry's place on screen, in GUI pixels from the top left, from its own corners.</summary>
        static Rect ScreenRect()
        {
            var rt = _entry != null ? _entry.transform as RectTransform : null;
            if (rt == null) return new Rect();
            var r = rt.rect;
            Vector3 a = rt.TransformPoint(new Vector3(r.xMin, r.yMin, 0)), b = rt.TransformPoint(new Vector3(r.xMax, r.yMax, 0));
            // a canvas drawn by a camera needs that camera to turn its corners into screen pixels
            Camera cam = null;
            for (var t = rt.parent; t != null && cam == null; t = t.parent)
            {
                var canvas = Rt.Comp(t.gameObject, Rt.Find("UnityEngine.Canvas"));
                if (canvas == null) continue;
                var mode = (Rt.Get(canvas, "renderMode") ?? "").ToString();
                if (mode.Contains("Overlay") || mode == "0") break;
                cam = Rt.Get(canvas, "worldCamera") as Camera;
                break;
            }
            if (cam != null) { a = cam.WorldToScreenPoint(a); b = cam.WorldToScreenPoint(b); }
            float x0 = Math.Min(a.x, b.x), x1 = Math.Max(a.x, b.x), y0 = Math.Min(a.y, b.y), y1 = Math.Max(a.y, b.y);
            return new Rect(x0, Screen.height - y1, x1 - x0, y1 - y0);
        }

        static void Hover()
        {
            if (!_injected || _entry == null) return;
            var r = ScreenRect();
            var m = Input.mousePosition;
            bool over = r.width > 4 && r.Contains(new Vector2(m.x, Screen.height - m.y));
            if (over != _hover)
            {
                _hover = over;
                if (_bg != null) Rt.Set(_bg, over ? new Color(1f, 1f, 1f, Math.Max(_bgColor.a, 0.25f)) : _bgColor, "color");
            }
            if (over && Input.GetMouseButtonDown(0)) Click();
        }

        static void Click()
        {
            Rt.Set(_entryText, Creative ? "Starting your normal game…" : "Starting Creative…", "text");
            Switch(Creative ? "normal" : "creative");
        }

        public static void Switch(string to)
        {
            try
            {
                File.WriteAllText(Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "switch.json"), "{\"to\": \"" + to + "\"}");
                _note = "Starting again in " + (to == "creative" ? "Creative" : "your normal game") + "…";
                _quitAt = Time.unscaledTime + 1.5f;
            }
            catch (Exception e) { _note = "Couldn't switch: " + e.Message; }
        }

        public static void Update()
        {
            if (_inMenu && !_injected && Time.unscaledTime < _lookUntil && Time.unscaledTime >= _nextLook)
            {
                _nextLook = Time.unscaledTime + 0.5f;
                try { Inject(); }
                catch (Exception e)
                {
                    Say("couldn't add Creative: " + e.GetType().Name + ": " + e.Message);  // tried again until the 30 seconds are up
                    if (_entry != null) { UnityEngine.Object.Destroy(_entry); _entry = null; }
                }
                if (Time.unscaledTime + 0.5f >= _lookUntil && !_injected) Say("gave up after 30 seconds; Frankenstein's small panel shows instead");
            }
            if (_inMenu && _entry == null && _injected) { _injected = false; _lookUntil = Time.unscaledTime + 30f; }  // the menu was rebuilt
            try { Hover(); } catch (Exception e) { Say("click: " + e.Message); }
            if (_quitAt > 0 && Time.unscaledTime >= _quitAt) { _quitAt = -1; Application.Quit(); }
        }

        public static void Draw()
        {
            if (!_inMenu)
            {
                if (Who.Length > 0) GUI.Label(new Rect(16, Screen.height - 34, 600, 24), "Playing as " + Who);
                return;
            }
            if (_injected)
            {
                // Creative is in the game's own list; the rundown sits in the corner, out of the way
                float rw = 380, rx = Screen.width - rw - 24, ry = 24;
                if (GUI.Button(new Rect(rx, ry, rw, 28), (_showRundown ? "Hide" : "What's in") + " " + (Merge.Length > 0 ? Merge : "this merge"))) _showRundown = !_showRundown;
                if (_note.Length > 0) GUI.Label(new Rect(rx, ry + 32, rw, 24), _note);
                if (!_showRundown) return;
                GUI.Box(new Rect(rx, ry + 56, rw, 12 + 22 * Math.Min(Rundown.Count, 12)), "");
                for (int i = 0; i < Rundown.Count && i < 12; i++)
                    GUI.Label(new Rect(rx + 10, ry + 62 + 22 * i, rw - 20, 22), Rundown[i]);
                return;
            }
            // the game's list couldn't take the entry: a small panel in the top right corner, away from the title
            float w = 330, x = Screen.width - w - 24, y = 24;
            float h = _showRundown ? 112 + 22 * Math.Min(Rundown.Count, 12) : 112;
            GUI.Box(new Rect(x, y, w, h), "");
            GUI.Label(new Rect(x + 10, y + 6, w - 20, 22), "FRANKENSTEIN" + (Creative ? "  ·  Creative" : ""));
            if (GUI.Button(new Rect(x + 10, y + 30, w - 20, 30), Creative ? "Back to my normal game" : "Creative (every item, its own saves)"))
                Switch(Creative ? "normal" : "creative");
            if (GUI.Button(new Rect(x + 10, y + 66, w - 20, 26), _showRundown ? "Hide the rundown" : "What's in " + (Merge.Length > 0 ? Merge : "this merge"))) _showRundown = !_showRundown;
            if (_note.Length > 0) GUI.Label(new Rect(x + 10, y + 94, w - 20, 22), _note);
            if (!_showRundown) return;
            for (int i = 0; i < Rundown.Count && i < 12; i++)
                GUI.Label(new Rect(x + 10, y + 112 + 22 * i, w - 20, 22), Rundown[i]);
        }
    }
}
