using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.Grass
{
    public class GrassField : MonoBehaviour
    {
        [Header("Меши чанка (генерирует Tools/Portfolio/Build Grass Field)")]
        public Mesh lod0;
        public Mesh lod1;
        public int lod0Blades;
        public int lod1Blades;
        public Material material;

        [Header("Поле")]
        public Vector2 size = new(96f, 96f);
        public float chunkSize = 4f;
        public float maxBladeHeight = 1f;
        public int seed = 1;

        [Header("Дальность")]
        public float lod0Distance = 14f;
        public float maxDistance = 40f;
        public float fadeLength = 10f;

        static readonly int s_GrassFade = Shader.PropertyToID("_GrassFade");

        Matrix4x4[] m_Chunks;
        Bounds[] m_ChunkBounds;
        Matrix4x4[] m_Lod0Matrices;
        Matrix4x4[] m_Lod1Matrices;
        float[] m_Lod0Keys;
        float[] m_Lod1Keys;
        readonly Plane[] m_Planes = new Plane[6];
        RenderParams m_RenderParams;

        public int VisibleLod0 { get; private set; }
        public int VisibleLod1 { get; private set; }
        public int VisibleBlades => VisibleLod0 * lod0Blades + VisibleLod1 * lod1Blades;
        public int ChunkCount => m_Chunks?.Length ?? 0;

        void OnEnable() => Build();

        void OnValidate()
        {
            if (isActiveAndEnabled && Application.isPlaying)
                Build();
        }

        void Build()
        {
            var countX = Mathf.Max(1, Mathf.RoundToInt(size.x / chunkSize));
            var countZ = Mathf.Max(1, Mathf.RoundToInt(size.y / chunkSize));
            var count = countX * countZ;

            m_Chunks = new Matrix4x4[count];
            m_ChunkBounds = new Bounds[count];
            m_Lod0Matrices = new Matrix4x4[count];
            m_Lod1Matrices = new Matrix4x4[count];
            m_Lod0Keys = new float[count];
            m_Lod1Keys = new float[count];

            var random = new System.Random(seed);
            var origin = transform.position - new Vector3(countX * chunkSize, 0f, countZ * chunkSize) * 0.5f;
            for (var z = 0; z < countZ; z++)
            for (var x = 0; x < countX; x++)
            {
                var center = origin + new Vector3((x + 0.5f) * chunkSize, 0f, (z + 0.5f) * chunkSize);
                var rotation = Quaternion.Euler(0f, random.Next(4) * 90f, 0f);
                var index = z * countX + x;
                m_Chunks[index] = Matrix4x4.TRS(center, rotation, Vector3.one);
                m_ChunkBounds[index] = new Bounds(center + Vector3.up * (maxBladeHeight * 0.5f),
                    new Vector3(chunkSize + maxBladeHeight * 2f, maxBladeHeight, chunkSize + maxBladeHeight * 2f));
            }

            m_RenderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(transform.position, new Vector3(size.x, maxBladeHeight * 4f, size.y))
            };
        }

        void Update()
        {
            Shader.SetGlobalVector(s_GrassFade, new Vector4(maxDistance - fadeLength, maxDistance, 0f, 0f));
        }

        void LateUpdate()
        {
            var camera = Camera.main;
            if (camera == null || material == null || lod0 == null)
                return;

            GeometryUtility.CalculateFrustumPlanes(camera, m_Planes);
            var cameraPosition = camera.transform.position;
            var maxSqr = maxDistance * maxDistance;
            var lod0Sqr = lod0Distance * lod0Distance;
            var lod0Count = 0;
            var lod1Count = 0;

            for (var i = 0; i < m_Chunks.Length; i++)
            {
                var bounds = m_ChunkBounds[i];
                var distanceSqr = bounds.SqrDistance(cameraPosition);
                if (distanceSqr > maxSqr || !GeometryUtility.TestPlanesAABB(m_Planes, bounds))
                    continue;

                if (distanceSqr < lod0Sqr || lod1 == null)
                {
                    m_Lod0Keys[lod0Count] = distanceSqr;
                    m_Lod0Matrices[lod0Count++] = m_Chunks[i];
                }
                else
                {
                    m_Lod1Keys[lod1Count] = distanceSqr;
                    m_Lod1Matrices[lod1Count++] = m_Chunks[i];
                }
            }

            // спереди назад, чтобы early-z отбрасывал закрытые травинки
            Array.Sort(m_Lod0Keys, m_Lod0Matrices, 0, lod0Count);
            Array.Sort(m_Lod1Keys, m_Lod1Matrices, 0, lod1Count);

            if (lod0Count > 0)
                Graphics.RenderMeshInstanced(m_RenderParams, lod0, 0, m_Lod0Matrices, lod0Count);
            if (lod1Count > 0)
                Graphics.RenderMeshInstanced(m_RenderParams, lod1, 0, m_Lod1Matrices, lod1Count);

            VisibleLod0 = lod0Count;
            VisibleLod1 = lod1Count;
        }
    }
}
