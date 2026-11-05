using System.IO;
using Portfolio.Digitize;
using Portfolio.Editor.Showcase;
using Portfolio.Grass;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portfolio.Editor.Digitize
{
    public static class DigitizeSceneBuilder
    {
        const string Root = "Assets/_Project/Art/Digitize";
        const string ScenePath = "Assets/_Project/Scenes/Digitize.unity";
        const float PedestalHeight = 0.5f;

        // модели Poly Haven (CC0, см. License.txt): имя, высота фигуры в метрах, место на площадке
        static readonly (string id, float height, Vector3 position)[] Statues =
        {
            ("MarbleBust", 0.95f, new Vector3(-3.6f, 0f, 0.8f)),
            ("HorseStatue", 1.35f, new Vector3(0f, 0f, 2f)),
            ("GothicStatue", 1.7f, new Vector3(3.6f, 0f, 0.8f))
        };

        [MenuItem("Tools/Portfolio/Build Digitize")]
        public static void Build()
        {
            var shader = Shader.Find("Portfolio/Digitize");
            var materials = new Material[Statues.Length];
            for (var i = 0; i < Statues.Length; i++)
                materials[i] = StatueMaterial(Statues[i].id, shader);

            var ball = MaterialAsset(Root + "/M_Digitize_Ball.mat", shader);
            ball.SetFloat("_ForceDigital", 1f);
            ball.SetFloat("_GlitchStrength", 0.14f);
            ball.SetFloat("_GridScale", 18f);
            ball.SetFloat("_Pixelate", 0.15f);
            SetPalette(ball);
            AssetDatabase.SaveAssets();

            BuildScene(materials, ball);
            Debug.Log("Digitize scene built: " + ScenePath);
        }

        static void BuildScene(Material[] statueMaterials, Material ballMaterial)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ProjectStyle.CreateFloor(new Vector2(14f, 10f), 2.5f).transform.position = new Vector3(0f, 0f, 1f);

            for (var i = 0; i < Statues.Length; i++)
            {
                var (id, height, position) = Statues[i];
                var pedestal = ProjectStyle.CreateGridBox("Pedestal " + id, new Vector3(1.3f, PedestalHeight, 1.3f));
                pedestal.transform.position = position;
                PlaceStatue(id, height, position + Vector3.up * PedestalHeight, statueMaterials[i]);
            }

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Digitize Ball";
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.position = new Vector3(-5f, 1.1f, -1.5f);
            ball.transform.localScale = Vector3.one * 0.45f;
            var ballRenderer = ball.GetComponent<MeshRenderer>();
            ballRenderer.sharedMaterial = ballMaterial;
            ballRenderer.shadowCastingMode = ShadowCastingMode.Off;
            var controller = ball.AddComponent<DigitizeBall>();
            controller.capturePath = new[]
            {
                new Vector3(-3.6f, 1.1f, 0.3f), new Vector3(-1.9f, 1.1f, 1.2f), new Vector3(-0.35f, 1.15f, 1.75f)
            };

            // синий отсвет шара на фигурах
            var glow = new GameObject("Ball Light").AddComponent<Light>();
            glow.transform.SetParent(ball.transform, false);
            glow.type = LightType.Point;
            glow.color = ProjectStyle.Accent;
            glow.intensity = 2.5f;
            glow.range = 3.5f;
            glow.shadows = LightShadows.None;

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            light.color = Color.white;
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            ProjectStyle.ApplyEnvironment(ProjectStyle.Line);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Art/Lightning/LightningVolumeProfile.asset");

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.fieldOfView = 50f;
            camera.farClipPlane = 100f;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var orbit = camera.gameObject.AddComponent<OrbitCamera>();
            orbit.target = new Vector3(0f, 1.1f, 1.2f);
            orbit.distance = 8.5f;
            orbit.pitch = 14f;
            orbit.yaw = 0f;
            orbit.autoRotateSpeed = 0f;
            orbit.distanceLimits = new Vector2(3f, 14f);
            orbit.pitchLimits = new Vector2(2f, 50f);
            orbit.dragWithRightButton = true;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
        }

        // Фигура подгоняется под нужную высоту и ставится низом на постамент: размеры исходников разные
        static void PlaceStatue(string id, float height, Vector3 basePosition, Material material)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{id}/SM_{id}.fbx");
            var statue = (GameObject)PrefabUtility.InstantiatePrefab(model);
            statue.name = id;
            // поворот корня FBX сохраняем: в нём перевод из Z-up исходника
            statue.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * statue.transform.rotation;

            var renderers = statue.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                var slots = new Material[renderer.sharedMaterials.Length];
                for (var s = 0; s < slots.Length; s++)
                    slots[s] = material;
                renderer.sharedMaterials = slots;
            }

            var bounds = Bounds(renderers);
            statue.transform.localScale *= height / bounds.size.y;
            bounds = Bounds(renderers);
            statue.transform.position += basePosition - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }

        static Bounds Bounds(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static Material StatueMaterial(string id, Shader shader)
        {
            var modelPath = $"{Root}/{id}/SM_{id}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();

            var normalPath = $"{Root}/{id}/T_{id}_N.jpg";
            var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();

            var material = MaterialAsset($"{Root}/{id}/M_Digitize_{id}.mat", shader);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{id}/T_{id}_D.jpg"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            material.SetFloat("_ForceDigital", 0f);
            material.SetFloat("_GridScale", 10f);
            material.SetFloat("_Pixelate", 0.45f);
            SetPalette(material);
            return material;
        }

        // палитра проекта: синий акцент, тёмные 1A1A1A и 222222, серые блоки 626262
        static void SetPalette(Material material)
        {
            material.SetColor("_DigitalColor", ProjectStyle.Accent);
            material.SetColor("_DarkColor", ProjectStyle.Background);
            material.SetColor("_MidColor", ProjectStyle.Surface);
            material.SetColor("_LineColor", ProjectStyle.Line);
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
