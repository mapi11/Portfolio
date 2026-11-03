using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
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

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            AssetDatabase.CreateAsset(budgets, Path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = budgets;
        }
    }
}
