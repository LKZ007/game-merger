using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Real 3D models in the game. The app downloads free model packs (CC0) onto your PC and copies them
    /// into UserData/Frankenstein/Models with a catalogue; you can also drop your own .glb files into
    /// UserData/Frankenstein/Models/Mine. A model is read once, off the game's main thread (Gltf.cs), then made into
    /// meshes and materials on it and kept as a hidden template; every copy in the world is cloned from that.
    /// What loaded, how big it is and anything that failed goes to models-debug.txt.</summary>
    internal static class Models
    {
        internal sealed class Entry { public string Pack = "", Kind = "", Name = "", File = ""; public string Path => System.IO.Path.Combine(Dir, File.Replace('/', System.IO.Path.DirectorySeparatorChar)); }

        static string Dir => Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein", "Models");
        static List<Entry> _catalog;
        static readonly Dictionary<string, GameObject> _templates = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, GltfDoc> _parsed = new Dictionary<string, GltfDoc>();
        static readonly HashSet<string> _failed = new HashSet<string>(), _reading = new HashSet<string>();
        static readonly object _lock = new object();
        static readonly List<string> _log = new List<string>();
        static GameObject _holder;
        static Shader _lit;
        static Material _fallback;

        public static List<Entry> Catalog
        {
            get
            {
                if (_catalog != null) return _catalog;
                _catalog = new List<Entry>();
                try
                {
                    var cat = Path.Combine(Dir, "catalog.json");
                    if (File.Exists(cat))
                        foreach (var m in J.L(MiniJson.Parse(File.ReadAllText(cat)), "models"))
                            _catalog.Add(new Entry { Pack = J.S(m, "pack"), Kind = J.S(m, "kind"), Name = J.S(m, "name"), File = J.S(m, "file") });
                    var mine = Path.Combine(Dir, "Mine");
                    if (Directory.Exists(mine))
                        foreach (var f in Directory.GetFiles(mine, "*.gl*", SearchOption.AllDirectories).Where(f => f.EndsWith(".glb", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)))
                            _catalog.Add(new Entry { Pack = "mine", Kind = f.IndexOf("car", StringComparison.OrdinalIgnoreCase) >= 0 ? "cars" : "props", Name = Path.GetFileNameWithoutExtension(f), File = "Mine/" + f.Substring(mine.Length + 1).Replace('\\', '/') });
                    Say(_catalog.Count + " models in the catalogue (" + string.Join(", ", _catalog.GroupBy(e => e.Pack).Select(g => g.Key + " " + g.Count()).ToArray()) + ")");
                }
                catch (Exception e) { Say("couldn't read the catalogue: " + e.Message); }
                return _catalog;
            }
        }

        /// <summary>The first model whose name matches one of these (exact, then containing it), of this kind.</summary>
        public static Entry Pick(string kind, params string[] prefs)
        {
            var list = Catalog.Where(e => kind == null || e.Kind == kind).ToList();
            foreach (var p in prefs)
            {
                var e = list.FirstOrDefault(x => string.Equals(x.Name, p, StringComparison.OrdinalIgnoreCase));
                if (e != null) return e;
            }
            foreach (var p in prefs)
            {
                var e = list.FirstOrDefault(x => x.Name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
                if (e != null) return e;
            }
            return null;
        }

        /// <summary>A ready template, or null while it's still being read (it starts reading in the background).</summary>
        public static GameObject Template(Entry e)
        {
            if (e == null) return null;
            GameObject t;
            if (_templates.TryGetValue(e.File, out t) && t != null) return t;
            if (_failed.Contains(e.File)) return null;
            GltfDoc doc;
            lock (_lock)
            {
                if (!_parsed.TryGetValue(e.File, out doc))
                {
                    if (_reading.Add(e.File))
                    {
                        var path = e.Path;
                        var file = e.File;
                        new Thread(() => Read(file, path)) { IsBackground = true }.Start();
                    }
                    return null;
                }
                _parsed.Remove(e.File);
            }
            if (doc == null) { _failed.Add(e.File); return null; }
            try
            {
                var started = DateTime.Now;
                t = Build(doc, e);
                _templates[e.File] = t;
                Say(e.Name + " (" + e.Pack + "): " + doc.Triangles + " triangles, " + doc.Mats.Count + " materials, " + doc.Images.Count(x => x != null) +
                    " pictures, built in " + (DateTime.Now - started).TotalMilliseconds.ToString("0") + " ms");
                return t;
            }
            catch (Exception ex) { _failed.Add(e.File); Say(e.Name + ": couldn't build: " + ex.Message); return null; }
        }

        public static int Built => _templates.Count;
        public static bool Failed(Entry e) => e == null || _failed.Contains(e.File);

        /// <summary>Plain C# only (no Unity calls), so it can run off the main thread.</summary>
        static void Read(string file, string path)
        {
            GltfDoc doc = null;
            try { doc = Gltf.Load(path); }
            catch (Exception e) { lock (_log) _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + file + ": couldn't read: " + e.Message); }
            lock (_lock) { _parsed[file] = doc; _reading.Remove(file); }
        }

        /// <summary>A copy of the model in the world, active, or null while it's still loading.</summary>
        public static GameObject Spawn(Entry e, Vector3 at, Quaternion rot, Transform parent = null)
        {
            var t = Template(e);
            if (t == null) return null;
            var go = UnityEngine.Object.Instantiate(t);
            go.name = "Frankenstein model " + e.Name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.rotation = rot;
            go.SetActive(true);
            return go;
        }

        public static Bounds LocalBounds(GameObject go, Transform space)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var mf in Rt.Comps(go, typeof(MeshFilter), true).OfType<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        // ---------- building Unity objects ----------

        static GameObject Build(GltfDoc doc, Entry e)
        {
            if (_holder == null)
            {
                _holder = new GameObject("Frankenstein model templates");
                _holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            var textures = doc.Images.Select(b => b == null ? null : Rt.LoadImage(b)).ToList();
            var mats = doc.Mats.Select(m => Mat(m, m.Image >= 0 && m.Image < textures.Count ? textures[m.Image] : null)).ToList();
            var root = new GameObject(e.Name);
            root.transform.SetParent(_holder.transform, false);
            foreach (var r in doc.Roots) Node(doc, r, root.transform, mats, 0);
            return root;
        }

        static void Node(GltfDoc doc, int i, Transform parent, List<Material> mats, int depth)
        {
            if (depth > 64) return;
            var n = doc.Nodes[i];
            var go = new GameObject(n.Name.Length > 0 ? n.Name : "node " + i);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(n.T[0], n.T[1], n.T[2]);
            go.transform.localRotation = new Quaternion(n.R[0], n.R[1], n.R[2], n.R[3]);
            go.transform.localScale = new Vector3(n.S[0], n.S[1], n.S[2]);
            if (n.Mesh >= 0 && n.Mesh < doc.Meshes.Count)
            {
                var parts = doc.Meshes[n.Mesh];
                for (int p = 0; p < parts.Count; p++)
                {
                    var holder = parts.Count == 1 ? go : new GameObject(go.name + " part " + p);
                    if (holder != go) holder.transform.SetParent(go.transform, false);
                    var mf = holder.AddComponent<MeshFilter>();
                    mf.sharedMesh = Mesh(parts[p], go.name);
                    var mr = holder.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = parts[p].Material >= 0 && parts[p].Material < mats.Count ? mats[parts[p].Material] : Fallback();
                }
            }
            foreach (var c in n.Children) Node(doc, c, go.transform, mats, depth + 1);
        }

        static Mesh Mesh(GltfPart p, string name)
        {
            int n = p.Pos.Length / 3;
            var mesh = new Mesh { name = name };
            if (n > 65000)
            {
                var prop = typeof(Mesh).GetProperty("indexFormat");
                if (prop != null) prop.SetValue(mesh, Enum.ToObject(prop.PropertyType, 1), null);  // 32-bit triangle lists
            }
            var v = new Vector3[n];
            for (int i = 0; i < n; i++) v[i] = new Vector3(p.Pos[i * 3], p.Pos[i * 3 + 1], p.Pos[i * 3 + 2]);
            Rt.SetArray(mesh, "vertices", v);
            if (p.Uv != null)
            {
                var uv = new Vector2[n];
                for (int i = 0; i < n; i++) uv[i] = new Vector2(p.Uv[i * 2], p.Uv[i * 2 + 1]);
                Rt.SetArray(mesh, "uv", uv);
            }
            Rt.SetArray(mesh, "triangles", p.Idx);
            if (p.Nrm != null)
            {
                var nr = new Vector3[n];
                for (int i = 0; i < n; i++) nr[i] = new Vector3(p.Nrm[i * 3], p.Nrm[i * 3 + 1], p.Nrm[i * 3 + 2]);
                Rt.SetArray(mesh, "normals", nr);
            }
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Shader Lit()
        {
            if (_lit != null) return _lit;
            _lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            if (_lit == null)  // the game's own: borrow the shader of any lit thing in the scene
                _lit = Rt.All(typeof(MeshRenderer)).OfType<MeshRenderer>().Select(r => r.sharedMaterial).FirstOrDefault(m => m != null && m.shader != null)?.shader;
            Say("model shader: " + (_lit != null ? _lit.name : "none found"));
            return _lit;
        }

        static Material Fallback()
        {
            if (_fallback == null) _fallback = Mat(new GltfMat { Name = "plain", Color = new[] { 0.7f, 0.7f, 0.7f, 1f }, Metal = 0, Rough = 0.8f }, null);
            return _fallback;
        }

        static Material Mat(GltfMat g, Texture2D tex)
        {
            var m = new Material(Lit()) { name = g.Name.Length > 0 ? g.Name : "model" };
            var c = new Color(g.Color[0], g.Color[1], g.Color[2], g.Color[3]);
            Set(m, "_BaseColor", c);
            Set(m, "_Color", c);
            if (tex != null)
            {
                tex.wrapMode = TextureWrapMode.Repeat;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            }
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", Mathf.Clamp01(g.Metal));
            float smooth = 1f - Mathf.Clamp01(g.Rough);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            var emit = new Color(g.Emit[0], g.Emit[1], g.Emit[2]);
            if (emit.maxColorComponent > 0.01f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emit * 2f);  // lights, neon and screens glow
            }
            if (g.Blend && m.HasProperty("_Surface"))  // glass
            {
                m.SetFloat("_Surface", 1);
                m.SetFloat("_Blend", 0);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 10);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
            }
            return m;
        }

        static void Set(Material m, string prop, Color c) { if (m.HasProperty(prop)) m.SetColor(prop, c); }

        /// <summary>A collider shaped like the model, so you can walk on it and drive into it (buildings, roads).</summary>
        public static int Solid(GameObject go)
        {
            var mc = Rt.Find("UnityEngine.MeshCollider");
            var add = Rt.Generic(typeof(GameObject), "AddComponent", 0);
            if (mc == null || add == null) return 0;
            int n = 0;
            foreach (var mf in Rt.Comps(go, typeof(MeshFilter), true).OfType<MeshFilter>())
            {
                try
                {
                    var c = add.MakeGenericMethod(mc).Invoke(mf.gameObject, null);
                    Rt.Set(c, mf.sharedMesh, "sharedMesh");
                    n++;
                }
                catch (Exception) { }
            }
            return n;
        }

        public static void Say(string line)
        {
            lock (_log)
            {
                _log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + line);
                if (_log.Count > 400) _log.RemoveRange(0, _log.Count - 400);
            }
            Melon<Plugin>.Logger.Msg("Models: " + line);
            Flush();
        }

        static void Flush()
        {
            try
            {
                var dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Frankenstein");
                Directory.CreateDirectory(dir);
                string[] lines;
                lock (_log) lines = _log.ToArray();
                File.WriteAllLines(Path.Combine(dir, "models-debug.txt"), new[] { "Frankenstein " + Plugin.Version + " · models from " + Dir }.Concat(lines).ToArray());
            }
            catch (Exception) { }
        }
    }
}
