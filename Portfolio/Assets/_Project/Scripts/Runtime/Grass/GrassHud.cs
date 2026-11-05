using System;
using System.Globalization;
using Portfolio.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.Grass
{
    public class GrassHud : MonoBehaviour
    {
        public GrassField field;
        public bool show = true;

        [Header("Вид оптимизации (F)")]
        [Tooltip("Выключенная камера сверху: включается вместо основной, отсечение по-прежнему считает основная")]
        public Camera observer;
        public Material frustumMaterial;
        public float frustumLength = 30f;

        readonly FrameStats m_Stats = new(120);
        readonly Vector3[] m_Corners = new Vector3[4];
        GUIStyle m_Style;
        string m_ScreenshotPath;
        float m_CaptureAt = -1f;
        bool m_DebugView;
        Mesh m_Frustum;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-debug-view") >= 0)
                SetDebugView(true);

            var index = Array.IndexOf(args, "-screenshot");
            if (index < 0 || index + 1 >= args.Length)
                return;

            m_ScreenshotPath = args[index + 1];
            var after = Array.IndexOf(args, "-capture-after");
            m_CaptureAt = after >= 0 && after + 1 < args.Length &&
                          float.TryParse(args[after + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : 4f;
        }

        void OnDestroy()
        {
            if (m_Frustum != null)
                Destroy(m_Frustum);
        }

        void Update()
        {
            m_Stats.Add(Time.unscaledDeltaTime);

            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                SetDebugView(!m_DebugView);

            if (m_CaptureAt >= 0f && Time.time >= m_CaptureAt)
            {
                ScreenCapture.CaptureScreenshot(m_ScreenshotPath);
                Debug.Log($"Frame: {m_Stats.AverageMs:0.00} ms, 1% low {m_Stats.OnePercentLowFps:0} fps" + (field != null
                    ? $", blades {field.VisibleBlades}, chunks LOD0 {field.VisibleLod0} / LOD1 {field.VisibleLod1}"
                    : ""));
                m_CaptureAt = -1f;
                Invoke(nameof(Quit), 0.5f);
            }
        }

        void LateUpdate()
        {
            if (m_DebugView)
                DrawFrustum();
        }

        void SetDebugView(bool on)
        {
            if (field == null || observer == null)
                return;
            m_DebugView = on;
            field.debugLod = on;
            observer.enabled = on;
        }

        // Обзор основной камеры на земле: видно, что трава рисуется только внутри него
        void DrawFrustum()
        {
            var main = Camera.main;
            if (main == null || frustumMaterial == null)
                return;

            if (m_Frustum == null)
            {
                m_Frustum = new Mesh { name = "Camera Frustum" };
                m_Frustum.MarkDynamic();
            }

            // клин на земле: от камеры до верхних углов дальней плоскости, всё прижато к полу
            main.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), frustumLength, Camera.MonoOrStereoscopicEye.Mono, m_Corners);
            Vector3 Ground(Vector3 p) => new(p.x, 0.15f, p.z);
            var vertices = new[]
            {
                Ground(main.transform.position),
                Ground(main.transform.TransformPoint(m_Corners[1])),
                Ground(main.transform.TransformPoint(m_Corners[2]))
            };

            m_Frustum.Clear();
            m_Frustum.vertices = vertices;
            m_Frustum.SetIndices(new[] { 0, 1, 0, 2, 1, 2 }, MeshTopology.Lines, 0);
            m_Frustum.RecalculateBounds();
            Graphics.RenderMesh(new RenderParams(frustumMaterial), m_Frustum, 0, Matrix4x4.identity);
        }

        void Quit() => Application.Quit();

        void OnGUI()
        {
            if (!show)
                return;

            var scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
            m_Style ??= new GUIStyle(GUI.skin.label);
            m_Style.fontSize = Mathf.RoundToInt(14 * scale);

            var ms = m_Stats.AverageMs;
            GUILayout.BeginArea(new Rect(10 * scale, 10 * scale, 760 * scale, 140 * scale));
            GUILayout.Label($"{ms:0.0} ms  {1000f / Mathf.Max(ms, 0.01f):0} fps  1% low {m_Stats.OnePercentLowFps:0}", m_Style);
            if (field != null)
            {
                GUILayout.Label($"Blades {field.VisibleBlades:N0}  chunks LOD0 {field.VisibleLod0} / LOD1 {field.VisibleLod1}  draw calls {(field.VisibleLod0 > 0 ? 1 : 0) + (field.VisibleLod1 > 0 ? 1 : 0)}", m_Style);
                if (observer != null)
                    GUILayout.Label(m_DebugView
                        ? "Вид сверху: зелёный — LOD0, жёлтый — LOD1, вне обзора камеры (синий клин) трава не рисуется.  F — назад"
                        : "F — вид оптимизации", m_Style);
            }
            GUILayout.EndArea();
        }
    }
}
