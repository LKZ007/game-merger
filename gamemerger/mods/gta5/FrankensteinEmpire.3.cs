// Frankenstein's GTA V empire: Schedule I's business and Hood Warfare's gangs, rebuilt inside GTA V's story mode.
// Script Hook V .NET compiles it when the game starts (".3.cs"). It only runs in a merge whose Extension is
// "Schedule I x Hood Warfare" (settings.json has "empire": true), offline, on Frankenstein's own copy of the game.
//
// Business (Schedule I): buy a lab, buy supplies, grow weed, cook meth and coke, package it, sell it to people on
// the street (walk up to someone and press E / D-pad right), hire a worker who keeps the lab running and dealers
// who sell on your turf.
// Gangs (Hood Warfare): GTA V's own gang neighbourhoods (Families, Ballas, Vagos, Aztecas, Marabunte, Lost MC,
// Kkangpae). Hold E / D-pad right for two seconds in a gang's turf to start a turf war; beat their crew there to
// take it. Turf pays every in-game hour, raises your reputation, and the gang may come back for it.
// Heat: selling, cooking and fighting draw the police; a lot of heat brings raids on your labs.
// F5: the empire menu (on a controller, hold D-pad left for a second; E / D-pad right at a lab). Everything it does is logged to
// scripts/Frankenstein/empire-debug.txt, and the empire is saved to scripts/Frankenstein/empire.txt.
//
// Two modes, each with its own empire (Creative's is saved to empire-creative.txt):
// Story plays like Schedule I: you start with one small stash house and a few supplies, earn XP from every batch,
// sale and turf war, and climb ranks (Street Rat to Kingpin) that unlock the bigger properties, meth, workers,
// dealers, turf wars and coke, with a step-by-step objective on screen.
// Creative is for testing: everything unlocked and owned, unlimited money and supplies, 30-second batches, no heat
// unless you turn it on, and test tools in the F5 menu (product, heat, raids, turf wars, ranks, teleports).
// The mode is the save's own (Frankenstein's Creative switch); the F5 menu can switch it too, so a controller can.
// Story has cutscenes for its big moments (the start, each rank, each property, first meth and coke, first turf won
// and lost, first raid, Kingpin), each once per save, played with GTA V's own cameras when you're safe; Enter, Space
// or A skips one. Creative can replay any of them from its test tools.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.Native;
using GTA.UI;
using Font = GTA.UI.Font;
using Screen = GTA.UI.Screen;

public class FrankensteinEmpire : Script
{
    // ---------- what there is ----------

    class Lab
    {
        public string Id, Name, Kind;   // kind: farm (weed), lab (meth, coke), stash
        public Vector3 At;
        public int Price, Rank;         // Story: the rank that puts it up for sale
        public int Sprite;              // its map icon
        public bool Owned, Worker;
        public int Supplies;            // one unit makes one batch
        public string Batch = "";       // what's growing or cooking
        public int ReadyAt;             // Game.GameTime when it's done
        public Blip Blip;
    }

    class Gang
    {
        public string Id, Name, Model;
        public string[] Zones;
        public Color Color;
        public int Rep;                 // how they feel about you: -100 war, 0 neutral, 100 allies
        public WeaponHash Gun;
        public BlipColor Ring;          // its turf rings on the map
    }

    // the middle of each gang neighbourhood and how far it reaches, for the coloured turf rings on the map
    // (GTA V's zones aren't circles, so the rings are close, not exact)
    static readonly Dictionary<string, float[]> ZoneRing = new Dictionary<string, float[]>
    {
        { "CHAMH", new[] { -150f, -1620f, 260f } }, { "STRAW", new[] { 90f, -1400f, 260f } }, { "DAVIS", new[] { 110f, -1760f, 280f } },
        { "RANCHO", new[] { 420f, -1880f, 260f } }, { "CYPRE", new[] { 820f, -2280f, 300f } }, { "EBURO", new[] { 1350f, -2050f, 280f } },
        { "EAST_V", new[] { 880f, -160f, 280f } }, { "STAB", new[] { 70f, 3700f, 200f } }, { "SANDY", new[] { 1800f, 3720f, 380f } },
        { "KOREAT", new[] { -680f, -900f, 300f } },
    };
    readonly List<Blip> rings = new List<Blip>();

    static readonly string[] Products = { "weed", "meth", "coke" };
    static readonly Dictionary<string, int> StreetPrice = new Dictionary<string, int> { { "weed", 60 }, { "meth", 140 }, { "coke", 220 } };
    static readonly Dictionary<string, int> BatchMinutes = new Dictionary<string, int> { { "weed", 4 }, { "meth", 6 }, { "coke", 8 } };
    static readonly Dictionary<string, int> BatchSize = new Dictionary<string, int> { { "weed", 12 }, { "meth", 8 }, { "coke", 6 } };
    const int SupplyPrice = 250, WorkerPrice = 5000, DealerPrice = 2500, WarMinutes = 4;

    readonly List<Lab> labs = new List<Lab>
    {
        new Lab { Id = "grapeseed", Name = "Grapeseed farm", Sprite = 496, Kind = "farm", At = new Vector3(2434f, 4969f, 46.8f), Price = 45000, Rank = 1 },
        new Lab { Id = "sandy", Name = "Sandy Shores trailer lab", Sprite = 499, Kind = "lab", At = new Vector3(1972f, 3817f, 33.4f), Price = 60000, Rank = 2 },
        new Lab { Id = "strawberry", Name = "Strawberry stash house", Sprite = 40, Kind = "farm", At = new Vector3(-14f, -1440f, 31.1f), Price = 30000 },
        new Lab { Id = "docks", Name = "Elysian Island warehouse", Sprite = 473, Kind = "lab", At = new Vector3(155f, -3080f, 5.9f), Price = 90000, Rank = 5 },
    };

    readonly List<Gang> gangs = new List<Gang>
    {
        new Gang { Id = "families", Name = "The Families", Model = "g_m_y_famca_01", Zones = new[] { "CHAMH", "STRAW" }, Color = Color.FromArgb(40, 180, 70), Gun = WeaponHash.MicroSMG, Ring = BlipColor.Green },
        new Gang { Id = "ballas", Name = "Ballas", Model = "g_m_y_ballaorig_01", Zones = new[] { "DAVIS" }, Color = Color.FromArgb(150, 60, 200), Gun = WeaponHash.Pistol, Ring = BlipColor.Purple },
        new Gang { Id = "vagos", Name = "Vagos", Model = "g_m_y_mexgoon_01", Zones = new[] { "RANCHO", "CYPRE" }, Color = Color.FromArgb(240, 200, 30), Gun = WeaponHash.AssaultRifle, Ring = BlipColor.Yellow },
        new Gang { Id = "aztecas", Name = "Aztecas", Model = "g_m_y_azteca_01", Zones = new[] { "EBURO" }, Color = Color.FromArgb(60, 160, 230), Gun = WeaponHash.SawnOffShotgun, Ring = BlipColor.BlueLight },
        new Gang { Id = "marabunta", Name = "Marabunta Grande", Model = "g_m_y_salvagoon_01", Zones = new[] { "EAST_V" }, Color = Color.FromArgb(70, 120, 230), Gun = WeaponHash.Pistol, Ring = BlipColor.BlueDark },
        new Gang { Id = "lost", Name = "The Lost MC", Model = "g_m_y_lost_01", Zones = new[] { "STAB", "SANDY" }, Color = Color.FromArgb(200, 200, 200), Gun = WeaponHash.PumpShotgun, Ring = BlipColor.White },
        new Gang { Id = "kkangpae", Name = "Kkangpae", Model = "g_m_y_korean_01", Zones = new[] { "KOREAT" }, Color = Color.FromArgb(220, 60, 60), Gun = WeaponHash.SMG, Ring = BlipColor.Red },
    };

