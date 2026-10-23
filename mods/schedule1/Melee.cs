using System;
using System.Linq;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Swings with weight behind them, for the bat, knife, machete and the rest. The camera pulls back on
    /// the wind-up and whips through on the swing; a hit freezes the moment for a few hundredths of a second, shakes
    /// the screen and knocks the person back, with extra damage on top of the game's own. Hold attack (left mouse,
    /// or RT) for half a second before letting go for a heavy swing: twice the damage and a bigger knock.</summary>
    internal static class Melee
    {
        static readonly string[] Words = { "bat", "knife", "machete", "fryingpan", "crowbar", "axe", "hammer", "pipe", "golfclub", "shovel", "katana", "sword" };
        static float _down = -1, _swingAt = -1, _swingPower, _pauseUntil = -1, _shake;
        static bool _hitThisSwing, _holding, _wasTrigger;
        static float _nextLook;
        static Quaternion _lastSet;
        static Quaternion _lastOffset = Quaternion.identity;

        static bool HoldingMelee()
        {
            if (Time.unscaledTime < _nextLook) return _holding;
            _nextLook = Time.unscaledTime + 0.3f;
            _holding = false;
            foreach (var s in Boom.Slots())
                if (Rt.Get(s, "IsEquipped", "isEquipped") is bool on && on)
                {
                    var id = (Boom.Id(s) ?? "").ToLowerInvariant().Replace(" ", "").Replace("_", "");
                    _holding = Words.Any(id.Contains);
                }
            return _holding;
        }

        public static void Update()
        {
            if (_pauseUntil > 0 && Time.unscaledTime >= _pauseUntil) { Time.timeScale = 1f; _pauseUntil = -1; }
            if (ItemMenu.IsOpen || Wheel.Open || Time.timeScale == 0f || !HoldingMelee()) { _down = -1; return; }
            bool trigger = Pad.RightTrigger > 0.6f;
            bool pressed = Input.GetMouseButtonDown(0) || (trigger && !_wasTrigger);
            bool released = Input.GetMouseButtonUp(0) || (!trigger && _wasTrigger);
            _wasTrigger = trigger;
            if (pressed) _down = Time.unscaledTime;
            if (released && _down > 0)
            {
                bool heavy = Time.unscaledTime - _down > 0.5f;
                _down = -1;
                _swingAt = Time.unscaledTime;
                _swingPower = heavy ? 2f : 1f;
                _hitThisSwing = false;
            }
            // the moment the swing lands: who's in front of you
            if (_swingAt > 0 && !_hitThisSwing && Time.unscaledTime - _swingAt > 0.12f)
            {
                _hitThisSwing = true;
                Hit();
            }
        }

        static void Hit()
        {
            var me = Player.Local;
            var cam = Camera.main;
            if (me == null || cam == null) return;
            var fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
            NPC best = null;
            float bestD = 2.8f;
            foreach (var n in NPC.All)
            {
                if (n == null) continue;
                var to = n.Position - me.Position; to.y = 0;
                float d = to.magnitude;
                if (d < bestD && d > 0.1f && Vector3.Angle(fwd, to) < 55f) { bestD = d; best = n; }
            }
            if (best == null) return;
            // the impact: a freeze, a shake, a knock back, and the extra damage
            Time.timeScale = 0.05f;
            _pauseUntil = Time.unscaledTime + (_swingPower > 1 ? 0.11f : 0.06f);
            _shake = _swingPower > 1 ? 0.35f : 0.2f;
            try { best.Movement.Warp(best.Position + fwd * (_swingPower > 1 ? 2.2f : 1.1f)); } catch (Exception) { }
            try
            {
                int dmg = (int)(18f * _swingPower);
                if (!Boom.Call(best, "Damage", dmg))
                    Boom.Call(Rt.Get(Rt.Get(best, "S1NPC"), "Health", "health"), "TakeDamage", (float)dmg, true);
            }
            catch (Exception) { }
            if (_swingPower > 1) ItemMenu.Notify("Heavy hit!");
        }

        /// <summary>The camera: pulled back while you hold the swing, whipped through as it goes, shaken on a hit.</summary>
        public static void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var t = cam.transform;
            // take off last frame's offset if the game didn't set the camera again itself
            if (t.localRotation == _lastSet) t.localRotation = t.localRotation * Quaternion.Inverse(_lastOffset);
            float pitch = 0, roll = 0, yaw = 0;
            if (_down > 0 && _holding) { float k = Mathf.Clamp01((Time.unscaledTime - _down) / 0.5f); pitch = -5f * k; roll = 4f * k; }  // wind-up
            if (_swingAt > 0)
            {
                float s = (Time.unscaledTime - _swingAt) / 0.28f;
                if (s >= 1) _swingAt = -1;
                else { float k = Mathf.Sin(s * Mathf.PI); pitch = 7f * k * _swingPower; yaw = -6f * k * _swingPower; roll = -5f * k; }  // follow-through
            }
            if (_shake > 0)
            {
                _shake = Mathf.Max(0, _shake - Time.unscaledDeltaTime);
                float a = _shake * 10f;
                pitch += UnityEngine.Random.Range(-a, a); yaw += UnityEngine.Random.Range(-a, a);
            }
            _lastOffset = Quaternion.Euler(pitch, yaw, roll);
            t.localRotation = t.localRotation * _lastOffset;
            _lastSet = t.localRotation;
        }
    }
}
