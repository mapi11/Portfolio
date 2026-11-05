using System.Collections.Generic;
using System.IO;
using Portfolio.Editor.Showcase;
using Portfolio.Editor.Winter;
using Portfolio.Grass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portfolio.Editor.Grass
{
    public static class GrassSceneBuilder
    {
        const string Root = "Assets/_Project/Art/Grass";
        const string ScenePath = "Assets/_Project/Scenes/GrassField.unity";

        const float ChunkSize = 4f;
        const int Lod0Blades = 1600;
        const int Lod1Step = 4;

        const float PlatformSize = 30f;
        const float RockPathRadius = 5f;

        [MenuItem("Tools/Portfolio/Build Grass Field")]
        public static void Build()
        {
            var lod0 = MeshAsset(Root + "/Meshes/SM_GrassChunk_LOD0.asset");
            var lod1 = MeshAsset(Root + "/Meshes/SM_GrassChunk_LOD1.asset");
            var (lod0Blades, lod1Blades) = GrassMeshGenerator.Build(lod0, lod1, new GrassMeshGenerator.Settings
            {
                chunkSize = ChunkSize,
                blades = Lod0Blades,
                height = new Vector2(0.35f, 0.8f),
                width = new Vector2(0.08f, 0.13f),
                lean = new Vector2(0.15f, 0.5f),
                seed = 7,
                flowerPatches = 4,
                flowerPatchRadius = 0.55f,
                flowerDensity = 0.28f
            }, Lod1Step, 2.2f);
            EditorUtility.SetDirty(lod0);
            EditorUtility.SetDirty(lod1);

            var noise = CreateNoise(Root + "/Textures/T_Grass_Wind_Mask.png");

            // сочная луговая трава: тёмный корень, жёлто-зелёный кончик, сильный просвет против солнца
            var grass = MaterialAsset(Root + "/Materials/M_Grass.mat", Shader.Find("Portfolio/Grass"));
            grass.enableInstancing = true;
            grass.SetColor("_RootColor", new Color(0.16f, 0.34f, 0.06f));
            grass.SetColor("_TipColor", new Color(0.78f, 0.93f, 0.32f));
            grass.SetColor("_DryColor", new Color(0.9f, 0.85f, 0.45f));
            grass.SetColor("_TranslucencyColor", new Color(0.85f, 1f, 0.35f));
            grass.SetFloat("_DryAmount", 0.06f);
            grass.SetFloat("_ColorVariation", 0.25f);
            grass.SetFloat("_RootAO", 0.4f);
            grass.SetFloat("_Translucency", 2.2f);
            grass.SetFloat("_PatchScale", 0.05f);
            grass.SetFloat("_PatchMinHeight", 0.6f);

            var rock = MaterialAsset(Root + "/Materials/M_Grass_Rock.mat", Shader.Find("Universal Render Pipeline/Lit"));
            rock.SetColor("_BaseColor", new Color(0.34f, 0.36f, 0.4f));
            rock.SetFloat("_Smoothness", 0.15f);

            var rollingStone = MeshAsset(Root + "/Meshes/SM_Grass_RollingStone.asset");
            Boulder(rollingStone, 3, 0.12f, 0.95f, 0.9f);
            EditorUtility.SetDirty(rollingStone);
            var boulders = new Mesh[3];
            for (var i = 0; i < boulders.Length; i++)
            {
                boulders[i] = MeshAsset(Root + $"/Meshes/SM_Grass_Boulder_{i}.asset");
                Boulder(boulders[i], 11 + i * 7, 0.25f, 0.6f, 0.7f);
                EditorUtility.SetDirty(boulders[i]);
            }
            AssetDatabase.SaveAssets();

            BuildScene(lod0, lod1, lod0Blades, lod1Blades, grass, rock, rollingStone, boulders, noise);
            Debug.Log("Grass field built: " + ScenePath);
        }

        static void BuildScene(Mesh lod0, Mesh lod1, int lod0Blades, int lod1Blades,
            Material grass, Material rockMaterial, Mesh rollingStone, Mesh[] boulders, Texture2D noise)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ProjectStyle.CreateFloor(Vector2.one * PlatformSize, 2.5f);

            var grassGo = new GameObject("Grass");
            var wind = grassGo.AddComponent<GrassWind>();
            wind.noise = noise;
            var field = grassGo.AddComponent<GrassField>();
            field.lod0 = lod0;
            field.lod1 = lod1;
            field.lod0Blades = lod0Blades;
            field.lod1Blades = lod1Blades;
            field.material = grass;
            field.chunkSize = ChunkSize;
            // на полчанка меньше платформы, чтобы трава не свисала с краёв
            field.size = Vector2.one * (PlatformSize - ChunkSize * 0.5f);
            field.lod0Distance = 16f;
            field.maxDistance = 60f;
            field.fadeLength = 15f;

            // валуны вне круга, по которому катится камень
            var rocks = new GameObject("Rocks").transform;
            var spots = new[]
            {
                (-9f, 6f, 1.6f), (-7.4f, 7.8f, 0.9f), (8.5f, -4f, 1.3f), (10.2f, -2.4f, 0.7f),
                (-1.6f, -1.2f, 0.8f), (3f, 9.5f, 1.1f), (-8.5f, -9f, 1.4f), (9f, 8.5f, 0.6f)
            };
            var obstacles = new Vector4[spots.Length];
            for (var i = 0; i < spots.Length; i++)
            {
                var (x, z, scale) = spots[i];
                obstacles[i] = new Vector4(x, z, scale * 0.9f, 0f);
                var go = new GameObject("Boulder_" + i) { isStatic = true };
                go.transform.SetParent(rocks, false);
                go.transform.SetPositionAndRotation(new Vector3(x, scale * 0.15f, z), Quaternion.Euler(0f, i * 67f, 0f));
                go.transform.localScale = Vector3.one * scale;
                go.AddComponent<MeshFilter>().sharedMesh = boulders[i % boulders.Length];
                go.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial;
            }
            field.obstacles = obstacles;

            const float stoneRadius = 0.45f;
            var stone = new GameObject("Rolling Stone (Grass Interactor)");
            stone.transform.position = new Vector3(RockPathRadius, stoneRadius * 0.95f, 0f);
            stone.transform.localScale = Vector3.one * stoneRadius;
            stone.AddComponent<MeshFilter>().sharedMesh = rollingStone;
            stone.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial;
            var interactor = stone.AddComponent<GrassInteractor>();
            interactor.radius = 0.85f;
            interactor.orbitRadius = RockPathRadius;
            interactor.orbitSpeed = 0.22f;
            interactor.rollRadius = stoneRadius;

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(32f, -140f, 0f);
            light.color = new Color(1f, 0.93f, 0.78f);
            light.intensity = 2.4f;
            light.shadows = LightShadows.Soft;
            // трава светлая и сочная только при сильной заливке окружением
            ProjectStyle.ApplyEnvironment(new Color(0.6f, 0.63f, 0.66f));

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/SampleSceneProfile.asset");

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.farClipPlane = 150f;
            camera.fieldOfView = 55f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var orbit = camera.gameObject.AddComponent<OrbitCamera>();
            orbit.distance = 12f;
            orbit.pitch = 16f;
            orbit.autoRotateSpeed = 4f;

            var hud = new GameObject("Demo HUD").AddComponent<GrassHud>();
            hud.field = field;

            // камера сверху для вида оптимизации: выключена, включается по F, отсекает траву по-прежнему основная камера
            var observer = new GameObject("Observer Camera (F)").AddComponent<Camera>();
            var observerPosition = new Vector3(0f, 26f, -22f);
            observer.transform.SetPositionAndRotation(observerPosition, Quaternion.LookRotation(new Vector3(0f, 0f, 3f) - observerPosition));
            observer.fieldOfView = 50f;
            observer.farClipPlane = 150f;
            observer.depth = camera.depth + 1f;
            observer.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            observer.enabled = false;
            var lines = MaterialAsset(Root + "/Materials/M_Grass_DebugLines.mat", Shader.Find("Universal Render Pipeline/Unlit"));
            lines.SetColor("_BaseColor", ProjectStyle.Accent);
            hud.observer = observer;
            hud.frustumMaterial = lines;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
        }

        // Стилизованный валун: сфера с мягкими буграми и несколькими плоскими срезами
        static void Boulder(Mesh mesh, int seed, float roughness, float flatten, float cut)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            WinterMeshGenerator.Icosphere(vertices, indices, 3);

            var random = new System.Random(seed);
            float Next() => (float)random.NextDouble();
            var offset = new Vector2(Next() * 100f, Next() * 100f);
            var planes = new Vector3[4];
            for (var i = 0; i < planes.Length; i++)
                planes[i] = new Vector3(Next() * 2f - 1f, Next() * 1.4f - 0.4f, Next() * 2f - 1f).normalized;

            for (var i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                var n = Mathf.PerlinNoise(v.x * 1.6f + offset.x, v.z * 1.6f + v.y * 0.9f + offset.y);
                var p = v * (1f + (n - 0.5f) * roughness * 2f);
                foreach (var plane in planes)
                {
                    var d = Vector3.Dot(p, plane);
                    if (d > cut)
                        p -= plane * (d - cut);
                }
                p.y *= flatten;
                vertices[i] = p;
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static Texture2D CreateNoise(string path)
        {
            const int size = 128;
            const float frequency = 6f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float Sample(float du, float dv) => Fbm((u + du) * frequency, (v + dv) * frequency);
                var value = Mathf.Lerp(
                    Mathf.Lerp(Sample(0, 0), Sample(-1, 0), u),
                    Mathf.Lerp(Sample(0, -1), Sample(-1, -1), u), v);
                value = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, value));
                texture.SetPixel(x, y, new Color(value, value, value, 1f));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = false; // читается только в вершинном шейдере
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Fbm(float x, float y)
        {
            var sum = 0f;
            var amplitude = 0.5f;
            for (var i = 0; i < 3; i++)
            {
                sum += Mathf.PerlinNoise(x + 17.3f, y + 9.1f) * amplitude;
                x *= 2f;
                y *= 2f;
                amplitude *= 0.5f;
            }
            return sum / 0.875f;
        }

        static Mesh MeshAsset(string path)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
                return mesh;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            mesh = new Mesh { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material MaterialAsset(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
