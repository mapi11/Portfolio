using System.IO;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Lightning
{
    static class StormAssetGenerator
    {
        // Атлас 2×2 следов удара. R — ветви фигуры Лихтенберга, G — размытое гало ветвей (где дольше держится жар),
        // B — выжженное пятно
        public static Texture2D ScorchAtlas(string path)
        {
            const int tile = 512;
            const int size = tile * 2;
            var pixels = new Color[size * size];
            for (var t = 0; t < 4; t++)
            {
                var (branches, halo, charMask) = ScorchTile(tile, 31 + t * 17);
                var ox = t % 2 * tile;
                var oy = t / 2 * tile;
                for (var y = 0; y < tile; y++)
                for (var x = 0; x < tile; x++)
                {
                    var i = y * tile + x;
                    pixels[(oy + y) * size + ox + x] = new Color(branches[i], halo[i], charMask[i], 1f);
                }
            }
            return Save(path, size, pixels);
        }

        static (float[] branches, float[] halo, float[] charMask) ScorchTile(int size, int seed)
        {
            var random = new System.Random(seed);
            var branches = new float[size * size];
            var center = new Vector2(size * 0.5f, size * 0.5f);

            var primaries = 7 + random.Next(4);
            for (var i = 0; i < primaries; i++)
            {
                var angle = (i + Range(random, -0.35f, 0.35f)) / primaries * Mathf.PI * 2f;
                var start = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(random, 4f, 10f);
                Walk(branches, size, start, angle, Range(random, 4.5f, 6.5f), Range(random, 0.6f, 1f) * size * 0.42f, 0, random);
            }

            var halo = Blur(branches, size, 7, 3);
            Normalize(halo, 0.7f);

            var charMask = new float[size * size];
            var offset = Range(random, 0f, 100f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var i = y * size + x;
                var r = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                var noise = Mathf.PerlinNoise(x * 0.015f + offset, y * 0.015f) * 0.65f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f + offset) * 0.35f;
                var blotch = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.34f + 0.18f * noise, r));
                var crater = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.09f, r));
                var value = Mathf.Max(blotch * (0.75f + 0.25f * noise), crater) + halo[i] * 0.55f + branches[i] * 0.25f;
                // края тайла должны быть пустыми — атлас без зазоров
                charMask[i] = Mathf.Clamp01(value) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 0.98f, r)));
            }
            return (branches, halo, charMask);
        }

        // Ветвь разряда по земле: ломаный путь от центра наружу, по дороге отщепляет более тонкие ветви
        static void Walk(float[] buffer, int size, Vector2 position, float angle, float thickness, float length, int depth, System.Random random)
        {
            var center = size * 0.5f;
            const float step = 1f;
            var travelled = 0f;
            while (travelled < length)
            {
                var k = travelled / length;
                var radius = Mathf.Lerp(thickness, 0.75f, Mathf.Pow(k, 0.6f));
                Stamp(buffer, size, position, radius);

                var outward = Mathf.Atan2(position.y - center, position.x - center);
                angle += Gauss(random) * 0.5f;
                angle = Mathf.LerpAngle(angle * Mathf.Rad2Deg, outward * Mathf.Rad2Deg, 0.07f) * Mathf.Deg2Rad;
                position += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
                travelled += step;

                if (Vector2.Distance(position, new Vector2(center, center)) > size * 0.46f)
                    break;
                if (depth < 5 && random.NextDouble() < 0.02 + depth * 0.006)
                {
                    var side = random.NextDouble() < 0.5 ? -1f : 1f;
                    Walk(buffer, size, position, angle + side * Range(random, 0.35f, 0.9f), radius * 0.72f,
                        (length - travelled) * Range(random, 0.45f, 0.85f), depth + 1, random);
                }
            }
        }

        static void Stamp(float[] buffer, int size, Vector2 center, float radius)
        {
            var r = Mathf.CeilToInt(radius + 1f);
            var cx = Mathf.RoundToInt(center.x);
            var cy = Mathf.RoundToInt(center.y);
            for (var y = cy - r; y <= cy + r; y++)
            for (var x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size)
                    continue;
                var value = Mathf.Clamp01(radius + 0.5f - Vector2.Distance(new Vector2(x, y), center));
                var i = y * size + x;
                buffer[i] = Mathf.Max(buffer[i], value);
            }
        }

        static float[] Blur(float[] source, int size, int radius, int passes)
        {
            var a = (float[])source.Clone();
            var b = new float[a.Length];
            for (var p = 0; p < passes; p++)
            {
                BoxBlur(a, b, size, radius, 1, size);
                BoxBlur(b, a, size, radius, size, 1);
            }
            return a;
        }

        static void BoxBlur(float[] from, float[] to, int size, int radius, int stride, int lineStride)
        {
            for (var line = 0; line < size; line++)
            {
                var start = line * lineStride;
                var sum = 0f;
                for (var i = -radius; i <= radius; i++)
                    sum += from[start + Mathf.Clamp(i, 0, size - 1) * stride];
                for (var i = 0; i < size; i++)
                {
                    to[start + i * stride] = sum / (radius * 2 + 1);
                    sum += from[start + Mathf.Min(i + radius + 1, size - 1) * stride];
                    sum -= from[start + Mathf.Max(i - radius, 0) * stride];
                }
            }
        }

        static void Normalize(float[] values, float power)
        {
            var max = 0.0001f;
            foreach (var v in values)
                max = Mathf.Max(max, v);
            for (var i = 0; i < values.Length; i++)
                values[i] = Mathf.Pow(values[i] / max, power);
        }

        public static void Quad(Mesh mesh)
        {
            mesh.Clear();
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            mesh.RecalculateBounds();
        }

        static Texture2D Save(string path, int size, Color[] pixels)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.SetPixels(pixels);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        static float Gauss(System.Random random) =>
            ((float)random.NextDouble() + (float)random.NextDouble() + (float)random.NextDouble() - 1.5f) * 1.2f;
    }
}
