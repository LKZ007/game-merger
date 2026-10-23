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
        public int Price;
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
    }

    static readonly string[] Products = { "weed", "meth", "coke" };
    static readonly Dictionary<string, int> StreetPrice = new Dictionary<string, int> { { "weed", 60 }, { "meth", 140 }, { "coke", 220 } };
    static readonly Dictionary<string, int> BatchMinutes = new Dictionary<string, int> { { "weed", 4 }, { "meth", 6 }, { "coke", 8 } };
    static readonly Dictionary<string, int> BatchSize = new Dictionary<string, int> { { "weed", 12 }, { "meth", 8 }, { "coke", 6 } };
    const int SupplyPrice = 250, WorkerPrice = 5000, DealerPrice = 2500, WarMinutes = 4;

    readonly List<Lab> labs = new List<Lab>
    {
        new Lab { Id = "grapeseed", Name = "Grapeseed farm", Kind = "farm", At = new Vector3(2434f, 4969f, 46.8f), Price = 45000 },
        new Lab { Id = "sandy", Name = "Sandy Shores trailer lab", Kind = "lab", At = new Vector3(1972f, 3817f, 33.4f), Price = 60000 },
        new Lab { Id = "strawberry", Name = "Strawberry stash house", Kind = "farm", At = new Vector3(-14f, -1440f, 31.1f), Price = 30000 },
        new Lab { Id = "docks", Name = "Elysian Island warehouse", Kind = "lab", At = new Vector3(155f, -3080f, 5.9f), Price = 90000 },
    };

    readonly List<Gang> gangs = new List<Gang>
    {
        new Gang { Id = "families", Name = "The Families", Model = "g_m_y_famca_01", Zones = new[] { "CHAMH", "STRAW" }, Color = Color.FromArgb(40, 180, 70), Gun = WeaponHash.MicroSMG },
        new Gang { Id = "ballas", Name = "Ballas", Model = "g_m_y_ballaorig_01", Zones = new[] { "DAVIS" }, Color = Color.FromArgb(150, 60, 200), Gun = WeaponHash.Pistol },
        new Gang { Id = "vagos", Name = "Vagos", Model = "g_m_y_mexgoon_01", Zones = new[] { "RANCHO", "CYPRE" }, Color = Color.FromArgb(240, 200, 30), Gun = WeaponHash.AssaultRifle },
        new Gang { Id = "aztecas", Name = "Aztecas", Model = "g_m_y_azteca_01", Zones = new[] { "EBURO" }, Color = Color.FromArgb(60, 160, 230), Gun = WeaponHash.SawnOffShotgun },
        new Gang { Id = "marabunta", Name = "Marabunta Grande", Model = "g_m_y_salvagoon_01", Zones = new[] { "EAST_V" }, Color = Color.FromArgb(70, 120, 230), Gun = WeaponHash.Pistol },
        new Gang { Id = "lost", Name = "The Lost MC", Model = "g_m_y_lost_01", Zones = new[] { "STAB", "SANDY" }, Color = Color.FromArgb(200, 200, 200), Gun = WeaponHash.PumpShotgun },
        new Gang { Id = "kkangpae", Name = "Kkangpae", Model = "g_m_y_korean_01", Zones = new[] { "KOREAT" }, Color = Color.FromArgb(220, 60, 60), Gun = WeaponHash.SMG },
    };

    // ---------- state ----------

    readonly Dictionary<string, int> stock = new Dictionary<string, int> { { "weed", 0 }, { "meth", 0 }, { "coke", 0 } };
    readonly HashSet<string> turf = new HashSet<string>();          // zones you hold
    readonly Dictionary<string, int> dealers = new Dictionary<string, int>();  // zone -> dealers working it
    int heat, earned, lastHour = -1;
    bool on, loaded;
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
            on = File.Exists(p) && System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(p), "\"empire\"\\s*:\\s*true");
        }
        catch (Exception) { on = false; }
        Say(on ? "empire on (Schedule I x Hood Warfare merge)" : "empire off (not this merge's Extension)");
    }

    // ---------- every frame ----------

    void OnTick(object sender, EventArgs e)
    {
        if (!on) return;
        var me = Game.Player.Character;
        if (me == null || !me.Exists() || Game.IsLoading) return;
        if (!loaded) { Load(); loaded = true; Blips(); Say("loaded: " + labs.Count(l => l.Owned) + " labs, " + turf.Count + " turf, $" + earned + " earned"); }
        try
        {
            Production();
            Hourly();
            Heat();
            War(me);
            Interact(me);
            Hud(me);
            Pad(me);
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
        l.Supplies--;
        l.Batch = what;
        l.ReadyAt = Game.GameTime + BatchMinutes[what] * 60000;
        heat = Math.Min(100, heat + (what == "weed" ? 2 : 5));
        if (!quiet) Notification.Show("~g~" + Title(what) + "~s~ started at " + l.Name + ": ready in " + BatchMinutes[what] + " min.");
        Say(l.Name + ": started " + what + (quiet ? " (worker)" : ""));
        Save();
    }

    void Collect(Lab l, bool quiet = false)
    {
        if (l.Batch.Length == 0) return;
        if (Game.GameTime < l.ReadyAt) { Notification.Show(Title(l.Batch) + " ready in " + Mins(l.ReadyAt - Game.GameTime) + "."); return; }
        int n = BatchSize[l.Batch];
        stock[l.Batch] += n;
        Notification.Show("~g~+" + n + " " + l.Batch + "~s~ packaged at " + l.Name + (quiet ? " by your worker" : "") + ".");
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
        if (cop) { Game.Player.WantedLevel = Math.Max(Game.Player.WantedLevel, 2); Notification.Show("~r~That was an undercover cop!"); heat = Math.Min(100, heat + 15); return; }
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
        heat = Math.Min(100, heat + 3);
        Notification.Show("Sold 1 " + p + " for ~g~$" + price + (mine ? "~s~ (your turf)" : ""));
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
        heat = Math.Min(100, heat + 15);
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
            Notification.Show("~g~" + Game.GetLocalizedString(warZone) + " is yours.~s~ It pays every hour, and your dealers can work it.");
            Say("took " + warZone);
            EndWar();
        }
        else if (lost)
        {
            if (turf.Remove(warZone)) Notification.Show("~r~" + warGang.Name + " took " + Game.GetLocalizedString(warZone) + " back.");
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
            list.Add(Item("Buy for $" + l.Price.ToString("N0"), () =>
            {
                if (Game.Player.Money < l.Price) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= l.Price; l.Owned = true; l.Supplies = 2; Blips(); Save();
                Notification.Show("~g~" + l.Name + " is yours.~s~ It comes with 2 supplies."); Say("bought " + l.Name);
            }));
        }
        else
        {
            if (l.Batch.Length > 0) list.Add(Item(Game.GameTime >= l.ReadyAt ? "Package the " + l.Batch : Title(l.Batch) + ": " + Mins(l.ReadyAt - Game.GameTime) + " left", () => Collect(l)));
            else if (l.Kind == "farm") list.Add(Item("Grow weed (1 supply, " + BatchMinutes["weed"] + " min)", () => Start(l, "weed")));
            else
            {
                list.Add(Item("Cook meth (1 supply, " + BatchMinutes["meth"] + " min)", () => Start(l, "meth")));
                list.Add(Item("Cook coke (1 supply, " + BatchMinutes["coke"] + " min)", () => Start(l, "coke")));
            }
            list.Add(Item("Buy 5 supplies ($" + (5 * SupplyPrice) + ")  ·  has " + l.Supplies, () =>
            {
                if (Game.Player.Money < 5 * SupplyPrice) { Notification.Show("~r~Not enough money."); return; }
                Game.Player.Money -= 5 * SupplyPrice; l.Supplies += 5; Save();
            }));
            if (!l.Worker) list.Add(Item("Hire a worker ($" + WorkerPrice + ", $100 an hour)", () =>
            {
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
        foreach (var l in labs)
        {
            var lab = l;
            list.Add(Item((l.Owned ? "Your " : "For sale: ") + l.Name + (l.Owned ? "  ·  " + (l.Batch.Length > 0 ? l.Batch + " " + (Game.GameTime >= l.ReadyAt ? "ready" : Mins(l.ReadyAt - Game.GameTime)) : "idle") + ", " + l.Supplies + " supplies" + (l.Worker ? ", worker" : "") : "  ·  $" + l.Price.ToString("N0")),
                () => { Function.Call(Hash.SET_NEW_WAYPOINT, lab.At.X, lab.At.Y); Notification.Show("Waypoint set: " + lab.Name); }));
        }
        string zone = Zone(me.Position);
        if (turf.Contains(zone))
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
                    if (Game.Player.Money < 10000) { Notification.Show("A truce with " + gang.Name + " costs $10,000."); return; }
                    Game.Player.Money -= 10000; gang.Rep = Math.Min(100, gang.Rep + 30); Save();
                    Notification.Show("You paid " + gang.Name + " $10,000. Relations: " + gang.Rep + ".");
                }));
        }
        Open("EMPIRE  ·  earned $" + earned.ToString("N0") + "  ·  heat " + heat, list);
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
        if (!on || e.KeyCode != Keys.F5) return;
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
        if (warZone.Length > 0)
            new TextElement(warPeds.Count(p => p.IsAlive) + " left  ·  " + Mins(warEnds - Game.GameTime), new PointF(640, 40), 0.5f, Color.FromArgb(255, 90, 90), Font.ChaletLondon, Alignment.Center).Draw();
    }

    void Blips()
    {
        foreach (var l in labs)
        {
            if (l.Blip != null && l.Blip.Exists()) l.Blip.Delete();
            l.Blip = World.CreateBlip(l.At);
            l.Blip.Sprite = l.Kind == "farm" ? (BlipSprite)140 : (BlipSprite)499;  // weed leaf, meth lab
            l.Blip.Color = l.Owned ? BlipColor.Green : BlipColor.White;
            l.Blip.Name = (l.Owned ? "" : "For sale: ") + l.Name;
            l.Blip.IsShortRange = !l.Owned;
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
        foreach (var p in warPeds) if (p != null && p.Exists()) p.Delete();
    }

    // ---------- saving: one key=value line each ----------

    string SaveFile { get { return Path.Combine(Dir, "empire.txt"); } }

    void Save()
    {
        try
        {
            var lines = new List<string> { "heat=" + heat, "earned=" + earned, "turf=" + string.Join(",", turf.ToArray()),
                                           "dealers=" + string.Join(",", dealers.Select(kv => kv.Key + ":" + kv.Value).ToArray()) };
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
            if (!File.Exists(SaveFile)) return;
            foreach (var raw in File.ReadAllLines(SaveFile))
            {
                int eq = raw.IndexOf('=');
                if (eq < 0) continue;
                string k = raw.Substring(0, eq), v = raw.Substring(eq + 1);
                int n;
                if (k == "heat" && int.TryParse(v, out n)) heat = n;
                else if (k == "earned" && int.TryParse(v, out n)) earned = n;
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
