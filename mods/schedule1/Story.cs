using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using S1API.Console;
using S1API.Entities;
using S1API.GameTime;
using S1API.Items;
using S1API.Law;
using S1API.Messaging;
using S1API.Quests;
using S1API.Saveables;

namespace Frankenstein
{
    /// <summary>The story pack Frankenstein wrote for this merge (UserData/Frankenstein/story.json).</summary>
    internal class Pack
    {
        public string Title = "";
        public int UnlockDay = 1;
        public List<object> Characters = new List<object>();
        public List<object> Quests = new List<object>();

        public static Pack Load(string path)
        {
            var root = MiniJson.Parse(File.ReadAllText(path));
            return new Pack
            {
                Title = J.S(root, "title"),
                UnlockDay = (int)J.N(root, "unlock_day", 1),
                Characters = J.L(root, "characters"),
                Quests = J.L(root, "quests"),
            };
        }

        public object Character(string id) => Characters.FirstOrDefault(c => J.S(c, "id") == id);
        public int CharacterIndex(string id) => Characters.FindIndex(c => J.S(c, "id") == id);
        public List<object> Steps(int quest) => J.L(Quests[quest], "steps");
    }

    /// <summary>Story progress, saved inside the game's own save slot.</summary>
    public class StoryProgress : S1API.Internal.Abstraction.Saveable
    {
        [SaveableField("frankenstein_story")]
        public ProgressData Data = new ProgressData();

        public static StoryProgress Instance;

        public StoryProgress() { Instance = this; }
    }

    public class ProgressData
    {
        public bool Started;
        public bool Finished;
        public int Quest;
        public int Step;
        public float Count;      // takedowns so far, or the money / day count when the step began
        public bool Flag;        // escape steps: has the player been wanted yet
        public string Bought = ""; // items bought from story characters, separated by |
        public string Pack = "";   // which story pack this progress belongs to
        public string KitGiven = ""; // which merge's starter kit this save already got
    }

#if STORY  // story mode: off until it's been tested in game (contacts would show up in every save)
    /// <summary>Six phone contacts that the story pack gives names and roles to.</summary>
    public abstract class StoryContact : NPC
    {
        protected abstract int Slot { get; }
        public override bool IsPhysical => false;

        protected override void ConfigurePrefab(NPCPrefabBuilder builder)
        {
            var c = Story.CharacterAt(Slot);
            builder.WithIdentity("frankenstein_story_" + Slot, J.S(c, "first", "Unknown"), J.S(c, "last", "Contact " + (Slot + 1)));
        }

        protected override void OnCreated()
        {
            base.OnCreated();
            Story.Contacts[Slot] = this;
        }
    }

    public sealed class StoryContact0 : StoryContact { protected override int Slot => 0; }
    public sealed class StoryContact1 : StoryContact { protected override int Slot => 1; }
    public sealed class StoryContact2 : StoryContact { protected override int Slot => 2; }
    public sealed class StoryContact3 : StoryContact { protected override int Slot => 3; }
    public sealed class StoryContact4 : StoryContact { protected override int Slot => 4; }
    public sealed class StoryContact5 : StoryContact { protected override int Slot => 5; }

    /// <summary>Each story quest in the game's quest journal. One class per quest number, because the
    /// game recreates quests from their class when a save loads.</summary>
    public abstract class StoryQuest : Quest
    {
        protected abstract int Index { get; }
        protected override string Title => J.S(Story.QuestAt(Index), "title", "Story");
        protected override string Description => J.S(Story.QuestAt(Index), "briefing");
        protected override bool AutoBegin => false;

        protected override void OnCreated()
        {
            base.OnCreated();
            if (QuestEntries.Count == 0)
            {
                foreach (var s in J.L(Story.QuestAt(Index), "steps"))
                    AddEntry(J.S(s, "text", "Keep going"), (UnityEngine.Vector3?)null);
            }
            Story.Journal[Index] = this;
        }
    }

