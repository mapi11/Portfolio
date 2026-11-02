using System.IO;
using Portfolio.Editor.Showcase;
using Portfolio.Grass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portfolio.Editor.Grass
{
    /// <summary>
    /// Собирает сцену с полем травы: меши чанка (LOD0/LOD1), шум ветра, материал, сцену.
    /// Повторный запуск перезаписывает сгенерированное, GUID ассетов сохраняются.
    /// Batchmode: -executeMethod Portfolio.Editor.Grass.GrassSceneBuilder.Build
    /// </summary>
    public static class GrassSceneBuilder
    {
        const string Root = "Assets/_Project/Art/Grass";
        const string ScenePath = "Assets/_Project/Scenes/GrassField.unity";

        const float ChunkSize = 4f;
        const int Lod0Blades = 729; // 27 x 27, сетка заполняется целиком
        const int Lod1Step = 4;

        [MenuItem("Tools/Portfolio/Build Grass Field")]
        public static void Build()
        {
            var lod0 = MeshAsset(Root + "/Meshes/SM_GrassChunk_LOD0.asset");
            var lod1 = MeshAsset(Root + "/Meshes/SM_GrassChunk_LOD1.asset");
            var (lod0Blades, lod1Blades) = GrassMeshGenerator.Build(lod0, lod1, new GrassMeshGenerator.Settings
            {
                chunkSize = ChunkSize,
                blades = Lod0Blades,
                height = new Vector2(0.3f, 0.7f),
                width = new Vector2(0.06f, 0.1f),
                lean = new Vector2(0.15f, 0.5f),
                seed = 7
            }, Lod1Step, 2.2f);
            EditorUtility.SetDirty(lod0);
            EditorUtility.SetDirty(lod1);

            var noise = CreateNoise(Root + "/Textures/T_Grass_Wind_Mask.png");

            var grass = MaterialAsset(Root + "/Materials/M_Grass.mat", Shader.Find("Portfolio/Grass"));
            grass.enableInstancing = true;
            grass.SetColor("_RootColor", new Color(0.2f, 0.32f, 0.08f));
            grass.SetColor("_TipColor", new Color(0.76f, 0.88f, 0.36f));
            grass.SetColor("_DryColor", new Color(0.88f, 0.8f, 0.48f));
            grass.SetColor("_TranslucencyColor", new Color(0.8f, 0.95f, 0.3f));
            grass.SetFloat("_DryAmount", 0.12f);
            grass.SetFloat("_ColorVariation", 0.3f);
            grass.SetFloat("_RootAO", 0.45f);
            grass.SetFloat("_Translucency", 2f);
            grass.SetFloat("_PatchScale", 0.025f);
            grass.SetFloat("_PatchMinHeight", 0.5f);
            // Земля в тон середины травинок: просветы между ними не читаются как дыры.
            var ground = MaterialAsset("Assets/_Project/Art/Environment/Materials/M_Env_Ground.mat", Shader.Find("Universal Render Pipeline/Lit"));
            ground.SetColor("_BaseColor", new Color(0.3f, 0.4f, 0.12f));
            ground.SetFloat("_Smoothness", 0.05f);
            var ball = MaterialAsset("Assets/_Project/Art/Environment/Materials/M_Env_Ball.mat", Shader.Find("Universal Render Pipeline/Lit"));
            ball.SetColor("_BaseColor", new Color(0.85f, 0.3f, 0.12f));
            ball.SetFloat("_Smoothness", 0.45f);
            AssetDatabase.SaveAssets();

            BuildScene(lod0, lod1, lod0Blades, lod1Blades, grass, ground, ball, noise);
            Debug.Log("Grass field built: " + ScenePath);
        }

        static void BuildScene(Mesh lod0, Mesh lod1, int lod0Blades, int lod1Blades,
            Material grass, Material groundMaterial, Material ballMaterial, Texture2D noise)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.isStatic = true;
            ground.transform.localScale = new Vector3(14f, 1f, 14f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

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
            field.size = new Vector2(140f, 140f);
            field.lod0Distance = 16f;
            field.maxDistance = 55f;
            field.fadeLength = 18f;

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball (Grass Interactor)";
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.position = new Vector3(4f, 0.35f, 0f);
            ball.transform.localScale = Vector3.one * 0.7f;
            ball.GetComponent<MeshRenderer>().sharedMaterial = ballMaterial;
            ball.AddComponent<GrassInteractor>().radius = 0.8f;

            // Низкое солнце: на облёте камера то по свету, то против него — виден просвет травинок.
            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(20f, -140f, 0f);
            light.color = new Color(1f, 0.9f, 0.75f);
            light.intensity = 2f;
            light.shadows = LightShadows.Hard;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            // Туман догоняет исчезновение травы: граница дальности растворяется в дымке.
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 95f;
            RenderSettings.fogColor = new Color(0.62f, 0.7f, 0.76f);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.farClipPlane = 150f;
            camera.fieldOfView = 55f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            camera.gameObject.AddComponent<OrbitCamera>();

            var hud = new GameObject("Demo HUD").AddComponent<GrassHud>();
            hud.field = field;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
        }

        static Texture2D CreateNoise(string path)
        {
            // Тайлящийся шум: смешивание четырёх сдвинутых копий Perlin по весам.
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
                // Растягиваем контраст: волны порывов читаются чётче.
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
            importer.mipmapEnabled = false; // выборка только в вершинном шейдере, mip 0
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
