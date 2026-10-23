using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Frankenstein
{
    /// <summary>Reads a glTF 2.0 model (.glb, or .gltf with its .bin and pictures) into plain arrays, already turned
    /// into Unity's left-handed axes: x is mirrored, triangles flip their winding, and texture v runs the other way.
    /// It has no Unity types in it, so it's tested on its own outside the game. Models.cs turns the result into
    /// meshes, materials and objects in the game. Only triangle meshes are read (no skins or animation: cars and
    /// buildings don't need them).</summary>
    internal sealed class GltfPart
    {
        public float[] Pos, Nrm, Uv;
        public int[] Idx;
        public int Material = -1;
    }

    internal sealed class GltfMat
    {
        public string Name = "";
        public float[] Color = { 1, 1, 1, 1 };
        public float[] Emit = { 0, 0, 0 };
        public int Image = -1;
        public float Metal = 1, Rough = 1;
        public bool Blend, Cutout;
    }

    internal sealed class GltfNode
    {
        public string Name = "";
        public float[] T = { 0, 0, 0 }, R = { 0, 0, 0, 1 }, S = { 1, 1, 1 };
        public int Mesh = -1;
        public int[] Children = new int[0];
    }

    internal sealed class GltfDoc
    {
        public List<GltfNode> Nodes = new List<GltfNode>();
        public List<List<GltfPart>> Meshes = new List<List<GltfPart>>();
        public List<GltfMat> Mats = new List<GltfMat>();
        public List<byte[]> Images = new List<byte[]>();
        public int[] Roots = new int[0];
        public int Triangles => Meshes.Sum(m => m.Sum(p => p.Idx.Length / 3));
    }

    internal static class Gltf
    {
        const uint Magic = 0x46546C67, JsonChunk = 0x4E4F534A, BinChunk = 0x004E4942;

        public static GltfDoc Load(string path) => Read(File.ReadAllBytes(path), Path.GetDirectoryName(path) ?? "");

        /// <summary>file: a .glb's bytes or a .gltf's text; dir: where its .bin and pictures are.</summary>
        public static GltfDoc Read(byte[] file, string dir)
        {
            string json;
            byte[] bin = null;
            if (file.Length >= 12 && BitConverter.ToUInt32(file, 0) == Magic)
            {
                if (BitConverter.ToUInt32(file, 4) != 2) throw new FormatException("only glTF 2.0 models are read");
                int at = 12;
                json = null;
                while (at + 8 <= file.Length)
                {
                    int len = (int)BitConverter.ToUInt32(file, at);
                    uint type = BitConverter.ToUInt32(file, at + 4);
                    if (at + 8 + len > file.Length) throw new FormatException("the .glb file is cut short");
                    if (type == JsonChunk) json = Encoding.UTF8.GetString(file, at + 8, len);
                    else if (type == BinChunk && bin == null) { bin = new byte[len]; Buffer.BlockCopy(file, at + 8, bin, 0, len); }
                    at += 8 + len;
                }
                if (json == null) throw new FormatException("the .glb file has no JSON part");
            }
            else json = Encoding.UTF8.GetString(file).TrimStart('﻿');
            var root = MiniJson.Parse(json);
            var buffers = J.L(root, "buffers").Select((b, i) => Buffer_(b, i, bin, dir)).ToList();
            var views = J.L(root, "bufferViews");
            var accessors = J.L(root, "accessors");
            var doc = new GltfDoc();

            // pictures
            foreach (var img in J.L(root, "images"))
            {
                byte[] data = null;
                try
                {
                    if (J.O(img, "bufferView") != null) data = View(views, buffers, (int)J.N(img, "bufferView"));
                    else data = Uri(J.S(img, "uri"), dir);
                }
                catch (Exception) { }
                doc.Images.Add(data);
            }
            var texSource = J.L(root, "textures").Select(t => J.O(t, "source") != null ? (int)J.N(t, "source") : -1).ToList();

            foreach (var m in J.L(root, "materials"))
            {
                var mat = new GltfMat { Name = J.S(m, "name") };
                var pbr = J.O(m, "pbrMetallicRoughness");
                var c = Floats(J.L(pbr, "baseColorFactor"));
                if (c.Length == 4) mat.Color = c;
                var tex = J.O(pbr, "baseColorTexture");
                if (tex != null)
                {
                    int ti = (int)J.N(tex, "index", -1);
                    if (ti >= 0 && ti < texSource.Count) mat.Image = texSource[ti];
                }
                mat.Metal = (float)J.N(pbr, "metallicFactor", 1);
                mat.Rough = (float)J.N(pbr, "roughnessFactor", 1);
                var e = Floats(J.L(m, "emissiveFactor"));
                if (e.Length == 3) mat.Emit = e;
                var mode = J.S(m, "alphaMode", "OPAQUE");
                mat.Blend = mode == "BLEND";
                mat.Cutout = mode == "MASK";
                doc.Mats.Add(mat);
            }

            foreach (var mesh in J.L(root, "meshes"))
            {
                var parts = new List<GltfPart>();
                foreach (var p in J.L(mesh, "primitives"))
                {
                    if ((int)J.N(p, "mode", 4) != 4) continue;  // triangles only
                    var attr = J.O(p, "attributes");
                    if (J.O(attr, "POSITION") == null) continue;
                    var part = new GltfPart();
                    part.Pos = Read(accessors, views, buffers, (int)J.N(attr, "POSITION"), 3);
                    int n = part.Pos.Length / 3;
                    if (J.O(attr, "NORMAL") != null) part.Nrm = Read(accessors, views, buffers, (int)J.N(attr, "NORMAL"), 3);
                    if (J.O(attr, "TEXCOORD_0") != null) part.Uv = Read(accessors, views, buffers, (int)J.N(attr, "TEXCOORD_0"), 2);
                    part.Idx = J.O(p, "indices") != null ? Indices(accessors, views, buffers, (int)J.N(p, "indices")) : Enumerable.Range(0, n).ToArray();
                    part.Material = J.O(p, "material") != null ? (int)J.N(p, "material") : -1;
                    for (int i = 0; i < n; i++)
                    {
                        part.Pos[i * 3] = -part.Pos[i * 3];
                        if (part.Nrm != null && part.Nrm.Length == n * 3) part.Nrm[i * 3] = -part.Nrm[i * 3];
                        if (part.Uv != null && part.Uv.Length == n * 2) part.Uv[i * 2 + 1] = 1f - part.Uv[i * 2 + 1];
                    }
                    if (part.Nrm != null && part.Nrm.Length != n * 3) part.Nrm = null;
                    if (part.Uv != null && part.Uv.Length != n * 2) part.Uv = null;
                    for (int i = 0; i + 2 < part.Idx.Length; i += 3)
                    {
                        int t = part.Idx[i + 1];
                        part.Idx[i + 1] = part.Idx[i + 2];
                        part.Idx[i + 2] = t;
                    }
                    if (part.Idx.Any(i => i < 0 || i >= n)) throw new FormatException("a triangle points past the end of its mesh");
                    parts.Add(part);
                }
                doc.Meshes.Add(parts);
            }

            foreach (var nd in J.L(root, "nodes"))
            {
                var node = new GltfNode { Name = J.S(nd, "name") };
                var mx = Floats(J.L(nd, "matrix"));
                if (mx.Length == 16) Decompose(mx, node);
                else
                {
                    var t = Floats(J.L(nd, "translation"));
                    var r = Floats(J.L(nd, "rotation"));
                    var s = Floats(J.L(nd, "scale"));
                    if (t.Length == 3) node.T = t;
                    if (r.Length == 4) node.R = r;
                    if (s.Length == 3) node.S = s;
                }
                node.T[0] = -node.T[0];       // mirror x
                node.R[1] = -node.R[1];       // and turn the rotation the mirrored way
                node.R[2] = -node.R[2];
                node.Mesh = J.O(nd, "mesh") != null ? (int)J.N(nd, "mesh") : -1;
                node.Children = J.L(nd, "children").Select(x => (int)(double)x).ToArray();
                doc.Nodes.Add(node);
            }

            var scenes = J.L(root, "scenes");
            int si = (int)J.N(root, "scene", 0);
            if (scenes.Count > 0) doc.Roots = J.L(scenes[Math.Min(si, scenes.Count - 1)], "nodes").Select(x => (int)(double)x).ToArray();
            else
            {
                var kids = new HashSet<int>(doc.Nodes.SelectMany(x => x.Children));
                doc.Roots = Enumerable.Range(0, doc.Nodes.Count).Where(i => !kids.Contains(i)).ToArray();
            }
            if (doc.Nodes.Any(x => x.Children.Any(c => c < 0 || c >= doc.Nodes.Count)) || doc.Roots.Any(c => c < 0 || c >= doc.Nodes.Count))
                throw new FormatException("a node points to a node that isn't there");
            return doc;
        }

        static float[] Floats(List<object> l) => l.Select(x => x is double d ? (float)d : 0f).ToArray();

        static byte[] Buffer_(object b, int i, byte[] bin, string dir)
        {
            var uri = J.S(b, "uri");
            if (uri.Length == 0)
            {
                if (bin == null) throw new FormatException("buffer " + i + " has no data");
                return bin;
            }
            return Uri(uri, dir);
        }

        static byte[] Uri(string uri, string dir)
        {
            if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                int comma = uri.IndexOf(',');
                return Convert.FromBase64String(uri.Substring(comma + 1));
            }
            var rel = System.Uri.UnescapeDataString(uri).Replace('\\', '/');
            if (rel.Split('/').Any(p => p == "..")) throw new FormatException("a model may only use files next to it");
            return File.ReadAllBytes(Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar)));
        }

        static byte[] View(List<object> views, List<byte[]> buffers, int vi)
        {
            var v = views[vi];
            var buf = buffers[(int)J.N(v, "buffer")];
            int off = (int)J.N(v, "byteOffset"), len = (int)J.N(v, "byteLength");
            var data = new byte[len];
            Buffer.BlockCopy(buf, off, data, 0, len);
            return data;
        }

        static int Size(string type)
        {
            switch (type) { case "SCALAR": return 1; case "VEC2": return 2; case "VEC3": return 3; case "VEC4": return 4; case "MAT4": return 16; default: return 1; }
        }

        static int Bytes(int componentType) => componentType == 5120 || componentType == 5121 ? 1 : componentType == 5122 || componentType == 5123 ? 2 : 4;

        /// <summary>An accessor as floats (normalized integers are scaled to 0..1 or -1..1).</summary>
        static float[] Read(List<object> accessors, List<object> views, List<byte[]> buffers, int ai, int want)
        {
            var a = accessors[ai];
            int count = (int)J.N(a, "count"), comps = Size(J.S(a, "type")), ct = (int)J.N(a, "componentType", 5126);
            bool norm = J.O(a, "normalized") is bool b && b;
            var outp = new float[count * want];
            if (J.O(a, "bufferView") == null) return outp;  // all zero (a sparse-only accessor)
            var v = views[(int)J.N(a, "bufferView")];
            var buf = buffers[(int)J.N(v, "buffer")];
            int size = Bytes(ct), stride = (int)J.N(v, "byteStride", 0);
            if (stride == 0) stride = size * comps;
            int start = (int)J.N(v, "byteOffset") + (int)J.N(a, "byteOffset");
            int k = Math.Min(want, comps);
            if (count > 0 && start + (count - 1) * stride + k * size > buf.Length) throw new FormatException("accessor " + ai + " runs past its buffer");
            for (int i = 0; i < count; i++)
                for (int c = 0; c < k; c++)
                {
                    int p = start + i * stride + c * size;
                    float f;
                    switch (ct)
                    {
                        case 5126: f = BitConverter.ToSingle(buf, p); break;
                        case 5121: f = norm ? buf[p] / 255f : buf[p]; break;
                        case 5120: f = norm ? Math.Max((sbyte)buf[p] / 127f, -1f) : (sbyte)buf[p]; break;
                        case 5123: f = norm ? BitConverter.ToUInt16(buf, p) / 65535f : BitConverter.ToUInt16(buf, p); break;
                        case 5122: f = norm ? Math.Max(BitConverter.ToInt16(buf, p) / 32767f, -1f) : BitConverter.ToInt16(buf, p); break;
                        default: f = BitConverter.ToUInt32(buf, p); break;
                    }
                    outp[i * want + c] = f;
                }
            return outp;
        }

        static int[] Indices(List<object> accessors, List<object> views, List<byte[]> buffers, int ai)
        {
            var a = accessors[ai];
            int count = (int)J.N(a, "count"), ct = (int)J.N(a, "componentType", 5125);
            var v = views[(int)J.N(a, "bufferView")];
            var buf = buffers[(int)J.N(v, "buffer")];
            int size = Bytes(ct), start = (int)J.N(v, "byteOffset") + (int)J.N(a, "byteOffset");
            if (start + count * size > buf.Length) throw new FormatException("the triangle list runs past its buffer");
            var idx = new int[count];
            for (int i = 0; i < count; i++)
            {
                int p = start + i * size;
                idx[i] = size == 1 ? buf[p] : size == 2 ? BitConverter.ToUInt16(buf, p) : (int)BitConverter.ToUInt32(buf, p);
            }
            return idx;
        }

        /// <summary>A node's 4x4 matrix (column-major) as position, rotation and scale.</summary>
        static void Decompose(float[] m, GltfNode n)
        {
            n.T = new[] { m[12], m[13], m[14] };
            float sx = Len(m[0], m[1], m[2]), sy = Len(m[4], m[5], m[6]), sz = Len(m[8], m[9], m[10]);
            n.S = new[] { sx, sy, sz };
            if (sx == 0 || sy == 0 || sz == 0) return;
            float r00 = m[0] / sx, r10 = m[1] / sx, r20 = m[2] / sx;
            float r01 = m[4] / sy, r11 = m[5] / sy, r21 = m[6] / sy;
            float r02 = m[8] / sz, r12 = m[9] / sz, r22 = m[10] / sz;
            float tr = r00 + r11 + r22, x, y, z, w;
            if (tr > 0) { float s = (float)Math.Sqrt(tr + 1) * 2; w = s / 4; x = (r21 - r12) / s; y = (r02 - r20) / s; z = (r10 - r01) / s; }
            else if (r00 > r11 && r00 > r22) { float s = (float)Math.Sqrt(1 + r00 - r11 - r22) * 2; w = (r21 - r12) / s; x = s / 4; y = (r01 + r10) / s; z = (r02 + r20) / s; }
            else if (r11 > r22) { float s = (float)Math.Sqrt(1 + r11 - r00 - r22) * 2; w = (r02 - r20) / s; x = (r01 + r10) / s; y = s / 4; z = (r12 + r21) / s; }
            else { float s = (float)Math.Sqrt(1 + r22 - r00 - r11) * 2; w = (r10 - r01) / s; x = (r02 + r20) / s; y = (r12 + r21) / s; z = s / 4; }
            n.R = new[] { x, y, z, w };
        }

        static float Len(float a, float b, float c) => (float)Math.Sqrt(a * a + b * b + c * c);
    }
}
