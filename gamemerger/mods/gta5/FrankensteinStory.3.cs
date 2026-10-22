// Frankenstein's in-game story for GTA V merges (Script Hook V .NET compiles this file itself).
// Frankenstein writes the merge's story to scripts/Frankenstein/story.json; progress is kept next to it.
// Quests arrive as texts from the merge's characters; the current objective shows at the bottom of the screen.
// Written in plain C# 5, the language Script Hook V .NET compiles scripts with.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using GTA;

public class FrankensteinStory : Script
{
    Dictionary<string, object> pack;
    List<object> quests = new List<object>();
    int quest, step, baseMoney, baseDay, hadWanted, kills;
    bool done, started;
    HashSet<int> seen = new HashSet<int>();
    DateTime nextHint = DateTime.MinValue;

    string Dir { get { return Path.Combine(BaseDirectory, "Frankenstein"); } }

    public FrankensteinStory()
    {
        Interval = 1000;
        Tick += OnTick;
        KeyDown += OnKeyDown;
        try
        {
            pack = Json.Parse(File.ReadAllText(Path.Combine(Dir, "story.json"))) as Dictionary<string, object>;
            if (pack != null && pack.ContainsKey("quests")) quests = (List<object>)pack["quests"];
            var prog = File.Exists(Path.Combine(Dir, "progress.txt")) ? File.ReadAllText(Path.Combine(Dir, "progress.txt")).Split('|') : new string[0];
            if (prog.Length >= 3 && prog[0] == Str(pack, "title")) { quest = int.Parse(prog[1]); step = int.Parse(prog[2]); started = true; }
        }
        catch (Exception) { pack = null; }
    }

    static string Str(object o, string k)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(k, out v) && v != null ? v.ToString() : "";
    }

    static double Num(object o, string k)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(k, out v) && v is double ? (double)v : 0;
    }

    static List<object> Lst(object o, string k)
    {
        var d = o as Dictionary<string, object>;
        object v;
        return d != null && d.TryGetValue(k, out v) && v is List<object> ? (List<object>)v : new List<object>();
    }

    string Who(object q)
    {
        foreach (var c in Lst(pack, "characters"))
            if (Str(c, "id") == Str(q, "giver")) return (Str(c, "first") + " " + Str(c, "last")).Trim();
        return "Unknown";
    }

    void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, "progress.txt"), Str(pack, "title") + "|" + quest + "|" + step);
        }
        catch (Exception) { }
    }

    void Text(string from, string text) { GTA.UI.Notification.Show("~p~" + from + "~s~~n~" + text); }

    void StartStep()
    {
        baseMoney = Game.Player.Money;
        baseDay = World.CurrentDate.DayOfYear;
        hadWanted = 0;
        kills = 0;
        seen.Clear();
        Save();
    }

    void OnTick(object sender, EventArgs e)
    {
        if (pack == null || done || quests.Count == 0 || Game.Player.Character == null || !Game.Player.Character.Exists()) return;
        if (!started)
        {
            started = true;
            Text("Frankenstein", Str(pack, "title") + ": " + Str(pack, "intro"));
            BeginQuest();
            return;
        }
        if (quest >= quests.Count) { done = true; return; }
        var q = quests[quest];
        var steps = Lst(q, "steps");
        if (step >= steps.Count) { FinishQuest(q); return; }
        var s = steps[step];
        string type = Str(s, "type");
        int amount = Math.Max(1, (int)Num(s, "amount"));
        bool ok = false;
        if (type == "text") { Text(Who(q), Str(s, "text")); ok = true; }
        else if (type == "earn" || type == "cash") ok = Game.Player.Money - baseMoney >= amount || (type == "cash" && Game.Player.Money >= amount);
        else if (type == "wanted") ok = Game.Player.WantedLevel >= Math.Min(5, amount);
        else if (type == "escape") { if (Game.Player.WantedLevel > 0) hadWanted = 1; ok = hadWanted == 1 && Game.Player.WantedLevel == 0; }
        else if (type == "days") ok = (World.CurrentDate.DayOfYear - baseDay + 365) % 365 >= amount;
        else if (type == "takedown")
        {
            foreach (var p in World.GetNearbyPeds(Game.Player.Character, 120f))
            {
                if (p.IsDead && !seen.Contains(p.Handle) && p.Killer == Game.Player.Character) { seen.Add(p.Handle); kills++; }
            }
            ok = kills >= amount;
        }
        else ok = true; // "buy" and anything else: the hint says what to do, then it moves on
        if (ok) { step++; StartStep(); }
        else if (DateTime.Now > nextHint)
        {
            nextHint = DateTime.Now.AddSeconds(45);
            GTA.UI.Screen.ShowSubtitle(Str(q, "title") + ": " + Str(s, "text"), 6000);
        }
    }

    void BeginQuest()
    {
        var q = quests[quest];
        Text(Who(q), Str(q, "start_text"));
        step = 0;
        StartStep();
    }

    void FinishQuest(object q)
    {
        var reward = q is Dictionary<string, object> && ((Dictionary<string, object>)q).ContainsKey("reward") ? ((Dictionary<string, object>)q)["reward"] : null;
        int cash = (int)Num(reward, "cash");
        if (cash > 0) Game.Player.Money += cash;
        foreach (var it in Lst(reward, "items"))
        {
            try { Game.Player.Character.Weapons.Give((WeaponHash)Game.GenerateHash("WEAPON_" + Str(it, "item").ToUpperInvariant().Replace(" ", "")), 250, false, true); }
            catch (Exception) { }
        }
        Text(Who(q), Str(q, "done_text") + (cash > 0 ? " (+$" + cash + ")" : ""));
        quest++;
        if (quest >= quests.Count) { done = true; Save(); Text("Frankenstein", "Story complete: " + Str(pack, "title")); return; }
        BeginQuest();
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        // F9 repeats the current objective (F8 is Menyoo)
        if (e.KeyCode != Keys.F9 || pack == null || quest >= quests.Count) return;
        var q = quests[quest];
        var steps = Lst(q, "steps");
        GTA.UI.Screen.ShowSubtitle(Str(q, "title") + ": " + (step < steps.Count ? Str(steps[step], "text") : "almost done"), 6000);
    }
}

static class Json
{
    public static object Parse(string s) { int i = 0; return Value(s, ref i); }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>();
            i++; Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(s, ref i);
                string k = (string)Value(s, ref i);
                Ws(s, ref i); i++; // :
                d[k] = Value(s, ref i);
                Ws(s, ref i);
                if (s[i++] == '}') return d;
            }
        }
        if (c == '[')
        {
            var l = new List<object>();
            i++; Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Ws(s, ref i);
                if (s[i++] == ']') return l;
            }
        }
        if (c == '"')
        {
            var b = new StringBuilder();
            i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    char e = s[i];
                    if (e == 'n') b.Append('\n');
                    else if (e == 't') b.Append('\t');
                    else if (e == 'u') { b.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; }
                    else b.Append(e);
                }
                else b.Append(s[i]);
                i++;
            }
            i++;
            return b.ToString();
        }
        if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
        if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
        if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
    }
}
