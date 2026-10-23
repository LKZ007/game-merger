using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The Vehicle Spawner, laid out like levy's sheet (F3, the wheel, or F6's Vehicles tab): categories
    /// down the left (Supercars, Sports Cars, SUVs, Trucks, Motorcycles, Custom), a grid of colour thumbnails, and
    /// the picked car's name, top speed and 0-60 with a Spawn button. "Your garage" lists the Frankenstein cars in
    /// this save and brings one to you. Schedule I has no motorcycles to build on, and that category says so.</summary>
    internal static class Showroom
    {
        public static bool Open;
        static readonly string[] Cats = { "Supercars", "Sports Cars", "SUVs", "Trucks", "Motorcycles", "Custom" };
        static int _cat, _sel;
        static List<Vehicles.Car> _all;

        public static void Toggle()
        {
            Open = !Open;
            if (Open) { _all = null; _sel = 0; }
        }

        static List<Vehicles.Car> All()
        {
            if (_all == null) { try { _all = Vehicles.All(); } catch (Exception) { _all = new List<Vehicles.Car>(); } }
            return _all;
        }

        static string CatOf(Vehicles.Car c)
        {
            if (c.Style != null) return c.Style.Category;
            return c.Kind == "van" || c.Kind == "pickup" ? "Trucks" : "Custom";
        }

        static List<Vehicles.Car> Shown() => All().Where(c => CatOf(c) == Cats[_cat]).ToList();

        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) Toggle();
            if (!Open) return;
            if (Input.GetKeyDown(KeyCode.Escape) || (Pad.Pressed & Pad.B) != 0) { Open = false; return; }
            int n = Math.Max(1, Shown().Count);
            if ((Pad.Pressed & Pad.LB) != 0) { _cat = (_cat + Cats.Length - 1) % Cats.Length; _sel = 0; }
            if ((Pad.Pressed & Pad.RB) != 0) { _cat = (_cat + 1) % Cats.Length; _sel = 0; }
            if ((Pad.Pressed & Pad.Right) != 0) _sel = (_sel + 1) % n;
            if ((Pad.Pressed & Pad.Left) != 0) _sel = (_sel + n - 1) % n;
            if ((Pad.Pressed & Pad.Down) != 0) _sel = Math.Min(n - 1, _sel + 4);
            if ((Pad.Pressed & Pad.Up) != 0) _sel = Math.Max(0, _sel - 4);
            if ((Pad.Pressed & Pad.A) != 0) SpawnSel();
        }

        static void SpawnSel()
        {
            var list = Shown();
            if (_sel < 0 || _sel >= list.Count) return;
            Open = false;
            Vehicles.Spawn(list[_sel]);
        }

        public static void Draw()
        {
            if (!Open) return;
            float W = Screen.width, H = Screen.height, k = H / 1080f;
            Paint.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.05f, 0.88f));
            float pw = Mathf.Min(1180 * k, W - 40), ph = 700 * k, x = (W - pw) / 2, y = (H - ph) / 2;
            Paint.Fill(new Rect(x, y, pw, ph), new Color(0.07f, 0.09f, 0.12f, 0.97f));
            GUI.Label(new Rect(x + 24 * k, y + 16 * k, 400 * k, 40 * k), "Vehicle Spawner", RefArt.Style(30 * k, Color.white, 3, true));
            GUI.Label(new Rect(x + pw - 420 * k, y + 22 * k, 400 * k, 30 * k), Pad.InUse ? "LB / RB category · d-pad pick · A spawn · B back" : "F3 or Esc to close", RefArt.Style(15 * k, new Color(0.7f, 0.72f, 0.78f), 5, false));
            // categories down the left
            float lx = x + 20 * k, ly = y + 74 * k, lw = 230 * k;
            Paint.Fill(new Rect(lx, ly, lw, Cats.Length * 46 * k + 12 * k), new Color(0.1f, 0.13f, 0.17f));
            for (int i = 0; i < Cats.Length; i++)
            {
                var r = new Rect(lx + 6 * k, ly + 6 * k + i * 46 * k, lw - 12 * k, 40 * k);
                if (i == _cat) Paint.Fill(r, new Color(0.18f, 0.42f, 0.6f));
                GUI.Label(new Rect(r.x + 14 * k, r.y, r.width, r.height), Cats[i], RefArt.Style(18 * k, Color.white, 3, i == _cat));
                if (GUI.Button(r, "", GUIStyle.none)) { _cat = i; _sel = 0; }
            }
            var back = new Rect(lx, y + ph - 64 * k, lw, 44 * k);
            Paint.Fill(back, new Color(0.1f, 0.13f, 0.17f));
            GUI.Label(new Rect(back.x + 18 * k, back.y, back.width, back.height), "←  Back", RefArt.Style(18 * k, Color.white, 3, false));
            if (GUI.Button(back, "", GUIStyle.none)) Open = false;
            // the thumbnails
            var list = Shown();
            float gx = lx + lw + 24 * k, gy = ly, tw = 150 * k, th = 92 * k, gap = 12 * k;
            if (Cats[_cat] == "Motorcycles")
                GUI.Label(new Rect(gx, gy, 600 * k, 60 * k), "Schedule I has no motorbikes for Frankenstein to build on, so there are none yet.", RefArt.Style(18 * k, new Color(0.8f, 0.8f, 0.85f), 3, false));
            for (int i = 0; i < list.Count; i++)
            {
                var r = new Rect(gx + (i % 4) * (tw + gap), gy + (i / 4) * (th + gap), tw, th);
                if (i == _sel) Paint.Fill(new Rect(r.x - 3 * k, r.y - 3 * k, r.width + 6 * k, r.height + 6 * k), new Color(0.35f, 0.65f, 0.95f));
                Vehicles.Picture(list[i], r);
                if (GUI.Button(r, "", GUIStyle.none)) { if (_sel == i) SpawnSel(); else _sel = i; }
            }
            // the picked car
            if (_sel < list.Count)
            {
                var c = list[_sel];
                float dx = gx, dy = y + ph - 190 * k;
                Paint.Fill(new Rect(dx, dy, pw - (dx - x) - 24 * k, 150 * k), new Color(0.1f, 0.13f, 0.17f));
                GUI.Label(new Rect(dx + 20 * k, dy + 14 * k, 600 * k, 36 * k), c.Name, RefArt.Style(26 * k, Color.white, 3, true));
                string stats = c.Style != null ? "Top Speed: " + c.Style.Mph + " MPH   |   0-60: " + c.Style.ZeroSixty.ToString("0.0") + "s" : "The game's own " + c.Kind + ", as it drives in Schedule I";
                GUI.Label(new Rect(dx + 20 * k, dy + 56 * k, 600 * k, 28 * k), stats, RefArt.Style(18 * k, new Color(0.85f, 0.87f, 0.92f), 3, false));
                var spawn = new Rect(dx + pw - (dx - x) - 24 * k - 230 * k, dy + 90 * k, 210 * k, 44 * k);
                Paint.Fill(spawn, new Color(0.18f, 0.6f, 0.3f));
                GUI.Label(spawn, (Pad.InUse ? "(A)  " : "") + "Spawn", RefArt.Style(20 * k, Color.white, 4, true));
                if (GUI.Button(spawn, "", GUIStyle.none)) SpawnSel();
            }
        }
    }
}
