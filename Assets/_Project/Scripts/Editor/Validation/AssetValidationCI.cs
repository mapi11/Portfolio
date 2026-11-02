using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
    /// <summary>
    /// Точка входа для CI:
    /// Unity -batchmode -nographics -projectPath . -executeMethod Portfolio.Editor.Validation.AssetValidationCI.Run -quit
    /// Код выхода 1, если есть ошибки (предупреждения сборку не валят).
    /// </summary>
    public static class AssetValidationCI
    {
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
