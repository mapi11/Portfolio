using System;
using System.Collections.Generic;
using System.Globalization;
using Portfolio.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace Portfolio.Lightning
{
    public class LightningStorm : MonoBehaviour
    {
        [Header("Materials")]
        public Material boltMaterial;
        public Material scorchMaterial;
        public Mesh scorchQuad;

        [Header("Lights")]
        public Light keyLight;
        public Light flashLight;
        public Light emberLight;

        [Header("Particles")]
        public ParticleSystem sparks;
        public ParticleSystem debris;
        public ParticleSystem dust;
        public ParticleSystem groundFlash;
        public ParticleSystem smoke;

        [Header("Strikes")]
        public bool autoStrike = true;
        public Vector2 strikeInterval = new(2.5f, 6f);
        public float strikeRadius = 13f;
        public float cloudHeight = 110f;
        public float leaderTime = 0.16f;

        [Header("Scorch Marks")]
        public int maxScorchMarks = 8;
        public float scorchSize = 7f;
        [Tooltip("Быстрое остывание вспыхнувших веток, сек")]
        public float coolingTime = 4f;
        [Tooltip("Долгое тление самых толстых веток, сек")]
        public float smolderTime = 35f;

        [Header("Flash")]
        public float flashLightIntensity = 80f;
        public float keyFlashIntensity = 1.8f;
        public Color flashColor = new(0.78f, 0.84f, 1f);
        public float ambientFlash = 1.5f;

        static readonly int s_Reveal = Shader.PropertyToID("_Reveal");
        static readonly int s_Leader = Shader.PropertyToID("_Leader");
        static readonly int s_Stroke = Shader.PropertyToID("_Stroke");
        static readonly int s_BranchGlow = Shader.PropertyToID("_BranchGlow");
        static readonly int s_Heat = Shader.PropertyToID("_Heat");
        static readonly int s_Fade = Shader.PropertyToID("_Fade");
        static readonly int s_AtlasRect = Shader.PropertyToID("_AtlasRect");

        class Bolt
        {
            public Mesh mesh;
            public MeshRenderer renderer;
            public Vector3 ground;
            public Vector3 glowPoint;
            public float time = -1f;
            public float end;
            public bool impacted;
            public int nextPulse;
            public readonly List<(float time, float peak)> pulses = new();
        }

        class Scorch
        {
            public Transform transform;
            public MeshRenderer renderer;
            public float age;
            public float fade = 1f;
            public bool dying;
            public float smokeDebt;
        }

        readonly List<Bolt> m_Bolts = new();
        readonly List<Scorch> m_Scorches = new();
        readonly Stack<Scorch> m_FreeScorches = new();
        readonly FrameStats m_Stats = new(120);
        MaterialPropertyBlock m_Block;
        System.Random m_Random;
        float m_NextStrike;
        float m_Shake;
        float m_Flash;
        Vector3 m_FlashPosition;
        Vector3 m_FlashGround;
        Quaternion m_KeyRotation;
        float m_KeyIntensity;
        Color m_KeyColor;
        Color m_Ambient;
        Vector2 m_PressPosition;
        Camera m_Camera;
        GUIStyle m_Style;

        string m_ScreenshotPath;
        float m_CaptureAt = -1f;
        readonly Queue<(float time, Vector3 point)> m_Scripted = new();

        void OnEnable()
        {
            m_Block ??= new MaterialPropertyBlock();
            m_Random ??= new System.Random(Environment.TickCount);
            if (m_Bolts.Count == 0)
                for (var i = 0; i < 3; i++)
                    m_Bolts.Add(CreateBolt(i));
        }

        void Start()
        {
            m_Camera = Camera.main;
            m_KeyRotation = keyLight.transform.rotation;
            m_KeyIntensity = keyLight.intensity;
            m_KeyColor = keyLight.color;
            m_Ambient = RenderSettings.ambientLight.linear;
            m_NextStrike = 0.6f;
            ReadCaptureArgs();
        }

        Bolt CreateBolt(int index)
        {
            var go = new GameObject("Bolt " + index) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Lightning Bolt" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = boltMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return new Bolt { mesh = mesh, renderer = renderer };
        }

        void Update()
        {
            m_Stats.Add(Time.unscaledDeltaTime);
            HandleInput();

            if (m_Scripted.Count > 0 && Time.time >= m_Scripted.Peek().time)
                Strike(m_Scripted.Dequeue().point);
            else if (autoStrike && m_CaptureAt < 0f && Time.time >= m_NextStrike)
            {
                Strike(RandomGroundPoint());
                m_NextStrike = Time.time + Mathf.Lerp(strikeInterval.x, strikeInterval.y, (float)m_Random.NextDouble());
            }

            m_Flash = 0f;
            foreach (var bolt in m_Bolts)
                UpdateBolt(bolt);

            UpdateLighting();
            UpdateScorches();

            if (m_CaptureAt >= 0f && Time.time >= m_CaptureAt)
            {
                ScreenCapture.CaptureScreenshot(m_ScreenshotPath);
                Debug.Log($"Frame: {m_Stats.AverageMs:0.00} ms, 1% low {m_Stats.OnePercentLowFps:0} fps");
                m_CaptureAt = -1f;
                Invoke(nameof(Quit), 0.5f);
            }
        }

        void LateUpdate()
        {
            if (m_Camera == null)
                return;
            // OrbitCamera каждый кадр выставляет позицию заново, поэтому тряска не накапливается
            if (m_Shake > 0.001f)
            {
                m_Camera.transform.position += Random.insideUnitSphere * (m_Shake * 0.18f);
                m_Shake *= Mathf.Exp(-Time.deltaTime * 7f);
            }
        }

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame)
                    Strike(RandomGroundPoint());
                if (keyboard.aKey.wasPressedThisFrame)
                    autoStrike = !autoStrike;
                if (keyboard.cKey.wasPressedThisFrame)
                    Clear();
            }

            // клик без перетаскивания — удар в точку под курсором; перетаскивание крутит камеру
            var mouse = Mouse.current;
            if (mouse == null || m_Camera == null)
                return;
            if (mouse.leftButton.wasPressedThisFrame)
                m_PressPosition = mouse.position.ReadValue();
            if (mouse.leftButton.wasReleasedThisFrame && Vector2.Distance(m_PressPosition, mouse.position.ReadValue()) < 6f)
            {
                var ray = m_Camera.ScreenPointToRay(mouse.position.ReadValue());
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var distance) && distance < 200f)
                {
                    var point = ray.GetPoint(distance);
                    if (new Vector2(point.x, point.z).magnitude < strikeRadius * 1.3f)
                        Strike(point);
                }
            }
        }

        Vector3 RandomGroundPoint()
        {
            var angle = (float)m_Random.NextDouble() * Mathf.PI * 2f;
            var radius = Mathf.Sqrt((float)m_Random.NextDouble()) * strikeRadius;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        // Убирает с площадки всё, что оставили удары: следы, дым, искры и текущие разряды
        public void Clear()
        {
            foreach (var scorch in m_Scorches)
            {
                scorch.transform.gameObject.SetActive(false);
                m_FreeScorches.Push(scorch);
            }
            m_Scorches.Clear();

            foreach (var bolt in m_Bolts)
            {
                bolt.time = -1f;
                bolt.renderer.enabled = false;
            }

            foreach (var system in new[] { sparks, debris, dust, groundFlash, smoke })
                if (system != null)
                    system.Clear();

            m_Shake = 0f;
        }

        public void Strike(Vector3 ground)
        {
            var bolt = m_Bolts[0];
            foreach (var candidate in m_Bolts)
                if (candidate.time < 0f || candidate.time > bolt.time)
                {
                    bolt = candidate;
                    if (candidate.time < 0f)
                        break;
                }

            var drift = new Vector3(Range(-25f, 25f), 0f, Range(-25f, 25f));
            var top = ground + drift + Vector3.up * cloudHeight;
            LightningBoltMesh.Build(bolt.mesh, top, ground, m_Random);

            bolt.ground = ground;
            bolt.glowPoint = Vector3.Lerp(ground, top, 0.3f);
            bolt.time = 0f;
            bolt.impacted = false;
            bolt.nextPulse = 0;
            bolt.pulses.Clear();

            // главный удар и 1–4 повторных по тому же каналу, каждый слабее
            var t = leaderTime;
            bolt.pulses.Add((t, 1f));
            var restrikes = 1 + m_Random.Next(4);
            for (var i = 0; i < restrikes; i++)
            {
                t += Range(0.05f, 0.14f);
                bolt.pulses.Add((t, Range(0.45f, 0.9f) * (1f - i * 0.12f)));
            }
            bolt.end = t + 0.5f;
            bolt.renderer.enabled = true;
        }

        void UpdateBolt(Bolt bolt)
        {
            if (bolt.time < 0f)
                return;
            bolt.time += Time.deltaTime;
            var t = bolt.time;
            // удар о землю проверяем раньше конца разряда: при подвисании кадра весь разряд может уложиться в один шаг
            if (t >= leaderTime && !bolt.impacted)
            {
                bolt.impacted = true;
                Impact(bolt.ground);
            }
            if (t > bolt.end)
            {
                bolt.time = -1f;
                bolt.renderer.enabled = false;
                return;
            }

            float reveal, leader;
            if (t < leaderTime)
            {
                // лидер идёт ступенями с неровной скоростью
                var k = Mathf.Pow(t / leaderTime, 1.25f);
                reveal = Mathf.Floor(k * 28f) / 28f;
                leader = Range(0.35f, 1f);
            }
            else
            {
                reveal = 10f;
                leader = 0f;
            }

            var stroke = 0f;
            foreach (var (time, peak) in bolt.pulses)
                if (t >= time)
                    stroke += peak * Mathf.Exp(-(t - time) / 0.045f);
            // продолжающийся ток: канал тлеет дольше самих импульсов
            if (t >= leaderTime)
                stroke += 0.12f * Mathf.Exp(-(t - leaderTime) / 0.3f);

            while (bolt.nextPulse < bolt.pulses.Count && t >= bolt.pulses[bolt.nextPulse].time)
            {
                var peak = bolt.pulses[bolt.nextPulse].peak;
                Emit(groundFlash, bolt.ground + Vector3.up * 2.2f, Vector3.zero, 0.14f, 4.5f * peak, Color.white);
                bolt.nextPulse++;
            }

            var branchGlow = t < leaderTime ? 1f : Mathf.Exp(-(t - leaderTime) / 0.07f);
            m_Block.Clear();
            m_Block.SetFloat(s_Reveal, reveal);
            m_Block.SetFloat(s_Leader, leader);
            m_Block.SetFloat(s_Stroke, stroke);
            m_Block.SetFloat(s_BranchGlow, branchGlow);
            bolt.renderer.SetPropertyBlock(m_Block);

            var brightness = Mathf.Max(stroke, leader * 0.06f);
            if (brightness > m_Flash)
            {
                m_Flash = brightness;
                m_FlashPosition = bolt.glowPoint;
                m_FlashGround = bolt.ground;
            }
        }

        void Impact(Vector3 point)
        {
            for (var i = 0; i < 90; i++)
            {
                var direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) * 0.8f + 0.25f;
                Emit(sparks, point + Vector3.up * 0.1f, direction.normalized * Range(4f, 15f), Range(0.4f, 1.3f), Range(0.04f, 0.09f), Color.white);
            }
            for (var i = 0; i < 40; i++)
            {
                var direction = Random.onUnitSphere;
                direction.y = Mathf.Abs(direction.y) + 0.6f;
                Emit(debris, point + Vector3.up * 0.1f, direction.normalized * Range(3f, 9f), Range(1f, 2f), Range(0.05f, 0.16f), Color.white);
            }
            for (var i = 0; i < 48; i++)
            {
                var angle = i / 48f * Mathf.PI * 2f + Range(-0.1f, 0.1f);
                var outward = new Vector3(Mathf.Cos(angle), Range(0.05f, 0.25f), Mathf.Sin(angle));
                Emit(dust, point + outward * 0.4f + Vector3.up * 0.25f, outward * Range(2f, 5f), Range(1.2f, 2f), Range(0.3f, 0.6f), Color.white);
            }
            // первый клуб дыма: много мелких частиц, чтобы облако было мягким, а не из крупных кусков
            for (var i = 0; i < 24; i++)
            {
                var offset = Random.insideUnitCircle * 0.6f;
                var velocity = new Vector3(offset.x, Range(1f, 2.6f), offset.y);
                Emit(smoke, point + new Vector3(offset.x, 0.2f, offset.y), velocity, Range(3f, 5f), Range(0.35f, 0.7f), Color.white);
            }

            SpawnScorch(point);
            var distance = m_Camera != null ? Vector3.Distance(m_Camera.transform.position, point) : 20f;
            m_Shake = Mathf.Max(m_Shake, 1.4f / (1f + distance / 25f));
        }

        void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float lifetime, float size, Color color)
        {
            if (system == null)
                return;
            system.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = lifetime,
                startSize = size,
                startColor = color,
                rotation = Range(0f, 360f),
                applyShapeToPosition = false
            }, 1);
        }

        void SpawnScorch(Vector3 point)
        {
            var alive = 0;
            foreach (var scorch in m_Scorches)
                if (!scorch.dying)
                    alive++;
            if (alive >= maxScorchMarks)
                foreach (var scorch in m_Scorches)
                    if (!scorch.dying)
                    {
                        scorch.dying = true;
                        break;
                    }

            var mark = m_FreeScorches.Count > 0 ? m_FreeScorches.Pop() : CreateScorch();
            mark.transform.gameObject.SetActive(true);
            // небольшой подъём по номеру следа, чтобы пересекающиеся следы не мерцали
            mark.transform.SetPositionAndRotation(point + Vector3.up * (0.02f + m_Scorches.Count * 0.002f),
                Quaternion.Euler(90f, Range(0f, 360f), 0f));
            mark.transform.localScale = Vector3.one * scorchSize * Range(0.85f, 1.15f);
            mark.age = 0f;
            mark.fade = 1f;
            mark.dying = false;
            mark.smokeDebt = 0f;

            var tile = m_Random.Next(4);
            m_Block.Clear();
            m_Block.SetVector(s_AtlasRect, new Vector4(0.5f, 0.5f, tile % 2 * 0.5f, tile / 2 * 0.5f));
            m_Block.SetFloat(s_Heat, 1f);
            m_Block.SetFloat(s_Fade, 1f);
            mark.renderer.SetPropertyBlock(m_Block);
            m_Scorches.Add(mark);
        }

        Scorch CreateScorch()
        {
            var go = new GameObject("Scorch") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = scorchQuad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = scorchMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return new Scorch { transform = go.transform, renderer = renderer };
        }

        void UpdateScorches()
        {
            Scorch hottest = null;
            var hottestHeat = 0f;
            for (var i = m_Scorches.Count - 1; i >= 0; i--)
            {
                var scorch = m_Scorches[i];
                scorch.age += Time.deltaTime;
                if (scorch.dying)
                {
                    scorch.fade -= Time.deltaTime / 2f;
                    if (scorch.fade <= 0f)
                    {
                        scorch.transform.gameObject.SetActive(false);
                        m_Scorches.RemoveAt(i);
                        m_FreeScorches.Push(scorch);
                        continue;
                    }
                }

                var heat = 0.72f * Mathf.Exp(-scorch.age / coolingTime) + 0.28f * Mathf.Exp(-scorch.age / smolderTime);
                scorch.renderer.GetPropertyBlock(m_Block);
                m_Block.SetFloat(s_Heat, heat);
                m_Block.SetFloat(s_Fade, scorch.fade);
                scorch.renderer.SetPropertyBlock(m_Block);

                if (heat > hottestHeat)
                {
                    hottestHeat = heat;
                    hottest = scorch;
                }

                // от горячего следа поднимается дым, пока угли не остынут
                scorch.smokeDebt += heat * 16f * Time.deltaTime;
                while (scorch.smokeDebt >= 1f)
                {
                    scorch.smokeDebt -= 1f;
                    var offset = Random.insideUnitCircle * scorch.transform.localScale.x * 0.08f;
                    var position = scorch.transform.position + new Vector3(offset.x, 0.1f, offset.y);
                    var velocity = new Vector3(Range(0.1f, 0.35f), Range(0.5f, 1f), Range(-0.1f, 0.2f));
                    Emit(smoke, position, velocity, Range(4f, 6f), Range(0.25f, 0.5f), Color.white);
                }
            }

            if (emberLight != null)
            {
                emberLight.enabled = hottest != null && hottestHeat > 0.02f;
                if (emberLight.enabled)
                {
                    emberLight.transform.position = hottest.transform.position + Vector3.up * 0.6f;
                    emberLight.intensity = Mathf.Pow(hottestHeat, 1.5f) * 7f * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 9f, 0.3f));
                }
            }
        }

        void UpdateLighting()
        {
            var flash = Mathf.Clamp01(m_Flash);

            flashLight.enabled = flash > 0.002f;
            flashLight.transform.position = m_FlashGround + Vector3.up * 3f;
            flashLight.intensity = m_Flash * flashLightIntensity;
            flashLight.color = flashColor;

            // в момент вспышки основной свет разворачивается от разряда, тени ложатся от молнии
            if (flash > 0.02f)
            {
                var direction = (Vector3.zero - m_FlashPosition).normalized;
                keyLight.transform.rotation = Quaternion.LookRotation(direction);
                keyLight.intensity = m_KeyIntensity + flash * keyFlashIntensity;
                keyLight.color = Color.Lerp(m_KeyColor, flashColor, flash);
            }
            else
            {
                keyLight.transform.rotation = m_KeyRotation;
                keyLight.intensity = m_KeyIntensity;
                keyLight.color = m_KeyColor;
            }

            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(m_Ambient + flashColor * (m_Ambient.maxColorComponent * ambientFlash * flash));
            RenderSettings.ambientProbe = ambient;
        }

        float Range(float min, float max) => min + (float)m_Random.NextDouble() * (max - min);

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
                : 6f;

            // для скриншота: пара остывающих следов и разряд, пойманный на главном ударе
            m_Random = new System.Random(7);
            m_Scripted.Enqueue((m_CaptureAt - 3.2f, new Vector3(-3.5f, 0f, 2.5f)));
            m_Scripted.Enqueue((m_CaptureAt - 1.6f, new Vector3(3.5f, 0f, -1.5f)));
            m_Scripted.Enqueue((m_CaptureAt - leaderTime - 0.02f, new Vector3(0.5f, 0f, 1f)));
        }

        void Quit() => Application.Quit();

        void OnGUI()
        {
            var scale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;
            m_Style ??= new GUIStyle(GUI.skin.label) { richText = true };
            m_Style.fontSize = Mathf.RoundToInt(14 * scale);

            var ms = m_Stats.AverageMs;
            GUILayout.BeginArea(new Rect(10 * scale, 10 * scale, 560 * scale, 120 * scale));
            GUILayout.Label($"{ms:0.0} ms  {1000f / Mathf.Max(ms, 0.01f):0} fps", m_Style);
            GUILayout.Label($"Клик — удар в точку   Пробел — случайный удар   A — авто: {(autoStrike ? "вкл" : "выкл")}   C — очистить", m_Style);
            GUILayout.EndArea();
        }
    }
}
