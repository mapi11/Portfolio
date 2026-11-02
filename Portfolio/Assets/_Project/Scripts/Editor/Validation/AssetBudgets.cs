using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
    [CreateAssetMenu(menuName = "Portfolio/Asset Budgets", fileName = "AssetBudgets")]
    public class AssetBudgets : ScriptableObject
    {
        [Serializable]
        public class Rule
        {
            public string name = "Default";
            public string folder = "Assets/_Project/Art";

            [Header("Геометрия")]
            public int maxTrianglesLod0 = 10000;
            public int minLodCount = 0;
            [Range(0.1f, 1f)]
            public float maxLodTriangleRatio = 0.6f;
            public int maxMaterialsPerRenderer = 2;

            [Header("Скиннинг")]
            public int maxBones = 60;
            [Range(1, 4)]
            public int maxBonesPerVertex = 4;

            [Header("Текстуры")]
            public int maxTextureSize = 2048;
            public bool requireMobileCompression = true;
        }

        public List<Rule> rules = new() { new Rule() };

        [Header("Именование")]
        public string staticMeshPrefix = "SM_";
        public string skinnedMeshPrefix = "SK_";
        public string texturePrefix = "T_";
        public string materialPrefix = "M_";

        public string[] linearSuffixes = { "_N", "_MRAO", "_ORM", "_Mask" };
        public string normalSuffix = "_N";

        public Rule GetRule(string assetPath)
        {
            Rule best = null;
            foreach (var rule in rules)
            {
                if (string.IsNullOrEmpty(rule.folder) || !assetPath.StartsWith(rule.folder, StringComparison.Ordinal))
                    continue;
                if (best == null || rule.folder.Length > best.folder.Length)
                    best = rule;
            }
            return best;
        }

        static AssetBudgets s_Cached;

        public static AssetBudgets Load()
        {
            if (s_Cached != null)
                return s_Cached;

            var guids = AssetDatabase.FindAssets("t:" + nameof(AssetBudgets));
            if (guids.Length > 0)
                s_Cached = AssetDatabase.LoadAssetAtPath<AssetBudgets>(AssetDatabase.GUIDToAssetPath(guids[0]));

            if (s_Cached == null)
                s_Cached = CreateInstance<AssetBudgets>();
            return s_Cached;
        }

        void OnValidate() => s_Cached = null;
    }
}
