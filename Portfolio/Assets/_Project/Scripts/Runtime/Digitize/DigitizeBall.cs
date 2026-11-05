using System;
using System.Globalization;
using Portfolio.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.Digitize
{
    // Шар оцифровки: летит за курсором по площадке и оставляет за собой затухающий след.
    // Сам эффект целиком в шейдере Portfolio/Digitize — сюда приходят только точки следа
    public class DigitizeBall : MonoBehaviour
    {
        [Tooltip("Радиус, в котором объекты оцифровываются")]
        public float radius = 1.8f;
        public float height = 1.1f;
        public Vector2 area = new(7f, 5f);
        public float followSharpness = 8f;

        [Header("След")]
        public float trailInterval = 0.12f;
        public float trailLife = 2.2f;

        [Header("Скриншот: шар облетает эти точки")]
        public Vector3[] capturePath = Array.Empty<Vector3>();

        const int TrailPoints = 7;
        static readonly int s_Trail = Shader.PropertyToID("_DigitizeTrail");

        readonly Vector4[] m_Trail = new Vector4[TrailPoints + 1];
        readonly Vector3[] m_Points = new Vector3[TrailPoints];
        readonly float[] m_Ages = new float[TrailPoints];
        readonly FrameStats m_Stats = new(120);
        int m_Next;
        float m_TrailTimer;
        Vector3 m_Target;
        Camera m_Camera;
        GUIStyle m_Style;
        string m_ScreenshotPath;
        float m_CaptureAt = -1f;

        void Start()
        {
            m_Camera = Camera.main;
            m_Target = transform.position;
            for (var i = 0; i < TrailPoints; i++)
                m_Ages[i] = trailLife;
            ReadCaptureArgs();
        }

        void OnDisable()
        {
            Array.Clear(m_Trail, 0, m_Trail.Length);
            Shader.SetGlobalVectorArray(s_Trail, m_Trail);
        }

        void Update()
        {
            m_Stats.Add(Time.unscaledDeltaTime);
            UpdateTarget();

            var position = Vector3.Lerp(transform.position, m_Target, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            position.y = height + Mathf.Sin(Time.time * 2.2f) * 0.06f;
            transform.position = position;

            UpdateTrail();

            if (m_CaptureAt >= 0f && Time.time >= m_CaptureAt)
            {
                ScreenCapture.CaptureScreenshot(m_ScreenshotPath);
                Debug.Log($"Frame: {m_Stats.AverageMs:0.00} ms, 1% low {m_Stats.OnePercentLowFps:0} fps");
                m_CaptureAt = -1f;
                Invoke(nameof(Quit), 0.5f);
            }
        }

        void UpdateTarget()
        {
            if (capturePath.Length > 1 && m_ScreenshotPath != null)
            {
                // для скриншота шар идёт по заданным точкам, по секунде на отрезок
                var t = Mathf.Min(Time.time, capturePath.Length - 1.001f);
                var i = Mathf.FloorToInt(t);
                m_Target = Vector3.Lerp(capturePath[i], capturePath[i + 1], Mathf.SmoothStep(0f, 1f, t - i));
                return;
            }

            Vector2? pointer = null;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                pointer = Touchscreen.current.primaryTouch.position.ReadValue();
            else if (Mouse.current != null)
                pointer = Mouse.current.position.ReadValue();
            if (pointer == null || m_Camera == null)
                return;

            // курсор переносим на горизонтальную плоскость на высоте шара: так шар идёт точно под курсором
            var ray = m_Camera.ScreenPointToRay(pointer.Value);
            if (!new Plane(Vector3.up, Vector3.up * height).Raycast(ray, out var distance))
                return;
            var point = ray.GetPoint(distance);
            m_Target = new Vector3(Mathf.Clamp(point.x, -area.x, area.x), height, Mathf.Clamp(point.z, -area.y, area.y));
        }

        void UpdateTrail()
        {
            for (var i = 0; i < TrailPoints; i++)
                m_Ages[i] += Time.deltaTime;

            m_TrailTimer += Time.deltaTime;
            if (m_TrailTimer >= trailInterval)
            {
                m_TrailTimer = 0f;
                m_Points[m_Next] = transform.position;
                m_Ages[m_Next] = 0f;
                m_Next = (m_Next + 1) % TrailPoints;
            }

            // нулевой слот — сам шар, остальные — след, сжимающийся со временем
            m_Trail[0] = new Vector4(transform.position.x, transform.position.y, transform.position.z, radius);
            for (var i = 0; i < TrailPoints; i++)
            {
                var life = 1f - m_Ages[i] / trailLife;
                var p = m_Points[i];
                m_Trail[i + 1] = life > 0f ? new Vector4(p.x, p.y, p.z, radius * life) : Vector4.zero;
            }
            Shader.SetGlobalVectorArray(s_Trail, m_Trail);
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
                : 4f;
        }

        void Quit() => Application.Quit();

        void OnGUI()
        {
            var scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
            m_Style ??= new GUIStyle(GUI.skin.label);
            m_Style.fontSize = Mathf.RoundToInt(14 * scale);

            var ms = m_Stats.AverageMs;
            GUILayout.BeginArea(new Rect(10 * scale, 10 * scale, 640 * scale, 80 * scale));
            GUILayout.Label($"{ms:0.0} ms  {1000f / Mathf.Max(ms, 0.01f):0} fps", m_Style);
            GUILayout.Label("Мышь — вести шар   ПКМ — вращать камеру   Колесо — приближение", m_Style);
            GUILayout.EndArea();
        }
    }
}
