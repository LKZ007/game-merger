using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>levy's Schedule 1 x Hood Warfare reference, cut into art by the app (UserData/Frankenstein/ref-*.jpg
    /// and ref-icon-*.png, Hood Warfare merges only). When it's there the main menu, Profiles and the loading screen
    /// are laid out on the reference's own spots: the title, the city, the hooded figure and the taglines are in the
    /// pictures, and Frankenstein draws the parts that change (the list, the pills, the cards, the bar) where the
    /// reference has them.</summary>
    internal static class RefArt
    {
        static bool _tried;
        public static Texture2D Menu, Profiles, Loading, Avatar, Map_;
        /// <summary>levy's city map (Expansion's island is laid out on it).</summary>
        public static Texture2D Map { get { Load(); return Map_; } }
        static readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, GUIStyle> _styles = new Dictionary<string, GUIStyle>();

        public static bool On { get { Load(); return Menu != null; } }

        static void Load()
        {
            if (_tried) return;
            _tried = true;
            Menu = Tex("ref-menu.jpg");
            Profiles = Tex("ref-profiles.jpg");
            Loading = Tex("ref-loading.jpg");
            Avatar = Tex("ref-avatar.jpg");
            Map_ = Tex("ref-map.jpg");
            foreach (var n in new[] { "rep", "cash", "empire", "house", "money", "crown" })
            {
                var t = Tex("ref-icon-" + n + ".png");
                if (t != null) _icons[n] = t;
            }
        }

        static Texture2D Tex(string name)
        {
            var p = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", name);
            if (!File.Exists(p)) return null;
            try { return Rt.LoadImage(File.ReadAllBytes(p)); } catch (Exception) { return null; }
        }

        public static Texture2D Icon(string name)
        {
            Load();
            Texture2D t;
            return _icons.TryGetValue(name, out t) ? t : null;
        }

        /// <summary>Where a picture goes so it covers the whole screen (16:9 fits exactly).</summary>
        public static Rect Cover(Texture2D t, float W, float H)
        {
            float a = t.width / (float)t.height, bw = Mathf.Max(W, H * a), bh = bw / a;
            return new Rect((W - bw) / 2, (H - bh) / 2, bw, bh);
        }

        /// <summary>A label style by size, colour, alignment (3 left, 4 centre, 5 right) and bold, made once.</summary>
        public static GUIStyle Style(float size, Color c, int align, bool bold)
        {
            int px = Mathf.Max(9, Mathf.RoundToInt(size));
            var key = px + "|" + c + "|" + align + "|" + bold;
            GUIStyle st;
            if (_styles.TryGetValue(key, out st)) return st;
            st = new GUIStyle(GUI.skin.label) { fontSize = px, wordWrap = false, clipping = TextClipping.Overflow };
            st.normal.textColor = c;
            Set(st, "alignment", align);
            if (bold) Set(st, "fontStyle", 1);
            _styles[key] = st;
            return st;
        }

        static void Set(object o, string prop, int value)
        {
            try
            {
                var p = o.GetType().GetProperty(prop);
                p?.SetValue(o, System.Enum.ToObject(p.PropertyType, value), null);
            }
            catch (Exception) { }
        }
    }

    /// <summary>One panel of the reference mapped onto the screen: a point in the panel's own pixels (as the
    /// reference has it) lands on the same spot of the picture drawn full screen. Ox/Oy are the margins the app
    /// added to make the panel 16:9; Pw/Ph its size after that.</summary>
    internal struct RefPanel
    {
        public Rect Art;
        public float Ox, Oy, Pw, Ph;

        public RefPanel(Rect art, float ox, float oy, float pw, float ph) { Art = art; Ox = ox; Oy = oy; Pw = pw; Ph = ph; }

        public static RefPanel Menu(Rect art) => new RefPanel(art, 137, 0, 903, 508);
        public static RefPanel Profiles(Rect art) => new RefPanel(art, 0, 176, 1536, 864);
        public static RefPanel Loading(Rect art) => new RefPanel(art, 68, 0, 903, 508);

        /// <summary>Screen pixels per reference pixel, for sizes and fonts.</summary>
        public float S => Art.height / Ph;
        public float X(float x) => Art.x + (x + Ox) / Pw * Art.width;
        public float Y(float y) => Art.y + (y + Oy) / Ph * Art.height;
        public Rect R(float x0, float y0, float x1, float y1) => new Rect(X(x0), Y(y0), X(x1) - X(x0), Y(y1) - Y(y0));
    }
}
