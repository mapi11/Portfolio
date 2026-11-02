using System;
using Portfolio.VFX;
using Portfolio.Weapon;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.Diagnostics
{
    /// <summary>
    /// Повторяемый сценарий для замеров из Docs/Profiling.md: 30 с непрерывной стрельбы
    /// с проводкой прицела по всем мишеням. Запуск: кнопка в HUD, клавиша B
    /// или аргумент командной строки -benchmark (тогда приложение закроется по окончании).
    /// </summary>
    public class Benchmark : MonoBehaviour
    {
        public WeaponController weapon;
        public FirstPersonLook look;
        [Tooltip("Необязательно: показывать число декалей в HUD")]
        public DecalSystem decals;
        public float duration = 30f;
        [Tooltip("Амплитуда проводки прицела, градусы (рыскание, тангаж)")]
        public Vector2 sweep = new(35f, 6f);
        public float sweepSpeed = 0.35f;
        public bool showHud = true;

        readonly FrameStats m_Live = new(120);
        readonly FrameStats m_Run = new(4096);
        float m_StartTime = -1f;
        Vector2 m_StartAngles;
        int m_StartShots;
        bool m_QuitWhenDone;
        string m_ScreenshotPath;
        string m_LastResult;
        GUIStyle m_Style;

        public bool Running => m_StartTime >= 0f;

        void Start()
        {
            // -benchmark [-benchmark-duration 30] [-screenshot path.png]
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-benchmark") < 0)
                return;

            if (float.TryParse(Argument(args, "-benchmark-duration"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                duration = seconds;
            m_ScreenshotPath = Argument(args, "-screenshot");
            m_QuitWhenDone = true;
            Begin();
        }

        static string Argument(string[] args, string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        public void Begin()
        {
            m_Run.Reset();
            m_StartTime = Time.time;
            m_StartAngles = new Vector2(look.Yaw, look.Pitch);
            m_StartShots = weapon.ShotsFired;
            weapon.TriggerOverride = true;
        }

        void End()
        {
            weapon.TriggerOverride = false;
            m_StartTime = -1f;
            look.Yaw = m_StartAngles.x;
            look.Pitch = m_StartAngles.y;

            m_LastResult = $"Benchmark {duration:0}s | {SystemInfo.deviceModel} | {SystemInfo.graphicsDeviceName} | " +
                           $"avg {m_Run.AverageMs:0.00} ms ({1000f / m_Run.AverageMs:0} fps) | 1% low {m_Run.OnePercentLowFps:0} fps | " +
                           $"shots {weapon.ShotsFired - m_StartShots}";
            Debug.Log(m_LastResult);

            if (m_QuitWhenDone)
                Application.Quit();
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            m_Live.Add(dt);

            if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
            {
                if (Running) End();
                else Begin();
            }

            if (!Running)
                return;

            m_Run.Add(dt);
            var t = Time.time - m_StartTime;
            look.Yaw = m_StartAngles.x + Mathf.Sin(t * sweepSpeed * Mathf.PI * 2f) * sweep.x;
            look.Pitch = m_StartAngles.y + Mathf.Sin(t * sweepSpeed * Mathf.PI * 4f) * sweep.y;

            // Кадр для README/breakdown: середина очереди, когда на сцене уже есть декали и гильзы.
            if (!string.IsNullOrEmpty(m_ScreenshotPath) && t >= duration * 0.4f)
            {
                ScreenCapture.CaptureScreenshot(m_ScreenshotPath);
                m_ScreenshotPath = null;
            }

            if (t >= duration)
                End();
        }

        void OnGUI()
        {
            if (!showHud)
                return;

            var scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
            m_Style ??= new GUIStyle(GUI.skin.label);
            m_Style.fontSize = Mathf.RoundToInt(14 * scale);

            var ms = m_Live.AverageMs;
            GUILayout.BeginArea(new Rect(10 * scale, 10 * scale, 520 * scale, 200 * scale));
            GUILayout.Label($"{ms:0.0} ms  {1000f / Mathf.Max(ms, 0.01f):0} fps  1% low {m_Live.OnePercentLowFps:0}", m_Style);
            if (decals != null)
                GUILayout.Label($"Decals {decals.ActiveCount}/{decals.maxDecals}, draw calls {decals.DrawCalls}", m_Style);
            if (Running)
                GUILayout.Label($"Benchmark: {duration - (Time.time - m_StartTime):0}s", m_Style);
            else if (GUILayout.Button("Benchmark (B)", GUILayout.Width(180 * scale), GUILayout.Height(36 * scale)))
                Begin();
            if (!string.IsNullOrEmpty(m_LastResult))
                GUILayout.Label(m_LastResult, m_Style);
            GUILayout.EndArea();
        }
    }
}
