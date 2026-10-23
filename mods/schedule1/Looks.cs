using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>Each of Frankenstein's own weapons gets its own look: an AK-47 with a wooden stock and curved
    /// magazine, a boxy Uzi, a scoped bolt-action sniper, an RPG tube with its warhead, and a six-shot drum grenade
    /// launcher. Schedule I can't load new models, so each is put together from simple shapes (boxes, cylinders,
    /// a sphere) in the gun's own materials and colours, held where the base gun was; the base gun's model is
    /// hidden. The same parts draw the F6 menu's picture of the gun, from the side.</summary>
    internal static class Looks
    {
        internal class Part
        {
            public char Shape;     // 'c' box, 'z' cylinder along the barrel, 'x' cylinder across, 'y' cylinder upright, 's' sphere
            public Vector3 Pos, Size;  // metres; +z is the barrel, +y up, 0 is where the hand holds it
            public Color Color;
            public float Tilt;     // degrees, nose down
            public float Shine;    // 0 matt, 1 polished metal
            public bool Glow, Warhead;
        }

        static Part P(char shape, float x, float y, float z, float sx, float sy, float sz, Color c, float tilt = 0, float shine = 0.3f) =>
            new Part { Shape = shape, Pos = new Vector3(x, y, z), Size = new Vector3(sx, sy, sz), Color = c, Tilt = tilt, Shine = shine };

        static Part W(this Part p) { p.Warhead = true; return p; }

        static readonly Color Steel = new Color(0.16f, 0.16f, 0.18f), Black = new Color(0.07f, 0.07f, 0.08f),
                              Wood = new Color(0.45f, 0.24f, 0.11f), Olive = new Color(0.29f, 0.33f, 0.2f),
                              Tan = new Color(0.62f, 0.53f, 0.38f), Brass = new Color(0.78f, 0.62f, 0.25f),
                              Glass = new Color(0.25f, 0.45f, 0.6f), Red = new Color(0.65f, 0.12f, 0.1f);

        /// <summary>The parts of each gun, by key, and the length of the gun it's built on (to match its size).</summary>
        public static readonly Dictionary<string, Part[]> Guns = new Dictionary<string, Part[]>
        {
            ["ak47"] = new[]
            {
                P('c', 0, 0.035f, 0.07f, 0.05f, 0.075f, 0.34f, Steel, 0, 0.6f),           // receiver
                P('c', 0, 0.075f, 0.07f, 0.042f, 0.012f, 0.3f, Steel, 0, 0.6f),           // top cover
                P('c', 0, 0.045f, 0.32f, 0.058f, 0.06f, 0.2f, Wood),                      // handguard
                P('z', 0, 0.082f, 0.33f, 0.022f, 0.022f, 0.22f, Steel, 0, 0.6f),          // gas tube
                P('z', 0, 0.05f, 0.5f, 0.02f, 0.02f, 0.36f, Black, 0, 0.7f),              // barrel
                P('z', 0, 0.05f, 0.69f, 0.03f, 0.03f, 0.05f, Black, 0, 0.7f),             // muzzle brake
                P('c', 0, 0.1f, 0.62f, 0.008f, 0.04f, 0.012f, Black),                     // front sight
                P('c', 0, 0.09f, 0.2f, 0.03f, 0.02f, 0.03f, Black),                       // rear sight
                P('c', 0, -0.04f, -0.01f, 0.032f, 0.1f, 0.04f, Wood, -18),                // pistol grip
                P('c', 0, -0.005f, -0.25f, 0.042f, 0.085f, 0.3f, Wood, 6),                // stock
                P('c', 0, -0.035f, -0.4f, 0.044f, 0.11f, 0.02f, Black, 6),                // butt plate
                P('c', 0, -0.05f, 0.14f, 0.03f, 0.1f, 0.055f, Steel, 14, 0.5f),           // magazine, top
                P('c', 0, -0.13f, 0.17f, 0.03f, 0.09f, 0.055f, Steel, 30, 0.5f),          // magazine, curving forward
                P('c', 0, -0.015f, 0.04f, 0.012f, 0.015f, 0.06f, Black),                  // trigger guard
            },
            ["uzi"] = new[]
            {
                P('c', 0, 0.035f, 0.06f, 0.055f, 0.075f, 0.27f, Black, 0, 0.5f),          // body
                P('c', 0, 0.078f, 0.04f, 0.02f, 0.014f, 0.12f, Steel, 0, 0.6f),           // cocking slide
                P('z', 0, 0.035f, 0.225f, 0.022f, 0.022f, 0.07f, Steel, 0, 0.7f),         // barrel nut and barrel
                P('c', 0, -0.075f, 0.0f, 0.036f, 0.17f, 0.045f, Black, -6),               // grip with the magazine in it
                P('c', 0, -0.17f, -0.01f, 0.034f, 0.06f, 0.035f, Steel, -6, 0.5f),        // magazine sticking out
                P('c', 0, -0.012f, 0.06f, 0.012f, 0.012f, 0.08f, Black),                  // trigger guard
                P('c', 0.031f, 0.02f, -0.1f, 0.006f, 0.012f, 0.2f, Steel, 0, 0.6f),       // folded stock, right arm
                P('c', -0.031f, 0.02f, -0.1f, 0.006f, 0.012f, 0.2f, Steel, 0, 0.6f),      // left arm
                P('c', 0, 0.02f, -0.2f, 0.068f, 0.04f, 0.012f, Steel, 0, 0.6f),           // butt
                P('c', 0, 0.09f, 0.17f, 0.012f, 0.025f, 0.012f, Black),                   // front sight
            },
            ["sniper"] = new[]
            {
                P('c', 0, 0.0f, 0.2f, 0.05f, 0.06f, 0.48f, Olive),                        // fore-end
                P('z', 0, 0.04f, 0.0f, 0.036f, 0.036f, 0.24f, Steel, 0, 0.7f),            // receiver
                P('z', 0, 0.04f, 0.55f, 0.022f, 0.022f, 0.62f, Black, 0, 0.7f),           // barrel
                P('c', 0, 0.04f, 0.88f, 0.04f, 0.03f, 0.07f, Black, 0, 0.6f),             // muzzle brake
                P('c', 0, -0.045f, -0.03f, 0.034f, 0.1f, 0.045f, Olive, -20),             // grip
                P('c', 0, -0.0f, -0.3f, 0.046f, 0.1f, 0.36f, Olive, 4),                   // stock
                P('c', 0, 0.06f, -0.27f, 0.04f, 0.03f, 0.16f, Olive),                     // cheek rest
                P('c', 0, -0.025f, -0.49f, 0.048f, 0.13f, 0.025f, Black, 4),              // recoil pad
                P('z', 0, 0.115f, 0.02f, 0.034f, 0.034f, 0.3f, Black, 0, 0.8f),           // scope tube
                P('z', 0, 0.115f, 0.2f, 0.054f, 0.054f, 0.08f, Black, 0, 0.8f),           // scope front bell
                P('z', 0, 0.115f, -0.15f, 0.046f, 0.046f, 0.06f, Black, 0, 0.8f),         // eyepiece
                P('z', 0, 0.115f, 0.241f, 0.046f, 0.046f, 0.004f, Glass, 0, 1f),          // lens
                P('y', 0, 0.15f, 0.03f, 0.022f, 0.03f, 0.022f, Black, 0, 0.7f),           // elevation turret
                P('c', 0, 0.08f, 0.09f, 0.03f, 0.03f, 0.02f, Black),                      // rings
                P('c', 0, 0.08f, -0.06f, 0.03f, 0.03f, 0.02f, Black),
                P('x', 0.045f, 0.04f, -0.06f, 0.06f, 0.012f, 0.012f, Steel, 0, 0.7f),     // bolt handle
                P('s', 0.075f, 0.04f, -0.06f, 0.02f, 0.02f, 0.02f, Black, 0, 0.6f),       // bolt knob
                P('c', 0, -0.012f, 0.03f, 0.012f, 0.014f, 0.06f, Black),                  // trigger guard
                P('c', 0, -0.07f, 0.36f, 0.012f, 0.1f, 0.012f, Black, -20),               // bipod
            },
            ["rpg"] = new[]
            {
                P('z', 0, 0.06f, 0.05f, 0.046f, 0.046f, 0.95f, Olive, 0, 0.35f),          // tube
                P('z', 0, 0.06f, 0.0f, 0.062f, 0.062f, 0.3f, Wood),                       // heat shield
                P('z', 0, 0.06f, -0.45f, 0.075f, 0.075f, 0.1f, Olive, 0, 0.35f),          // rear flare
                P('z', 0, 0.06f, 0.555f, 0.04f, 0.04f, 0.06f, Steel, 0, 0.5f).W(),            // warhead stem
                P('z', 0, 0.06f, 0.66f, 0.095f, 0.095f, 0.18f, Olive, 0, 0.4f).W(),           // warhead
                P('s', 0, 0.06f, 0.78f, 0.095f, 0.095f, 0.14f, Olive, 0, 0.4f).W(),           // its nose
                P('z', 0, 0.06f, 0.86f, 0.02f, 0.02f, 0.06f, Steel, 0, 0.6f).W(),             // fuse tip
                P('c', 0, -0.04f, 0.0f, 0.032f, 0.11f, 0.04f, Black, -10),                // rear grip
                P('c', 0, -0.035f, 0.17f, 0.03f, 0.08f, 0.035f, Black, -5),               // front grip
                P('c', 0.04f, 0.12f, 0.16f, 0.014f, 0.06f, 0.04f, Black),                 // sight
                P('c', 0, -0.005f, 0.06f, 0.012f, 0.012f, 0.05f, Black),                  // trigger guard
            },
            ["launcher"] = new[]
            {
                P('z', 0, 0.0f, 0.13f, 0.14f, 0.14f, 0.2f, Black, 0, 0.5f),               // drum
                P('z', 0.042f, 0.024f, 0.235f, 0.036f, 0.036f, 0.012f, Brass, 0, 0.8f),   // grenades in the drum
                P('z', -0.042f, 0.024f, 0.235f, 0.036f, 0.036f, 0.012f, Brass, 0, 0.8f),
                P('z', 0.0f, -0.048f, 0.235f, 0.036f, 0.036f, 0.012f, Brass, 0, 0.8f),
                P('z', 0, 0.045f, 0.36f, 0.056f, 0.056f, 0.26f, Tan, 0, 0.35f),           // barrel
                P('c', 0, 0.083f, 0.2f, 0.03f, 0.02f, 0.5f, Black, 0, 0.5f),              // top rail
                P('c', 0, 0.105f, 0.35f, 0.035f, 0.035f, 0.04f, Black),                   // sight
                P('c', 0, -0.06f, -0.02f, 0.034f, 0.1f, 0.042f, Tan, -15),                // grip
                P('c', 0, -0.02f, 0.39f, 0.03f, 0.07f, 0.035f, Tan, -5),                  // front grip
                P('c', 0, 0.02f, -0.07f, 0.05f, 0.07f, 0.1f, Tan),                        // frame behind the drum
                P('c', 0, 0.01f, -0.26f, 0.04f, 0.08f, 0.28f, Tan, 4),                    // stock
                P('c', 0, -0.01f, -0.41f, 0.045f, 0.12f, 0.025f, Black, 4),               // butt pad
            },
        };

        /// <summary>How long the gun each is built on is, in the game's units at real size.</summary>
        static readonly Dictionary<string, float> BaseLength = new Dictionary<string, float>
        {
            ["m1911"] = 0.22f, ["revolver"] = 0.28f, ["pumpshotgun"] = 1.0f, ["shotgun"] = 1.0f,
        };

        public const string Marker = "Frankenstein model";
        static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
        static readonly HashSet<int> _logged = new HashSet<int>();

        /// <summary>The held gun nearest the camera: yours (NPCs carry guns too).</summary>
        public static object HeldGun(Type gunType)
        {
            var cam = Camera.main;
            object best = null;
            float bestD = 3f;
            foreach (var g in Rt.All(gunType))
            {
                var c = g as Component;
                if (c == null) continue;
                if (cam == null) return g;
                float d = Vector3.Distance(c.transform.position, cam.transform.position);
                if (d < bestD) { bestD = d; best = g; }
            }
            return best;
        }

        /// <summary>Puts the gun's own model on the held gun once (and again if the game rebuilt the gun).</summary>
        public static void Dress(GameObject gun, string key, string baseKey)
        {
            if (gun == null || !Guns.ContainsKey(key)) return;
            var t = gun.transform;
            for (int i = 0; i < t.childCount; i++)
                if (t.GetChild(i).name == Marker) return;
            var renderers = Rt.Comps(gun, typeof(MeshRenderer), true).OfType<MeshRenderer>().Where(r => r != null && r.enabled).ToList();
            if (renderers.Count == 0) return;
            var cam = Camera.main;
            // where the base gun is, which way it points, and how big it is
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            var fwd = cam != null ? cam.transform.forward : t.forward;
            var up = cam != null ? cam.transform.up : t.up;
            float len = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(fwd.x), Mathf.Abs(fwd.y), Mathf.Abs(fwd.z))));
            float height = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z))));
            float real;
            if (!BaseLength.TryGetValue(baseKey, out real)) real = 0.25f;
            float k = Mathf.Clamp(len / real, 0.05f, 20f);
            var root = new GameObject(Marker);
            root.layer = renderers[0].gameObject.layer;
            // the hand holds the grip: behind and below the middle of the base gun
            root.transform.position = b.center - fwd * (len * (baseKey.Contains("shotgun") ? 0.18f : 0.12f)) - up * (height * 0.15f);
            root.transform.rotation = Quaternion.LookRotation(fwd, up);
            root.transform.localScale = Vector3.one * k;
            root.transform.SetParent(t, true);
            var template = renderers[0].sharedMaterial;
            int made = 0;
            foreach (var p in Guns[key])
                if (Build(p, root.transform, template) != null) made++;
            if (made == 0) { UnityEngine.Object.Destroy(root); return; }
            foreach (var r in renderers) r.enabled = false;
            if (_logged.Add(gun.GetInstanceID()))
                Melon<Plugin>.Logger.Msg("Looks: " + key + " built from " + made + " parts on the " + baseKey + " (x" + k.ToString("0.##") + ").");
        }

        /// <summary>Shows or hides the RPG's warhead (it's gone once fired, back when reloaded).</summary>
        public static void Warhead(GameObject gun, bool on)
        {
            if (gun == null) return;
            foreach (var t in Rt.Comps(gun, typeof(Transform), true).OfType<Transform>())
                if (t != null && t.name == "warhead" && t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }

        internal static GameObject Build(Part p, Transform parent, Material template)
        {
            var type = p.Shape == 'c' ? PrimitiveType.Cube : p.Shape == 's' ? PrimitiveType.Sphere : PrimitiveType.Cylinder;
            GameObject go;
            try { go = GameObject.CreatePrimitive(type); }
            catch (Exception e) { Melon<Plugin>.Logger.Warning("Looks: no shapes in this build (" + e.Message + ")"); return null; }
            go.name = p.Warhead ? "warhead" : "part";
            foreach (var col in Rt.Comps(go, Rt.Find("UnityEngine.Collider"), false))
                if (col is UnityEngine.Object o) UnityEngine.Object.DestroyImmediate(o);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = p.Pos;
            var tilt = Quaternion.Euler(p.Tilt, 0, 0);
            switch (p.Shape)
            {
                case 'z': go.transform.localRotation = tilt * Quaternion.Euler(90, 0, 0); go.transform.localScale = new Vector3(p.Size.x, p.Size.z / 2f, p.Size.y); break;
                case 'x': go.transform.localRotation = tilt * Quaternion.Euler(0, 0, 90); go.transform.localScale = new Vector3(p.Size.y, p.Size.x / 2f, p.Size.z); break;
                case 'y': go.transform.localRotation = tilt; go.transform.localScale = new Vector3(p.Size.x, p.Size.y / 2f, p.Size.z); break;
                default: go.transform.localRotation = tilt; go.transform.localScale = p.Size; break;
            }
            var r = go.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.sharedMaterial = Mat(template, p.Color, p.Shine, p.Glow);
                try { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; } catch (Exception) { }
            }
            return go;
        }

        /// <summary>A material in the game's own shader (so it's lit like everything else), in one colour.</summary>
        internal static Material Mat(Material template, Color c, float shine, bool glow)
        {
            var key = (template != null ? template.GetInstanceID() : 0) + "|" + c + "|" + shine + "|" + glow;
            Material m;
            if (_mats.TryGetValue(key, out m) && m != null) return m;
            if (template != null) m = new Material(template);
            else
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                m = new Material(sh);
            }
            foreach (var tex in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_DetailAlbedoMap" })
                if (m.HasProperty(tex)) m.SetTexture(tex, null);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", shine > 0.5f ? 0.8f : 0.1f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", shine);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", shine);
            if (glow && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * 4f);
            }
            _mats[key] = m;
            return m;
        }

        /// <summary>The F6 menu's picture of the gun: its parts from the side, fitted to the tile.</summary>
        public static void Icon(string key, Rect at)
        {
            Part[] parts;
            if (!Guns.TryGetValue(key, out parts)) return;
            float minZ = parts.Min(p => p.Pos.z - Len(p) / 2), maxZ = parts.Max(p => p.Pos.z + Len(p) / 2);
            float minY = parts.Min(p => p.Pos.y - Tall(p) / 2), maxY = parts.Max(p => p.Pos.y + Tall(p) / 2);
            float s = Mathf.Min(at.width / (maxZ - minZ), at.height / (maxY - minY)) * 0.95f;
            float ox = at.x + (at.width - (maxZ - minZ) * s) / 2, oy = at.y + (at.height - (maxY - minY) * s) / 2;
            foreach (var p in parts.OrderBy(p => p.Pos.x))  // the far side first
            {
                if (Math.Abs(p.Pos.x) > 0.02f && p.Pos.x < 0) continue;
                float l = Len(p), h = Tall(p);
                // a tilted part leans: drawn as a few steps
                int steps = Math.Abs(p.Tilt) > 8 ? 3 : 1;
                for (int i = 0; i < steps; i++)
                {
                    float f = steps == 1 ? 0 : (i - 1) / 2f;
                    float shift = f * h * Mathf.Tan(p.Tilt * Mathf.Deg2Rad);
                    float sy = h / steps;
                    var r = new Rect(ox + (p.Pos.z - l / 2 + shift - minZ) * s, oy + (maxY - (p.Pos.y + h / 2 - i * sy)) * s, Math.Max(1.5f, l * s), Math.Max(1.5f, sy * s + 0.5f));
                    Paint.Fill(r, Lift(p.Color));
                    if (i == 0) Paint.Fill(new Rect(r.x, r.y, r.width, Math.Max(1f, r.height * 0.2f)), Color.Lerp(Lift(p.Color), Color.white, 0.25f + p.Shine * 0.2f));
                }
            }
        }

        static Color Lift(Color c) => Color.Lerp(c, Color.white, 0.12f);
        static float Len(Part p) => p.Shape == 'x' ? p.Size.z : p.Size.z;
        static float Tall(Part p) => p.Size.y;
    }
}