    public sealed class StoryQuest0 : StoryQuest { protected override int Index => 0; }
    public sealed class StoryQuest1 : StoryQuest { protected override int Index => 1; }
    public sealed class StoryQuest2 : StoryQuest { protected override int Index => 2; }
    public sealed class StoryQuest3 : StoryQuest { protected override int Index => 3; }
    public sealed class StoryQuest4 : StoryQuest { protected override int Index => 4; }
    public sealed class StoryQuest5 : StoryQuest { protected override int Index => 5; }
    public sealed class StoryQuest6 : StoryQuest { protected override int Index => 6; }
    public sealed class StoryQuest7 : StoryQuest { protected override int Index => 7; }
    public sealed class StoryQuest8 : StoryQuest { protected override int Index => 8; }
    public sealed class StoryQuest9 : StoryQuest { protected override int Index => 9; }
    public sealed class StoryQuest10 : StoryQuest { protected override int Index => 10; }
    public sealed class StoryQuest11 : StoryQuest { protected override int Index => 11; }
    public sealed class StoryQuest12 : StoryQuest { protected override int Index => 12; }
    public sealed class StoryQuest13 : StoryQuest { protected override int Index => 13; }
    public sealed class StoryQuest14 : StoryQuest { protected override int Index => 14; }
    public sealed class StoryQuest15 : StoryQuest { protected override int Index => 15; }


#endif
    /// <summary>Runs the story: starts quests, checks objectives, sends texts, runs the characters' shops.</summary>
    internal static class Story
    {
#if STORY
        public const int MaxQuests = 16;
        static readonly Type[] QuestTypes =
        {
            typeof(StoryQuest0), typeof(StoryQuest1), typeof(StoryQuest2), typeof(StoryQuest3), typeof(StoryQuest4),
            typeof(StoryQuest5), typeof(StoryQuest6), typeof(StoryQuest7), typeof(StoryQuest8), typeof(StoryQuest9),
            typeof(StoryQuest10), typeof(StoryQuest11), typeof(StoryQuest12), typeof(StoryQuest13), typeof(StoryQuest14),
            typeof(StoryQuest15),
        };

#endif
        public static Pack Pack;
        public static string Folder;
#if STORY
        public static readonly StoryContact[] Contacts = new StoryContact[6];
        public static readonly Dictionary<int, StoryQuest> Journal = new Dictionary<int, StoryQuest>();
#endif
        static readonly Dictionary<string, bool> Down = new Dictionary<string, bool>();
        static bool _dumped;
        static string _lastStatus = "";

        public static object CharacterAt(int slot) =>
            Pack != null && slot < Pack.Characters.Count ? Pack.Characters[slot] : null;

        public static object QuestAt(int i) => Pack != null && i < Pack.Quests.Count ? Pack.Quests[i] : null;

        static ProgressData P
        {
            get
            {
                return StoryProgress.Instance?.Data;
            }
        }

        public static void Load()
        {
            Folder = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
            var path = Path.Combine(Folder, "story.json");
            if (!File.Exists(path))
            {
                Melon<Plugin>.Logger.Msg("No story pack here (" + path + "). Make one in Frankenstein > Questlines.");
                return;
            }
            try
            {
                Pack = Pack.Load(path);
                Melon<Plugin>.Logger.Msg("Story mode: " + Pack.Title + " (" + Pack.Quests.Count + " quests, " +
                                         Pack.Characters.Count + " characters)");
            }
            catch (Exception e)
            {
                Melon<Plugin>.Logger.Error("The story pack couldn't be read: " + e.Message);
            }
        }

        static float Money() => S1API.Money.Money.GetCashBalance() + S1API.Money.Money.GetOnlineBalance();

        /// <summary>Called about once a second while a save is loaded.</summary>
        public static void Tick()
        {
            if (Player.Local == null) return;
            if (!_dumped) DumpWorld();
            Rename();
            var p = P;
            if (p == null) return;
            GiveKit(p);
#if STORY
            if (Pack == null || Pack.Quests.Count == 0) return;
            if (p.Pack != Pack.Title)
            {
                // a new story pack replaced the old one: start over
                p.Started = p.Finished = false;
                p.Quest = p.Step = 0;
                p.Pack = Pack.Title;
            }
            if (p.Finished) return;
            if (!p.Started)
            {
                if (TimeManager.ElapsedDays < Pack.UnlockDay) return;
                p.Started = true;
                StartQuest(0);
                return;
            }
            CheckStep();
            WriteStatus();
#endif
        }

#if STORY
        static StoryQuest JournalQuest(int i)
        {
            if (Journal.TryGetValue(i, out var q) && q != null) return q;
            if (i >= MaxQuests) return null;
            try
            {
                return (StoryQuest)QuestManager.CreateQuest(QuestTypes[i], "frankenstein-story-" + i);
            }
            catch (Exception e)
            {
                Melon<Plugin>.Logger.Warning("Couldn't add quest " + i + " to the journal: " + e.Message);
                return null;
            }
        }

