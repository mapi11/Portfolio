using System;
using System.Collections.Generic;
using System.Globalization;
using Portfolio.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Portfolio.Crowd
{
    // Толпа манекенов на кольцевых дорожках. Два режима для сравнения:
    // VAT — анимация из текстуры и инстансинг, Skinned — обычные SkinnedMeshRenderer с Animator
    public class CrowdDemo : MonoBehaviour
    {
        [Header("VAT")]
        public Mesh vatMesh;
        public Material vatMaterial;

        [Header("Skinned")]
        public GameObject skinnedPrefab;

        [Header("Толпа")]
        public int count = 1000;
        public float walkSpeed = 1.1f;
        public Vector2 ringRadius = new(3f, 14f);
        public float ringSpacing = 0.9f;
        public bool useVat = true;

        static readonly int[] s_Counts = { 250, 500, 1000, 2000, 4000 };
        const int BatchSize = 1023;
        const float RadiusSteps = 4096f;   // то же значение в CrowdVat.shader

        struct Agent
        {
            public float radius;
            public float angle;
            public float direction;
        }

        readonly FrameStats m_Stats = new(120);
        readonly List<GameObject> m_Skinned = new();
        Agent[] m_Agents = Array.Empty<Agent>();
        Matrix4x4[] m_Matrices = Array.Empty<Matrix4x4>();
        RenderParams m_RenderParams;
        MaterialPropertyBlock m_Props;
        float m_FlashStarted = -10f;
        static readonly int s_ModeFlash = Shader.PropertyToID("_ModeFlash");
        GUIStyle m_Style;
        string m_ScreenshotPath;
        float m_CaptureAt = -1f;
        bool m_Compare;
        float m_ModeStarted;
        string m_CompareLog = "";

        public int DrawCalls => useVat ? Mathf.CeilToInt(count / (float)BatchSize) : count;

        void Start()
        {
            m_Props = new MaterialPropertyBlock();
            m_RenderParams = new RenderParams(vatMaterial)
            {
                shadowCastingMode = ShadowCastingMode.On,
                receiveShadows = true,
                matProps = m_Props,
                worldBounds = new Bounds(Vector3.zero, new Vector3(ringRadius.y * 2f + 4f, 6f, ringRadius.y * 2f + 4f))
            };
            ReadArgs();
            Spawn();
        }

        // Агенты по кольцам: соседние кольца идут в разные стороны, на кольце — равномерно с небольшим разбросом
        void Spawn()
        {
            var random = new System.Random(5);
            m_Agents = new Agent[count];
            m_Matrices = new Matrix4x4[count];
            var rings = new List<float>();
            for (var r = ringRadius.x; r <= ringRadius.y; r += ringSpacing)
                rings.Add(r);
            var circumference = 0f;
            foreach (var r in rings)
                circumference += r;

            var index = 0;
            for (var i = 0; i < rings.Count && index < count; i++)
            {
                var onRing = i == rings.Count - 1 ? count - index : Mathf.RoundToInt(count * rings[i] / circumference);
                // у каждого агента своя дорожка внутри кольца (разброс 0.25 м), радиус кратен 1/RadiusSteps.
                // Шейдер берёт из радиуса фазу шага: в отличие от позиции, радиус не меняется на ходу
                var lanes = new int[onRing];
                for (var k = 0; k < onRing; k++)
                    lanes[k] = k;
                for (var k = onRing - 1; k > 0; k--)
                {
                    var j = random.Next(k + 1);
                    (lanes[k], lanes[j]) = (lanes[j], lanes[k]);
                }
                for (var k = 0; k < onRing && index < count; k++, index++)
                {
                    var radius = rings[i] + ((lanes[k] + 0.5f) / onRing - 0.5f) * 0.25f;
                    m_Agents[index] = new Agent
                    {
                        radius = Mathf.Round(radius * RadiusSteps) / RadiusSteps,
                        angle = (k + (float)random.NextDouble() * 0.5f) / onRing * Mathf.PI * 2f,
                        direction = i % 2 == 0 ? 1f : -1f
                    };
                }
            }
            SpawnSkinned();
        }

        void SpawnSkinned()
        {
            foreach (var go in m_Skinned)
                Destroy(go);
            m_Skinned.Clear();
            if (useVat || skinnedPrefab == null)
                return;

            var random = new System.Random(9);
            for (var i = 0; i < count; i++)
            {
                var go = Instantiate(skinnedPrefab, transform);
                // свой момент цикла у каждого, как фаза из хэша в VAT
                go.GetComponent<Animator>().Play(0, 0, (float)random.NextDouble());
                m_Skinned.Add(go);
            }
        }

        void Update()
        {
            m_Stats.Add(Time.unscaledDeltaTime);
            HandleInput();
            Move();

            // при включении VAT толпа на полсекунды вспыхивает синим контуром
            m_Props.SetFloat(s_ModeFlash, 1f - Mathf.Clamp01((Time.time - m_FlashStarted) / 0.5f));
            if (useVat)
                for (var start = 0; start < count; start += BatchSize)
                    Graphics.RenderMeshInstanced(m_RenderParams, vatMesh, 0, m_Matrices, Mathf.Min(BatchSize, count - start), start);

            UpdateCompare();
            if (m_CaptureAt >= 0f && Time.time >= m_CaptureAt)
            {
                ScreenCapture.CaptureScreenshot(m_ScreenshotPath);
                Debug.Log($"Frame: {m_Stats.AverageMs:0.00} ms, agents {count}, mode {(useVat ? "VAT" : "Skinned")}");
                m_CaptureAt = -1f;
                Invoke(nameof(Quit), 0.5f);
            }
        }

        void Move()
        {
            for (var i = 0; i < m_Agents.Length; i++)
            {
                ref var agent = ref m_Agents[i];
                agent.angle += agent.direction * walkSpeed / agent.radius * Time.deltaTime;
                var position = new Vector3(Mathf.Cos(agent.angle), 0f, Mathf.Sin(agent.angle)) * agent.radius;
                var forward = new Vector3(-Mathf.Sin(agent.angle), 0f, Mathf.Cos(agent.angle)) * agent.direction;
                var rotation = Quaternion.LookRotation(forward);
                if (useVat)
                    m_Matrices[i] = Matrix4x4.TRS(position, rotation, Vector3.one);
                else if (i < m_Skinned.Count)
                    m_Skinned[i].transform.SetPositionAndRotation(position, rotation);
            }
        }

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || m_Compare)
                return;
            if (keyboard.tabKey.wasPressedThisFrame)
                SetMode(!useVat);
            var step = Array.IndexOf(s_Counts, count);
            if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame)
                SetCount(s_Counts[Mathf.Min(s_Counts.Length - 1, step + 1)]);
            if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame)
                SetCount(s_Counts[Mathf.Max(0, step - 1)]);
        }

        void SetMode(bool vat)
        {
            useVat = vat;
            m_ModeStarted = Time.time;
            if (vat)
                m_FlashStarted = Time.time;
            SpawnSkinned();
        }

        void SetCount(int value)
        {
            if (value == count)
                return;
            count = value;
            Spawn();
        }

        // -compare: по 4 с на каждый режим, среднее время кадра пишется в лог и на экран
        void UpdateCompare()
        {
            if (!m_Compare || Time.time - m_ModeStarted < 4f)
                return;
            var line = $"{(useVat ? "VAT" : "Skinned")}: {m_Stats.AverageMs:0.00} ms, draw calls ~{DrawCalls}";
            m_CompareLog += line + "\n";
            Debug.Log($"Crowd compare ({count} agents) {line}");
            if (!useVat)
            {
                SetMode(true);
                return;
            }
            m_Compare = false;
        }

        void ReadArgs()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-compare") >= 0)
            {
                m_Compare = true;
                useVat = false;
            }
            if (Array.IndexOf(args, "-skinned") >= 0)
                useVat = false;
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
            m_Style ??= new GUIStyle(GUI.skin.label) { richText = true };
            m_Style.fontSize = Mathf.RoundToInt(14 * scale);

            var ms = m_Stats.AverageMs;
            GUILayout.BeginArea(new Rect(10 * scale, 10 * scale, 700 * scale, 160 * scale));
            GUILayout.Label($"{ms:0.0} ms  {1000f / Mathf.Max(ms, 0.01f):0} fps", m_Style);
            GUILayout.Label($"Режим: {(useVat ? "<color=#2096F3>VAT + инстансинг</color>" : "SkinnedMeshRenderer + Animator")}   Агентов: {count}   Draw calls толпы: ~{DrawCalls}", m_Style);
            GUILayout.Label("Tab - сменить режим   +/- - число агентов   ЛКМ - камера", m_Style);
            if (m_CompareLog.Length > 0)
                GUILayout.Label(m_CompareLog, m_Style);
            GUILayout.EndArea();
        }
    }
}
