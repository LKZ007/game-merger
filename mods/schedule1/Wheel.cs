using System;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Everything Frankenstein adds, on one controller button: hold View (Back) and a wheel opens; point
    /// the left stick at what you want and let go. The same things are on the keyboard keys (F4, F6, F8, F9, F10, T, G).</summary>
    internal static class Wheel
    {
        public static bool Open;
        static float _heldSince = -1;
        static int _sel = -1;
        static bool _inTown;

        static readonly string[] Names = { "Items", "Frankenstein menu", "Gangs", "Who's who", "Throw grenade", "Rob", "Game Mode", "Fly", "Speed", "Car: in or out", "Drop item", "City map" };
        static readonly string[] Keys = { "F6", "F8", "F9", "F10", "T", "G", "", "A A", "F7", "E / X", "D-pad down", "F4" };

        public static void Scene(bool inTown) { _inTown = inTown; Open = false; _heldSince = -1; }

        static void Run(int i)
        {
            switch (i)
            {
                case 0: ItemMenu.Toggle(); break;
                case 1: World.Toggle(); break;
                case 2: if (Gangs.On) Gangs.Toggle(); else ItemMenu.Notify("This merge has no gangs."); break;
                case 3: if (Gangs.On) Crew.Toggle(); else ItemMenu.Notify("This merge has no gangs."); break;
                case 4: Boom.ThrowNow(); break;
                case 5: if (Gangs.On) Gangs.TryRob(); break;
                case 6: Mode.Set(MainMenu.Creative ? "survival" : "creative"); break;
                case 7: Fly.Toggle(); break;
                case 8: Fly.NextSpeed(); break;
                case 9: Vehicles.GetInOrOut(); break;
                case 10: Drop.Now(); break;
                case 11: CityMap.Toggle(); break;
            }
        }

        public static void Update()
        {
            if (!_inTown || ItemMenu.IsOpen) { Open = false; _heldSince = -1; return; }
            bool back = (Pad.Now & Pad.Back) != 0;
            if (back && (Pad.Now & Pad.Y) != 0) { _heldSince = -1; Open = false; return; }  // View + Y is the item menu's own shortcut
            if (back)
            {
                if (_heldSince < 0) _heldSince = Time.unscaledTime;
                if (!Open && Time.unscaledTime - _heldSince > 0.3f) { Open = true; _sel = -1; }
                if (Open && Pad.Stick.magnitude > 0.5f)
                {
                    // 0 at the top, going round clockwise
                    float a = Mathf.Atan2(Pad.Stick.x, Pad.Stick.y) * Mathf.Rad2Deg;
                    if (a < 0) a += 360f;
                    _sel = Mathf.RoundToInt(a / (360f / Names.Length)) % Names.Length;
                }
                if (Open && (Pad.Pressed & Pad.B) != 0) { Open = false; _sel = -1; }
                return;
            }
            if (Open && _sel >= 0) { try { Run(_sel); } catch (Exception e) { ItemMenu.Notify(Names[_sel] + ": " + e.Message); } }
            Open = false;
            _heldSince = -1;
        }

        public static void Draw()
        {
            if (!Open) return;
            float cx = Screen.width / 2f, cy = Screen.height / 2f, rad = 190f;
            GUI.Box(new Rect(cx - 260, cy - 250, 520, 500), "");
            GUI.Label(new Rect(cx - 120, cy - 12, 240, 24), "Point the stick, let go of View");
            for (int i = 0; i < Names.Length; i++)
            {
                float a = i * (360f / Names.Length) * Mathf.Deg2Rad;
                float x = cx + Mathf.Sin(a) * rad, y = cy - Mathf.Cos(a) * rad;
                var r = new Rect(x - 80, y - 22, 160, 44);
                var old = GUI.color;
                if (i == _sel) GUI.color = new Color(0.62f, 0.35f, 1f, 1f);
                string label = Names[i] == "Game Mode" ? "Game Mode: " + (MainMenu.Creative ? "go Survival" : "go Creative")
                             : Names[i] == "Fly" ? (Fly.Flying ? "Land" : "Fly") : Names[i] == "Speed" ? "Speed " + Fly.Speed + "x" : Names[i];
                GUI.Box(r, label + (Keys[i].Length > 0 ? "\n(" + Keys[i] + ")" : ""));
                GUI.color = old;
            }
            GUI.Label(new Rect(cx - 120, cy + 14, 240, 24), "B: close");
        }
    }
}
