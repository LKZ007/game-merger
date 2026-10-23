using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The look, pushed towards the concept art: Frankenstein's own global post-processing volume over the
    /// game's (bloom, ACES tone mapping, warm-and-teal colour grading, vignette), golden-hour and night grades that
    /// follow the game's clock, longer shadows and sharper far detail. The preset comes from the app's Graphics
    /// setting (cinematic, high, balanced, performance). When the mod's performance check turns its extras down,
    /// the look steps down with it, so the no-lag rule wins. Schedule I's own characters, terrain and art style
    /// stay as they are: this changes light and colour, not models. What it found and set goes to gfx-debug.txt.</summary>
    internal static class Gfx
    {
        public static string Setting = "high";
        public static string Now => _applied.Length > 0 ? _applied + " (" + _grade + ")" : "not applied yet";
        static string _applied = "", _grade = "";
        static float _next;
        static GameObject _volume;
        static object _profile;
        static readonly List<string> _log = new List<string>();
        static readonly Dictionary<string, object> _comps = new Dictionary<string, object>();
        static float _shadowWas = -1, _lodWas = -1;

        public static void Read(string setting)
        {
            setting = (setting ?? "").Trim().ToLowerInvariant();
            Setting = setting == "cinematic" || setting == "balanced" || setting == "performance" ? setting : "high";
            Say("graphics setting: " + Setting);
        }

        /// <summary>The preset in use now: the setting, one step lower for each step the performance check took.</summary>
        static string Effective()
        {
            var order = new[] { "cinematic", "high", "balanced", "performance" };
            int i = Array.IndexOf(order, Setting);
            return order[Math.Min(order.Length - 1, i + Perf.Level)];
        }

        static string Grade()
        {
            int hhmm = -1;
            try
            {
                var tm = Rt.Instance(Rt.Find("ScheduleOne.GameTime.TimeManager"));
                var v = Rt.Get(tm, "CurrentTime");
                if (v != null) hhmm = Convert.ToInt32(v);
            }
            catch (Exception) { }
            if (hhmm < 0) return "day";
            if (hhmm >= 1700 && hhmm < 2000) return "sunset";
            if (hhmm >= 2000 || hhmm < 500) return "night";
            if (hhmm < 700) return "dawn";
            return "day";
        }

        public static void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            var cam = Camera.main;
            if (cam == null) return;
            string want = Effective(), grade = Grade();
            if (want == _applied && grade == _grade && _volume != null) return;
            try { Apply(cam, want, grade); }
            catch (Exception e) { Say("couldn't apply the look: " + (e.InnerException ?? e).Message); _next = Time.unscaledTime + 30f; }
        }

        static void Apply(Camera cam, string preset, string grade)
        {
            bool first = _applied.Length == 0;
            _applied = preset;
            _grade = grade;
            // the camera has to draw post-processing at all
            var acd = Rt.Comp(cam.gameObject, Rt.Find("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData"));
            if (acd != null)
            {
                Rt.Set(acd, preset != "performance", "renderPostProcessing");
                SetEnum(acd, "antialiasing", preset == "cinematic" ? 2 : preset == "high" ? 1 : 0);  // SMAA, FXAA, none
            }
            else if (first) Say("this camera has no URP camera data: post-processing may not show (is the game on URP?)");
            if (_shadowWas < 0) { _shadowWas = QualitySettings.shadowDistance; _lodWas = QualitySettings.lodBias; }
            QualitySettings.shadowDistance = preset == "cinematic" ? Mathf.Max(_shadowWas, 160f) : preset == "high" ? Mathf.Max(_shadowWas, 110f) : preset == "balanced" ? _shadowWas : Mathf.Min(_shadowWas, 50f);
            QualitySettings.lodBias = preset == "cinematic" ? Mathf.Max(_lodWas, 2f) : preset == "high" ? Mathf.Max(_lodWas, 1.5f) : preset == "balanced" ? _lodWas : Mathf.Min(_lodWas, 0.8f);
            if (!Volume()) return;
            Rt.Set(Rt.Comp(_volume, Rt.Find("UnityEngine.Rendering.Volume")), preset == "performance" ? 0f : 1f, "weight");
            if (preset == "performance") { Say("performance: the game's own look, shadows " + QualitySettings.shadowDistance.ToString("0") + " m"); return; }
            float k = preset == "cinematic" ? 1f : preset == "high" ? 0.7f : 0.4f;
            bool night = grade == "night", sunset = grade == "sunset" || grade == "dawn";
            Param("Bloom", "intensity", (night ? 1.3f : sunset ? 1.0f : 0.7f) * k);
            Param("Bloom", "threshold", night ? 0.85f : 0.95f);
            Param("Bloom", "scatter", 0.7f);
            ParamEnum("Tonemapping", "mode", preset == "balanced" ? 1 : 2);  // Neutral, ACES
            Param("ColorAdjustments", "contrast", 18f * k);
            Param("ColorAdjustments", "saturation", (night ? 4f : 12f) * k);
            Param("ColorAdjustments", "postExposure", night ? 0.15f : 0.05f);
            Param("WhiteBalance", "temperature", (night ? -14f : sunset ? 22f : 8f) * k);
            Param("WhiteBalance", "tint", night ? 4f * k : 0f);
            Param("Vignette", "intensity", 0.3f * k);
            Param("Vignette", "smoothness", 0.45f);
            ParamColor("SplitToning", "shadows", night ? new Color(0.3f, 0.38f, 0.62f) : new Color(0.32f, 0.44f, 0.52f));
            ParamColor("SplitToning", "highlights", night ? new Color(0.85f, 0.55f, 0.95f) : new Color(1f, 0.72f, 0.45f));
            Param("SplitToning", "balance", sunset ? 20f : 0f);
            Say(preset + " look, " + grade + " grade: bloom, ACES, warm/teal grading, vignette" +
                "; shadows " + QualitySettings.shadowDistance.ToString("0") + " m, LOD bias " + QualitySettings.lodBias.ToString("0.0") +
                (Perf.Level > 0 ? " (stepped down from " + Setting + " to keep 40+ FPS)" : "") + "; found " + _comps.Count + " effects");
        }

        /// <summary>Frankenstein's own global volume, above the game's, so the game's own profile is never changed.</summary>
        static bool Volume()
        {
            if (_volume != null && _profile != null) return true;
            var vt = Rt.Find("UnityEngine.Rendering.Volume");
            var pt = Rt.Find("UnityEngine.Rendering.VolumeProfile");
            var add = Rt.Generic(typeof(GameObject), "AddComponent", 0);
            var make = Rt.Generic(typeof(ScriptableObject), "CreateInstance", 0);
            if (vt == null || pt == null || add == null || make == null) { Say("no post-processing volumes in this build (Volume " + (vt != null) + ", profile " + (pt != null) + ")"); _next = Time.unscaledTime + 600f; return false; }
            _volume = new GameObject("Frankenstein look");
            UnityEngine.Object.DontDestroyOnLoad(_volume);
            var vol = add.MakeGenericMethod(vt).Invoke(_volume, null);
            _profile = make.MakeGenericMethod(pt).Invoke(null, null);
            Rt.Set(vol, true, "isGlobal");
            Rt.Set(vol, 50f, "priority");
            Rt.Set(vol, 1f, "weight");
            Rt.Set(vol, _profile, "sharedProfile");
            _comps.Clear();
            Say("own post-processing volume made (the game's own volumes: " + Rt.All(vt).Count + ")");
            return true;
        }

        /// <summary>An effect on our profile, added the first time (with nothing overridden until we set it).</summary>
        static object Comp(string name)
        {
            object c;
            if (_comps.TryGetValue(name, out c)) return c;
            var t = Rt.Find("UnityEngine.Rendering.Universal." + name);
            var add = _profile?.GetType().GetMethods().FirstOrDefault(m => m.Name == "Add" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (t == null || add == null) { _comps[name] = null; if (t == null) Say("no " + name + " effect in this build"); return null; }
            try { c = add.MakeGenericMethod(t).Invoke(_profile, new object[] { false }); }
            catch (Exception e) { Say(name + ": " + (e.InnerException ?? e).Message); c = null; }
            _comps[name] = c;
            return c;
        }

        static void Param(string comp, string field, float v) => SetParam(comp, field, v);
        static void ParamColor(string comp, string field, Color v) => SetParam(comp, field, v);

        static void ParamEnum(string comp, string field, int v)
        {
            var p = Rt.Get(Comp(comp), field);
            if (p == null) return;
            SetEnum(p, "value", v);
            Rt.Set(p, true, "overrideState");
        }

        static void SetParam(string comp, string field, object v)
        {
            var p = Rt.Get(Comp(comp), field);
            if (p == null) return;
            Rt.Set(p, v, "value");
            Rt.Set(p, true, "overrideState");
        }

        static void SetEnum(object o, string name, int v)
        {
            if (o == null) return;
            var prop = o.GetType().GetProperties().FirstOrDefault(x => x.Name == name && x.CanWrite);
            try { if (prop != null && prop.PropertyType.IsEnum) prop.SetValue(o, Enum.ToObject(prop.PropertyType, v), null); }
            catch (Exception) { }
        }

        static void Say(string line)
        {
            _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
            if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);
            try
            {
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "gfx-debug.txt"), new[] { "Frankenstein " + Plugin.Version + " · graphics: " + Setting }.Concat(_log).ToArray());
            }
            catch (Exception) { }
        }
    }
}
