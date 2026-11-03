using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.LodDemo
{
    public class LODDemoHud : MonoBehaviour
    {
        public LODGroup[] groups = Array.Empty<LODGroup>();
        public LODDemoCamera demoCamera;
        public bool tint = true;

        static readonly Color[] s_LodColors = { new(0.3f, 0.85f, 0.35f), new(0.95f, 0.8f, 0.2f), new(0.95f, 0.35f, 0.25f) };

        readonly Dictionary<Renderer, Material[]> m_Original = new();
        readonly List<Material> m_Tints = new();
        GUIStyle m_Style;
        string m_ScreenshotPath;
        float m_CaptureAt = -1f;

        void Start()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var color in s_LodColors)
            {
                var material = new Material(shader);
                material.SetColor("_BaseColor", color);
                m_Tints.Add(material);
            }
            foreach (var group in groups)
                foreach (var renderer in group.GetComponentsInChildren<Renderer>())
                    m_Original[renderer] = renderer.sharedMaterials;
            ApplyTint();

            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-screenshot");
            if (index >= 0 && index + 1 < args.Length)
            {
                m_ScreenshotPath = args[index + 1];
                var after = Array.IndexOf(args, "-capture-after");
                m_CaptureAt = after >= 0 && float.TryParse(args[after + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var s) ? s : 4f;
                var noTint = Array.IndexOf(args, "-no-tint") >= 0;
                if (noTint)
                {
                    tint = false;
                    ApplyTint();
                }
            }
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
            {
                tint = !tint;
                ApplyTint();
            }

            if (m_CaptureAt >= 0f && Time.time >= m_CaptureAt)
            {
                ScreenCapture.CaptureScreenshot(m_ScreenshotPath);
                m_CaptureAt = -1f;
                Invoke(nameof(Quit), 0.5f);
            }
        }

        void Quit() => Application.Quit();

        void ApplyTint()
        {
            foreach (var group in groups)
            {
                var lods = group.GetLODs();
                for (var i = 0; i < lods.Length; i++)
                    foreach (var renderer in lods[i].renderers)
                    {
                        if (renderer == null)
                            continue;
                        var original = m_Original[renderer];
                        if (!tint)
                        {
                            renderer.sharedMaterials = original;
                            continue;
                        }
                        var tinted = new Material[original.Length];
                        for (var m = 0; m < tinted.Length; m++)
                            tinted[m] = m_Tints[Mathf.Min(i, m_Tints.Count - 1)];
                        renderer.sharedMaterials = tinted;
                    }
            }
        }

        // Тот же расчёт, что у LODGroup: доля высоты экрана, которую занимает объект, с учётом LOD Bias
        public static int CurrentLod(LODGroup group, Camera camera)
        {
            var t = group.transform;
            var scale = t.lossyScale;
            var size = group.size * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var distance = Vector3.Distance(camera.transform.position, t.TransformPoint(group.localReferencePoint));
            var height = size / (2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)) * QualitySettings.lodBias;

            var lods = group.GetLODs();
            for (var i = 0; i < lods.Length; i++)
                if (height >= lods[i].screenRelativeTransitionHeight)
                    return i;
            return -1;
        }

        static int Triangles(LOD lod)
        {
            var total = 0;
            foreach (var renderer in lod.renderers)
                if (renderer != null && renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                    for (var s = 0; s < filter.sharedMesh.subMeshCount; s++)
                        total += (int)filter.sharedMesh.GetIndexCount(s) / 3;
            return total;
        }

        void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null)
                return;

            var scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
            m_Style ??= new GUIStyle(GUI.skin.label) { richText = true };
            m_Style.fontSize = Mathf.RoundToInt(16 * scale);

            GUILayout.BeginArea(new Rect(14 * scale, 10 * scale, 620 * scale, 300 * scale));
            if (demoCamera != null)
                GUILayout.Label($"Дистанция камеры: {demoCamera.Distance:0} м   LOD Bias: {QualitySettings.lodBias:0.##}", m_Style);
            var total = 0;
            foreach (var group in groups)
            {
                var lod = CurrentLod(group, camera);
                var lods = group.GetLODs();
                var tris = lod >= 0 ? Triangles(lods[lod]) : 0;
                total += tris;
                var color = lod >= 0 ? ColorUtility.ToHtmlStringRGB(s_LodColors[Mathf.Min(lod, 2)]) : "999999";
                var label = lod >= 0 ? $"LOD{lod}" : "Culled";
                GUILayout.Label($"{group.name,-8} <color=#{color}><b>{label}</b></color>  {tris} tris", m_Style);
            }
            GUILayout.Label($"Всего в кадре: {total} tris", m_Style);
            GUILayout.Label("T — подсветка уровней   Пробел — пауза", m_Style);
            GUILayout.EndArea();
        }
    }
}
