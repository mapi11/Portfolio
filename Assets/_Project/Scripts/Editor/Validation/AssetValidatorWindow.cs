using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
    public class AssetValidatorWindow : EditorWindow
    {
        const string DefaultFolder = "Assets/_Project/Art";

        string m_Folder = DefaultFolder;
        bool m_ShowWarnings = true;
        List<Issue> m_Issues = new();
        Vector2 m_Scroll;

        [MenuItem("Tools/Portfolio/Asset Validator")]
        static void Open() => GetWindow<AssetValidatorWindow>("Asset Validator");

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                m_Folder = EditorGUILayout.TextField("Папка", m_Folder);
                if (GUILayout.Button("Проверить", GUILayout.Width(100)))
                    m_Issues = AssetValidator.ValidateFolder(m_Folder);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Бюджеты", GUILayout.Width(100)))
                    Selection.activeObject = AssetBudgets.Load();
                m_ShowWarnings = GUILayout.Toggle(m_ShowWarnings, "Предупреждения");
            }

            var errors = m_Issues.Count(i => i.severity == Severity.Error);
            EditorGUILayout.HelpBox($"Ошибок: {errors}, предупреждений: {m_Issues.Count - errors}",
                errors > 0 ? MessageType.Error : MessageType.Info);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            foreach (var issue in m_Issues)
            {
                if (issue.severity == Severity.Warning && !m_ShowWarnings)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    var icon = issue.severity == Severity.Error ? "console.erroricon.sml" : "console.warnicon.sml";
                    GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(20));
                    if (GUILayout.Button(System.IO.Path.GetFileName(issue.assetPath), EditorStyles.linkLabel, GUILayout.Width(200)))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(issue.assetPath));
                    GUILayout.Label(issue.message, EditorStyles.wordWrappedLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
