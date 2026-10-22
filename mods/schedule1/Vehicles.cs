using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Every vehicle in the game for the F6 menu's Vehicles tab: the list comes from the game's own
    /// vehicle manager (mods' cars included), with the base game's six as a fallback. Picking one spawns it on the
    /// road in front of you through S1API. The game has no pictures of its cars, so each tile is drawn: the car's
    /// shape (hatchback, van, pickup, SUV, sports) in its own colour.</summary>
    internal static class Vehicles
    {
        public class Car
        {
            public string Code, Name, Kind;
            public float Price;
            public Color Color;
        }

        static readonly string[][] Known =
        {
            new[] { "shitbox", "Shitbox", "hatch" }, new[] { "veeper", "Veeper", "van" }, new[] { "bruiser", "Bruiser", "suv" },
            new[] { "dinkler", "Dinkler", "pickup" }, new[] { "hounddog", "Hounddog", "sports" }, new[] { "cheetah", "Cheetah", "sports" },
        };

        public static List<Car> All()
        {
            var cars = new List<Car>();
            try
            {
                var mgr = Rt.Find("ScheduleOne.Vehicles.VehicleManager");
                var inst = mgr?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null, null)
                           ?? (mgr != null ? Rt.All(mgr).FirstOrDefault() : null);
                foreach (var v in Creative.Each(Rt.Get(inst, "VehiclePrefabs", "vehiclePrefabs")))
                {
                    var code = Rt.Get(v, "VehicleCode", "vehicleCode") as string;
                    if (string.IsNullOrEmpty(code) || cars.Any(c => c.Code == code)) continue;
                    var name = Rt.Get(v, "VehicleName", "vehicleName") as string;
                    float price = 0;
                    try { price = Convert.ToSingle(Rt.Get(v, "VehiclePrice", "vehiclePrice") ?? 0f); } catch (Exception) { }
                    cars.Add(Make(code, string.IsNullOrEmpty(name) ? code : name, price));
                }
            }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Vehicles: couldn't read the game's list (" + e.Message + "), using the known six"); }
            foreach (var k in Known)
                if (!cars.Any(c => c.Code == k[0])) cars.Add(Make(k[0], k[1], 0));
            return cars;
        }

        static Car Make(string code, string name, float price)
        {
            var known = Known.FirstOrDefault(k => k[0] == code.ToLowerInvariant());
            string kind = known != null ? known[2] : Guess(code + " " + name);
            int h = 0;
            foreach (var ch in code) h = h * 31 + ch;
            return new Car { Code = code, Name = name, Price = price, Kind = kind, Color = Color.HSVToRGB((Math.Abs(h) % 360) / 360f, 0.6f, 0.85f) };
        }

        static string Guess(string s)
        {
            s = s.ToLowerInvariant();
            if (s.Contains("van") || s.Contains("bus")) return "van";
            if (s.Contains("truck") || s.Contains("pickup")) return "pickup";
            if (s.Contains("suv") || s.Contains("jeep")) return "suv";
            if (s.Contains("sport") || s.Contains("race") || s.Contains("gt")) return "sports";
            return "hatch";
        }

        /// <summary>Spawns the car a few metres in front of you, facing where you look.</summary>
        public static bool Spawn(Car car)
        {
            var cam = Camera.main;
            var me = Player.Local;
            if (cam == null || me == null) { ItemMenu.Notify("Load into the town first, then spawn a car."); return false; }
            var fwd = cam.transform.forward;
            fwd.y = 0;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            var at = me.Position + fwd * 6f + Vector3.up * 0.6f;
            try
            {
                var v = S1API.Vehicles.VehicleRegistry.CreateVehicle(car.Code);
                if (v == null) { ItemMenu.Notify("This game has no \"" + car.Name + "\"."); return false; }
                v.Spawn(at, Quaternion.LookRotation(fwd, Vector3.up));
                ItemMenu.Notify("Spawned a " + car.Name + " in front of you.");
                return true;
            }
            catch (Exception e)
            {
                ItemMenu.Notify("Couldn't spawn the " + car.Name + ": " + e.Message);
                return false;
            }
        }

        /// <summary>The tile's picture: the car's side view in blocks.</summary>
        public static void Picture(Car car, Rect r)
        {
            var dark = new Color(0.06f, 0.05f, 0.08f);
            var glass = new Color(0.62f, 0.78f, 0.9f, 0.9f);
            Paint.Fill(r, new Color(0.13f, 0.11f, 0.18f));
            Paint.Fill(new Rect(r.x, r.y + r.height * 0.86f, r.width, r.height * 0.14f), new Color(0.2f, 0.2f, 0.22f));  // road
            float w = r.width, h = r.height, x = r.x, y = r.y;
            float bodyTop = h * 0.5f, bodyH = h * 0.24f, cabX = 0.22f, cabW = 0.5f, cabTop = h * 0.3f;
            switch (car.Kind)
            {
                case "van": bodyTop = h * 0.3f; bodyH = h * 0.44f; cabX = 0.06f; cabW = 0.88f; cabTop = h * 0.3f; break;
                case "pickup": cabX = 0.14f; cabW = 0.34f; break;
                case "suv": bodyTop = h * 0.44f; bodyH = h * 0.3f; cabX = 0.12f; cabW = 0.7f; cabTop = h * 0.24f; break;
                case "sports": bodyTop = h * 0.56f; bodyH = h * 0.18f; cabX = 0.3f; cabW = 0.4f; cabTop = h * 0.42f; break;
            }
            float left = x + w * 0.06f, width = w * 0.88f;
            Paint.Fill(new Rect(left + width * cabX, y + cabTop, width * cabW, bodyTop - cabTop + 1), car.Color);  // cabin
            if (car.Kind != "van")
                Paint.Fill(new Rect(left + width * cabX + 4, y + cabTop + 3, width * cabW - 8, (bodyTop - cabTop) * 0.7f), glass);
            else
                Paint.Fill(new Rect(left + width * 0.08f, y + cabTop + 4, width * 0.2f, (bodyTop - cabTop) * 0.3f + h * 0.06f), glass);
            Paint.Fill(new Rect(left, y + bodyTop, width, bodyH), car.Color);  // body
            Paint.Fill(new Rect(left, y + bodyTop + bodyH * 0.55f, width, 2), new Color(0, 0, 0, 0.25f));
            float wheel = h * 0.2f;
            foreach (var wx in new[] { 0.2f, 0.8f })
            {
                Paint.Fill(new Rect(left + width * wx - wheel / 2, y + bodyTop + bodyH - wheel * 0.45f, wheel, wheel), dark);
                Paint.Fill(new Rect(left + width * wx - wheel / 5, y + bodyTop + bodyH - wheel * 0.45f + wheel * 0.3f, wheel * 0.4f, wheel * 0.4f), new Color(0.6f, 0.6f, 0.65f));
            }
            Paint.Fill(new Rect(left + width - 5, y + bodyTop + 3, 5, bodyH * 0.3f), new Color(1f, 0.95f, 0.7f));  // headlight
        }
    }
}