        static void StartQuest(int i)
        {
            var p = P;
            p.Quest = i;
            p.Step = 0;
            var quest = QuestAt(i);
            var q = JournalQuest(i);
            try { q?.Begin(); } catch (Exception) { }
            BeginStep();
            var giver = Contact(J.S(quest, "giver"));
            var steps = Pack.Steps(i);
            bool reply = steps.Count > 0 && J.S(steps[0], "type") == "text";
            Text(giver, J.S(quest, "start_text", J.S(quest, "briefing")),
                 reply ? new[] { Reply(J.S(steps[0], "reply", "I'm in."), () => CompleteStep()) } : null);
        }

        static void BeginStep()
        {
            var p = P;
            var steps = Pack.Steps(p.Quest);
            if (p.Step >= steps.Count) return;
            var s = steps[p.Step];
            p.Flag = false;
            switch (J.S(s, "type"))
            {
                case "earn": p.Count = Money(); break;
                case "days": p.Count = TimeManager.ElapsedDays; break;
                default: p.Count = 0; break;
            }
            Down.Clear();
            try
            {
                var q = JournalQuest(p.Quest);
                if (q != null && p.Step < q.QuestEntries.Count) q.QuestEntries[p.Step].Begin();
            }
            catch (Exception) { }
        }

        static void CheckStep()
        {
            var p = P;
            var steps = Pack.Steps(p.Quest);
            if (p.Step >= steps.Count) { FinishQuest(); return; }
            var s = steps[p.Step];
            var amount = (float)J.N(s, "amount", 1);
            var crime = Player.Local.CrimeData;
            switch (J.S(s, "type"))
            {
                case "earn":
                    if (Money() - p.Count >= amount) CompleteStep();
                    break;
                case "cash":
                    if (Money() >= amount) CompleteStep();
                    break;
                case "days":
                    if (TimeManager.ElapsedDays - p.Count >= amount) CompleteStep();
                    break;
                case "wanted":
                    if (crime != null && (int)crime.CurrentPursuitLevel >= Math.Max(1, (int)amount)) CompleteStep();
                    break;
                case "escape":
                    if (crime == null) break;
                    if (crime.CurrentPursuitLevel != PursuitLevel.None) p.Flag = true;
                    else if (p.Flag) CompleteStep();
                    break;
                case "takedown":
                    CountTakedowns(J.S(s, "target", "any"));
                    if (p.Count >= amount) CompleteStep();
                    break;
                case "buy":
                    if (Bought(J.S(s, "item"))) CompleteStep();
                    break;
                // "text" steps finish when the player replies to the message
            }
        }

