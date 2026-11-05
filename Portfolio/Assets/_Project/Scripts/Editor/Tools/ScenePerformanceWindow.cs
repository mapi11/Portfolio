using System;
using System.Collections.Generic;
using System.Linq;
using Portfolio.Editor.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

namespace Portfolio.Editor.Tools
{
    // Что реально стоит в открытой сцене: треугольники, материалы, LOD и память текстур по каждому объекту,
    // со сравнением с бюджетами из AssetBudgets и тепловой картой прямо в Scene View
    public class ScenePerformanceWindow : EditorWindow
    {
        enum Column { Name, Triangles, Lods, Materials, Textures }

        // у мелких мешей LOD ничего не экономят, предупреждение о них — только шум
        const int LodWorthTriangles = 500;

        class Row
        {
            public GameObject gameObject;
            public string name;
            public int triangles;
            public int lods;
            public int materials;
            public long textureBytes;
            public Bounds bounds;
            public string problem;
            public bool overBudget;
        }

        readonly List<Row> m_Rows = new();
        Column m_Sort = Column.Triangles;
        bool m_Descending = true;
        bool m_Heatmap = true;
        string m_Search = "";
        Vector2 m_Scroll;
        int m_TotalTriangles;
        int m_TotalMaterials;
        int m_UniqueMaterials;
        long m_TotalTextureBytes;
        int m_MaxTriangles = 1;

        [MenuItem("Tools/Portfolio/Scene Performance")]
        static void Open() => GetWindow<ScenePerformanceWindow>("Scene Performance");

        void OnEnable()
        {
            Collect();
            SceneView.duringSceneGui += DrawHeatmap;
            EditorApplication.hierarchyChanged += Collect;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChanged;
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= DrawHeatmap;
            EditorApplication.hierarchyChanged -= Collect;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChanged;
        }

        void OnSceneChanged(Scene from, Scene to) => Collect();

        void Collect()
        {
            m_Rows.Clear();
            var budgets = AssetBudgets.Load();
            var counted = new HashSet<Renderer>();

            // объект с LODGroup — одна строка: считаем LOD0, остальные уровни не суммируем
            foreach (var group in FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                var lods = group.GetLODs();
                var renderers = lods.Length > 0 ? lods[0].renderers.Where(r => r != null).ToArray() : Array.Empty<Renderer>();
                foreach (var lod in lods)
                    foreach (var renderer in lod.renderers)
                        if (renderer != null)
                            counted.Add(renderer);
                if (renderers.Length > 0)
                    m_Rows.Add(CreateRow(group.gameObject, renderers, lods.Length, budgets));
            }

            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (counted.Contains(renderer) || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
                    continue;
                m_Rows.Add(CreateRow(renderer.gameObject, new[] { renderer }, 0, budgets));
            }

            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();
            foreach (var row in m_Rows)
                foreach (var renderer in Renderers(row))
                    foreach (var material in renderer.sharedMaterials.Where(m => m != null))
                    {
                        materials.Add(material);
                        foreach (var texture in Textures(material))
                            textures.Add(texture);
                    }

            m_TotalTriangles = m_Rows.Sum(r => r.triangles);
            m_TotalMaterials = m_Rows.Sum(r => r.materials);
            m_UniqueMaterials = materials.Count;
            m_TotalTextureBytes = textures.Sum(TextureBytes);
            m_MaxTriangles = Mathf.Max(1, m_Rows.Count > 0 ? m_Rows.Max(r => r.triangles) : 1);
            SortRows();
            Repaint();
            SceneView.RepaintAll();
        }

