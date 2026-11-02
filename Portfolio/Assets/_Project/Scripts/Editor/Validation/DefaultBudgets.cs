using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
    /// <summary>
    /// Бюджеты из Docs/Budgets.md. Цель — mid/low Android (Mali-G52, 3 ГБ RAM), 60 fps.
    /// </summary>
    public static class DefaultBudgets
    {
        const string Path = "Assets/_Project/Settings/AssetBudgets.asset";

        [MenuItem("Tools/Portfolio/Create Default Budgets")]
        public static void Create()
        {
            var budgets = ScriptableObject.CreateInstance<AssetBudgets>();
            budgets.rules.Clear();
            budgets.rules.Add(new AssetBudgets.Rule
            {
                name = "Props", folder = "Assets/_Project/Art",
                maxTrianglesLod0 = 3000, minLodCount = 2, maxTextureSize = 1024,
                maxMaterialsPerRenderer = 1, maxBones = 0
            });
            budgets.rules.Add(new AssetBudgets.Rule
            {
                name = "FP Weapon", folder = "Assets/_Project/Art/Weapons",
                maxTrianglesLod0 = 12000, minLodCount = 3, maxTextureSize = 2048,
                maxMaterialsPerRenderer = 2, maxBones = 20, maxBonesPerVertex = 2
            });
            budgets.rules.Add(new AssetBudgets.Rule
            {
                name = "FP Arms", folder = "Assets/_Project/Art/Characters/FPSArms",
                maxTrianglesLod0 = 8000, minLodCount = 0, maxTextureSize = 1024,
                maxMaterialsPerRenderer = 1, maxBones = 45, maxBonesPerVertex = 4
            });

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            AssetDatabase.CreateAsset(budgets, Path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = budgets;
        }
    }
}
