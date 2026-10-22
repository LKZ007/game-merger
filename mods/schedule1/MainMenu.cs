using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Frankenstein inside the game's own main menu, under Continue and New game: Creative (its own
    /// saves), back to the normal game, and the merge's rundown. Creative needs the game to start again with other
    /// saves, so the button leaves UserData/Frankenstein/switch.json and closes the game; Frankenstein starts it
    /// again in that mode. While you play, the corner shows who you play as.</summary>
    internal static class MainMenu
    {
        public static string Merge = "";
        public static bool Creative;
        public static List<string> Rundown = new List<string>();
        public static string Who = "", Moves = "", Close = "";
        static bool _inMenu, _showRundown;
        static float _quitAt = -1;
        static string _note = "";

        public static void Scene(string name) => _inMenu = name != null && name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0;

        static void Switch(string to)
        {
            try
            {
                File.WriteAllText(Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "switch.json"), "{\"to\": \"" + to + "\"}");
                _note = "Starting again in " + (to == "creative" ? "Creative" : "your normal game") + "…";
                _quitAt = Time.unscaledTime + 1.5f;
            }
            catch (Exception e) { _note = "Couldn't switch: " + e.Message; }
        }

        public static void Update()
        {
            if (_quitAt > 0 && Time.unscaledTime >= _quitAt) { _quitAt = -1; Application.Quit(); }
        }

        public static void Draw()
        {
            if (!_inMenu)
            {
                if (Who.Length > 0) GUI.Label(new Rect(16, Screen.height - 34, 600, 24), "Playing as " + Who);
                return;
            }
            float w = 360, x = Screen.width * 0.08f, y = Screen.height * 0.62f;
            float h = _showRundown ? 150 + 22 * Math.Min(Rundown.Count, 12) : 150;
            GUI.Box(new Rect(x, y, w, h), "");
            GUI.Label(new Rect(x + 12, y + 8, w - 24, 22), "FRANKENSTEIN  ·  " + (Merge.Length > 0 ? Merge : "this merge") + (Creative ? "  ·  Creative" : ""));
            if (Creative)
            {
                if (GUI.Button(new Rect(x + 12, y + 36, w - 24, 32), "Back to my normal game")) Switch("normal");
            }
            else if (GUI.Button(new Rect(x + 12, y + 36, w - 24, 32), "Creative (every item, its own saves)")) Switch("creative");
            if (GUI.Button(new Rect(x + 12, y + 74, w - 24, 28), _showRundown ? "Hide the rundown" : "What's in this merge")) _showRundown = !_showRundown;
            GUI.Label(new Rect(x + 12, y + 108, w - 24, 40), _note.Length > 0 ? _note : (Who.Length > 0 ? "You play as " + Who + " (" + Close + ")" : "F6 in game: every item"));
            if (!_showRundown) return;
            for (int i = 0; i < Rundown.Count && i < 12; i++)
                GUI.Label(new Rect(x + 12, y + 146 + 22 * i, w - 24, 22), Rundown[i]);
        }
    }
}
