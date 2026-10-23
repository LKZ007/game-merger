using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Entities;
using S1API.Map;
using S1API.Messaging;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>One of the gangs' own people: a real NPC of the game (S1API's custom NPCs), so they always exist in
    /// the world, are saved with it, and don't need the cartel. They wear their gang's colour and carry its gun.
    /// What they do every day is Crew's job.</summary>
    public abstract class CrewNpc : NPC
    {
        protected CrewNpc() : base() { }
        protected abstract string Key { get; }
        public string PersonKey => Key;
        public override bool IsPhysical => true;

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            var p = Crew.Find(Key);
            if (p == null) return;
            var shirt = Crew.GangColor(p.Gang);
            builder.WithIdentity("frankenstein_" + Key, p.First, p.Last)
                   .WithAppearanceDefaults(av =>
                   {
                       av.Gender = p.Female;
                       av.Height = p.Role == "enforcer" ? 1.08f : p.Role == "boss" ? 1.02f : 1f;
                       av.Weight = p.Role == "enforcer" ? 0.8f : 0.45f;
                       av.SkinColor = p.Skin;
                       av.WithFaceLayer("Avatar/Layers/Face/Face_Neutral", Color.black)
                         .WithBodyLayer("Avatar/Layers/Top/T-Shirt", shirt);
                   });
            Region r;
            if (Crew.TryRegion(p.Gang, out r)) builder.WithRegion(r);
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            try { Appearance?.Build(); } catch (Exception) { }
            Crew.Created(this);
        }
    }

    /// <summary>The gangs' own people living their lives (F10 shows who's who): bosses hold their HQ, lieutenants
    /// and enforcers patrol their turf, dealers and gun runners walk up to the town's people and do business, and
    /// rival crews fight when they meet; now and then a crew raids its rival's turf. Each gang has a three-part
    /// story told by text from its people. Patrol routes come from the district's own buildings and from where
    /// you've walked there. Everything is kept in UserData/Frankenstein/crew.json.</summary>
    internal static partial class Crew
    {
        public class Person
        {
            public string Gang, Role, First, Last, Nick;
            public float Female;
            public Color32 Skin;
            public Person(string gang, string role, string first, string last, string nick, int female, Color32 skin)
            {
                Gang = gang; Role = role; First = first; Last = last; Nick = nick; Female = female; Skin = skin;
            }
            public string Key => Gang + "_" + Role;
            public string Name => First + " \"" + Nick + "\" " + Last;
        }

        class Life
        {
            public CrewNpc Npc;
            public Person P;
            public bool Placed;
            public int Stop;
            public float NextMove, BusyUntil, NextFight, NextThink, DownSince = -1;
            public string Doing = "on the way";
            public NPC Target;
        }

        static readonly Dictionary<string, Life> _lives = new Dictionary<string, Life>();
        static readonly Dictionary<string, List<Vector3>> _points = new Dictionary<string, List<Vector3>>();  // per district
        static readonly Dictionary<string, int> _chapter = new Dictionary<string, int>();
        static readonly Dictionary<string, int> _mark = new Dictionary<string, int>();
        static readonly Dictionary<string, float> _bank = new Dictionary<string, float>();
        static readonly Dictionary<string, float> _textAt = new Dictionary<string, float>();
        static bool _loaded, _inTown, _open, _anchored, _warned;
        static float _next, _nextRaid, _nextSave, _since;
        static Vector3 _lastWalk;
        static string _raid = "", _raidTarget = "";
        static readonly List<string> _log = new List<string>();

        public static Person Find(string key) => People.FirstOrDefault(p => p.Key == key);

        static readonly Dictionary<string, string> Districts = new Dictionary<string, string>
        {
            { "crips", "Northtown" }, { "bloods", "Westville" }, { "london", "Downtown" }, { "yakuza", "Docks" }, { "mafia", "Uptown" }, { "eastlondon", "Suburbia" },
        };

        static string District(string gang)
        {
            var r = Gangs.RegionOf(gang);
            string d;
            return r.Length > 0 ? r : Districts.TryGetValue(gang, out d) ? d : "";
        }

        public static bool TryRegion(string gang, out Region r)
        {
            r = Region.Northtown;
            try { r = (Region)Enum.Parse(typeof(Region), District(gang)); return true; } catch (Exception) { return false; }
        }

        public static Color GangColor(string gang)
        {
            if (Gangs.On) return Gangs.ColorOf(gang);
            switch (gang)
            {
                case "crips": return new Color(0.15f, 0.39f, 0.92f);
                case "bloods": return new Color(0.86f, 0.15f, 0.15f);
                case "yakuza": return new Color(0.1f, 0.1f, 0.1f);
                case "mafia": return new Color(0.2f, 0.2f, 0.24f);
                case "london": return new Color(0.55f, 0.36f, 0.96f);
                default: return new Color(0.92f, 0.7f, 0.1f);
            }
        }

        // the district's own buildings: where each crew's routes start until you've walked there yourself
        static readonly Dictionary<string, string[]> Landmarks = new Dictionary<string, string[]>
        {
            { "Northtown", new[] { "Motel Office", "North apartments", "Pawn Shop", "North Warehouse" } },
            { "Westville", new[] { "West Gas-Mart", "Arcade", "Bud's Bar", "Slop Shop" } },
            { "Downtown", new[] { "Town hall", "Hyland Bank", "Courthouse", "Supermarket" } },
            { "Docks", new[] { "Fish Warehouse", "Docks Industrial Building", "Docks Shipping Container", "Les Ordures Puantes" } },
            { "Uptown", new[] { "Modern Mansion", "Upscale Apartments", "Mayor's House", "Casino" } },
            { "Suburbia", new[] { "Sherman House", "Kennedy House", "Holt House", "Wilkinson House" } },
        };

        static string File_ => Mode.DataFile("crew");  // one per save

        static void Say(string s)
        {
            _log.Insert(0, s);
            if (_log.Count > 8) _log.RemoveAt(8);
        }

        static void Warn(string what, Exception e)
        {
            if (_warned) return;
            _warned = true;
            Melon<Plugin>.Logger.Warning("Crew (" + what + "): " + e.Message);
        }

        public static void Created(CrewNpc npc)
        {
            var p = Find(npc.PersonKey);
            if (p == null) return;
            _lives[p.Key] = new Life { Npc = npc, P = p };
            if (!Gangs.On)
            {
                // this merge has no gangs: the people stay out of the town
                try { npc.Transform.gameObject.SetActive(false); } catch (Exception) { }
                return;
            }
            try
            {
                var w = Gangs.WeaponOf(p.Gang);
                if (w.Length > 0 && npc.CombatBehaviour != null) npc.CombatBehaviour.DefaultWeaponAssetPath = w;
            }
            catch (Exception e) { Warn("weapon", e); }
        }

        public static void Scene(bool inTown)
        {
            _inTown = inTown;
            _anchored = false;
            _since = Time.unscaledTime;
            foreach (var l in _lives.Values) l.Placed = false;
            if (inTown && Gangs.On) Load();
        }

        // ---------- every second or two ----------
        public static void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10) && Gangs.On) _open = !_open;
            if (!_inTown || !Gangs.On || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Perf.Every(1.5f);
            if (Time.unscaledTime - _since < 12f) return;  // the town needs a moment
            if (_lives.Count == 0) Collect();
            Learn();
            if (!_anchored) Anchor();
            foreach (var l in _lives.Values.ToList())
            {
                try { Live(l); } catch (Exception e) { Warn(l.P.Key, e); }
            }
            try { Raid(); } catch (Exception e) { Warn("raid", e); }
            try { Story(); } catch (Exception e) { Warn("story", e); }
            if (Time.unscaledTime >= _nextSave) { _nextSave = Time.unscaledTime + 30f; Save(); }
        }

        static void Collect()
        {
            foreach (var n in NPC.All)
                if (n is CrewNpc c) Created(c);
        }

        /// <summary>Where you walk on foot in a district becomes part of its crews' routes: those spots are always
        /// on the ground and reachable.</summary>
        static void Learn()
        {
            var me = Player.Local;
            if (me == null) return;
            var pos = me.Position;
            if (Vector3.Distance(pos, _lastWalk) < 18f) return;
            _lastWalk = pos;
            string region;
            try { region = me.CurrentRegion.ToString(); } catch (Exception) { return; }
            List<Vector3> pts;
            if (!_points.TryGetValue(region, out pts)) _points[region] = pts = new List<Vector3>();
            if (pts.Any(x => Vector3.Distance(x, pos) < 15f)) return;
            pts.Add(pos);
            if (pts.Count > 24) pts.RemoveAt(0);
        }

        static Vector3? BuildingAt(string name)
        {
            try
            {
                var b = S1API.Map.Building.GetByName(name);
                var f = b?.GetType().GetField("_gameBuilding", BindingFlags.NonPublic | BindingFlags.Instance);
                var c = f?.GetValue(b) as Component;
                return c != null ? c.transform.position : (Vector3?)null;
            }
            catch (Exception) { return null; }
        }

        static void Anchor()
        {
            int found = 0;
            foreach (var kv in Landmarks)
            {
                List<Vector3> pts;
                if (!_points.TryGetValue(kv.Key, out pts)) _points[kv.Key] = pts = new List<Vector3>();
                foreach (var name in kv.Value)
                {
                    var at = BuildingAt(name);
                    if (at == null || pts.Any(x => Vector3.Distance(x, at.Value) < 10f)) continue;
                    pts.Add(at.Value);
                    found++;
                }
            }
            foreach (var p in People)
            {
                Vector3 hq;
                if (Gangs.HqOf(p.Gang, out hq) && !_points[District(p.Gang)].Any(x => Vector3.Distance(x, hq) < 10f)) _points[District(p.Gang)].Insert(0, hq);
            }
            _anchored = true;
            Melon<Plugin>.Logger.Msg("Crew: " + _lives.Count + " gang members, " + found + " landmark(s) found for their routes.");
        }

        /// <summary>An island gang's own turf (Expansion): its crew start from and hold its HQ there.</summary>
        public static void Island(string region, List<Vector3> pts)
        {
            if (region.Length > 0 && pts.Count > 0) _points[region] = pts;
        }

        static List<Vector3> Route(string gang, Life l)
        {
            List<Vector3> pts;
            if (!_points.TryGetValue(District(gang), out pts) || pts.Count == 0) return null;
            return pts;
        }

        static bool Down(NPC n)
        {
            try { return n == null || n.IsDead || n.IsKnockedOut || !n.IsConscious; } catch (Exception) { return true; }
        }

        static void Live(Life l)
        {
            var n = l.Npc;
            if (n == null) return;
            // far from you nobody sees them: they think every 6 seconds instead of every 1.5 (less often, and nearer, when Perf turns extras down)
            var me = Player.Local;
            if (me != null && l.Placed && Time.unscaledTime < l.NextThink)
                return;
            l.NextThink = me != null && Vector3.Distance(n.Position, me.Position) > Perf.FarNpc ? Time.unscaledTime + Perf.Every(6f) : 0f;
            var route = Route(l.P.Gang, l);
            if (route == null) { l.Doing = "waiting for the town"; return; }
            float now = Time.unscaledTime;
            if (Down(n))
            {
                if (l.DownSince < 0) { l.DownSince = now; l.Doing = "down"; Say(l.P.Name + " (" + Gangs.NameOf(l.P.Gang) + ") went down."); }
                if (now - l.DownSince > 240f) { try { n.Revive(); } catch (Exception) { } l.DownSince = -1; l.Placed = false; Say(l.P.Name + " is back on the street."); }
                return;
            }
            l.DownSince = -1;
            if (!l.Placed)
            {
                var start = route[(Math.Abs(l.P.Key.GetHashCode()) + l.Stop) % route.Count] + Offset(l, 3f);
                try { n.Movement.Warp(start); } catch (Exception) { }
                l.Placed = true;
                l.NextMove = now + 2f;
                return;
            }
            if (Fight(l, now)) return;
            if (now < l.BusyUntil || now < l.NextMove) return;
            switch (l.P.Role)
            {
                case "boss":
                    l.Stop = l.Stop == 0 ? 1 : 0;  // between the HQ and the block next to it
                    Go(l, route[l.Stop % route.Count] + Offset(l, 4f), "holding court", 50f);
                    break;
                case "dealer":
                case "gunrunner":
                    if (!Deal(l, now)) { l.Stop++; Go(l, route[l.Stop % route.Count] + Offset(l, 8f), "looking for buyers", 30f); }
                    break;
                default:  // lieutenant and enforcer: patrol the turf, or ride with a raid
                    if (_raid == l.P.Gang)
                    {
                        var target = Route(_raidTarget, l);
                        if (target != null) { Go(l, target[0] + Offset(l, 6f), "raiding " + Gangs.NameOf(_raidTarget), 40f); break; }
                    }
                    l.Stop++;
                    Go(l, route[l.Stop % route.Count] + Offset(l, 10f), "patrolling " + District(l.P.Gang), 25f);
                    break;
            }
        }

        static Vector3 Offset(Life l, float r)
        {
            float a = (Math.Abs(l.P.Key.GetHashCode()) % 360) * Mathf.Deg2Rad + l.Stop * 1.7f;
            return new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
        }

        static void Go(Life l, Vector3 to, string doing, float wait)
        {
            try
            {
                if (l.Npc.Movement.CanGetTo(to, 6f)) l.Npc.Movement.SetDestination(to);
                else l.Npc.Goto(to);
            }
            catch (Exception) { try { l.Npc.Goto(to); } catch (Exception) { } }
            l.Doing = doing;
            l.NextMove = Time.unscaledTime + wait;
        }

        /// <summary>A dealer walks up to one of the town's people near the turf and does business; the gang's money
        /// goes up.</summary>
        static bool Deal(Life l, float now)
        {
            var me = l.Npc.Position;
            if (l.Target != null && !Down(l.Target) && Vector3.Distance(l.Target.Position, me) < 2.6f)
            {
                try { l.Npc.Movement.FacePoint(l.Target.Position); } catch (Exception) { }
                float take = UnityEngine.Random.Range(60f, 240f) * (l.P.Role == "gunrunner" ? 3f : 1f);
                _bank[l.P.Gang] = (_bank.ContainsKey(l.P.Gang) ? _bank[l.P.Gang] : 0) + take;
                l.Doing = (l.P.Role == "gunrunner" ? "selling a piece to " : "serving ") + l.Target.FirstName;
                l.BusyUntil = now + 8f;
                l.Target = null;
                return true;
            }
            if (l.Target == null || Down(l.Target))
            {
                l.Target = NPC.All.Where(x => x != null && !(x is CrewNpc) && !Down(x) && Vector3.Distance(x.Position, me) < 45f)
                                  .OrderBy(x => UnityEngine.Random.value).FirstOrDefault();
                if (l.Target == null) return false;
            }
            Go(l, l.Target.Position, "walking up to " + l.Target.FirstName, 12f);
            return true;
        }

        /// <summary>Rival crews fight when they meet, and a crew you've crossed comes for you on its turf.</summary>
        static bool Fight(Life l, float now)
        {
            if (l.P.Role == "dealer" || l.P.Role == "gunrunner" || now < l.NextFight) return false;
            var me = l.Npc.Position;
            foreach (var o in _lives.Values)
            {
                if (o == l || o.P.Gang == l.P.Gang || o.Npc == null || Down(o.Npc)) continue;
                bool rivals = Gangs.RivalOf(l.P.Gang) == o.P.Gang || Gangs.RivalOf(o.P.Gang) == l.P.Gang || _raid == l.P.Gang || _raid == o.P.Gang;
                if (!rivals || Vector3.Distance(o.Npc.Position, me) > 22f) continue;
                l.Npc.CombatBehaviour.SetAndAttackTarget(o.Npc);
                l.Doing = "fighting " + o.P.Nick;
                l.NextFight = now + 45f;
                l.BusyUntil = now + 20f;
                Say(l.P.Nick + " (" + Gangs.NameOf(l.P.Gang) + ") and " + o.P.Nick + " (" + Gangs.NameOf(o.P.Gang) + ") are fighting in " + District(l.P.Gang) + ".");
                return true;
            }
            var player = Player.Local;
            if (player != null && Vector3.Distance(player.Position, me) < 14f && Gangs.HeatOf(l.P.Gang) >= 3 && Gangs.MemberOf != l.P.Gang)
            {
                l.Npc.CombatBehaviour.SetAndAttackTarget(player);
                l.Doing = "coming for you";
                l.NextFight = now + 60f;
                l.BusyUntil = now + 20f;
                Say(l.P.Nick + " of the " + Gangs.NameOf(l.P.Gang) + " remembers what you did.");
                return true;
            }
            return false;
        }

        /// <summary>Every few minutes one crew sends its lieutenant and enforcer into its rival's turf.</summary>
        static void Raid()
        {
            float now = Time.unscaledTime;
            if (_nextRaid == 0) _nextRaid = now + 300f;
            if (_raid.Length > 0 && now > _nextRaid - 240f) { Say("The " + Gangs.NameOf(_raid) + " raid on " + District(_raidTarget) + " is over."); _raid = ""; }
            if (now < _nextRaid) return;
            _nextRaid = now + UnityEngine.Random.Range(360f, 600f);
            var gangs = People.Select(p => p.Gang).Distinct().Where(g => Gangs.RivalOf(g).Length > 0).ToList();
            if (gangs.Count == 0) return;
            _raid = gangs[UnityEngine.Random.Range(0, gangs.Count)];
            _raidTarget = Gangs.RivalOf(_raid);
            Say("The " + Gangs.NameOf(_raid) + " are rolling into " + District(_raidTarget) + " to hit the " + Gangs.NameOf(_raidTarget) + ".");
            ItemMenu.Notify("The " + Gangs.NameOf(_raid) + " are raiding " + District(_raidTarget) + ".");
        }

        // ---------- each gang's story, told by its people ----------
        static void Text(string gang, string role, string msg, params Response[] answers)
        {
            var who = _lives.Values.FirstOrDefault(x => x.P.Gang == gang && x.P.Role == role) ?? _lives.Values.FirstOrDefault(x => x.P.Gang == gang);
            if (who?.Npc == null) return;
            try { who.Npc.SendTextMessage(msg, answers.Length > 0 ? answers : null); }
            catch (Exception) { ItemMenu.Notify(who.P.Nick + ": " + msg); }
        }

        static Response Answer(string label, Action then) => new Response { Label = label, Text = label, OnTriggered = then };

        static void Story()
        {
            var me = Player.Local;
            if (me == null) return;
            string region;
            try { region = me.CurrentRegion.ToString(); } catch (Exception) { return; }
            float now = Time.unscaledTime;
            foreach (var gang in People.Select(p => p.Gang).Distinct())
            {
                int ch;
                _chapter.TryGetValue(gang, out ch);
                float at;
                if (_textAt.TryGetValue(gang, out at) && now < at) continue;
                var Lines = StoryOf(gang);
                var rival = Gangs.RivalOf(gang);
                if (ch == 0 && region == District(gang))
                {
                    Text(gang, "lieutenant", Lines[0],
                         Answer("Respect. I want in", () => { Text(gang, "boss", Lines[1]); Gangs.Reward(25, 0, "The " + Gangs.NameOf(gang) + " know your name now."); }),
                         Answer("Just passing through", () => Text(gang, "lieutenant", "Then pass. Quick.")));
                    Next(gang, 1, now + 240f);
                }
                else if (ch == 1)
                {
                    Text(gang, "boss", Lines[2].Replace("{rival}", Gangs.NameOf(rival)).Replace("{turf}", District(rival)));
                    _mark[gang] = Gangs.HeatOf(rival);
                    Next(gang, 2, now + 60f);
                }
                else if (ch == 2 && Gangs.HeatOf(rival) > (_mark.ContainsKey(gang) ? _mark[gang] : 0))
                {
                    Text(gang, "boss", Lines[3]);
                    Gangs.Reward(150, 1500, "Story: the " + Gangs.NameOf(gang) + " paid you $1,500 for the job.");
                    Next(gang, 3, now + 300f);
                }
                else if (ch == 3)
                {
                    var target = People.FirstOrDefault(p => p.Gang == rival && p.Role == "enforcer");
                    Text(gang, "lieutenant", Lines[4].Replace("{name}", target != null ? target.Name : "their enforcer").Replace("{turf}", District(rival)));
                    Next(gang, 4, now + 30f);
                }
                else if (ch == 4)
                {
                    Life t;
                    if (_lives.TryGetValue(rival + "_enforcer", out t) && t.Npc != null && Down(t.Npc) && Vector3.Distance(me.Position, t.Npc.Position) < 40f)
                    {
                        Text(gang, "boss", Lines[5]);
                        Gangs.Reward(300, 4000, "Story: the " + Gangs.NameOf(gang) + " finished their war. $4,000 and their respect.");
                        Next(gang, 5, now);
                    }
                }
            }
        }

        static void Next(string gang, int ch, float wait)
        {
            _chapter[gang] = ch;
            _textAt[gang] = wait;
            Save();
        }

        static string[] StoryOf(string gang)
        {
            switch (gang)
            {
                case "crips":
                    return new[] {
                        "Northtown's blue, cuz. You walking our blocks, you check in with me. Smurf.",
                        "Big Loc here. Smurf says you got heart. We'll see.",
                        "Them {rival} been eating on {turf}. Go take what their dealer's holding. G when he's down.",
                        "That's how it's done. You family now, cuz.",
                        "{rival} sent {name} at us. He hangs on {turf}. Put him down.",
                        "Word is {rival} lost their muscle. Northtown's quiet tonight. Respect.",
                    };
                case "bloods":
                    return new[] {
                        "Westville is red. Who sent you? Red Jay.",
                        "Pistol. You want to run with us, you prove it.",
                        "{rival} think {turf} is theirs. Rob their dealer, bring back the bag.",
                        "Damu. You did that. Money's on you.",
                        "{name} been shooting at our little homies. He's in {turf}. Handle it.",
                        "Big Rome says it's done. Westville eats tonight.",
                    };
                case "london":
                    return new[] {
                        "Oi, this is Mandem ends. Downtown's ours. Ty.",
                        "K-Money. Ty says you're sound. Don't make him a liar.",
                        "The {rival} are moving food on {turf}. Take their shotter's bag, innit.",
                        "Calm. You're one of us now, still.",
                        "{name}'s been chatting reckless about us. He's on {turf}. Sort him out.",
                        "Madness. Ends are ours. Proper respect, fam.",
                    };
                case "yakuza":
                    return new[] {
                        "The Docks belong to the Moriyama family. Bow your head here. Hiro.",
                        "Moriyama. Loyalty first, money second. Show me both.",
                        "The {rival} insult us on {turf}. Take what their dealer carries.",
                        "You understand duty. The family will remember.",
                        "{name} disgraced us. He waits on {turf}. End it.",
                        "Honour is restored. The Docks are calm. Thank you.",
                    };
                case "mafia":
                    return new[] {
                        "Uptown's spoken for, capisce? Sal.",
                        "Vincent Moretti. A friend of Sal's is a friend of mine. For now.",
                        "The {rival} are skimming on {turf}. Lighten their dealer's pockets.",
                        "Beautiful. You did right by the family.",
                        "{name} made it personal. You'll find him on {turf}. Make it go away.",
                        "It's handled. Come by the house, you're family now.",
                    };
                default:
                    return new[] {
                        "Suburbia's the Firm's manor. Behave. Tel.",
                        "Frank Doyle. Tel rates you. I don't, yet.",
                        "The {rival} are taking liberties on {turf}. Have their dealer over.",
                        "Lovely job. You're with the Firm now, son.",
                        "{name}'s been mugging us off. He's on {turf}. Deal with it.",
                        "Sorted. The manor's ours. Pint's on me.",
                    };
            }
        }

        // ---------- saved ----------
        static string _loadedFor;

        static void Load()
        {
            if (_loaded && _loadedFor == File_) return;
            _loaded = true;
            _loadedFor = File_;
            _chapter.Clear(); _bank.Clear(); _points.Clear();
            try
            {
                if (!File.Exists(File_)) return;
                var s = MiniJson.Parse(File.ReadAllText(File_));
                var ch = J.O(s, "chapter") as Dictionary<string, object>;
                if (ch != null) foreach (var kv in ch) _chapter[kv.Key] = (int)J.N(ch, kv.Key);
                var bank = J.O(s, "bank") as Dictionary<string, object>;
                if (bank != null) foreach (var kv in bank) _bank[kv.Key] = (float)J.N(bank, kv.Key);
                var pts = J.O(s, "points") as Dictionary<string, object>;
                if (pts != null)
                    foreach (var kv in pts)
                        _points[kv.Key] = J.L(pts, kv.Key).Select(p => new Vector3((float)J.N(p, "x"), (float)J.N(p, "y"), (float)J.N(p, "z"))).ToList();
            }
            catch (Exception e) { Warn("load", e); }
        }

        static string F(float f) => f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        static void Save()
        {
            try
            {
                var b = new StringBuilder("{\"chapter\": {");
                b.Append(string.Join(", ", _chapter.Select(kv => "\"" + kv.Key + "\": " + kv.Value).ToArray()));
                b.Append("}, \"bank\": {").Append(string.Join(", ", _bank.Select(kv => "\"" + kv.Key + "\": " + F(kv.Value)).ToArray()));
                b.Append("}, \"points\": {");
                b.Append(string.Join(", ", _points.Select(kv => "\"" + kv.Key + "\": [" + string.Join(", ", kv.Value.Select(p =>
                    "{\"x\": " + F(p.x) + ", \"y\": " + F(p.y) + ", \"z\": " + F(p.z) + "}").ToArray()) + "]").ToArray()));
                b.Append("}}");
                File.WriteAllText(File_, b.ToString());
            }
            catch (Exception e) { Warn("save", e); }
        }

        // ---------- F10: who's who ----------
        public static void Toggle() { if (Gangs.On && _inTown) _open = !_open; }
        public static void Close() => _open = false;

        public static void Draw()
        {
            if (!_open || !_inTown || !Gangs.On) return;
            float w = 640, h = 560, x = (Screen.width - w) / 2f, y = (Screen.height - h) / 2f;
            Paint.Fill(new Rect(x, y, w, h), new Color(0.07f, 0.05f, 0.11f, 0.97f));
            GUI.Label(new Rect(x + 14, y + 8, w - 28, 22), "THE STREETS  ·  who's who (F10 to close)");
            float yy = y + 34;
            foreach (var gang in People.Select(p => p.Gang).Distinct())
            {
                int ch;
                _chapter.TryGetValue(gang, out ch);
                float bank;
                _bank.TryGetValue(gang, out bank);
                Paint.Fill(new Rect(x + 14, yy + 4, 10, 10), GangColor(gang));
                GUI.Label(new Rect(x + 30, yy, w - 44, 20), "<b>" + Gangs.NameOf(gang) + "</b>  ·  " + District(gang) + "  ·  made $" + ((int)bank).ToString("N0") +
                          "  ·  story " + Math.Min(ch, 5) + "/5");
                yy += 20;
                foreach (var p in People.Where(p => p.Gang == gang))
                {
                    Life l;
                    string doing = _lives.TryGetValue(p.Key, out l) ? l.Doing : "not in town yet";
                    GUI.Label(new Rect(x + 30, yy, w - 44, 18), Cap(p.Role) + ": " + p.Name + "  -  " + doing);
                    yy += 17;
                }
                yy += 4;
            }
            for (int i = 0; i < _log.Count && i < 3; i++)
                GUI.Label(new Rect(x + 14, y + h - 24 - 18 * (2 - i), w - 28, 18), _log[i]);
        }

        static string Cap(string role) => role == "gunrunner" ? "Gun runner" : char.ToUpperInvariant(role[0]) + role.Substring(1);
    }
}
