using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(Frankenstein.Plugin), "Frankenstein", "1.0.0", "Frankenstein", "https://github.com/LKZ007/game-merger")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace Frankenstein
{
    /// <summary>Frankenstein's own Schedule I mod: the item menu (F6) and story mode.
    /// Frankenstein writes UserData/Frankenstein/settings.json before each launch.</summary>
    public class Plugin : MelonMod
    {
        float _next;
        public static bool MenuAllowed = true;
        public static System.Collections.Generic.List<string> Kit = new System.Collections.Generic.List<string>();
        public static string KitName = "";
        public static System.Collections.Generic.List<string> Shop = new System.Collections.Generic.List<string>();
        public static string ShopName = "hardware";

        public override void OnInitializeMelon()
        {
            ReadSettings();
            World.Load();
            Story.Load();
            ItemMenu.Load();
        }

        static void ReadSettings()
        {
            try
            {
                var p = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "settings.json");
                if (!File.Exists(p)) return;
                var s = MiniJson.Parse(File.ReadAllText(p));
                MenuAllowed = J.S(s, "item_menu", "true") != "False" && J.S(s, "item_menu", "true") != "false";
                foreach (var k in J.L(s, "kit")) Kit.Add(k.ToString());
                KitName = J.S(s, "kit_name", "Starter kit");
                foreach (var k in J.L(s, "shop")) Shop.Add(k.ToString());
                ShopName = J.S(s, "shop_name", "hardware");
                MainMenu.Merge = J.S(s, "merge");
                MainMenu.Creative = J.S(s, "creative", "false").ToLowerInvariant() == "true";
                foreach (var k in J.L(s, "rundown")) MainMenu.Rundown.Add(k.ToString());
                var pa = s is System.Collections.Generic.Dictionary<string, object> d && d.TryGetValue("play_as", out var v) ? v : null;
                var w = s is System.Collections.Generic.Dictionary<string, object> d2 && d2.TryGetValue("world", out var wv) ? wv : null;
                if (w != null)
                {
                    World.Style = J.S(w, "style");
                    World.Zone = J.S(w, "zone");
                    World.Lighting = J.S(w, "lighting");
                    World.Title = J.S(w, "title");
                    foreach (var k in J.L(w, "modes")) World.Modes.Add(k.ToString());
                }
                World.Host = J.S(s, "host", "Schedule I");
                if (pa != null && J.S(pa, "style").Length > 0)
                {
                    MainMenu.Who = J.S(pa, "who");
                    MainMenu.Moves = J.S(pa, "moves");
                    MainMenu.Close = J.S(pa, "close");
                }
            }
            catch (Exception e)
            {
                Melon<Plugin>.Logger.Warning("Couldn't read Frankenstein's settings: " + e.Message);
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName) { MainMenu.Scene(sceneName); World.Scene(sceneName); }

        public override void OnUpdate()
        {
            ItemMenu.Update();
            MainMenu.Update();
            try { World.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("World: " + e.Message); }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            try { Story.Tick(); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Story: " + e.Message); }
            try { Bridge.Tick(); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Spawner: " + e.Message); }
            try { GunShop.Tick(); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Gun shop: " + e.Message); }
        }

        public override void OnGUI()
        {
            ItemMenu.Draw();
            MainMenu.Draw();
            World.Draw();
        }
    }
}
