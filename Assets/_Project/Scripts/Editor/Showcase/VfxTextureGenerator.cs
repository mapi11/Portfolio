using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Showcase
{
    /// <summary>
    /// Процедурные временные текстуры: атлас частиц 2x2 и атлас следов от пуль 2x2.
    /// Белый цвет + маска в альфе: оттенок задаёт материал или цвет частицы.
    /// Заменяются на текстуры из Substance Designer / EmberGen без изменений в материалах.
    /// </summary>
    static class VfxTextureGenerator
    {
        public const int ParticleFlash = 0, ParticleSoft = 1, ParticleSmoke = 2, ParticleChip = 3;
        public const int DecalMetal = 0, DecalConcrete = 1, DecalWood = 2;

        const int Tile = 128;

        public static Texture2D CreateParticleAtlas(string path) => Create(path, (tile, u, v) => tile switch
        {
            ParticleFlash => Flash(u, v),
            ParticleSoft => Soft(u, v),
            ParticleSmoke => Smoke(u, v),
            _ => Chip(u, v)
        });

        public static Texture2D CreateDecalAtlas(string path) => Create(path, (tile, u, v) => tile switch
        {
            DecalMetal => MetalHole(u, v),
            DecalWood => WoodHole(u, v),
            _ => ConcreteHole(u, v)
        });

        static Texture2D Create(string path, Func<int, float, float, Color> pixel)
        {
            var size = Tile * 2;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var tile = x / Tile + y / Tile * 2;
                var u = (x % Tile + 0.5f) / Tile * 2f - 1f;
                var v = (y % Tile + 0.5f) / Tile * 2f - 1f;
                texture.SetPixel(x, y, pixel(tile, u, v));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = size;
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = size;
                settings.format = TextureImporterFormat.ASTC_6x6;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Тайлинг материала под ячейку атласа 2x2 (0 — левая нижняя).</summary>
        public static void SetTile(Material material, int tile)
        {
            material.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
            material.SetTextureOffset("_BaseMap", new Vector2(tile % 2 * 0.5f, tile / 2 * 0.5f));
        }

        // --- Частицы ---

        static Color Flash(float u, float v)
        {
            var r = Mathf.Sqrt(u * u + v * v);
            var core = Mathf.Exp(-r * r * 10f);
            // Лучи: крест + диагонали послабее, затухают к краю ячейки.
            var cross = Mathf.Exp(-Mathf.Abs(u) * 30f) * (1f - Mathf.Abs(v)) + Mathf.Exp(-Mathf.Abs(v) * 30f) * (1f - Mathf.Abs(u));
            var du = (u + v) * 0.7071f;
            var dv = (u - v) * 0.7071f;
            var diagonal = Mathf.Exp(-Mathf.Abs(du) * 40f) * Mathf.Clamp01(1f - Mathf.Abs(dv) * 1.4f) +
                           Mathf.Exp(-Mathf.Abs(dv) * 40f) * Mathf.Clamp01(1f - Mathf.Abs(du) * 1.4f);
            var alpha = Mathf.Clamp01(core + cross * 0.8f + diagonal * 0.4f) * Edge(r);
            return new Color(1f, 1f, 1f, alpha);
        }

        static Color Soft(float u, float v)
        {
            var r = Mathf.Sqrt(u * u + v * v);
            var a = Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a * a);
        }

        static Color Smoke(float u, float v)
        {
            var r = Mathf.Sqrt(u * u + v * v);
            var noise = Fbm(u * 2.2f + 3.1f, v * 2.2f + 7.3f);
            var alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.2f, 1f, r + (noise - 0.5f) * 0.5f));
            // Лёгкое затенение снизу даёт объём без освещения.
            var shade = Mathf.Lerp(0.75f, 1f, (v + 1f) * 0.5f);
            return new Color(shade, shade, shade, alpha * Mathf.Clamp01(noise * 1.6f - 0.1f));
        }

        static Color Chip(float u, float v)
        {
            var angle = Mathf.Atan2(v, u);
            var r = Mathf.Sqrt(u * u + v * v);
            var radius = 0.45f + 0.35f * Mathf.PerlinNoise(Mathf.Cos(angle) * 1.5f + 5f, Mathf.Sin(angle) * 1.5f + 5f);
            var alpha = Mathf.Clamp01((radius - r) * 30f);
            var shade = Mathf.Lerp(0.65f, 1f, (u - v + 2f) * 0.25f);
            return new Color(shade, shade, shade, alpha);
        }

        // --- Следы от пуль (premultiplied в шейдере: тёмный цвет + альфа затемняют поверхность) ---

        static Color MetalHole(float u, float v)
        {
            var r = Mathf.Sqrt(u * u + v * v);
            if (r < 0.12f)
                return new Color(0.02f, 0.02f, 0.02f, 1f);
            // Сбитая краска: светлое кольцо голого металла с неровным краем.
            var ring = 0.24f + 0.06f * Mathf.PerlinNoise(Mathf.Atan2(v, u) * 2f + 10f, 0.5f);
            if (r < ring)
                return new Color(0.78f, 0.78f, 0.8f, 0.95f);
            var soot = Mathf.Clamp01(1f - (r - ring) / 0.5f);
            return new Color(0.05f, 0.05f, 0.05f, soot * soot * 0.5f);
        }

        static Color ConcreteHole(float u, float v)
        {
            var r = Mathf.Sqrt(u * u + v * v);
            var angle = Mathf.Atan2(v, u);
            var jagged = 0.12f * Mathf.PerlinNoise(angle * 3f + 20f, 1.5f);
            if (r < 0.12f + jagged * 0.5f)
                return new Color(0.04f, 0.04f, 0.04f, 1f);
            if (r < 0.42f + jagged)
            {
                // Скол: светлее основы, с шумом.
                var n = Mathf.PerlinNoise(u * 8f + 30f, v * 8f + 30f);
                return new Color(0.68f, 0.66f, 0.63f, Mathf.Lerp(0.5f, 0.9f, n));
            }
            // Радиальные трещины.
            var crack = Mathf.Pow(Mathf.Abs(Mathf.Sin(angle * 3.5f + Mathf.PerlinNoise(r * 4f, angle) * 2f)), 40f);
            var fade = Mathf.Clamp01(1f - (r - 0.42f) / 0.5f);
            return new Color(0.08f, 0.08f, 0.08f, crack * fade * 0.8f + fade * fade * 0.15f);
        }

        static Color WoodHole(float u, float v)
        {
            var r = Mathf.Sqrt(u * u + v * v);
            if (r < 0.1f)
                return new Color(0.03f, 0.02f, 0.01f, 1f);
            // Щепа вытянута вдоль волокон (ось V).
            var stretched = Mathf.Sqrt(u * u * 6f + v * v * 0.6f);
            var noise = Mathf.PerlinNoise(u * 12f + 50f, v * 2f + 50f);
            if (stretched < 0.55f + noise * 0.3f)
                return new Color(0.78f, 0.6f, 0.4f, 0.9f); // свежая древесина светлее покрытия
            var fade = Mathf.Clamp01(1f - (r - 0.3f) / 0.5f);
            return new Color(0.1f, 0.07f, 0.04f, fade * fade * 0.25f);
        }

        static float Edge(float r) => Mathf.Clamp01((1f - r) * 4f);

        static float Fbm(float x, float y)
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
