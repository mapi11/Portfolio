using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.VFX
{
    public class TracerSystem : MonoBehaviour
    {
        public Material material;
        public int capacity = 32;
        public float speed = 350f;
        public float length = 4f;
        public float width = 0.025f;

        struct Tracer
        {
            public Vector3 start;
            public Vector3 direction;
            public float distance;
            public float travelled;
        }

        Tracer[] m_Tracers;
        Matrix4x4[] m_Matrices;
        int m_Count;
        RenderParams m_RenderParams;
        Camera m_Camera;

        void OnEnable()
        {
            if (m_Tracers == null)
            {
                m_Tracers = new Tracer[capacity];
                m_Matrices = new Matrix4x4[capacity];
                m_Count = 0;
            }
            m_RenderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f)
            };
        }

        public void Fire(Vector3 from, Vector3 to)
        {
            if (m_Count == m_Tracers.Length)
                return;

            var delta = to - from;
            m_Tracers[m_Count++] = new Tracer
            {
                start = from,
                direction = delta.normalized,
                distance = delta.magnitude
            };
        }

        void LateUpdate()
        {
            if (m_Count == 0)
                return;

            if (m_Camera == null)
                m_Camera = Camera.main;
            var cameraPosition = m_Camera != null ? m_Camera.transform.position : Vector3.zero;
            var step = speed * Time.deltaTime;

            for (var i = m_Count - 1; i >= 0; i--)
            {
                ref var tracer = ref m_Tracers[i];
                tracer.travelled += step;

                var tail = Mathf.Max(0f, tracer.travelled - length);
                if (tail >= tracer.distance)
                {
                    // идём с конца, последний уже обработан в этом кадре, забираем и его матрицу
                    m_Count--;
                    m_Tracers[i] = m_Tracers[m_Count];
                    m_Matrices[i] = m_Matrices[m_Count];
                    continue;
                }

                var head = Mathf.Min(tracer.travelled, tracer.distance);
                var tailPosition = tracer.start + tracer.direction * tail;

                var toCamera = cameraPosition - tailPosition;
                var up = toCamera - tracer.direction * Vector3.Dot(toCamera, tracer.direction);
                if (up.sqrMagnitude < 1e-6f)
                    up = Vector3.up;

                m_Matrices[i] = Matrix4x4.TRS(tailPosition,
                    Quaternion.LookRotation(tracer.direction, up),
                    new Vector3(width, 1f, head - tail));
            }

            if (m_Count > 0)
                Graphics.RenderMeshInstanced(m_RenderParams, InstancedMeshes.Tracer, 0, m_Matrices, m_Count);
        }
    }
}
