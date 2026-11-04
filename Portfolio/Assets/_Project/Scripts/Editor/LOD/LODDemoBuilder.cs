using System.Collections.Generic;
using System.IO;
using Portfolio.Editor.Showcase;
using Portfolio.LodDemo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portfolio.Editor.LodDemo
{
    public static class LODDemoBuilder
    {
        const string Art = "Assets/_Project/Art/LODDemo";
        const string ScenePath = "Assets/_Project/Scenes/LODDemo.unity";

        // доля высоты экрана, ниже которой включается следующий LOD; после последнего — Culled
        static readonly float[] Transitions = { 0.3f, 0.12f, 0.035f };
        // мелким объектам пороги ниже, иначе машина упрощается раньше, чем дом успевает отдалиться
        static readonly float[] SmallTransitions = { 0.12f, 0.05f, 0.012f };

        [MenuItem("Tools/Portfolio/Build LOD Demo")]
        public static void Build()
        {
            RemapMaterials("Car");
            RemapMaterials("House");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // платформа в общем стиле: при отъезде камеры она уменьшается вместе с моделями
            // и тянется к камере, чтобы на ближнем плане пол заполнял низ кадра
            ProjectStyle.CreateFloor(new Vector2(32f, 34f), 2.5f).transform.position = new Vector3(0f, 0f, -7f);

            var groups = new List<LODGroup>();
            foreach (var (name, x, yaw, small) in new[] { ("House", -9f, 200f, false), ("Car", 0f, 145f, true), ("Tree", 8f, 0f, false) })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/SM_{name}.fbx");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = name;
                instance.transform.SetPositionAndRotation(new Vector3(x, 0f, 0f), Quaternion.Euler(0f, yaw, 0f));

                var group = instance.GetComponent<LODGroup>();
                var lods = group.GetLODs();
                var transitions = small ? SmallTransitions : Transitions;
                for (var i = 0; i < lods.Length && i < transitions.Length; i++)
                    lods[i].screenRelativeTransitionHeight = transitions[i];
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.None;
                groups.Add(group);
            }

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            ProjectStyle.ApplyEnvironment(ProjectStyle.Line);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/SampleSceneProfile.asset");

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.fieldOfView = 50f;
            camera.farClipPlane = 800f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var demoCamera = camera.gameObject.AddComponent<LODDemoCamera>();

            var hud = new GameObject("LOD HUD").AddComponent<LODDemoHud>();
            hud.groups = groups.ToArray();
            hud.demoCamera = demoCamera;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
            Debug.Log("LOD demo built: " + ScenePath);
        }

        // Встроенные в FBX текстуры Unity не извлекает сама — подключаем палитру Kenney явно
        static void RemapMaterials(string name)
        {
            var texturePath = $"{Art}/T_{name}_Colormap.png";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.filterMode = FilterMode.Point; // палитра из цветных ячеек, без размытия на стыках
            textureImporter.mipmapEnabled = false;
            textureImporter.SaveAndReimport();

            var materialPath = $"{Art}/M_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(material);

            var modelPath = $"{Art}/SM_{name}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            foreach (var embedded in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (embedded is Material)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            importer.SaveAndReimport();
        }
    }
}
