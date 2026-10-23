using System;
using System.Linq;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The island's map (F4, F8 > City map, or the wheel): levy's map picture with you on it, every
    /// district and gang HQ, and the bridge back to town. Click a district (or pick it with the d-pad and press A)
    /// to go there. Without the picture it draws the island's own plan.</summary>
    internal static class CityMap
    {
        public static bool Open;
        static int _sel = -1;
        static GUIStyle _name, _small;
        static Texture2D _dot;

        public static void Toggle()
        {
            if (!Expansion.On) { ItemMenu.Notify("This merge has no city island."); return; }
            Open = !Open;
            _sel = -1;
        }

        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4)) Toggle();
            if (!Open) return;
            if (Input.GetKeyDown(KeyCode.Escape) || (Pad.Pressed & Pad.B) != 0) { Open = false; return; }
            int n = Expansion.Plan.Length;
            if ((Pad.Pressed & (Pad.Down | Pad.Right)) != 0) _sel = (_sel + 1) % n;
            if ((Pad.Pressed & (Pad.Up | Pad.Left)) != 0) _sel = (_sel + n - 1) % n;
            if (_sel >= 0 && (Pad.Pressed & Pad.A) != 0) Go(_sel);
        }

        static void Go(int i)
        {
            if (!Expansion.Built) { ItemMenu.Notify("The island is still being built: a few more seconds."); return; }
            Expansion.Travel(i);
            Open = false;
        }

        public static void Draw()
        {
            if (!Open) return;
            float W = Screen.width, H = Screen.height, k = H / 1080f;
            if (_name == null || _name.fontSize != Mathf.RoundToInt(17 * k))
            {
                _name = RefArt.Style(17 * k, Color.white, 4, false);
                _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * k), wordWrap = false };
                _small.normal.textColor = new Color(0.8f, 0.82f, 0.86f);
            }
            Paint.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.05f, 0.92f));
            // the map, as big as fits, with room on the left for the way back to town
            float mh = H * 0.86f, mw = mh * Expansion.PW / Expansion.PH;
            if (mw > W - 140 * k) { mw = W - 140 * k; mh = mw * Expansion.PH / Expansion.PW; }
            float mx = (W - mw) / 2 + 50 * k, my = (H - mh) / 2 + 14 * k, sc = mw / Expansion.PW;
            Func<Vector2, Vector2> at = p => new Vector2(mx + p.x * sc, my + p.y * sc);
            var art = RefArt.Map;
            if (art != null) Paint.Picture(new Rect(mx, my, mw, mh), art);
            else
            {
                Paint.Fill(new Rect(mx, my, mw, mh), new Color(0.06f, 0.18f, 0.27f));
                foreach (var d in Expansion.Plan)
                    Paint.Fill(new Rect(mx + d.Px.x * sc, my + d.Px.y * sc, d.Px.width * sc, d.Px.height * sc), new Color(0.24f, 0.32f, 0.2f));
            }
            GUI.Label(new Rect(mx, my - 34 * k, mw, 30 * k), "<b>THE CITY</b>   ·   F4 or B to close   ·   " + (Pad.InUse ? "d-pad: pick a district, A: go there" : "click a district to go there"), _small);
            // the way back to town
            var link = at(new Vector2(0, 455));
            Paint.Fill(new Rect(link.x - 60 * k, link.y - 3 * k, 60 * k, 6 * k), new Color(0.9f, 0.9f, 0.9f, 0.8f));
            GUI.Label(new Rect(link.x - 140 * k, link.y + 6 * k, 140 * k, 40 * k), "← to town\n(Hyland Point)", _small);
            var mouse = Event.current.mousePosition;
            for (int i = 0; i < Expansion.Plan.Length; i++)
            {
                var d = Expansion.Plan[i];
                var r = new Rect(mx + d.Px.x * sc, my + d.Px.y * sc, d.Px.width * sc, d.Px.height * sc);
                bool hot = r.Contains(mouse) || i == _sel;
                if (hot)
                {
                    var col = d.Gang.Length > 0 ? Gangs.ColorOf(d.Gang) : new Color(0.66f, 0.55f, 0.98f);
                    Outline(r, new Color(col.r, col.g, col.b, 0.95f), 3f * k);
                    Paint.Fill(r, new Color(col.r, col.g, col.b, 0.12f));
                    var label = new Rect(r.center.x - 160 * k, r.yMax + 2 * k, 320 * k, 24 * k);
                    Paint.Fill(new Rect(label.x + 60 * k, label.y, label.width - 120 * k, label.height), new Color(0, 0, 0, 0.75f));
                    GUI.Label(label, "<b>" + d.Name + "</b>" + (d.Gang.Length > 0 ? " · " + Gangs.NameOf(d.Gang) : ""), _name);
                }
                if (art == null) GUI.Label(new Rect(r.x, r.center.y - 12 * k, r.width, 24 * k), d.Name, _name);
                if (d.Gang.Length > 0)
                {
                    var c = Gangs.ColorOf(d.Gang);
                    Paint.Fill(new Rect(r.center.x - 6 * k, r.center.y - 6 * k, 12 * k, 12 * k), Color.black);
                    Paint.Fill(new Rect(r.center.x - 4.5f * k, r.center.y - 4.5f * k, 9 * k, 9 * k), c);
                }
                if (GUI.Button(r, "", GUIStyle.none)) Go(i);
            }
            // you
            var me = Player.Local;
            if (me != null && Expansion.Built)
            {
                var p = Expansion.ToPx(me.Position);
                bool onMap = p.x >= 0 && p.y >= 0 && p.x <= Expansion.PW && p.y <= Expansion.PH;
                var s = onMap ? at(p) : new Vector2(link.x - 70 * k, link.y);
                float pulse = 9f + Mathf.Sin(Time.unscaledTime * 5f) * 2f;
                Paint.Fill(new Rect(s.x - pulse * k, s.y - pulse * k, pulse * 2 * k, pulse * 2 * k), new Color(1f, 1f, 1f, 0.35f));
                Paint.Fill(new Rect(s.x - 5 * k, s.y - 5 * k, 10 * k, 10 * k), new Color(1f, 0.25f, 0.4f));
                GUI.Label(new Rect(s.x + 10 * k, s.y - 12 * k, 200 * k, 24 * k), onMap ? "<b>You</b>" : "<b>You (in town)</b>", _small);
            }
            if (!Expansion.Built) GUI.Label(new Rect(mx, my + mh + 4 * k, mw, 26 * k), "Being built a piece at a time, a few seconds after the town loads...", _small);
        }

        static void Outline(Rect r, Color c, float t)
        {
            Paint.Fill(new Rect(r.x, r.y, r.width, t), c);
            Paint.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Paint.Fill(new Rect(r.x, r.y, t, r.height), c);
            Paint.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }
    }
}
