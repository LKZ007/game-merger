using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using MelonLoader;
using S1API.Console;
using S1API.Items;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>F6 (or Back + Y on a controller): every item in the game, mods' items included, as a grid of
    /// tiles with the game's own icons. Click a tile to grab one, right-click for a full stack. Search and
    /// categories at the top. Only GUI calls without option arrays are used, so the same mod works on the
    /// Mono and IL2CPP builds of the game.</summary>
    internal static class ItemMenu
    {
        class Entry
        {
            public string Id, Name, Category, Search;
            public Sprite Icon;
            public int Stack;
            public Vehicles.Car Car;  // a vehicle: spawned in front of you
            public string Own;        // Frankenstein's own: "grenade" or "launcher"
        }

        static bool _open;
        public static bool IsOpen => _open;
        static List<Entry> _all = new List<Entry>();
        static List<string> _cats = new List<string>();
        static int _cat;
        static string _search = "";
        static string _lastSearch = null;
        static int _lastCat = -1;
        static List<Entry> _shown = new List<Entry>();
        static Vector2 _scroll;
        static int _sel;
        static string _toast = "";
        static float _toastUntil;
        static CursorLockMode _oldLock;
        static bool _oldVisible;
        static int _cols = 1;

        const float W = 940, H = 660, Tile = 104, TileH = 120, Gap = 8;

        public static void Load() { }

        static Sprite IconOf(ItemDefinition d)
        {
            try { return d.Icon ?? Rt.Get(Rt.Get(d, "S1ItemDefinition"), "Icon", "icon") as Sprite; }
            catch (Exception) { return null; }
        }

        static void Refresh()
        {
            try
            {
                _all = ItemManager.GetAllItemDefinitions()
                    .Where(d => d != null && !string.IsNullOrEmpty(d.ID) && OwnItems.KeyOf(d.ID) == null)  // Frankenstein's own guns come below
                    .Select(d => new Entry
                    {
                        Id = d.ID, Name = string.IsNullOrEmpty(d.Name) ? d.ID : d.Name, Category = IsWeapon(d.ID, d.Name) ? "Weapons" : CatName(d.Category),
                        Icon = IconOf(d), Stack = Math.Max(1, d.StackLimit),
                    })
                    .OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();
                // Frankenstein's own explosives, with the guns; every vehicle in the game in its own tab
                var shotgun = _all.FirstOrDefault(e => e.Id.ToLowerInvariant().Contains("shotgun"));
                _all.Add(new Entry { Id = "frankenstein:launcher", Name = "Grenade Launcher", Category = "Weapons", Own = "launcher", Stack = 1, Icon = shotgun?.Icon });
                _all.Add(new Entry { Id = "frankenstein:grenade", Name = "Frag Grenade", Category = "Weapons", Own = "grenade", Stack = 10 });
                // the AK, Uzi, sniper and RPG: More Guns' own if it loaded, otherwise Frankenstein's
                Func<string, string> Squash = x => (x ?? "").ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
                OwnGuns.MoreGunsLoaded = _all.Any(e => !e.Id.StartsWith("frankenstein:") && (Squash(e.Id).Contains("ak47") || Squash(e.Name).Contains("ak47")));
                if (!OwnGuns.MoreGunsLoaded)
                    foreach (var d in OwnGuns.Defs)
                    {
                        var on = _all.FirstOrDefault(e => Squash(e.Id).Contains(d.Base));
                        _all.Add(new Entry { Id = "frankenstein:" + d.Key, Name = d.Name, Category = "Weapons", Own = "gun:" + d.Key, Stack = 1, Icon = on?.Icon });
                    }
                Melon<Plugin>.Logger.Msg("Guns source: " + OwnGuns.Source + ".");
                try
                {
                    foreach (var c in Vehicles.All())
                        _all.Add(new Entry { Id = "vehicle:" + (c.Style != null ? c.Style.Key : c.Code), Name = c.Name, Category = "Vehicles", Car = c, Stack = 1 });
                }
                catch (Exception ex) { Melon<Plugin>.Logger.Warning("Item menu: vehicles: " + ex.Message); }
                _all = _all.OrderBy(e => e.Category).ThenBy(e => e.Name).ToList();
                foreach (var e in _all) e.Search = (e.Name + " " + e.Id + " " + e.Category + (e.Car != null ? " car " + e.Car.Kind + (e.Car.Style != null ? " frankenstein supercar suv " + e.Car.Style.Base : "") : "")).ToLowerInvariant();
                _cats = new[] { "All" }.Concat(_all.Select(e => e.Category).Distinct().OrderBy(c => c)).ToList();
            }
            catch (Exception e)
            {
                Melon<Plugin>.Logger.Warning("Item menu: couldn't list items: " + e.Message);
            }
            _lastSearch = null;
        }

        // ---------- controller (XInput, read directly so it works whatever the game's own input does) ----------

        [StructLayout(LayoutKind.Sequential)]
        struct XState
        {
            public uint Packet;
            public ushort Buttons;
            public byte LT, RT;
            public short LX, LY, RX, RY;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        static extern int XInputGetState(int user, out XState state);

        const ushort Up = 0x0001, Down = 0x0002, Left = 0x0004, Right = 0x0008, Back = 0x0020,
                     LB = 0x0100, RB = 0x0200, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
        static ushort _prev;
        static bool _padOk = true;
        static float _repeatAt;

        static ushort Pad()
        {
            if (!_padOk) return 0;
            try
            {
                if (XInputGetState(0, out var s) != 0) return 0;
                ushort b = s.Buttons;
                // the left stick moves the selection like the d-pad
                if (s.LY > 16000) b |= Up;
                if (s.LY < -16000) b |= Down;
                if (s.LX < -16000) b |= Left;
                if (s.LX > 16000) b |= Right;
                return b;
            }
            catch (Exception) { _padOk = false; return 0; } // no XInput on this PC
        }

        public static void Update()
        {
            ushort now = Pad();
            ushort pressed = (ushort)(now & ~_prev);
            // held direction repeats, so you can scroll through a long list
            const ushort dirs = Up | Down | Left | Right;
            if ((now & dirs) != 0 && (now & dirs) == (_prev & dirs) && Time.unscaledTime > _repeatAt)
            {
                pressed |= (ushort)(now & dirs);
                _repeatAt = Time.unscaledTime + 0.09f;
            }
            else if ((pressed & dirs) != 0) _repeatAt = Time.unscaledTime + 0.35f;
            _prev = now;

            bool toggle = Input.GetKeyDown(KeyCode.F6) || ((now & Back) != 0 && (pressed & Y) != 0);
            if (toggle)
            {
                if (!Plugin.MenuAllowed && !MainMenu.Creative && !_open)
                {
                    Toast("The item menu is for Creative. Switch this save to Creative with Game Mode (F8 or the wheel), or turn it on for Survival in Frankenstein > Settings.");
                    return;
                }
                if (_open) Close();
                else Open();
                return;
            }
            if (!_open) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (Input.GetKeyDown(KeyCode.Escape) || (pressed & B) != 0) { Close(); return; }
            Type();

            int n = _shown.Count;
            if ((pressed & Back) != 0 && (now & Y) == 0) _search = ""; // Back alone clears the search
            if ((pressed & Left) != 0 || Input.GetKeyDown(KeyCode.LeftArrow)) Move(-1);
            if ((pressed & Right) != 0 || Input.GetKeyDown(KeyCode.RightArrow)) Move(1);
            if ((pressed & Up) != 0 || Input.GetKeyDown(KeyCode.UpArrow)) Move(-_cols);
            if ((pressed & Down) != 0 || Input.GetKeyDown(KeyCode.DownArrow)) Move(_cols);
            if ((pressed & LB) != 0 && _cats.Count > 0) _cat = (_cat + _cats.Count - 1) % _cats.Count;
            if ((pressed & RB) != 0 && _cats.Count > 0) _cat = (_cat + 1) % _cats.Count;
            if (n > 0 && _sel < n)
            {
                if ((pressed & A) != 0 || Input.GetKeyDown(KeyCode.Return)) Give(_shown[_sel], 1);
                if ((pressed & X) != 0) Give(_shown[_sel], Math.Min(10, _shown[_sel].Stack));
                if ((pressed & Y) != 0 && (now & Back) == 0) Give(_shown[_sel], _shown[_sel].Stack);
            }
        }

        // The search box reads the keyboard itself instead of using a Unity text field: the game's own input
        // fights over text fields (and they don't take focus reliably on every build), so typing anywhere
        // while the menu is open goes into the search. The game is paused meanwhile, so keys don't move you.
        static void Type()
        {
            string typed = Input.inputString;
            if (string.IsNullOrEmpty(typed)) return;
            foreach (char c in typed)
            {
                if (c == '\b') { if (_search.Length > 0) _search = _search.Substring(0, _search.Length - 1); }
                else if (c == '\n' || c == '\r') { }
                else if (!char.IsControl(c) && _search.Length < 40) _search += c;
            }
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Backspace)) _search = "";
        }

        static void Move(int by)
        {
            if (_shown.Count == 0) return;
            _sel = Math.Max(0, Math.Min(_shown.Count - 1, _sel + by));
            // keep the selected tile in view
            float rowTop = (_sel / _cols) * (TileH + Gap);
            float viewH = H - 190;
            if (rowTop < _scroll.y) _scroll.y = rowTop;
            if (rowTop + TileH > _scroll.y + viewH) _scroll.y = rowTop + TileH - viewH;
        }

        static float _oldScale = 1f;

        public static void Toggle() { if (_open) Close(); else if (!Plugin.MenuAllowed && !MainMenu.Creative) Toast("The item menu is for Creative. Switch this save to Creative with Game Mode (F8 or the wheel)."); else Open(); }

        static void Open()
        {
            Refresh();
            _open = true;
            _oldLock = Cursor.lockState;
            _oldVisible = Cursor.visible;
            _oldScale = Time.timeScale;
            Time.timeScale = 0f; // paused while you browse and type
        }

        static void Close()
        {
            _open = false;
            Cursor.lockState = _oldLock;
            Cursor.visible = _oldVisible;
            Time.timeScale = _oldScale > 0f ? _oldScale : 1f;
        }

        public static void Notify(string text) => Toast(text);

        static void Toast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 3f;
        }

        static void Give(Entry e, int qty)
        {
            try
            {
                if (e.Car != null) { Vehicles.Spawn(e.Car); Close(); return; }  // closed, so you see it arrive
                if (e.Own == "grenade") { Boom.GiveGrenades(qty); return; }
                if (e.Own == "launcher") { Boom.GiveLauncher(); return; }
                if (e.Own != null && e.Own.StartsWith("gun:")) { OwnGuns.Give(OwnGuns.Defs.First(d => "gun:" + d.Key == e.Own)); return; }
                ConsoleHelper.AddItemToInventory(e.Id, qty);
                Toast("Got " + qty + " x " + e.Name);
            }
            catch (Exception ex)
            {
                Toast("Couldn't give " + e.Name + ": " + ex.Message);
            }
        }

        static bool _iconsLogged;

        static void DrawIcon(Entry e, Rect at)
        {
            if (e.Icon == null) return;
            try { if (!Paint.Sprite(at, e.Icon)) e.Icon = null; }
            catch (Exception ex)
            {
                e.Icon = null;
                if (!_iconsLogged) { _iconsLogged = true; Melon<Plugin>.Logger.Warning("Icons: " + ex.Message); }
            }
        }

        // every gun (the game's and More Guns': pistols, revolver, shotgun, AK, SMG, sniper, minigun, RPG), its
        // ammo and melee weapons go together under Weapons
        static readonly string[] WeaponWords = { "m1911", "pistol", "revolver", "shotgun", "rifle", "smg", "ak47", "sniper", "minigun",
                                                 "rpg", "rocket", "uzi", "glock", "magazine", "cylinder", "shell", "ammo", "machete",
                                                 "knife", "bat", "taser", "grenade", "launcher" };

        static bool IsWeapon(string id, string name)
        {
            var key = ((id ?? "") + " " + (name ?? "")).ToLowerInvariant().Replace(" ", "").Replace("_", "");
            if (key.Contains("battery") || key.Contains("bathtub") || key.Contains("seashell")) return false;
            return WeaponWords.Any(w => key.Contains(w));
        }

        // the game's item categories by number, for builds where a category has no name (it shows as "12")
        static readonly string[] CatNames = { "Product", "Packaging", "Growing", "Tools", "Furniture", "Lighting", "Cash",
                                              "Consumable", "Equipment", "Ingredient", "Decoration", "Clothing", "Storage" };

        static string CatName(object c)
        {
            var s = c?.ToString() ?? "";
            int n;
            if (int.TryParse(s, out n)) return n >= 0 && n < CatNames.Length ? CatNames[n] : "Other";
            return s;
        }

        // no picture in the game for this item: a coloured badge with its first letter, by category
        static GUIStyle _glyph;
        static void Glyph(Entry e, Rect at)
        {
            int h = 0;
            foreach (var ch in e.Category) h = h * 31 + ch;
            var c = Color.HSVToRGB((Math.Abs(h) % 360) / 360f, 0.55f, 0.75f);
            Paint.Fill(at, c);
            if (_glyph == null) { _glyph = new GUIStyle(GUI.skin.box) { fontSize = 30 }; _glyph.normal.background = null; }  // the box style centres
            GUI.Label(at, e.Name.Length > 0 ? e.Name.Substring(0, 1).ToUpperInvariant() : "?", _glyph);
        }

        public static void Draw()
        {
            if (!_open && Time.unscaledTime < _toastUntil)
                GUI.Box(new Rect(Screen.width / 2f - 260, 40, 520, 30), _toast);
            if (!_open) return;

            float x = (Screen.width - W) / 2f, y = (Screen.height - H) / 2f;
            Paint.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.01f, 0.04f, 0.55f));
            Paint.Fill(new Rect(x - 6, y - 6, W + 12, H + 12), new Color(0.65f, 0.55f, 0.98f, 0.9f));
            Paint.Fill(new Rect(x, y, W, H), new Color(0.07f, 0.05f, 0.11f, 1f));
            GUI.Label(new Rect(x + 16, y + 10, 580, 24), "FRANKENSTEIN " + Plugin.Version + "  ·  Every item in this merge (" + _all.Count + ")" + (_cats.Count > _cat && _cats[_cat] == "Weapons" ? "  ·  " + OwnGuns.Source : ""));
            if (GUI.Button(new Rect(x + W - 40, y + 8, 28, 26), "X")) { Close(); return; }
            GUI.Label(new Rect(x + 16, y + 42, 60, 24), "Search");
            bool blink = ((int)(Time.unscaledTime * 2)) % 2 == 0;
            GUI.Box(new Rect(x + 76, y + 40, 280, 26), "");
            GUI.Label(new Rect(x + 82, y + 42, 270, 24), (_search.Length > 0 ? _search : (blink ? "" : "just start typing…")) + (blink ? "|" : ""));
            if (_search.Length > 0 && GUI.Button(new Rect(x + 360, y + 40, 40, 26), "Clear")) _search = "";
            GUI.Label(new Rect(x + 410, y + 42, W - 426, 24), "Type to search  ·  Click: grab 1  ·  Right-click: stack  ·  Pad: A / X (10) / Y (stack), LB RB category");

            // categories, as a row of buttons that wraps
            float cx = x + 16, cy = y + 76;
            for (int i = 0; i < _cats.Count; i++)
            {
                var c = _cats[i];
                float w = 16 + c.Length * 8;
                if (cx + w > x + W - 16) { cx = x + 16; cy += 30; }
                if (GUI.Toggle(new Rect(cx, cy, w, 26), _cat == i, c, GUI.skin.button)) _cat = i;
                cx += w + 6;
            }

            if (_search != _lastSearch || _cat != _lastCat)
            {
                var words = (_search ?? "").ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var cat = _cat < _cats.Count ? _cats[_cat] : "All";
                _shown = _all.Where(e => (cat == "All" || e.Category == cat) && words.All(w => e.Search.Contains(w))).ToList();
                _lastSearch = _search;
                _lastCat = _cat;
                _scroll = Vector2.zero;
                _sel = 0;
            }

            float top = cy + 38, listH = y + H - top - 34, listW = W - 32;
            _cols = Math.Max(1, (int)((listW - 16) / (Tile + Gap)));
            int rows = (_shown.Count + _cols - 1) / _cols;
            var view = new Rect(0, 0, listW - 20, Math.Max(listH, rows * (TileH + Gap)));
            _scroll = GUI.BeginScrollView(new Rect(x + 16, top, listW, listH), _scroll, view);
            int firstRow = Math.Max(0, (int)(_scroll.y / (TileH + Gap)) - 1);
            int lastRow = Math.Min(rows, firstRow + (int)(listH / (TileH + Gap)) + 3);
            var ev = Event.current;
            for (int row = firstRow; row < lastRow; row++)
            {
                for (int col = 0; col < _cols; col++)
                {
                    int i = row * _cols + col;
                    if (i >= _shown.Count) break;
                    var e = _shown[i];
                    var r = new Rect(col * (Tile + Gap), row * (TileH + Gap), Tile, TileH);
                    // a right-click grabs a whole stack; a left-click (the button itself) grabs one
                    if (ev != null && ev.type == EventType.MouseDown && ev.button == 1 && r.Contains(ev.mousePosition))
                    {
                        _sel = i;
                        Give(e, e.Stack);
                        ev.Use();
                    }
                    string label = e.Name.Length > 22 ? e.Name.Substring(0, 21) + "…" : e.Name;
                    if (GUI.Button(r, "")) { _sel = i; Give(e, 1); }
                    if (i == _sel) GUI.Box(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), "");
                    if (e.Car != null) Vehicles.Picture(e.Car, new Rect(r.x + 8, r.y + 10, Tile - 16, 60));
                    else
                    {
                        var look = e.Own == "launcher" ? "launcher" : e.Own != null && e.Own.StartsWith("gun:") ? e.Own.Substring(4) : null;
                        if (look != null) Looks.Icon(look, new Rect(r.x + 8, r.y + 14, Tile - 16, 56));  // its own picture, not the base gun's
                        else
                        {
                            DrawIcon(e, new Rect(r.x + (Tile - 64) / 2, r.y + 8, 64, 64));
                            if (e.Icon == null) Glyph(e, new Rect(r.x + (Tile - 56) / 2, r.y + 12, 56, 56));
                        }
                    }
                    GUI.Label(new Rect(r.x + 6, r.y + 76, Tile - 12, 40), label);
                }
            }
            GUI.EndScrollView();
            var selName = _sel < _shown.Count ? _shown[_sel].Name + "  (" + _shown[_sel].Category + ")" : "";
            GUI.Label(new Rect(x + 16, y + H - 28, W - 32, 24),
                Time.unscaledTime < _toastUntil ? _toast : _shown.Count + " shown  ·  " + selName + "  ·  F6, Esc or B to close");
            if (_cats.Count > _cat && _cats[_cat] == "Vehicles" && GUI.Button(new Rect(x + W - 236, y + H - 30, 220, 26), "Vehicle Spawner (F3)")) { Close(); Showroom.Toggle(); }
        }
    }
}
