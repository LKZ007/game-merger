// Written by make_crew.py: one class per person, because the game makes each NPC from its own type.
using UnityEngine;

namespace Frankenstein
{
    internal static partial class Crew
    {
        public static readonly Person[] People =
        {
            new Person("crips", "boss", "Darnell", "Hayes", "Big Loc", 0, new Color32(92, 58, 40, 255)),
            new Person("crips", "lieutenant", "Marcus", "Reed", "Smurf", 0, new Color32(110, 72, 50, 255)),
            new Person("crips", "enforcer", "Tyrell", "Banks", "Tiny", 0, new Color32(80, 52, 36, 255)),
            new Person("crips", "dealer", "Kevin", "Moore", "Lil Kev", 0, new Color32(120, 80, 56, 255)),
            new Person("bloods", "boss", "Andre", "Jackson", "Pistol", 0, new Color32(86, 54, 38, 255)),
            new Person("bloods", "lieutenant", "Jamal", "Price", "Red Jay", 0, new Color32(104, 68, 46, 255)),
            new Person("bloods", "enforcer", "Jerome", "Carter", "Big Rome", 0, new Color32(76, 48, 34, 255)),
            new Person("bloods", "gunrunner", "Dre", "Wallace", "Shells", 0, new Color32(116, 76, 52, 255)),
            new Person("london", "boss", "Kwame", "Asante", "K-Money", 0, new Color32(84, 54, 40, 255)),
            new Person("london", "lieutenant", "Tyrese", "Okafor", "Ty", 0, new Color32(98, 64, 46, 255)),
            new Person("london", "enforcer", "Reece", "Bailey", "Rizzla", 0, new Color32(214, 170, 140, 255)),
            new Person("london", "dealer", "Jaden", "Campbell", "J-Bandz", 0, new Color32(150, 104, 74, 255)),
            new Person("yakuza", "boss", "Kenji", "Moriyama", "Oyabun", 0, new Color32(222, 190, 160, 255)),
            new Person("yakuza", "lieutenant", "Hiro", "Tanaka", "Waka", 0, new Color32(226, 194, 164, 255)),
            new Person("yakuza", "enforcer", "Daisuke", "Kuroda", "Oni", 0, new Color32(218, 184, 152, 255)),
            new Person("yakuza", "gunrunner", "Ren", "Saito", "Ghost", 0, new Color32(230, 198, 170, 255)),
            new Person("mafia", "boss", "Vincent", "Moretti", "Don Vinnie", 0, new Color32(220, 182, 150, 255)),
            new Person("mafia", "lieutenant", "Sal", "Romano", "Sally Knives", 0, new Color32(214, 176, 144, 255)),
            new Person("mafia", "enforcer", "Tony", "Bianchi", "The Bull", 0, new Color32(208, 170, 138, 255)),
            new Person("mafia", "dealer", "Gina", "Esposito", "Books", 1, new Color32(226, 190, 160, 255)),
            new Person("eastlondon", "boss", "Frank", "Doyle", "The Guv'nor", 0, new Color32(232, 196, 170, 255)),
            new Person("eastlondon", "lieutenant", "Terry", "Mason", "Tel", 0, new Color32(228, 190, 164, 255)),
            new Person("eastlondon", "enforcer", "Danny", "Shaw", "Mad Dog", 0, new Color32(224, 186, 158, 255)),
            new Person("eastlondon", "gunrunner", "Lee", "Whitlock", "Wheels", 0, new Color32(220, 182, 152, 255)),
            new Person("cartel", "boss", "Rafael", "Ochoa", "El Patron", 0, new Color32(176, 124, 88, 255)),
            new Person("cartel", "lieutenant", "Mateo", "Ruiz", "El Flaco", 0, new Color32(184, 132, 96, 255)),
            new Person("cartel", "enforcer", "Hector", "Salas", "El Toro", 0, new Color32(168, 116, 82, 255)),
            new Person("cartel", "dealer", "Lucia", "Vargas", "La Reina", 1, new Color32(190, 140, 104, 255)),
            new Person("outlaw", "boss", "Wade", "Colter", "Preacher", 0, new Color32(226, 184, 150, 255)),
            new Person("outlaw", "lieutenant", "Jesse", "Harlan", "Road Dog", 0, new Color32(220, 178, 142, 255)),
            new Person("outlaw", "enforcer", "Boone", "McCray", "Tank", 0, new Color32(214, 170, 136, 255)),
            new Person("outlaw", "gunrunner", "Dixie", "Malone", "Lucky", 1, new Color32(230, 190, 158, 255)),
        };
    }

