using System;
using System.IO;
using System.Linq;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>One page that says how this milestone is doing in your game, rewritten every 30 seconds:
    /// UserData/Frankenstein/selftest.txt. The other *-debug.txt files have the detail.</summary>
    internal static class SelfTest
    {
        static float _next;

        public static void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 30f;
            try
            {
                var cat = global::Frankenstein.Models.Catalog;
                var lines = new[]
                {
                    "Frankenstein " + Plugin.Version + " self-test, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    "Models on this PC: " + cat.Count + " (" + string.Join(", ", cat.GroupBy(e => e.Kind).Select(g => g.Count() + " " + g.Key).ToArray()) + ")" + (cat.Count == 0 ? "  <- the app downloads them; Settings > Graphics and models" : ""),
                    "Models built in the game so far: " + global::Frankenstein.Models.Built,
                    "Graphics: " + Gfx.Setting + ", now " + Gfx.Now,
                    "FPS: " + Perf.Fps.ToString("0") + ", Frankenstein's own work " + Perf.OwnMs.ToString("0.00") + " ms a frame, extras " + Perf.LevelName,
                    "Test city block: " + (Districts.Has ? Districts.Note : "not placed (F8)"),
                    "Detail: models-debug.txt, gfx-debug.txt, districts-debug.txt, garage-debug.txt, perf-debug.txt",
                };
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "selftest.txt"), lines);
            }
            catch (Exception) { }
        }
    }
}
