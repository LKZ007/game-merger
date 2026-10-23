using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Cartel;
using S1API.Entities;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>The Extension game's gangs, each running its own district of Schedule I's town (F9 in game).
    /// Rival dealers stand on their turf with guards: knock them out and rob them (G) for cash and product.
    /// Join a gang through its initiation and climb its ranks to the top, or start your own gang: recruit a crew,
    /// pick drugs, guns or both, take districts in turf wars, and trade product and guns with the other gangs.
    /// Each gang's headquarters is built in its district the first time you go there. The fighters are the
    /// game's own cartel goons, so they look like Schedule I's people. Everything is kept in
    /// UserData/Frankenstein/gangs.json (gangs-creative.json in Creative).</summary>
    internal static class Gangs
    {
        class Gang
        {
            public string Id, Name, Region, Weapon, Rival, Product, Hex, District;
            public Color Color;
            public List<string> Ranks = new List<string>();
            public List<string> Guns = new List<string>();
            public double DrugPrice = 1, GunPrice = 1;
        }

        class Fighter
        {
            public CartelGoon Goon;
            public string Gang;
            public bool Dealer, Robbed, Counted, Scored;
            public float Since;
        }

        // ---------- what's saved ----------
        static string _member = "", _ownName = "", _specialty = "both";
        static int _rank, _rep, _crew, _bricks, _crates, _initStep, _lastDay = -1;
        static readonly List<string> _turf = new List<string>();         // districts you took for your own gang
        static readonly Dictionary<string, int> _heat = new Dictionary<string, int>();
        static readonly Dictionary<string, Vector3> _hq = new Dictionary<string, Vector3>();
        static string _initGang = "";

        static readonly List<Gang> _gangs = new List<Gang>();
        static readonly List<Fighter> _fighters = new List<Fighter>();
        static readonly List<GameObject> _built = new List<GameObject>();
        static readonly int[] RankRep = { 0, 100, 250, 500, 900, 1500 };
        static bool _open, _inTown;
        static string _note = "", _region = "", _nameField = "";
        static float _next, _nextDealer, _nextHit, _warLeftAt = -1, _built_at = -1;
        static int _tab;
        // a turf war in progress
        static string _war = "";
        static int _wave;

        static string File_ => Mode.DataFile("gangs");  // one per save

        public static bool On => _gangs.Count > 0;

        // ---------- for the gangs' own people (Crew.cs) ----------
        public static string MemberOf => _member;
        public static int HeatOf(string id) { int h; return _heat.TryGetValue(id, out h) ? h : 0; }
        public static bool HqOf(string id, out Vector3 at) => _hq.TryGetValue(id, out at);
        public static string RegionOf(string id) => G(id)?.Region ?? "";
        public static string NameOf(string id) => G(id)?.Name ?? id;
        public static List<string> Ids => _gangs.Select(g => g.Id).ToList();
        public static string NewDistrictOf(string id) => G(id)?.District ?? "";
        public static string RivalOf(string id) => G(id)?.Rival ?? "";
        public static string WeaponOf(string id) => G(id)?.Weapon ?? "";
        public static Color ColorOf(string id) => G(id)?.Color ?? Color.gray;
        public static bool Runs(string id) => G(id) != null && !_turf.Contains(G(id).Region);
        public static void Reward(int rep, float cash, string why)
        {
            if (cash > 0) Cash(cash);
            if (rep > 0 && _member.Length > 0) Rep(rep, "");
            Say(why);
        }

        public static void Read(object list)
        {
            foreach (var o in J.L(new Dictionary<string, object> { { "g", list } }, "g"))
            {
                var g = new Gang
                {
                    Id = J.S(o, "id"), Name = J.S(o, "name"), Region = J.S(o, "region"), Weapon = J.S(o, "weapon"),
                    Rival = J.S(o, "rival"), District = J.S(o, "district"), Product = J.S(o, "product", "OG Kush"), Hex = J.S(o, "color", "#888888"),
                    DrugPrice = J.N(o, "drug_price", 1), GunPrice = J.N(o, "gun_price", 1),
                };
                foreach (var r in J.L(o, "ranks")) g.Ranks.Add(r.ToString());
                foreach (var r in J.L(o, "guns")) g.Guns.Add(r.ToString());
                g.Color = Hex(g.Hex);
                if (g.Id.Length > 0 && g.Region.Length > 0) _gangs.Add(g);
            }
        }

        static Color Hex(string h)
        {
            try { return new Color(Convert.ToInt32(h.Substring(1, 2), 16) / 255f, Convert.ToInt32(h.Substring(3, 2), 16) / 255f, Convert.ToInt32(h.Substring(5, 2), 16) / 255f); }
            catch (Exception) { return Color.gray; }
        }

        static Gang G(string id) => _gangs.FirstOrDefault(g => g.Id == id);
        static Gang OwnerGang(string region) => _turf.Contains(region) ? null : _gangs.FirstOrDefault(g => g.Region == region);
        static string Rank(Gang g, int r) => g.Ranks.Count > 0 ? g.Ranks[Math.Min(r, g.Ranks.Count - 1)] : "Rank " + (r + 1);

        // ---------- save and load ----------
        static void Load()
        {
            // each save has its own standing with the gangs: start clean, then read this save's
            _member = ""; _rank = 0; _rep = 0; _ownName = ""; _specialty = "both"; _crew = 0; _bricks = 0; _crates = 0;
            _initGang = ""; _initStep = 0; _lastDay = -1;
            _turf.Clear(); _heat.Clear(); _hq.Clear();
            try
            {
                if (!File.Exists(File_)) return;
                var s = MiniJson.Parse(File.ReadAllText(File_));
                _member = J.S(s, "member"); _rank = (int)J.N(s, "rank"); _rep = (int)J.N(s, "rep");
                _ownName = J.S(s, "own"); _specialty = J.S(s, "specialty", "both"); _crew = (int)J.N(s, "crew");
                _bricks = (int)J.N(s, "bricks"); _crates = (int)J.N(s, "crates");
                _initGang = J.S(s, "init_gang"); _initStep = (int)J.N(s, "init_step"); _lastDay = (int)J.N(s, "last_day", -1);
                _turf.Clear();
                foreach (var t in J.L(s, "turf")) _turf.Add(t.ToString());
                var heat = J.O(s, "heat") as Dictionary<string, object>;
                if (heat != null) foreach (var kv in heat) _heat[kv.Key] = (int)J.N(heat, kv.Key);
                var hq = J.O(s, "hq") as Dictionary<string, object>;
                if (hq != null)
                    foreach (var kv in hq)
                        _hq[kv.Key] = new Vector3((float)J.N(kv.Value, "x"), (float)J.N(kv.Value, "y"), (float)J.N(kv.Value, "z"));
            }
            catch (Exception e) { Warn(e); }
        }

        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string F(float f) => f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        static void Save()
        {
            try
            {
                var b = new StringBuilder("{");
                b.Append("\"member\": ").Append(Q(_member)).Append(", \"rank\": ").Append(_rank).Append(", \"rep\": ").Append(_rep);
                b.Append(", \"own\": ").Append(Q(_ownName)).Append(", \"specialty\": ").Append(Q(_specialty)).Append(", \"crew\": ").Append(_crew);
                b.Append(", \"bricks\": ").Append(_bricks).Append(", \"crates\": ").Append(_crates);
                b.Append(", \"init_gang\": ").Append(Q(_initGang)).Append(", \"init_step\": ").Append(_initStep).Append(", \"last_day\": ").Append(_lastDay);
                b.Append(", \"turf\": [").Append(string.Join(", ", _turf.Select(Q).ToArray())).Append("]");
                b.Append(", \"heat\": {").Append(string.Join(", ", _heat.Select(kv => Q(kv.Key) + ": " + kv.Value).ToArray())).Append("}");
                b.Append(", \"hq\": {").Append(string.Join(", ", _hq.Select(kv => Q(kv.Key) + ": {\"x\": " + F(kv.Value.x) + ", \"y\": " + F(kv.Value.y) + ", \"z\": " + F(kv.Value.z) + "}").ToArray())).Append("}");
                b.Append("}");
                File.WriteAllText(File_, b.ToString());
            }
            catch (Exception e) { Warn(e); }
        }

        public static void Scene(bool inTown)
        {
            _inTown = inTown;
            _fighters.Clear();
            _built.Clear();
            _war = "";
            if (!inTown || !On) return;
            Load();
            _built_at = Time.unscaledTime + 15f;  // the town needs a moment before its props exist
            _nextDealer = Time.unscaledTime + 40f;
        }

        static void Warn(Exception e) => Melon<Plugin>.Logger.Warning("Gangs: " + e.Message);
        static void Say(string s) { _note = s; ItemMenu.Notify(s); }

        static void Cash(float amount)
        {
            try { S1API.Money.Money.ChangeCashBalance(amount, true, true); } catch (Exception e) { Warn(e); }
        }

        static float CashNow()
        {
            try { return S1API.Money.Money.GetCashBalance(); } catch (Exception) { return 0; }
        }

        static void Give(string name, int qty)
        {
            var id = Story.ItemId(name);
            if (id == null) return;
            try { S1API.Console.ConsoleHelper.AddItemToInventory(id, qty); } catch (Exception e) { Warn(e); }
        }

        // ---------- fighters: the game's own cartel goons ----------
        static CartelGoon Spawn(Vector3 at, Gang g)
        {
            var pool = Cartel.Instance?.GoonPool;
            if (pool == null || pool.AvailableGoonCount <= 0) return null;
            var goon = pool.SpawnGoon(at);
            if (goon == null) return null;
            try { if (!string.IsNullOrEmpty(g?.Weapon)) goon.SetDefaultWeapon(g.Weapon); } catch (Exception) { }
            return goon;
        }

        static Vector3 Near(Vector3 me, float min, float max)
        {
            var a = UnityEngine.Random.Range(0f, 6.283f);
            var d = UnityEngine.Random.Range(min, max);
            return me + new Vector3((float)Math.Cos(a) * d, 0.5f, (float)Math.Sin(a) * d);
        }

        static int Squad(Gang g, Vector3 me, int count, bool dealer, bool attack)
        {
            int made = 0;
            var spot = Near(me, 22, 34);
            for (int i = 0; i < count; i++)
            {
                var goon = Spawn(spot + new Vector3(i * 1.6f, 0, (i % 2) * 1.6f), g);
                if (goon == null) break;
                _fighters.Add(new Fighter { Goon = goon, Gang = g.Id, Dealer = dealer && i == 0, Since = Time.unscaledTime });
                if (attack) try { goon.AttackPlayer(); } catch (Exception) { }
                made++;
            }
            return made;
        }

        static bool Down(Fighter f)
        {
            try { return f.Goon == null || !f.Goon.IsSpawned || !f.Goon.IsConscious; } catch (Exception) { return true; }
        }

        // ---------- every second ----------
        public static void Update()
        {
            if (!On || !_inTown) return;
            if (Input.GetKeyDown(KeyCode.F9)) _open = !_open;
            if (Input.GetKeyDown(KeyCode.G)) TryRob();
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Perf.Every(1f);
            var me = Player.Local;
            if (me == null) return;
            try { _region = me.CurrentRegion.ToString(); } catch (Exception) { _region = ""; }
            if (_built_at > 0 && Time.unscaledTime >= _built_at) { _built_at = -1; foreach (var kv in _hq) BuildHq(G(kv.Key), kv.Value); }
            var owner = OwnerGang(_region);
            // a gang's headquarters goes up the first time you walk into its district
            if (owner != null && !_hq.ContainsKey(owner.Id) && _built_at < 0)
            {
                var spot = me.Position + Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0) * new Vector3(0, 0, 30);
                spot.y = me.Position.y;
                _hq[owner.Id] = spot;
                BuildHq(owner, spot);
                Save();
                Say(owner.Name + " run " + _region + ". Their HQ is marked on your screen.");
            }
            Fights(me.Position);
            War(me.Position);
            Pay();
            // rival dealers on rival turf
            if (owner != null && owner.Id != _member && _war.Length == 0 && Time.unscaledTime >= _nextDealer &&
                !_fighters.Any(f => f.Dealer && !f.Robbed))
            {
                _nextDealer = Time.unscaledTime + UnityEngine.Random.Range(120f, 200f);
                int guards = Math.Min(3, 1 + Heat(owner.Id) / 2);
                if (Squad(owner, me.Position, 1 + guards, true, false) > 0)
                    Say("A " + owner.Name + " dealer is working this corner. Knock them out and press G to rob them.");
            }
            // gangs you robbed send people after you on their turf
            if (owner != null && owner.Id != _member && Heat(owner.Id) >= 3 && Time.unscaledTime >= _nextHit)
            {
                _nextHit = Time.unscaledTime + 240f;
                if (Squad(owner, me.Position, 2 + Math.Min(3, Heat(owner.Id) / 3), false, true) > 0)
                    Say(owner.Name + " are coming for you: you've robbed them " + Heat(owner.Id) + " times.");
            }
        }

        static int Heat(string id) => _heat.TryGetValue(id, out var h) ? h : 0;

        static void Fights(Vector3 me)
        {
            foreach (var f in _fighters.ToList())
            {
                bool down = Down(f);
                // guards fight when you walk up to their dealer, unless you're one of them
                if (!down && !f.Dealer && f.Gang != _member && f.Goon != null && Vector3.Distance(f.Goon.Position, me) < 14 && !f.Counted)
                {
                    f.Counted = true;
                    try { f.Goon.AttackPlayer(); } catch (Exception) { }
                }
                if (down && !f.Dealer && !f.Scored)
                {
                    f.Scored = true;
                    Rep(5, null);
                }
                if (Time.unscaledTime - f.Since > 600 || (f.Robbed && Time.unscaledTime - f.Since > 30))
                {
                    try { f.Goon?.Despawn(); } catch (Exception) { }
                    _fighters.Remove(f);
                }
            }
        }

        public static void Toggle() { if (On && _inTown) _open = !_open; }
        public static void Close() => _open = false;

        public static void TryRob()
        {
            var me = Player.Local;
            if (me == null) return;
            var f = _fighters.FirstOrDefault(x => x.Dealer && !x.Robbed && Down(x) && x.Goon != null &&
                                                   Vector3.Distance(x.Goon.Position, me.Position) < 4);
            if (f == null) return;
            f.Robbed = true;
            f.Since = Time.unscaledTime;
            var g = G(f.Gang);
            int cash = UnityEngine.Random.Range(250, 900) + 120 * _rank;
            Cash(cash);
            int qty = UnityEngine.Random.Range(1, 4);
            Give(g?.Product ?? "OG Kush", qty);
            if (UnityEngine.Random.value < 0.35f && g != null && g.Guns.Count > 0) Give(g.Guns[0], 1);
            _heat[f.Gang] = Heat(f.Gang) + 1;
            Rep(20, f.Gang);
            if (_initGang.Length > 0 && _initStep == 0 && G(_initGang)?.Rival == f.Gang) { _initStep = 1; Say("Initiation: dealer robbed. Now bring the $500 tribute (F9)."); }
            else Say("Robbed the " + (g?.Name ?? "rival") + " dealer: +$" + cash + " and " + qty + " " + (g?.Product ?? "product") + ".");
            Save();
        }

        static void Rep(int points, string robbedGang)
        {
            if (_member.Length == 0) return;
            var mine = G(_member);
            // robbing your own gang costs you
            if (robbedGang == _member) { _rep = Math.Max(0, _rep - 50); Say("You robbed your own people. -50 rep."); Save(); return; }
            _rep += points;
            while (_rank < RankRep.Length - 1 && _rep >= RankRep[_rank + 1])
            {
                _rank++;
                Say("Promoted: you're now " + Rank(mine, _rank) + " of the " + mine.Name + ".");
            }
            Save();
        }

        // ---------- turf wars: three waves, then the district is yours ----------
        static void StartWar()
        {
            var owner = OwnerGang(_region);
            if (owner == null) { Say("Nobody runs " + _region + " for you to take."); return; }
            if (_crew < 2 * (_turf.Count + 1)) { Say("You need a crew of " + 2 * (_turf.Count + 1) + " to hold another district."); return; }
            _war = _region;
            _wave = 0;
            _warLeftAt = -1;
            Say("Turf war for " + _region + " against the " + owner.Name + ". Survive three waves.");
        }

        static void War(Vector3 me)
        {
            if (_war.Length == 0) return;
            if (_region != _war)
            {
                if (_warLeftAt < 0) _warLeftAt = Time.unscaledTime;
                else if (Time.unscaledTime - _warLeftAt > 60) { Say("You left " + _war + ". The turf war is lost."); _war = ""; }
                return;
            }
            _warLeftAt = -1;
            var owner = OwnerGang(_war);
            if (owner == null) { _war = ""; return; }
            if (_fighters.Any(f => f.Gang == owner.Id && !Down(f))) return;
            if (_wave >= 3)
            {
                _turf.Add(_war);
                _crew = Math.Max(0, _crew - 1);
                try { Cartel.Instance?.Influence?.ChangeInfluence((S1API.Map.Region)Enum.Parse(typeof(S1API.Map.Region), _war), -0.3f); } catch (Exception) { }
                Say(_war + " is yours. " + (_ownName.Length > 0 ? _ownName : "Your gang") + " now runs " + _turf.Count + " district" + (_turf.Count == 1 ? "" : "s") + ".");
                _war = "";
                Save();
                return;
            }
            _wave++;
            if (Squad(owner, me, 2 + _wave, false, true) == 0) { Say("No fighters available yet (the game's cartel isn't active in this save)."); _war = ""; }
            else Say("Wave " + _wave + " of 3: " + owner.Name + " incoming.");
        }

        // ---------- your empire pays out once a game day ----------
        static void Pay()
        {
            int day;
            try { day = S1API.GameTime.TimeManager.ElapsedDays; } catch (Exception) { return; }
            if (_lastDay < 0) { _lastDay = day; return; }
            if (day == _lastDay) return;
            _lastDay = day;
            int pay = 0;
            if (_member.Length > 0) pay += 150 * (_rank + 1);
            if (_ownName.Length > 0)
            {
                int turf = _turf.Count;
                if (_specialty != "guns") { pay += 300 * turf + 25 * _crew; _bricks += turf * 2 + _crew / 4; }
                if (_specialty != "drugs") { pay += 250 * turf + 20 * _crew; _crates += turf + _crew / 6; }
                if (_specialty == "both") pay = (int)(pay * 0.8);
            }
            if (pay > 0) { Cash(pay); Say("Your cut for the day: +$" + pay + "."); }
            Save();
        }

        // ---------- headquarters: new places built from the town's own props ----------
        static void BuildHq(Gang g, Vector3 at)
        {
            if (g == null) return;
            var kinds = World.Kinds();
            for (int i = 0; i < 16 && kinds.Count > 0; i++)
            {
                float a = i * 6.283f / 16;
                var copy = UnityEngine.Object.Instantiate(kinds[i % kinds.Count], at + new Vector3((float)Math.Cos(a) * 12, 0, (float)Math.Sin(a) * 12), Quaternion.Euler(0, i * 23, 0));
                copy.name = "Frankenstein " + g.Name + " HQ";
                _built.Add(copy);
            }
            // the gang's flag: a pole with its colour, seen from the street
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pole.transform.position = at + new Vector3(0, 3, 0);
            pole.transform.localScale = new Vector3(0.2f, 6, 0.2f);
            var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            flag.transform.position = at + new Vector3(0.9f, 5.4f, 0);
            flag.transform.localScale = new Vector3(1.6f, 1, 0.05f);
            var r = flag.GetComponent<MeshRenderer>();
            if (r != null && r.material != null) r.material.color = g.Color;
            _built.Add(pole);
            _built.Add(flag);
        }

        // ---------- on screen ----------
        static void Arrow(Vector3 world, string text)
        {
            var cam = Camera.main;
            if (cam == null || Plugin.MenuUp) return;
            var p = cam.WorldToScreenPoint(world + Vector3.up * 2);
            if (p.z < 0) return;
            GUI.Label(new Rect(p.x - 90, Screen.height - p.y - 12, 180, 24), text);
        }

        public static void Draw()
        {
            if (!On || !_inTown) return;
            var me = Player.Local;
            var owner = OwnerGang(_region);
            string who = _turf.Contains(_region) ? "yours" : owner == null ? "nobody's" : owner.Name + (owner.Id == _member ? " (your gang)" : "");
            float x = 16, y = 96, w = 330;
            GUI.Box(new Rect(x, y, w, 50), "");
            GUI.Label(new Rect(x + 8, y + 3, w - 16, 22), "Turf: " + (_region.Length > 0 ? _region : "?") + " · " + who);
            string me_ = _member.Length > 0 ? Rank(G(_member), _rank) + " of the " + G(_member)?.Name + " · " + _rep + " rep"
                       : _ownName.Length > 0 ? _ownName + " · crew " + _crew + " · " + _turf.Count + " districts" : "No gang · F9 to join or start one";
            GUI.Label(new Rect(x + 8, y + 25, w - 16, 22), me_);
            if (me != null)
            {
                foreach (var f in _fighters)
                {
                    if (!f.Dealer || f.Robbed || f.Goon == null) continue;
                    float d = Vector3.Distance(f.Goon.Position, me.Position);
                    Arrow(f.Goon.Position, (Down(f) ? (d < 4 ? "G: rob the dealer" : "Dealer down") : G(f.Gang)?.Name + " dealer") + " · " + (int)d + "m");
                }
                foreach (var kv in _hq)
                    if (Vector3.Distance(kv.Value, me.Position) < 220) Arrow(kv.Value, (G(kv.Key)?.Name ?? kv.Key).ToUpperInvariant() + " HQ");
            }
            if (_war.Length > 0) GUI.Box(new Rect(Screen.width / 2f - 200, 60, 400, 26), "TURF WAR · " + _war + " · wave " + _wave + " of 3");
            if (_open) Panel();
        }

        static void Panel()
        {
            float w = 460, x = Screen.width - w - 24, y = 110, h = 470;
            GUI.Box(new Rect(x, y, w, h), "");
            GUI.Label(new Rect(x + 12, y + 8, w - 24, 22), "GANGS  ·  F9 closes");
            string[] tabs = { "Gangs", "Your gang", "Market" };
            for (int i = 0; i < tabs.Length; i++)
                if (GUI.Toggle(new Rect(x + 12 + i * 146, y + 32, 140, 26), _tab == i, tabs[i], GUI.skin.button)) _tab = i;
            float cy = y + 68;
            if (_tab == 0)
            {
                foreach (var g in _gangs)
                {
                    bool mine = g.Id == _member;
                    string runs = _turf.Contains(g.Region) ? "lost " + g.Region + " to you" : "run " + g.Region;
                    GUI.Label(new Rect(x + 12, cy, w - 150, 22), "<color=" + g.Hex + ">■</color> " + g.Name + " · " + runs + (Heat(g.Id) > 0 ? " · robbed " + Heat(g.Id) + "x" : ""));
                    if (mine) GUI.Label(new Rect(x + w - 130, cy, 118, 22), Rank(g, _rank));
                    else if (_member.Length == 0 && _ownName.Length == 0 && _initGang.Length == 0 && Pad.Button(new Rect(x + w - 130, cy, 118, 24), "Join"))
                    {
                        _initGang = g.Id; _initStep = 0; Save();
                        Say("Initiation for the " + g.Name + ": rob a " + (G(g.Rival)?.Name ?? "rival") + " dealer on their turf.");
                    }
                    cy += 28;
                }
                if (_initGang.Length > 0)
                {
                    var ig = G(_initGang);
                    cy += 8;
                    string step = _initStep == 0 ? "1. Rob a " + (G(ig.Rival)?.Name ?? "rival") + " dealer on " + (G(ig.Rival)?.Region ?? "their") + " turf."
                                : _initStep == 1 ? "2. Bring the $500 tribute." : "3. Survive the " + (G(ig.Rival)?.Name ?? "rival") + " hit squad on " + ig.Region + ".";
                    GUI.Label(new Rect(x + 12, cy, w - 24, 44), "Initiation, " + ig.Name + ": " + step);
                    cy += 46;
                    if (_initStep == 1 && Pad.Button(new Rect(x + 12, cy, 200, 26), "Pay the $500 tribute"))
                    {
                        if (CashNow() < 500) Say("You need $500 cash.");
                        else { Cash(-500); _initStep = 2; Save(); Say("Tribute paid. Now hold " + ig.Region + " against the hit squad."); }
                    }
                    if (_initStep == 2 && _region == ig.Region && Pad.Button(new Rect(x + 12, cy, 200, 26), "Start the fight"))
                    {
                        var rival = G(ig.Rival);
                        var me = Player.Local;
                        if (rival != null && me != null && Squad(rival, me.Position, 3, false, true) > 0) { _initStep = 3; Say("Here they come."); }
                        else Say("No fighters available yet (the game's cartel isn't active in this save).");
                    }
                    if (_initStep == 3 && !_fighters.Any(f => f.Gang == ig.Rival && !Down(f)))
                    {
                        _member = ig.Id; _rank = 0; _rep = 0; _initGang = ""; _initStep = 0; Save();
                        Say("You're in. Welcome to the " + ig.Name + ", " + Rank(ig, 0) + ".");
                    }
                    if (Pad.Button(new Rect(x + w - 130, cy, 118, 26), "Give up")) { _initGang = ""; _initStep = 0; Save(); }
                }
                else if (_member.Length > 0 && Pad.Button(new Rect(x + 12, y + h - 40, 200, 28), "Leave the " + G(_member)?.Name))
                {
                    _heat[_member] = Heat(_member) + 3; _member = ""; _rank = 0; _rep = 0; Save();
                    Say("You left. They won't forget it.");
                }
                return;
            }
            if (_tab == 1)
            {
                if (_ownName.Length == 0)
                {
                    GUI.Label(new Rect(x + 12, cy, w - 24, 22), "Start your own gang" + (_member.Length > 0 ? " (you'll leave yours)" : ""));
                    _nameField = GUI.TextField(new Rect(x + 12, cy + 26, w - 24, 26), _nameField ?? "", 28);
                    GUI.Label(new Rect(x + 12, cy + 58, w - 24, 22), "Runs on: " + Spec());
                    if (Pad.Button(new Rect(x + 12, cy + 84, 140, 26), "Drugs")) _specialty = "drugs";
                    if (Pad.Button(new Rect(x + 158, cy + 84, 140, 26), "Guns")) _specialty = "guns";
                    if (Pad.Button(new Rect(x + 304, cy + 84, 140, 26), "Drugs and guns")) _specialty = "both";
                    if (Pad.Button(new Rect(x + 12, cy + 122, w - 24, 30), "Start " + ((_nameField ?? "").Trim().Length > 0 ? _nameField.Trim() : "my gang") + " ($1,000)"))
                    {
                        if (CashNow() < 1000) Say("You need $1,000 cash.");
                        else
                        {
                            Cash(-1000);
                            if (_member.Length > 0) _heat[_member] = Heat(_member) + 3;
                            _ownName = (_nameField ?? "").Trim().Length > 0 ? _nameField.Trim() : "Your gang";
                            _member = ""; _crew = 2; Save();
                            Say(_ownName + " is born. Recruit a crew, then take a district (F9, Your gang).");
                        }
                    }
                    return;
                }
                GUI.Label(new Rect(x + 12, cy, w - 24, 22), _ownName + " · runs on " + Spec());
                GUI.Label(new Rect(x + 12, cy + 24, w - 24, 22), "Crew " + _crew + " · districts: " + (_turf.Count > 0 ? string.Join(", ", _turf.ToArray()) : "none yet"));
                GUI.Label(new Rect(x + 12, cy + 48, w - 24, 22), "Stock: " + _bricks + " bricks of product, " + _crates + " crates of guns");
                if (Pad.Button(new Rect(x + 12, cy + 78, w - 24, 28), "Recruit someone ($400)"))
                {
                    if (_crew >= 40) Say("Your crew is as big as it gets.");
                    else if (CashNow() < 400) Say("You need $400 cash.");
                    else { Cash(-400); _crew++; Save(); Say("Recruited. Crew: " + _crew + "."); }
                }
                var owner = OwnerGang(_region);
                if (_war.Length == 0 && owner != null && Pad.Button(new Rect(x + 12, cy + 112, w - 24, 28), "Take " + _region + " from the " + owner.Name))
                    StartWar();
                GUI.Label(new Rect(x + 12, cy + 150, w - 24, 66), "Each game day your districts pay out and your crew makes stock: product if you run drugs, " +
                                                                 "guns if you run guns. Sell stock to other gangs in Market.");
                if (Pad.Button(new Rect(x + 12, cy + 220, 140, 26), "Drugs")) { _specialty = "drugs"; Save(); }
                if (Pad.Button(new Rect(x + 158, cy + 220, 140, 26), "Guns")) { _specialty = "guns"; Save(); }
                if (Pad.Button(new Rect(x + 304, cy + 220, 140, 26), "Both")) { _specialty = "both"; Save(); }
                return;
            }
            // market: buy and sell product and guns with whichever gang runs this district
            var g2 = OwnerGang(_region) ?? G(_member);
            if (g2 == null) { GUI.Label(new Rect(x + 12, cy, w - 24, 44), "Nobody trades here. Go to a gang's district."); return; }
            if (Heat(g2.Id) >= 5 && g2.Id != _member) { GUI.Label(new Rect(x + 12, cy, w - 24, 44), "The " + g2.Name + " won't trade with you: you've robbed them too often."); return; }
            float off = g2.Id == _member ? 0.8f : 1f;
            int brick = (int)(900 * g2.DrugPrice), crate = (int)(1400 * g2.GunPrice);
            GUI.Label(new Rect(x + 12, cy, w - 24, 22), "Trading with the " + g2.Name + (off < 1 ? " (member prices)" : ""));
            GUI.Label(new Rect(x + 12, cy + 24, w - 24, 22), "You hold " + _bricks + " bricks, " + _crates + " crates");
            if (Pad.Button(new Rect(x + 12, cy + 52, 210, 28), "Buy a brick $" + (int)(brick * off))) Buy((int)(brick * off), () => _bricks++);
            if (Pad.Button(new Rect(x + 230, cy + 52, 210, 28), "Sell a brick $" + (int)(brick * 1.15f))) Sell(ref _bricks, (int)(brick * 1.15f));
            if (Pad.Button(new Rect(x + 12, cy + 86, 210, 28), "Buy a crate $" + (int)(crate * off))) Buy((int)(crate * off), () => _crates++);
            if (Pad.Button(new Rect(x + 230, cy + 86, 210, 28), "Sell a crate $" + (int)(crate * 1.15f))) Sell(ref _crates, (int)(crate * 1.15f));
            GUI.Label(new Rect(x + 12, cy + 120, w - 24, 22), "Their guns and product, into your pockets:");
            float by = cy + 146;
            foreach (var gun in g2.Guns.Take(4))
            {
                int price = (int)(gun.Contains("@") ? int.Parse(gun.Split('@')[1]) * off : 1000);
                var name = gun.Split('@')[0];
                if (Pad.Button(new Rect(x + 12, by, w - 24, 26), name + "  $" + price)) Buy(price, () => Give(name, 1));
                by += 30;
            }
            int bag = (int)(60 * g2.DrugPrice * off);
            if (Pad.Button(new Rect(x + 12, by, w - 24, 26), "5 " + g2.Product + "  $" + bag * 5)) Buy(bag * 5, () => Give(g2.Product, 5));
            GUI.Label(new Rect(x + 12, y + h - 30, w - 24, 22), "Prices change by gang: buy cheap in one district, sell in another.");
        }

        static string Spec() => _specialty == "drugs" ? "drugs" : _specialty == "guns" ? "guns" : "drugs and guns";

        static void Buy(int price, Action got)
        {
            if (CashNow() < price) { Say("You need $" + price + " cash."); return; }
            Cash(-price);
            got();
            Save();
            Say("Bought for $" + price + ".");
        }

        static void Sell(ref int stock, int price)
        {
            if (stock <= 0) { Say("You have none to sell."); return; }
            stock--;
            Cash(price);
            Save();
            Say("Sold for $" + price + ".");
        }
    }
}
