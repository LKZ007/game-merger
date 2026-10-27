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
// Also from Schedule I: mixing (up to three ingredients per product, each an effect that changes price and
// addiction), 14 customers who take a free sample, get hooked and text orders, specialist workers (handler, cleaner),
// resting at your properties, and La Sombra's cartel arc (pay 15% or fight: ambushes, three stashes, the boss).
// From Hood Warfare: the La Mesa gun workshop's crates for the gangs, weapon kits (F5 > Arsenal), and a crew you hire,
// gear up and call to fight beside you or leave guarding your turf. Plus a garage of rides to buy, call, upgrade and
// paint (F5 > Vehicles), and Outward-style survival in Story (hunger, thirst, energy; vending machines work).
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
        public string Id, Name, Kind;   // kind: farm (weed), lab (meth, coke), guns (gun crates)
        public Vector3 At;
        public int Price, Rank;         // Story: the rank that puts it up for sale
        public int Sprite;              // its map icon
        public bool Owned, Worker;      // Worker: the botanist, chemist or gunsmith who runs it
        public bool Handler, Cleaner;   // Handler: bigger batches; Cleaner: keeps the place off the police's radar
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

    static readonly string[] Products = { "weed", "meth", "coke" };    // drugs, sold on the street and to customers
    static readonly string[] Goods = { "weed", "meth", "coke", "guns" }; // everything a property makes (guns: crates)
    static readonly Dictionary<string, int> StreetPrice = new Dictionary<string, int> { { "weed", 60 }, { "meth", 140 }, { "coke", 220 }, { "guns", 3000 } };
    static readonly Dictionary<string, int> BatchMinutes = new Dictionary<string, int> { { "weed", 4 }, { "meth", 6 }, { "coke", 8 }, { "guns", 10 } };
    static readonly Dictionary<string, int> BatchSize = new Dictionary<string, int> { { "weed", 12 }, { "meth", 8 }, { "coke", 6 }, { "guns", 4 } };
    static readonly Dictionary<string, int> Addictive = new Dictionary<string, int> { { "weed", 3 }, { "meth", 9 }, { "coke", 7 } };
    const int SupplyPrice = 250, WorkerPrice = 5000, HandlerPrice = 4000, CleanerPrice = 3500, DealerPrice = 2500, WarMinutes = 4;

    // Schedule I's mixing: an ingredient in the batch gives the product an effect, and each effect adds to its price
    // (and to how hooked customers get). Up to three per product; the recipe stays until you change it.
    class Effect { public string Name, Ingredient; public float Value; public int Addict, Cost; }
    static readonly Effect[] Effects =
    {
        new Effect { Name = "Refreshing", Ingredient = "cucumber", Value = 0.10f, Addict = 0, Cost = 2 },
        new Effect { Name = "Calming", Ingredient = "banana", Value = 0.16f, Addict = 0, Cost = 2 },
        new Effect { Name = "Energizing", Ingredient = "energy drink", Value = 0.22f, Addict = 2, Cost = 6 },
        new Effect { Name = "Sneaky", Ingredient = "paracetamol", Value = 0.24f, Addict = 1, Cost = 3 },
        new Effect { Name = "Slippery", Ingredient = "motor oil", Value = 0.34f, Addict = 1, Cost = 6 },
        new Effect { Name = "Spicy", Ingredient = "chili", Value = 0.38f, Addict = 1, Cost = 7 },
        new Effect { Name = "Bright-eyed", Ingredient = "battery", Value = 0.40f, Addict = 3, Cost = 8 },
        new Effect { Name = "Glowing", Ingredient = "glow sticks", Value = 0.48f, Addict = 2, Cost = 9 },
        new Effect { Name = "Toxic", Ingredient = "gasoline", Value = -0.10f, Addict = 6, Cost = 5 },
    };
    readonly Dictionary<string, List<string>> mix = new Dictionary<string, List<string>> { { "weed", new List<string>() }, { "meth", new List<string>() }, { "coke", new List<string>() } };

    readonly List<Lab> labs = new List<Lab>
    {
        new Lab { Id = "grapeseed", Name = "Grapeseed farm", Sprite = 496, Kind = "farm", At = new Vector3(2434f, 4969f, 46.8f), Price = 45000, Rank = 1 },
        new Lab { Id = "sandy", Name = "Sandy Shores trailer lab", Sprite = 499, Kind = "lab", At = new Vector3(1972f, 3817f, 33.4f), Price = 60000, Rank = 2 },
        new Lab { Id = "strawberry", Name = "Strawberry stash house", Sprite = 40, Kind = "farm", At = new Vector3(-14f, -1440f, 31.1f), Price = 30000 },
        new Lab { Id = "docks", Name = "Elysian Island warehouse", Sprite = 473, Kind = "lab", At = new Vector3(155f, -3080f, 5.9f), Price = 90000, Rank = 5 },
        new Lab { Id = "lamesa", Name = "La Mesa gun workshop", Sprite = 110, Kind = "guns", At = new Vector3(716f, -962f, 30.4f), Price = 75000, Rank = 4 },
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
        { "weed", 0 }, { "meth", 2 }, { "worker", 3 }, { "dealer", 4 }, { "turf", 4 }, { "truce", 4 }, { "guns", 4 }, { "crew", 4 }, { "coke", 5 },
    };

    // ---------- state ----------

    readonly Dictionary<string, int> stock = new Dictionary<string, int> { { "weed", 0 }, { "meth", 0 }, { "coke", 0 }, { "guns", 0 } };
    readonly HashSet<string> turf = new HashSet<string>();          // zones you hold
    readonly Dictionary<string, int> dealers = new Dictionary<string, int>();  // zone -> dealers working it
    int heat, earned, lastHour = -1;
    bool on, loaded, creative, creativeHeat, fresh;
    string graphics = "high";
    int look = -1;                  // which film look (-1: the graphics setting's own pick)
    // GTA V's own film filters (the Rockstar Editor's), checked in game: one that isn't there is skipped
    static readonly string[] Looks = { "NG_filmic02", "NG_filmic06", "NG_filmic10", "NG_filmic13", "NG_filmic20" };
    int xp, quest, madeCount, packedCount, soldCount, warsWon, nextTopUp, switchConfirm, mixedCount, cratesSold;
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
            Customers(me);
            CrewTick();
            Survival(me);
            CartelTick(me);
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
                Start(l, l.Kind == "farm" ? "weed" : l.Kind == "guns" ? "guns" : Unlocked("coke") && heat < 60 ? "coke" : "meth", true);
        }
    }

    void Start(Lab l, string what, bool quiet = false)
    {
        if (l.Supplies <= 0) { Notification.Show("~r~No supplies at " + l.Name + "."); return; }
        if (l.Batch.Length > 0) { Notification.Show(l.Name + " is busy."); return; }
        if (!Unlocked(what)) { Notification.Show(Title(what) + " unlocks at " + Ranks[Needs[what]] + "."); return; }
        int ingredients = MixCost(what) * BatchSize[what];
        if (!creative && ingredients > 0)
        {
            if (Game.Player.Money < ingredients) { if (!quiet) Notification.Show("~r~The ingredients for this mix cost $" + ingredients + "."); return; }
            Game.Player.Money -= ingredients;
        }
        if (!creative) l.Supplies--;
        l.Batch = what;
        l.ReadyAt = Game.GameTime + BatchMs(what);
        int h = what == "weed" ? 2 : 5;
        AddHeat(l.Cleaner ? h / 2 : h);
        madeCount++;
        if (mix.ContainsKey(what) && mix[what].Count > 0) mixedCount++;
        if (what != "weed") Cutscene("first-" + what, LabShots(l, "Uncle Ray (phone)", what == "meth" ? "That's the real stuff now. Careful who you sell it to." : "Coke. Rich people's problem, your profit. Keep it clean."));
        if (!quiet) Notification.Show("~g~" + Title(what) + "~s~ started at " + l.Name + ": ready in " + Mins(BatchMs(what)) + "." + (MixText(what).Length > 0 ? " Mix: " + MixText(what) + "." : ""));
        Say(l.Name + ": started " + what + (quiet ? " (worker)" : ""));
        Save();
    }

    void Collect(Lab l, bool quiet = false)
    {
        if (l.Batch.Length == 0) return;
        if (Game.GameTime < l.ReadyAt) { Notification.Show(Title(l.Batch) + " ready in " + Mins(l.ReadyAt - Game.GameTime) + "."); return; }
        int n = l.Handler ? (int)Math.Ceiling(BatchSize[l.Batch] * 1.25) : BatchSize[l.Batch];
        stock[l.Batch] += n;
        packedCount++;
        Notification.Show("~g~+" + n + " " + (l.Batch == "guns" ? "gun crates" : l.Batch) + "~s~ " + (l.Batch == "guns" ? "built" : "packaged") + " at " + l.Name + (quiet ? " by your " + WorkerTitle(l) : "") + ".");
        AddXp(l.Batch == "weed" ? 20 : l.Batch == "meth" ? 40 : l.Batch == "guns" ? 50 : 60, "batch");
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
        int price = (int)(Price(p) * (mine ? 1.3 : 1.0) * (0.8 + rnd.NextDouble() * 0.4));
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
        int income = turf.Count * 150, sold = 0, wages = labs.Count(l => l.Worker) * 100 + labs.Count(l => l.Handler) * 80 + labs.Count(l => l.Cleaner) * 60 + HourlyExtras();
        foreach (var kv in dealers)
            for (int i = 0; i < kv.Value; i++)
            {
                var p = Products.FirstOrDefault(x => stock[x] > 0);
                if (p == null) break;
                stock[p]--;
                income += Price(p);
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
        var lab = labs.Where(l => l.Owned && !l.Cleaner && (l.Supplies > 0 || l.Batch.Length > 0)).OrderBy(l => rnd.Next()).FirstOrDefault();
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
            if (cartel == 2) { Say("La Sombra kept " + g.Id + " off " + z); Save(); return; }
            if (crewGuard && crewHired >= 2 && rnd.Next(100) < 60 + crewGear * 10)
            {
                Notification.Show("~g~" + g.Name + " came for " + Game.GetLocalizedString(z) + ". Your crew held it.");
                if (rnd.Next(3) == 0) { crewHired--; Notification.Show("~r~You lost one of your crew holding it.~s~ " + crewHired + " left."); }
                Save();
                return;
            }
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
        if (CartelInteract(me)) return;
        if (vend != null && vend.Exists() && SurvivalOn)
        {
            Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Buy something to eat or drink");
            if (Game.IsControlJustPressed(GTA.Control.Context)) UseVend();
            return;
        }
        var cust = CustomerNear(me);
        if (cust != null)
        {
            Screen.ShowHelpTextThisFrame(cust.Unlocked && cust.OrderQty > 0 ? "~INPUT_CONTEXT~ Hand " + cust.Name + " " + cust.OrderQty + " " + cust.OrderProduct :
                                         "~INPUT_CONTEXT~ Give " + cust.Name + " a free sample (likes " + cust.Likes.ToLowerInvariant() + ")");
            if (Game.IsControlJustPressed(GTA.Control.Context)) { if (cust.Unlocked) Deliver(cust); else Sample(cust); }
            return;
        }
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
        var who = World.GetNearbyPeds(me, 2.2f).FirstOrDefault(p => p.IsAlive && p.IsHuman && !p.IsPlayer && !p.IsInVehicle() && !served.Contains(p.Handle) && !warPeds.Contains(p) && !crewOut.Contains(p) && !cartelPeds.Contains(p) && !customers.Any(c => c.Ped == p));
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

    // ---------- mixing, roles and gun crates ----------

    static Effect EffectNamed(string n) { return Effects.FirstOrDefault(e => e.Name == n); }
    float MixValue(string p) { List<string> m; return mix.TryGetValue(p, out m) ? m.Sum(n => EffectNamed(n) != null ? EffectNamed(n).Value : 0f) : 0f; }
    int MixCost(string p) { List<string> m; return mix.TryGetValue(p, out m) ? m.Sum(n => EffectNamed(n) != null ? EffectNamed(n).Cost : 0) : 0; }
    int MixAddict(string p) { List<string> m; return mix.TryGetValue(p, out m) ? m.Sum(n => EffectNamed(n) != null ? EffectNamed(n).Addict : 0) : 0; }
    string MixText(string p) { List<string> m; return mix.TryGetValue(p, out m) ? string.Join(", ", m.ToArray()) : ""; }
    int Price(string p) { return (int)Math.Max(1, StreetPrice[p] * (1f + MixValue(p))); }
    static string WorkerTitle(Lab l) { return l.Kind == "farm" ? "botanist" : l.Kind == "guns" ? "gunsmith" : "chemist"; }

    void MixMenu(Lab l, string p)
    {
        var list = new List<KeyValuePair<string, Action>>();
        foreach (var e in Effects)
        {
            var eff = e;
            bool inMix = mix[p].Contains(e.Name);
            list.Add(Item((inMix ? "[x] " : "[ ] ") + e.Name + " (" + e.Ingredient + ", $" + e.Cost + " a bag)  ·  " + (e.Value >= 0 ? "+" : "") + (int)(e.Value * 100) + "% price" + (e.Addict > 2 ? ", very addictive" : ""), () =>
            {
                if (mix[p].Contains(eff.Name)) mix[p].Remove(eff.Name);
                else if (mix[p].Count >= 3) Notification.Show("Three effects at most. Take one out first.");
                else mix[p].Add(eff.Name);
                Save();
                MixMenu(l, p);
                sel = Array.IndexOf(Effects, eff);
            }));
        }
        list.Add(Item("Done  ·  " + Title(p) + " sells for about $" + Price(p) + " a bag", () => LabMenu(l)));
        Open("MIX " + p.ToUpperInvariant() + "  ·  " + (MixText(p).Length > 0 ? MixText(p) : "no effects"), list);
    }

    /// <summary>Gun-running: crates from the La Mesa workshop to a gang. They like you more; the police notice.</summary>
    void SellCrates(Gang g, int each)
    {
        int n = stock["guns"];
        if (n <= 0) return;
        stock["guns"] = 0;
        int pay = n * each;
        Game.Player.Money += pay;
        earned += pay;
        cratesSold += n;
        g.Rep = Math.Min(100, g.Rep + 5 * n);
        AddHeat(8);
        AddXp(60 * n, "gun crates");
        var shop = labs.FirstOrDefault(x => x.Id == "lamesa");
        if (shop != null) Cutscene("first-crates", LabShots(shop, "Uncle Ray (phone)", "Guns. Every gang in the city will want you now. Don't sell to both sides of a fight."));
        Notification.Show("~g~" + g.Name + " paid $" + pay.ToString("N0") + "~s~ for " + n + " gun crates. Relations: " + g.Rep + ".");
        Say("sold " + n + " crates to " + g.Id);
        Save();
    }

    // ---------- customers: samples, orders and addiction, like Schedule I ----------

    class Customer
    {
        public string Id, Name, Area, Likes, Wants;
        public Vector3 At;
        public bool Unlocked;
        public int Addiction, OrderQty, OrderHours;
        public string OrderProduct = "";
        public Ped Ped;
        public Blip Blip;
    }

    static readonly string[] Areas = { "South Los Santos", "East Los Santos", "Blaine County", "Vinewood", "Vespucci" };
    static readonly int[] AreaRank = { 0, 1, 2, 3, 5 };
    static readonly string[] CustomerModels = { "a_m_y_stbla_01", "a_f_y_genhot_01", "a_m_m_afriamer_01", "a_m_y_mexthug_01", "a_f_m_fatbla_01", "a_m_y_hippy_01", "a_m_m_hillbilly_01", "a_f_y_hipster_01", "a_m_y_vinewood_01", "a_m_y_beach_01" };

    readonly List<Customer> customers = new List<Customer>
    {
        new Customer { Id = "darnell", Name = "Darnell", Area = "South Los Santos", At = new Vector3(16f, -1420f, 29f), Likes = "Calming", Wants = "weed" },
        new Customer { Id = "keisha", Name = "Keisha", Area = "South Los Santos", At = new Vector3(-80f, -1820f, 26f), Likes = "Energizing", Wants = "weed" },
        new Customer { Id = "marcus", Name = "Marcus", Area = "South Los Santos", At = new Vector3(110f, -1760f, 29f), Likes = "Spicy", Wants = "meth" },
        new Customer { Id = "tasha", Name = "Tasha", Area = "South Los Santos", At = new Vector3(-160f, -1620f, 33f), Likes = "Refreshing", Wants = "weed" },
        new Customer { Id = "hector", Name = "Hector", Area = "East Los Santos", At = new Vector3(420f, -1880f, 26f), Likes = "Sneaky", Wants = "meth" },
        new Customer { Id = "luz", Name = "Luz", Area = "East Los Santos", At = new Vector3(850f, -2150f, 30f), Likes = "Bright-eyed", Wants = "meth" },
        new Customer { Id = "ramon", Name = "Ramon", Area = "East Los Santos", At = new Vector3(1250f, -1650f, 50f), Likes = "Slippery", Wants = "coke" },
        new Customer { Id = "earl", Name = "Earl", Area = "Blaine County", At = new Vector3(1960f, 3800f, 32f), Likes = "Toxic", Wants = "meth" },
        new Customer { Id = "wes", Name = "Wes", Area = "Blaine County", At = new Vector3(1700f, 4800f, 42f), Likes = "Calming", Wants = "weed" },
        new Customer { Id = "dolly", Name = "Dolly", Area = "Blaine County", At = new Vector3(100f, 3650f, 40f), Likes = "Glowing", Wants = "meth" },
        new Customer { Id = "chad", Name = "Chad", Area = "Vinewood", At = new Vector3(300f, 200f, 104f), Likes = "Energizing", Wants = "coke" },
        new Customer { Id = "jade", Name = "Jade", Area = "Vinewood", At = new Vector3(-50f, 200f, 95f), Likes = "Glowing", Wants = "coke" },
        new Customer { Id = "sky", Name = "Sky", Area = "Vespucci", At = new Vector3(-1200f, -1500f, 4f), Likes = "Refreshing", Wants = "weed" },
        new Customer { Id = "brett", Name = "Brett", Area = "Vespucci", At = new Vector3(-1500f, -500f, 35f), Likes = "Bright-eyed", Wants = "coke" },
    };
    int samplesWon, deliveries, nextCustomers;

    bool AreaOpen(Customer c) { return creative || Rank >= AreaRank[Math.Max(0, Array.IndexOf(Areas, c.Area))]; }
    bool Wanted(Customer c) { return AreaOpen(c) && (!c.Unlocked || c.OrderQty > 0); }

    /// <summary>Customers stand at their spot while they're worth visiting (a sample to win, or an order waiting),
    /// with a map icon: grey for someone who hasn't tried your product yet, yellow for an order.</summary>
    void Customers(Ped me)
    {
        if (Game.GameTime < nextCustomers) return;
        nextCustomers = Game.GameTime + 500;
        foreach (var c in customers)
        {
            bool want = Wanted(c);
            float d = me.Position.DistanceTo(c.At);
            if (want && (c.Ped == null || !c.Ped.Exists()) && d < 70f)
            {
                var model = new Model(CustomerModels[Math.Abs(c.Id.GetHashCode()) % CustomerModels.Length]);
                if (!model.IsInCdImage) model = new Model("a_m_y_genstreet_01");
                if (model.Request(2000))
                {
                    var at = c.At;
                    float ground = World.GetGroundHeight(c.At + new Vector3(0, 0, 50f));
                    if (ground > 0) at.Z = ground;
                    c.Ped = World.CreatePed(model, at);
                    if (c.Ped != null) { c.Ped.BlockPermanentEvents = true; c.Ped.Task.StartScenario("WORLD_HUMAN_SMOKING", 0f); }
                    model.MarkAsNoLongerNeeded();
                }
            }
            if (c.Ped != null && c.Ped.Exists() && (!want || d > 130f)) { c.Ped.Delete(); c.Ped = null; }
            if (want && (c.Blip == null || !c.Blip.Exists()))
            {
                c.Blip = World.CreateBlip(c.At);
                c.Blip.Sprite = BlipSprite.Friend;
                c.Blip.Scale = 0.8f;
            }
            if (!want && c.Blip != null && c.Blip.Exists()) { c.Blip.Delete(); c.Blip = null; }
            if (c.Blip != null && c.Blip.Exists())
            {
                c.Blip.Color = c.Unlocked ? BlipColor.Yellow : BlipColor.Grey;
                c.Blip.Name = c.Unlocked ? "Order: " + c.Name + " wants " + c.OrderQty + " " + c.OrderProduct : "Free sample: " + c.Name + " (" + c.Area + ")";
                c.Blip.IsShortRange = !c.Unlocked;
            }
        }
    }

    Customer CustomerNear(Ped me) { return customers.FirstOrDefault(c => c.Ped != null && c.Ped.Exists() && c.Ped.IsAlive && me.Position.DistanceTo(c.Ped.Position) < 2.5f); }

    void Sample(Customer c)
    {
        var have = Products.Where(x => stock[x] > 0).ToList();
        if (have.Count == 0) { Notification.Show("~r~You've got nothing to give " + c.Name + "."); return; }
        var p = have.Contains(c.Wants) ? c.Wants : have.OrderByDescending(x => mix[x].Contains(c.Likes) ? 1 : 0).First();
        stock[p]--;
        int chance = 35 + (mix[p].Contains(c.Likes) ? 35 : 0) + (p == c.Wants ? 15 : 0) + (int)(Math.Max(0f, MixValue(p)) * 30);
        if (new Random(Game.GameTime).Next(100) < chance)
        {
            c.Unlocked = true;
            c.Addiction = 10;
            samplesWon++;
            Notification.Show(NotificationIcon.Default, c.Name, "New customer", "That's good stuff. I'll text you when I need more.");
            AddXp(40, "new customer");
            if (c.Ped != null && c.Ped.Exists()) Cutscene("first-sample", Orbit(c.Ped.Position, 4f, 1.6f, "Uncle Ray (phone)", "That's how it starts. One taste, and they come back. Keep them happy."));
        }
        else Notification.Show(NotificationIcon.Default, c.Name, "Sample", "Not for me. I like it " + c.Likes.ToLowerInvariant() + ". Try a different mix.");
        Say("sample " + p + " to " + c.Id + ": " + (c.Unlocked ? "won" : "no"));
        Save();
    }

    void Deliver(Customer c)
    {
        if (stock[c.OrderProduct] < c.OrderQty) { Notification.Show("~r~" + c.Name + " wants " + c.OrderQty + " " + c.OrderProduct + ". You have " + stock[c.OrderProduct] + "."); return; }
        stock[c.OrderProduct] -= c.OrderQty;
        bool liked = mix[c.OrderProduct].Contains(c.Likes);
        int pay = (int)(c.OrderQty * Price(c.OrderProduct) * (1f + c.Addiction / 200f) * (liked ? 1.2f : 1f));
        Game.Player.Money += pay;
        earned += pay;
        soldCount += c.OrderQty;
        deliveries++;
        c.Addiction = Math.Min(100, c.Addiction + Addictive[c.OrderProduct] + MixAddict(c.OrderProduct));
        AddHeat(2);
        Notification.Show(NotificationIcon.Default, c.Name, "Paid", "~g~$" + pay.ToString("N0") + "~s~ for " + c.OrderQty + " " + c.OrderProduct + (liked ? ". Just how I like it." : ".") + " Hooked: " + c.Addiction + "%");
        AddXp(15 * c.OrderQty, "delivery");
        c.OrderQty = 0;
        c.OrderProduct = "";
        Say("delivered to " + c.Id + " for $" + pay);
        Save();
    }

    /// <summary>Every in-game hour: hooked customers text orders (the more hooked, the more often and the bigger),
    /// and orders nobody brings run out.</summary>
    void CustomerHour()
    {
        var rnd = new Random(Game.GameTime);
        foreach (var c in customers.Where(x => x.Unlocked && AreaOpen(x)))
        {
            if (c.OrderQty > 0)
            {
                if (--c.OrderHours > 0) continue;
                c.OrderQty = 0;
                c.Addiction = Math.Max(0, c.Addiction - 5);
                Notification.Show(NotificationIcon.Default, c.Name, "Text", "Forget it. I found someone else.");
                continue;
            }
            if (rnd.Next(100) >= Math.Min(70, 15 + c.Addiction / 2)) continue;
            c.OrderProduct = Unlocked(c.Wants) ? c.Wants : "weed";
            c.OrderQty = 1 + rnd.Next(2) + c.Addiction / 35;
            c.OrderHours = 8;
            Notification.Show(NotificationIcon.Default, c.Name, "Text", "You got " + c.OrderProduct + "? I need " + c.OrderQty + ". Come find me (" + c.Area + ").");
        }
    }

    void CustomersMenu()
    {
        var list = new List<KeyValuePair<string, Action>>();
        foreach (var c in customers)
        {
            var cu = c;
            string state = !AreaOpen(c) ? c.Area + " opens at " + Ranks[AreaRank[Array.IndexOf(Areas, c.Area)]] :
                           !c.Unlocked ? "give a free sample" :
                           c.OrderQty > 0 ? "wants " + c.OrderQty + " " + c.OrderProduct + " (" + c.OrderHours + " h left)" : "hooked " + c.Addiction + "%";
            list.Add(Item(c.Name + "  ·  " + c.Area + "  ·  likes " + c.Likes + "  ·  " + state, () =>
            {
                if (!AreaOpen(cu)) return;
                Function.Call(Hash.SET_NEW_WAYPOINT, cu.At.X, cu.At.Y);
                Notification.Show("Waypoint set: " + cu.Name);
            }));
        }
        Open("CUSTOMERS  ·  " + customers.Count(c => c.Unlocked) + " hooked", list);
    }

    // ---------- your crew (Hood Warfare): people who fight for you ----------

    int crewHired, crewGear, crewEver;
    bool crewGuard;
    readonly List<Ped> crewOut = new List<Ped>();
    RelationshipGroup crewGroup;
    const int CrewPrice = 3000;
    static readonly string[] CrewModels = { "g_m_y_famdnf_01", "g_m_y_famfor_01", "g_m_y_strpunk_01", "g_m_y_mexgang_01" };
    static readonly string[] GearNames = { "Street", "Corner", "Soldier", "Elite" };
    static readonly int[] GearPrice = { 0, 8000, 20000, 45000 };
    static readonly WeaponHash[][] GearGuns =
    {
        new[] { WeaponHash.Pistol },
        new[] { WeaponHash.MicroSMG, WeaponHash.Pistol },
        new[] { WeaponHash.AssaultRifle, WeaponHash.Pistol },
        new[] { WeaponHash.CarbineRifle, WeaponHash.PumpShotgun },
    };

    int CrewCap { get { return creative ? 12 : Rank < Needs["crew"] ? 0 : (Rank - 3) * 2; } }

    void Groups(Ped me)
    {
        if (enemies == default(RelationshipGroup) || enemies.Hash == 0)
        {
            enemies = World.AddRelationshipGroup("FRANKENSTEIN_ENEMY");
            enemies.SetRelationshipBetweenGroups(me.RelationshipGroup, Relationship.Hate, true);
        }
        if (crewGroup == default(RelationshipGroup) || crewGroup.Hash == 0)
        {
            crewGroup = World.AddRelationshipGroup("FRANKENSTEIN_CREW");
            crewGroup.SetRelationshipBetweenGroups(me.RelationshipGroup, Relationship.Respect, true);
            crewGroup.SetRelationshipBetweenGroups(enemies, Relationship.Hate, true);
        }
    }

    void CallCrew()
    {
        var me = Game.Player.Character;
        Groups(me);
        crewOut.RemoveAll(p => p == null || !p.Exists() || p.IsDead);
        int want = crewHired - crewOut.Count;
        if (want <= 0) { Notification.Show(crewHired == 0 ? "You haven't hired anyone yet (F5 > Crew)." : "Your whole crew is already here."); return; }
        var rnd = new Random(Game.GameTime);
        for (int i = 0; i < want; i++)
        {
            var model = new Model(CrewModels[rnd.Next(CrewModels.Length)]);
            if (!model.IsInCdImage) model = new Model("g_m_y_famdnf_01");
            if (!model.Request(3000)) continue;
            var p = World.CreatePed(model, me.Position.Around(4f + i));
            model.MarkAsNoLongerNeeded();
            if (p == null) continue;
            p.RelationshipGroup = crewGroup;
            foreach (var gun in GearGuns[crewGear]) p.Weapons.Give(gun, 600, true, true);
            p.Armor = 50 + crewGear * 15;
            p.Accuracy = 45 + crewGear * 12;
            Function.Call(Hash.SET_PED_COMBAT_ABILITY, p, 2);
            Function.Call(Hash.SET_PED_COMBAT_MOVEMENT, p, 2);
            me.PedGroup.Add(p, false);
            var b = p.AddBlip();
            b.Color = BlipColor.Blue;
            b.Scale = 0.6f;
            b.Name = "Your crew";
            crewOut.Add(p);
        }
        Notification.Show("Your crew is with you: " + crewOut.Count + " " + GearNames[crewGear].ToLowerInvariant() + " soldiers.");
        Say("crew called: " + crewOut.Count);
    }

    void SendCrewHome()
    {
        foreach (var p in crewOut) if (p != null && p.Exists()) { if (p.AttachedBlip != null) p.AttachedBlip.Delete(); p.Delete(); }
        crewOut.Clear();
    }

    /// <summary>Losses: a crew member who dies is gone, and you'll have to hire someone new.</summary>
    void CrewTick()
    {
        for (int i = crewOut.Count - 1; i >= 0; i--)
        {
            var p = crewOut[i];
            if (p == null || !p.Exists()) { crewOut.RemoveAt(i); continue; }
            if (!p.IsDead) continue;
            crewOut.RemoveAt(i);
            crewHired = Math.Max(0, crewHired - 1);
            if (p.AttachedBlip != null) p.AttachedBlip.Delete();
            p.MarkAsNoLongerNeeded();
            Notification.Show("~r~You lost one of your crew.~s~ " + crewHired + " left.");
            Save();
        }
    }

    void CrewMenu()
    {
        var list = new List<KeyValuePair<string, Action>>();
        if (CrewCap == 0) { list.Add(Item("A crew of your own unlocks at " + Ranks[Needs["crew"]] + ".", () => { })); Open("CREW", list); return; }
        list.Add(Item("Hire someone ($" + CrewPrice.ToString("N0") + ", $150 an hour)  ·  " + crewHired + " / " + CrewCap, () =>
        {
            if (crewHired >= CrewCap) { Notification.Show("That's as many as you can run at " + RankName + "."); return; }
            if (Game.Player.Money < CrewPrice) { Notification.Show("~r~Not enough money."); return; }
            Game.Player.Money -= CrewPrice; crewHired++; crewEver++; Save(); CrewMenu();
        }));
        list.Add(Item("Call your crew (" + crewHired + ")", CallCrew));
        if (crewOut.Count > 0) list.Add(Item("Send your crew home", SendCrewHome));
        if (crewGear + 1 < GearNames.Length)
            list.Add(Item("Gear: " + GearNames[crewGear] + " -> " + GearNames[crewGear + 1] + " ($" + GearPrice[crewGear + 1].ToString("N0") + ")", () =>
            {
                if (Game.Player.Money < GearPrice[crewGear + 1]) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= GearPrice[crewGear + 1]; crewGear++; SendCrewHome(); Save();
                Notification.Show("Your crew now carries " + GearNames[crewGear].ToLowerInvariant() + " gear. Call them to see it.");
            }));
        list.Add(Item("Guard your turf while they're home: " + (crewGuard ? "on" : "off"), () => { crewGuard = !crewGuard; Save(); CrewMenu(); }));
        Open("CREW  ·  " + crewHired + " soldiers, " + GearNames[crewGear] + " gear", list);
    }

    // ---------- the arsenal (Hood Warfare's guns, by rank) ----------

    static readonly string[] KitNames = { "Street kit (pistol, knife, bat)", "Corner kit (micro SMG, sawn-off, .50 pistol)", "Soldier kit (assault rifle, carbine, pump shotgun, grenades)", "Heavy kit (RPG, minigun, combat MG, sticky bombs)" };
    static readonly int[] KitRank = { 0, 2, 4, 7 }, KitPrice = { 1500, 6000, 18000, 60000 };
    static readonly WeaponHash[][] Kits =
    {
        new[] { WeaponHash.Pistol, WeaponHash.Knife, WeaponHash.Bat },
        new[] { WeaponHash.MicroSMG, WeaponHash.SawnOffShotgun, WeaponHash.Pistol50 },
        new[] { WeaponHash.AssaultRifle, WeaponHash.CarbineRifle, WeaponHash.PumpShotgun, WeaponHash.Grenade },
        new[] { WeaponHash.RPG, WeaponHash.Minigun, WeaponHash.CombatMG, WeaponHash.StickyBomb },
    };

    void ArsenalMenu()
    {
        var me = Game.Player.Character;
        var list = new List<KeyValuePair<string, Action>>();
        for (int i = 0; i < Kits.Length; i++)
        {
            int k = i;
            bool open = creative || Rank >= KitRank[i];
            list.Add(Item(open ? KitNames[i] + "  ·  $" + KitPrice[i].ToString("N0") : KitNames[i] + "  ·  unlocks at " + Ranks[KitRank[i]], () =>
            {
                if (!(creative || Rank >= KitRank[k])) return;
                if (!creative && Game.Player.Money < KitPrice[k]) { Notification.Show("~r~Not enough money."); return; }
                if (!creative) Game.Player.Money -= KitPrice[k];
                foreach (var w in Kits[k]) me.Weapons.Give(w, w == WeaponHash.RPG || w == WeaponHash.Grenade || w == WeaponHash.StickyBomb ? 20 : 600, false, true);
                Notification.Show("~g~" + KitNames[k].Split('(')[0].Trim() + "~s~ in your pockets.");
            }));
        }
        list.Add(Item("Body armour  ·  $500", () =>
        {
            if (!creative && Game.Player.Money < 500) { Notification.Show("~r~Not enough money."); return; }
            if (!creative) Game.Player.Money -= 500;
            me.Armor = 100;
        }));
        Open("ARSENAL", list);
    }

    // ---------- vehicles: buy, own, call and upgrade ----------

    class Ride { public string Model, Name; public int Price, Rank; }
    static readonly Ride[] Rides =
    {
        new Ride { Model = "sanchez", Name = "Sanchez dirt bike", Price = 8000, Rank = 0 },
        new Ride { Model = "speedo", Name = "Speedo delivery van", Price = 12000, Rank = 0 },
        new Ride { Model = "chino2", Name = "Chino lowrider", Price = 15000, Rank = 1 },
        new Ride { Model = "buccaneer2", Name = "Buccaneer lowrider", Price = 20000, Rank = 1 },
        new Ride { Model = "faction2", Name = "Faction lowrider", Price = 25000, Rank = 2 },
        new Ride { Model = "moonbeam2", Name = "Moonbeam custom van", Price = 22000, Rank = 2 },
        new Ride { Model = "bati", Name = "Bati 801 bike", Price = 25000, Rank = 2 },
        new Ride { Model = "granger", Name = "Granger SUV", Price = 35000, Rank = 3 },
        new Ride { Model = "baller2", Name = "Baller", Price = 60000, Rank = 3 },
        new Ride { Model = "dubsta2", Name = "Dubsta", Price = 80000, Rank = 4 },
        new Ride { Model = "kuruma2", Name = "Armoured Kuruma", Price = 150000, Rank = 5 },
        new Ride { Model = "insurgent2", Name = "Insurgent", Price = 250000, Rank = 7 },
        new Ride { Model = "t20", Name = "T20 supercar", Price = 500000, Rank = 9 },
    };
    readonly HashSet<string> garage = new HashSet<string>();
    Vehicle called;
    int paint;
    static readonly VehicleColor[] Paints = { VehicleColor.MetallicPurple, VehicleColor.MetallicBlack, VehicleColor.PureGold, VehicleColor.MattePurple };
    static readonly string[] PaintNames = { "purple", "black", "gold", "matte purple" };

    Vehicle SpawnRide(Ride r)
    {
        var me = Game.Player.Character;
        var model = new Model(r.Model);
        if (!model.IsInCdImage || !model.IsVehicle) { Notification.Show("~r~The " + r.Name + " isn't in your copy of GTA V."); return null; }
        if (!model.Request(5000)) { Notification.Show("~r~The " + r.Name + " didn't load. Try again."); return null; }
        if (called != null && called.Exists() && me.CurrentVehicle != called) called.Delete();
        var at = World.GetNextPositionOnStreet(me.Position.Around(15f));
        called = World.CreateVehicle(model, at, me.Heading);
        model.MarkAsNoLongerNeeded();
        if (called == null) return null;
        called.Mods.InstallModKit();
        called.Mods.PrimaryColor = Paints[paint];
        called.Mods.SecondaryColor = VehicleColor.MetallicBlack;
        var b = called.AddBlip();
        b.Sprite = BlipSprite.PersonalVehicleCar;
        b.Color = BlipColor.Purple;
        b.Name = "Your " + r.Name;
        Notification.Show("Your ~p~" + r.Name + "~s~ is parked nearby.");
        return called;
    }

    void VehiclesMenu()
    {
        var me = Game.Player.Character;
        var list = new List<KeyValuePair<string, Action>>();
        var cur = me.CurrentVehicle;
        if (cur != null && cur.Exists())
        {
            list.Add(Item("Upgrade this one: engine, brakes, gearbox, suspension, armour ($8,000)", () =>
            {
                if (!creative && Game.Player.Money < 8000) { Notification.Show("~r~Not enough money."); return; }
                if (!creative) Game.Player.Money -= 8000;
                cur.Mods.InstallModKit();
                foreach (var t in new[] { VehicleModType.Engine, VehicleModType.Brakes, VehicleModType.Transmission, VehicleModType.Suspension, VehicleModType.Armor })
                    if (cur.Mods[t].Count > 0) cur.Mods[t].Index = cur.Mods[t].Count - 1;
                Notification.Show("~g~Upgraded.");
            }));
            list.Add(Item("Paint this one " + PaintNames[(paint + 1) % Paints.Length] + " ($1,000)", () =>
            {
                if (!creative && Game.Player.Money < 1000) { Notification.Show("~r~Not enough money."); return; }
                if (!creative) Game.Player.Money -= 1000;
                paint = (paint + 1) % Paints.Length;
                cur.Mods.PrimaryColor = Paints[paint];
                Save();
            }));
        }
        foreach (var r in Rides)
        {
            var ride = r;
            bool owned = garage.Contains(r.Model), open = creative || Rank >= r.Rank;
            list.Add(Item(owned ? "Call your " + r.Name : open ? "Buy the " + r.Name + "  ·  $" + r.Price.ToString("N0") : r.Name + "  ·  unlocks at " + Ranks[r.Rank], () =>
            {
                if (owned || creative) { SpawnRide(ride); return; }
                if (!open) return;
                if (Game.Player.Money < ride.Price) { Notification.Show("~r~Not enough money."); return; }
                if (SpawnRide(ride) == null) return;
                Game.Player.Money -= ride.Price;
                garage.Add(ride.Model);
                Save();
            }));
        }
        Open("VEHICLES  ·  " + garage.Count + " owned", list);
    }

    // ---------- survival (Outward-style): hunger, thirst and energy ----------

    float hunger = 100f, thirst = 100f, energy = 100f;
    int snacks = 2, drinks = 2, restCount, nextSurvival, nextHurt, nextStumble, nextVend;
    bool creativeSurvival, shaking;
    Prop vend;
    static readonly Model[] Vending =
    {
        new Model("prop_vend_soda_01"), new Model("prop_vend_soda_02"), new Model("prop_vend_snak_01"), new Model("prop_vend_coffe_01"),
        new Model("prop_vend_water_01"), new Model("prop_burgerstand_01"), new Model("prop_hotdogstand_01"),
    };

    bool SurvivalOn { get { return creative ? creativeSurvival : true; } }

    void Survival(Ped me)
    {
        if (!SurvivalOn || me.IsDead) { if (shaking) { Function.Call(Hash.STOP_GAMEPLAY_CAM_SHAKING, true); shaking = false; } return; }
        if (hunger < 15f || thirst < 15f) Game.DisableControlThisFrame(GTA.Control.Sprint);
        if (Game.GameTime >= nextVend)
        {
            nextVend = Game.GameTime + 300;
            vend = me.IsInVehicle() ? null : World.GetNearbyProps(me.Position, 1.8f, Vending).FirstOrDefault();
        }
        if (Game.GameTime < nextSurvival) return;
        float dt = nextSurvival == 0 ? 1f : 1f + (Game.GameTime - nextSurvival) / 1000f;
        nextSurvival = Game.GameTime + 1000;
        bool sprint = me.IsSprinting;
        hunger = Math.Max(0f, hunger - dt * 100f / 2880f);                       // empty in 48 minutes
        thirst = Math.Max(0f, thirst - dt * 100f / 1920f * (sprint ? 2f : 1f));  // 32 minutes, faster when sprinting
        energy = Math.Max(0f, energy - dt * 100f / 3600f * (sprint ? 1.5f : 1f)); // an hour
        if ((hunger <= 0f || thirst <= 0f) && Game.GameTime >= nextHurt && me.HealthFloat > me.MaxHealthFloat * 0.25f)
        {
            nextHurt = Game.GameTime + 4000;
            me.ApplyDamage(3);
            if (Game.GameTime % 5 == 0) Screen.ShowSubtitle(hunger <= 0f ? "You're starving. Eat something (F5)." : "You're parched. Drink something (F5).", 3000);
        }
        if (energy < 10f && !shaking) { Function.Call(Hash.SHAKE_GAMEPLAY_CAM, "DRUNK_SHAKE", 0.5f); shaking = true; }
        if (energy >= 10f && shaking) { Function.Call(Hash.STOP_GAMEPLAY_CAM_SHAKING, true); shaking = false; }
        if (energy < 5f && Game.GameTime >= nextStumble && me.IsOnFoot && !me.IsInVehicle())
        {
            nextStumble = Game.GameTime + 25000;
            me.Ragdoll(1500, RagdollType.Relax);
            Screen.ShowSubtitle("You're exhausted. Rest at one of your properties.", 3000);
        }
    }

    void UseVend()
    {
        string m = vend.Model.Hash == new Model("prop_vend_snak_01").Hash || vend.Model.Hash == new Model("prop_burgerstand_01").Hash || vend.Model.Hash == new Model("prop_hotdogstand_01").Hash ? "food" :
                   vend.Model.Hash == new Model("prop_vend_coffe_01").Hash ? "coffee" : "drink";
        int cost = m == "food" ? 6 : 3;
        if (Game.Player.Money < cost) { Notification.Show("~r~Not enough cash."); return; }
        Game.Player.Money -= cost;
        if (m == "food") hunger = Math.Min(100f, hunger + 35f);
        else thirst = Math.Min(100f, thirst + 40f);
        if (m == "coffee") energy = Math.Min(100f, energy + 15f);
        Notification.Show(m == "food" ? "Ate something. ~g~Hunger " + (int)hunger : "Had a " + m + ". ~g~Thirst " + (int)thirst);
    }

    void Eat() { if (snacks <= 0) { Notification.Show("No snacks left. Stock up at a property or a vending machine."); return; } snacks--; hunger = Math.Min(100f, hunger + 35f); Save(); }
    void Drink() { if (drinks <= 0) { Notification.Show("No drinks left. Stock up at a property or a vending machine."); return; } drinks--; thirst = Math.Min(100f, thirst + 40f); Save(); }

    bool CanRest(Lab l) { return l.Owned; }

    void Rest(Lab l)
    {
        Screen.FadeOut(800);
        Wait(1000);
        World.CurrentTimeOfDay = World.CurrentTimeOfDay.Add(TimeSpan.FromHours(6));
        energy = 100f;
        hunger = Math.Max(0f, hunger - 10f);
        thirst = Math.Max(0f, thirst - 15f);
        restCount++;
        Save();
        Wait(300);
        Screen.FadeIn(800);
        Notification.Show("You slept 6 hours at " + l.Name + ". ~g~Energy 100.");
    }

    void StockUp()
    {
        if (!creative && Game.Player.Money < 50) { Notification.Show("~r~Not enough money."); return; }
        if (!creative) Game.Player.Money -= 50;
        snacks += 5;
        drinks += 5;
        Save();
        Notification.Show("Snacks " + snacks + ", drinks " + drinks + ". Eat and drink from the F5 menu.");
    }

    // ---------- La Sombra: the cartel story ----------

    int cartel, cartelMask, nextAmbush, cartelCheck;   // cartel: 0 not met, 1 meeting set, 2 paying, 3 at war, 4 beaten
    readonly List<Ped> cartelPeds = new List<Ped>();
    Ped boss;
    Blip cartelBlip;
    int stashUp = -1;
    static readonly Vector3 CartelMeet = new Vector3(1733f, 3290f, 41.1f);
    static readonly Vector3[] Stashes = { new Vector3(2350f, 3130f, 48f), new Vector3(1530f, -2110f, 77f), new Vector3(-1135f, 4925f, 220f) };
    static readonly string[] StashNames = { "the Grand Senora scrapyard", "the Murrieta oil fields", "the Chiliad hills camp" };
    static readonly Vector3 CartelBoss = new Vector3(1394.6f, 1141.7f, 114.3f);

    void Ray(string text) { Notification.Show(NotificationIcon.MpRayLavoy, "Uncle Ray", "Phone", text); }
    void Sombra(string text) { Notification.Show(NotificationIcon.MpMexBoss, "La Sombra", "Message", text); }

    Ped CartelPed(Vector3 at, bool armed, bool hostile, string model = "g_m_y_mexgoon_02")
    {
        var me = Game.Player.Character;
        Groups(me);
        var m = new Model(model);
        if (!m.IsInCdImage) m = new Model("g_m_y_mexgoon_01");
        if (!m.Request(3000)) return null;
        float ground = World.GetGroundHeight(at + new Vector3(0, 0, 50f));
        if (ground > 0) at.Z = ground;
        var p = World.CreatePed(m, at);
        m.MarkAsNoLongerNeeded();
        if (p == null) return null;
        if (armed) p.Weapons.Give(new Random(p.Handle).Next(2) == 0 ? WeaponHash.AssaultRifle : WeaponHash.MicroSMG, 600, true, true);
        p.Armor = 50;
        p.Accuracy = 40;
        if (hostile)
        {
            p.RelationshipGroup = enemies;
            p.Task.FightAgainst(me);
            var b = p.AddBlip();
            b.Color = BlipColor.Red;
            b.Scale = 0.7f;
        }
        else p.BlockPermanentEvents = true;
        cartelPeds.Add(p);
        return p;
    }

    void ClearCartel(bool delete)
    {
        foreach (var p in cartelPeds) if (p != null && p.Exists()) { if (p.AttachedBlip != null) p.AttachedBlip.Delete(); if (delete) p.Delete(); else p.MarkAsNoLongerNeeded(); }
        cartelPeds.Clear();
        boss = null;
        stashUp = -1;
    }

    void CartelBlip(Vector3 at, string name)
    {
        if (cartelBlip != null && cartelBlip.Exists()) cartelBlip.Delete();
        cartelBlip = null;
        if (name.Length == 0) return;
        cartelBlip = World.CreateBlip(at);
        cartelBlip.Sprite = BlipSprite.Enemy;
        cartelBlip.Color = BlipColor.Red;
        cartelBlip.Name = name;
    }

    void StartCartel()
    {
        cartel = 1;
        Ray("Kid, La Sombra want a word. Cartel. Meet them at the Sandy Shores airfield, and go easy.");
        CartelBlip(CartelMeet, "La Sombra: the meeting");
        Save();
    }

    void CartelWar()
    {
        cartel = 3;
        Sombra("You made your choice. Now you'll lose everything.");
        Ray("They'll come for you now. Hit their stashes before they hit you. I've marked them.");
        nextAmbush = Game.GameTime + 240000;
        Save();
    }

    /// <summary>The cartel arc: a meeting, a choice (pay 15% or fight), stashes to hit, and the boss at the ranch.</summary>
    void CartelTick(Ped me)
    {
        if (cartel == 0 && !creative && Rank >= Needs["crew"] && warZone.Length == 0) StartCartel();
        if (Game.GameTime < cartelCheck) return;
        cartelCheck = Game.GameTime + 500;
        cartelPeds.RemoveAll(p => p == null || !p.Exists());
        if (cartel == 1)
        {
            if (cartelBlip == null) CartelBlip(CartelMeet, "La Sombra: the meeting");
            float d = me.Position.DistanceTo(CartelMeet);
            if (d < 80f && cartelPeds.Count == 0)
            {
                boss = CartelPed(CartelMeet, false, false, "g_m_m_mexboss_01");
                CartelPed(CartelMeet + new Vector3(2f, 1f, 0f), true, false);
                CartelPed(CartelMeet + new Vector3(-2f, 1f, 0f), true, false);
            }
            if (d > 200f && cartelPeds.Count > 0) ClearCartel(true);
            return;
        }
        if (cartel == 3)
        {
            // their stashes, one at a time, then the boss
            int next = Enumerable.Range(0, Stashes.Length).FirstOrDefault(i => (cartelMask & (1 << i)) == 0);
            bool stashesLeft = Enumerable.Range(0, Stashes.Length).Any(i => (cartelMask & (1 << i)) == 0);
            var target = stashesLeft ? Stashes[next] : CartelBoss;
            if (cartelBlip == null || !cartelBlip.Exists() || cartelBlip.Position.DistanceTo(target) > 5f)
                CartelBlip(target, stashesLeft ? "La Sombra's stash: " + StashNames[next] : "La Sombra's boss: the ranch");
            float d = me.Position.DistanceTo(target);
            if (d < 130f && cartelPeds.Count == 0 && (stashesLeft ? stashUp != next : boss == null))
            {
                for (int i = 0; i < (stashesLeft ? 5 : 8); i++) CartelPed(target.Around(6f + i * 2f), true, true);
                if (stashesLeft) stashUp = next;
                else
                {
                    boss = CartelPed(target, true, true, "g_m_m_mexboss_01");
                    if (boss != null) { boss.Armor = 100; boss.MaxHealth = 600; boss.Health = 600; boss.Weapons.Give(WeaponHash.CombatMG, 900, true, true); }
                }
            }
            if (!stashesLeft && boss != null && boss.Exists() && boss.IsDead)
            {
                cartel = 4;
                ClearCartel(false);
                CartelBlip(Vector3.Zero, "");
                Game.Player.Money += 100000;
                earned += 100000;
                AddXp(1500, "La Sombra");
                Cutscene("cartel-beaten", Orbit(me.Position, 9f, 3f, "Uncle Ray (phone)", "La Sombra is finished. Nobody runs this state but you now."));
                Notification.Show("~g~La Sombra is finished.~s~ +$100,000");
                Say("cartel beaten");
                Save();
                return;
            }
            // ambushes while the war is on
            if (Game.GameTime > nextAmbush && warZone.Length == 0 && me.IsOnFoot && cartelPeds.Count == 0 && playing == null)
            {
                nextAmbush = Game.GameTime + 360000 + new Random(Game.GameTime).Next(180000);
                for (int i = 0; i < 4; i++) CartelPed(me.Position.Around(45f + i * 4f), true, true);
                Sombra("Found you.");
                Say("cartel ambush");
            }
            if (d > 250f && stashUp >= 0 && cartelPeds.Count > 0 && cartelPeds.All(p => p.Position.DistanceTo(me.Position) > 250f)) ClearCartel(true);
        }
    }

    bool CartelInteract(Ped me)
    {
        if (cartel == 1 && boss != null && boss.Exists() && me.Position.DistanceTo(boss.Position) < 3f)
        {
            Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Hear La Sombra out");
            if (Game.IsControlJustPressed(GTA.Control.Context))
            {
                Cutscene("cartel-meet", Orbit(boss.Position, 5f, 1.8f, "La Sombra", "Your product is in our state. Fifteen percent of everything, or you're finished."), true);
                var list = new List<KeyValuePair<string, Action>>
                {
                    Item("Pay La Sombra 15% of everything (they keep the gangs off your turf)", () =>
                    {
                        cartel = 2; ClearCartel(false); CartelBlip(Vector3.Zero, "");
                        Sombra("Smart. We'll be watching your books."); Save();
                    }),
                    Item("Tell them to get lost", () =>
                    {
                        foreach (var p in cartelPeds.ToList()) if (p != null && p.Exists()) { p.RelationshipGroup = enemies; p.Task.FightAgainst(me); }
                        CartelWar();
                    }),
                };
                Open("LA SOMBRA", list);
            }
            return true;
        }
        if (cartel == 3 && stashUp >= 0 && (cartelMask & (1 << stashUp)) == 0 && me.Position.DistanceTo(Stashes[stashUp]) < 4f && cartelPeds.All(p => !p.Exists() || p.IsDead))
        {
            Screen.ShowHelpTextThisFrame("~INPUT_CONTEXT~ Take La Sombra's stash");
            if (Game.IsControlJustPressed(GTA.Control.Context))
            {
                cartelMask |= 1 << stashUp;
                stock["coke"] += 20;
                Game.Player.Money += 15000;
                earned += 15000;
                AddXp(400, "cartel stash");
                Cutscene("first-stash", Orbit(me.Position, 7f, 2.5f, "Uncle Ray (phone)", "That'll hurt them. They'll want it back, so watch your back."));
                Notification.Show("~g~Stash taken:~s~ $15,000 and 20 coke. " + (3 - Enumerable.Range(0, 3).Count(i => (cartelMask & (1 << i)) != 0)) + " left.");
                if (Enumerable.Range(0, Stashes.Length).All(i => (cartelMask & (1 << i)) != 0)) Ray("That's all their stashes. The boss is holed up at a ranch in the hills. End it.");
                ClearCartel(false);
                Save();
            }
            return true;
        }
        return false;
    }

    /// <summary>What the hour costs on top of the labs' wages: the crew, and La Sombra's cut if you pay it.</summary>
    int HourlyExtras()
    {
        CustomerHour();
        int crewWages = crewHired * 150;
        int tribute = cartel == 2 ? Math.Max(0, (turf.Count * 150 + dealers.Values.Sum() * 100) * 15 / 100) : 0;
        return crewWages + tribute;
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
            if (l.Batch.Length > 0) list.Add(Item(Game.GameTime >= l.ReadyAt ? (l.Batch == "guns" ? "Crate up the guns" : "Package the " + l.Batch) : Title(l.Batch) + ": " + Mins(l.ReadyAt - Game.GameTime) + " left", () => Collect(l)));
            else if (l.Kind == "farm") list.Add(Item("Grow weed (1 supply, " + Mins(BatchMs("weed")) + ")", () => Start(l, "weed")));
            else if (l.Kind == "guns") list.Add(Item("Build gun crates (1 parts supply, " + Mins(BatchMs("guns")) + ")", () => Start(l, "guns")));
            else
            {
                list.Add(Item(Unlocked("meth") ? "Cook meth (1 supply, " + Mins(BatchMs("meth")) + ")" : "Meth: unlocks at " + Ranks[Needs["meth"]], () => Start(l, "meth")));
                list.Add(Item(Unlocked("coke") ? "Cook coke (1 supply, " + Mins(BatchMs("coke")) + ")" : "Coke: unlocks at " + Ranks[Needs["coke"]], () => Start(l, "coke")));
            }
            if (l.Kind == "guns")
            {
                if (stock["guns"] > 0)
                    foreach (var g in gangs)
                    {
                        var buyer = g;
                        int each = 3000 + Math.Max(0, g.Rep) * 20;
                        list.Add(Item("Sell " + stock["guns"] + " crates to " + g.Name + " ($" + each.ToString("N0") + " each)", () => SellCrates(buyer, each)));
                    }
            }
            else
                foreach (var p in Products.Where(x => (l.Kind == "farm") == (x == "weed")))
                {
                    var prod = p;
                    list.Add(Item(Unlocked(p) ? "Mix for " + p + ": " + (MixText(p).Length > 0 ? MixText(p) + " (x" + (1f + MixValue(p)).ToString("0.00") + ")" : "nothing yet") : Title(p) + " mixing: unlocks at " + Ranks[Needs[p]], () => { if (Unlocked(prod)) MixMenu(l, prod); }));
                }
            list.Add(Item("Buy 5 supplies ($" + (5 * SupplyPrice) + ")  ·  has " + l.Supplies, () =>
            {
                if (Game.Player.Money < 5 * SupplyPrice) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= 5 * SupplyPrice; l.Supplies += 5; Save();
            }));
            if (!l.Worker) list.Add(Item(Unlocked("worker") ? "Hire a " + WorkerTitle(l) + " ($" + WorkerPrice + ", $100 an hour): runs it by themselves" : "Workers: unlock at " + Ranks[Needs["worker"]], () =>
            {
                if (!Unlocked("worker")) return;
                if (Game.Player.Money < WorkerPrice) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= WorkerPrice; l.Worker = true; Save();
                Notification.Show("Your " + WorkerTitle(l) + " keeps " + l.Name + " running while there are supplies.");
            }));
            if (!l.Handler) list.Add(Item(Unlocked("worker") ? "Hire a handler ($" + HandlerPrice + ", $80 an hour): 25% bigger batches" : "Handlers: unlock at " + Ranks[Needs["worker"]], () =>
            {
                if (!Unlocked("worker")) return;
                if (Game.Player.Money < HandlerPrice) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= HandlerPrice; l.Handler = true; Save();
                Notification.Show("Your handler packs 25% more out of every batch at " + l.Name + ".");
            }));
            if (!l.Cleaner) list.Add(Item(Unlocked("worker") ? "Hire a cleaner ($" + CleanerPrice + ", $60 an hour): half the heat, no raids here" : "Cleaners: unlock at " + Ranks[Needs["worker"]], () =>
            {
                if (!Unlocked("worker")) return;
                if (Game.Player.Money < CleanerPrice) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= CleanerPrice; l.Cleaner = true; Save();
                Notification.Show("Your cleaner keeps " + l.Name + " spotless. The police won't find a thing.");
            }));
            if (CanRest(l)) list.Add(Item("Rest here for 6 hours", () => Rest(l)));
            if (CanRest(l)) list.Add(Item("Stock up: 5 snacks and 5 drinks ($50)", () => StockUp()));
        }
        Open(l.Name, list);
    }

    void MainMenu()
    {
        var me = Game.Player.Character;
        var list = new List<KeyValuePair<string, Action>>();
        if (creative) list.Add(Item("Creative test tools...", CreativeMenu));
        else list.Add(Item("Objective: " + QuestText(), () => QuestWaypoint()));
        list.Add(Item("Customers (" + customers.Count(c => c.Unlocked) + " hooked, " + customers.Count(c => c.OrderQty > 0) + " orders)...", CustomersMenu));
        list.Add(Item("Crew (" + crewHired + ")...", CrewMenu));
        list.Add(Item("Arsenal...", ArsenalMenu));
        list.Add(Item("Vehicles (" + garage.Count + ")...", VehiclesMenu));
        if (SurvivalOn)
        {
            list.Add(Item("Eat a snack (" + snacks + " left)  ·  hunger " + (int)hunger, Eat));
            list.Add(Item("Drink (" + drinks + " left)  ·  thirst " + (int)thirst, Drink));
        }
        if (cartel == 2) list.Add(Item("Stop paying La Sombra (war)", () => { menuOpen = false; CartelWar(); }));
        foreach (var l in labs)
        {
            var lab = l;
            list.Add(Item((l.Owned ? "Your " : !creative && Rank < l.Rank ? "Locked (" + Ranks[l.Rank] + "): " : "For sale: ") + l.Name + (l.Owned ? "  ·  " + (l.Batch.Length > 0 ? l.Batch + " " + (Game.GameTime >= l.ReadyAt ? "ready" : Mins(l.ReadyAt - Game.GameTime)) : "idle") + ", " + l.Supplies + " supplies" + (l.Worker ? ", " + WorkerTitle(l) : "") + (l.Handler ? ", handler" : "") + (l.Cleaner ? ", cleaner" : "") : "  ·  $" + l.Price.ToString("N0")),
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
        list.Add(Item("First customer", () => Cutscene("first-sample", Orbit(me.Position, 4f, 1.6f, "Uncle Ray (phone)", "That's how it starts. One taste, and they come back."), true)));
        list.Add(Item("La Sombra's meeting", () => Cutscene("cartel-meet", Orbit(me.Position, 5f, 1.8f, "La Sombra", "Your product is in our state. Fifteen percent of everything, or you're finished."), true)));
        list.Add(Item("First stash taken", () => Cutscene("first-stash", Orbit(me.Position, 7f, 2.5f, "Uncle Ray (phone)", "That'll hurt them. Watch your back."), true)));
        list.Add(Item("First gun crates", () => Cutscene("first-crates", LabShots(labs.First(l => l.Id == "lamesa"), "Uncle Ray (phone)", "Guns. Every gang in the city will want you now."), true)));
        list.Add(Item("La Sombra beaten", () => Cutscene("cartel-beaten", Orbit(me.Position, 9f, 3f, "Uncle Ray (phone)", "La Sombra is finished. Nobody runs this state but you now."), true)));
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
        list.Add(Item("+20 gun crates", () => { stock["guns"] += 20; Save(); }));
        list.Add(Item("Every customer hooked", () => { foreach (var c in customers) { c.Unlocked = true; c.Addiction = Math.Max(c.Addiction, 40); } Save(); }));
        list.Add(Item("Every customer orders now", () => { foreach (var c in customers) { c.Unlocked = true; c.OrderProduct = c.Wants; c.OrderQty = 2; c.OrderHours = 8; } Save(); }));
        list.Add(Item("Survival: " + (creativeSurvival ? "on" : "off"), () => { creativeSurvival = !creativeSurvival; Save(); CreativeMenu(); }));
        list.Add(Item("Refill hunger, thirst and energy", () => { hunger = thirst = energy = 100f; }));
        list.Add(Item("Drain energy (test exhaustion)", () => { creativeSurvival = true; energy = 4f; hunger = 20f; thirst = 20f; }));
        list.Add(Item("+4 crew, Elite gear", () => { crewHired = Math.Min(12, crewHired + 4); crewEver += 4; crewGear = GearNames.Length - 1; SendCrewHome(); Save(); }));
        list.Add(Item("La Sombra: start the meeting", () => { ClearCartel(true); StartCartel(); }));
        list.Add(Item("La Sombra: war now", () => { ClearCartel(true); CartelWar(); }));
        list.Add(Item("La Sombra: ambush me now", () => { if (cartel != 3) CartelWar(); nextAmbush = 0; }));
        list.Add(Item("La Sombra: take every stash (go to the boss)", () => { if (cartel != 3) CartelWar(); cartelMask = 7; ClearCartel(true); Save(); }));
        list.Add(Item("La Sombra: reset", () => { ClearCartel(true); CartelBlip(Vector3.Zero, ""); cartel = 0; cartelMask = 0; Save(); }));
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
        "Win a customer with a free sample (grey people on the map)",
        "Sell 5 bags",
        "Rest at your stash house",
        "Mix an ingredient into your weed",
        "Reach Hoodlum and buy the Grapeseed farm",
        "Reach Peddler, buy the Sandy Shores lab and cook meth",
        "Reach Hustler and hire a worker",
        "Reach Bagman and win a turf war",
        "Hire your first crew member (F5 > Crew)",
        "Meet La Sombra at the Sandy Shores airfield",
        "Deal with La Sombra: pay them, or take their stashes and their boss",
        "Buy the La Mesa gun workshop and sell gun crates to a gang",
        "Reach Enforcer and cook coke at the Elysian Island warehouse",
        "Make 10 deliveries to your customers",
        "Become Kingpin",
    };
    // what Uncle Ray says when each objective comes up
    static readonly string[] QuestRay =
    {
        "Kid, it's Ray. The stash house in Strawberry is yours now. Go take a look.",
        "There's a grow tent in there. Get some weed going.",
        "Bag it up when it's done. Nobody buys a plant.",
        "People won't buy from a stranger. Give someone a taste first. I've marked a few who might bite.",
        "Now sell. Walk up to people and offer it. Keep an eye out for cops.",
        "You look half dead. Sleep at the stash house, the business runs while you're out.",
        "Plain weed is plain money. Mix something in, every customer likes a different kick.",
        "There's a farm up in Grapeseed. Get it when you can afford it, that's where the real growing is.",
        "Sandy Shores. A trailer, a chemist's kit. Meth pays five times what weed does.",
        "You can't do everything yourself. Hire someone to run a place for you.",
        "The gangs won't share. Take a block off one of them and hold it.",
        "You need people who fight for you. Hire some muscle.",
        "La Sombra want a meeting. Cartel. Go easy and hear them out.",
        "However you play this, La Sombra is either your partner or your problem. Sort it.",
        "Guns sell better than anything to the right people. There's a workshop in La Mesa.",
        "Coke. The docks. This is the big league.",
        "Your regulars keep you rich. Keep them stocked.",
        "One more push, kid. Run the whole city.",
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
            case 3: return samplesWon > 0;
            case 4: return soldCount >= 5;
            case 5: return restCount > 0;
            case 6: return mixedCount > 0;
            case 7: return labs.First(l => l.Id == "grapeseed").Owned;
            case 8: return labs.First(l => l.Id == "sandy").Owned && (stock["meth"] > 0 || labs.Any(l => l.Batch == "meth"));
            case 9: return labs.Any(l => l.Worker);
            case 10: return warsWon > 0;
            case 11: return crewEver > 0;
            case 12: return cartel >= 2;
            case 13: return cartel == 2 || cartel == 4;
            case 14: return cratesSold > 0;
            case 15: return labs.First(l => l.Id == "docks").Owned && (stock["coke"] > 0 || labs.Any(l => l.Batch == "coke"));
            case 16: return deliveries >= 10;
            case 17: return Rank >= Ranks.Length - 1;
        }
        return false;
    }

    /// <summary>Saves from before the story grew (10 objectives) carry on at the same point of the new one.</summary>
    static int OldQuest(int q) { int[] map = { 0, 1, 2, 3, 7, 8, 9, 10, 15, 17 }; return q < map.Length ? map[q] : 18; }

    void QuestWaypoint()
    {
        string id = quest <= 2 || quest == 5 || quest == 6 ? "strawberry" : quest == 7 ? "grapeseed" : quest == 8 ? "sandy" : quest == 14 ? "lamesa" : quest == 15 ? "docks" : "";
        var l = labs.FirstOrDefault(x => x.Id == id);
        if (l != null) { Function.Call(Hash.SET_NEW_WAYPOINT, l.At.X, l.At.Y); return; }
        if (quest == 3) { var c = customers.FirstOrDefault(x => !x.Unlocked && AreaOpen(x)); if (c != null) Function.Call(Hash.SET_NEW_WAYPOINT, c.At.X, c.At.Y); }
        if (quest == 12) Function.Call(Hash.SET_NEW_WAYPOINT, CartelMeet.X, CartelMeet.Y);
    }

    void Quests()
    {
        if (creative || quest >= QuestCount || !QuestDone(quest)) return;
        int reward = 500 + quest * 450;
        Game.Player.Money += reward;
        Notification.Show("~g~Objective done:~s~ " + QuestNames[quest] + "  ·  +$" + reward.ToString("N0"));
        AddXp(50 + quest * 15, "objective");
        quest++;
        if (quest < QuestCount) { Ray(QuestRay[quest]); Notification.Show("Next: " + QuestNames[quest]); QuestWaypoint(); }
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
        string line = string.Join("  ·  ", Products.Select(p => Title(p) + " " + stock[p]).ToArray()) + (stock["guns"] > 0 ? "  ·  Crates " + stock["guns"] : "") + "  ·  Heat " + heat;
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
        if (SurvivalOn)
        {
            // hunger, thirst and energy, like Outward's bars
            float[] v = { hunger, thirst, energy };
            Color[] c = { Color.FromArgb(230, 230, 150, 60), Color.FromArgb(230, 70, 160, 240), Color.FromArgb(230, 240, 220, 80) };
            string[] n = { "Food", "Water", "Energy" };
            new ContainerElement(new PointF(rx, 720 - 104), new SizeF(282, 28), Color.FromArgb(160, 10, 8, 14)).Draw();
            for (int i = 0; i < 3; i++)
            {
                float x = rx + 8 + i * 92;
                new TextElement(n[i], new PointF(x, 720 - 102), 0.22f, v[i] < 15f ? Color.FromArgb(255, 90, 90) : Color.FromArgb(200, 200, 210), Font.ChaletLondon).Draw();
                new ContainerElement(new PointF(x, 720 - 86), new SizeF(84, 5), Color.FromArgb(150, 50, 50, 50)).Draw();
                new ContainerElement(new PointF(x, 720 - 86), new SizeF(84 * v[i] / 100f, 5), c[i]).Draw();
            }
        }
        if (cartel == 3)
            new TextElement("At war with La Sombra  ·  stashes " + Enumerable.Range(0, 3).Count(i => (cartelMask & (1 << i)) != 0) + "/3", new PointF(28, creative ? 48 : 72), 0.28f, Color.FromArgb(255, 90, 90), Font.ChaletLondon).Draw();
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
        foreach (var c in customers) { if (c.Ped != null && c.Ped.Exists()) c.Ped.Delete(); if (c.Blip != null && c.Blip.Exists()) c.Blip.Delete(); }
        SendCrewHome();
        ClearCartel(true);
        CartelBlip(Vector3.Zero, "");
        if (called != null && called.Exists()) called.MarkAsNoLongerNeeded();
        if (shaking) Function.Call(Hash.STOP_GAMEPLAY_CAM_SHAKING, true);
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
                                           "wars=" + warsWon, "creativeHeat=" + (creativeHeat ? 1 : 0), "look=" + look, "seen=" + string.Join(",", seen.ToArray()),
                                           "story=2", "samples=" + samplesWon, "deliveries=" + deliveries, "rests=" + restCount, "mixed=" + mixedCount, "crates=" + cratesSold,
                                           "crew=" + crewHired + "|" + crewGear + "|" + crewEver + "|" + (crewGuard ? 1 : 0),
                                           "garage=" + string.Join(",", garage.ToArray()), "paint=" + paint,
                                           "survival=" + (int)hunger + "|" + (int)thirst + "|" + (int)energy + "|" + snacks + "|" + drinks + "|" + (creativeSurvival ? 1 : 0),
                                           "cartel=" + cartel + "|" + cartelMask };
            foreach (var p in Goods) lines.Add("stock." + p + "=" + stock[p]);
            foreach (var kv in mix) lines.Add("mix." + kv.Key + "=" + string.Join(",", kv.Value.ToArray()));
            foreach (var c in customers) lines.Add("cust." + c.Id + "=" + (c.Unlocked ? 1 : 0) + "|" + c.Addiction + "|" + c.OrderProduct + "|" + c.OrderQty + "|" + c.OrderHours);
            foreach (var l in labs)
            {
                int left = l.Batch.Length > 0 ? Math.Max(0, l.ReadyAt - Game.GameTime) : 0;
                lines.Add("lab." + l.Id + "=" + (l.Owned ? 1 : 0) + "|" + (l.Worker ? 1 : 0) + "|" + l.Supplies + "|" + l.Batch + "|" + left + "|" + (l.Handler ? 1 : 0) + "|" + (l.Cleaner ? 1 : 0));
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
            bool story2 = false;
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
                else if (k == "story") story2 = v == "2";
                else if (k == "samples" && int.TryParse(v, out n)) samplesWon = n;
                else if (k == "deliveries" && int.TryParse(v, out n)) deliveries = n;
                else if (k == "rests" && int.TryParse(v, out n)) restCount = n;
                else if (k == "mixed" && int.TryParse(v, out n)) mixedCount = n;
                else if (k == "crates" && int.TryParse(v, out n)) cratesSold = n;
                else if (k == "paint" && int.TryParse(v, out n)) paint = Math.Max(0, Math.Min(Paints.Length - 1, n));
                else if (k == "garage") foreach (var x in v.Split(',')) { if (x.Length > 0) garage.Add(x); }
                else if (k == "crew") { var b = v.Split('|'); if (b.Length >= 4) { int.TryParse(b[0], out crewHired); int.TryParse(b[1], out crewGear); int.TryParse(b[2], out crewEver); crewGuard = b[3] == "1"; crewGear = Math.Max(0, Math.Min(GearNames.Length - 1, crewGear)); } }
                else if (k == "survival")
                {
                    var b = v.Split('|');
                    if (b.Length >= 6) { int a; if (int.TryParse(b[0], out a)) hunger = a; if (int.TryParse(b[1], out a)) thirst = a; if (int.TryParse(b[2], out a)) energy = a; int.TryParse(b[3], out snacks); int.TryParse(b[4], out drinks); creativeSurvival = b[5] == "1"; }
                }
                else if (k == "cartel") { var b = v.Split('|'); if (b.Length >= 2) { int.TryParse(b[0], out cartel); int.TryParse(b[1], out cartelMask); } }
                else if (k.StartsWith("mix.") && mix.ContainsKey(k.Substring(4))) { mix[k.Substring(4)].Clear(); foreach (var x in v.Split(',')) if (EffectNamed(x) != null) mix[k.Substring(4)].Add(x); }
                else if (k.StartsWith("cust."))
                {
                    var c = customers.FirstOrDefault(x => x.Id == k.Substring(5));
                    var b = v.Split('|');
                    if (c == null || b.Length < 5) continue;
                    c.Unlocked = b[0] == "1"; int.TryParse(b[1], out c.Addiction); c.OrderProduct = stock.ContainsKey(b[2]) ? b[2] : ""; int.TryParse(b[3], out c.OrderQty); int.TryParse(b[4], out c.OrderHours);
                    if (c.OrderProduct.Length == 0) c.OrderQty = 0;
                }
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
                    if (b.Length >= 7) { l.Handler = b[5] == "1"; l.Cleaner = b[6] == "1"; }
                }
            }
            if (!story2) quest = OldQuest(quest);
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