    // Story's ranks (Schedule I's ladder) and what each one unlocks
    static readonly string[] Ranks = { "Street Rat", "Hoodlum", "Peddler", "Hustler", "Bagman", "Enforcer", "Shot Caller", "Block Boss", "Underlord", "Kingpin" };
    static readonly int[] RankXp = { 0, 300, 800, 1600, 3000, 5000, 8000, 12000, 17000, 25000 };
    static readonly Dictionary<string, int> Needs = new Dictionary<string, int>
    {
        { "weed", 0 }, { "meth", 2 }, { "worker", 3 }, { "dealer", 4 }, { "turf", 4 }, { "truce", 4 }, { "coke", 5 },
    };

    // ---------- state ----------

    readonly Dictionary<string, int> stock = new Dictionary<string, int> { { "weed", 0 }, { "meth", 0 }, { "coke", 0 } };
    readonly HashSet<string> turf = new HashSet<string>();          // zones you hold
    readonly Dictionary<string, int> dealers = new Dictionary<string, int>();  // zone -> dealers working it
    int heat, earned, lastHour = -1;
    bool on, loaded, creative, creativeHeat, fresh;
    string graphics = "high";
    int look = -1;                  // which film look (-1: the graphics setting's own pick)
    // GTA V's own film filters (the Rockstar Editor's), checked in game: one that isn't there is skipped
    static readonly string[] Looks = { "NG_filmic02", "NG_filmic06", "NG_filmic10", "NG_filmic13", "NG_filmic20" };
    int xp, quest, madeCount, packedCount, soldCount, warsWon, nextTopUp, switchConfirm;
    readonly HashSet<int> served = new HashSet<int>();
    readonly List<string> log = new List<string>();

    // turf war
    string warZone = "";
    Gang warGang;
    int warEnds;
    readonly List<Ped> warPeds = new List<Ped>();
    RelationshipGroup enemies;

    // menu
    bool menuOpen;
    int sel, holdStart = -1, padHold = -1, nextHeat, nextRaid, nextHint;
    List<KeyValuePair<string, Action>> items = new List<KeyValuePair<string, Action>>();
    string menuTitle = "";

    string Dir { get { return Path.Combine(BaseDirectory, "Frankenstein"); } }

    public FrankensteinEmpire()
    {
        Interval = 0;
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Aborted += (s, e) => Cleanup();
        try
        {
            var p = Path.Combine(Dir, "settings.json");
            var text = File.Exists(p) ? File.ReadAllText(p) : "";
            on = System.Text.RegularExpressions.Regex.IsMatch(text, "\"empire\"\\s*:\\s*true");
            creative = System.Text.RegularExpressions.Regex.IsMatch(text, "\"creative\"\\s*:\\s*true");
            var g = System.Text.RegularExpressions.Regex.Match(text, "\"graphics\"\\s*:\\s*\"(\\w+)\"");
            if (g.Success) graphics = g.Groups[1].Value;
        }
        catch (Exception) { on = false; }
        Say(on ? "empire on (Schedule I x Hood Warfare merge), " + (creative ? "Creative" : "Story") : "empire off (not this merge's Extension)");
    }

    // ---------- every frame ----------

    void OnTick(object sender, EventArgs e)
    {
        if (!on) return;
        var me = Game.Player.Character;
        if (me == null || !me.Exists() || Game.IsLoading) return;
        if (!loaded) { Load(); loaded = true; Begin(); Blips(); Look(); Say("loaded: " + labs.Count(l => l.Owned) + " labs, " + turf.Count + " turf, $" + earned + " earned, " + RankName + " (" + xp + " XP)"); }
        if (playing != null) { try { Scenes(me); } catch (Exception ex) { Say("cutscene: " + ex.Message); EndScene(); } return; }  // a cutscene owns the screen
        try
        {
            Production();
            Hourly();
            Creative();
            Quests();
            Heat();
            War(me);
            Interact(me);
            Hud(me);
            Pad(me);
            Scenes(me);
            if (menuOpen) Menu();
        }
        catch (Exception ex) { Say("tick: " + ex.Message); }
    }

    // ---------- business ----------

    void Production()
    {
        foreach (var l in labs.Where(x => x.Owned))
        {
            if (l.Batch.Length > 0 && Game.GameTime >= l.ReadyAt && l.Worker)
                Collect(l, true);  // the worker harvests and packages by themselves
            if (l.Batch.Length == 0 && l.Worker && l.Supplies > 0)
                Start(l, l.Kind == "farm" ? "weed" : (heat < 60 ? "meth" : "coke"), true);
        }
    }

    void Start(Lab l, string what, bool quiet = false)
    {
        if (l.Supplies <= 0) { Notification.Show("~r~No supplies at " + l.Name + "."); return; }
        if (l.Batch.Length > 0) { Notification.Show(l.Name + " is busy."); return; }
        if (!Unlocked(what)) { Notification.Show(Title(what) + " unlocks at " + Ranks[Needs[what]] + "."); return; }
        if (!creative) l.Supplies--;
        l.Batch = what;
        l.ReadyAt = Game.GameTime + BatchMs(what);
        AddHeat(what == "weed" ? 2 : 5);
        madeCount++;
        if (what != "weed") Cutscene("first-" + what, LabShots(l, "Uncle Ray (phone)", what == "meth" ? "That's the real stuff now. Careful who you sell it to." : "Coke. Rich people's problem, your profit. Keep it clean."));
        if (!quiet) Notification.Show("~g~" + Title(what) + "~s~ started at " + l.Name + ": ready in " + Mins(BatchMs(what)) + ".");
        Say(l.Name + ": started " + what + (quiet ? " (worker)" : ""));
        Save();
    }

    void Collect(Lab l, bool quiet = false)
    {
        if (l.Batch.Length == 0) return;
        if (Game.GameTime < l.ReadyAt) { Notification.Show(Title(l.Batch) + " ready in " + Mins(l.ReadyAt - Game.GameTime) + "."); return; }
        int n = BatchSize[l.Batch];
        stock[l.Batch] += n;
        packedCount++;
        Notification.Show("~g~+" + n + " " + l.Batch + "~s~ packaged at " + l.Name + (quiet ? " by your worker" : "") + ".");
        AddXp(l.Batch == "weed" ? 20 : l.Batch == "meth" ? 40 : 60, "batch");
        Say(l.Name + ": +" + n + " " + l.Batch);
        l.Batch = "";
        Save();
    }

    /// <summary>Walk up to someone on foot and press E: they buy if the price is right and they're not a cop.</summary>
    void Sell(Ped me, Ped who)
    {
        var have = Products.Where(x => stock[x] > 0).ToList();
        if (have.Count == 0) { Notification.Show("~r~Nothing to sell.~s~ Make some at a lab (F5)."); return; }
        served.Add(who.Handle);
        string zone = Zone(me.Position);
        var owner = GangOf(zone);
        bool mine = turf.Contains(zone);
        var rnd = new Random(Game.GameTime ^ who.Handle);
        var p = have[rnd.Next(have.Count)];
        int price = (int)(StreetPrice[p] * (mine ? 1.3 : 1.0) * (0.8 + rnd.NextDouble() * 0.4));
        bool cop = Function.Call<int>(Hash.GET_PED_TYPE, who) == 6 || Function.Call<int>(Hash.GET_PED_TYPE, who) == 27;
        if (cop && (!creative || creativeHeat)) { Game.Player.WantedLevel = Math.Max(Game.Player.WantedLevel, 2); Notification.Show("~r~That was an undercover cop!"); AddHeat(15); return; }
        if (owner != null && !mine && owner.Rep < 0 && rnd.Next(3) == 0)
        {
            Notification.Show("~r~" + owner.Name + " don't like you dealing on their turf.");
            who.Task.FightAgainst(me);
            owner.Rep -= 5;
            return;
        }
        if (rnd.Next(10) < 2) { Notification.Show("Not interested."); return; }
        stock[p]--;
        Game.Player.Money += price;
        earned += price;
        soldCount++;
        AddHeat(3);
        Notification.Show("Sold 1 " + p + " for ~g~$" + price + (mine ? "~s~ (your turf)" : ""));
        AddXp(Math.Max(5, price / 10), "sale");
        if (heat > 40 && rnd.Next(100) < heat / 4 && World.GetNearbyPeds(me, 40f).Any(x => Function.Call<int>(Hash.GET_PED_TYPE, x) == 6))
        {
            Game.Player.WantedLevel = Math.Max(Game.Player.WantedLevel, 1);
            Notification.Show("~r~The police saw that.");
        }
        Say("sold " + p + " for $" + price + " in " + zone);
        Save();
    }

