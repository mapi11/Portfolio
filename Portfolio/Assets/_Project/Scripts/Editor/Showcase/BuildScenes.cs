using System.Linq;
using UnityEditor;

namespace Portfolio.Editor.Showcase
{
    static class BuildScenes
    {
        /// <summary>Добавляет сцену в Build Settings, не трогая остальные.</summary>
        public static void Add(string path)
        {
            if (EditorBuildSettings.scenes.Any(s => s.path == path))
                return;
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(path, true)).ToArray();
        }
    }
}
