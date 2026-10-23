using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Keeps the merge from lagging. Frankenstein's own mod has a budget: everything it does in a frame
    /// (its updates and its menus and HUD) should take under 2 ms. It times itself and watches the game's FPS, and
    /// when the game drops under 40 FPS, or the mod goes over its budget, for a few seconds, it turns its own extras
    /// down: the crews, gangs, cars and gun checks run less often, far-off crew members think less, and the launcher
    /// arc uses fewer dots. When the game runs smoothly again for a while it turns them back up. The app's
    /// Performance setting can fix the level instead (quality, balanced, performance). It can only lighten
    /// Frankenstein's own work: when the game itself is heavy on this PC, the game's own graphics settings help more.
    /// What it measured and did goes to UserData/Frankenstein/perf-debug.txt every 30 seconds.</summary>
    internal static class Perf
    {
        /// <summary>auto, quality, balanced or performance (from the app's Performance setting).</summary>
        public static string Setting = "auto";
        /// <summary>0 everything at full rate, 1 lighter, 2 lightest.</summary>
        public static int Level;
        public static float Fps = 60f, OwnMs;
        public const float BudgetMs = 2f;

        /// <summary>How much less often the extras run at this level.</summary>
        public static float Scale => Level == 0 ? 1f : Level == 1 ? 2f : 4f;
        public static float Every(float seconds) => seconds * Scale;
        /// <summary>Crew members further than this from you think only now and then.</summary>
        public static float FarNpc => Level == 0 ? 120f : Level == 1 ? 80f : 50f;
        public static string LevelName => Level == 0 ? "full" : Level == 1 ? "lighter" : "lightest";

        static readonly Stopwatch _update = new Stopwatch(), _gui = new Stopwatch();
        static float _dt, _own, _evalAt, _logAt, _slowFor, _smoothFor;
        static int _frames;
        static readonly List<string> _log = new List<string>();

        public static void Read(string setting)
        {
            setting = (setting ?? "").Trim().ToLowerInvariant();
            Setting = setting == "quality" || setting == "balanced" || setting == "performance" ? setting : "auto";
            Level = Setting == "balanced" ? 1 : Setting == "performance" ? 2 : 0;
            Note("setting: " + Setting + ", starting at " + LevelName);
        }

        public static void Begin() { _update.Reset(); _update.Start(); }

        public static void End()
        {
            _update.Stop();
            _own += (float)_update.Elapsed.TotalMilliseconds;
            _dt += Time.unscaledDeltaTime;
            _frames++;
            try { Check(); } catch (Exception) { }
        }

        public static void GuiBegin() { _gui.Reset(); _gui.Start(); }
        public static void GuiEnd() { _gui.Stop(); _own += (float)_gui.Elapsed.TotalMilliseconds; }

        /// <summary>Every 2 seconds: the FPS and the mod's own cost over those frames, and the level that fits.</summary>
        static void Check()
        {
            float now = Time.unscaledTime;
            if (now < _evalAt) return;
            _evalAt = now + 2f;
            if (_frames == 0) return;
            Fps = _frames / Mathf.Max(0.001f, _dt);
            OwnMs = _own / _frames;
            _frames = 0;
            _dt = _own = 0;
            int want = Level;
            if (Setting == "quality") want = 0;
            else if (Setting == "balanced") want = 1;
            else if (Setting == "performance") want = 2;
            else if (Loading.Showing || MenuSkin.Active || S1API.Entities.Player.Local == null)
                _slowFor = _smoothFor = 0;  // loading and menus aren't play
            else if (Fps < 40f || OwnMs > BudgetMs)
            {
                _smoothFor = 0;
                _slowFor += 2f;
                if (_slowFor >= 6f && Level < 2) { want = Level + 1; _slowFor = 0; }
            }
            else if (Fps > 55f && OwnMs < BudgetMs / 2)
            {
                _slowFor = 0;
                _smoothFor += 2f;
                if (_smoothFor >= 30f && Level > 0) { want = Level - 1; _smoothFor = 0; }
            }
            else _slowFor = _smoothFor = 0;
            if (want != Level)
            {
                bool down = want > Level;
                Level = want;
                Note((down ? "turned extras down" : "turned extras back up") + " to " + LevelName + " (" + Fps.ToString("0") + " FPS, mod " + OwnMs.ToString("0.00") + " ms)");
                if (Setting == "auto")
                    ItemMenu.Notify(down ? "Frankenstein turned its extras down to keep the game smooth (" + Fps.ToString("0") + " FPS)."
                                         : "The game's running smoothly again: Frankenstein's extras are back up.");
            }
            if (now >= _logAt)
            {
                _logAt = now + 30f;
                Note(Fps.ToString("0") + " FPS, mod " + OwnMs.ToString("0.00") + " ms a frame (budget " + BudgetMs + " ms), extras " + LevelName);
            }
        }

        static void Note(string line)
        {
            _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
            if (_log.Count > 300) _log.RemoveRange(0, _log.Count - 300);
            try
            {
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "perf-debug.txt"), new[] { "Frankenstein " + Plugin.Version + " · performance: " + Setting }.Concat(_log).ToArray());
            }
            catch (Exception) { }
        }
    }
}
