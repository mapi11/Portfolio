using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.VFX
{
    /// <summary>
    /// Гильзы без Rigidbody: простая баллистика с одним рейкастом на гильзу за кадр
    /// и отскоком от поверхности. Отрисовка — один instanced draw call.
    /// </summary>
    public class ShellSystem : MonoBehaviour
    {
        public Mesh mesh;
        public Material material;
        public int capacity = 24;
        public float lifetime = 2.5f;
        [Tooltip("Для встроенного Cylinder (высота 2, диаметр 1): гильза 5.56 — 9.5 x 45 мм")]
        public Vector3 scale = new(0.0095f, 0.0225f, 0.0095f);
        public Vector2 ejectSpeed = new(1.6f, 2.4f);
        [Range(0f, 1f)] public float bounce = 0.35f;
        public LayerMask collisionMask = ~0;

        struct Shell
        {
            public Vector3 position;
            public Vector3 velocity;
            public Quaternion rotation;
            public Vector3 angularVelocity;
            public float age;
            public bool resting;
        }

        Shell[] m_Shells;
        Matrix4x4[] m_Matrices;
        int m_Next;
        int m_Count;
        RenderParams m_RenderParams;

        void Awake()
        {
            m_Shells = new Shell[capacity];
            m_Matrices = new Matrix4x4[capacity];
            m_RenderParams = new RenderParams(material)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f)
            };
        }

        /// <param name="socket">socket_shell_eject: +X — направление выброса, +Z — вдоль ствола.</param>
        public void Eject(Transform socket, Vector3 inheritedVelocity)
        {
            // Вправо-вверх и чуть вперёд: гильза уходит из кадра, а не летит в камеру.
            var direction = (socket.right + socket.up * 0.5f + socket.forward * Random.Range(0.2f, 0.5f)).normalized;
            m_Shells[m_Next] = new Shell
            {
                position = socket.position,
                velocity = inheritedVelocity + direction * Random.Range(ejectSpeed.x, ejectSpeed.y),
                rotation = socket.rotation * Quaternion.Euler(90f, 0f, 0f),
                angularVelocity = Random.insideUnitSphere * 1500f + socket.up * 900f
            };
            m_Next = (m_Next + 1) % m_Shells.Length;
            m_Count = Mathf.Min(m_Count + 1, m_Shells.Length);
        }

        void Update()
        {
            var dt = Time.deltaTime;
            var gravity = Physics.gravity;
            var visible = 0;

            for (var i = 0; i < m_Count; i++)
            {
                ref var shell = ref m_Shells[i];
                shell.age += dt;
                if (shell.age > lifetime)
                    continue;

                if (!shell.resting)
                {
                    shell.velocity += gravity * dt;
                    var move = shell.velocity * dt;

                    if (Physics.Raycast(shell.position, move, out var hit, move.magnitude + 0.005f, collisionMask, QueryTriggerInteraction.Ignore))
                    {
                        shell.position = hit.point + hit.normal * 0.004f;
                        shell.velocity = Vector3.Reflect(shell.velocity, hit.normal) * bounce;
                        shell.angularVelocity *= 0.5f;
                        if (shell.velocity.sqrMagnitude < 0.25f)
                        {
                            // Укладываем гильзу на бок: ось цилиндра (локальная Y) — вдоль поверхности.
                            shell.resting = true;
                            var along = Vector3.ProjectOnPlane(shell.rotation * Vector3.up, hit.normal);
                            if (along.sqrMagnitude < 1e-4f)
                                along = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
                            shell.rotation = Quaternion.LookRotation(hit.normal, along);
                            shell.position = hit.point + hit.normal * scale.x * 0.5f;
                        }
                    }
                    else
                    {
                        shell.position += move;
                    }

                    shell.rotation = Quaternion.Euler(shell.angularVelocity * dt) * shell.rotation;
                }

                m_Matrices[visible++] = Matrix4x4.TRS(shell.position, shell.rotation, scale);
            }

            if (visible > 0)
                Graphics.RenderMeshInstanced(m_RenderParams, mesh, 0, m_Matrices, visible);
        }
    }
}
