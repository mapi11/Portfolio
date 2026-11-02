using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.VFX
{
    /// <summary>
    /// Следы от пуль: общий кольцевой буфер на все поверхности с жёстким лимитом
    /// и временем жизни. Отрисовка через Graphics.RenderMeshInstanced: все декали
    /// одной поверхности — один draw call, без GameObject-ов и без URP Decal Projector
    /// (он дорог на мобильных: нужен depth prepass или DBuffer).
    ///
    /// Затухание — instanced-свойство _FadeOut, поэтому исчезающие декали
    /// не ломают инстансинг и не требуют отдельных материалов.
    ///
    /// Ограничение: только статичная геометрия, к движущимся объектам декали не крепятся.
    /// </summary>
    public class DecalSystem : MonoBehaviour
    {
        [Tooltip("Максимум декалей на сцене (все поверхности вместе). Новая вытесняет самую старую")]
        [Range(1, 64)] public int maxDecals = 15;
        [Tooltip("Сколько секунд декаль живёт, включая затухание")]
        public float lifetime = 10f;
        [Tooltip("Длительность затухания в конце жизни")]
        public float fadeDuration = 2f;
        [Tooltip("Отступ от поверхности против z-fighting (вместе с Offset в шейдере)")]
        public float surfaceOffset = 0.002f;

        static readonly int s_FadeOut = Shader.PropertyToID("_FadeOut");

        struct Decal
        {
            public Material material;
            public Matrix4x4 matrix;
            public float spawnTime;
        }

        class Batch
        {
            public Matrix4x4[] matrices;
            public float[] fade;
            public int count;
            public MaterialPropertyBlock properties;
            public RenderParams renderParams;
        }

        Decal[] m_Decals;
        int m_Next;
        int m_Count;
        readonly Dictionary<Material, Batch> m_Batches = new();

        public int ActiveCount { get; private set; }
        public int DrawCalls { get; private set; }

        void Awake() => m_Decals = new Decal[maxDecals];

        public void Add(Material material, Vector3 point, Vector3 normal, float size)
        {
            if (material == null)
                return;

            // Случайный поворот вокруг нормали: одна текстура не выглядит повторяющейся.
            var rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            m_Decals[m_Next] = new Decal
            {
                material = material,
                matrix = Matrix4x4.TRS(point + normal * surfaceOffset, rotation, new Vector3(size, size, 1f)),
                spawnTime = Time.time
            };
            m_Next = (m_Next + 1) % m_Decals.Length;
            m_Count = Mathf.Min(m_Count + 1, m_Decals.Length);
        }

        void LateUpdate()
        {
            foreach (var batch in m_Batches.Values)
                batch.count = 0;

            var now = Time.time;
            var fadeStart = lifetime - fadeDuration;
            ActiveCount = 0;

            for (var i = 0; i < m_Count; i++)
            {
                ref var decal = ref m_Decals[i];
                var age = now - decal.spawnTime;
                if (age >= lifetime)
                    continue;

                var batch = GetBatch(decal.material);
                batch.matrices[batch.count] = decal.matrix;
                batch.fade[batch.count] = fadeDuration > 0f ? Mathf.Clamp01((age - fadeStart) / fadeDuration) : 0f;
                batch.count++;
                ActiveCount++;
            }

            DrawCalls = 0;
            foreach (var batch in m_Batches.Values)
            {
                if (batch.count == 0)
                    continue;
                batch.properties.SetFloatArray(s_FadeOut, batch.fade);
                Graphics.RenderMeshInstanced(batch.renderParams, InstancedMeshes.Decal, 0, batch.matrices, batch.count);
                DrawCalls++;
            }
        }

        Batch GetBatch(Material material)
        {
            if (m_Batches.TryGetValue(material, out var batch))
                return batch;

            // Массивы на полный лимит: Unity фиксирует размер массива свойства при первой установке.
            var properties = new MaterialPropertyBlock();
            batch = new Batch
            {
                matrices = new Matrix4x4[maxDecals],
                fade = new float[maxDecals],
                properties = properties,
                renderParams = new RenderParams(material)
                {
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                    matProps = properties
                }
            };
            m_Batches.Add(material, batch);
            return batch;
        }
    }
}
