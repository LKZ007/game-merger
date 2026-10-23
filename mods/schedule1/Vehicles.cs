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
            var rot = Quaternion.LookRotation(fwd, Vector3.up);
            var at = Spot(me.Position, fwd, rot);
            try
            {
                var v = S1API.Vehicles.VehicleRegistry.CreateVehicle(car.Code);
                if (v == null) { ItemMenu.Notify("This game has no \"" + car.Name + "\"."); return false; }
                v.Spawn(at, rot);
                // yours, so you can get in and drive it (the game only lets you drive cars you own)
                try { v.IsPlayerOwned = true; } catch (Exception e) { Melon<Plugin>.Logger.Warning("Vehicles: couldn't make it yours: " + e.Message); }
                ItemMenu.Notify("Your " + car.Name + " is in front of you. Walk up to it and get in (E, or the controller's interact button).");
                return true;
            }
            catch (Exception e)
            {
                ItemMenu.Notify("Couldn't spawn the " + car.Name + ": " + e.Message);
                return false;
            }
        }

        // ---------- getting in: a prompt by every car, and the controller's way in ----------
        static object _near;
        static float _nextNear;
        static string _how = "";

        /// <summary>The nearest car within 4 metres (you can get into it with E, the game's own interact key).</summary>
        public static void Update()
        {
            var me = Player.Local;
            if (me == null) { _near = null; return; }
            if (Time.unscaledTime >= _nextNear)
            {
                _nextNear = Time.unscaledTime + 0.75f;
                _near = null;
                if (!me.IsInVehicle)
                {
                    var t = Rt.FindByName("LandVehicle");
                    float best = 4f;
                    foreach (var v in Rt.All(t))
                    {
                        var c = v as Component;
                        if (c == null) continue;
                        float d = Vector3.Distance(c.transform.position, me.Position);
                        if (d < best) { best = d; _near = v; }
                    }
                }
            }
            // controller: X by a car gets you in, the same as E
            if (_near != null && (Pad.Pressed & Pad.X) != 0 && !ItemMenu.IsOpen && !Wheel.Open) GetIn();
        }

        /// <summary>Gets into the nearest car the way the game does when you press E on it.</summary>
        public static void GetIn()
        {
            if (_near == null) { ItemMenu.Notify("No car close enough. Walk up to one."); return; }
            foreach (var name in new[] { "EnterVehicle", "Interacted", "PlayerEnter", "Enter" })
                if (Call(_near, name)) { Say("got in with LandVehicle." + name); return; }
            // the car's own interact point (what E presses)
            var go = Rt.GameObjectOf(_near);
            foreach (var io in go != null ? Rt.Comps(go, Rt.FindByName("InteractableObject"), true) : new List<object>())
                foreach (var name in new[] { "StartInteract", "Interacted", "Interact" })
                    if (Call(io, name)) { Say("got in with InteractableObject." + name); return; }
            ItemMenu.Notify("Press E by the car to get in (this build doesn't let the controller do it for you).");
        }

        static void Say(string how) { if (_how != how) { _how = how; Melon<Plugin>.Logger.Msg("Vehicles: " + how); } }

        static bool Call(object o, string name)
        {
            var m = o?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == 0);
            if (m == null) return false;
            try { m.Invoke(o, null); return true; } catch (Exception) { return false; }
        }

        public static void Draw()
        {
            var me = Player.Local;
            if (me == null) return;
            bool inCar;
            try { inCar = me.IsInVehicle; } catch (Exception) { return; }
            string text = inCar ? (Pad.InUse ? "Get out: the game's interact button" : "Get out: E") : _near != null ? (Pad.InUse ? "(X) Get in the car" : "E: get in the car") : "";
            if (text.Length > 0) GUI.Box(new Rect(Screen.width / 2f - 130, Screen.height * 0.62f, 260, 28), text);
        }

        static MethodInfo _line, _box, _ray;

        /// <summary>A clear spot for the car in front of you: nothing in the way from your eyes to it, no wall or
        /// object where the car would stand, and on the ground there.</summary>
        static Vector3 Spot(Vector3 me, Vector3 fwd, Quaternion rot)
        {
            var phys = Rt.Find("UnityEngine.Physics");
            var ms = phys?.GetMethods(BindingFlags.Public | BindingFlags.Static);
            if (ms != null && _line == null)
            {
                _line = ms.FirstOrDefault(m => m.Name == "Linecast" && m.GetParameters().Length == 2);
                _box = ms.FirstOrDefault(m => m.Name == "CheckBox" && m.GetParameters().Length == 3);
                _ray = ms.FirstOrDefault(m => m.Name == "Raycast" && m.GetParameters().Length == 4 && m.GetParameters()[2].ParameterType.IsByRef);
            }
            var right = Vector3.Cross(Vector3.up, fwd);
            var eye = me + Vector3.up * 1.6f;
            foreach (var d in new[] { 7f, 10f, 5f, 13f })
                foreach (var side in new[] { 0f, 4f, -4f })
                {
                    var p = me + fwd * d + right * side;
                    try
                    {
                        if (_line != null && (bool)_line.Invoke(null, new object[] { eye, p + Vector3.up * 1f })) continue;
                        if (_box != null && (bool)_box.Invoke(null, new object[] { p + Vector3.up * 1.4f, new Vector3(1.3f, 0.9f, 2.7f), rot })) continue;
                        return Ground(p) + Vector3.up * 0.8f;
                    }
                    catch (Exception) { return me + fwd * 7f + Vector3.up * 1f; }
                }
            return me + fwd * 7f + Vector3.up * 1.5f;  // nowhere clear: a little higher, it drops onto whatever's there
        }

        static Vector3 Ground(Vector3 p)
        {
            if (_ray == null) return p;
            var args = new object[] { p + Vector3.up * 6f, Vector3.down, null, 20f };
            try
            {
                if ((bool)_ray.Invoke(null, args) && Rt.Get(args[2], "point") is Vector3 hit) return hit;
            }
            catch (Exception) { }
            return p;
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
