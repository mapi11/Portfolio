using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.Editor.Winter
{
    static class WinterMeshGenerator
    {
        public const float IceLevel = -0.3f;
        const float RiverHalfWidth = 2.6f;
        const float BankWidth = 2.2f;

        public static float RiverZ(float x) => Mathf.Sin(x * 0.11f) * 3.2f + Mathf.Sin(x * 0.043f + 1.3f) * 2f - 4f;

        public static float DistanceToRiver(float x, float z)
        {
            var slope = (RiverZ(x + 0.1f) - RiverZ(x - 0.1f)) / 0.2f;
            return Mathf.Abs(z - RiverZ(x)) / Mathf.Sqrt(1f + slope * slope);
        }

        public static float Height(float x, float z)
        {
            var hills = (Fbm(x * 0.035f + 11f, z * 0.035f + 5f) - 0.45f) * 2.2f;
            var bowl = Mathf.Max(0f, new Vector2(x, z * 1.2f).magnitude - 22f) * 0.12f;
            var distance = DistanceToRiver(x, z);
            var bank = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RiverHalfWidth * 0.75f, RiverHalfWidth + BankWidth, distance));
            return Mathf.Lerp(IceLevel - 0.25f, hills + bowl, bank);
        }

        public static bool IsOnBank(float x, float z, float margin) => DistanceToRiver(x, z) > RiverHalfWidth + margin;

        public static void Terrain(Mesh mesh, float size, float step)
        {
            var count = Mathf.RoundToInt(size / step) + 1;
            var vertices = new Vector3[count * count];
            var colors = new Color[count * count];
            var half = size * 0.5f;
            for (var z = 0; z < count; z++)
            for (var x = 0; x < count; x++)
            {
                var px = x * step - half;
                var pz = z * step - half;
                vertices[z * count + x] = new Vector3(px, Height(px, pz), pz);
                colors[z * count + x] = new Color(0.42f, 0.36f, 0.33f, 1f);
            }

            var indices = new int[(count - 1) * (count - 1) * 6];
            var i = 0;
            for (var z = 0; z < count - 1; z++)
            for (var x = 0; x < count - 1; x++)
            {
                var a = z * count + x;
                indices[i++] = a; indices[i++] = a + count; indices[i++] = a + 1;
                indices[i++] = a + 1; indices[i++] = a + count; indices[i++] = a + count + 1;
            }

            Apply(mesh, vertices, colors, indices, null);
        }

        public static void River(Mesh mesh, float length, float step)
        {
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();
            var halfWidth = RiverHalfWidth + 0.9f;
            const int across = 6;

            var rows = Mathf.RoundToInt(length / step) + 1;
            for (var r = 0; r < rows; r++)
            {
                var x = r * step - length * 0.5f;
                var slope = (RiverZ(x + 0.1f) - RiverZ(x - 0.1f)) / 0.2f;
                var side = new Vector2(-slope, 1f).normalized;
                var center = new Vector2(x, RiverZ(x));
                for (var c = 0; c <= across; c++)
                {
                    var v = (float)c / across;
                    var p = center + side * Mathf.Lerp(-halfWidth, halfWidth, v);
                    vertices.Add(new Vector3(p.x, IceLevel, p.y));
                    uvs.Add(new Vector2(x, v));
                }
                if (r == 0)
                    continue;
                for (var c = 0; c < across; c++)
                {
                    var a = (r - 1) * (across + 1) + c;
                    var b = a + across + 1;
                    indices.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public static void Pine(Mesh mesh, int seed)
        {
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();

            AddCylinder(vertices, normals, colors, indices, 0.34f, 0.08f, 6.5f, 9, new Color(0.42f, 0.27f, 0.18f, 0f));

            const int tiers = 5;
            for (var t = 0; t < tiers; t++)
            {
                var k = (float)t / (tiers - 1);
                var baseY = Mathf.Lerp(1.1f, 5.3f, k);
                var radius = Mathf.Lerp(2.5f, 0.85f, k);
                var height = Mathf.Lerp(1.9f, 1.5f, k);
                var phase = Next() * 6.28f;
                var dark = Color.Lerp(new Color(0.07f, 0.26f, 0.24f), new Color(0.12f, 0.33f, 0.27f), k);
                var light = Color.Lerp(new Color(0.18f, 0.46f, 0.33f), new Color(0.26f, 0.55f, 0.36f), k);
                AddTier(vertices, normals, colors, indices, baseY, radius, height, phase, dark, light);
            }

            Apply(mesh, vertices, colors, indices, normals);
        }

        static void AddTier(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> indices,
            float baseY, float radius, float height, float phase, Color dark, Color light)
        {
            const int segments = 28;
            const int rings = 4;
            var start = vertices.Count;
            var apex = new Vector3(0f, baseY + height, 0f);
            var center = new Vector3(0f, baseY + height * 0.35f, 0f);

            for (var r = 0; r <= rings; r++)
            {
                var t = (float)r / rings;
                for (var s = 0; s <= segments; s++)
                {
                    var angle = s * Mathf.PI * 2f / segments;
                    var wave = Mathf.Sin(angle * 7f + phase);
                    var rimRadius = radius * (1f + 0.1f * wave);
                    var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var droop = t * t * (0.35f + 0.12f * wave);
                    var position = apex + radial * (rimRadius * t) + Vector3.down * (height * t + droop);
                    var cone = (radial * height + Vector3.up * radius).normalized;
                    var normal = Vector3.Lerp(cone, (position - center).normalized, 0.55f).normalized;
                    vertices.Add(position);
                    normals.Add(normal);
                    var color = Color.Lerp(dark, light, Mathf.Lerp(0.3f, 1f, t) * (0.85f + 0.15f * wave));
                    color.a = 1f;
                    colors.Add(color);
                }
            }

            for (var r = 0; r < rings; r++)
            for (var s = 0; s < segments; s++)
            {
                var a = start + r * (segments + 1) + s;
                var b = a + segments + 1;
                indices.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }

            var rimStart = start + rings * (segments + 1);
            var underApex = vertices.Count;
            vertices.Add(new Vector3(0f, baseY + 0.25f, 0f));
            normals.Add(Vector3.down);
            colors.Add(new Color(dark.r * 0.6f, dark.g * 0.6f, dark.b * 0.6f, 0f));
            var underStart = vertices.Count;
            for (var s = 0; s <= segments; s++)
            {
                vertices.Add(vertices[rimStart + s]);
                var radial = vertices[rimStart + s];
                radial.y = 0f;
                normals.Add((radial.normalized * 0.4f + Vector3.down).normalized);
                var c = dark;
                c.a = 0f;
                colors.Add(c);
            }
            for (var s = 0; s < segments; s++)
                indices.AddRange(new[] { underApex, underStart + s, underStart + s + 1 });
        }

        static void AddCylinder(List<Vector3> vertices, List<Vector3> normals, List<Color> colors, List<int> indices,
            float bottomRadius, float topRadius, float height, int segments, Color color)
        {
            var start = vertices.Count;
            for (var r = 0; r <= 1; r++)
            for (var s = 0; s <= segments; s++)
            {
                var angle = s * Mathf.PI * 2f / segments;
                var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(radial * (r == 0 ? bottomRadius : topRadius) + Vector3.up * (r == 0 ? -0.3f : height));
                normals.Add(radial);
                colors.Add(color);
            }
            for (var s = 0; s < segments; s++)
            {
                var a = start + s;
                var b = a + segments + 1;
                indices.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        }

        public static void Rock(Mesh mesh, int seed)
        {
            var random = new System.Random(seed);
            var offset = new Vector3((float)random.NextDouble() * 50f, (float)random.NextDouble() * 50f, 0f);
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            Icosphere(vertices, indices, 2);

            var tint = Color.Lerp(new Color(0.38f, 0.4f, 0.48f), new Color(0.5f, 0.48f, 0.5f), (float)random.NextDouble());
            var colors = new List<Color>();
            for (var i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                var n = Mathf.PerlinNoise(v.x * 1.8f + offset.x, v.z * 1.8f + v.y * 1.3f + offset.y);
                var p = v * (0.8f + n * 0.45f);
                p.y = p.y > 0f ? p.y * 0.65f : p.y * 0.3f;
                vertices[i] = p;
                colors.Add(new Color(tint.r, tint.g, tint.b, 1f));
            }

            Apply(mesh, vertices.ToArray(), colors.ToArray(), indices.ToArray(), null);
        }

        internal static void Icosphere(List<Vector3> vertices, List<int> indices, int subdivisions)
        {
            var t = (1f + Mathf.Sqrt(5f)) / 2f;
            vertices.AddRange(new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            });
            for (var i = 0; i < vertices.Count; i++)
                vertices[i] = vertices[i].normalized;
            indices.AddRange(new[]
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            });

            for (var s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>();
                int Middle(int a, int b)
                {
                    var key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out var index))
                        return index;
                    vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
                    cache[key] = vertices.Count - 1;
                    return vertices.Count - 1;
                }
                for (var i = 0; i < indices.Count; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    int ab = Middle(a, b), bc = Middle(b, c), ca = Middle(c, a);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                indices.Clear();
                indices.AddRange(next);
            }
        }

        public static void GrassTufts(Mesh mesh, IList<Vector3> positions, int seed)
        {
            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            var root = new Color(0.35f, 0.38f, 0.2f, 0f);
            var tip = new Color(0.82f, 0.72f, 0.38f, 0f);

            foreach (var position in positions)
            {
                var blades = 8 + random.Next(6);
                var scale = Mathf.Lerp(0.7f, 1.2f, Next());
                for (var b = 0; b < blades; b++)
                {
                    var angle = Next() * Mathf.PI * 2f;
                    var forward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var right = new Vector3(-forward.z, 0f, forward.x);
                    var rootPosition = position + forward * (Next() * 0.08f);
                    var height = Mathf.Lerp(0.35f, 0.75f, Next()) * scale;
                    var lean = Mathf.Lerp(0.15f, 0.45f, Next()) * height;
                    var width = Mathf.Lerp(0.05f, 0.08f, Next()) * scale;
                    var start = vertices.Count;
                    const int segments = 3;
                    for (var s = 0; s <= segments; s++)
                    {
                        var t = (float)s / segments;
                        var center = rootPosition + Vector3.up * (height * t) + forward * (lean * t * t);
                        var color = Color.Lerp(root, tip, t);
                        if (s == segments)
                        {
                            vertices.Add(center);
                            normals.Add(Vector3.up);
                            colors.Add(color);
                            break;
                        }
                        var halfWidth = width * 0.5f * (1f - t);
                        vertices.Add(center - right * halfWidth);
                        vertices.Add(center + right * halfWidth);
                        normals.Add(Vector3.up);
                        normals.Add(Vector3.up);
                        colors.Add(color);
                        colors.Add(color);
                    }
                    for (var s = 0; s < segments - 1; s++)
                    {
                        var a = start + s * 2;
                        indices.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3, a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                    }
                    var last = start + (segments - 1) * 2;
                    indices.AddRange(new[] { last, last + 2, last + 1, last, last + 1, last + 2 });
                }
            }

            Apply(mesh, vertices.ToArray(), colors.ToArray(), indices.ToArray(), normals.ToArray());
        }

        static void Apply(Mesh mesh, IList<Vector3> vertices, IList<Color> colors, IList<int> indices, IList<Vector3> normals)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(new List<Vector3>(vertices));
            mesh.SetColors(new List<Color>(colors));
            mesh.SetTriangles(new List<int>(indices), 0);
            if (normals != null)
                mesh.SetNormals(new List<Vector3>(normals));
            else
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public static float Fbm(float x, float y)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            for (var i = 0; i < 4; i++)
            {
                sum += Mathf.PerlinNoise(x, y) * amplitude;
                x *= 2.03f;
                y *= 2.03f;
                amplitude *= 0.5f;
            }
            return sum / 0.9375f;
        }
    }
}
