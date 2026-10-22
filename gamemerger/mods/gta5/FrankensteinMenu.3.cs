// Frankenstein's menu inside GTA V, and Play as. Script Hook V .NET compiles it when the game starts (".3.cs").
// Frankenstein writes scripts/Frankenstein/settings.json before each launch: the merge's name, its rundown,
// whether this is Creative, and who you play as.
// F10: the merge's rundown. Shift+F10: switch to Creative (or back); quit the game and Frankenstein restarts it
// in that mode, with its own saves. F11: Play as on/off.
// Play as swaps you into the closest GTA V character and gives them the other game's way of moving.
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GTA;
using GTA.Native;

public class FrankensteinMenu : Script
{
    string merge = "", style = "", who = "", moves = "";
    string[] rundown = new string[0];
    bool creative, playAs, applied;
    int page = -1, pageAt, tries, nextCheck;
    int ownModel;

    string Dir { get { return Path.Combine(BaseDirectory, "Frankenstein"); } }

    public FrankensteinMenu()
    {
        Interval = 0;
        Tick += OnTick;
        KeyDown += OnKeyDown;
        Read();
    }

    static string Field(string json, string name)
    {
        var m = Regex.Match(json, "\"" + name + "\"\\s*:\\s*(\"((?:[^\"\\\\]|\\\\.)*)\"|true|false)");
        if (!m.Success) return "";
        if (m.Groups[2].Success) return Regex.Unescape(m.Groups[2].Value);
        return m.Groups[1].Value;
    }

    void Read()
    {
        try
        {
            var p = Path.Combine(Dir, "settings.json");
            if (!File.Exists(p)) return;
            var s = File.ReadAllText(p);
            merge = Field(s, "merge");
            creative = Field(s, "creative") == "true";
            var text = Field(s, "rundown_text");
            rundown = text.Length > 0 ? text.Split('\n') : new string[0];
            var pa = Regex.Match(s, "\"play_as\"\\s*:\\s*\\{[^}]*\\}");
            if (pa.Success)
            {
                style = Field(pa.Value, "style");
                who = Field(pa.Value, "who");
                moves = Field(pa.Value, "moves");
                playAs = Field(pa.Value, "on") != "false" && style.Length > 0;
            }
        }
        catch (Exception) { }
    }

    // the closest GTA V character and walk for the other game's kind of character
    string Look()
    {
        switch (style)
        {
            case "wobbly": return "u_m_y_pogo_01";
            case "superhero": return "u_m_y_imporage";
            case "knight": return "u_m_y_juggernaut_01";
            case "soldier": return "s_m_y_marine_03";
            case "gangster": return "g_m_y_ballaorig_01";
            case "racer": return "a_m_y_motox_01";
            default: return "";
        }
    }

    string Walk()
    {
        switch (style)
        {
            case "wobbly": return "move_m@drunk@verydrunk";
            case "knight": return "move_m@muscle@a";
            case "soldier": return "move_m@brave";
            case "gangster": return "move_m@gangster@generic";
            case "superhero": return "move_m@tough_guy@";
            case "stick": return "move_m@quick";
            default: return "";
        }
    }

    void ApplyPlayAs()
    {
        var me = Game.Player.Character;
        if (me == null || !me.Exists() || me.IsDead) return;
        var look = Look();
        if (look.Length > 0 && me.Model.Hash != Game.GenerateHash(look) && tries < 3)
        {
            tries++;
            if (ownModel == 0) ownModel = me.Model.Hash;
            var model = new Model(look);
            if (model.IsInCdImage && model.Request(3000))
            {
                Game.Player.ChangeModel(model);
                Function.Call(Hash.SET_PED_DEFAULT_COMPONENT_VARIATION, Game.Player.Character);
                model.MarkAsNoLongerNeeded();
                me = Game.Player.Character;
            }
        }
        var walk = Walk();
        if (walk.Length > 0)
        {
            Function.Call(Hash.REQUEST_ANIM_SET, walk);
            if (Function.Call<bool>(Hash.HAS_ANIM_SET_LOADED, walk))
                Function.Call(Hash.SET_PED_MOVEMENT_CLIPSET, me, walk, 1.0f);
        }
        bool floppy = style == "wobbly" || style == "stick";
        Function.Call(Hash.SET_PED_RAGDOLL_ON_COLLISION, me, floppy);
        float sprint = style == "superhero" ? 1.49f : style == "stick" || style == "racer" ? 1.3f : 1.0f;
        Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player, sprint);
        if (!applied)
        {
            applied = true;
            GTA.UI.Notification.Show("Frankenstein: playing as " + who + " (" + moves + "). F11 switches back.");
        }
    }

    void DropPlayAs()
    {
        var me = Game.Player.Character;
        if (ownModel != 0 && me.Model.Hash != ownModel)
        {
            var model = new Model(ownModel);
            if (model.Request(3000)) Game.Player.ChangeModel(model);
            model.MarkAsNoLongerNeeded();
            me = Game.Player.Character;
        }
        Function.Call(Hash.RESET_PED_MOVEMENT_CLIPSET, me, 0.0f);
        Function.Call(Hash.SET_PED_RAGDOLL_ON_COLLISION, me, false);
        Function.Call(Hash.SET_RUN_SPRINT_MULTIPLIER_FOR_PLAYER, Game.Player, 1.0f);
        applied = false;
        tries = 0;
        GTA.UI.Notification.Show("Frankenstein: back to your own character. F11 plays as " + who + " again.");
    }

    void OnTick(object sender, EventArgs e)
    {
        if (playAs)
        {
            if (style == "superhero") Function.Call(Hash.SET_SUPER_JUMP_THIS_FRAME, Game.Player);
            if (Game.GameTime >= nextCheck)
            {
                nextCheck = Game.GameTime + 1500;
                try { ApplyPlayAs(); } catch (Exception) { }
            }
        }
        if (page >= 0 && Game.GameTime >= pageAt)
        {
            if (page >= rundown.Length) { page = -1; return; }
            GTA.UI.Screen.ShowSubtitle(rundown[page], 4200);
            page++;
            pageAt = Game.GameTime + 4300;
        }
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F10 && e.Shift)
        {
            try
            {
                File.WriteAllText(Path.Combine(Dir, "switch.json"), "{\"to\": \"" + (creative ? "normal" : "creative") + "\"}");
                GTA.UI.Notification.Show("Frankenstein: save, then quit from the pause menu. The game starts again in " +
                                         (creative ? "your normal game" : "Creative (its own saves)") + ".");
            }
            catch (Exception ex) { GTA.UI.Notification.Show("Frankenstein couldn't switch: " + ex.Message); }
        }
        else if (e.KeyCode == Keys.F10)
        {
            GTA.UI.Notification.Show("Frankenstein · " + (merge.Length > 0 ? merge : "this merge") + (creative ? " · Creative" : "") +
                                     "~n~Shift+F10: " + (creative ? "back to your normal game" : "Creative") +
                                     (style.Length > 0 ? "~n~F11: play as " + who : "") + "~n~F7: every weapon");
            page = 0;
            pageAt = Game.GameTime + 600;
        }
        else if (e.KeyCode == Keys.F11 && style.Length > 0)
        {
            playAs = !playAs;
            try { if (playAs) { tries = 0; ApplyPlayAs(); } else DropPlayAs(); } catch (Exception) { }
        }
    }
}
