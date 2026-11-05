using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Portfolio.Editor.Showcase
{
    public static class PlayerBuilds
    {
        const string WeaponRange = "Assets/_Project/Scenes/WeaponRange.unity";
        const string GrassField = "Assets/_Project/Scenes/GrassField.unity";
        const string WinterGlade = "Assets/_Project/Scenes/WinterGlade.unity";
        const string LODDemo = "Assets/_Project/Scenes/LODDemo.unity";
        const string LightningStorm = "Assets/_Project/Scenes/LightningStorm.unity";
        const string Digitize = "Assets/_Project/Scenes/Digitize.unity";

        [MenuItem("Tools/Portfolio/Build/Windows - Weapon Range")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/WeaponRange.exe", WeaponRange);

        [MenuItem("Tools/Portfolio/Build/Windows - Grass Field")]
        public static void WindowsGrass() => Build(BuildTarget.StandaloneWindows64, "Builds/WindowsGrass/GrassField.exe", GrassField);

        [MenuItem("Tools/Portfolio/Build/Windows - Winter Glade")]
        public static void WindowsWinter() => Build(BuildTarget.StandaloneWindows64, "Builds/WindowsWinter/WinterGlade.exe", WinterGlade);

        [MenuItem("Tools/Portfolio/Build/Android - Winter Glade")]
        public static void AndroidWinter() => Build(BuildTarget.Android, "Builds/Android/WinterGlade.apk", WinterGlade);

        [MenuItem("Tools/Portfolio/Build/Windows - LOD Demo")]
        public static void WindowsLOD() => Build(BuildTarget.StandaloneWindows64, "Builds/WindowsLOD/LODDemo.exe", LODDemo);

        [MenuItem("Tools/Portfolio/Build/Android - LOD Demo")]
        public static void AndroidLOD() => Build(BuildTarget.Android, "Builds/Android/LODDemo.apk", LODDemo);

        [MenuItem("Tools/Portfolio/Build/Windows - Lightning Storm")]
        public static void WindowsLightning() => Build(BuildTarget.StandaloneWindows64, "Builds/WindowsLightning/LightningStorm.exe", LightningStorm);

        [MenuItem("Tools/Portfolio/Build/Windows - Digitize")]
        public static void WindowsDigitize() => Build(BuildTarget.StandaloneWindows64, "Builds/WindowsDigitize/Digitize.exe", Digitize);

        [MenuItem("Tools/Portfolio/Build/Android - Weapon Range")]
        public static void Android() => Build(BuildTarget.Android, "Builds/Android/WeaponRange.apk", WeaponRange);

        [MenuItem("Tools/Portfolio/Build/Android - Grass Field")]
        public static void AndroidGrass() => Build(BuildTarget.Android, "Builds/Android/GrassField.apk", GrassField);

        static void Build(BuildTarget target, string path, string scene)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
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