    public sealed class Crew_crips_boss : CrewNpc { public Crew_crips_boss() : base() { } protected override string Key => "crips_boss"; }
    public sealed class Crew_crips_lieutenant : CrewNpc { public Crew_crips_lieutenant() : base() { } protected override string Key => "crips_lieutenant"; }
    public sealed class Crew_crips_enforcer : CrewNpc { public Crew_crips_enforcer() : base() { } protected override string Key => "crips_enforcer"; }
    public sealed class Crew_crips_dealer : CrewNpc { public Crew_crips_dealer() : base() { } protected override string Key => "crips_dealer"; }
    public sealed class Crew_bloods_boss : CrewNpc { public Crew_bloods_boss() : base() { } protected override string Key => "bloods_boss"; }
    public sealed class Crew_bloods_lieutenant : CrewNpc { public Crew_bloods_lieutenant() : base() { } protected override string Key => "bloods_lieutenant"; }
    public sealed class Crew_bloods_enforcer : CrewNpc { public Crew_bloods_enforcer() : base() { } protected override string Key => "bloods_enforcer"; }
    public sealed class Crew_bloods_gunrunner : CrewNpc { public Crew_bloods_gunrunner() : base() { } protected override string Key => "bloods_gunrunner"; }
    public sealed class Crew_london_boss : CrewNpc { public Crew_london_boss() : base() { } protected override string Key => "london_boss"; }
    public sealed class Crew_london_lieutenant : CrewNpc { public Crew_london_lieutenant() : base() { } protected override string Key => "london_lieutenant"; }
    public sealed class Crew_london_enforcer : CrewNpc { public Crew_london_enforcer() : base() { } protected override string Key => "london_enforcer"; }
    public sealed class Crew_london_dealer : CrewNpc { public Crew_london_dealer() : base() { } protected override string Key => "london_dealer"; }
    public sealed class Crew_yakuza_boss : CrewNpc { public Crew_yakuza_boss() : base() { } protected override string Key => "yakuza_boss"; }
    public sealed class Crew_yakuza_lieutenant : CrewNpc { public Crew_yakuza_lieutenant() : base() { } protected override string Key => "yakuza_lieutenant"; }
    public sealed class Crew_yakuza_enforcer : CrewNpc { public Crew_yakuza_enforcer() : base() { } protected override string Key => "yakuza_enforcer"; }
    public sealed class Crew_yakuza_gunrunner : CrewNpc { public Crew_yakuza_gunrunner() : base() { } protected override string Key => "yakuza_gunrunner"; }
    public sealed class Crew_mafia_boss : CrewNpc { public Crew_mafia_boss() : base() { } protected override string Key => "mafia_boss"; }
    public sealed class Crew_mafia_lieutenant : CrewNpc { public Crew_mafia_lieutenant() : base() { } protected override string Key => "mafia_lieutenant"; }
    public sealed class Crew_mafia_enforcer : CrewNpc { public Crew_mafia_enforcer() : base() { } protected override string Key => "mafia_enforcer"; }
    public sealed class Crew_mafia_dealer : CrewNpc { public Crew_mafia_dealer() : base() { } protected override string Key => "mafia_dealer"; }
    public sealed class Crew_eastlondon_boss : CrewNpc { public Crew_eastlondon_boss() : base() { } protected override string Key => "eastlondon_boss"; }
    public sealed class Crew_eastlondon_lieutenant : CrewNpc { public Crew_eastlondon_lieutenant() : base() { } protected override string Key => "eastlondon_lieutenant"; }
    public sealed class Crew_eastlondon_enforcer : CrewNpc { public Crew_eastlondon_enforcer() : base() { } protected override string Key => "eastlondon_enforcer"; }
    public sealed class Crew_eastlondon_gunrunner : CrewNpc { public Crew_eastlondon_gunrunner() : base() { } protected override string Key => "eastlondon_gunrunner"; }
    public sealed class Crew_cartel_boss : CrewNpc { public Crew_cartel_boss() : base() { } protected override string Key => "cartel_boss"; }
    public sealed class Crew_cartel_lieutenant : CrewNpc { public Crew_cartel_lieutenant() : base() { } protected override string Key => "cartel_lieutenant"; }
    public sealed class Crew_cartel_enforcer : CrewNpc { public Crew_cartel_enforcer() : base() { } protected override string Key => "cartel_enforcer"; }
    public sealed class Crew_cartel_dealer : CrewNpc { public Crew_cartel_dealer() : base() { } protected override string Key => "cartel_dealer"; }
    public sealed class Crew_outlaw_boss : CrewNpc { public Crew_outlaw_boss() : base() { } protected override string Key => "outlaw_boss"; }
    public sealed class Crew_outlaw_lieutenant : CrewNpc { public Crew_outlaw_lieutenant() : base() { } protected override string Key => "outlaw_lieutenant"; }
    public sealed class Crew_outlaw_enforcer : CrewNpc { public Crew_outlaw_enforcer() : base() { } protected override string Key => "outlaw_enforcer"; }
    public sealed class Crew_outlaw_gunrunner : CrewNpc { public Crew_outlaw_gunrunner() : base() { } protected override string Key => "outlaw_gunrunner"; }
}
