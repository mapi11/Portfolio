using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Portfolio.Editor.Showcase
{
    /// <summary>
    /// Билды для замеров. Batchmode:
    /// -executeMethod Portfolio.Editor.Showcase.PlayerBuilds.Windows
    /// -executeMethod Portfolio.Editor.Showcase.PlayerBuilds.Android
    /// </summary>
    public static class PlayerBuilds
    {
        [MenuItem("Tools/Portfolio/Build/Windows")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/WeaponRange.exe");

        [MenuItem("Tools/Portfolio/Build/Android")]
        public static void Android() => Build(BuildTarget.Android, "Builds/Android/WeaponRange.apk");

        static void Build(BuildTarget target, string path)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Project/Scenes/WeaponRange.unity" },
                locationPathName = path,
                target = target,
                options = BuildOptions.None
            });

            Debug.Log($"Build {target}: {report.summary.result}, {report.summary.totalSize / (1024f * 1024f):0.0} MB, {report.summary.totalTime}");
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