    /// <summary>Every in-game hour: turf pays, dealers sell, workers get paid, and heat cools a little.</summary>
    void Hourly()
    {
        int hour = World.CurrentTimeOfDay.Hours;
        if (lastHour < 0) { lastHour = hour; return; }
        if (hour == lastHour) return;
        lastHour = hour;
        int income = turf.Count * 150, sold = 0, wages = labs.Count(l => l.Worker) * 100;
        foreach (var kv in dealers)
            for (int i = 0; i < kv.Value; i++)
            {
                var p = Products.FirstOrDefault(x => stock[x] > 0);
                if (p == null) break;
                stock[p]--;
                income += StreetPrice[p];
                sold++;
            }
        int net = income - wages;
        if (income > 0 || wages > 0)
        {
            Game.Player.Money += net;
            earned += Math.Max(0, net);
            Notification.Show("Empire: turf ~g~$" + (turf.Count * 150) + "~s~, dealers sold " + sold + ", wages ~r~$" + wages);
        }
        heat = Math.Max(0, heat - 4);
        Say("hour " + hour + ": +" + income + " -" + wages + ", heat " + heat);
        Save();
    }

    /// <summary>A lot of heat (or a big empire) brings trouble: police raids on your labs, gangs taking turf back.</summary>
    void Heat()
    {
        if (creative && !creativeHeat) return;
        if (Game.GameTime < nextRaid) return;
        nextRaid = Game.GameTime + 120000;
        var rnd = new Random(Game.GameTime);
        var me = Game.Player.Character;
        int attention = heat + labs.Count(l => l.Owned) * 5 + turf.Count * 4;
        if (attention < 50 || rnd.Next(100) > attention / 2) return;
        var lab = labs.Where(l => l.Owned && (l.Supplies > 0 || l.Batch.Length > 0)).OrderBy(l => rnd.Next()).FirstOrDefault();
        if (lab != null && rnd.Next(2) == 0)
        {
            if (me.Position.DistanceTo(lab.At) < 80f) { Game.Player.WantedLevel = Math.Max(Game.Player.WantedLevel, 3); Notification.Show("~r~Police raid at " + lab.Name + "!"); }
            else { lab.Supplies = Math.Max(0, lab.Supplies - 2); lab.Batch = ""; Notification.Show("~r~The police raided " + lab.Name + " while you were away: the batch and some supplies are gone."); }
            Cutscene("first-raid", LabShots(lab, "Uncle Ray (phone)", "The cops hit " + lab.Name + ". Lay low. Let the heat die down before you cook again."));
            heat = Math.Max(0, heat - 25);
            Say("raid at " + lab.Name);
        }
        else if (turf.Count > 0)
        {
            var z = turf.OrderBy(x => rnd.Next()).First();
            var g = gangs.FirstOrDefault(x => x.Zones.Contains(z));
            if (g == null) return;
            g.Rep -= 10;
            Notification.Show("~r~" + g.Name + " want " + Game.GetLocalizedString(z) + " back. Defend it next time you're there.");
            if (Zone(me.Position) == z && warZone.Length == 0) BeginWar(z, g, true);
        }
        Save();
    }

    // ---------- turf wars ----------

    void BeginWar(string zone, Gang g, bool defend = false)
    {
        var me = Game.Player.Character;
        if (enemies == default(RelationshipGroup) || enemies.Hash == 0)
        {
            enemies = World.AddRelationshipGroup("FRANKENSTEIN_ENEMY");
            enemies.SetRelationshipBetweenGroups(me.RelationshipGroup, Relationship.Hate, true);
        }
        var model = new Model(g.Model);
        if (!model.IsInCdImage) model = new Model("g_m_y_mexgoon_01");
        model.Request(3000);
        var rnd = new Random(Game.GameTime);
        int n = 6 + Math.Min(6, turf.Count);
        for (int i = 0; i < n; i++)
        {
            var at = me.Position.Around(35f + (float)rnd.NextDouble() * 25f);
            at.Z = World.GetGroundHeight(at + new Vector3(0, 0, 50f));
            var p = World.CreatePed(model, at);
            if (p == null) continue;
            p.RelationshipGroup = enemies;
            p.Weapons.Give(rnd.Next(3) == 0 ? WeaponHash.AssaultRifle : g.Gun, 500, true, true);
            p.Armor = 40;
            p.Task.FightAgainst(me);
            p.AddBlip().Color = BlipColor.Red;
            warPeds.Add(p);
        }
        model.MarkAsNoLongerNeeded();
        warZone = zone;
        warGang = g;
        warEnds = Game.GameTime + WarMinutes * 60000;
        g.Rep = Math.Min(g.Rep, -20);
        AddHeat(15);
        Screen.ShowSubtitle((defend ? "Defend " : "Turf war: take ") + Game.GetLocalizedString(zone) + " from " + g.Name + ". Take out their crew.", 6000);
        Say("turf war in " + zone + " against " + g.Name + " (" + warPeds.Count + " of them)");
    }

    void War(Ped me)
    {
        if (warZone.Length == 0) return;
        warPeds.RemoveAll(p => p == null || !p.Exists());
        int left = warPeds.Count(p => p.IsAlive);
        bool lost = me.IsDead || Game.GameTime > warEnds;
        if (left == 0)
        {
            turf.Add(warZone);
            warGang.Rep -= 10;
            Game.Player.Money += 2500;
            warsWon++;
            Notification.Show("~g~" + Game.GetLocalizedString(warZone) + " is yours.~s~ It pays every hour, and your dealers can work it.");
            AddXp(250, "turf war");
            Blips();
            Cutscene("first-turf", Orbit(me.Position, 9f, 3f, "Uncle Ray (phone)", Game.GetLocalizedString(warZone) + " is yours. Taking a block is easy. Holding it is the job."));
            Say("took " + warZone);
            EndWar();
        }
        else if (lost)
        {
            Cutscene("first-loss", Orbit(me.Position, 10f, 4f, "Uncle Ray (phone)", "You lost that one. It happens once. Don't let it happen twice."));
            if (turf.Remove(warZone)) { Blips(); Notification.Show("~r~" + warGang.Name + " took " + Game.GetLocalizedString(warZone) + " back."); }
            else Notification.Show("~r~The turf war in " + Game.GetLocalizedString(warZone) + " is lost.");
            Say("lost the war in " + warZone);
            EndWar();
        }
    }

    void EndWar()
    {
        foreach (var p in warPeds) if (p != null && p.Exists()) { if (p.AttachedBlip != null) p.AttachedBlip.Delete(); p.MarkAsNoLongerNeeded(); }
        warPeds.Clear();
        warZone = "";
        warGang = null;
        Save();
    }

    /// <summary>On a controller: hold D-pad left for a second on foot to open the empire menu (F5 on a keyboard).</summary>
    void Pad(Ped me)
    {
        if (menuOpen || me.IsInVehicle() || Game.LastInputMethod != InputMethod.GamePad) { padHold = -1; return; }
        if (!Game.IsControlPressed(GTA.Control.Detonate)) { padHold = -1; return; }
        if (padHold < 0) padHold = Game.GameTime;
        if (Game.GameTime - padHold > 1000) { padHold = -1; MainMenu(); }
    }

    // ---------- E / D-pad right ----------

