// Frankenstein's GTA V script. Script Hook V .NET compiles it itself when the game starts (".3.cs": its v3 API).
// Frankenstein's Spawner in the app sends cars and weapons through scripts/Frankenstein/commands.txt,
// one request per line: number|car or weapon|model name|amount|display name.
// F7 in game: every weapon with full ammo.
using System;
using System.IO;
using System.Windows.Forms;
using GTA;
using GTA.Math;

public class FrankensteinSpawner : Script
{
    long seq = -1;
    string File_ { get { return Path.Combine(BaseDirectory, "Frankenstein", "commands.txt"); } }

    public FrankensteinSpawner()
    {
        Interval = 500;
        Tick += OnTick;
        KeyDown += OnKeyDown;
    }

    string[] Lines()
    {
        try { return File.Exists(File_) ? File.ReadAllLines(File_) : new string[0]; }
        catch (Exception) { return new string[0]; }
    }

    void OnTick(object sender, EventArgs e)
    {
        var lines = Lines();
        if (seq < 0)
        {
            seq = 0;  // requests from before this session don't count
            foreach (var l in lines) { long n; if (long.TryParse(l.Split('|')[0], out n)) seq = Math.Max(seq, n); }
            return;
        }
        foreach (var l in lines)
        {
            var p = l.Split('|');
            long n;
            if (p.Length < 3 || !long.TryParse(p[0], out n) || n <= seq) continue;
            seq = n;
            int qty = 1;
            if (p.Length > 3) int.TryParse(p[3], out qty);
            string name = p.Length > 4 ? p[4] : p[2];
            try
            {
                if (p[1] == "car") SpawnCar(p[2], name);
                else
                {
                    Game.Player.Character.Weapons.Give((WeaponHash)Game.GenerateHash(p[2]), Math.Max(qty, 1) * 250, true, true);
                    GTA.UI.Notification.Show("Frankenstein: " + name);
                }
            }
            catch (Exception ex) { GTA.UI.Notification.Show("Frankenstein couldn't spawn " + name + ": " + ex.Message); }
        }
    }

    void SpawnCar(string model, string name)
    {
        var me = Game.Player.Character;
        var m = new Model(model);
        if (!m.IsInCdImage || !m.IsVehicle) { GTA.UI.Notification.Show("Frankenstein: no car called " + model + " in this game"); return; }
        m.Request(3000);
        var v = World.CreateVehicle(m, me.Position + me.ForwardVector * 6f, me.Heading);
        m.MarkAsNoLongerNeeded();
        if (v == null) { GTA.UI.Notification.Show("Frankenstein couldn't spawn " + name); return; }
        v.PlaceOnGround();
        v.IsPersistent = false;
        GTA.UI.Notification.Show("Frankenstein: " + name + " is in front of you");
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.F7) return;
        foreach (WeaponHash w in Enum.GetValues(typeof(WeaponHash)))
        {
            try { Game.Player.Character.Weapons.Give(w, 9999, false, true); } catch (Exception) { }
        }
        GTA.UI.Notification.Show("Frankenstein: every weapon, full ammo");
    }
}
