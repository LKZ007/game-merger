using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The merge's own loading screen, drawn over Schedule I's while a save loads: Frankenstein's merged
    /// picture of the two games (made by the app, UserData/Frankenstein/loading.jpg), the merge's title in the
    /// Extension game's colours, its tips in turn, and the game's own loading status.</summary>
    internal static class Loading
    {
        public static string Title = "", Host = "Schedule I", Extension = "", Sub = "";
        public static List<string> Tips = new List<string>();
        public static Color Accent = new Color(0.88f, 0.11f, 0.28f);  // Hood Warfare red, unless the settings say otherwise

        static Texture2D _art;
        static bool _artTried, _left, _showing;
        public static bool Showing => _showing;
        static float _since, _fallbackUntil;
        static object _screen;
        static float _nextFind;
        static GUIStyle _big, _mid, _small;

        public static void Read(object s)
        {
            if (s == null) return;
            Title = J.S(s, "title");
            Host = J.S(s, "host", "Schedule I");
            Extension = J.S(s, "extension");
            Sub = J.S(s, "sub");
            foreach (var t in J.L(s, "tips")) Tips.Add(t.ToString());
            var hex = J.S(s, "accent");
            if (hex.Length == 7 && hex[0] == '#')
            {
                try
                {
                    Accent = new Color(Convert.ToInt32(hex.Substring(1, 2), 16) / 255f, Convert.ToInt32(hex.Substring(3, 2), 16) / 255f,
                                       Convert.ToInt32(hex.Substring(5, 2), 16) / 255f);
                }
                catch (Exception) { }
            }
        }

        public static bool On => Extension.Length > 0 || Tips.Count > 0;

        public static void Scene(string name)
        {
            // leaving the menu for a save: the screen shows until the game's own loading screen closes
            if (name != null && name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0) { _left = false; return; }
            if (!_left) { _left = true; _fallbackUntil = Time.unscaledTime + 12f; }
        }

        static bool GameLoading()
        {
            if (Time.unscaledTime >= _nextFind)
            {
                _nextFind = Time.unscaledTime + 1f;
                if (_screen == null || (_screen as UnityEngine.Object) == null)
                {
                    var t = Rt.Find("ScheduleOne.UI.LoadingScreen");
                    var all = Rt.All(t);
                    _screen = all.Count > 0 ? all[0] : null;
                }
            }
            if (_screen == null) return _left && Time.unscaledTime < _fallbackUntil;
            var open = Rt.Get(_screen, "IsOpen", "isOpen", "Open");
            if (open is bool b) return b;
            var canvas = Rt.Get(_screen, "Canvas", "canvas");
            var en = Rt.Get(canvas, "enabled");
            return en is bool e ? e : _left && Time.unscaledTime < _fallbackUntil;
        }

        static string Status()
        {
            var s = Rt.Get(_screen, "LoadStatusLabel", "StatusLabel", "LoadingLabel", "StatusText", "LoadStatusText");
            return s as string ?? Rt.Text(s) ?? "";
        }

        static void Art()
        {
            if (_artTried) return;
            _artTried = true;
            foreach (var n in new[] { "loading.jpg", "loading.png" })
            {
                var p = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", n);
                if (!File.Exists(p)) continue;
                try { _art = Rt.LoadImage(File.ReadAllBytes(p)); } catch (Exception) { }
                if (_art != null) return;
            }
        }

        static void Fill(Rect r, Color c) => Paint.Fill(r, c);

        /// <summary>Like levy's reference: the night city, the title lockup in the middle, LOADING..., a green bar
        /// and the tagline. One scene, painted by the app.</summary>
        static void DrawScene(float W, float H, float t)
        {
            var back = MenuSkin.Backdrop();
            float a = back.width / (float)back.height, z = 1.03f + 0.002f * Math.Min(t, 40f), bw = Mathf.Max(W, H * a) * z, bh = bw / a;
            Paint.Picture(new Rect((W - bw) / 2 - 6f * (float)Math.Sin(t * 0.07f), (H - bh) / 2, bw, bh), back);
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.35f));
            var logo = MenuSkin.Logo();
            float y = H * 0.24f;
            if (logo != null)
            {
                float lh = H * 0.24f, lw = lh * logo.width / logo.height;
                if (lw > W * 0.7f) { lw = W * 0.7f; lh = lw * logo.height / logo.width; }
                Paint.Picture(new Rect((W - lw) / 2, y, lw, lh), logo);
                y += lh + H * 0.08f;
            }
            else y = H * 0.55f;
            if (_center == null)
            {
                _center = new GUIStyle(GUI.skin.label) { fontSize = Math.Max(20, (int)(H * 0.034f)), richText = true };
                _centerSmall = new GUIStyle(GUI.skin.label) { fontSize = Math.Max(13, (int)(H * 0.02f)), richText = true };
                foreach (var st in new[] { _center, _centerSmall })
                {
                    var p = st.GetType().GetProperty("alignment");
                    try { p?.SetValue(st, System.Enum.ToObject(p.PropertyType, 4), null); } catch (Exception) { }
                }
            }
            GUI.Label(new Rect(0, y, W, H * 0.05f), "<b>LOADING" + new string('.', 1 + (int)(t * 2) % 3) + "</b>", _center);
            float bw2 = W * 0.36f, bx = (W - bw2) / 2, by = y + H * 0.065f;
            Fill(new Rect(bx - 2, by - 2, bw2 + 4, 12), new Color(1, 1, 1, 0.35f));
            Fill(new Rect(bx, by, bw2, 8), new Color(0.06f, 0.07f, 0.08f, 0.9f));
            float p2 = 1f - 1f / (1f + t * 0.12f);  // eases toward full while the game loads
            Fill(new Rect(bx, by, bw2 * p2, 8), new Color(0.27f, 0.72f, 0.29f));
            string tag = Sub.Length > 0 ? Sub : "Build your empire. Run your block.";
            GUI.Label(new Rect(0, by + H * 0.03f, W, H * 0.04f), "<color=#C9CDD4>" + tag + "</color>", _centerSmall);
            if (Tips.Count > 0)
                GUI.Label(new Rect(0, H * 0.9f, W, H * 0.04f), "<color=#9CA3AF>" + Tips[(int)(t / 6f) % Tips.Count] + "</color>", _centerSmall);
        }

        static GUIStyle _center, _centerSmall;

        /// <summary>levy's reference loading screen (Hood Warfare merges): the picture has the lockup, the crews,
        /// LOADING... and the tagline; the green bar fills where the reference has it.</summary>
        static void DrawRef(float W, float H, float t)
        {
            var P = RefPanel.Loading(RefArt.Cover(RefArt.Loading, W, H));
            Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.03f));
            Paint.Picture(P.Art, RefArt.Loading);
            var track = P.R(177, 425, 588, 435);
            Fill(new Rect(track.x - 1, track.y - 1, track.width + 2, track.height + 2), new Color(1, 1, 1, 0.45f));
            Fill(track, new Color(0.05f, 0.06f, 0.07f, 0.95f));
            float p = 1f - 1f / (1f + t * 0.12f);  // eases toward full while the game loads
            Fill(new Rect(track.x, track.y, track.width * p, track.height), new Color(0.27f, 0.72f, 0.29f));
        }

        static string Hex(Color c) => "#" + ((int)(c.r * 255)).ToString("X2") + ((int)(c.g * 255)).ToString("X2") + ((int)(c.b * 255)).ToString("X2");

        public static void Draw()
        {
            if (!On) return;
            bool now;
            try { now = GameLoading(); } catch (Exception) { now = false; }
            if (now && !_showing) _since = Time.unscaledTime;
            _showing = now;
            if (!now) return;
            Art();
            float W = Screen.width, H = Screen.height, t = Time.unscaledTime - _since;
            if (RefArt.On && RefArt.Loading != null) { DrawRef(W, H, t); return; }
            if (MenuSkin.Backdrop() != null) { DrawScene(W, H, t); return; }
            Fill(new Rect(0, 0, W, H), new Color(0.03f, 0.02f, 0.05f));
            if (_art != null)
            {
                // a slow push in across the merged picture
                float z = 1.04f + 0.0025f * Math.Min(t, 40f);
                float w = W * z, h = H * z;
                Paint.Picture(new Rect((W - w) / 2 - 8f * (float)Math.Sin(t * 0.07f), (H - h) / 2, w, h), _art);
            }
            // the Extension's colour washed over the top and a dark fade at the bottom for the words
            Fill(new Rect(0, 0, W, H), new Color(Accent.r, Accent.g, Accent.b, 0.10f));
            for (int i = 0; i < 40; i++)
                Fill(new Rect(0, H * 0.5f + i * H * 0.0125f, W, H * 0.0125f + 1), new Color(0.02f, 0.01f, 0.03f, 0.025f * i));
            Fill(new Rect(0, 0, W, H * 0.06f), new Color(0, 0, 0, 0.55f));
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.label) { fontSize = Math.Max(30, (int)(H * 0.065f)), richText = true, wordWrap = false };
                _mid = new GUIStyle(GUI.skin.label) { fontSize = Math.Max(16, (int)(H * 0.024f)), richText = true, wordWrap = true };
                _small = new GUIStyle(GUI.skin.label) { fontSize = Math.Max(12, (int)(H * 0.017f)), richText = true, wordWrap = true };
            }
            float x = W * 0.05f, y = H * 0.66f;
            string accent = Hex(Accent);
            var title = "<b>" + Host.ToUpperInvariant() + "</b> <color=#A78BFA>×</color> <b><color=" + accent + ">" +
                        (Extension.Length > 0 ? Extension : Title).ToUpperInvariant() + "</color></b>";
            GUI.Label(new Rect(x + 3, y + 3, W * 0.9f, H * 0.1f), "<color=#000000AA>" + System.Text.RegularExpressions.Regex.Replace(title, "<color=[^>]+>|</color>", "") + "</color>", _big);
            GUI.Label(new Rect(x, y, W * 0.9f, H * 0.1f), title, _big);
            y += H * 0.085f;
            Fill(new Rect(x, y, W * 0.12f, 5), Accent);
            if (Sub.Length > 0) GUI.Label(new Rect(x, y + 10, W * 0.8f, H * 0.04f), "<color=#C4B5FD>" + Sub + "</color>", _mid);
            y += H * 0.07f;
            if (Tips.Count > 0)
            {
                var tip = Tips[(int)(t / 6f) % Tips.Count];
                GUI.Label(new Rect(x, y, W * 0.12f, H * 0.03f), "<b><color=" + accent + ">HOOD TIP</color></b>", _small);
                GUI.Label(new Rect(x + W * 0.09f, y, W * 0.62f, H * 0.08f), tip, _mid);
            }
            // the game's own status and a moving bar, bottom right
            var status = "";
            try { status = Status(); } catch (Exception) { }
            if (status.Length == 0) status = "Loading the block" + new string('.', 1 + (int)(t * 2) % 3);
            GUI.Label(new Rect(W * 0.72f, H * 0.9f, W * 0.25f, H * 0.04f), "<color=#E9E5F5>" + status + "</color>", _small);
            float bw = W * 0.25f, bx = W * 0.72f, by = H * 0.945f;
            Fill(new Rect(bx, by, bw, 4), new Color(1, 1, 1, 0.12f));
            float k = (t * 0.35f) % 1f;
            Fill(new Rect(bx + bw * k * 0.75f, by, bw * 0.25f, 4), Accent);
            GUI.Label(new Rect(x, H * 0.012f, W * 0.6f, H * 0.04f), "<color=#A78BFA><b>FRANKENSTEIN</b></color>  <color=#E9E5F5>" + Title + "</color>", _small);
        }
    }
}