    void Interact(Ped me)
    {
        if (menuOpen || me.IsInVehicle()) { holdStart = -1; return; }
        var lab = labs.FirstOrDefault(l => me.Position.DistanceTo(l.At) < 3f);
        foreach (var l in labs)
            if (me.Position.DistanceTo(l.At) < 60f)
                World.DrawMarker(MarkerType.VerticalCylinder, l.At - new Vector3(0, 0, 1f), Vector3.Zero, Vector3.Zero, new Vector3(1.6f, 1.6f, 0.6f), l.Owned ? Color.FromArgb(150, 60, 220, 90) : Color.FromArgb(150, 140, 90, 250));
        if (lab != null)
        {
            Screen.ShowHelpTextThisFrame(lab.Owned ? "~INPUT_CONTEXT~ " + lab.Name : "~INPUT_CONTEXT~ Buy " + lab.Name + " for $" + lab.Price.ToString("N0"));
            if (Game.IsControlJustPressed(GTA.Control.Context)) LabMenu(lab);
            return;
        }
        var who = World.GetNearbyPeds(me, 2.2f).FirstOrDefault(p => p.IsAlive && p.IsHuman && !p.IsPlayer && !p.IsInVehicle() && !served.Contains(p.Handle) && !warPeds.Contains(p));
        if (who != null && Products.Any(p => stock[p] > 0))
        {
            Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Offer product");
            if (Game.IsControlJustPressed(GTA.Control.Context)) Sell(me, who);
            return;
        }
        string zone = Zone(me.Position);
        var g = GangOf(zone);
        if (g == null || turf.Contains(zone) || warZone.Length > 0) { holdStart = -1; return; }
        if (!Unlocked("turf"))
        {
            if (Game.GameTime > nextHint) { nextHint = Game.GameTime + 120000; Notification.Show(g.Name + " run " + Game.GetLocalizedString(zone) + ". Turf wars unlock at " + Ranks[Needs["turf"]] + "."); }
            holdStart = -1;
            return;
        }
        if (Game.GameTime > nextHint) { nextHint = Game.GameTime + 60000; Notification.Show(g.Name + " run " + Game.GetLocalizedString(zone) + ". Hold ~INPUT_CONTEXT~ to start a turf war."); }
        if (Game.IsControlPressed(GTA.Control.Context))
        {
            if (holdStart < 0) holdStart = Game.GameTime;
            if (Game.GameTime - holdStart > 2000) { holdStart = -1; BeginWar(zone, g); }
        }
        else holdStart = -1;
    }

    // ---------- menus ----------

    void LabMenu(Lab l)
    {
        var list = new List<KeyValuePair<string, Action>>();
        if (!l.Owned)
        {
            if (!creative && Rank < l.Rank) { list.Add(Item("For sale at " + Ranks[l.Rank] + " (you're " + RankName + ")", () => { })); Open(l.Name, list); return; }
            list.Add(Item("Buy for $" + l.Price.ToString("N0"), () =>
            {
                if (Game.Player.Money < l.Price) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= l.Price; l.Owned = true; l.Supplies = 2; Blips(); Save();
                Notification.Show("~g~" + l.Name + " is yours.~s~ It comes with 2 supplies."); Say("bought " + l.Name);
                Cutscene("bought-" + l.Id, LabShots(l, "Uncle Ray (phone)", l.Id == "grapeseed" ? "Grapeseed. Room to grow, and nobody out here asks questions." :
                                                                        l.Id == "sandy" ? "Out in the desert nobody hears the cook. Keep the windows open." :
                                                                        l.Id == "docks" ? "The docks. Ships in, product out. This is the big league." : "Yours now. Make it work."));
            }));
        }
        else
        {
            if (l.Batch.Length > 0) list.Add(Item(Game.GameTime >= l.ReadyAt ? "Package the " + l.Batch : Title(l.Batch) + ": " + Mins(l.ReadyAt - Game.GameTime) + " left", () => Collect(l)));
            else if (l.Kind == "farm") list.Add(Item("Grow weed (1 supply, " + Mins(BatchMs("weed")) + ")", () => Start(l, "weed")));
            else
            {
                list.Add(Item(Unlocked("meth") ? "Cook meth (1 supply, " + Mins(BatchMs("meth")) + ")" : "Meth: unlocks at " + Ranks[Needs["meth"]], () => Start(l, "meth")));
                list.Add(Item(Unlocked("coke") ? "Cook coke (1 supply, " + Mins(BatchMs("coke")) + ")" : "Coke: unlocks at " + Ranks[Needs["coke"]], () => Start(l, "coke")));
            }
            list.Add(Item("Buy 5 supplies ($" + (5 * SupplyPrice) + ")  ·  has " + l.Supplies, () =>
            {
                if (Game.Player.Money < 5 * SupplyPrice) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= 5 * SupplyPrice; l.Supplies += 5; Save();
            }));
            if (!l.Worker) list.Add(Item(Unlocked("worker") ? "Hire a worker ($" + WorkerPrice + ", $100 an hour)" : "Workers: unlock at " + Ranks[Needs["worker"]], () =>
            {
                if (!Unlocked("worker")) return;
                if (Game.Player.Money < WorkerPrice) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= WorkerPrice; l.Worker = true; Save();
                Notification.Show("Your worker keeps " + l.Name + " running while there are supplies.");
            }));
        }
        Open(l.Name, list);
    }

    void MainMenu()
    {
        var me = Game.Player.Character;
        var list = new List<KeyValuePair<string, Action>>();
        if (creative) list.Add(Item("Creative test tools...", CreativeMenu));
        else list.Add(Item("Objective: " + QuestText(), () => QuestWaypoint()));
        foreach (var l in labs)
        {
            var lab = l;
            list.Add(Item((l.Owned ? "Your " : !creative && Rank < l.Rank ? "Locked (" + Ranks[l.Rank] + "): " : "For sale: ") + l.Name + (l.Owned ? "  ·  " + (l.Batch.Length > 0 ? l.Batch + " " + (Game.GameTime >= l.ReadyAt ? "ready" : Mins(l.ReadyAt - Game.GameTime)) : "idle") + ", " + l.Supplies + " supplies" + (l.Worker ? ", worker" : "") : "  ·  $" + l.Price.ToString("N0")),
                () => { Function.Call(Hash.SET_NEW_WAYPOINT, lab.At.X, lab.At.Y); Notification.Show("Waypoint set: " + lab.Name); }));
        }
        string zone = Zone(me.Position);
        if (turf.Contains(zone) && Unlocked("dealer"))
            list.Add(Item("Put a dealer on " + Game.GetLocalizedString(zone) + " ($" + DealerPrice + ")  ·  has " + (dealers.ContainsKey(zone) ? dealers[zone] : 0), () =>
            {
                if (Game.Player.Money < DealerPrice) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= DealerPrice; dealers[zone] = (dealers.ContainsKey(zone) ? dealers[zone] : 0) + 1; Save();
            }));
        foreach (var g in gangs)
        {
            var gang = g;
            list.Add(Item(g.Name + ": " + (g.Rep <= -20 ? "at war" : g.Rep >= 40 ? "allies" : "neutral") + " (" + g.Rep + ")  ·  " + string.Join(", ", g.Zones.Select(z => Game.GetLocalizedString(z) + (turf.Contains(z) ? " (yours)" : ""))),
                () =>
                {
                    if (!Unlocked("truce")) { Notification.Show("Truces unlock at " + Ranks[Needs["truce"]] + "."); return; }
                    if (Game.Player.Money < 10000) { Notification.Show("A truce with " + gang.Name + " costs $10,000."); return; }
                    Game.Player.Money -= 10000; gang.Rep = Math.Min(100, gang.Rep + 30); Save();
                    Notification.Show("You paid " + gang.Name + " $10,000. Relations: " + gang.Rep + ".");
                }));
        }
        list.Add(Item("Look...", LookMenu));
        list.Add(Item(Game.GameTime < switchConfirm ? "Press again to switch to " + (creative ? "Story" : "Creative") : "Switch this game to " + (creative ? "Story" : "Creative") + "...", SwitchMode));
        Open((creative ? "CREATIVE" : "STORY") + "  ·  " + RankName + "  ·  earned $" + earned.ToString("N0") + "  ·  heat " + heat, list);
    }

