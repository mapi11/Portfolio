using System;
using System.Globalization;
using Portfolio.Diagnostics;
using UnityEngine;

namespace Portfolio.Grass
{
    public class GrassHud : MonoBehaviour
    {
        public GrassField field;
        public bool show = true;

        readonly FrameStats m_Stats = new(120);
        GUIStyle m_Style;
        string m_ScreenshotPath;
        float m_CaptureAt = -1f;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
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

        void Update()
        {
            m_Stats.Add(Time.unscaledDeltaTime);

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

        void Quit() => Application.Quit();

        void OnGUI()
        {
            if (!show)
                return;

            var scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
            m_Style ??= new GUIStyle(GUI.skin.label);
            m_Style.fontSize = Mathf.RoundToInt(14 * scale);

            var ms = m_Stats.AverageMs;
            GUILayout.BeginArea(new Rect(10 * scale, 10 * scale, 520 * scale, 120 * scale));
            GUILayout.Label($"{ms:0.0} ms  {1000f / Mathf.Max(ms, 0.01f):0} fps  1% low {m_Stats.OnePercentLowFps:0}", m_Style);
            if (field != null)
                GUILayout.Label($"Blades {field.VisibleBlades:N0}  chunks LOD0 {field.VisibleLod0} / LOD1 {field.VisibleLod1}  draw calls {(field.VisibleLod0 > 0 ? 1 : 0) + (field.VisibleLod1 > 0 ? 1 : 0)}", m_Style);
            GUILayout.EndArea();
        }
    }
}