        Row CreateRow(GameObject gameObject, Renderer[] renderers, int lods, AssetBudgets budgets)
        {
            var row = new Row { gameObject = gameObject, name = gameObject.name, lods = lods, bounds = renderers[0].bounds };
            var textures = new HashSet<Texture>();
            string meshPath = null;
            foreach (var renderer in renderers)
            {
                row.bounds.Encapsulate(renderer.bounds);
                var mesh = MeshOf(renderer);
                if (mesh != null)
                {
                    for (var s = 0; s < mesh.subMeshCount; s++)
                        row.triangles += (int)(mesh.GetIndexCount(s) / 3);
                    meshPath ??= AssetDatabase.GetAssetPath(mesh);
                }
                row.materials += renderer.sharedMaterials.Length;
                foreach (var material in renderer.sharedMaterials.Where(m => m != null))
                    foreach (var texture in Textures(material))
                        textures.Add(texture);
            }
            row.textureBytes = textures.Sum(TextureBytes);

            // бюджет той же папки, что и у валидатора ассетов; встроенные меши (Cube, Plane) не проверяем
            if (budgets != null && !string.IsNullOrEmpty(meshPath) && meshPath.StartsWith("Assets/"))
            {
                var rule = budgets.GetRule(meshPath);
                if (rule != null)
                {
                    if (row.triangles > rule.maxTrianglesLod0)
                    {
                        row.overBudget = true;
                        row.problem = $"{row.triangles:N0} > {rule.maxTrianglesLod0:N0} ({rule.name})";
                    }
                    else if (rule.minLodCount > 0 && row.lods < rule.minLodCount && row.triangles >= LodWorthTriangles)
                    {
                        row.problem = $"нет LOD ({rule.name})";
                    }
                }
            }
            return row;
        }

        static Mesh MeshOf(Renderer renderer) => renderer switch
        {
            SkinnedMeshRenderer skinned => skinned.sharedMesh,
            _ => renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null
        };

        static IEnumerable<Renderer> Renderers(Row row)
        {
            if (row.gameObject == null)
                yield break;
            if (row.gameObject.TryGetComponent<LODGroup>(out var group) && group.lodCount > 0)
            {
                foreach (var renderer in group.GetLODs()[0].renderers)
                    if (renderer != null)
                        yield return renderer;
            }
            else if (row.gameObject.TryGetComponent<Renderer>(out var renderer))
            {
                yield return renderer;
            }
        }

        static IEnumerable<Texture> Textures(Material material)
        {
            foreach (var id in material.GetTexturePropertyNameIDs())
            {
                var texture = material.GetTexture(id);
                if (texture != null)
                    yield return texture;
            }
        }

        // Размер в памяти для формата, в который текстура импортирована на текущей платформе, со всеми мипами
        static long TextureBytes(Texture texture)
        {
            if (texture is not Texture2D texture2D)
                return UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            long bytes = 0;
            for (var mip = 0; mip < texture2D.mipmapCount; mip++)
                bytes += GraphicsFormatUtility.ComputeMipmapSize(Mathf.Max(1, texture2D.width >> mip), Mathf.Max(1, texture2D.height >> mip), texture2D.graphicsFormat);
            return bytes;
        }

        void SortRows()
        {
            Func<Row, IComparable> key = m_Sort switch
            {
                Column.Name => r => r.name,
                Column.Lods => r => r.lods,
                Column.Materials => r => r.materials,
                Column.Textures => r => r.textureBytes,
                _ => r => r.triangles
            };
            var sorted = m_Descending ? m_Rows.OrderByDescending(key).ToList() : m_Rows.OrderBy(key).ToList();
            m_Rows.Clear();
            m_Rows.AddRange(sorted);
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Обновить", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    Collect();
                var heatmap = GUILayout.Toggle(m_Heatmap, "Тепловая карта", EditorStyles.toolbarButton, GUILayout.Width(110));
                if (heatmap != m_Heatmap)
                {
                    m_Heatmap = heatmap;
                    SceneView.RepaintAll();
                }
                GUILayout.FlexibleSpace();
                m_Search = GUILayout.TextField(m_Search, EditorStyles.toolbarSearchField, GUILayout.Width(180));
            }