    /// <summary>The same switch as Frankenstein's Shift+F10, reachable from a controller: Frankenstein restarts the
    /// game in the other mode, each with its own saves and its own empire.</summary>
    void SwitchMode()
    {
        if (Game.GameTime >= switchConfirm) { switchConfirm = Game.GameTime + 8000; MainMenu(); sel = items.Count - 1; return; }
        switchConfirm = 0;
        try
        {
            File.WriteAllText(Path.Combine(Dir, "switch.json"), "{\"to\": \"" + (creative ? "normal" : "creative") + "\"}");
            Notification.Show("Save, then quit from the pause menu. Frankenstein starts the game again in " + (creative ? "Story" : "Creative") + ".");
            Say("switch to " + (creative ? "story" : "creative") + " asked");
        }
        catch (Exception e) { Notification.Show("~r~Couldn't switch: " + e.Message); }
    }

    // ---------- cutscenes ----------

    class Shot { public Vector3 From, To, Look; public int Ms; public string Who = "", Line = ""; }

    readonly HashSet<string> seen = new HashSet<string>();
    readonly Queue<KeyValuePair<string, List<Shot>>> queued = new Queue<KeyValuePair<string, List<Shot>>>();
    List<Shot> playing;
    string playingId = "";
    int shotAt, shotStart;
    Camera cam;
    bool skipKey;

    static readonly string[] RankLines =
    {
        "",
        "People on the street know your face now. There's a farm up in Grapeseed going cheap.",
        "Weed pays the bills. Meth pays for everything else. There's a trailer in Sandy Shores.",
        "You can't be everywhere. Hire hands you trust and let the labs run themselves.",
        "The gangs have noticed you. Their corners, their rules. Unless you take them.",
        "There's a warehouse at the docks, and a coke connect who'll only talk to you now.",
        "Crews call you before they move. That's power. Don't waste it.",
        "Whole blocks answer to you now. So do the cops' notebooks.",
        "You're not on the street any more. You are the street.",
        "",
    };

    /// <summary>Queue a cutscene. Story plays each one once per save; Creative only when asked from its tools.</summary>
    void Cutscene(string id, List<Shot> shots, bool replay = false)
    {
        if (!replay && (creative || seen.Contains(id))) return;
        if (!replay) { seen.Add(id); Save(); }
        queued.Enqueue(new KeyValuePair<string, List<Shot>>(id, shots));
    }

    /// <summary>Plays the next queued cutscene once you're safe (no wanted level, no turf war, no menu open).</summary>
    void Scenes(Ped me)
    {
        if (playing == null)
        {
            if (queued.Count == 0 || me.IsDead || Game.Player.WantedLevel > 0 || warZone.Length > 0 || menuOpen) return;
            var next = queued.Dequeue();
            playing = next.Value;
            playingId = next.Key;
            shotAt = 0;
            shotStart = Game.GameTime;
            skipKey = false;
            cam = World.CreateCamera(playing[0].From, Vector3.Zero, 48f);
            World.RenderingCamera = cam;
            Game.Player.CanControlCharacter = false;
            me.IsInvincible = true;
            Say("cutscene " + playingId);
            return;
        }
        var s = playing[shotAt];
        float t = Math.Min(1f, (Game.GameTime - shotStart) / (float)Math.Max(1, s.Ms));
        float ease = t * t * (3f - 2f * t);
        cam.Position = Vector3.Lerp(s.From, s.To, ease);
        cam.PointAt(s.Look);
        Function.Call(Hash.HIDE_HUD_AND_RADAR_THIS_FRAME);
        // letterbox, the speaker and the line, like a film
        new ContainerElement(new PointF(0, 0), new SizeF(1280, 78), Color.Black).Draw();
        new ContainerElement(new PointF(0, 642), new SizeF(1280, 78), Color.Black).Draw();
        new TextElement("Enter / A: skip", new PointF(1260, 30), 0.3f, Color.FromArgb(150, 150, 160), Font.ChaletLondon, Alignment.Right).Draw();
        if (s.Who.Length > 0) new TextElement(s.Who, new PointF(640, 650), 0.34f, Color.FromArgb(200, 170, 255), Font.ChaletLondon, Alignment.Center).Draw();
        if (s.Line.Length > 0) new TextElement(s.Line, new PointF(640, 674), 0.42f, Color.White, Font.ChaletLondon, Alignment.Center).Draw();
        if (Function.Call<bool>(Hash.IS_DISABLED_CONTROL_JUST_PRESSED, 2, (int)GTA.Control.FrontendAccept) || skipKey) { skipKey = false; Say("cutscene " + playingId + " skipped"); EndScene(); return; }
        if (t < 1f) return;
        if (++shotAt >= playing.Count) { EndScene(); return; }
        shotStart = Game.GameTime;
    }

    void EndScene()
    {
        if (cam != null) { World.RenderingCamera = null; cam.Delete(); cam = null; }
        if (playing == null) return;
        playing = null;
        var me = Game.Player.Character;
        Game.Player.CanControlCharacter = true;
        if (me != null && me.Exists()) me.IsInvincible = false;
    }

    static Shot Fly(Vector3 from, Vector3 to, Vector3 look, int ms, string who, string line) { return new Shot { From = from, To = to, Look = look, Ms = ms, Who = who, Line = line }; }

    /// <summary>A slow half circle round a spot, in two shots.</summary>
    static List<Shot> Orbit(Vector3 at, float r, float h, string who, string line)
    {
        var look = at + new Vector3(0, 0, 0.8f);
        Func<float, Vector3> p = a => at + new Vector3((float)Math.Cos(a) * r, (float)Math.Sin(a) * r, h);
        return new List<Shot> { Fly(p(0.2f), p(1.4f), look, 4500, who, line), Fly(p(1.4f), p(2.6f), look, 4500, who, "") };
    }

    /// <summary>Drop in from the sky onto a property, then hold on it.</summary>
    static List<Shot> LabShots(Lab l, string who, string line)
    {
        var look = l.At + new Vector3(0, 0, 1.5f);
        return new List<Shot>
        {
            Fly(l.At + new Vector3(70f, 60f, 45f), l.At + new Vector3(22f, 18f, 9f), look, 4500, "", l.Name.ToUpperInvariant()),
            Fly(l.At + new Vector3(18f, 14f, 6f), l.At + new Vector3(9f, 4f, 3f), look, 5500, who, line),
        };
    }

    List<Shot> RankShots(int r)
    {
        if (r == Ranks.Length - 1) return KingpinShots();
        var shots = Orbit(Game.Player.Character.Position, 6f, 2.2f, "", "RANK UP  ·  " + Ranks[r].ToUpperInvariant());
        shots[1].Who = "Uncle Ray (phone)";
        shots[1].Line = RankLines[r];
        return shots;
    }

    List<Shot> IntroShots(Lab start)
    {
        var city = new Vector3(-75f, -819f, 320f);
        var look = start.At + new Vector3(0, 0, 2f);
        var me = Game.Player.Character.Position;
        return new List<Shot>
        {
            Fly(city + new Vector3(300f, 200f, 120f), city + new Vector3(-100f, -150f, 60f), city - new Vector3(0, 0, 250f), 6000, "", "LOS SANTOS"),
            Fly(start.At + new Vector3(160f, 120f, 90f), start.At + new Vector3(24f, 20f, 10f), look, 6000, "Uncle Ray (phone)", "Kid, it's Ray. I'm out of the game. The house in Strawberry is yours now."),
            Fly(start.At + new Vector3(14f, 10f, 5f), start.At + new Vector3(8f, 3f, 2.5f), look, 6000, "Uncle Ray (phone)", "There's a grow out back and enough supplies to start. Keep it small. Keep it quiet."),
            Fly(me + new Vector3(4f, 4f, 2f), me + new Vector3(2.5f, -2f, 1.6f), me + new Vector3(0, 0, 0.7f), 6000, "Uncle Ray (phone)", "Grow it, package it, sell it. Do it right and people notice. That's the problem, and the point."),
        };
    }

