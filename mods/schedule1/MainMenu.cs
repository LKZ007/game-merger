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

        /// <summary>Copies the menu's own New game button, so Creative looks like the rest of the list: same font,
        /// size and hover. Its own clicks and scripts are taken off and Frankenstein's click put on.</summary>
        static void Inject()
        {
            object newText = null, nearText = null;
            foreach (var t in Rt.Texts())
            {
                var s = (Rt.Text(t) ?? "").Trim().ToLowerInvariant();
                if (s == "new game" && newText == null) newText = t;
                else if ((s == "continue" || s == "settings" || s == "load game") && nearText == null) nearText = t;
            }
            if (newText == null) return;
            var newGo = Rt.GameObjectOf(newText);
            var near = Rt.GameObjectOf(nearText);
            if (newGo == null) return;
            // the New game row: the ancestor whose parent also holds the other rows
            var row = newGo.transform;
            if (near != null)
            {
                while (row.parent != null && !near.transform.IsChildOf(row.parent)) row = row.parent;
            }
            else if (row.parent != null) row = row.parent;
            var list = row.parent;
            if (list == null) return;
            _entry = UnityEngine.Object.Instantiate(row.gameObject, list);
            _entry.name = "Frankenstein Creative";
            _entry.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);
            // the game's own scripts on the copy would still start a new game: only Unity's UI parts stay
            foreach (var c in Rt.Comps(_entry, typeof(MonoBehaviour), true))
            {
                var n = Rt.TypeName(c);
                if (n.StartsWith("UnityEngine.UI.") && !n.Contains("EventTrigger") || n.StartsWith("TMPro.") || n.StartsWith("Il2CppTMPro."))
                    continue;
                if (c is UnityEngine.Object o) UnityEngine.Object.DestroyImmediate(o);
            }
            _entryText = null;
            foreach (var n in new[] { "TMPro.TMP_Text", "UnityEngine.UI.Text" })
                if (_entryText == null) _entryText = Rt.CompInChildren(_entry, Rt.Find(n));
            Rt.Set(_entryText, Label(), "text");
            var button = Rt.Comp(_entry, Rt.Find("UnityEngine.UI.Button"));
            if (_entryText == null || !Rt.ButtonClick(button, Click))
            {
                UnityEngine.Object.Destroy(_entry);
                _entry = null;
                return;  // tried again in a moment; after 30 seconds Frankenstein's own panel shows instead
            }
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
            Melon<Plugin>.Logger.Msg("Main menu: Creative added under New game.");
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
                catch (Exception e) { Melon<Plugin>.Logger.Warning("Main menu: " + e.Message); _lookUntil = 0; }
            }
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
            float w = 360, x = Screen.width * 0.08f, y = Screen.height * 0.62f;
            float h = _showRundown ? 150 + 22 * Math.Min(Rundown.Count, 12) : 150;
            GUI.Box(new Rect(x, y, w, h), "");
            GUI.Label(new Rect(x + 12, y + 8, w - 24, 22), "FRANKENSTEIN  ·  " + (Merge.Length > 0 ? Merge : "this merge") + (Creative ? "  ·  Creative" : ""));
            if (Creative)
            {
                if (GUI.Button(new Rect(x + 12, y + 36, w - 24, 32), "Back to my normal game")) Switch("normal");
            }
            else if (GUI.Button(new Rect(x + 12, y + 36, w - 24, 32), "Creative (every item, its own saves)")) Switch("creative");
            if (GUI.Button(new Rect(x + 12, y + 74, w - 24, 28), _showRundown ? "Hide the rundown" : "What's in this merge")) _showRundown = !_showRundown;
            GUI.Label(new Rect(x + 12, y + 108, w - 24, 40), _note.Length > 0 ? _note : (Who.Length > 0 ? "You play as " + Who + " (" + Close + ")" : "F6 in game: every item"));
            if (!_showRundown) return;
            for (int i = 0; i < Rundown.Count && i < 12; i++)
                GUI.Label(new Rect(x + 12, y + 146 + 22 * i, w - 24, 22), Rundown[i]);
        }
    }
}
