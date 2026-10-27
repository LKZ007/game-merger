// Frankenstein's Gang Beasts-style ragdoll for the GTA V empire merge (Schedule I x Hood Warfare): floppy,
// physical brawling on top of GTA V's own Euphoria ragdolls. It only runs where settings.json has "empire": true.
//
// J (controller: double-tap D-pad down) flops you over, like letting go in Gang Beasts; press it again while down
// to stay down longer. Punches, kicks and melee weapons send people flying instead of just knocking them back, and
// a sprinting shoulder barge knocks over whoever you run into. Shift+J turns the launches on and off.
// Everything it does is logged to scripts/Frankenstein/ragdoll-debug.txt.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GTA;
using GTA.Math;
using GTA.UI;

public class FrankensteinRagdoll : Script
{
    bool on, launches = true;
    int lastTap = -10000, floppedUntil, nextScan, nextBarge;
    readonly List<string> log = new List<string>();

    string Dir { get { return Path.Combine(BaseDirectory, "Frankenstein"); } }

    public FrankensteinRagdoll()
    {
        Interval = 0;
        Tick += OnTick;
        KeyDown += OnKeyDown;
        try
        {
            var p = Path.Combine(Dir, "settings.json");
            on = File.Exists(p) && Regex.IsMatch(File.ReadAllText(p), "\"empire\"\\s*:\\s*true");
        }
        catch (Exception) { on = false; }
        Say(on ? "ragdoll on" : "ragdoll off (not the empire merge)");
    }

    void OnTick(object sender, EventArgs e)
    {
        if (!on) return;
        var me = Game.Player.Character;
        if (me == null || !me.Exists() || me.IsDead || Game.IsLoading) return;
        try
        {
            // controller: two quick taps of D-pad down (the character wheel button) while on foot
            if (me.IsOnFoot && Game.IsControlJustPressed(GTA.Control.CharacterWheel))
            {
                if (Game.GameTime - lastTap < 350) { Flop(me); lastTap = -10000; }
                else lastTap = Game.GameTime;
            }
            if (Game.GameTime < floppedUntil && me.IsRagdoll) Game.DisableControlThisFrame(GTA.Control.Jump);
            if (launches) { Launch(me); Barge(me); }
        }
        catch (Exception ex) { Say("tick: " + ex.Message); }
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!on || e.KeyCode != Keys.J) return;
        var me = Game.Player.Character;
        if (me == null || !me.Exists() || me.IsDead) return;
        if (e.Shift)
        {
            launches = !launches;
            Notification.Show("Gang Beasts launches: " + (launches ? "on" : "off"));
            Say("launches " + (launches ? "on" : "off"));
            return;
        }
        if (me.IsOnFoot) Flop(me);
    }

    /// <summary>Go limp, with a little push in the way you were facing, like Gang Beasts.</summary>
    void Flop(Ped me)
    {
        bool again = Game.GameTime < floppedUntil;
        int ms = again ? 4000 : 2500;
        floppedUntil = Game.GameTime + ms;
        me.Ragdoll(ms, RagdollType.Relax);
        if (!again) me.ApplyForce(me.ForwardVector * 2.5f + new Vector3(0, 0, 1.2f), Vector3.Zero, ForceType.MaxForceRot2);
        Say(again ? "flop (longer)" : "flop");
    }

    /// <summary>Anyone you've just hit with a fist, foot or melee weapon goes flying, away from you.</summary>
    void Launch(Ped me)
    {
        if (Game.GameTime < nextScan) return;
        nextScan = Game.GameTime + 50;
        var group = me.Weapons.Current != null ? me.Weapons.Current.Group : WeaponGroup.Unarmed;
        if (group != WeaponGroup.Unarmed && group != WeaponGroup.Melee) return;
        foreach (var p in World.GetNearbyPeds(me, 4f))
        {
            if (p == null || !p.Exists() || p.IsPlayer || p.IsInVehicle()) continue;
            if (!p.HasBeenDamagedBy(me) || !p.HasBeenDamagedByAnyMeleeWeapon()) continue;
            p.ClearLastWeaponDamage();
            GTA.Native.Function.Call(GTA.Native.Hash.CLEAR_ENTITY_LAST_DAMAGE_ENTITY, p);
            var away = p.Position - me.Position;
            away.Z = 0;
            away.Normalize();
            float power = group == WeaponGroup.Melee ? 14f : 9f;
            p.Ragdoll(3500, RagdollType.Relax);
            p.ApplyForce(away * power + new Vector3(0, 0, power * 0.45f), Vector3.Zero, ForceType.MaxForceRot2);
            Say("launched " + p.Handle + (group == WeaponGroup.Melee ? " (weapon)" : " (fists)"));
        }
    }

    /// <summary>Sprint into someone and they go over.</summary>
    void Barge(Ped me)
    {
        if (!me.IsSprinting || Game.GameTime < nextBarge) return;
        var p = World.GetNearbyPeds(me, 1.2f).FirstOrDefault(x => x != null && x.Exists() && !x.IsPlayer && x.IsAlive && !x.IsRagdoll && !x.IsInVehicle());
        if (p == null) return;
        nextBarge = Game.GameTime + 800;
        var away = p.Position - me.Position;
        away.Z = 0;
        away.Normalize();
        p.Ragdoll(2500, RagdollType.Relax);
        p.ApplyForce(away * 6f + new Vector3(0, 0, 2f), Vector3.Zero, ForceType.MaxForceRot2);
        Say("barged " + p.Handle);
    }

    void Say(string line)
    {
        log.Add(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + line);
        if (log.Count > 200) log.RemoveRange(0, log.Count - 200);
        try { Directory.CreateDirectory(Dir); File.WriteAllLines(Path.Combine(Dir, "ragdoll-debug.txt"), log.ToArray()); } catch (Exception) { }
    }
}