            var overBudget = m_Rows.Count(r => r.overBudget);
            var warnings = m_Rows.Count(r => r.problem != null && !r.overBudget);
            EditorGUILayout.HelpBox(
                $"Объектов: {m_Rows.Count}   Треугольников (LOD0): {m_TotalTriangles:N0}\n" +
                $"Материалов на рендерерах: {m_TotalMaterials} (≈ draw calls до батчинга), уникальных: {m_UniqueMaterials}   " +
                $"Текстуры: {m_TotalTextureBytes / (1024f * 1024f):0.0} МБ\n" +
                $"Сверх бюджета: {overBudget}   Без LOD по правилам папки: {warnings}",
                overBudget > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                Header("Объект", Column.Name, 200);
                Header("Треуг.", Column.Triangles, 80);
                Header("LOD", Column.Lods, 40);
                Header("Мат.", Column.Materials, 45);
                Header("Текстуры", Column.Textures, 80);
                GUILayout.Label("Бюджет", EditorStyles.boldLabel);
            }

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            foreach (var row in m_Rows)
            {
                if (row.gameObject == null || (m_Search.Length > 0 && row.name.IndexOf(m_Search, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(row.name, EditorStyles.linkLabel, GUILayout.Width(200)))
                    {
                        Selection.activeGameObject = row.gameObject;
                        SceneView.lastActiveSceneView?.FrameSelected();
                    }
                    var previous = GUI.color;
                    GUI.color = CostColor(row.triangles);
                    GUILayout.Label(row.triangles.ToString("N0"), GUILayout.Width(80));
                    GUI.color = previous;
                    GUILayout.Label(row.lods > 0 ? row.lods.ToString() : "-", GUILayout.Width(40));
                    GUILayout.Label(row.materials.ToString(), GUILayout.Width(45));
                    GUILayout.Label($"{row.textureBytes / (1024f * 1024f):0.00} МБ", GUILayout.Width(80));
                    if (row.problem != null)
                    {
                        var icon = row.overBudget ? "console.erroricon.sml" : "console.warnicon.sml";
                        GUILayout.Label(EditorGUIUtility.TrIconContent(icon, row.problem), GUILayout.Width(18));
                        GUILayout.Label(row.problem);
                    }
                    else
                    {
                        GUILayout.Label("в бюджете", EditorStyles.miniLabel);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void Header(string title, Column column, float width)
        {
            var label = m_Sort == column ? title + (m_Descending ? " ▼" : " ▲") : title;
            if (!GUILayout.Button(label, EditorStyles.boldLabel, GUILayout.Width(width)))
                return;
            m_Descending = m_Sort != column || !m_Descending;
            m_Sort = column;
            SortRows();
        }

        // зелёный — лёгкий объект, красный — самый тяжёлый в сцене
        Color CostColor(int triangles)
        {
            var t = Mathf.Sqrt((float)triangles / m_MaxTriangles);
            return t < 0.5f ? Color.Lerp(new Color(0.45f, 0.85f, 0.45f), new Color(0.95f, 0.8f, 0.3f), t * 2f)
                            : Color.Lerp(new Color(0.95f, 0.8f, 0.3f), new Color(0.95f, 0.35f, 0.3f), t * 2f - 1f);
        }

        void DrawHeatmap(SceneView view)
        {
            if (!m_Heatmap || Event.current.type != EventType.Repaint)
                return;
            var style = new GUIStyle(EditorStyles.whiteMiniLabel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            foreach (var row in m_Rows)
            {
                if (row.gameObject == null || !row.gameObject.activeInHierarchy)
                    continue;
                var color = CostColor(row.triangles);
                Handles.color = color;
                Handles.DrawWireCube(row.bounds.center, row.bounds.size);
                style.normal.textColor = color;
                Handles.Label(row.bounds.center + Vector3.up * row.bounds.extents.y, row.triangles >= 1000 ? $"{row.triangles / 1000f:0.#}k" : row.triangles.ToString(), style);
            }
        }
    }
}
