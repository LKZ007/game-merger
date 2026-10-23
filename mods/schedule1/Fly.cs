using System;
using System.Linq;
using MelonLoader;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Creative's flying and speed, like Minecraft's. Double-tap jump (Space, or A on the controller) to fly
    /// or land; while flying, Space / A goes up and Ctrl / B goes down, and you move where you look. Speed (F7, or
    /// the controller wheel) steps through 1x, 2x, 3x and 5x for walking and flying. Only in Creative saves.
    /// Flying moves you after the game's own movement each frame and turns the game's gravity off while you're up.</summary>
    internal static class Fly
    {
        public static bool Flying;
        public static float Speed = 1f;
        static readonly float[] Speeds = { 1f, 2f, 3f, 5f };
        static float _lastJump = -1;
        static object _move;
        static string _gravityName;
        static float _gravityWas = float.NaN;
        static bool _told;

        public static void Scene(bool inTown) { Flying = false; _move = null; _gravityName = null; _gravityWas = float.NaN; }

        public static void NextSpeed()
        {
            if (!MainMenu.Creative) { ItemMenu.Notify("Speed is a Creative thing: switch this save to Creative first."); return; }
            int i = Array.IndexOf(Speeds, Speed);
            Speed = Speeds[(i + 1) % Speeds.Length];
            ItemMenu.Notify("Speed: " + Speed + "x");
        }

        public static void Toggle()
        {
            if (!MainMenu.Creative) { ItemMenu.Notify("Flying is a Creative thing: switch this save to Creative first."); return; }
            Flying = !Flying;
            Gravity(!Flying);
            ItemMenu.Notify(Flying ? "Flying. Jump goes up, Ctrl (or B) goes down. Double-tap jump to land." : "Landed.");
        }

        static object Move()
        {
            if (_move is UnityEngine.Object o && o != null) return _move;
            _move = Rt.All(Rt.FindByName("PlayerMovement")).FirstOrDefault();
            return _move;
        }

        /// <summary>The game's gravity off while flying, back on when you land.</summary>
        static void Gravity(bool on)
        {
            var m = Move();
            if (m == null) return;
            if (_gravityName == null)
                foreach (var n in new[] { "GravityMultiplier", "gravityMultiplier", "Gravity", "gravity" })
                {
                    var v = Rt.Get(m, n);
                    if (v is float f) { _gravityName = n; _gravityWas = f; break; }
                }
            if (_gravityName == null)
            {
                if (!_told) { _told = true; Melon<Plugin>.Logger.Msg("Fly: no gravity setting found; flying holds you up by itself."); }
                return;
            }
            Rt.Set(m, on ? _gravityWas : 0f, _gravityName);
        }

        public static void Update()
        {
            if (!MainMenu.Creative)
            {
                if (Flying) { Flying = false; Gravity(true); }
                Speed = 1f;
                return;
            }
            if (ItemMenu.IsOpen || Wheel.Open) return;
            bool jump = Input.GetKeyDown(KeyCode.Space) || (Pad.Pressed & Pad.A) != 0;
            if (jump)
            {
                if (_lastJump > 0 && Time.unscaledTime - _lastJump < 0.3f) { Toggle(); _lastJump = -1; }
                else _lastJump = Time.unscaledTime;
            }
            if (Input.GetKeyDown(KeyCode.F7)) NextSpeed();
        }

        /// <summary>After the game has moved you this frame: flying, and the extra speed.</summary>
        public static void LateUpdate()
        {
            if (!MainMenu.Creative || (!Flying && Speed <= 1f)) return;
            var me = Player.Local;
            var cam = Camera.main;
            if (me == null || cam == null || me.IsInVehicle) return;
            // where you want to go: keys or the left stick, along where you look
            float x = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0) + Pad.Stick.x;
            float z = (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0) + Pad.Stick.y;
            var fwd = cam.transform.forward; var right = cam.transform.right;
            if (!Flying) { fwd.y = 0; right.y = 0; }
            var dir = Vector3.ClampMagnitude(fwd.normalized * z + right.normalized * x, 1f);
            float dt = Time.deltaTime;
            var step = Vector3.zero;
            if (Flying)
            {
                float up = (Input.GetKey(KeyCode.Space) || (Pad.Now & Pad.A) != 0 ? 1 : 0) -
                           (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || (Pad.Now & Pad.B) != 0 ? 1 : 0);
                step = (dir * 10f + Vector3.up * up * 7f) * Speed * dt;
                // hold your height: undo whatever the game's gravity did this frame
                var p = me.Position;
                if (float.IsNaN(_hold.y) || up != 0) _hold = p;
                me.Position = new Vector3(p.x, _hold.y, p.z) + step;
                _hold = me.Position;
                return;
            }
            _hold = new Vector3(float.NaN, float.NaN, float.NaN);
            // walking faster: the game's own step, plus the extra
            if (dir.sqrMagnitude > 0.01f) me.Position += dir * 4.5f * (Speed - 1f) * dt;
        }

        static Vector3 _hold = new Vector3(float.NaN, float.NaN, float.NaN);

        public static void Draw()
        {
            if (!MainMenu.Creative || (!Flying && Speed <= 1f)) return;
            GUI.Box(new Rect(16, Screen.height - 104, 300, 26), (Flying ? "Flying" : "Walking") + "  ·  Speed " + Speed + "x  ·  " +
                    (Pad.InUse ? "A A: fly/land" : "Space Space: fly/land, F7: speed"));
        }
    }
}
