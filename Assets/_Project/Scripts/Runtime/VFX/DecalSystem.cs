using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.VFX
{
    /// <summary>
    /// Следы от пуль: кольцевой буфер матриц на материал, отрисовка через
    /// Graphics.RenderMeshInstanced. Все декали одной поверхности — один draw call,
    /// без GameObject-ов и без URP Decal Projector (он дорог на мобильных:
    /// нужен depth prepass или DBuffer).
    ///
    /// Ограничение: только статичная геометрия, к движущимся объектам декали не крепятся.
    /// </summary>
    public class DecalSystem : MonoBehaviour
    {
        [Tooltip("Декалей на материал; старые перезаписываются")]
        public int capacityPerMaterial = 64;
        [Tooltip("Отступ от поверхности против z-fighting (вместе с Offset в шейдере)")]
        public float surfaceOffset = 0.002f;

        class Batch
        {
            public Matrix4x4[] matrices;
            public int count;
            public int next;
            public RenderParams renderParams;
        }

        readonly Dictionary<Material, Batch> m_Batches = new();

        public void Add(Material material, Vector3 point, Vector3 normal, float size)
        {
            if (material == null)
                return;

            if (!m_Batches.TryGetValue(material, out var batch))
            {
                batch = new Batch
                {
                    matrices = new Matrix4x4[capacityPerMaterial],
                    renderParams = new RenderParams(material)
                    {
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = false,
                        worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f)
                    }
                };
                m_Batches.Add(material, batch);
            }

            // Случайный поворот вокруг нормали: одна текстура не выглядит повторяющейся.
            var rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            batch.matrices[batch.next] = Matrix4x4.TRS(point + normal * surfaceOffset, rotation, new Vector3(size, size, 1f));
            batch.next = (batch.next + 1) % batch.matrices.Length;
            batch.count = Mathf.Min(batch.count + 1, batch.matrices.Length);
        }

        void LateUpdate()
        {
            foreach (var batch in m_Batches.Values)
                if (batch.count > 0)
                    Graphics.RenderMeshInstanced(batch.renderParams, InstancedMeshes.Decal, 0, batch.matrices, batch.count);
        }

        public int DrawCalls => m_Batches.Count;
    }
}
