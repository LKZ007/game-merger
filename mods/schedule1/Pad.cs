using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The controller for every Frankenstein menu (XInput, read directly so it works whatever the game's
    /// own input does). Pad.Button is GUI.Button that the d-pad can also reach: up and down move a highlight
    /// through the buttons on screen, A presses the highlighted one, B closes Frankenstein's panels.</summary>
    internal static class Pad
    {
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

        public const ushort Up = 0x0001, Down = 0x0002, Left = 0x0004, Right = 0x0008, Start = 0x0010, Back = 0x0020,
                            LB = 0x0100, RB = 0x0200, A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;

        public static ushort Now, Pressed, Released;
        public static Vector2 Stick;
        public static float RightTrigger, LeftTrigger;
        /// <summary>True once the controller is used, false again when the mouse moves: prompts show buttons then.</summary>
        public static bool InUse;
        static ushort _prev;
        static bool _ok = true;
        static Vector3 _mouse;

        // buttons on screen this frame, and the highlighted one
        static int _idx, _total, _focus;
        static bool _fire;

        public static void Update()
        {
            Now = 0;
            Stick = Vector2.zero;
            RightTrigger = 0;
            LeftTrigger = 0;
            if (_ok)
            {
                try
                {
                    if (XInputGetState(0, out var s) == 0)
                    {
                        Now = s.Buttons;
                        Stick = new Vector2(s.LX / 32768f, s.LY / 32768f);
                        RightTrigger = s.RT / 255f;
                        LeftTrigger = s.LT / 255f;
                    }
                }
                catch (Exception) { _ok = false; }  // no XInput on this PC
            }
            Pressed = (ushort)(Now & ~_prev);
            Released = (ushort)(_prev & ~Now);
            _prev = Now;
            if (Pressed != 0 || Stick.magnitude > 0.5f) InUse = true;
            if ((Input.mousePosition - _mouse).sqrMagnitude > 4f) { if (_mouse != Vector3.zero) InUse = false; _mouse = Input.mousePosition; }

            if (_total > 0 && !ItemMenu.IsOpen && !Wheel.Open)
            {
                if ((Pressed & Down) != 0) { _focus = (_focus + 1) % _total; InUse = true; }
                if ((Pressed & Up) != 0) { _focus = (_focus - 1 + _total) % _total; InUse = true; }
                if ((Pressed & A) != 0) _fire = true;
                if ((Pressed & B) != 0) { World.Close(); Gangs.Close(); Crew.Close(); }
            }
            if (_focus >= Math.Max(_total, 1)) _focus = 0;
        }

        /// <summary>Called at the start of every OnGUI pass.</summary>
        public static void GuiBegin() => _idx = 0;

        /// <summary>Called at the end of every OnGUI pass.</summary>
        public static void GuiEnd()
        {
            if (Event.current.type != EventType.Repaint) return;
            _total = _idx;
            _fire = false;  // A with nothing on screen does nothing
        }

        /// <summary>A Frankenstein panel with buttons is on screen (the d-pad is moving through it).</summary>
        public static bool PanelsOpen => _total > 0;

        public static bool Button(Rect r, string label)
        {
            int me = _idx++;
            bool focused = InUse && me == _focus;
            if (focused)
            {
                var old = GUI.color;
                GUI.color = new Color(0.62f, 0.35f, 1f, 1f);
                GUI.Box(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), "");
                GUI.color = old;
            }
            if (GUI.Button(r, focused ? "(A) " + label : label)) return true;
            if (focused && _fire && Event.current.type == EventType.Repaint) { _fire = false; return true; }
            return false;
        }
    }
}