        static void CountTakedowns(string target)
        {
            var me = Player.Local.Position;
            foreach (var npc in NPC.All.ToList())
            {
                if (npc == null || npc is StoryContact) continue;
                bool down;
                try { down = npc.IsDead || npc.IsKnockedOut; } catch (Exception) { continue; }
                var key = npc.ID ?? npc.FullName;
                bool was = Down.TryGetValue(key, out var w) && w;
                if (!Down.ContainsKey(key)) { Down[key] = down; continue; } // first look: just remember it
                Down[key] = down;
                if (!down || was) continue;
                if (UnityEngine.Vector3.Distance(me, npc.Position) > 60f) continue; // someone else's doing
                var name = npc.FullName ?? "";
                bool police = name.StartsWith("Officer", StringComparison.OrdinalIgnoreCase) ||
                              (npc.ID ?? "").IndexOf("officer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              (npc.ID ?? "").IndexOf("police", StringComparison.OrdinalIgnoreCase) >= 0;
                bool hit = target == "any" || (target == "police" && police) ||
                           name.Equals(target, StringComparison.OrdinalIgnoreCase);
                if (hit) P.Count += 1;
            }
        }

        static void CompleteStep()
        {
            var p = P;
            try
            {
                var q = JournalQuest(p.Quest);
                if (q != null && p.Step < q.QuestEntries.Count) q.QuestEntries[p.Step].Complete();
            }
            catch (Exception) { }
            p.Step += 1;
            if (p.Step >= Pack.Steps(p.Quest).Count) FinishQuest();
            else BeginStep();
        }

        static void FinishQuest()
        {
            var p = P;
            var quest = QuestAt(p.Quest);
            var reward = J.O(quest, "reward");
            var cash = (float)J.N(reward, "cash");
            if (cash > 0) S1API.Money.Money.ChangeCashBalance(cash, true, true);
            var xp = (int)J.N(reward, "xp");
            if (xp > 0) ConsoleHelper.GiveXp(xp);
            foreach (var it in J.L(reward, "items"))
            {
                var id = ItemId(J.S(it, "item"));
                if (id != null) ConsoleHelper.AddItemToInventory(id, Math.Max(1, (int)J.N(it, "qty", 1)));
            }
            try { JournalQuest(p.Quest)?.Complete(); } catch (Exception) { }
            Text(Contact(J.S(quest, "giver")), J.S(quest, "done_text", "Nice work."), null);
            if (p.Quest + 1 < Math.Min(Pack.Quests.Count, MaxQuests)) StartQuest(p.Quest + 1);
            else
            {
                p.Finished = true;
                WriteStatus();
            }
        }


#endif
        /// <summary>The merge's starter kit (e.g. guns for a Hood Warfare merge), once per save.</summary>
        static void GiveKit(ProgressData p)
        {
            if (Plugin.Kit.Count == 0 || p.KitGiven == Plugin.KitName) return;
            // Creative starts empty, like Minecraft's: no starter guns (the mode is known once the save is)
            if (Mode.SavePath.Length == 0 || Mode.Pending.Length > 0 || MainMenu.Creative) return;
            p.KitGiven = Plugin.KitName;
            var got = new List<string>();
            foreach (var entry in Plugin.Kit)
            {
                var parts = entry.Split('*');  // "M1911*1", or "M1911=Glock*1" to show it under the merged game's name
                int qty = parts.Length > 1 && int.TryParse(parts[1], out var q) ? q : 1;
                var name = parts[0].Split('=')[0];
                var id = ItemId(name);
                if (id == null) { Melon<Plugin>.Logger.Msg("Starter kit: the game has no \"" + name + "\"; skipped."); continue; }
                ConsoleHelper.AddItemToInventory(id, qty);
                got.Add(name);
            }
            if (got.Count == 0)
            {
                // none of the named items exist in this version of the game: give the first gun it has
                var gun = WeaponIds().FirstOrDefault();
                if (gun != null) { ConsoleHelper.AddItemToInventory(gun, 1); got.Add(gun); }
            }
            Melon<Plugin>.Logger.Msg(Plugin.KitName + ": " + string.Join(", ", got.ToArray()));
        }

        static bool _renamed;

        /// <summary>The merged game's names on the host game's items ("M1911=Glock" shows the M1911 as "Glock"),
        /// every session, so the item menu and inventory use them.</summary>
        public static void Rename()
        {
            if (_renamed) return;
            _renamed = true;
            foreach (var entry in Plugin.Kit)
            {
                var pair = entry.Split('*')[0].Split('=');
                if (pair.Length < 2) continue;
                var id = ItemId(pair[0]);
                if (id == null) continue;
                try
                {
                    var def = ItemManager.GetDefinition(id);
                    if (def != null && !def.Name.StartsWith(pair[1])) def.Name = pair[1] + " (" + def.Name + ")";
                }
                catch (Exception) { }
            }
        }

        /// <summary>Every item that looks like a gun.</summary>
        public static IEnumerable<string> WeaponIds()
        {
            string[] words = { "m1911", "pistol", "revolver", "shotgun", "rifle", "smg", "gun", "glock", "ak47", "uzi" };
            List<ItemDefinition> all;
            try { all = ItemManager.GetAllItemDefinitions(); } catch (Exception) { yield break; }
            foreach (var d in all)
            {
                var key = Squash(d.ID) + " " + Squash(d.Name);
                if (words.Any(w => key.Contains(w)) && !key.Contains("mag") && !key.Contains("ammo") && !key.Contains("cylinder"))
                    yield return d.ID;
            }
        }

#if STORY
        // ---------- texts and shops ----------

        static StoryContact Contact(string id)
        {
            int i = Pack.CharacterIndex(id);
            return i >= 0 && i < Contacts.Length ? Contacts[i] : Contacts.FirstOrDefault(c => c != null);
        }

        static Response Reply(string text, Action then) =>
            new Response { Label = "fs_" + Guid.NewGuid().ToString("N").Substring(0, 8), Text = text, OnTriggered = then };

        static void Text(StoryContact who, string message, Response[] replies)
        {
            if (who == null || string.IsNullOrEmpty(message)) return;
            var list = new List<Response>(replies ?? new Response[0]);
            var shop = ShopFor(who);
            if (shop.Count > 0) list.Add(Reply("What are you selling?", () => ShowShop(who)));
            try { who.SendTextMessage(message, list.ToArray(), 1f, true); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Couldn't send a text: " + e.Message); }
        }

        static List<object> ShopFor(StoryContact who)
        {
            var c = CharacterAt(Array.IndexOf(Contacts, who));
            var p = P;
            return J.L(c, "shop").Where(s =>
            {
                var after = (int)J.N(s, "after_quest", -1);  // only on sale once that quest is done
                return after < 0 || (p != null && (p.Quest > after || p.Finished));
            }).ToList();
        }

        static void ShowShop(StoryContact who)
        {
            var items = ShopFor(who);
            var replies = items.Take(4).Select(s =>
            {
                var name = J.S(s, "item");
                var price = (int)J.N(s, "price", 100);
                return Reply(name + " ($" + price + ")", () => Buy(who, name, price));
            }).ToList();
            replies.Add(Reply("Nah, I'm good.", () => { }));
            try { who.SendTextMessage(J.S(CharacterAt(Array.IndexOf(Contacts, who)), "shop_line", "Here's what I've got."), replies.ToArray(), 1f, true); }
            catch (Exception) { }
        }

        static void Buy(StoryContact who, string item, int price)
        {
            var id = ItemId(item);
            if (id == null)
            {
                Text(who, "Fresh out of " + item + ". Check back later.", null);
                Melon<Plugin>.Logger.Warning("The story pack sells \"" + item + "\", but the game has no item by that name.");
                return;
            }
            if (S1API.Money.Money.GetCashBalance() < price)
            {
                Text(who, "That's $" + price + " in cash. Come back when you've got it.", null);
                return;
            }
            S1API.Money.Money.ChangeCashBalance(-price, true, true);
            ConsoleHelper.AddItemToInventory(id, 1);
            P.Bought += "|" + item.ToLowerInvariant();
            Text(who, "Pleasure doing business.", null);
        }

        static bool Bought(string item) => (P.Bought ?? "").Contains("|" + (item ?? "").ToLowerInvariant());


#endif
        static string Squash(string s) => new string((s ?? "").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        /// <summary>The game's item id for a name the story uses ("M1911", "Pump Shotgun", "baseballbat"...).</summary>
        public static string ItemId(string name)
        {
            var want = Squash(name);
            try
            {
                var all = ItemManager.GetAllItemDefinitions();
                var hit = all.FirstOrDefault(d => Squash(d.ID) == want) ?? all.FirstOrDefault(d => Squash(d.Name) == want)
                          ?? all.FirstOrDefault(d => Squash(d.Name).Contains(want) || Squash(d.ID).Contains(want))
                          ?? all.FirstOrDefault(d => want.Contains(Squash(d.Name)) && Squash(d.Name).Length > 3);
                return hit?.ID;
            }
            catch (Exception) { return null; }
        }

        // ---------- what Frankenstein reads back ----------

        /// <summary>The game's real items and people, so the next story Frankenstein writes only uses what exists.</summary>
        static void DumpWorld()
        {
            _dumped = true;
            try
            {
                Directory.CreateDirectory(Folder);
                var items = ItemManager.GetAllItemDefinitions().Select(d => (object)new Dictionary<string, object>
                    { ["id"] = d.ID, ["name"] = d.Name, ["category"] = d.Category.ToString(), ["legal"] = d.LegalStatus.ToString() }).ToList();
                var people = NPC.All.Where(n => n != null && !(n.ID ?? "").StartsWith("frankenstein_story_")).Select(n => (object)new Dictionary<string, object>
                    { ["id"] = n.ID, ["name"] = n.FullName }).ToList();
                var world = new Dictionary<string, object> { ["items"] = items, ["npcs"] = people };
                File.WriteAllText(Path.Combine(Folder, "world.json"), MiniJson.Write(world));
            }
            catch (Exception e)
            {
                Melon<Plugin>.Logger.Warning("Couldn't list the game's items: " + e.Message);
            }
        }

#if STORY
        static void WriteStatus()
        {
            var p = P;
            if (p == null || Folder == null) return;
            var status = MiniJson.Write(new Dictionary<string, object>
            {
                ["pack"] = p.Pack, ["started"] = p.Started, ["finished"] = p.Finished, ["quest"] = p.Quest, ["step"] = p.Step,
            });
            if (status == _lastStatus) return;
            _lastStatus = status;
            try { File.WriteAllText(Path.Combine(Folder, "status.json"), status); } catch (Exception) { }
        }
#endif
    }
}
