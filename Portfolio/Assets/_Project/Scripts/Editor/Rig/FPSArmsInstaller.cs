using System.Linq;
using Portfolio.Weapon;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Portfolio.Editor.Rig
{
    public static class FPSArmsInstaller
    {
        const string Folder = "Assets/_Project/Art/Characters/FPSArms";
        const string FbxPath = Folder + "/SK_FPSArms_AK47.fbx";
        const string ControllerPath = Folder + "/AC_FPSArms.controller";
        const string SkinPath = Folder + "/M_FPSArms_Skin.mat";
        const string ScenePath = "Assets/_Project/Scenes/WeaponRange.unity";

        [MenuItem("Tools/Portfolio/Install FPS Arms In Weapon Range")]
        public static void Install()
        {
            ConfigureImporter();
            var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            var controller = CreateController(clips["Hold"], clips["Reload"]);
            InstallInScene(controller, clips["Hold"], clips["Reload"]);
        }

        static void ConfigureImporter()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            // иначе Blender-экспорт вешает на каждый меш все 52 кости, хотя рукам нужно 38, автомату 4
            importer.optimizeBones = true;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = clip.takeName.Contains("Reload") ? "Reload" : "Hold";
                clip.loopTime = clip.name == "Hold";
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static AnimatorController CreateController(AnimationClip hold, AnimationClip reload)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);

            var machine = controller.layers[0].stateMachine;
            var holdState = machine.AddState("Hold");
            holdState.motion = hold;
            var reloadState = machine.AddState("Reload");
            reloadState.motion = reload;
            reloadState.tag = "Reload";
            machine.defaultState = holdState;

            var toReload = holdState.AddTransition(reloadState);
            toReload.AddCondition(AnimatorConditionMode.If, 0, "Reload");
            toReload.hasExitTime = false;
            toReload.duration = 0.08f;

            var toHold = reloadState.AddTransition(holdState);
            toHold.hasExitTime = true;
            toHold.exitTime = 1f;
            toHold.duration = 0.1f;
            return controller;
        }

        static void InstallInScene(AnimatorController controller, AnimationClip hold, AnimationClip reload)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var weapon = Object.FindFirstObjectByType<WeaponController>();
            var kickRoot = weapon.kickRoot;

            var old = kickRoot.Find("SK_FPSArms_AK47");
            if (old != null)
            {
                weapon.muzzleFlash.transform.SetParent(kickRoot, false);
                Object.DestroyImmediate(old.gameObject);
            }
            var placeholder = kickRoot.Find("SM_Placeholder_AR");
            if (placeholder != null)
                placeholder.gameObject.SetActive(false);

            // кость b_camera в FBX стоит в начале координат — руки ставим прямо под камеру
            weapon.transform.localPosition = Vector3.zero;
            weapon.transform.localRotation = Quaternion.identity;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var arms = (GameObject)PrefabUtility.InstantiatePrefab(model, kickRoot);
            arms.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            arms.transform.localScale = Vector3.one;

            if (!arms.TryGetComponent<Animator>(out var animator))
                animator = arms.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var skin = AssetDatabase.LoadAssetAtPath<Material>(SkinPath);
            if (skin == null)
            {
                skin = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                skin.SetColor("_BaseColor", new Color(0.86f, 0.68f, 0.56f));
                skin.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            foreach (var renderer in arms.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.updateWhenOffscreen = true;
                if (renderer.name == "SK_FPSArms")
                    renderer.sharedMaterial = skin;
            }

            // поза удержания нужна, чтобы снять с неё цель и подсказку IK
            hold.SampleAnimation(arms, 0f);
            Transform Bone(string name) => arms.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

            var target = new GameObject("LeftHandIK_Target").transform;
            target.SetParent(Bone("socket_lhand_ik"), false);
            target.SetPositionAndRotation(Bone("b_hand_l").position, Bone("b_hand_l").rotation);

            var hint = new GameObject("LeftHandIK_Hint").transform;
            hint.SetParent(Bone("b_spine_fp"), false);
            hint.position = Bone("b_forearm_l").position + arms.transform.TransformDirection(new Vector3(0.1f, -0.25f, 0f));

            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(arms.transform, false);
            var rig = rigGo.AddComponent<UnityEngine.Animations.Rigging.Rig>();
            var builder = arms.AddComponent<RigBuilder>();
            builder.layers.Add(new RigLayer(rig));

            var ikGo = new GameObject("LeftHandIK");
            ikGo.transform.SetParent(rigGo.transform, false);
            var ik = ikGo.AddComponent<TwoBoneIKConstraint>();
            ik.data.root = Bone("b_upperarm_l");
            ik.data.mid = Bone("b_forearm_l");
            ik.data.tip = Bone("b_hand_l");
            ik.data.target = target;
            ik.data.hint = hint;
            ik.data.targetPositionWeight = 1f;
            ik.data.targetRotationWeight = 1f;
            ik.data.hintWeight = 1f;

            // кости из Blender смотрят осью Y вдоль кости — для VFX нужен +Z вдоль ствола
            Transform Socket(string bone, string name)
            {
                var socket = new GameObject(name).transform;
                socket.SetParent(Bone(bone), false);
                socket.SetPositionAndRotation(Bone(bone).position, kickRoot.rotation);
                return socket;
            }
            weapon.muzzleSocket = Socket("socket_muzzle", "MuzzleFX");
            weapon.shellSocket = Socket("socket_shell_eject", "ShellFX");
            weapon.muzzleFlash.transform.SetParent(weapon.muzzleSocket, false);
            weapon.muzzleFlash.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var animatorDriver = arms.AddComponent<WeaponAnimator>();
            animatorDriver.animator = animator;
            animatorDriver.weapon = weapon;
            animatorDriver.leftHandIK = ik;
            animatorDriver.reloadClip = reload;

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("FPS arms installed in " + ScenePath);
        }
    }
}
