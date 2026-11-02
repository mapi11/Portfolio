using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
    public static class AssetValidationCI
    {
        // Unity -batchmode -nographics -projectPath Portfolio -executeMethod Portfolio.Editor.Validation.AssetValidationCI.Run -quit
        public static void Run()
        {
            var issues = AssetValidator.ValidateFolder("Assets/_Project/Art");
            foreach (var issue in issues)
                Debug.Log(issue.ToString());

            var errors = issues.Count(i => i.severity == Severity.Error);
            Debug.Log($"Asset validation: {errors} errors, {issues.Count - errors} warnings");
            EditorApplication.Exit(errors > 0 ? 1 : 0);
        }
    }
}
