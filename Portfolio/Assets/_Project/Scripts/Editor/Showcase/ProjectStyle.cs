using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.Editor.Showcase
{
    // Общий стиль демо-сцен: тёмный фон, тёмный пол с тонкой серой метровой сеткой, синий акцент для эффектов
    public static class ProjectStyle
    {
        public static readonly Color Background = Hex(0x1A1A1A);
        public static readonly Color Surface = Hex(0x222222);
        public static readonly Color Line = Hex(0x626262);
        public static readonly Color Accent = Hex(0x2096F3);

        const string Root = "Assets/_Project/Art/Style";
        const int PixelsPerMeter = 256;

        public static readonly Color BackdropTop = Hex(0x1A1A1A);
        public static readonly Color BackdropHorizon = Hex(0x626262);

        // Платформа со скруглёнными углами: линии каждый метр, тонкие — каждые полметра
        public static GameObject CreateFloor(Vector2 size, float cornerRadius)
        {
            var mesh = MeshAsset($"SM_Style_Floor_{size.x:0.##}x{size.y:0.##}");
            FloorMesh(mesh, size, cornerRadius, 0.25f);

            var floor = new GameObject("Floor") { isStatic = true };
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = floor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = FloorMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return floor;
        }

        // Стена или блок в той же сетке. Пивот внизу по центру, UV в метрах — клетки везде одного размера
        public static GameObject CreateGridBox(string name, Vector3 size)
        {
            var mesh = MeshAsset($"SM_Style_Box_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}");
            var builder = new PropMeshBuilder(1);
            builder.Box(0, Vector3.up * size.y * 0.5f, size);
            builder.Apply(mesh);

            var box = new GameObject(name) { isStatic = true };
            box.AddComponent<MeshFilter>().sharedMesh = mesh;
            box.AddComponent<MeshRenderer>().sharedMaterial = FloorMaterial();
            return box;
        }

        // Фон, отражения и заливка окружением для демо-сцены
        public static void ApplyEnvironment(Color ambient)
        {
            RenderSettings.skybox = CreateBackdrop();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(ambient.linear);
            RenderSettings.ambientProbe = probe;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = ReflectionCube();
        }

        // Однотонное окружение в тон фона: без него URP отражает дефолтное дневное небо и всё блестит
        static Cubemap ReflectionCube()
        {
            var path = Root + "/T_Style_Reflection.cubemap";
            const int size = 8;
            var color = Color.Lerp(BackdropTop, BackdropHorizon, 0.5f).linear;
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, false);
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY,
                         CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    cube.SetPixel(face, x, y, color);
            cube.Apply();
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        static Mesh MeshAsset(string name)
        {
            var path = $"{Root}/Meshes/{name.Replace(',', '.')}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // Студийный фон: тёмный верх плавно светлеет к горизонту
        public static Material CreateBackdrop()
        {
            var path = Root + "/M_Style_Backdrop.mat";
            var shader = Shader.Find("Portfolio/Style/Backdrop");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_TopColor", BackdropTop);
            material.SetColor("_HorizonColor", BackdropHorizon);
            material.SetColor("_BottomColor", Surface);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Верх — выпуклый скруглённый прямоугольник веером из центра, по контуру — бортик вниз.
        // UV верха в метрах от центра, поэтому линии сетки совпадают с целыми координатами мира
        static void FloorMesh(Mesh mesh, Vector2 size, float radius, float thickness)
        {
            const int cornerSegments = 12;
            var ix = size.x * 0.5f - radius;
            var iz = size.y * 0.5f - radius;
            var outline = new List<Vector3>();
            var corners = new[] { new Vector2(ix, iz), new Vector2(-ix, iz), new Vector2(-ix, -iz), new Vector2(ix, -iz) };
            for (var c = 0; c < 4; c++)
                for (var s = 0; s <= cornerSegments; s++)
                {
                    var angle = (c + (float)s / cornerSegments) * Mathf.PI * 0.5f;
                    outline.Add(new Vector3(corners[c].x + Mathf.Cos(angle) * radius, 0f, corners[c].y + Mathf.Sin(angle) * radius));
                }

            var vertices = new List<Vector3> { Vector3.zero };
            var normals = new List<Vector3> { Vector3.up };
            var uvs = new List<Vector2> { Vector2.zero };
            var indices = new List<int>();
            var n = outline.Count;
            foreach (var p in outline)
            {
                vertices.Add(p);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(p.x, p.z));
            }
            for (var i = 0; i < n; i++)
                indices.AddRange(new[] { 0, 1 + (i + 1) % n, 1 + i });

            // бортик: на нём середина тайла — цвет пола без линий
            var side = vertices.Count;
            for (var i = 0; i < n; i++)
            {
                var normal = new Vector3(outline[i].x, 0f, outline[i].z) - new Vector3(Mathf.Clamp(outline[i].x, -ix, ix), 0f, Mathf.Clamp(outline[i].z, -iz, iz));
                normal.Normalize();
                vertices.Add(outline[i]);
                vertices.Add(outline[i] + Vector3.down * thickness);
                normals.Add(normal);
                normals.Add(normal);
                uvs.Add(new Vector2(0.25f, 0.25f));
                uvs.Add(new Vector2(0.25f, 0.25f));
            }
            for (var i = 0; i < n; i++)
            {
                var a = side + i * 2;
                var b = side + (i + 1) % n * 2;
                indices.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        static Material FloorMaterial()
        {
            var (albedo, mask) = GridTextures();
            var path = Root + "/M_Style_Floor.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetTexture("_EmissionMap", mask);
            material.SetColor("_EmissionColor", Line * 0.35f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Тайл = 1 м². Линии лежат на границе тайла, поэтому при повторе каждая целая метка — одна сплошная линия
        static (Texture2D albedo, Texture2D mask) GridTextures()
        {
            const int size = PixelsPerMeter;
            var albedo = new Color[size * size];
            var mask = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var line = Mathf.Max(GridLine(x, size), GridLine(y, size));
                albedo[y * size + x] = Color.Lerp(Surface, Line, line);
                mask[y * size + x] = new Color(line, line, line, 1f);
            }
            return (Save(Root + "/T_Style_Grid.png", albedo, srgb: true), Save(Root + "/T_Style_Grid_Mask.png", mask, srgb: false));
        }

        static float GridLine(int pixel, int size)
        {
            var p = pixel + 0.5f;
            var meter = Mathf.Min(p, size - p);
            var half = Mathf.Abs(p - size * 0.5f);
            // метр — 1.2 см, полметра — 0.6 см и бледнее, с мягким краем в пиксель
            var major = Mathf.Clamp01(size * 0.006f + 0.5f - meter);
            var minor = Mathf.Clamp01(size * 0.003f + 0.5f - half) * 0.4f;
            return Mathf.Max(major, minor);
        }

        static Texture2D Save(string path, Color[] pixels, bool srgb)
        {
            var texture = new Texture2D(PixelsPerMeter, PixelsPerMeter, TextureFormat.RGBA32, false, !srgb);
            texture.SetPixels(pixels);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = srgb;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color Hex(int rgb) => new((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
