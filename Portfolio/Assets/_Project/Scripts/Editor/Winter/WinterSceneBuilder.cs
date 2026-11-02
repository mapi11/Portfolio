using System;
using System.Collections.Generic;
using System.IO;
using Portfolio.Editor.Showcase;
using Portfolio.Grass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Portfolio.Editor.Winter
{
    /// <summary>
    /// Зимняя cartoon-поляна: рельеф со снегом, замёрзшая река, ёлка, камни, трава
    /// из-под снега, снегопад. Batchmode: -executeMethod Portfolio.Editor.Winter.WinterSceneBuilder.Build
    /// </summary>
    public static class WinterSceneBuilder
    {
        const string Root = "Assets/_Project/Art/Winter";
        const string ScenePath = "Assets/_Project/Scenes/WinterGlade.unity";

        [MenuItem("Tools/Portfolio/Build Winter Glade")]
        public static void Build()
        {
            var noise = CreateTexture(Root + "/Textures/T_Winter_Noise_Mask.png", 256, WinterNoise);
            var cracks = CreateTexture(Root + "/Textures/T_Ice_Cracks_Mask.png", 256, IceCracks);

            var terrain = MeshAsset("SM_Winter_Terrain");
            WinterMeshGenerator.Terrain(terrain, 90f, 0.6f);
            var river = MeshAsset("SM_Winter_River");
            WinterMeshGenerator.River(river, 90f, 0.5f);
            var pine = MeshAsset("SM_Winter_Pine");
            WinterMeshGenerator.Pine(pine, 3);
            var rocks = new Mesh[3];
            for (var i = 0; i < rocks.Length; i++)
            {
                rocks[i] = MeshAsset("SM_Winter_Rock_" + i);
                WinterMeshGenerator.Rock(rocks[i], 11 + i * 7);
            }
            var tufts = MeshAsset("SM_Winter_GrassTufts");
            WinterMeshGenerator.GrassTufts(tufts, TuftPositions(), 5);

            var snow = ToonMaterial("M_Toon_Snow", noise);
            snow.SetFloat("_SparkleStrength", 2.5f);
            snow.SetFloat("_RimStrength", 0.15f);
            snow.SetFloat("_SnowVariation", 0.16f);
            var props = ToonMaterial("M_Toon_Props", noise);
            props.SetFloat("_SparkleStrength", 1f);
            // У ярусов ёлки нормали «раздуты» в стороны: снегу нужен порог ниже, чем у камней.
            var pineMaterial = ToonMaterial("M_Toon_Pine", noise);
            pineMaterial.SetFloat("_SnowThreshold", 0.3f);
            pineMaterial.SetFloat("_SnowNoise", 0.55f);
            pineMaterial.SetFloat("_SnowNoiseScale", 1.4f);
            pineMaterial.SetFloat("_SparkleStrength", 1f);

            var ice = MaterialAsset(Root + "/Materials/M_Toon_Ice.mat", Shader.Find("Portfolio/Toon/Ice"));
            ice.SetTexture("_CrackMap", cracks);
            ice.SetTexture("_NoiseMap", noise);
            ice.SetFloat("_CrackScale", 0.16f);
            ice.SetFloat("_SurfaceCrack", 0.18f);
            ice.SetFloat("_BubbleStrength", 0.4f);
            ice.SetFloat("_InnerDepth", 0.45f);

            var snowflake = MaterialAsset("Assets/_Project/VFX/Materials/M_VFX_Snowflake.mat", Shader.Find("Portfolio/VFX/Particle"));
            snowflake.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/VFX/Textures/T_VFX_Atlas.png"));
            VfxTextureGenerator.SetTile(snowflake, VfxTextureGenerator.ParticleSoft);
            snowflake.SetColor("_BaseColor", Color.white);
            snowflake.SetFloat("_Additive", 0f);

            var sky = MaterialAsset(Root + "/Materials/M_Winter_Sky.mat", Shader.Find("Skybox/Procedural"));
            sky.SetColor("_SkyTint", new Color(0.42f, 0.55f, 0.8f));
            sky.SetColor("_GroundColor", new Color(0.86f, 0.9f, 0.96f));
            sky.SetFloat("_AtmosphereThickness", 0.55f);
            sky.SetFloat("_Exposure", 1.2f);
            sky.SetFloat("_SunSize", 0.025f);

            AssetDatabase.SaveAssets();
            BuildScene(terrain, river, pine, rocks, tufts, snow, props, pineMaterial, ice, snowflake, sky);
            Debug.Log("Winter glade built: " + ScenePath);
        }

        // --- Раскладка ---

        static readonly Vector3 TreePosition = new(-3f, 0f, 3.5f);

        static List<Vector3> TuftPositions()
        {
            var random = new System.Random(21);
            float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
            var result = new List<Vector3>();
            // Пучки группами: в мультфильмах трава торчит островками, а не ровным ковром.
            for (var cluster = 0; cluster < 30; cluster++)
            {
                var center = new Vector2(Range(-16f, 16f), Range(-12f, 14f));
                var count = 3 + random.Next(5);
                for (var i = 0; i < count; i++)
                {
                    var p = center + new Vector2(Range(-1.2f, 1.2f), Range(-1.2f, 1.2f));
                    if (!WinterMeshGenerator.IsOnBank(p.x, p.y, 1.3f))
                        continue;
                    if (Vector2.Distance(p, new Vector2(TreePosition.x, TreePosition.z)) < 2f)
                        continue;
                    result.Add(new Vector3(p.x, WinterMeshGenerator.Height(p.x, p.y) - 0.04f, p.y));
                }
            }
            return result;
        }

        static void BuildScene(Mesh terrain, Mesh river, Mesh pine, Mesh[] rocks, Mesh tufts,
            Material snow, Material props, Material pineMaterial, Material ice, Material snowflake, Material sky)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var environment = new GameObject("Environment").transform;
            Static(environment, "Terrain", terrain, snow, Vector3.zero, Quaternion.identity, Vector3.one, castShadows: false);
            Static(environment, "River Ice", river, ice, Vector3.zero, Quaternion.identity, Vector3.one, castShadows: false);
            Static(environment, "Grass Tufts", tufts, props, Vector3.zero, Quaternion.identity, Vector3.one, castShadows: false);

            var treePosition = TreePosition;
            treePosition.y = WinterMeshGenerator.Height(treePosition.x, treePosition.z) - 0.1f;
            Static(environment, "Pine", pine, pineMaterial, treePosition, Quaternion.Euler(0f, 20f, 0f), Vector3.one, castShadows: true);

            // Камни по берегам, часть наполовину во льду.
            var rockSpots = new[] { (-10f, 1f, 1.1f), (-6.5f, -1f, 0.7f), (-1f, 1f, 0.9f), (3.5f, -1f, 1.2f), (8f, 1f, 0.6f), (11f, -1f, 0.85f), (5.5f, 1f, 0.5f) };
            for (var i = 0; i < rockSpots.Length; i++)
            {
                var (x, side, scale) = rockSpots[i];
                var z = WinterMeshGenerator.RiverZ(x) + side * 3f;
                var position = new Vector3(x, Mathf.Max(WinterMeshGenerator.Height(x, z), WinterMeshGenerator.IceLevel) - 0.1f * scale, z);
                Static(environment, "Rock_" + i, rocks[i % rocks.Length], props, position,
                    Quaternion.Euler(0f, i * 73f, 0f), Vector3.one * scale, castShadows: true);
            }

            // Свет: низкое тёплое солнце против холодных теней и неба.
            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(30f, -55f, 0f);
            light.color = new Color(1f, 0.9f, 0.76f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Hard;

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.72f, 0.78f, 0.9f);
            RenderSettings.ambientGroundColor = new Color(0.55f, 0.6f, 0.72f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 75f;
            RenderSettings.fogColor = new Color(0.8f, 0.87f, 0.96f);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.farClipPlane = 120f;
            camera.fieldOfView = 45f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var orbit = camera.gameObject.AddComponent<OrbitCamera>();
            orbit.target = new Vector3(-1.5f, 1.6f, 0f);
            orbit.distance = 17f;
            orbit.pitch = 14f;
            orbit.yaw = 160f;
            orbit.autoRotateSpeed = 4f;
            orbit.distanceLimits = new Vector2(5f, 30f);

            CreateSnowfall(snowflake, orbit.target);
            new GameObject("Demo HUD").AddComponent<GrassHud>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
        }

        static void CreateSnowfall(Material material, Vector3 center)
        {
            var go = new GameObject("Snowfall");
            go.transform.SetPositionAndRotation(center + Vector3.up * 9f, Quaternion.Euler(90f, 0f, 0f));
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.duration = 10f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = 12f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.rateOverTime = 110f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 36f, 0.5f); // повёрнут на 90°: плоскость над поляной

            // Падение со сносом ветром; шум даёт «кружение» снежинок без физики.
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            velocity.y = new ParticleSystem.MinMaxCurve(-1.2f, -0.75f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(1f, 0.9f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.None;
        }

        static void Static(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale, bool castShadows)
        {
            var go = new GameObject(name);
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        // --- Текстуры ---

        /// <summary>R — плавный тайлящийся шум (края снега, иней), G — редкие яркие точки (искры).</summary>
        static Color WinterNoise(int x, int y, int size, System.Random random)
        {
            float u = (float)x / size, v = (float)y / size;
            const float frequency = 8f;
            float Sample(float du, float dv) => WinterMeshGenerator.Fbm((u + du) * frequency + 3f, (v + dv) * frequency + 7f);
            var smooth = Mathf.Lerp(Mathf.Lerp(Sample(0, 0), Sample(-1, 0), u), Mathf.Lerp(Sample(0, -1), Sample(-1, -1), u), v);
            var sparkle = Mathf.Pow((float)random.NextDouble(), 6f);
            return new Color(smooth, sparkle, 0f, 1f);
        }

        /// <summary>R — трещины (рёбра тайлящейся диаграммы Вороного), G — пузырьки.</summary>
        static Color IceCracks(int x, int y, int size, System.Random random)
        {
            float u = (float)x / size, v = (float)y / size;
            var (f1, f2) = Voronoi(u, v, 4, 101);
            var crack = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.003f, 0.018f, f2 - f1));
            var (b1, _) = Voronoi(u, v, 22, 202);
            var bubble = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.004f, 0.012f, b1));
            return new Color(crack, bubble, 0f, 1f);
        }

        static (float f1, float f2) Voronoi(float u, float v, int cells, int seed)
        {
            var cx = Mathf.FloorToInt(u * cells);
            var cy = Mathf.FloorToInt(v * cells);
            float f1 = 10f, f2 = 10f;
            for (var oy = -1; oy <= 1; oy++)
            for (var ox = -1; ox <= 1; ox++)
            {
                var gx = cx + ox;
                var gy = cy + oy;
                // Точка ячейки по модулю сетки: текстура тайлится без швов.
                var hash = Hash(((gx % cells) + cells) % cells, ((gy % cells) + cells) % cells, seed);
                var point = new Vector2((gx + hash.x) / cells, (gy + hash.y) / cells);
                var d = Vector2.Distance(new Vector2(u, v), point);
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }
            return (f1, f2);
        }

        static Vector2 Hash(int x, int y, int seed)
        {
            var random = new System.Random(x * 73856093 ^ y * 19349663 ^ seed);
            return new Vector2(0.15f + 0.7f * (float)random.NextDouble(), 0.15f + 0.7f * (float)random.NextDouble());
        }

        static Texture2D CreateTexture(string path, int size, Func<int, int, int, System.Random, Color> pixel)
        {
            var random = new System.Random(5);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                texture.SetPixel(x, y, pixel(x, y, size, random));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // --- Ассеты ---

        static Material ToonMaterial(string name, Texture2D noise)
        {
            var material = MaterialAsset(Root + "/Materials/" + name + ".mat", Shader.Find("Portfolio/Toon/Lit"));
            material.SetTexture("_NoiseMap", noise);
            return material;
        }

        static Mesh MeshAsset(string name)
        {
            var path = Root + "/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                EditorUtility.SetDirty(mesh);
                return mesh;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh); // геометрия заполняется после создания ассета
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
