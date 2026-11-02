using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Tools
{
    public class MaterialReplacer : EditorWindow
    {
        GameObject m_Target;
        Material m_Material;
        bool m_IncludeChildren;

        [MenuItem("Tools/Portfolio/Material Replacer")]
        static void Open() => GetWindow<MaterialReplacer>("Material Replacer");

        void OnGUI()
        {
            m_Target = (GameObject)EditorGUILayout.ObjectField("Объект", m_Target, typeof(GameObject), true);
            m_Material = (Material)EditorGUILayout.ObjectField("Материал", m_Material, typeof(Material), false);
            m_IncludeChildren = EditorGUILayout.Toggle("Включая дочерние", m_IncludeChildren);

            var renderers = GetRenderers();
            if (m_Target != null && renderers.Length == 0)
                EditorGUILayout.HelpBox("На объекте нет Renderer", MessageType.Warning);

            using (new EditorGUI.DisabledScope(m_Material == null || renderers.Length == 0))
            {
                if (GUILayout.Button("Заменить материал", GUILayout.Height(30)))
                    Replace(renderers);
            }
        }

        Renderer[] GetRenderers()
        {
            if (m_Target == null)
                return new Renderer[0];
            if (m_IncludeChildren)
                return m_Target.GetComponentsInChildren<Renderer>(true);
            return m_Target.TryGetComponent<Renderer>(out var renderer) ? new[] { renderer } : new Renderer[0];
        }

        void Replace(Renderer[] renderers)
        {
            // через Undo, чтобы замену можно было откатить Ctrl+Z и сцена пометилась изменённой
            Undo.RecordObjects(renderers, "Replace Material");
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                    materials[i] = m_Material;
                renderer.sharedMaterials = materials;
            }
            Debug.Log($"Material Replacer: {m_Material.name} -> {renderers.Length} renderer(s)", m_Target);
        }
    }
}