    List<Shot> KingpinShots()
    {
        var city = new Vector3(-75f, -819f, 320f);
        var me = Game.Player.Character.Position;
        return new List<Shot>
        {
            Fly(city + new Vector3(-400f, -300f, 200f), city + new Vector3(250f, -200f, 150f), city - new Vector3(0, 0, 250f), 7000, "", "KINGPIN"),
            Fly(city + new Vector3(250f, -200f, 150f), city + new Vector3(300f, 250f, 120f), city - new Vector3(0, 0, 250f), 6000, "Uncle Ray (phone)", "Kingpin. Los Santos runs on your product now."),
            Fly(me + new Vector3(6f, 6f, 3f), me + new Vector3(3f, -2f, 1.8f), me + new Vector3(0, 0, 0.7f), 6000, "Uncle Ray (phone)", "Just remember how it started. One house in Strawberry."),
        };
    }

    void CutsceneMenu()
    {
        var me = Game.Player.Character;
        var list = new List<KeyValuePair<string, Action>>();
        list.Add(Item("Intro", () => Cutscene("intro", IntroShots(labs.First(l => l.Id == "strawberry")), true)));
        for (int r = 1; r < Ranks.Length; r++) { int rr = r; list.Add(Item("Rank up: " + Ranks[r], () => Cutscene("rank-" + rr, RankShots(rr), true))); }
        foreach (var l in labs) { var lab = l; list.Add(Item("Bought: " + l.Name, () => Cutscene("bought-" + lab.Id, LabShots(lab, "Uncle Ray (phone)", "Yours now. Make it work."), true))); }
        list.Add(Item("First meth", () => Cutscene("first-meth", LabShots(labs.First(l => l.Id == "sandy"), "Uncle Ray (phone)", "That's the real stuff now. Careful who you sell it to."), true)));
        list.Add(Item("First raid", () => Cutscene("first-raid", LabShots(labs[0], "Uncle Ray (phone)", "The cops hit " + labs[0].Name + ". Lay low."), true)));
        list.Add(Item("First turf won", () => Cutscene("first-turf", Orbit(me.Position, 9f, 3f, "Uncle Ray (phone)", "Taking a block is easy. Holding it is the job."), true)));
        Open("CUTSCENES", list);
    }

    // ---------- the look, towards the concept art ----------

    /// <summary>The concept art's film grade over GTA V's own lighting: one of GTA V's own film filters, at a
    /// strength from the app's Graphics setting (Cinematic strongest, Performance off). F5 > Look picks another.</summary>
    void Look()
    {
        Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
        if (graphics == "performance" || look == -2) { Say("look: GTA V's own (" + graphics + ")"); return; }
        float strength = graphics == "cinematic" ? 0.85f : graphics == "balanced" ? 0.4f : 0.6f;
        for (int i = 0; i < Looks.Length; i++)
        {
            string name = Looks[(Math.Max(0, look) + i) % Looks.Length];
            Function.Call(Hash.SET_TIMECYCLE_MODIFIER, name);
            if (Function.Call<int>(Hash.GET_TIMECYCLE_MODIFIER_INDEX) >= 0)
            {
                Function.Call(Hash.SET_TIMECYCLE_MODIFIER_STRENGTH, strength);
                Say("look: " + name + " at " + strength + " (" + graphics + ")");
                return;
            }
            Say("look: " + name + " isn't in this GTA V");
        }
        Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
    }

