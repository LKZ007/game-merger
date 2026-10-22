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
            }
            catch (Exception e)
            {
                Melon<Plugin>.Logger.Warning("Couldn't read Frankenstein's settings: " + e.Message);
            }
        }

        public override void OnUpdate()
        {
            ItemMenu.Update();
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            try { Story.Tick(); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Story: " + e.Message); }
            try { GunShop.Tick(); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Gun shop: " + e.Message); }
        }

        public override void OnGUI()
        {
            ItemMenu.Draw();
        }
    }
}
