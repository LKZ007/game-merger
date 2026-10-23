using System;
using System.Globalization;
using System.Linq;
using Frankenstein;

// Reads a model with the mod's own glTF reader (no Unity needed) and prints what it got, for tests/test_gltf.py.
static class GltfCheck
{
    static string F(float[] a) => a == null ? "null" : "[" + string.Join(",", a.Select(x => x.ToString("0.###", CultureInfo.InvariantCulture))) + "]";

    static int Main(string[] args)
    {
        try
        {
            var d = Gltf.Load(args[0]);
            Console.WriteLine("nodes " + d.Nodes.Count + " meshes " + d.Meshes.Count + " mats " + d.Mats.Count + " images " + d.Images.Count(x => x != null) + " tris " + d.Triangles);
            Console.WriteLine("roots " + string.Join(",", d.Roots));
            foreach (var n in d.Nodes) Console.WriteLine("node " + n.Name + " T" + F(n.T) + " R" + F(n.R) + " S" + F(n.S) + " mesh " + n.Mesh + " kids " + string.Join(",", n.Children));
            foreach (var m in d.Meshes) foreach (var p in m)
                Console.WriteLine("part pos" + F(p.Pos) + " nrm" + F(p.Nrm) + " uv" + F(p.Uv) + " idx[" + string.Join(",", p.Idx) + "] mat " + p.Material);
            foreach (var m in d.Mats) Console.WriteLine("mat " + m.Name + " color" + F(m.Color) + " image " + m.Image + " metal " + m.Metal + " rough " + m.Rough + " emit" + F(m.Emit) + " blend " + m.Blend);
            return 0;
        }
        catch (Exception e) { Console.WriteLine("error " + e.GetType().Name + ": " + e.Message); return 1; }
    }
}
