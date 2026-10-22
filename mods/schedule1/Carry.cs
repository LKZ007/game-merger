using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using S1API.Console;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Carry over my save: the Extension game's character in Schedule I. Its money (and a bat for
    /// strength) arrive once per character card; the rest of the card shows in the F8 menu, honestly marked
    /// where Schedule I can't do it yet.</summary>
    internal static class Carry
    {
        public static string Id = "", Name = "", From = "";
        public static double Money;
        public static int Level;
        public static List<string> Powers = new List<string>(), Card = new List<string>();
        static float _at = -1;

        static string Done => Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "carry-done.txt");

        public static void Read(object c)
        {
            if (c == null) return;
            Id = J.S(c, "id");
            Name = J.S(c, "name");
            From = J.S(c, "from");
            Money = J.N(c, "money");
            Level = (int)J.N(c, "level");
            foreach (var p in J.L(c, "powers")) Powers.Add(p.ToString());
            foreach (var p in J.L(c, "card")) Card.Add(p.ToString());
        }

        public static void Scene(bool inTown) { if (inTown && Id.Length > 0) _at = Time.unscaledTime + 15f; }

        public static void Update()
        {
            if (_at < 0 || Time.unscaledTime < _at) return;
            _at = -1;
            try
            {
                if (File.Exists(Done) && File.ReadAllText(Done).Trim() == Id) return;  // this card already came over
                File.WriteAllText(Done, Id);
                if (Powers.Contains("money") && Money > 0) S1API.Money.Money.ChangeCashBalance((float)Money, true, true);
                if (Powers.Contains("strength")) ConsoleHelper.AddItemToInventory("baseballbat", 1);
                ItemMenu.Notify((Name.Length > 0 ? Name : "Your character") + " from " + From + " is here" +
                                (Powers.Contains("money") && Money > 0 ? " with $" + Money : "") + ". F8 shows your character.");
            }
            catch (Exception e) { ItemMenu.Notify("Couldn't bring your character over: " + e.Message); }
        }

        public static void Draw(float x, float y, float w)
        {
            if (Id.Length == 0) return;
            float h = 34 + 20 * Card.Count;
            GUI.Box(new Rect(x, y, w, h), "");
            GUI.Label(new Rect(x + 12, y + 6, w - 24, 22), "Your character: " + (Name.Length > 0 ? Name : "from " + From) + (Level > 0 ? "  ·  level " + Level : ""));
            for (int i = 0; i < Card.Count; i++) GUI.Label(new Rect(x + 12, y + 28 + 20 * i, w - 24, 20), Card[i]);
        }
    }
}
