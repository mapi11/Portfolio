using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
    public class AssetValidationPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var budgets = AssetBudgets.Load();
            var rule = budgets.GetRule(assetPath);
            if (rule == null)
                return;

            var importer = (TextureImporter)assetImporter;
            var name = Path.GetFileNameWithoutExtension(assetPath);

            if (!importer.importSettingsMissing)
                return;

            if (name.EndsWith(budgets.normalSuffix))
                importer.textureType = TextureImporterType.NormalMap;
            else if (AssetValidator.HasSuffix(name, budgets.linearSuffixes))
                importer.sRGBTexture = false;

            importer.maxTextureSize = Mathf.Min(importer.maxTextureSize, rule.maxTextureSize);

            if (rule.requireMobileCompression)
            {
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    settings.overridden = true;
                    settings.maxTextureSize = rule.maxTextureSize;
                    settings.format = importer.textureType == TextureImporterType.NormalMap
                        ? TextureImporterFormat.ASTC_4x4
                        : TextureImporterFormat.ASTC_6x6;
                    importer.SetPlatformTextureSettings(settings);
                }
            }
        }

        void OnPostprocessModel(GameObject root)
        {
            var budgets = AssetBudgets.Load();
            var rule = budgets.GetRule(assetPath);
            if (rule == null)
                return;

            var issues = new List<Issue>();
            AssetValidator.ValidateModel(assetPath, root, rule, budgets, issues);
            foreach (var issue in issues)
                Log(issue);
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            var issues = new List<Issue>();
            foreach (var path in imported)
                if (path.EndsWith(".mat") || AssetImporter.GetAtPath(path) is TextureImporter)
                    AssetValidator.Validate(path, issues);
            foreach (var issue in issues)
                Log(issue);
        }

        static void Log(Issue issue)
        {
            var context = AssetDatabase.LoadMainAssetAtPath(issue.assetPath);
            if (issue.severity == Severity.Error)
                Debug.LogError(issue.ToString(), context);
            else
                Debug.LogWarning(issue.ToString(), context);
        }
    }
}
