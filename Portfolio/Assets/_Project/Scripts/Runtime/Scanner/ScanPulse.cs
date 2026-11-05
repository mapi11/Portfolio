using System;
using System.Globalization;
using Portfolio.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.Scanner
{
    // Тактический сканер: волна расходится от точки, цели, через которые прошёл фронт,
    // несколько секунд видны сквозь стены. Сама волна рисуется ScanRendererFeature
    public class ScanPulse : MonoBehaviour
    {
        [Tooltip("Маяк: отсюда волна идёт по пробелу и в авторежиме")]
        public Transform beacon;
        public Renderer[] targets = Array.Empty<Renderer>();

        [Header("Волна")]
        public float speed = 16f;
        public float maxRadius = 32f;
        public float trailLength = 9f;

        [Header("Подсветка целей")]
        public float revealTime = 3.5f;
        public bool autoScan = true;
        public float autoInterval = 5f;

        public static bool Visible { get; private set; }

        static readonly int s_Origin = Shader.PropertyToID("_ScanOrigin");
        static readonly int s_Params = Shader.PropertyToID("_ScanParams");
        static readonly int s_Reveal = Shader.PropertyToID("_Reveal");

        readonly FrameStats m_Stats = new(120);
        MaterialPropertyBlock m_Block;
        float[] m_Reveal = Array.Empty<float>();
        bool[] m_Hit = Array.Empty<bool>();
        Vector3 m_Origin;
        float m_Radius = -1f;
        float m_NextAuto;
        Camera m_Camera;
        GUIStyle m_Style;
        string m_ScreenshotPath;
        float m_CaptureAt = -1f;

        void OnEnable() => m_Block ??= new MaterialPropertyBlock();

        void OnDisable() => Visible = false;

        void Start()
        {
            m_Camera = Camera.main;
            m_Reveal = new float[targets.Length];
            m_Hit = new bool[targets.Length];
            m_NextAuto = 1f;
            ReadCaptureArgs();
        }

        public void Scan(Vector3 origin)
        {
            m_Origin = origin;
            m_Radius = 0f;
            Array.Clear(m_Hit, 0, m_Hit.Length);
        }

        void Update()
        {
            m_Stats.Add(Time.unscaledDeltaTime);
            HandleInput();

            if (autoScan && m_CaptureAt < 0f && Time.time >= m_NextAuto)
            {
                Scan(BeaconPoint());
                m_NextAuto = Time.time + autoInterval;
            }

            UpdateWave();
            UpdateTargets();

            if (m_CaptureAt >= 0f && Time.time >= m_CaptureAt)
            {
                ScreenCapture.CaptureScreenshot(m_ScreenshotPath);
                Debug.Log($"Frame: {m_Stats.AverageMs:0.00} ms, 1% low {m_Stats.OnePercentLowFps:0} fps");
                m_CaptureAt = -1f;
                Invoke(nameof(Quit), 0.5f);
            }
        }

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame)
                    Scan(BeaconPoint());
                if (keyboard.aKey.wasPressedThisFrame)
                    autoScan = !autoScan;
            }

            // клик по полу — волна из этой точки
            var mouse = Mouse.current;
            if (mouse == null || m_Camera == null || !mouse.leftButton.wasPressedThisFrame)
                return;
            var ray = m_Camera.ScreenPointToRay(mouse.position.ReadValue());
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance) && distance < 200f)
                Scan(ray.GetPoint(distance) + Vector3.up * 0.5f);
        }

        Vector3 BeaconPoint() => beacon != null ? beacon.position : transform.position;

        void UpdateWave()
        {
            if (m_Radius < 0f)
            {
                Visible = false;
                return;
            }

            m_Radius += speed * Time.deltaTime;
            // в конце волна гаснет, а не обрывается
            var intensity = 1f - Mathf.SmoothStep(0.6f, 1f, m_Radius / maxRadius);
            if (m_Radius > maxRadius)
            {
                m_Radius = -1f;
                Visible = false;
                return;
            }

            Visible = true;
            Shader.SetGlobalVector(s_Origin, m_Origin);
            Shader.SetGlobalVector(s_Params, new Vector4(m_Radius, trailLength, intensity, 0f));
        }

        void UpdateTargets()
        {
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null)
                    continue;

                // фронт дошёл до цели — она подсвечивается и дальше гаснет сама
                if (m_Radius >= 0f && !m_Hit[i] && m_Radius >= Vector3.Distance(m_Origin, target.bounds.center))
                {
                    m_Hit[i] = true;
                    m_Reveal[i] = 1f;
                }
                else
                {
                    m_Reveal[i] = Mathf.Max(0f, m_Reveal[i] - Time.deltaTime / revealTime);
                }

                target.GetPropertyBlock(m_Block);
                m_Block.SetFloat(s_Reveal, m_Reveal[i] * m_Reveal[i]);
                target.SetPropertyBlock(m_Block);
            }
        }

        void ReadCaptureArgs()
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
                : 3f;
            // для скриншота волна стартует от маяка так, чтобы к кадру пройти половину площадки
            Invoke(nameof(ScanFromBeacon), Mathf.Max(0f, m_CaptureAt - 1.1f));
        }

        void ScanFromBeacon() => Scan(BeaconPoint());

        void Quit() => Application.Quit();

        void OnGUI()
        {
            var scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
            m_Style ??= new GUIStyle(GUI.skin.label);
            m_Style.fontSize = Mathf.RoundToInt(14 * scale);

            var ms = m_Stats.AverageMs;
            GUILayout.BeginArea(new Rect(10 * scale, 10 * scale, 760 * scale, 80 * scale));
            GUILayout.Label($"{ms:0.0} ms  {1000f / Mathf.Max(ms, 0.01f):0} fps", m_Style);
            GUILayout.Label($"ЛКМ - скан из точки   Пробел - скан от маяка   A - авто: {(autoScan ? "вкл" : "выкл")}   ПКМ - камера", m_Style);
            GUILayout.EndArea();
        }
    }
}
