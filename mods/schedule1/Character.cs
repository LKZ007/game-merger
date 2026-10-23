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
    /// <summary>A new Creative world doesn't make you build a character. When the game's character creator opens in
    /// a Creative save, Frankenstein asks first: Quick start (your latest story character's look, or the game's
    /// default), use the character from one of your story saves (its look is copied, the save itself is only read),
    /// or make a new one in the game's own creator. Mouse, keyboard (arrows, Enter) or controller (d-pad, A).
    /// What it found and did is written to UserData/Frankenstein/character-debug.txt.</summary>
    internal static class Character
    {
        class Choice
        {
            public string Title, Sub, Look;  // Look: the appearance JSON to use, null for the game's own default
            public bool Create;              // keep the game's creator open
        }

        static readonly List<Choice> _choices = new List<Choice>();
        static readonly HashSet<string> _asked = new HashSet<string>();
        static readonly List<string> _log = new List<string>();
        static object _creator;
        static Type _ccType;
        static float _scanAt, _finishAt = -1;
        static int _sel;
        static bool _open, _stickReady = true;
        static string _pendingLook;
        static GUIStyle _title, _row, _sub;

        public static bool Open => _open;

        public static void Update()
        {
            bool creative = MainMenu.Creative || Mode.Pending == "creative";
            if (_finishAt > 0 && Time.unscaledTime >= _finishAt) { _finishAt = -1; Finish(); }
            if (!_open)
            {
                if (!creative || Time.unscaledTime < _scanAt) return;
                _scanAt = Time.unscaledTime + 0.5f;
                if (_ccType == null) _ccType = Rt.FindByName("CharacterCreator");
                if (_ccType == null) return;
                var cc = Rt.All(_ccType).FirstOrDefault(c => c is Component k && k != null && k.gameObject.activeInHierarchy && IsOpen(c));
                var key = Mode.SavePath.Length > 0 ? Mode.SavePath : "new";
                if (cc == null || _asked.Contains(key)) return;
                _asked.Add(key);
                _creator = cc;
                Build();
                _open = true;
                _sel = 0;
                Say("creator open in a Creative save; it has: " + string.Join(", ", _ccType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                                                                     .Select(m => m.Name).Where(nm => !nm.StartsWith("get_") && !nm.StartsWith("set_")).Distinct().ToArray()));
                return;
            }
            int n = _choices.Count;
            if ((Pad.Pressed & Pad.Down) != 0 || Input.GetKeyDown(KeyCode.DownArrow)) _sel = (_sel + 1) % n;
            if ((Pad.Pressed & Pad.Up) != 0 || Input.GetKeyDown(KeyCode.UpArrow)) _sel = (_sel + n - 1) % n;
            if (_stickReady && Mathf.Abs(Pad.Stick.y) > 0.6f) { _sel = (_sel + (Pad.Stick.y < 0 ? 1 : n - 1)) % n; _stickReady = false; }
            if (Mathf.Abs(Pad.Stick.y) < 0.3f) _stickReady = true;
            if ((Pad.Pressed & Pad.A) != 0 || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Pick(_choices[_sel]);
        }

        static bool IsOpen(object cc)
        {
            var o = Rt.Get(cc, "IsOpen", "isOpen", "Open");
            if (o is bool b) return b;
            var canvas = Rt.Get(cc, "Canvas", "canvas");
            var en = Rt.Get(canvas, "enabled");
            return !(en is bool e) || e;
        }

        /// <summary>Quick start, every story save that has a character, and make a new one.</summary>
        static void Build()
        {
            _choices.Clear();
            var looks = new List<KeyValuePair<Profiles.Slot, string>>();
            foreach (var s in Profiles.All().Where(s => !s.Empty && s.Mode != "creative").OrderByDescending(s => s.Played))
            {
                var look = LookOf(s.Path);
                if (look != null) looks.Add(new KeyValuePair<Profiles.Slot, string>(s, look));
            }
            var latest = looks.FirstOrDefault();
            _choices.Add(new Choice
            {
                Title = "Quick start", Look = latest.Value,
                Sub = latest.Value != null ? "Skip character creation, as " + latest.Key.Name + "'s character" : "Skip character creation with the game's own look",
            });
            foreach (var kv in looks)
                _choices.Add(new Choice { Title = "Use " + kv.Key.Name + "'s character", Sub = "From your story save " + kv.Key.Number + " (that save isn't changed)", Look = kv.Value });
            _choices.Add(new Choice { Title = "Make a new character", Sub = "The game's own character creator", Create = true });
            Say(looks.Count + " story save(s) with a character: " + string.Join(", ", looks.Select(l => l.Key.Name).ToArray()));
        }

        /// <summary>A save's character as appearance JSON: the object in its Players folder that has a hair style and skin colour.</summary>
        static string LookOf(string save)
        {
            try
            {
                var dir = Path.Combine(save, "Players");
                if (!Directory.Exists(dir)) dir = save;
                foreach (var f in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
                {
                    var text = File.ReadAllText(f);
                    if (text.IndexOf("HairStyle", StringComparison.Ordinal) < 0) continue;
                    var found = Holder(MiniJson.Parse(text));
                    if (found != null) { Say("look found in " + f.Substring(save.Length)); return MiniJson.Write(found); }
                }
            }
            catch (Exception e) { Say("reading " + save + " failed: " + e.Message); }
            return null;
        }

        static object Holder(object o)
        {
            var d = o as Dictionary<string, object>;
            if (d != null)
            {
                if (d.ContainsKey("HairStyle") && (d.ContainsKey("SkinColor") || d.ContainsKey("Gender"))) return d;
                foreach (var v in d.Values) { var h = Holder(v is string s && s.TrimStart().StartsWith("{") ? MiniJson.Parse(s) : v); if (h != null) return h; }
            }
            var l = o as List<object>;
            if (l != null) foreach (var v in l) { var h = Holder(v); if (h != null) return h; }
            return null;
        }

        static void Pick(Choice c)
        {
            _open = false;
            if (c.Create) { Say("making a new character in the game's creator"); return; }
            Say("picked: " + c.Title);
            if (c.Look != null) Apply(c.Look);
            _pendingLook = c.Look;
            _finishAt = Time.unscaledTime + 0.4f;  // a moment for the creator to show the look
        }

        /// <summary>The look goes into the creator's own settings, so the game builds the character from it.</summary>
        static void Apply(string json)
        {
            var settings = Rt.Get(_creator, "ActiveSettings", "activeSettings", "CurrentSettings", "currentSettings", "Settings", "settings");
            if (settings == null) { Say("the creator's settings weren't found; the look is put on after it closes"); return; }
            if (Overwrite(json, settings)) Say("look put into the creator's " + settings.GetType().Name);
            foreach (var name in new[] { "ApplySettings", "SetSettings", "RefreshAvatar", "UpdateAvatar", "Refresh" })
            {
                var m = _creator.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                .FirstOrDefault(x => x.Name == name && (x.GetParameters().Length == 0 || (x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType.IsInstanceOfType(settings))));
                if (m == null) continue;
                try { m.Invoke(_creator, m.GetParameters().Length == 0 ? null : new[] { settings }); Say("creator." + name + " ran"); break; }
                catch (Exception e) { Say("creator." + name + " failed: " + (e.InnerException ?? e).Message); }
            }
        }

        static bool Overwrite(string json, object target)
        {
            try
            {
                var ju = Rt.Find("UnityEngine.JsonUtility");
                var m = ju?.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => x.Name == "FromJsonOverwrite" && x.GetParameters().Length == 2);
                if (m == null) { Say("no JsonUtility.FromJsonOverwrite in this build"); return false; }
                m.Invoke(null, new[] { json, target });
                return true;
            }
            catch (Exception e) { Say("putting the look in failed: " + (e.InnerException ?? e).Message); return false; }
        }

        static readonly string[] Done = { "Done", "Finish", "Complete", "Confirm", "Submit", "Close" };

        /// <summary>Closes the creator the way its own Done button does.</summary>
        static void Finish()
        {
            foreach (var name in Done)
            {
                var m = _creator?.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                 .FirstOrDefault(x => x.Name == name && x.GetParameters().All(p => p.IsOptional));
                if (m == null) continue;
                try
                {
                    m.Invoke(_creator, m.GetParameters().Select(p => p.DefaultValue).ToArray());
                    Say("closed the creator with " + name);
                    if (_pendingLook != null) ItemMenu.Notify("Creative: your character is ready.");
                    return;
                }
                catch (Exception e) { Say(name + " failed: " + (e.InnerException ?? e).Message); }
            }
            Say("couldn't close the creator by itself");
            ItemMenu.Notify("Your look is set: press Done in the character creator to start.");
        }

        public static void Draw()
        {
            if (!_open) return;
            float W = Screen.width, H = Screen.height, k = H / 1080f;
            if (_title == null || _title.fontSize != Mathf.RoundToInt(44 * k))
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(44 * k), wordWrap = false };
                _row = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(28 * k), wordWrap = false };
                _sub = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * k), wordWrap = false };
                _sub.normal.textColor = new Color(0.72f, 0.75f, 0.8f);
            }
            Paint.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.02f, 0.04f, 0.88f));
            float w = Mathf.Min(900 * k, W - 40), x = (W - w) / 2, y = H * 0.2f, h = 92 * k;
            GUI.Label(new Rect(x, y, w, 60 * k), "<b>YOUR CREATIVE CHARACTER</b>", _title);
            y += 86 * k;
            var m = Event.current.mousePosition;
            for (int i = 0; i < _choices.Count; i++)
            {
                var c = _choices[i];
                var r = new Rect(x, y, w, h - 10 * k);
                bool on = i == _sel;
                Paint.Fill(r, on ? new Color(0.26f, 0.6f, 0.23f, 0.95f) : new Color(0.08f, 0.09f, 0.12f, 0.9f));
                GUI.Label(new Rect(r.x + 24 * k, r.y + 10 * k, w, 36 * k), c.Title, _row);
                GUI.Label(new Rect(r.x + 24 * k, r.y + 48 * k, w, 26 * k), c.Sub, _sub);
                if (GUI.Button(r, "", GUIStyle.none)) { _sel = i; Pick(c); }
                y += h;
            }
            GUI.Label(new Rect(x, y + 10 * k, w, 30 * k), Pad.InUse ? "D-pad: choose   ·   A: select" : "Arrows: choose   ·   Enter or click: select", _sub);
        }

        static void Say(string line)
        {
            Melon<Plugin>.Logger.Msg("Character: " + line);
            _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
            try
            {
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, "character-debug.txt"), new[] { "Frankenstein " + Plugin.Version }.Concat(_log.Skip(Math.Max(0, _log.Count - 200))).ToArray());
            }
            catch (Exception) { }
        }
    }
}
