using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using S1API.Console;
using S1API.Items;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>F6 (or Back + Y on a controller): every item in the game, mods' items included, searchable
    /// and by category. Click one to get it. Only GUI calls without option arrays are used, so the same
    /// mod works on the Mono and IL2CPP builds of the game.</summary>
    internal static class ItemMenu
    {
        class Entry
        {
            public string Id, Name, Category, Search;
            public Sprite Icon;
            public int Stack;
        }

        static bool _open;
        static List<Entry> _all = new List<Entry>();
        static List<string> _cats = new List<string>();
        static string _cat = "All";
        static string _search = "";
        static string _lastSearch = null, _lastCat = null;
        static List<Entry> _shown = new List<Entry>();
        static Vector2 _scroll;
        static string _toast = "";
        static float _toastUntil;
        static CursorLockMode _oldLock;
        static bool _oldVisible;

        const float W = 760, H = 560, Row = 44;

        public static void Load() { }

        static void Refresh()
        {
            try
            {
                _all = ItemManager.GetAllItemDefinitions()
                    .Where(d => d != null && !string.IsNullOrEmpty(d.ID))
                    .Select(d => new Entry
                    {
                        Id = d.ID, Name = string.IsNullOrEmpty(d.Name) ? d.ID : d.Name, Category = d.Category.ToString(),
                        Icon = d.Icon, Stack = Math.Max(1, d.StackLimit),
                    })
                    .OrderBy(e => e.Name).ToList();
                foreach (var e in _all) e.Search = (e.Name + " " + e.Id + " " + e.Category).ToLowerInvariant();
                _cats = new[] { "All" }.Concat(_all.Select(e => e.Category).Distinct().OrderBy(c => c)).ToList();
            }
            catch (Exception e)
            {
                Melon<Plugin>.Logger.Warning("Item menu: couldn't list items: " + e.Message);
            }
            _lastSearch = null;
        }

        public static void Update()
        {
            bool pad = Input.GetKey(KeyCode.JoystickButton6) && Input.GetKeyDown(KeyCode.JoystickButton3);
            if (Input.GetKeyDown(KeyCode.F6) || pad)
            {
                if (!Plugin.MenuAllowed && !_open)
                {
                    Toast("The item menu is off for normal play. Turn it on in Frankenstein > Settings, or play in Creative.");
                    return;
                }
                _open = !_open;
                if (_open)
                {
                    Refresh();
                    _oldLock = Cursor.lockState;
                    _oldVisible = Cursor.visible;
                }
                else
                {
                    Cursor.lockState = _oldLock;
                    Cursor.visible = _oldVisible;
                }
            }
            if (_open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (Input.GetKeyDown(KeyCode.Escape)) { _open = false; Cursor.lockState = _oldLock; Cursor.visible = _oldVisible; }
            }
        }

        static void Toast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 3f;
        }

        static void Give(Entry e, int qty)
        {
            try
            {
                ConsoleHelper.AddItemToInventory(e.Id, qty);
                Toast("Got " + qty + " x " + e.Name);
            }
            catch (Exception ex)
            {
                Toast("Couldn't give " + e.Name + ": " + ex.Message);
            }
        }

        public static void Draw()
        {
            if (!_open && Time.unscaledTime < _toastUntil)
                GUI.Box(new Rect(Screen.width / 2f - 260, 40, 520, 30), _toast);
            if (!_open) return;

            float x = (Screen.width - W) / 2f, y = (Screen.height - H) / 2f;
            GUI.Box(new Rect(x - 6, y - 6, W + 12, H + 12), "");
            GUI.Box(new Rect(x, y, W, H), "");
            GUI.Label(new Rect(x + 16, y + 10, 400, 24), "FRANKENSTEIN  ·  Every item in this merge (" + _all.Count + ")");
            if (GUI.Button(new Rect(x + W - 40, y + 8, 28, 26), "X")) { _open = false; Cursor.lockState = _oldLock; Cursor.visible = _oldVisible; return; }

            GUI.Label(new Rect(x + 16, y + 42, 60, 24), "Search");
            _search = GUI.TextField(new Rect(x + 76, y + 40, 300, 26), _search ?? "");

            // categories, as a row of buttons that wraps
            float cx = x + 16, cy = y + 76;
            foreach (var c in _cats)
            {
                float w = 14 + c.Length * 8;
                if (cx + w > x + W - 16) { cx = x + 16; cy += 30; }
                if (GUI.Toggle(new Rect(cx, cy, w, 26), _cat == c, c, GUI.skin.button)) _cat = c;
                cx += w + 6;
            }

            if (_search != _lastSearch || _cat != _lastCat)
            {
                var words = (_search ?? "").ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                _shown = _all.Where(e => (_cat == "All" || e.Category == _cat) && words.All(w => e.Search.Contains(w))).ToList();
                _lastSearch = _search;
                _lastCat = _cat;
                _scroll = Vector2.zero;
            }

            float top = cy + 38, listH = y + H - top - 34;
            var view = new Rect(0, 0, W - 48, Math.Max(listH, _shown.Count * Row));
            _scroll = GUI.BeginScrollView(new Rect(x + 16, top, W - 32, listH), _scroll, view);
            int first = Math.Max(0, (int)(_scroll.y / Row) - 1), last = Math.Min(_shown.Count, first + (int)(listH / Row) + 3);
            for (int i = first; i < last; i++)
            {
                var e = _shown[i];
                float ry = i * Row;
                if (e.Icon != null)
                {
                    try
                    {
                        var t = e.Icon.texture;
                        var r = e.Icon.textureRect;
                        GUI.DrawTextureWithTexCoords(new Rect(4, ry + 4, 36, 36), t,
                            new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
                    }
                    catch (Exception) { e.Icon = null; }
                }
                if (GUI.Button(new Rect(48, ry + 4, W - 300, 36), e.Name + "   (" + e.Category + ")")) Give(e, 1);
                if (GUI.Button(new Rect(W - 244, ry + 4, 56, 36), "x1")) Give(e, 1);
                if (GUI.Button(new Rect(W - 184, ry + 4, 56, 36), "x10")) Give(e, Math.Min(10, e.Stack));
                if (GUI.Button(new Rect(W - 124, ry + 4, 72, 36), "Stack")) Give(e, e.Stack);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(x + 16, y + H - 28, W - 32, 24),
                Time.unscaledTime < _toastUntil ? _toast : _shown.Count + " shown  ·  F6 or Esc to close  ·  items go to your inventory");
        }
    }
}
