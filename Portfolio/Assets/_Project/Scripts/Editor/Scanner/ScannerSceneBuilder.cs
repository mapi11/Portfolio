using System.IO;
using System.Linq;
using Portfolio.Editor.Showcase;
using Portfolio.Grass;
using Portfolio.Scanner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portfolio.Editor.Scanner
{
    public static class ScannerSceneBuilder
    {
        const string Root = "Assets/_Project/Art/Scanner";
        const string ScenePath = "Assets/_Project/Scenes/Scanner.unity";
        static readonly string[] Renderers = { "Assets/_Project/Settings/PC_Renderer.asset", "Assets/_Project/Settings/Mobile_Renderer.asset" };

        // укрытия: позиция и размер; манекены стоят за ними, чтобы камера их не видела напрямую
        static readonly (Vector3 position, Vector3 size)[] Cover =
        {
            (new Vector3(-4f, 0f, 2f), new Vector3(4f, 2.6f, 0.3f)),
            (new Vector3(3.5f, 0f, 5f), new Vector3(5f, 2.6f, 0.3f)),
            (new Vector3(-7f, 0f, 7.5f), new Vector3(0.3f, 2.6f, 4f)),
            (new Vector3(-1f, 0f, 9f), new Vector3(3f, 2.6f, 0.3f)),
            (new Vector3(7.5f, 0f, 10f), new Vector3(0.3f, 2.6f, 3f)),
            (new Vector3(5.6f, 0f, 1f), new Vector3(1f, 1f, 1f)),
            (new Vector3(6.4f, 0f, 1.8f), new Vector3(1f, 1f, 1f)),
            (new Vector3(-8.5f, 0f, 0.5f), new Vector3(1f, 1.4f, 1f))
        };

        static readonly (Vector3 position, float yaw)[] Mannequins =
        {
            (new Vector3(-4.2f, 0f, 3.3f), 170f),
            (new Vector3(3.2f, 0f, 6.4f), 200f),
            (new Vector3(-1.3f, 0f, 10.3f), 185f),
            (new Vector3(-8.3f, 0f, 7.2f), 120f),
            (new Vector3(8.6f, 0f, 10.4f), 230f),
            (new Vector3(1.8f, 0f, 1.2f), 160f)
        };

        [MenuItem("Tools/Portfolio/Build Scanner")]
        public static void Build()
        {
            var pulse = MaterialAsset(Root + "/M_ScanPulse.mat", Shader.Find("Hidden/Portfolio/Scan Pulse"));
            pulse.SetColor("_EdgeColor", ProjectStyle.Accent * 2f);
            pulse.SetColor("_ShadeColor", ProjectStyle.Background);
            foreach (var path in Renderers)
                InstallFeature(path, pulse);

            var target = MaterialAsset(Root + "/M_ScanTarget.mat", Shader.Find("Portfolio/Scan Target"));
            target.SetColor("_BaseColor", ProjectStyle.Line);
            target.SetColor("_XRayColor", ProjectStyle.Accent * 1.5f);

            var glow = MaterialAsset(Root + "/M_ScanBeacon.mat", Shader.Find("Universal Render Pipeline/Unlit"));
            glow.SetColor("_BaseColor", ProjectStyle.Accent * 2f);

            var mannequin = MeshAsset("SM_Scan_Mannequin");
            Mannequin(mannequin);
            AssetDatabase.SaveAssets();

            BuildScene(target, glow, mannequin);
            Debug.Log("Scanner scene built: " + ScenePath);
        }

        static void BuildScene(Material targetMaterial, Material glowMaterial, Mesh mannequin)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ProjectStyle.CreateFloor(new Vector2(24f, 22f), 2.5f).transform.position = new Vector3(0f, 0f, 4f);

            var cover = new GameObject("Cover").transform;
            for (var i = 0; i < Cover.Length; i++)
            {
                var (position, size) = Cover[i];
                var box = ProjectStyle.CreateGridBox("Cover " + i, size);
                box.transform.SetParent(cover, false);
                box.transform.position = position;
            }

            var targets = new Renderer[Mannequins.Length];
            for (var i = 0; i < Mannequins.Length; i++)
            {
                var (position, yaw) = Mannequins[i];
                var go = new GameObject("Mannequin " + i) { isStatic = true };
                go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                go.AddComponent<MeshFilter>().sharedMesh = mannequin;
                targets[i] = go.AddComponent<MeshRenderer>();
                targets[i].sharedMaterial = targetMaterial;
            }

            // маяк: отсюда волна идёт по пробелу и в авторежиме
            var beacon = ProjectStyle.CreateGridBox("Beacon", new Vector3(0.5f, 1.1f, 0.5f));
            beacon.transform.position = new Vector3(0f, 0f, -4.5f);
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Beacon Core";
            Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(beacon.transform, false);
            core.transform.localPosition = new Vector3(0f, 1.3f, 0f);
            core.transform.localScale = Vector3.one * 0.3f;
            core.GetComponent<MeshRenderer>().sharedMaterial = glowMaterial;
            core.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var scanner = new GameObject("Scanner").AddComponent<ScanPulse>();
            scanner.beacon = core.transform;
            scanner.targets = targets;

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            light.color = Color.white;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            ProjectStyle.ApplyEnvironment(ProjectStyle.Line);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Art/Lightning/LightningVolumeProfile.asset");

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.fieldOfView = 50f;
            camera.farClipPlane = 120f;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var orbit = camera.gameObject.AddComponent<OrbitCamera>();
            orbit.target = new Vector3(0f, 1f, 3f);
            orbit.distance = 15f;
            orbit.pitch = 22f;
            orbit.yaw = 0f;
            orbit.autoRotateSpeed = 0f;
            orbit.distanceLimits = new Vector2(6f, 26f);
            orbit.pitchLimits = new Vector2(5f, 70f);
            orbit.dragWithRightButton = true;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
        }

        // Фича добавляется в рендерер один раз; в сценах без сканера она ничего не рисует
        static void InstallFeature(string path, Material material)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
                return;

            var feature = data.rendererFeatures.OfType<ScanRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScanRendererFeature>();
                feature.name = "Scan Pulse";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var serialized = new SerializedObject(data);
                var features = serialized.FindProperty("m_RendererFeatures");
                var map = serialized.FindProperty("m_RendererFeatureMap");
                features.InsertArrayElementAtIndex(features.arraySize);
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.InsertArrayElementAtIndex(map.arraySize);
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedProperties();
            }

            feature.material = material;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            data.SetDirty();
            EditorUtility.SetDirty(data);
        }

        // Тренировочный манекен ростом 1.8 м, пивот под ногами
        static void Mannequin(Mesh mesh)
        {
            var b = new PropMeshBuilder(1);

            // торс из трёх частей: широкая грудь, узкая талия, таз — уже не «столб»
            var flat = new Vector3(1f, 1f, 0.62f);
            b.Cylinder(0, Matrix4x4.TRS(new Vector3(0f, 1.36f, 0f), Quaternion.identity, flat), 0.18f, 0.3f, 12);
            b.Cylinder(0, Matrix4x4.TRS(new Vector3(0f, 1.13f, 0f), Quaternion.identity, flat), 0.14f, 0.2f, 12);
            b.Cylinder(0, Matrix4x4.TRS(new Vector3(0f, 0.96f, 0f), Quaternion.identity, new Vector3(1f, 1f, 0.7f)), 0.16f, 0.16f, 12);
            b.Sphere(0, new Vector3(0f, 1.5f, 0f), 0.11f, 10, 6);
            b.Cylinder(0, Matrix4x4.Translate(new Vector3(0f, 1.57f, 0f)), 0.045f, 0.1f, 8);
            b.Sphere(0, new Vector3(0f, 1.71f, 0.01f), 0.11f, 14, 10);

            for (var side = -1; side <= 1; side += 2)
            {
                var shoulder = new Vector3(side * 0.22f, 1.46f, 0f);
                var elbow = new Vector3(side * 0.27f, 1.18f, -0.02f);
                var wrist = new Vector3(side * 0.28f, 0.92f, 0.04f);
                Joint(b, shoulder, 0.065f);
                Limb(b, shoulder, elbow, 0.05f);
                Joint(b, elbow, 0.046f);
                Limb(b, elbow, wrist, 0.042f);
                Joint(b, wrist + Vector3.down * 0.05f, 0.05f);

                var hip = new Vector3(side * 0.1f, 0.9f, 0f);
                var knee = new Vector3(side * 0.11f, 0.48f, 0.02f);
                var ankle = new Vector3(side * 0.11f, 0.08f, 0f);
                Joint(b, hip, 0.08f);
                Limb(b, hip, knee, 0.072f);
                Joint(b, knee, 0.062f);
                Limb(b, knee, ankle, 0.056f);
                b.Box(0, new Vector3(side * 0.11f, 0.035f, 0.04f), new Vector3(0.09f, 0.07f, 0.22f));
            }
            b.Apply(mesh);
        }

        static void Limb(PropMeshBuilder builder, Vector3 from, Vector3 to, float radius)
        {
            var direction = to - from;
            var transform = Matrix4x4.TRS((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, direction), Vector3.one);
            builder.Cylinder(0, transform, radius, direction.magnitude, 10, caps: false);
        }

        static void Joint(PropMeshBuilder builder, Vector3 center, float radius) => builder.Sphere(0, center, radius, 10, 6);

        static Mesh MeshAsset(string name)
        {
            var path = $"{Root}/{name}.asset";
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
