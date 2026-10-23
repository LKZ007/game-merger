using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Frankenstein inside the game's own main menu, between New Game and Settings: "Game Mode", like
    /// Minecraft's. It picks Survival or Creative for the game you start or load next, and each save keeps its
    /// own mode (Mode). The corner lists your saves with their modes and the merge's rundown. While you play, the
    /// corner shows who you play as.</summary>
    internal static class MainMenu
    {
        public static string Merge = "";
        public static bool Creative;
        public static List<string> Rundown = new List<string>();
        public static string Who = "", Moves = "", Close = "";
        static bool _inMenu, _showRundown;
        static string _note = "";

        // the Creative entry copied into the game's own list, under Continue and New game
        static GameObject _entry;
        static object _entryText;
        static float _lookUntil, _nextLook, _nextLabel;
        static string[][] _saves = new string[0][];
        static bool _injected;

        public static void Scene(string name)
        {
            _inMenu = name != null && name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;
            _injected = false;
            _entry = null;
            _lookUntil = _inMenu ? Time.unscaledTime + 30f : 0;
            if (_inMenu) _saves = Mode.Saves();
            MenuSkin.Scene(_inMenu);
        }

        static bool NextCreative => Mode.Pending == "creative";

        /// <summary>The merge in one line, for the main menu's picture card.</summary>
        public static string RundownLine() => Rundown.Count > 1 ? Rundown[1] : Rundown.Count > 0 ? Rundown[0] : "";

        /// <summary>Your saves and their game modes, in one line.</summary>
        public static string SavesLine() => _saves.Length == 0 ? "" : "Saves: " + string.Join(", ", _saves.Select(s => s[0] + " " + (s[1] == "creative" ? "Creative" : "Survival")).ToArray());
        static string Label() => (Pad.InUse ? "(Y) " : "") + "Game Mode: " + (NextCreative ? "Creative" : "Survival");

        static int _tries;
        static bool _wordsOnly;

        /// <summary>The New Game text's place in the menu, written once to UserData/Frankenstein/menu-debug.txt
        /// (and the log), so a menu this build lays out differently can be fixed from one file.</summary>
        static void Describe(GameObject go)
        {
            try
            {
                var lines = new List<string>();
                for (var t = go != null ? go.transform : null; t != null; t = t.parent)
                    lines.Add(t.name + " [" + string.Join(", ", Rt.Comps(t.gameObject, typeof(Component), false).Select(Rt.TypeName).ToArray()) + "] children=" + t.childCount);
                File.WriteAllText(Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "menu-debug.txt"), string.Join("\n", lines.ToArray()));
                Say("New Game is at: " + string.Join(" < ", lines.Select(l => l.Split(' ')[0]).ToArray()));
            }
            catch (Exception) { }
        }
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
            if (_tries == 1 || _tries == 6) Describe(Rt.GameObjectOf(newText));
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
            // the whole row first; if this build won't let its scripts come off, just the row's words
            _entry = UnityEngine.Object.Instantiate(_wordsOnly ? newGo : row.gameObject, list);
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
                if (c is UnityEngine.Object o)
                {
                    try { UnityEngine.Object.DestroyImmediate(o); removed++; }
                    catch (Exception e)
                    {
                        Say("couldn't take " + n + " off the copied row (" + e.Message + "), so only its words are copied");
                        _wordsOnly = true;
                        UnityEngine.Object.Destroy(_entry);
                        _entry = null;
                        return;
                    }
                }
            }
            _entry.SetActive(true);
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
            Mode.Pending = NextCreative ? "survival" : "creative";
            if (_entryText != null) Rt.Set(_entryText, Label(), "text");
            _note = NextCreative ? "The game you start or load next will be Creative." : "The game you start or load next will be Survival.";
        }

        public static void Update()
        {
            // Frankenstein's own menu (MenuSkin) replaces the copied row: the game wouldn't take the copy
            try { MenuSkin.Update(); } catch (Exception e) { Say("menu: " + e.Message); }
            try { Hover(); } catch (Exception e) { Say("click: " + e.Message); }
            if (_inMenu && !MenuSkin.Found && (Pad.Pressed & Pad.Y) != 0) Click();  // controller: Y picks the mode
            if (_inMenu && _injected && _entryText != null && Time.unscaledTime >= _nextLabel)
            {
                _nextLabel = Time.unscaledTime + 1f;
                if (Rt.Text(_entryText) != Label()) Rt.Set(_entryText, Label(), "text");
            }
        }

        public static void Draw()
        {
            if (!_inMenu)
            {
                if (Who.Length > 0) GUI.Label(new Rect(16, Screen.height - 34, 600, 24), "Playing as " + Who);
                return;
            }
            if (MenuSkin.Found) { MenuSkin.Draw(); return; }
            // the corner: your saves with their modes, and the merge's rundown. If the game's list couldn't take
            // the Game Mode entry, its button is here instead.
            float w = 340, x = Screen.width - w - 24, y = 24;
            int rows = (_injected ? 0 : 1) + 1 + _saves.Length + (_note.Length > 0 ? 1 : 0) + (_showRundown ? Math.Min(Rundown.Count, 12) : 0);
            GUI.Box(new Rect(x, y, w, 40 + rows * 26), "");
            GUI.Label(new Rect(x + 10, y + 6, w - 20, 22), "FRANKENSTEIN  ·  " + (Merge.Length > 0 ? Merge : "Schedule I"));
            float cy = y + 32;
            if (!_injected)
            {
                if (Pad.Button(new Rect(x + 10, cy, w - 20, 24), Label())) Click();
                cy += 26;
            }
            foreach (var sv in _saves)
            {
                GUI.Label(new Rect(x + 10, cy, w - 20, 24), sv[0] + ":  " + (sv[1] == "creative" ? "Creative" : "Survival"));
                cy += 26;
            }
            if (_note.Length > 0) { GUI.Label(new Rect(x + 10, cy, w - 20, 24), _note); cy += 26; }
            if (Pad.Button(new Rect(x + 10, cy, w - 20, 24), (_showRundown ? "Hide" : "What's in") + " " + (Merge.Length > 0 ? Merge : "this merge"))) _showRundown = !_showRundown;
            cy += 26;
            if (!_showRundown) return;
            for (int i = 0; i < Rundown.Count && i < 12; i++)
                GUI.Label(new Rect(x + 10, cy + 22 * i, w - 20, 22), Rundown[i]);
        }
    }
}
