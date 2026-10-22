# Writes CrewPeople.cs: the gangs' own people (python3 make_crew.py). Each needs its own C# class.
# gang id, role, first, last, nickname, gender(0 male/1 female), skin (r,g,b)
M = [
 ("crips","boss","Darnell","Hayes","Big Loc",0,(92,58,40)),
 ("crips","lieutenant","Marcus","Reed","Smurf",0,(110,72,50)),
 ("crips","enforcer","Tyrell","Banks","Tiny",0,(80,52,36)),
 ("crips","dealer","Kevin","Moore","Lil Kev",0,(120,80,56)),
 ("bloods","boss","Andre","Jackson","Pistol",0,(86,54,38)),
 ("bloods","lieutenant","Jamal","Price","Red Jay",0,(104,68,46)),
 ("bloods","enforcer","Jerome","Carter","Big Rome",0,(76,48,34)),
 ("bloods","gunrunner","Dre","Wallace","Shells",0,(116,76,52)),
 ("london","boss","Kwame","Asante","K-Money",0,(84,54,40)),
 ("london","lieutenant","Tyrese","Okafor","Ty",0,(98,64,46)),
 ("london","enforcer","Reece","Bailey","Rizzla",0,(214,170,140)),
 ("london","dealer","Jaden","Campbell","J-Bandz",0,(150,104,74)),
 ("yakuza","boss","Kenji","Moriyama","Oyabun",0,(222,190,160)),
 ("yakuza","lieutenant","Hiro","Tanaka","Waka",0,(226,194,164)),
 ("yakuza","enforcer","Daisuke","Kuroda","Oni",0,(218,184,152)),
 ("yakuza","gunrunner","Ren","Saito","Ghost",0,(230,198,170)),
 ("mafia","boss","Vincent","Moretti","Don Vinnie",0,(220,182,150)),
 ("mafia","lieutenant","Sal","Romano","Sally Knives",0,(214,176,144)),
 ("mafia","enforcer","Tony","Bianchi","The Bull",0,(208,170,138)),
 ("mafia","dealer","Gina","Esposito","Books",1,(226,190,160)),
 ("eastlondon","boss","Frank","Doyle","The Guv'nor",0,(232,196,170)),
 ("eastlondon","lieutenant","Terry","Mason","Tel",0,(228,190,164)),
 ("eastlondon","enforcer","Danny","Shaw","Mad Dog",0,(224,186,158)),
 ("eastlondon","gunrunner","Lee","Whitlock","Wheels",0,(220,182,152)),
]
out = ['// Written by make_crew.py: one class per person, because the game makes each NPC from its own type.',
       'using UnityEngine;', '', 'namespace Frankenstein', '{', '    internal static partial class Crew', '    {',
       '        public static readonly Person[] People =', '        {']
for g,r,f,l,n,sex,(a,b,c) in M:
    out.append('            new Person("%s", "%s", "%s", "%s", "%s", %d, new Color32(%d, %d, %d, 255)),' % (g,r,f,l,n.replace('"','\\"'),sex,a,b,c))
out += ['        };', '    }', '']
for g,r,f,l,n,sex,_ in M:
    cls = "Crew_%s_%s" % (g, r)
    out.append('    public sealed class %s : CrewNpc { public %s() : base() { } protected override string Key => "%s_%s"; }' % (cls, cls, g, r))
out.append('}')
open(__import__("os").path.join(__import__("os").path.dirname(__import__("os").path.abspath(__file__)), "CrewPeople.cs"), "w").write("\n".join(out)+"\n")
