using System.Collections.Generic;
using UnityEngine;

namespace Portfolio.Editor.Grass
{
    /// <summary>
    /// Меш чанка травы: травинки из нескольких сегментов, сужающиеся к кончику,
    /// с изгибом вперёд. Каждой вершине записываются корень травинки и два случайных
    /// числа (TEXCOORD1) — шейдеру хватает этого для ветра, цвета и исчезновения,
    /// без текстур и без поиска данных по ID.
    ///
    /// LOD1 — каждая N-я травинка того же распределения, шире и из одного треугольника:
    /// переход между LOD читается как разрежение, а не как смена поля.
    /// </summary>
    static class GrassMeshGenerator
    {
        public struct Settings
        {
            public float chunkSize;
            public int blades;
            public Vector2 height;
            public Vector2 width;
            public Vector2 lean;
            public int seed;
        }

        struct Blade
        {
            public Vector3 root;
            public Vector3 forward;
            public Vector3 right;
            public float height;
            public float width;
            public float lean;
            public Vector4 data;
        }

        /// <returns>Число травинок в LOD0 и LOD1.</returns>
        public static (int lod0, int lod1) Build(Mesh lod0, Mesh lod1, Settings settings, int lod1Step, float lod1WidthScale)
        {
            var blades = CreateBlades(settings);
            return (Fill(lod0, blades, 1, 3, 1f), Fill(lod1, blades, lod1Step, 1, lod1WidthScale));
        }

        static List<Blade> CreateBlades(Settings settings)
        {
            var random = new System.Random(settings.seed);
            float Next() => (float)random.NextDouble();

            // Jittered grid: равномерное покрытие без проплешин и сгустков. Сетка
            // заполняется целиком, иначе у края каждого чанка остаётся пустая полоса.
            var grid = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(settings.blades)));
            var count = grid * grid;
            var cell = settings.chunkSize / grid;
            var half = settings.chunkSize * 0.5f;
            var result = new List<Blade>(count);

            for (var i = 0; i < count; i++)
            {
                var root = new Vector3((i % grid + Next()) * cell - half, 0f, (i / grid % grid + Next()) * cell - half);
                var rotation = Quaternion.Euler(0f, Next() * 360f, 0f);
                var height = Mathf.Lerp(settings.height.x, settings.height.y, Mathf.Pow(Next(), 0.7f));
                result.Add(new Blade
                {
                    root = root,
                    forward = rotation * Vector3.forward,
                    right = rotation * Vector3.right,
                    height = height,
                    width = Mathf.Lerp(settings.width.x, settings.width.y, Next()),
                    lean = Mathf.Lerp(settings.lean.x, settings.lean.y, Next()) * height,
                    data = new Vector4(root.x, root.z, Next(), Next())
                });
            }

            // Перемешиваем порядок: LOD1 берёт каждую N-ю травинку, и это должна быть
            // случайная выборка по всему чанку, а не регулярные столбцы сетки.
            for (var i = result.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (result[i], result[j]) = (result[j], result[i]);
            }
            return result;
        }

        static int Fill(Mesh mesh, List<Blade> blades, int step, int segments, float widthScale)
        {
            var bladeCount = 0;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector4>();
            var indices = new List<int>();

            for (var b = 0; b < blades.Count; b += step)
            {
                var blade = blades[b];
                var start = vertices.Count;
                bladeCount++;

                for (var s = 0; s <= segments; s++)
                {
                    var t = (float)s / segments;
                    var center = blade.root + Vector3.up * (blade.height * t) + blade.forward * (blade.lean * t * t);
                    var tangent = (Vector3.up * blade.height + blade.forward * (2f * blade.lean * t)).normalized;
                    var normal = Vector3.Cross(blade.right, tangent);
                    if (normal.y < 0f)
                        normal = -normal;

                    if (s == segments)
                    {
                        Add(center, normal, new Vector2(0.5f, 1f));
                        break;
                    }

                    var halfWidth = blade.width * widthScale * 0.5f * Mathf.Pow(1f - t, 0.7f);
                    Add(center - blade.right * halfWidth, normal, new Vector2(0f, t));
                    Add(center + blade.right * halfWidth, normal, new Vector2(1f, t));
                }

                for (var s = 0; s < segments - 1; s++)
                {
                    var a = start + s * 2;
                    indices.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
                }
                var lastRow = start + (segments - 1) * 2;
                indices.AddRange(new[] { lastRow, lastRow + 2, lastRow + 1 });

                void Add(Vector3 position, Vector3 normal, Vector2 uv)
                {
                    vertices.Add(position);
                    normals.Add(normal);
                    uv0.Add(uv);
                    uv1.Add(blade.data);
                }
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            return bladeCount;
        }
    }
}
