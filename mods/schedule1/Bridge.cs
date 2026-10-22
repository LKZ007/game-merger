using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using S1API.Console;

namespace Frankenstein
{
    /// <summary>Frankenstein's Spawner (in the app, e.g. on a second screen) sends item requests through
    /// UserData/Frankenstein/commands.json: [{"n": 1, "id": "ak47", "qty": 1, "name": "Choppa"}].
    /// Each request has a rising number, so nothing is given twice.</summary>
    internal static class Bridge
    {
        static double _seq = -1;

        static string File_ => Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "commands.json");

        static List<object> Read()
        {
            try { return File.Exists(File_) ? MiniJson.Parse(File.ReadAllText(File_)) as List<object> : null; }
            catch (Exception) { return null; }
        }

        public static void Tick()
        {
            var cmds = Read();
            if (_seq < 0)
            {
                // requests from before this session don't count
                _seq = 0;
                if (cmds != null) foreach (var c in cmds) _seq = Math.Max(_seq, J.N(c, "n"));
                return;
            }
            if (cmds == null) return;
            foreach (var c in cmds)
            {
                var n = J.N(c, "n");
                if (n <= _seq) continue;
                _seq = n;
                var id = J.S(c, "id");
                if (id.Length == 0) continue;
                int qty = Math.Max(1, (int)J.N(c, "qty", 1));
                try
                {
                    ConsoleHelper.AddItemToInventory(id, qty);
                    ItemMenu.Notify("From Frankenstein: " + qty + " x " + J.S(c, "name", id));
                }
                catch (Exception e) { ItemMenu.Notify("Couldn't give " + id + ": " + e.Message); }
            }
        }
    }
}
