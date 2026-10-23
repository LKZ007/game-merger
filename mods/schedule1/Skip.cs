using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>A new Creative world skips the prologue (the RV in the desert) and starts in town. A few seconds
    /// after the tutorial scene opens in a Creative save, Frankenstein asks the game to finish its tutorial; if this
    /// build has no such call, it loads the same save straight into the main map with the game's own loader. What
    /// it found is written to UserData/Frankenstein/tutorial-debug.txt.</summary>
    internal static class Skip
    {
        static string _scene = "";
        static float _start = -1, _checkAt;
        static int _try = -1;
        static readonly List<string> _tried = new List<string>();
        static readonly HashSet<string> _reloaded = new HashSet<string>();

        public static bool InTutorial => _scene.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         _scene.IndexOf("Prologue", StringComparison.OrdinalIgnoreCase) >= 0;

        public static void Scene(string name)
        {
            _scene = name ?? "";
            _try = -1;
            _tried.Clear();
            _start = InTutorial ? Time.unscaledTime : -1;
            _checkAt = _start + 4f;
        }

        public static void Update()
        {
            if (_start < 0 || Time.unscaledTime < _checkAt) return;
            if (!InTutorial) { _start = -1; return; }
            bool creative = MainMenu.Creative || Mode.Pending == "creative";
            if (!creative)
            {
                if (Time.unscaledTime - _start > 20f) _start = -1;  // a Survival world plays the tutorial as normal
                else _checkAt = Time.unscaledTime + 1f;
                return;
            }
            if (_try < 0) { Debug(); ItemMenu.Notify("Creative: skipping the tutorial, off to town..."); }
            _checkAt = Time.unscaledTime + 5f;  // each way gets a few seconds to change the scene
            if (!Next())
            {
                _start = -1;
                ItemMenu.Notify("Couldn't skip the tutorial on this build (tutorial-debug.txt has why). Play through it once; the save stays Creative.");
            }
        }

        static IEnumerable<Type> GameTypes()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var an = asm.GetName().Name;
                if (an != "Assembly-CSharp" && an != "Il2CppAssembly-CSharp") continue;
                Type[] ts;
                try { ts = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { ts = e.Types.Where(t => t != null).ToArray(); }
                foreach (var t in ts) yield return t;
            }
        }

        static readonly string[] Finish = { "SkipTutorial", "EndTutorial", "FinishTutorial", "CompleteTutorial", "TutorialComplete", "EndPrologue", "SkipPrologue" };

        /// <summary>The next way to leave the tutorial; false when none are left.</summary>
        static bool Next()
        {
            var ways = new List<Func<bool>>();
            foreach (var t in GameTypes().Where(t => t.Name.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 || t.Name == "LoadManager" || t.Name == "GameManager"))
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                   .Where(m => Finish.Contains(m.Name) && m.GetParameters().All(p => p.IsOptional)))
                {
                    var type = t; var method = m;
                    ways.Add(() => Call(type, method));
                }
            // once per save: if the game sends it back to the tutorial, it isn't sent round again
            if (_reloaded.Add(Mode.SavePath))
                ways.Add(() => Profiles.Play(new Profiles.Slot { Path = Mode.SavePath, Name = "this save" }) && Said("LoadManager.StartGame"));
            while (++_try < ways.Count)
                if (ways[_try]()) return true;
            return false;
        }

        static bool Said(string s) { _tried.Add(s); Melon<Plugin>.Logger.Msg("Skip tutorial: " + s); return true; }

        static bool Call(Type t, MethodInfo m)
        {
            try
            {
                object target = null;
                if (!m.IsStatic)
                {
                    target = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null, null) ?? Rt.All(t).FirstOrDefault();
                    if (target == null) return false;
                }
                m.Invoke(target, m.GetParameters().Select(p => p.DefaultValue).ToArray());
                return Said(t.Name + "." + m.Name);
            }
            catch (Exception e) { _tried.Add(t.Name + "." + m.Name + " failed: " + (e.InnerException ?? e).Message); return false; }
        }

        static void Debug()
        {
            try
            {
                var lines = new List<string> { "Frankenstein " + Plugin.Version + ": scene " + _scene + ", save " + Mode.SavePath };
                foreach (var t in GameTypes().Where(t => t.Name.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 || t.Name == "LoadManager").Take(20))
                    lines.Add(t.FullName + ": " + string.Join(", ", t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .Select(m => m.Name + "(" + m.GetParameters().Length + ")").Distinct().Take(60).ToArray()));
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "tutorial-debug.txt"), lines.ToArray());
            }
            catch (Exception) { }
        }
    }
}
