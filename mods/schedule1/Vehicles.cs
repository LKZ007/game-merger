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
            public Garage.Style Style;  // one of Frankenstein's own designs, built on this game car
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
                var inst = Rt.Instance(mgr)
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
            // Frankenstein's own designs, on the game cars they're built on
            foreach (var st in Garage.Styles)
                if (cars.Any(c => c.Code == st.Base)) cars.Add(Garage.Car(st));
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
                if (car.Style != null) Garage.Restyle(v, car.Style);
                ItemMenu.Notify("Your " + car.Name + " is in front of you. Walk up to its door: X (or E) gets you in.");
                return true;
            }
            catch (Exception e)
            {
                ItemMenu.Notify("Couldn't spawn the " + car.Name + ": " + e.Message);
                return false;
            }
        }

        // ---------- getting in and out: a prompt by every car, and the controller's way in ----------
        static Component _near;
        static string _nearName = "";
        static float _nextNear, _checkAt;
        static int _try = -1;
        static bool _wantIn, _debugged;
        static readonly List<string> _tried = new List<string>();

        /// <summary>Every car in the town: S1API's list (it knows the game's vehicle manager), or a search.</summary>
        static List<Component> Cars()
        {
            var list = new List<Component>();
            try
            {
                foreach (var w in S1API.Vehicles.VehicleRegistry.GetAll())
                    if (Rt.Get(w, "S1LandVehicle") is Component c && c != null) list.Add(c);
            }
            catch (Exception) { }
            if (list.Count == 0) list.AddRange(Rt.All(Rt.FindByName("LandVehicle")).OfType<Component>().Where(c => c != null));
            return list;
        }

        /// <summary>The car you're in (or next to): the nearest within reach.</summary>
        public static Component Nearest(Vector3 me, float reach)
        {
            Component best = null;
            float bestGap = reach;
            foreach (var c in Cars())
            {
                float g = Gap(c, me);
                if (g < bestGap) { bestGap = g; best = c; }
            }
            return best;
        }

        /// <summary>How far you are from the car's side (not its middle: a car is long).</summary>
        static float Gap(Component car, Vector3 me)
        {
            var local = car.transform.InverseTransformPoint(me);
            var dx = Mathf.Max(0, Mathf.Abs(local.x) - 1f);
            var dz = Mathf.Max(0, Mathf.Abs(local.z) - 2.3f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static void Update()
        {
            var me = Player.Local;
            if (me == null) { _near = null; return; }
            bool inCar;
            try { inCar = me.IsInVehicle; } catch (Exception) { return; }
            if (Time.unscaledTime >= _nextNear)
            {
                _nextNear = Time.unscaledTime + Mathf.Min(Perf.Every(0.4f), 0.8f);
                _near = null;
                float best = inCar ? 3f : 2.2f;
                var cars = Cars();
                foreach (var c in cars)
                {
                    float d = Gap(c, me.Position);
                    if (d < best) { best = d; _near = c; }
                }
                _nearName = _near != null ? NameOf(_near) : "";
                if (_near != null && !_debugged) Debug(_near, cars.Count);
            }
            // a try that didn't put you in the car: the next way
            if (_wantIn && Time.unscaledTime >= _checkAt)
            {
                if (inCar) { _wantIn = false; Melon<Plugin>.Logger.Msg("Vehicles: in the car with " + _tried.LastOrDefault()); }
                else if (!NextTry()) { _wantIn = false; ItemMenu.Notify("Couldn't get in with the controller here: press E (the game's own button). vehicles-debug.txt has the details."); }
            }
            if ((Pad.Pressed & Pad.X) == 0 || ItemMenu.IsOpen || Wheel.Open) return;
            if (inCar) GetOut();
            else if (_near != null) GetIn();
        }

        static string NameOf(Component car)
        {
            var code = Rt.Get(car, "VehicleCode", "vehicleCode") as string ?? "";
            var style = Garage.StyleOf(car);
            if (style != null) return style.Name;
            var name = Rt.Get(car, "VehicleName", "vehicleName") as string;
            return string.IsNullOrEmpty(name) ? (code.Length > 0 ? char.ToUpper(code[0]) + code.Substring(1) : "car") : name;
        }

        /// <summary>Gets into the nearest car the way the game does when you press E on it: the car's interact
        /// point first (what E presses), then the car's own enter calls. Each is checked a moment later; if you're
        /// not in, the next is tried.</summary>
        public static void GetInOrOut()
        {
            bool inCar = false;
            try { inCar = Player.Local != null && Player.Local.IsInVehicle; } catch (Exception) { }
            if (inCar) GetOut(); else GetIn();
        }

        public static void GetIn()
        {
            if (_near == null) { ItemMenu.Notify("No car close enough. Walk up to its door."); return; }
            _tried.Clear();
            _try = -1;
            _wantIn = true;
            if (!NextTry()) { _wantIn = false; ItemMenu.Notify("Press E by the car to get in."); }
        }

        static bool NextTry()
        {
            if (_near == null) return false;
            var go = _near.gameObject;
            var ways = new List<Func<bool>>();
            var ioType = Rt.FindByName("InteractableObject");
            foreach (var io in Rt.Comps(go, ioType, true))
            {
                var o = io;
                ways.Add(() => Call(o, "StartInteract", "InteractableObject.StartInteract"));
                ways.Add(() => Fire(Rt.Get(o, "onInteractStart"), "InteractableObject.onInteractStart"));
            }
            foreach (var n in new[] { "Interacted", "EnterVehicle", "PlayerEnter", "Enter", "Interact" })
            {
                var name = n;
                ways.Add(() => Call(_near, name, "LandVehicle." + name));
            }
            var gamePlayer = Rt.Get(Player.Local, "S1Player");
            ways.Add(() => Call1(gamePlayer, "EnterVehicle", _near, "Player.EnterVehicle"));
            while (++_try < ways.Count)
                if (ways[_try]()) { _checkAt = Time.unscaledTime + 0.6f; return true; }
            return false;
        }

        /// <summary>Out of the car: the car's own exit, as the game's button does it.</summary>
        public static void GetOut()
        {
            var car = _near;
            var gamePlayer = Rt.Get(Player.Local, "S1Player");
            if (car != null)
                foreach (var n in new[] { "ExitVehicle", "Exit", "PlayerExit" })
                    if (Call(car, n, "LandVehicle." + n)) return;
            if (Call(gamePlayer, "ExitVehicle", "Player.ExitVehicle")) return;
            ItemMenu.Notify("Press E to get out (the game's own button).");
        }

        static bool Call(object o, string name, string label)
        {
            var m = o?.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault(x => x.Name == name && x.GetParameters().All(p => p.IsOptional));
            if (m == null) return false;
            try
            {
                m.Invoke(o, m.GetParameters().Select(p => p.DefaultValue).ToArray());
                _tried.Add(label);
                return true;
            }
            catch (Exception e) { _tried.Add(label + " failed: " + (e.InnerException ?? e).Message); return false; }
        }

        static bool Call1(object o, string name, object arg, string label)
        {
            var m = o?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => x.Name == name && x.GetParameters().Length >= 1 && x.GetParameters()[0].ParameterType.IsInstanceOfType(arg) &&
                                     x.GetParameters().Skip(1).All(p => p.IsOptional));
            if (m == null) return false;
            try
            {
                m.Invoke(o, new[] { arg }.Concat(m.GetParameters().Skip(1).Select(p => p.DefaultValue)).ToArray());
                _tried.Add(label);
                return true;
            }
            catch (Exception e) { _tried.Add(label + " failed: " + (e.InnerException ?? e).Message); return false; }
        }

        /// <summary>A UnityEvent's Invoke(), for the interact point's own "pressed" event.</summary>
        static bool Fire(object ev, string label) => ev != null && Call(ev, "Invoke", label);

        /// <summary>What the game's cars can be called with on this build, once, for fixing it from levy's log.</summary>
        static void Debug(Component car, int count)
        {
            _debugged = true;
            try
            {
                var lines = new List<string> { "Frankenstein " + Plugin.Version + ": " + count + " car(s) found; nearest " + car.name };
                Func<object, string, string> Methods = (o, what) => what + ": " + (o == null ? "none" : string.Join(", ",
                    o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(m => new[] { "Enter", "Exit", "Interact", "Seat", "Drive" }.Any(w => m.Name.Contains(w)))
                        .Select(m => m.Name + "(" + m.GetParameters().Length + ")").Distinct().ToArray()));
                lines.Add(Methods(car, "LandVehicle"));
                foreach (var io in Rt.Comps(car.gameObject, Rt.FindByName("InteractableObject"), true).Take(3)) lines.Add(Methods(io, "InteractableObject"));
                lines.Add(Methods(Rt.Get(Player.Local, "S1Player"), "Player"));
                var dir = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Frankenstein");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllLines(System.IO.Path.Combine(dir, "vehicles-debug.txt"), lines.ToArray());
            }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Vehicles: debug file: " + e.Message); }
        }

        static GUIStyle _promptStyle;

        /// <summary>The prompt by a car (and while driving): which button gets you in or out.</summary>
        public static void Draw()
        {
            var me = Player.Local;
            if (me == null || Plugin.MenuUp) return;
            bool inCar;
            try { inCar = me.IsInVehicle; } catch (Exception) { return; }
            if (!inCar && _near == null) return;
            string text = inCar ? "to Exit Vehicle" : "to Enter Vehicle  ·  " + _nearName;
            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = false, clipping = TextClipping.Overflow };
                _promptStyle.normal.textColor = Color.white;
            }
            float tw = 11f * text.Length;  // the box fits the car's name (it was cut off at "Get in the")
            try { tw = _promptStyle.CalcSize(new GUIContent(text)).x; } catch (Exception) { }
            float w = Mathf.Max(300, tw + 110), h = 44, x = Screen.width / 2f - w / 2, y = Screen.height * 0.7f;
            if (inCar) y = Screen.height - 70;
            Paint.Fill(new Rect(x, y, w, h), new Color(0.07f, 0.04f, 0.12f, 0.82f));
            Paint.Fill(new Rect(x, y, 4, h), new Color(0.62f, 0.36f, 1f));
            // the X button (blue, like on the controller), then E for the keyboard
            Paint.Fill(new Rect(x + 14, y + 9, 26, 26), new Color(0.18f, 0.42f, 0.95f));
            GUI.Label(new Rect(x + 21, y + 9, 26, 26), "X", _promptStyle);
            Paint.Fill(new Rect(x + 46, y + 9, 26, 26), new Color(0.85f, 0.85f, 0.9f));
            var dark = new GUIStyle(_promptStyle);
            dark.normal.textColor = new Color(0.1f, 0.1f, 0.12f);
            GUI.Label(new Rect(x + 53, y + 9, 26, 26), "E", dark);
            GUI.Label(new Rect(x + 84, y + 10, w - 84, 28), text, _promptStyle);
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
                case "super": bodyTop = h * 0.6f; bodyH = h * 0.16f; cabX = 0.34f; cabW = 0.34f; cabTop = h * 0.47f; break;
                case "gwagon": bodyTop = h * 0.42f; bodyH = h * 0.32f; cabX = 0.1f; cabW = 0.74f; cabTop = h * 0.16f; break;
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
            var trim = car.Style != null ? car.Style.Trim : dark;
            if (car.Kind == "super")
            {
                // wedge nose, rear wing on posts, side intake, tail light
                Paint.Fill(new Rect(left + width * 0.72f, y + bodyTop - h * 0.04f, width * 0.2f, h * 0.04f), car.Color);
                Paint.Fill(new Rect(left + width * 0.04f, y + bodyTop - h * 0.14f, 3, h * 0.14f), trim);
                Paint.Fill(new Rect(left - w * 0.02f, y + bodyTop - h * 0.17f, width * 0.2f, h * 0.04f), trim);
                Paint.Fill(new Rect(left + width * 0.3f, y + bodyTop + bodyH * 0.2f, width * 0.12f, bodyH * 0.35f), new Color(0, 0, 0, 0.55f));
                Paint.Fill(new Rect(left, y + bodyTop + 2, 4, bodyH * 0.3f), new Color(1f, 0.1f, 0.06f));
                Paint.Fill(new Rect(left + width * 0.05f, y + bodyTop + bodyH - 3, width * 0.9f, 3), trim);  // skirt
            }
            else if (car.Kind == "gwagon")
            {
                // spare wheel on the back, roof light bar, round headlight, step bar
                Paint.Fill(new Rect(left - w * 0.05f, y + bodyTop - h * 0.06f, w * 0.06f, h * 0.24f), dark);
                Paint.Fill(new Rect(left + width * 0.2f, y + cabTop - h * 0.06f, width * 0.5f, h * 0.05f), dark);
                Paint.Fill(new Rect(left + width * 0.22f, y + cabTop - h * 0.04f, width * 0.46f, h * 0.02f), new Color(0.95f, 0.97f, 1f));
                Paint.Fill(new Rect(left + width - 8, y + bodyTop + bodyH * 0.15f, 8, 8), new Color(0.95f, 0.97f, 1f));
                Paint.Fill(new Rect(left + width * 0.25f, y + bodyTop + bodyH - 2, width * 0.5f, 4), trim);
                for (int i = 0; i < 3; i++) Paint.Fill(new Rect(left + width - 3, y + bodyTop + bodyH * (0.45f + i * 0.12f), 3, 2), trim);
            }
        }
    }
}
