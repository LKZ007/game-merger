using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace Frankenstein
{
    /// <summary>A whole new car body, made as a real mesh: the side profile (nose, hood, cabin, roof, tail), the
    /// width from above and the cross-section are lofted along the car, with the wheel arches cut around the base
    /// car's own wheels, a glass cabin and a painted roof. The base car's body is hidden; its wheels, physics, seats
    /// and driving stay, so it drives exactly as before. Each design (supercar, wedge, GT, boxy off-roader) is a set
    /// of heights in metres, scaled to the base car's wheels.</summary>
    internal static class Body
    {
        internal class Shape
        {
            public float Clear = 0.12f, Tail, Rear, Cabin, Front, Nose, Roof, Crown = 0.03f;
            public float C0, C1, C2, C3;            // cabin: starts, roof begins, roof ends, windshield meets the hood (0 rear axle, 1 front axle)
            public float FrontHang = 0.95f, RearHang = 0.9f, Width = 2f, Taper = 0.22f, CabinWidth = 0.64f, Boxy;
        }

        public static readonly Dictionary<string, Shape> Shapes = new Dictionary<string, Shape>
        {
            // low, curved, a teardrop cabin with a long glass engine cover
            ["super"] = new Shape { Tail = 0.74f, Rear = 0.8f, Cabin = 0.7f, Front = 0.6f, Nose = 0.42f, Roof = 1.17f, C0 = 0.02f, C1 = 0.42f, C2 = 0.58f, C3 = 0.95f },
            // the wedge: one line from the nose over the roof, sharp and flat
            ["wedge"] = new Shape { Tail = 0.86f, Rear = 0.84f, Cabin = 0.66f, Front = 0.55f, Nose = 0.32f, Roof = 1.1f, C0 = 0.08f, C1 = 0.3f, C2 = 0.42f, C3 = 1.05f, Width = 2.04f, Taper = 0.16f, CabinWidth = 0.6f },
            // long hood, cabin set back, a fastback
            ["gt"] = new Shape { Tail = 0.8f, Rear = 0.86f, Cabin = 0.8f, Front = 0.72f, Nose = 0.56f, Roof = 1.3f, C0 = -0.08f, C1 = 0.3f, C2 = 0.5f, C3 = 0.78f, FrontHang = 1.05f, RearHang = 1f, Width = 1.96f },
            // a big luxury SUV: tall and long, a gently raked windshield, a rounded roof
            ["suv"] = new Shape { Clear = 0.3f, Tail = 1.04f, Rear = 1.06f, Cabin = 1.04f, Front = 1f, Nose = 0.9f, Roof = 1.8f, Crown = 0.04f,
                                  C0 = -0.26f, C1 = -0.1f, C2 = 0.62f, C3 = 0.86f, FrontHang = 0.9f, RearHang = 0.86f, Width = 2f, Taper = 0.1f, CabinWidth = 0.84f },
            // rear-engined sports car: round nose, the roof sloping all the way down to the tail
            ["sports"] = new Shape { Tail = 0.78f, Rear = 0.84f, Cabin = 0.8f, Front = 0.62f, Nose = 0.48f, Roof = 1.27f, C0 = -0.12f, C1 = 0.36f, C2 = 0.5f, C3 = 0.86f, FrontHang = 0.9f, RearHang = 1.05f, Width = 1.92f, Taper = 0.2f, CabinWidth = 0.68f },
            // the box: upright everything, high off the ground, flat roof, near-vertical windshield
            ["gwagon"] = new Shape { Clear = 0.36f, Tail = 1.12f, Rear = 1.14f, Cabin = 1.14f, Front = 1.12f, Nose = 1.06f, Roof = 1.96f, Crown = 0.01f,
                                     C0 = -0.32f, C1 = -0.3f, C2 = 0.72f, C3 = 0.78f, FrontHang = 0.82f, RearHang = 0.86f, Width = 1.96f, Taper = 0.03f, CabinWidth = 0.9f, Boxy = 1 },
        };

        // ---------- the car's own wheels: where the body has to go ----------

        internal class Fit
        {
            public float FrontZ, RearZ, Track, R, WheelY, Ground, K;
            public List<Vector3> Wheels = new List<Vector3>();
            public float Z(float t) => Mathf.Lerp(RearZ, FrontZ, t);  // 0 rear axle, 1 front axle
        }

        public static Fit Measure(Component car)
        {
            var t = car.transform;
            var pts = new List<Vector3>();
            var radii = new List<float>();
            foreach (var wc in Rt.Comps(car.gameObject, Rt.Find("UnityEngine.WheelCollider"), true))
            {
                var c = wc as Component;
                if (c == null) continue;
                pts.Add(t.InverseTransformPoint(c.transform.position));
                var r = Rt.Get(wc, "radius");
                radii.Add(r is float f ? f * Mathf.Abs(c.transform.lossyScale.y / Mathf.Max(0.001f, t.lossyScale.y)) : 0.33f);
            }
            if (pts.Count < 4)
                foreach (var w in Rt.Comps(car.gameObject, Rt.FindByName("Wheel"), true))
                    if (w is Component c && c != null) { pts.Add(t.InverseTransformPoint(c.transform.position)); radii.Add(0.33f); }
            if (pts.Count < 4) return null;
            var fit = new Fit { Wheels = pts };
            fit.FrontZ = pts.Max(p => p.z);
            fit.RearZ = pts.Min(p => p.z);
            fit.Track = pts.Max(p => p.x) - pts.Min(p => p.x);
            fit.R = Mathf.Clamp(radii.Average(), 0.2f, 0.6f);
            fit.WheelY = pts.Average(p => p.y);
            fit.Ground = fit.WheelY - fit.R;
            if (fit.FrontZ - fit.RearZ < 1.5f || fit.Track < 0.8f) return null;
            fit.K = Mathf.Clamp((fit.FrontZ - fit.RearZ) / 2.65f, 0.8f, 1.3f);  // the base car's size against a real one
            return fit;
        }

        // ---------- the profile ----------

        static float Smooth(float a, float b, float t) { t = Mathf.Clamp01(t); return Mathf.Lerp(a, b, t * t * (3 - 2 * t)); }

        /// <summary>The beltline (top of the body) at t (0 rear axle, 1 front axle; beyond them, the overhangs).</summary>
        static float Belt(Shape s, Fit f, float t, float tTail, float tNose)
        {
            float h;
            if (t < 0) h = Smooth(s.Tail, s.Rear, (t - tTail) / -tTail);
            else if (t < s.C3 * 0.5f + 0.25f) h = Smooth(s.Rear, s.Cabin, t / (s.C3 * 0.5f + 0.25f));
            else if (t < 1) h = Smooth(s.Cabin, s.Front, (t - (s.C3 * 0.5f + 0.25f)) / (1 - (s.C3 * 0.5f + 0.25f)));
            else h = Smooth(s.Front, s.Nose, (t - 1) / (tNose - 1));
            return f.Ground + h * f.K;
        }

        /// <summary>The wings over each wheel: the body rises over the arch, like a supercar's fenders.</summary>
        static float Fender(Fit f, float z)
        {
            float top = f.WheelY + f.R * 1.18f * 0.92f + 0.06f * f.K, reach = f.R * 2.3f, h = float.MinValue;
            foreach (var az in new[] { f.FrontZ, f.RearZ })
            {
                float dz = Mathf.Abs(z - az);
                if (dz < reach) h = Mathf.Max(h, top - (top - f.Ground) * 0.6f * (dz / reach) * (dz / reach));
            }
            return h;
        }

        static float BeltZ(Shape s, Fit f, float t, float tTail, float tNose) => Mathf.Max(Belt(s, f, t, tTail, tNose), Fender(f, f.Z(t)));

        static float RoofAt(Shape s, Fit f, float t, float belt)
        {
            float roof = f.Ground + s.Roof * f.K;
            if (t <= s.C0 || t >= s.C3) return belt;
            if (t < s.C1) return Mathf.Lerp(belt, roof, s.Boxy > 0 ? 1 : Mathf.Sin((t - s.C0) / (s.C1 - s.C0) * Mathf.PI / 2));
            if (t <= s.C2) return roof;
            float u = (t - s.C2) / (s.C3 - s.C2);
            return Mathf.Lerp(roof, belt, s.Boxy > 0 ? u : u * u * 0.4f + u * 0.6f);
        }

        static float WidthAt(Shape s, Fit f, float t, float tTail, float tNose)
        {
            float w = Mathf.Max(s.Width * f.K, f.Track + 0.32f * f.K);
            float edge = t < 0.5f ? (0.5f - t) / (0.5f - tTail) : (t - 0.5f) / (tNose - 0.5f);  // 0 middle, 1 at the ends
            float e = Mathf.Clamp01((edge - 0.55f) / 0.45f);
            return w * (1 - s.Taper * e * e);
        }

        /// <summary>The bottom of the body: the ground clearance, raised into an arch over each wheel.</summary>
        static float Bottom(Shape s, Fit f, float z, float belt)
        {
            float y = f.Ground + s.Clear * f.K, R = f.R * 1.18f;
            foreach (var az in new[] { f.FrontZ, f.RearZ })
            {
                float dz = Mathf.Abs(z - az);
                if (dz < R) y = Mathf.Max(y, f.WheelY + Mathf.Sqrt(R * R - dz * dz) * 0.92f);
            }
            return Mathf.Min(y, belt - 0.1f * f.K);
        }

        // ---------- building ----------

        public class Built
        {
            public float TailZ, NoseZ, TTail, TNose;
            public Func<float, float> BeltAtZ, WidthAtZ, RoofAtZ;
            public int Parts;
        }

        public static Built Make(Transform root, Fit f, Shape s, Material template, Color paint, Color glass)
        {
            float wb = f.FrontZ - f.RearZ;
            float tTail = -s.RearHang * f.K / wb, tNose = 1 + s.FrontHang * f.K / wb;
            const int N = 56;
            var body = new List<Vector3[]>();
            var cabin = new List<Vector3[]>();
            var roof = new List<Vector3[]>();
            for (int i = 0; i <= N; i++)
            {
                float t = Mathf.Lerp(tTail, tNose, i / (float)N), z = f.Z(t);
                float yb = BeltZ(s, f, t, tTail, tNose), w = WidthAt(s, f, t, tTail, tNose) / 2, y0 = Bottom(s, f, z, yb);
                float side = s.Boxy > 0 ? 0.99f : 0.96f;
                body.Add(new[]
                {
                    new Vector3(0, y0, z), new Vector3(w * 0.9f, y0, z), new Vector3(w, y0 + (yb - y0) * 0.25f, z),
                    new Vector3(w * side, yb - 0.07f * f.K, z), new Vector3(w * (side - 0.04f), yb, z), new Vector3(0, yb + s.Crown * f.K, z),
                    new Vector3(-w * (side - 0.04f), yb, z), new Vector3(-w * side, yb - 0.07f * f.K, z), new Vector3(-w, y0 + (yb - y0) * 0.25f, z),
                    new Vector3(-w * 0.9f, y0, z),
                });
                float yr = RoofAt(s, f, t, yb);
                if (t > s.C0 - 0.02f && t < s.C3 + 0.02f)
                {
                    float cw = w * s.CabinWidth, bw = w * (s.Boxy > 0 ? 0.94f : 0.86f);
                    cabin.Add(new[]
                    {
                        new Vector3(bw, yb - 0.02f, z), new Vector3(Mathf.Lerp(bw, cw, 0.5f), Mathf.Lerp(yb, yr, 0.55f), z), new Vector3(cw, yr, z),
                        new Vector3(0, yr + s.Crown * f.K * (yr > yb + 0.05f ? 1 : 0), z),
                        new Vector3(-cw, yr, z), new Vector3(-Mathf.Lerp(bw, cw, 0.5f), Mathf.Lerp(yb, yr, 0.55f), z), new Vector3(-bw, yb - 0.02f, z),
                    });
                    if (t >= s.C1 - 0.03f && t <= s.C2 + 0.03f && yr > yb + 0.1f)
                        roof.Add(new[] { new Vector3(cw * 0.96f, yr + 0.006f, z), new Vector3(0, yr + s.Crown * f.K + 0.006f, z), new Vector3(-cw * 0.96f, yr + 0.006f, z) });
                }
            }
            int parts = 0;
            if (Mesh(root, "body", body, true, true, Looks.Mat(template, paint, 0.85f, false))) parts++;
            if (cabin.Count > 1 && Mesh(root, "cabin", cabin, false, false, Looks.Mat(template, glass, 1f, false))) parts++;
            if (roof.Count > 1 && Mesh(root, "roof", roof, false, false, Looks.Mat(template, paint, 0.85f, false))) parts++;
            var built = new Built { TTail = tTail, TNose = tNose, TailZ = f.Z(tTail), NoseZ = f.Z(tNose), Parts = parts };
            Func<float, float> tOf = z => (z - f.RearZ) / wb;
            built.BeltAtZ = z => BeltZ(s, f, tOf(z), tTail, tNose);
            built.WidthAtZ = z => WidthAt(s, f, tOf(z), tTail, tNose);
            built.RoofAtZ = z => RoofAt(s, f, tOf(z), BeltZ(s, f, tOf(z), tTail, tNose));
            return built;
        }

        /// <summary>Rings along the car joined into a mesh, each face turned outward; closed rings get end caps.</summary>
        static bool Mesh(Transform root, string name, List<Vector3[]> rings, bool closed, bool caps, Material mat)
        {
            try
            {
                var v = new List<Vector3>();
                var tri = new List<int>();
                int m = rings[0].Length;
                foreach (var r in rings) v.AddRange(r);
                Action<int, int, int, Vector3> Face = (a, b, c, outward) =>
                {
                    var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                    if (Vector3.Dot(n, outward) < 0) { tri.Add(a); tri.Add(c); tri.Add(b); }
                    else { tri.Add(a); tri.Add(b); tri.Add(c); }
                };
                for (int i = 0; i + 1 < rings.Count; i++)
                {
                    var mid = (Centre(rings[i]) + Centre(rings[i + 1])) / 2;
                    for (int j = 0; j < (closed ? m : m - 1); j++)
                    {
                        int j2 = (j + 1) % m;
                        int a = i * m + j, b = i * m + j2, c = (i + 1) * m + j, d = (i + 1) * m + j2;
                        var outward = (v[a] + v[b] + v[c] + v[d]) / 4 - mid;
                        outward.z = 0;
                        Face(a, c, b, outward);
                        Face(b, c, d, outward);
                    }
                }
                if (caps)
                    foreach (var end in new[] { 0, rings.Count - 1 })
                    {
                        int centre = v.Count;
                        v.Add(Centre(rings[end]));
                        var outward = new Vector3(0, 0, end == 0 ? -1 : 1);
                        for (int j = 0; j < m; j++) Face(centre, end * m + j, end * m + (j + 1) % m, outward);
                    }
                var mesh = new Mesh();
                mesh.name = "Frankenstein " + name;
                Rt.SetArray(mesh, "vertices", v.ToArray());
                Rt.SetArray(mesh, "triangles", tri.ToArray());
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.layer = root.gameObject.layer;
                go.transform.SetParent(root, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                return true;
            }
            catch (Exception e)
            {
                Melon<Plugin>.Logger.Warning("Garage: couldn't build the " + name + " mesh: " + (e.InnerException ?? e).Message);
                return false;
            }
        }

        static Vector3 Centre(Vector3[] ring)
        {
            var c = Vector3.zero;
            foreach (var p in ring) c += p;
            return c / ring.Length;
        }
    }
}
