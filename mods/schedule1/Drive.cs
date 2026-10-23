using System;
using System.Collections.Generic;
using System.Linq;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Driving, like levy's sheet: each of Frankenstein's cars pulls to 60 mph in its own time and tops
    /// out at its own speed (Garage's numbers: the P-One 2.4 s and 240 mph, the Centaur 2.3 s and 261 mph...).
    /// The game's own engine does the driving; when it's slower than the car should be, a push along the car's
    /// nose makes up the difference, and it never goes past the car's top speed. While you drive, any car: a
    /// speedometer (bottom right) and a round minimap that turns with the car (bottom left), drawn from a small
    /// camera above you a few times a second so it costs next to nothing. drive-debug.txt says what it measured.</summary>
    internal static class Drive
    {
        const float Sixty = 26.82f;  // 60 mph in m/s
        static Component _car;
        static object _rb;
        static Garage.Style _style;
        static float _findAt, _last, _mph, _nextMap, _logAt;
        static bool _was;
        static Type _rbType;
        static Camera _cam;
        static RenderTexture _rt;
        static Texture2D _map, _frame, _arrow;
        static readonly List<string> _log = new List<string>();

        public static bool Driving => _car != null;

        public static void Update()
        {
            var me = Player.Local;
            bool inCar = false;
            try { inCar = me != null && me.IsInVehicle; } catch (Exception) { }
            if (!inCar) { if (_car != null) { _car = null; _rb = null; if (_cam != null) _cam.enabled = false; } return; }
            if (_car == null || Time.unscaledTime >= _findAt)
            {
                _findAt = Time.unscaledTime + 1f;
                _car = Vehicles.Nearest(me.Position, 4f);
                if (_car == null) return;
                if (_rbType == null) _rbType = Rt.Find("UnityEngine.Rigidbody");
                _rb = Rt.Comp(_car.gameObject, _rbType) ?? Rt.Comps(_car.gameObject, _rbType, true).FirstOrDefault();
                var st = Garage.StyleOf(_car);
                if (st != _style && st != null) Note("driving the " + st.Name + ": " + st.Mph + " mph, 0-60 in " + st.ZeroSixty + " s");
                _style = st;
            }
            var v = Velocity();
            if (v == null) return;
            var fwd = _car.transform.forward;
            float along = Vector3.Dot(v.Value, fwd);
            _mph = v.Value.magnitude * 2.237f;
            if (_style != null) Push(v.Value, fwd, along);
            Map(me);
        }

        static Vector3? Velocity()
        {
            if (_rb == null) return null;
            var o = Rt.Get(_rb, "linearVelocity", "velocity");
            return o is Vector3 v ? v : (Vector3?)null;
        }

        /// <summary>Makes up the difference between how the game's engine pulls and how this car should.</summary>
        static void Push(Vector3 v, Vector3 fwd, float along)
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            float top = _style.Mph / 2.237f;
            bool gas = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Pad.RightTrigger > 0.3f;
            float got = (along - _last) / dt;
            _last = along;
            if (along > top) { Set(v - fwd * (along - top)); return; }
            if (!gas || along < -0.5f) { _was = false; return; }
            float a = Sixty / Mathf.Max(1.5f, _style.ZeroSixty);
            float want = along < Sixty ? a : a * Mathf.Max(0.12f, 1f - (along - Sixty) / Mathf.Max(1f, top - Sixty));
            if (got >= want) return;
            float dv = Mathf.Min((want - Mathf.Max(0, got)) * dt, want * dt);
            Set(v + fwd * dv);
            _last = along + dv;
            if (!_was && Time.unscaledTime >= _logAt) { _logAt = Time.unscaledTime + 10f; Note(_style.Name + ": the engine gave " + got.ToString("0.0") + " m/s², the car wants " + want.ToString("0.0") + ": making up the rest"); }
            _was = true;
        }

        static void Set(Vector3 v)
        {
            Rt.Set(_rb, v, "linearVelocity", "velocity");
        }

        // ---------- the minimap: a small camera straight above you ----------
        static void Map(Player me)
        {
            if (_cam != null && _cam.enabled)
            {
                // last frame it drew: copy it into a picture the menu code can show
                _cam.enabled = false;
                try
                {
                    var was = RenderTexture.active;
                    RenderTexture.active = _rt;
                    if (_map == null) _map = new Texture2D(_rt.width, _rt.height);
                    _map.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
                    _map.Apply();
                    RenderTexture.active = was;
                }
                catch (Exception e) { Note("minimap: " + e.Message); }
            }
            if (Time.unscaledTime < _nextMap) return;
            _nextMap = Time.unscaledTime + (Perf.Level == 2 ? 0.4f : 0.15f);
            try
            {
                if (_cam == null)
                {
                    var go = new GameObject("Frankenstein minimap");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _cam = go.AddComponent<Camera>();
                    _cam.orthographic = true;
                    _cam.orthographicSize = 70f;
                    _rt = new RenderTexture(160, 160, 16);
                    _cam.targetTexture = _rt;
                    _cam.depth = -50;
                    _cam.farClipPlane = 400f;
                    _cam.enabled = false;
                }
                float yaw = _car != null ? _car.transform.eulerAngles.y : 0;
                _cam.transform.position = me.Position + Vector3.up * 200f;
                _cam.transform.rotation = Quaternion.Euler(90f, yaw, 0);
                _cam.enabled = true;  // draws this frame, copied next frame
            }
            catch (Exception e) { Note("minimap camera: " + e.Message); _nextMap = Time.unscaledTime + 30f; }
        }

        static Texture2D Ring(int size, bool frame)
        {
            var t = new Texture2D(size, size);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    Color px;
                    if (frame) px = r > 1f ? new Color(0, 0, 0, 0) : r > 0.93f ? new Color(0.85f, 0.85f, 0.9f, 0.9f) : r > 0.9f ? new Color(0, 0, 0, 0.6f) : new Color(0, 0, 0, 0);
                    else
                    {
                        // an arrow pointing up
                        float ax = Mathf.Abs(x - c) / c, ay = y / (float)size;
                        px = ay > 0.15f && ay < 0.85f && ax < (ay - 0.15f) * 0.7f ? new Color(1f, 1f, 1f, 0.95f) : new Color(0, 0, 0, 0);
                    }
                    t.SetPixel(x, y, px);
                }
            t.Apply();
            return t;
        }

        public static void Draw()
        {
            if (_car == null || Plugin.MenuUp) return;
            float k = Screen.height / 1080f;
            // the minimap, bottom left
            float d = 200 * k, mx = 30 * k, my = Screen.height - d - 30 * k;
            if (_map != null)
            {
                if (_frame == null) { _frame = Ring(256, true); _arrow = Ring(64, false); }
                Paint.Fill(new Rect(mx + d * 0.15f, my + d * 0.15f, d * 0.7f, d * 0.7f), new Color(0, 0, 0, 0.3f));
                Paint.Picture(new Rect(mx + d * 0.08f, my + d * 0.08f, d * 0.84f, d * 0.84f), _map);
                Paint.Picture(new Rect(mx, my, d, d), _frame);
                Paint.Picture(new Rect(mx + d / 2 - 9 * k, my + d / 2 - 11 * k, 18 * k, 22 * k), _arrow);
            }
            // the speedometer, bottom right: an arc of marks lit up to your speed
            float top = _style != null ? Mathf.Ceil(_style.Mph / 20f) * 20f : 160f;
            float sx = Screen.width - 250 * k, sy = Screen.height - 250 * k, R = 100 * k, cx = sx + 110 * k, cy = sy + 130 * k;
            int marks = 36;
            for (int i = 0; i <= marks; i++)
            {
                float f = i / (float)marks, ang = Mathf.Lerp(220f, -40f, f) * Mathf.Deg2Rad;
                var p = new Vector2(cx + Mathf.Cos(ang) * R, cy - Mathf.Sin(ang) * R);
                bool lit = f <= _mph / top;
                var col = !lit ? new Color(1, 1, 1, 0.25f) : f > 0.85f ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.75f, 0.2f);
                float s = (i % 6 == 0 ? 7f : 4f) * k;
                Paint.Fill(new Rect(p.x - s / 2, p.y - s / 2, s, s), col);
            }
            GUI.Label(new Rect(cx - 80 * k, cy - 40 * k, 160 * k, 60 * k), Mathf.RoundToInt(_mph).ToString(), RefArt.Style(52 * k, Color.white, 4, true));
            GUI.Label(new Rect(cx - 80 * k, cy + 14 * k, 160 * k, 24 * k), "MPH", RefArt.Style(18 * k, new Color(0.85f, 0.85f, 0.9f), 4, false));
            if (_style != null) GUI.Label(new Rect(cx - 120 * k, cy + 40 * k, 240 * k, 22 * k), _style.Name, RefArt.Style(15 * k, new Color(0.75f, 0.75f, 0.8f), 4, false));
        }

        static void Note(string line)
        {
            _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
            if (_log.Count > 200) _log.RemoveAt(0);
            try
            {
                var dir = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Frankenstein");
                System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "drive-debug.txt"), new[] { "Frankenstein " + Plugin.Version }.Concat(_log).ToArray());
            }
            catch (Exception) { }
        }
    }
}
