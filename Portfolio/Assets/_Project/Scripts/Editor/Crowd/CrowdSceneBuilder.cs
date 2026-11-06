using System.Collections.Generic;
using System.IO;
using Portfolio.Crowd;
using Portfolio.Editor.Showcase;
using Portfolio.Grass;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Portfolio.Editor.Crowd
{
    // Сцена толпы: шаговый цикл манекена, префаб со SkinnedMeshRenderer для сравнения
    // и запекание того же цикла в текстуры позиций и нормалей для VAT
    public static class CrowdSceneBuilder
    {
        const string Root = "Assets/_Project/Art/Crowd";
        const string ScenePath = "Assets/_Project/Scenes/Crowd.unity";
        const int Frames = 30;
        const float ClipLength = 1.4f;

        [MenuItem("Tools/Portfolio/Build Crowd")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            var skinnedMesh = MeshAsset("SM_Crowd_Mannequin_Skinned");
            MannequinMesh.Build(skinnedMesh, skinned: true);

            var clip = WalkClip();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/AC_Crowd_Walk.controller")
                             ?? AnimatorController.CreateAnimatorControllerAtPathWithClip(Root + "/AC_Crowd_Walk.controller", clip);

            var lit = MaterialAsset(Root + "/M_Crowd_Skinned.mat", Shader.Find("Universal Render Pipeline/Lit"));
            lit.SetColor("_BaseColor", ProjectStyle.Line);
            lit.SetFloat("_Smoothness", 0.25f);
            lit.enableInstancing = true;

            var prefab = SkinnedPrefab(skinnedMesh, lit, controller);
            var (positions, normals) = Bake(prefab, clip);

            var vatMesh = MeshAsset("SM_Crowd_Mannequin_VAT");
            VatMesh(vatMesh);
            var vat = MaterialAsset(Root + "/M_Crowd_VAT.mat", Shader.Find("Portfolio/Crowd VAT"));
            vat.SetTexture("_PositionMap", positions);
            vat.SetTexture("_NormalMap", normals);
            vat.SetFloat("_Frames", Frames);
            vat.SetFloat("_FrameRate", Frames / ClipLength);
            vat.SetColor("_BaseColor", ProjectStyle.Line);
            vat.SetColor("_RimColor", ProjectStyle.Accent);
            vat.enableInstancing = true;
            AssetDatabase.SaveAssets();

            BuildScene(vatMesh, vat, prefab);
            Debug.Log("Crowd scene built: " + ScenePath);
        }

        // Шаговый цикл на 1 с: бёдра и руки в противофазе, колено сгибается на выносе ноги, корпус чуть покачивается
        static AnimationClip WalkClip()
        {
            var path = Root + "/A_Crowd_Walk.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = "A_Crowd_Walk" };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.frameRate = Frames;

            void Rotate(int bone, System.Func<float, Vector3> euler)
            {
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                for (var f = 0; f <= Frames; f++)
                {
                    var t = f / (float)Frames * ClipLength;
                    var q = Quaternion.Euler(euler(t / ClipLength * Mathf.PI * 2f));
                    curves[0].AddKey(t, q.x);
                    curves[1].AddKey(t, q.y);
                    curves[2].AddKey(t, q.z);
                    curves[3].AddKey(t, q.w);
                }
                var bonePath = BonePath(bone);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.x", curves[0]);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.y", curves[1]);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.z", curves[2]);
                clip.SetCurve(bonePath, typeof(Transform), "localRotation.w", curves[3]);
            }

            var bob = new AnimationCurve();
            for (var f = 0; f <= Frames; f++)
            {
                var t = f / (float)Frames * ClipLength;
                bob.AddKey(t, MannequinMesh.BonePositions[MannequinMesh.Hips].y + 0.03f * Mathf.Cos(t / ClipLength * Mathf.PI * 4f));
            }
            var hips = BonePath(MannequinMesh.Hips);
            clip.SetCurve(hips, typeof(Transform), "localPosition.x", AnimationCurve.Constant(0f, ClipLength, 0f));
            clip.SetCurve(hips, typeof(Transform), "localPosition.y", bob);
            clip.SetCurve(hips, typeof(Transform), "localPosition.z", AnimationCurve.Constant(0f, ClipLength, 0f));

            Rotate(MannequinMesh.Hips, p => new Vector3(0f, 5f * Mathf.Sin(p), 0f));
            Rotate(MannequinMesh.Spine, p => new Vector3(4f, -7f * Mathf.Sin(p), 0f));
            Rotate(MannequinMesh.Head, p => new Vector3(-3f, 2f * Mathf.Sin(p), 0f));
            // нога вперёд — отрицательный поворот по X, сгиб колена и локтя — в свою сторону
            Rotate(MannequinMesh.ThighL, p => new Vector3(-32f * Mathf.Sin(p), 0f, 0f));
            Rotate(MannequinMesh.ThighR, p => new Vector3(32f * Mathf.Sin(p), 0f, 0f));
            Rotate(MannequinMesh.ShinL, p => new Vector3(6f + 42f * Mathf.Max(0f, Mathf.Cos(p)), 0f, 0f));
            Rotate(MannequinMesh.ShinR, p => new Vector3(6f + 42f * Mathf.Max(0f, -Mathf.Cos(p)), 0f, 0f));
            Rotate(MannequinMesh.UpperArmL, p => new Vector3(28f * Mathf.Sin(p), 0f, -4f));
            Rotate(MannequinMesh.UpperArmR, p => new Vector3(-28f * Mathf.Sin(p), 0f, 4f));
            Rotate(MannequinMesh.ForeArmL, p => new Vector3(-18f - 8f * Mathf.Max(0f, -Mathf.Sin(p)), 0f, 0f));
            Rotate(MannequinMesh.ForeArmR, p => new Vector3(-18f - 8f * Mathf.Max(0f, Mathf.Sin(p)), 0f, 0f));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static string BonePath(int bone)
        {
            var parts = new List<string>();
            for (var b = bone; b >= 0; b = MannequinMesh.BoneParents[b])
                parts.Insert(0, MannequinMesh.BoneNames[b]);
            return string.Join("/", parts);
        }

        static GameObject SkinnedPrefab(Mesh mesh, Material material, RuntimeAnimatorController controller)
        {
            var root = new GameObject("SK_Crowd_Mannequin");
            var bones = new Transform[MannequinMesh.BoneNames.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                bones[i] = new GameObject(MannequinMesh.BoneNames[i]).transform;
                var parent = MannequinMesh.BoneParents[i];
                bones[i].SetParent(parent < 0 ? root.transform : bones[parent], false);
                bones[i].localPosition = MannequinMesh.BonePositions[i] - (parent < 0 ? Vector3.zero : MannequinMesh.BonePositions[parent]);
            }

            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.bones = bones;
            renderer.rootBone = bones[MannequinMesh.Hips];
            renderer.localBounds = new Bounds(Vector3.zero, new Vector3(1.4f, 2.2f, 1.4f));
            root.AddComponent<Animator>().runtimeAnimatorController = controller;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/SK_Crowd_Mannequin.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // Запекание: каждый кадр клипа проигрывается на копии префаба, BakeMesh снимает позы вершин.
        // Строка текстуры — кадр, столбец — вершина
        static (Texture2D positions, Texture2D normals) Bake(GameObject prefab, AnimationClip clip)
        {
            var instance = Object.Instantiate(prefab);
            var renderer = instance.GetComponent<SkinnedMeshRenderer>();
            var vertexCount = renderer.sharedMesh.vertexCount;
            var positions = new Color[vertexCount * Frames];
            var normals = new Color[vertexCount * Frames];
            var baked = new Mesh();
            var vertices = new List<Vector3>();
            var bakedNormals = new List<Vector3>();
            for (var f = 0; f < Frames; f++)
            {
                clip.SampleAnimation(instance, f / (float)Frames * ClipLength);
                renderer.BakeMesh(baked, true);
                baked.GetVertices(vertices);
                baked.GetNormals(bakedNormals);
                for (var v = 0; v < vertexCount; v++)
                {
                    positions[f * vertexCount + v] = new Color(vertices[v].x, vertices[v].y, vertices[v].z, 1f);
                    normals[f * vertexCount + v] = new Color(bakedNormals[v].x, bakedNormals[v].y, bakedNormals[v].z, 0f);
                }
            }
            Object.DestroyImmediate(baked);
            Object.DestroyImmediate(instance);
            return (VatTexture("T_Crowd_Walk_Positions", vertexCount, positions), VatTexture("T_Crowd_Walk_Normals", vertexCount, normals));
        }

        // half-формат: позы в метрах в пределах пары метров, точности хватает; фильтрация точечная, между кадрами смешивает шейдер
        static Texture2D VatTexture(string name, int width, Color[] pixels)
        {
            var path = $"{Root}/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var texture = new Texture2D(width, Frames, TextureFormat.RGBAHalf, false, true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        // Тот же манекен без костей, во втором UV — столбец вершины в текстурах анимации
        static void VatMesh(Mesh mesh)
        {
            MannequinMesh.Build(mesh, skinned: false);
            var columns = new List<Vector2>(mesh.vertexCount);
            for (var i = 0; i < mesh.vertexCount; i++)
                columns.Add(new Vector2((i + 0.5f) / mesh.vertexCount, 0f));
            mesh.SetUVs(1, columns);
            // вершины двигает шейдер, поэтому границы берём с запасом на размах рук и ног
            mesh.bounds = new Bounds(new Vector3(0f, 0.95f, 0f), new Vector3(1.4f, 2.1f, 1.4f));
        }

        static void BuildScene(Mesh vatMesh, Material vatMaterial, GameObject skinnedPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ProjectStyle.CreateFloor(new Vector2(34f, 34f), 3f);

            var crowd = new GameObject("Crowd").AddComponent<CrowdDemo>();
            crowd.vatMesh = vatMesh;
            crowd.vatMaterial = vatMaterial;
            crowd.skinnedPrefab = skinnedPrefab;

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            light.color = Color.white;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            ProjectStyle.ApplyEnvironment(ProjectStyle.Line);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Art/Lightning/LightningVolumeProfile.asset");

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.fieldOfView = 50f;
            camera.farClipPlane = 150f;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var orbit = camera.gameObject.AddComponent<OrbitCamera>();
            orbit.target = new Vector3(0f, 0.8f, 0f);
            orbit.distance = 26f;
            orbit.pitch = 30f;
            orbit.yaw = 20f;
            orbit.autoRotateSpeed = 3f;
            orbit.distanceLimits = new Vector2(6f, 50f);
            orbit.pitchLimits = new Vector2(5f, 80f);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
        }

        static Mesh MeshAsset(string name)
        {
            var path = $"{Root}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
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
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
