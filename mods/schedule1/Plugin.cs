using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

[assembly: MelonInfo(typeof(Frankenstein.Plugin), "Frankenstein", Frankenstein.Plugin.Version, "Frankenstein", "https://github.com/LKZ007/game-merger")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace Frankenstein
{
    /// <summary>Frankenstein's own Schedule I mod: the item menu (F6) and story mode.
    /// Frankenstein writes UserData/Frankenstein/settings.json before each launch.</summary>
    public class Plugin : MelonMod
    {
        /// <summary>The Frankenstein release this mod came with (equal to gamemerger/version.py; a test checks).
        /// Shown on F6's title and in MelonLoader's log, where the app reads it back for the merge's card.</summary>
        public const string Version = "3.36.0";
        float _next;
        public static bool MenuAllowed = true;
        public static System.Collections.Generic.List<string> Kit = new System.Collections.Generic.List<string>();
        public static string KitName = "";
        public static System.Collections.Generic.List<string> Shop = new System.Collections.Generic.List<string>();
        public static string ShopName = "hardware";

        public override void OnInitializeMelon()
        {
            ReadSettings();
            Melon<Plugin>.Logger.Msg("Frankenstein mod " + Version + " loaded for " + (MainMenu.Merge.Length > 0 ? MainMenu.Merge : "Schedule I") + (Mode.Pending == "creative" ? " (next game: Creative)" : "") + ".");
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
                // Creative is a mode on each save now (Mode): this only picks it for the next game started or loaded
                if (J.S(s, "creative", "false").ToLowerInvariant() == "true") Mode.Pending = "creative";
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
                Loading.Read(J.O(s, "loading"));
                Guns.Read(J.O(s, "guns"));
                Gangs.Read(J.O(s, "gangs"));
                Carry.Read(s is System.Collections.Generic.Dictionary<string, object> d3 && d3.TryGetValue("carry", out var cv) ? cv : null);
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

        public override void OnSceneWasInitialized(int buildIndex, string sceneName) { MainMenu.Scene(sceneName); World.Scene(sceneName); Loading.Scene(sceneName); Skip.Scene(sceneName); }

        public override void OnUpdate()
        {
            Pad.Update();
            try { Mode.Tick(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Game mode: " + e.Message); }
            try { Vehicles.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Vehicles: " + e.Message); }
            try { Skip.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Skip tutorial: " + e.Message); }
            try { Garage.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Garage: " + e.Message); }
            try { Melee.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Swing: " + e.Message); }
            try { Drop.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Drop: " + e.Message); }
            try { Fly.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Fly: " + e.Message); }
            try { Wheel.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Wheel: " + e.Message); }
            ItemMenu.Update();
            MainMenu.Update();
            try { Creative.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Creative: " + e.Message); }
            try { Gangs.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Gangs: " + e.Message); }
            try { OwnGuns.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Own guns: " + e.Message); }
            try { Guns.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Guns: " + e.Message); }
            try { Crew.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Crew: " + e.Message); }
            try { Boom.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Explosives: " + e.Message); }
            try { Carry.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Carry: " + e.Message); }
            try { World.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("World: " + e.Message); }
            try { Expansion.Update(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Expansion: " + e.Message); }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            try { Story.Tick(); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Story: " + e.Message); }
            try { Bridge.Tick(); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Spawner: " + e.Message); }
            try { GunShop.Tick(); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Gun shop: " + e.Message); }
        }

        public override void OnLateUpdate()
        {
            try { Fly.LateUpdate(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Fly: " + e.Message); }
            try { Melee.LateUpdate(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Swing: " + e.Message); }
        }

        public override void OnGUI()
        {
            Pad.GuiBegin();
            DrawAll();
            Pad.GuiEnd();
        }

        static void DrawAll()
        {
            try { Wheel.Draw(); } catch (Exception) { }
            try { Fly.Draw(); } catch (Exception) { }
            try { Drop.Draw(); } catch (Exception) { }
            try { Vehicles.Draw(); } catch (Exception) { }
            ItemMenu.Draw();
            MainMenu.Draw();
            World.Draw();
            try { Boom.Draw(); } catch (Exception) { }
            try { Crew.Draw(); } catch (Exception) { }
            try { Expansion.Draw(); } catch (Exception) { }
            try { Gangs.Draw(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Gangs: " + e.Message); }
            try { Loading.Draw(); } catch (Exception e) { Melon<Plugin>.Logger.Warning("Loading screen: " + e.Message); Loading.Tips.Clear(); Loading.Extension = ""; }
        }
    }
}
