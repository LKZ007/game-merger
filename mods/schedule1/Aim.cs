using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>A crosshair for each gun, centred where shots go, sized to how much that gun strays (the gun's own
    /// spread is set to match in Guns): a tight dot for the sniper, short lines for pistols, wider for the AK and
    /// wider still for the Uzi, a pellet circle for the shotgun, and for the RPG and grenade launcher the arc the
    /// rocket or grenade will fly, worked out from how Frankenstein throws it. The sniper zooms in through a scope
    /// while you aim (right mouse or the left trigger).</summary>
    internal static class Aim
    {
        /// <summary>The gun in your hand: sniper, ak47, uzi, rpg, launcher, shotgun, revolver, pistol, or null.</summary>
        public static string Kind;
        static float _next, _fov, _zoom;
        static bool _zoomed;
        static Texture2D _ring, _scope;

        public static void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.15f;
            Kind = Find();
        }

        static string Find()
        {
            if (OwnGuns.HeldKey != null) return OwnGuns.HeldKey;
            if (Boom.LauncherHeld) return "launcher";
            var s = Boom.EquippedSlot();
            if (s == null) return null;
            var id = (Boom.Id(s) ?? "").ToLowerInvariant();
            if (id.Contains("shotgun")) return "shotgun";
            if (id.Contains("revolver")) return "revolver";
            if (id.Contains("m1911") || id.Contains("pistol") || id.Contains("glock")) return "pistol";
            return null;
        }

        static bool Blocked => ItemMenu.IsOpen || Wheel.Open || MenuSkin.Active || Loading.Showing || Pad.PanelsOpen ||
                               Cursor.lockState != CursorLockMode.Locked || S1API.Entities.Player.Local == null;

        /// <summary>The sniper's scope: the camera zooms while you aim, and goes back after.</summary>
        public static void LateUpdate()
        {
            var cam = Camera.main;
            bool want = Kind == "sniper" && !Blocked && (Input.GetMouseButton(1) || Pad.LeftTrigger > 0.4f);
            if (cam == null) { _zoomed = false; return; }
            if (want)
            {
                if (!_zoomed) { _fov = cam.fieldOfView; _zoom = 0; _zoomed = true; }
                _zoom = Mathf.MoveTowards(_zoom, 1f, Time.unscaledDeltaTime * 6f);
                cam.fieldOfView = Mathf.Lerp(_fov, 14f, _zoom);
            }
            else if (_zoomed)
            {
                cam.fieldOfView = _fov;
                _zoomed = false;
                _zoom = 0;
            }
        }

        static readonly Color Ink = new Color(1, 1, 1, 0.9f), Edge = new Color(0, 0, 0, 0.55f);

        static void Bar(float x, float y, float w, float h)
        {
            Paint.Fill(new Rect(x - 1, y - 1, w + 2, h + 2), Edge);
            Paint.Fill(new Rect(x, y, w, h), Ink);
        }

        /// <summary>Four lines around the centre, the gap the gun's spread.</summary>
        static void Cross(float cx, float cy, float gap, float len, float th)
        {
            Bar(cx - th / 2, cy - gap - len, th, len);
            Bar(cx - th / 2, cy + gap, th, len);
            Bar(cx - gap - len, cy - th / 2, len, th);
            Bar(cx + gap, cy - th / 2, len, th);
        }

        static Texture2D Ring(int size, float inner, float outer, bool scope)
        {
            var t = new Texture2D(size, size);
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                    Color px;
                    if (scope) px = r > inner ? new Color(0, 0, 0, Mathf.Clamp01((r - inner) * 30f)) : new Color(0, 0, 0, 0);
                    else px = r > inner && r < outer ? Ink : new Color(0, 0, 0, 0);
                    t.SetPixel(x, y, px);
                }
            t.Apply();
            return t;
        }

        public static void Draw()
        {
            if (Kind == null || Blocked) return;
            float W = Screen.width, H = Screen.height, k = H / 1080f, cx = W / 2f, cy = H / 2f, th = Mathf.Max(2f, 2f * k);
            switch (Kind)
            {
                case "sniper":
                    if (_zoomed && _zoom > 0.5f)
                    {
                        // through the scope: black around a round view, fine lines to the centre
                        if (_scope == null) _scope = Ring(512, 0.96f, 1f, true);
                        float d = H * 0.92f;
                        Paint.Picture(new Rect(cx - d / 2, cy - d / 2, d, d), _scope);
                        Paint.Fill(new Rect(0, 0, cx - d / 2 + 1, H), Color.black);
                        Paint.Fill(new Rect(cx + d / 2 - 1, 0, W, H), Color.black);
                        Paint.Fill(new Rect(cx - d / 2, 0, d, cy - d / 2 + 1), Color.black);
                        Paint.Fill(new Rect(cx - d / 2, cy + d / 2 - 1, d, H), Color.black);
                        Paint.Fill(new Rect(cx - d / 2, cy - 0.5f, d, 1.5f * k), Color.black);
                        Paint.Fill(new Rect(cx - 0.5f, cy - d / 2, 1.5f * k, d), Color.black);
                        Paint.Fill(new Rect(cx - d / 2, cy - 2 * k, d * 0.35f, 4 * k), Color.black);
                        Paint.Fill(new Rect(cx + d * 0.15f, cy - 2 * k, d * 0.35f, 4 * k), Color.black);
                        Paint.Fill(new Rect(cx - 2 * k, cy + d * 0.15f, 4 * k, d * 0.35f), Color.black);
                        Paint.Fill(new Rect(cx - 1.5f * k, cy - 1.5f * k, 3 * k, 3 * k), new Color(0.9f, 0.1f, 0.1f));
                    }
                    else
                    {
                        Bar(cx - th, cy - th, th * 2, th * 2);
                        Cross(cx, cy, 5 * k, 7 * k, th);
                    }
                    break;
                case "ak47": Cross(cx, cy, 13 * k, 10 * k, th); Bar(cx - th / 2, cy - th / 2, th, th); break;
                case "uzi": Cross(cx, cy, 22 * k, 9 * k, th); Bar(cx - th / 2, cy - th / 2, th, th); break;
                case "pistol": Cross(cx, cy, 9 * k, 8 * k, th); break;
                case "revolver": Cross(cx, cy, 7 * k, 9 * k, th); break;
                case "shotgun":
                {
                    // where the pellets go
                    if (_ring == null) _ring = Ring(256, 0.9f, 1f, false);
                    float d = 90 * k;
                    Paint.Picture(new Rect(cx - d / 2, cy - d / 2, d, d), _ring);
                    Bar(cx - th, cy - th, th * 2, th * 2);
                    break;
                }
                case "rpg":
                case "launcher":
                    Cross(cx, cy, 10 * k, 8 * k, th);
                    Arc(Kind == "rpg" ? 1.4f : 1f);
                    break;
            }
        }

        static readonly List<Vector3> _pts = new List<Vector3>();

        /// <summary>Dots along the path the rocket or grenade flies (the same start, speed and drop Boom uses),
        /// with a bigger dot where it meets the ground level you're standing on.</summary>
        static void Arc(float size)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var tr = cam.transform;
            var p = tr.position + tr.forward * 0.9f - tr.up * 0.15f + tr.right * 0.12f;
            var v = tr.forward * 34f + Vector3.up * 2.5f;
            float floor = tr.position.y - 1.7f, k = Screen.height / 1080f;
            _pts.Clear();
            for (float t = 0.08f; t < 3f; t += 0.08f)
            {
                var at = p + v * t + 0.5f * new Vector3(0, -9.81f, 0) * t * t;
                if (at.y < floor) break;
                _pts.Add(at);
            }
            for (int i = 0; i < _pts.Count; i++)
            {
                var s = cam.WorldToScreenPoint(_pts[i]);
                if (s.z <= 0) continue;
                float y = Screen.height - s.y, r = (i == _pts.Count - 1 ? 6f : 2.5f) * size * k;
                Paint.Fill(new Rect(s.x - r - 1, y - r - 1, r * 2 + 2, r * 2 + 2), Edge);
                Paint.Fill(new Rect(s.x - r, y - r, r * 2, r * 2), i == _pts.Count - 1 ? new Color(1f, 0.55f, 0.15f, 0.95f) : new Color(1, 1, 1, 0.75f));
            }
        }
    }
}