    void LookMenu()
    {
        var list = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < Looks.Length; i++)
        {
            int n = i;
            list.Add(Item("Film look " + (i + 1) + (look == i || (look == -1 && i == 0) ? "  (on)" : ""), () => { look = n; Look(); Save(); LookMenu(); }));
        }
        list.Add(Item("GTA V's own look" + (look == -2 ? "  (on)" : ""), () => { look = -2; Look(); Save(); LookMenu(); }));
        list.Add(Item("Golden hour now (18:45, clear)", () => { World.CurrentTimeOfDay = new TimeSpan(18, 45, 0); World.Weather = Weather.ExtraSunny; }));
        list.Add(Item("Night now (22:30, clear)", () => { World.CurrentTimeOfDay = new TimeSpan(22, 30, 0); World.Weather = Weather.Clear; }));
        Open("LOOK  ·  graphics: " + graphics, list);
    }

    // ---------- Creative: everything, for testing ----------

    void CreativeMenu()
    {
        var me = Game.Player.Character;
        var list = new List<KeyValuePair<string, Action>>();
        list.Add(Item("+50 of each product", () => { foreach (var p in Products) stock[p] += 50; Save(); }));
        list.Add(Item("Finish every batch now", () => { foreach (var l in labs) if (l.Batch.Length > 0) l.ReadyAt = Game.GameTime; }));
        list.Add(Item("Heat and raids: " + (creativeHeat ? "on" : "off"), () => { creativeHeat = !creativeHeat; if (!creativeHeat) heat = 0; Save(); CreativeMenu(); }));
        list.Add(Item("Set heat to 90 and test a raid", () => { creativeHeat = true; heat = 90; nextRaid = 0; Save(); }));
        string zone = Zone(me.Position);
        var g = GangOf(zone);
        if (g != null && warZone.Length == 0) list.Add(Item("Start a turf war here (" + g.Name + ")", () => BeginWar(zone, g)));
        if (warZone.Length > 0) list.Add(Item("Win this turf war", () => { foreach (var p in warPeds) if (p != null && p.Exists()) p.Kill(); }));
        list.Add(Item("Take every gang's turf", () => { foreach (var x in gangs) foreach (var z in x.Zones) turf.Add(z); Save(); }));
        list.Add(Item("Cutscenes...", CutsceneMenu));
        list.Add(Item("Clear my turf, dealers and gang feelings", () => { turf.Clear(); dealers.Clear(); foreach (var x in gangs) x.Rep = 0; Save(); }));
        foreach (var l in labs)
        {
            var lab = l;
            list.Add(Item("Go to " + l.Name, () => { me.Position = lab.At + new Vector3(0, 0, 0.5f); }));
        }
        Open("CREATIVE TEST TOOLS", list);
    }

    /// <summary>Creative's unlimited money and supplies, topped up every few seconds.</summary>
    void Creative()
    {
        if (!creative || Game.GameTime < nextTopUp) return;
        nextTopUp = Game.GameTime + 5000;
        if (Game.Player.Money < 1000000) Game.Player.Money = 1000000;
        foreach (var l in labs) { l.Owned = true; if (l.Supplies < 99) l.Supplies = 99; }
        if (!creativeHeat) heat = 0;
    }

    // ---------- Story: ranks, unlocks and objectives, like Schedule I ----------

    int Rank { get { int r = 0; for (int i = 0; i < RankXp.Length; i++) if (xp >= RankXp[i]) r = i; return r; } }
    string RankName { get { return creative ? "Creative" : Ranks[Rank]; } }
    bool Unlocked(string what) { int n; return creative || !Needs.TryGetValue(what, out n) || Rank >= n; }
    int BatchMs(string what) { return creative ? 30000 : BatchMinutes[what] * 60000; }

    void AddHeat(int n)
    {
        if (creative && !creativeHeat) return;
        heat = Math.Min(100, heat + n);
    }

    void AddXp(int n, string why)
    {
        if (creative) return;
        int before = Rank;
        xp += n;
        if (Rank > before)
        {
            var now = new List<string>();
            foreach (var kv in Needs) if (kv.Value > before && kv.Value <= Rank) now.Add(kv.Key == "worker" ? "workers" : kv.Key == "dealer" ? "dealers" : kv.Key == "turf" ? "turf wars" : kv.Key == "truce" ? "truces" : kv.Key);
            foreach (var l in labs) if (l.Rank > before && l.Rank <= Rank) { now.Add(l.Name); }
            Blips();
            Screen.ShowSubtitle("~p~Rank up: " + Ranks[Rank] + "~s~" + (now.Count > 0 ? "  ·  Unlocked: " + string.Join(", ", now.ToArray()) : ""), 8000);
            for (int r = before + 1; r <= Rank; r++) Cutscene("rank-" + r, RankShots(r));
            Say("rank " + Ranks[Rank] + " (" + why + ")");
        }
    }

    /// <summary>A new Story empire starts like Schedule I: one small place, a couple of supplies, and a first job.</summary>
    void Begin()
    {
        if (creative) { Creative(); return; }
        if (!fresh) return;
        var start = labs.First(l => l.Id == "strawberry");
        start.Owned = true;
        start.Supplies = 3;
        quest = 0;
        Screen.ShowSubtitle("Your uncle left you a stash house in Strawberry and a few supplies. Start small.", 9000);
        Cutscene("intro", IntroShots(start));
        QuestWaypoint();
        Save();
        Say("new story empire");
    }

    static readonly string[] QuestNames =
    {
        "Go to your stash house in Strawberry",
        "Grow your first weed (press E at the stash house)",
        "Package the weed when it's ready",
        "Sell 5 bags to people on the street",
        "Reach Hoodlum and buy the Grapeseed farm",
        "Reach Peddler, buy the Sandy Shores lab and cook meth",
        "Reach Hustler and hire a worker",
        "Reach Bagman and win a turf war",
        "Reach Enforcer and cook coke at the Elysian Island warehouse",
        "Become Kingpin",
    };
    int QuestCount { get { return QuestNames.Length; } }
    string QuestText() { return quest >= QuestCount ? "You're the Kingpin. Keep growing." : QuestNames[quest]; }

    bool QuestDone(int q)
    {
        var me = Game.Player.Character;
        switch (q)
        {
            case 0: return me.Position.DistanceTo(labs.First(l => l.Id == "strawberry").At) < 6f;
            case 1: return madeCount > 0;
            case 2: return packedCount > 0;
            case 3: return soldCount >= 5;
            case 4: return labs.First(l => l.Id == "grapeseed").Owned;
            case 5: return labs.First(l => l.Id == "sandy").Owned && (stock["meth"] > 0 || labs.Any(l => l.Batch == "meth"));
            case 6: return labs.Any(l => l.Worker);
            case 7: return warsWon > 0;
            case 8: return labs.First(l => l.Id == "docks").Owned && (stock["coke"] > 0 || labs.Any(l => l.Batch == "coke"));
            case 9: return Rank >= Ranks.Length - 1;
        }
        return false;
    }

    void QuestWaypoint()
    {
        string id = quest <= 2 ? "strawberry" : quest == 4 ? "grapeseed" : quest == 5 ? "sandy" : quest == 8 ? "docks" : "";
        var l = labs.FirstOrDefault(x => x.Id == id);
        if (l != null) Function.Call(Hash.SET_NEW_WAYPOINT, l.At.X, l.At.Y);
    }

    void Quests()
    {
        if (creative || quest >= QuestCount || !QuestDone(quest)) return;
        int reward = 500 + quest * 750;
        Game.Player.Money += reward;
        Notification.Show("~g~Objective done:~s~ " + QuestNames[quest] + "  ·  +$" + reward.ToString("N0"));
        AddXp(50 + quest * 25, "objective");
        quest++;
        if (quest < QuestCount) { Notification.Show("Next: " + QuestNames[quest]); QuestWaypoint(); }
        Say("objective " + quest + " done");
        Save();
    }

    static KeyValuePair<string, Action> Item(string text, Action a) { return new KeyValuePair<string, Action>(text, a); }

    void Open(string title, List<KeyValuePair<string, Action>> list) { menuTitle = title; items = list; sel = 0; menuOpen = true; }

    void Menu()
    {
        Game.DisableControlThisFrame(GTA.Control.Phone);
        Game.DisableControlThisFrame(GTA.Control.Attack);
        Game.DisableControlThisFrame(GTA.Control.Aim);
        if (Game.IsControlJustPressed(GTA.Control.PhoneDown)) sel = (sel + 1) % Math.Max(1, items.Count);
        if (Game.IsControlJustPressed(GTA.Control.PhoneUp)) sel = (sel + items.Count - 1) % Math.Max(1, items.Count);
        if (Game.IsControlJustPressed(GTA.Control.PhoneCancel)) { menuOpen = false; return; }
        if (Game.IsControlJustPressed(GTA.Control.PhoneSelect) && items.Count > 0) { menuOpen = false; items[sel].Value(); return; }
        float x = 60, y = 120, w = 620, h = 30;
        new ContainerElement(new PointF(x, y), new SizeF(w, 40), Color.FromArgb(235, 18, 10, 30)).Draw();
        new TextElement(menuTitle, new PointF(x + 12, y + 6), 0.45f, Color.FromArgb(230, 200, 255), Font.ChaletLondon).Draw();
        for (int i = 0; i < items.Count; i++)
        {
            float yy = y + 40 + i * h;
            new ContainerElement(new PointF(x, yy), new SizeF(w, h), i == sel ? Color.FromArgb(235, 120, 60, 220) : Color.FromArgb(215, 12, 8, 20)).Draw();
            new TextElement(items[i].Key, new PointF(x + 12, yy + 4), 0.36f, Color.White, Font.ChaletLondon).Draw();
        }
        new TextElement("Up/Down  ·  Enter / A to pick  ·  Backspace / B to close", new PointF(x + 12, y + 46 + items.Count * h), 0.3f, Color.FromArgb(180, 180, 190), Font.ChaletLondon).Draw();
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (on && playing != null && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)) { skipKey = true; return; }
        if (!on || e.KeyCode != Keys.F5 || playing != null) return;
        if (menuOpen) menuOpen = false; else MainMenu();
    }

    // ---------- HUD, like the concept art ----------

    void Hud(Ped me)
    {
        string zone = Zone(me.Position);
        var g = GangOf(zone);
        float rx = 1280 - 300;
        if (g != null || turf.Contains(zone))
        {
            bool mine = turf.Contains(zone);
            new ContainerElement(new PointF(rx, 18), new SizeF(282, 54), Color.FromArgb(200, 10, 8, 14)).Draw();
            new TextElement("Territory: " + Game.GetLocalizedString(zone), new PointF(rx + 10, 22), 0.42f, Color.White, Font.ChaletLondon).Draw();
            new TextElement(mine ? "Your control" : warZone == zone ? "Turf war" : (g != null ? g.Name + " control" : "Enemy control"), new PointF(rx + 10, 44), 0.32f, mine ? Color.FromArgb(90, 220, 120) : Color.FromArgb(235, 70, 70), Font.ChaletLondon).Draw();
            new ContainerElement(new PointF(rx + 150, 50), new SizeF(120, 8), Color.FromArgb(160, 60, 60, 60)).Draw();
            float share = mine ? 1f : warZone == zone ? 1f - warPeds.Count(p => p.IsAlive) / (float)Math.Max(1, 6 + turf.Count) : 0f;
            new ContainerElement(new PointF(rx + 150, 50), new SizeF(120 * Math.Max(0.05f, share), 8), mine ? Color.FromArgb(90, 220, 120) : Color.FromArgb(235, 60, 60)).Draw();
        }
        // product, supplies and heat, bottom right above the radar's opposite corner
        string line = string.Join("  ·  ", Products.Select(p => Title(p) + " " + stock[p]).ToArray()) + "  ·  Heat " + heat;
        new ContainerElement(new PointF(rx, 720 - 70), new SizeF(282, 26), Color.FromArgb(180, 10, 8, 14)).Draw();
        new TextElement(line, new PointF(rx + 10, 720 - 66), 0.3f, Color.FromArgb(220, 220, 230), Font.ChaletLondon).Draw();
        new ContainerElement(new PointF(rx, 720 - 44), new SizeF(282 * heat / 100f, 4), Color.FromArgb(230, 230, 60, 60)).Draw();
        // mode, rank and the current objective, top left under the radar's opposite corner
        new ContainerElement(new PointF(18, 18), new SizeF(360, creative ? 26 : 50), Color.FromArgb(180, 10, 8, 14)).Draw();
        if (creative)
            new TextElement("CREATIVE  ·  F5 / hold D-pad left: test tools", new PointF(28, 22), 0.32f, Color.FromArgb(200, 170, 255), Font.ChaletLondon).Draw();
        else
        {
            int r = Rank, next = r + 1 < RankXp.Length ? RankXp[r + 1] : RankXp[r];
            new TextElement(Ranks[r] + "  ·  " + xp + (r + 1 < RankXp.Length ? " / " + next + " XP" : " XP"), new PointF(28, 22), 0.32f, Color.FromArgb(200, 170, 255), Font.ChaletLondon).Draw();
            float f = r + 1 < RankXp.Length ? (xp - RankXp[r]) / (float)Math.Max(1, next - RankXp[r]) : 1f;
            new ContainerElement(new PointF(250, 30), new SizeF(118 * Math.Min(1f, f), 6), Color.FromArgb(220, 150, 90, 250)).Draw();
            new TextElement(QuestText(), new PointF(28, 44), 0.28f, Color.White, Font.ChaletLondon).Draw();
        }
        if (warZone.Length > 0)
            new TextElement(warPeds.Count(p => p.IsAlive) + " left  ·  " + Mins(warEnds - Game.GameTime), new PointF(640, 40), 0.5f, Color.FromArgb(255, 90, 90), Font.ChaletLondon, Alignment.Center).Draw();
    }

    void Blips()
    {
        // every property, like the empire map: yours in violet, for sale in white, locked ones grey with the rank they need
        foreach (var l in labs)
        {
            if (l.Blip != null && l.Blip.Exists()) l.Blip.Delete();
            bool locked = !creative && !l.Owned && Rank < l.Rank;
            l.Blip = World.CreateBlip(l.At);
            l.Blip.Sprite = (BlipSprite)l.Sprite;
            l.Blip.Color = l.Owned ? BlipColor.Purple : locked ? BlipColor.Grey : BlipColor.White;
            l.Blip.Name = l.Owned ? "Your " + l.Name : l.Name + "  ·  " + Ranks[l.Rank] + "  ·  $" + l.Price.ToString("N0") + (locked ? " (locked)" : "");
            l.Blip.IsShortRange = false;
            if (locked) l.Blip.Alpha = 150;
        }
        // each gang's turf as a coloured ring; yours turns violet
        foreach (var b in rings) if (b != null && b.Exists()) b.Delete();
        rings.Clear();
        foreach (var g in gangs)
            foreach (var z in g.Zones)
            {
                float[] c;
                if (!ZoneRing.TryGetValue(z, out c)) continue;
                var ring = World.CreateBlip(new Vector3(c[0], c[1], 0f), c[2]);
                bool mine = turf.Contains(z);
                ring.Color = mine ? BlipColor.Purple : g.Ring;
                ring.Alpha = 90;
                ring.Name = Game.GetLocalizedString(z) + "  ·  " + (mine ? "your turf" : g.Name + " turf");
                rings.Add(ring);
            }
    }

    // ---------- helpers ----------

    static string Zone(Vector3 p) { return Function.Call<string>(Hash.GET_NAME_OF_ZONE, p.X, p.Y, p.Z); }
    Gang GangOf(string zone) { return gangs.FirstOrDefault(g => g.Zones.Contains(zone)); }
    static string Title(string s) { return s.Length == 0 ? s : char.ToUpper(s[0]) + s.Substring(1); }
    static string Mins(int ms) { var t = TimeSpan.FromMilliseconds(Math.Max(0, ms)); return t.Minutes + ":" + t.Seconds.ToString("00"); }

    void Cleanup()
    {
        foreach (var l in labs) if (l.Blip != null && l.Blip.Exists()) l.Blip.Delete();
        foreach (var b in rings) if (b != null && b.Exists()) b.Delete();
        Function.Call(Hash.CLEAR_TIMECYCLE_MODIFIER);
        foreach (var p in warPeds) if (p != null && p.Exists()) p.Delete();
        EndScene();
    }

    // ---------- saving: one key=value line each ----------

    string SaveFile { get { return Path.Combine(Dir, creative ? "empire-creative.txt" : "empire.txt"); } }

    void Save()
    {
        try
        {
            var lines = new List<string> { "heat=" + heat, "earned=" + earned, "turf=" + string.Join(",", turf.ToArray()),
                                           "dealers=" + string.Join(",", dealers.Select(kv => kv.Key + ":" + kv.Value).ToArray()),
                                           "xp=" + xp, "quest=" + quest, "made=" + madeCount, "packed=" + packedCount, "sold=" + soldCount,
                                           "wars=" + warsWon, "creativeHeat=" + (creativeHeat ? 1 : 0), "look=" + look, "seen=" + string.Join(",", seen.ToArray()) };
            foreach (var p in Products) lines.Add("stock." + p + "=" + stock[p]);
            foreach (var l in labs)
            {
                int left = l.Batch.Length > 0 ? Math.Max(0, l.ReadyAt - Game.GameTime) : 0;
                lines.Add("lab." + l.Id + "=" + (l.Owned ? 1 : 0) + "|" + (l.Worker ? 1 : 0) + "|" + l.Supplies + "|" + l.Batch + "|" + left);
            }
            foreach (var g in gangs) lines.Add("rep." + g.Id + "=" + g.Rep);
            Directory.CreateDirectory(Dir);
            File.WriteAllLines(SaveFile + ".tmp", lines.ToArray());
            if (File.Exists(SaveFile)) File.Delete(SaveFile);
            File.Move(SaveFile + ".tmp", SaveFile);
        }
        catch (Exception e) { Say("save: " + e.Message); }
    }

    void Load()
    {
        try
        {
            if (!File.Exists(SaveFile)) { fresh = true; return; }
            foreach (var raw in File.ReadAllLines(SaveFile))
            {
                int eq = raw.IndexOf('=');
                if (eq < 0) continue;
                string k = raw.Substring(0, eq), v = raw.Substring(eq + 1);
                int n;
                if (k == "heat" && int.TryParse(v, out n)) heat = n;
                else if (k == "earned" && int.TryParse(v, out n)) earned = n;
                else if (k == "xp" && int.TryParse(v, out n)) xp = n;
                else if (k == "quest" && int.TryParse(v, out n)) quest = n;
                else if (k == "made" && int.TryParse(v, out n)) madeCount = n;
                else if (k == "packed" && int.TryParse(v, out n)) packedCount = n;
                else if (k == "sold" && int.TryParse(v, out n)) soldCount = n;
                else if (k == "wars" && int.TryParse(v, out n)) warsWon = n;
                else if (k == "creativeHeat") creativeHeat = v == "1";
                else if (k == "look" && int.TryParse(v, out n)) look = n;
                else if (k == "seen") foreach (var x in v.Split(',')) { if (x.Length > 0) seen.Add(x); }
                else if (k == "turf") foreach (var z in v.Split(',')) { if (z.Length > 0) turf.Add(z); }
                else if (k == "dealers") foreach (var d in v.Split(',')) { var b = d.Split(':'); if (b.Length == 2 && int.TryParse(b[1], out n)) dealers[b[0]] = n; }
                else if (k.StartsWith("stock.") && stock.ContainsKey(k.Substring(6)) && int.TryParse(v, out n)) stock[k.Substring(6)] = n;
                else if (k.StartsWith("rep.")) { var g = gangs.FirstOrDefault(x => x.Id == k.Substring(4)); if (g != null && int.TryParse(v, out n)) g.Rep = n; }
                else if (k.StartsWith("lab."))
                {
                    var l = labs.FirstOrDefault(x => x.Id == k.Substring(4));
                    var b = v.Split('|');
                    if (l == null || b.Length < 5) continue;
                    l.Owned = b[0] == "1"; l.Worker = b[1] == "1";
                    int.TryParse(b[2], out l.Supplies);
                    l.Batch = b[3];
                    int left; int.TryParse(b[4], out left);
                    l.ReadyAt = Game.GameTime + left;
                }
            }
        }
        catch (Exception e) { Say("load: " + e.Message); }
    }

    void Say(string line)
    {
        log.Add(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + line);
        if (log.Count > 300) log.RemoveRange(0, log.Count - 300);
        try { Directory.CreateDirectory(Dir); File.WriteAllLines(Path.Combine(Dir, "empire-debug.txt"), log.ToArray()); } catch (Exception) { }
    }
}
