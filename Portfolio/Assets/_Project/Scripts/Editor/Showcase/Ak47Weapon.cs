using Portfolio.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.Editor.Showcase
{
    public static class Ak47Weapon
    {
        const string ModelPath = "Assets/_Project/Art/Weapons/AK47/SM_AK47.fbx";
        const string ScenePath = "Assets/_Project/Scenes/WeaponRange.unity";

        // пивот модели — пистолетная рукоять, поэтому отдача крутит оружие вокруг неё
        public static readonly Vector3 HoldPosition = new(0.13f, -0.21f, 0.34f);

        public static (Transform muzzle, Transform shell) Attach(Transform kickRoot)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var ak = (GameObject)PrefabUtility.InstantiatePrefab(model, kickRoot);
            ak.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var renderer in ak.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.Off;

            // у пустышек из Blender свои оси, VFX нужен +Z вдоль ствола
            Transform Fx(string socket, string name)
            {
                var fx = new GameObject(name).transform;
                fx.SetParent(ak.transform.Find(socket), false);
                fx.rotation = kickRoot.rotation;
                return fx;
            }
            return (Fx("socket_muzzle", "MuzzleFX"), Fx("socket_shell_eject", "ShellFX"));
        }

        [MenuItem("Tools/Portfolio/Install AK47 In Weapon Range")]
        public static void InstallInWeaponRange()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var weapon = Object.FindFirstObjectByType<WeaponController>();
            var kickRoot = weapon.kickRoot;
            var flash = weapon.muzzleFlash.transform;
            flash.SetParent(kickRoot, false);

            foreach (var old in new[] { "SM_Placeholder_AR", "socket_muzzle", "socket_shell_eject", "SM_AK47" })
            {
                var child = kickRoot.Find(old);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }
            // экземпляры, чей префаб уже удалён (например, старые руки), по имени не найти
            for (var i = kickRoot.childCount - 1; i >= 0; i--)
            {
                var child = kickRoot.GetChild(i).gameObject;
                if (PrefabUtility.IsPrefabAssetMissing(child))
                    Object.DestroyImmediate(child);
            }

            weapon.transform.SetLocalPositionAndRotation(HoldPosition, Quaternion.identity);
            var (muzzle, shell) = Attach(kickRoot);
            flash.SetParent(muzzle, false);
            flash.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            weapon.muzzleSocket = muzzle;
            weapon.shellSocket = shell;

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("AK47 installed in " + ScenePath);
        }
    }
}
