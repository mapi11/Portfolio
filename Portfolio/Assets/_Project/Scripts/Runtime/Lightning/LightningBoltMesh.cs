using System.Collections.Generic;
using UnityEngine;

namespace Portfolio.Lightning
{
    // Канал разряда: ломаная от облака до земли с ветвлениями. Каждая ветка — лента из пар вершин,
    // к камере её разворачивает вершинный шейдер, поэтому меш один на любой ракурс.
    public static class LightningBoltMesh
    {
        const float Roughness = 0.22f;

        static readonly List<Vector3> s_Vertices = new();
        static readonly List<Vector3> s_Tangents = new();
        static readonly List<Vector4> s_Uvs = new();
        static readonly List<Color> s_Colors = new();
        static readonly List<int> s_Indices = new();

        public static void Build(Mesh mesh, Vector3 top, Vector3 ground, System.Random random)
        {
            s_Vertices.Clear();
            s_Tangents.Clear();
            s_Uvs.Clear();
            s_Colors.Clear();
            s_Indices.Clear();

            var main = new List<Vector3> { top, ground };
            Subdivide(main, 8, random);
            var mainLength = Length(main);
            AddChannel(main, 0f, mainLength, 1f, 1f, false, random);

            var branches = 5 + random.Next(5);
            for (var b = 0; b < branches; b++)
            {
                // у реальных разрядов ветки гуще в верхней половине
                var k = Mathf.Pow((float)random.NextDouble(), 1.4f);
                var index = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0.08f, 0.85f, k) * (main.Count - 1)), 1, main.Count - 2);
                Branch(main, index, ArcLength(main, index) / mainLength, mainLength, ground.y, 0.55f, 0.65f, 0, random);
            }

            mesh.Clear();
            mesh.SetVertices(s_Vertices);
            mesh.SetNormals(s_Tangents);
            mesh.SetUVs(0, s_Uvs);
            mesh.SetColors(s_Colors);
            mesh.SetTriangles(s_Indices, 0);
            mesh.RecalculateBounds();
            var bounds = mesh.bounds;
            bounds.Expand(6f);
            mesh.bounds = bounds;
        }

        static void Branch(List<Vector3> parent, int index, float arrival, float mainLength, float groundY,
            float width, float intensity, int depth, System.Random random)
        {
            var start = parent[index];
            var tangent = (parent[index + 1] - parent[index - 1]).normalized;
            var outward = Vector3.Cross(tangent, RandomUnit(random)).normalized;
            var direction = (tangent + outward * Range(random, 0.7f, 1.5f)).normalized;
            direction.y = Mathf.Min(direction.y, -0.3f);
            direction.Normalize();

            var length = Range(random, 7f, 22f) * (1f - arrival * 0.5f) * (depth == 0 ? 1f : 0.45f);
            var end = start + direction * length;
            end.y = Mathf.Max(end.y, groundY + 2f);

            var points = new List<Vector3> { start, end };
            Subdivide(points, 6 - depth, random);
            AddChannel(points, arrival, mainLength, width, intensity, true, random);

            if (depth > 0)
                return;
            var children = random.Next(3);
            for (var c = 0; c < children; c++)
            {
                var i = Mathf.Clamp(random.Next(points.Count / 5, points.Count * 4 / 5), 1, points.Count - 2);
                var childArrival = arrival + ArcLength(points, i) / mainLength;
                Branch(points, i, childArrival, mainLength, groundY, width * 0.55f, intensity * 0.7f, depth + 1, random);
            }
        }

        // Смещение середин: на каждом уровне отрезок ломается перпендикулярно себе, амплитуда падает вместе с длиной
        static void Subdivide(List<Vector3> points, int levels, System.Random random)
        {
            var next = new List<Vector3>();
            for (var level = 0; level < levels; level++)
            {
                next.Clear();
                for (var i = 0; i < points.Count - 1; i++)
                {
                    var a = points[i];
                    var b = points[i + 1];
                    var segment = b - a;
                    var offset = Vector3.Cross(segment.normalized, RandomUnit(random)).normalized;
                    next.Add(a);
                    next.Add((a + b) * 0.5f + offset * (segment.magnitude * Roughness * Gauss(random)));
                }
                next.Add(points[^1]);
                points.Clear();
                points.AddRange(next);
            }
        }

        static void AddChannel(List<Vector3> points, float arrival, float mainLength, float width, float intensity,
            bool branch, System.Random random)
        {
            var total = Length(points);
            var noiseOffset = Range(random, 0f, 500f);
            var first = s_Vertices.Count;
            var arc = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                if (i > 0)
                    arc += Vector3.Distance(points[i - 1], points[i]);
                var tangent = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                var w = branch ? width * Mathf.Lerp(1f, 0.15f, arc / total) : width;
                for (var side = -1; side <= 1; side += 2)
                {
                    s_Vertices.Add(points[i]);
                    s_Tangents.Add(tangent);
                    s_Uvs.Add(new Vector4(side, arrival + arc / mainLength, noiseOffset + arc, branch ? 1f : 0f));
                    s_Colors.Add(new Color(w, intensity, 0f, 1f));
                }
                if (i == 0)
                    continue;
                var a = first + (i - 1) * 2;
                s_Indices.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
        }

        static float Length(List<Vector3> points) => ArcLength(points, points.Count - 1);

        static float ArcLength(List<Vector3> points, int index)
        {
            var length = 0f;
            for (var i = 1; i <= index; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        static float Gauss(System.Random random) =>
            ((float)random.NextDouble() + (float)random.NextDouble() + (float)random.NextDouble() - 1.5f) * 1.2f;

        static Vector3 RandomUnit(System.Random random)
        {
            var z = Range(random, -1f, 1f);
            var angle = Range(random, 0f, Mathf.PI * 2f);
            var r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(angle), r * Mathf.Sin(angle), z);
        }
    }
}
