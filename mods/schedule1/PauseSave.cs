using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>A Save Game button in Schedule I's pause menu, right under the game's own list (Resume, Settings,
    /// I'm Stuck, Bug Report, Quit). Click it, press Y on a controller or Ctrl+S: the game saves the way it does
    /// when you sleep, and the button says when it's done.</summary>
    internal static class PauseSave
    {
        static GameObject _stuck, _quit;
        static float _scanAt, _doneUntil;
        static string _said = "";
        static GUIStyle _big, _small;

        public static bool Showing => _stuck != null && _stuck.activeInHierarchy && _quit != null && _quit.activeInHierarchy && !MenuSkin.Active;

        public static void Update()
        {
            if (S1API.Entities.Player.Local == null) { _stuck = _quit = null; return; }
            if (!Showing && Time.unscaledTime >= _scanAt && Cursor.visible)
            {
                _scanAt = Time.unscaledTime + 0.5f;
                _stuck = _quit = null;
                foreach (var t in Rt.Texts())
                {
                    var s = (Rt.Text(t) ?? "").Trim().ToLowerInvariant();
                    if (s == "i'm stuck" || s == "i’m stuck") _stuck = Rt.GameObjectOf(t);
                    else if (s == "quit") _quit = Rt.GameObjectOf(t);
                }
            }
            if (!Showing) return;
            if ((Pad.Pressed & Pad.Y) != 0 || ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.S))) Save();
        }

        public static void Save()
        {
            bool ok = false;
            try { ok = S1API.Internal.Abstraction.Saveable.RequestGameSave(true); } catch (Exception) { }
            if (!ok)
            {
                try
                {
                    var t = Rt.FindByName("SaveManager");
                    var sm = t?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null, null);
                    var m = t?.GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(x => x.Name == "Save" && x.GetParameters().Length == 0);
                    if (sm != null && m != null) { m.Invoke(sm, null); ok = true; }
                }
                catch (Exception e) { Melon<Plugin>.Logger.Warning("Save Game: " + (e.InnerException ?? e).Message); }
            }
            _said = ok ? "Game saved" : "The game can't save right now";
            _doneUntil = Time.unscaledTime + 3f;
            Melon<Plugin>.Logger.Msg("Save Game: " + _said + ".");
        }

        public static void Draw()
        {
            if (!Showing) return;
            float k = Screen.height / 1080f;
            if (_big == null || _big.fontSize != Mathf.RoundToInt(40 * k))
            {
                _big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(40 * k), wordWrap = false, clipping = TextClipping.Overflow };
                _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * k), wordWrap = false, clipping = TextClipping.Overflow };
                _big.normal.textColor = Color.white;
                _small.normal.textColor = new Color(0.7f, 0.95f, 0.72f);
                var fs = _big.GetType().GetProperty("fontStyle");
                try { fs?.SetValue(_big, System.Enum.ToObject(fs.PropertyType, 1), null); } catch (Exception) { }
            }
            // just under the game's Quit (the pause menu is a screen-space canvas: positions are in pixels from the bottom)
            var q = Rt.Comp(_quit, typeof(RectTransform)) as RectTransform;
            float x = q != null ? q.position.x - q.rect.width * q.lossyScale.x * q.pivot.x : 120 * k;
            float y = q != null ? Screen.height - (q.position.y - q.rect.height * q.lossyScale.y * q.pivot.y) + 14 * k : Screen.height * 0.62f;
            string label = (Pad.InUse ? "(Y)  " : "") + "Save Game";
            var size = _big.CalcSize(new GUIContent(label));
            var r = new Rect(x, y, size.x + 24 * k, size.y + 6 * k);
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) Paint.Fill(new Rect(r.x - 10 * k, r.y, r.width + 160 * k, r.height), new Color(1, 1, 1, 0.22f));
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), "<color=#00000088>" + label + "</color>", _big);
            GUI.Label(r, label, _big);
            if (GUI.Button(r, "", GUIStyle.none)) Save();
            if (Time.unscaledTime < _doneUntil) GUI.Label(new Rect(r.xMax + 16 * k, r.y + 12 * k, 400 * k, 30 * k), "✓ " + _said, _small);
            else if (!Pad.InUse) GUI.Label(new Rect(r.xMax + 16 * k, r.y + 12 * k, 400 * k, 30 * k), "Ctrl+S", _small);
        }
    }
}
