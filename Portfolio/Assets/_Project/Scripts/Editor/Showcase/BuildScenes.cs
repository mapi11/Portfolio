using System.Linq;
using UnityEditor;

namespace Portfolio.Editor.Showcase
{
    static class BuildScenes
    {
        public static void Add(string path)
        {
            if (EditorBuildSettings.scenes.Any(s => s.path == path))
                return;
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(path, true)).ToArray();
        }
    }
}
