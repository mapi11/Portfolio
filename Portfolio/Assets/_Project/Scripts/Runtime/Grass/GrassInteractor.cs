using UnityEngine;

namespace Portfolio.Grass
{
    public class GrassInteractor : MonoBehaviour
    {
        public float radius = 0.9f;

        [Header("Демо: движение по кругу")]
        public bool orbit = true;
        public Vector3 orbitCenter;
        public float orbitRadius = 4f;
        public float orbitSpeed = 0.35f;

        static readonly int s_Interactor = Shader.PropertyToID("_GrassInteractor");

        void Update()
        {
            if (orbit)
            {
                var angle = Time.time * orbitSpeed;
                var previous = transform.position;
                var position = orbitCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;
                position.y = transform.position.y;
                transform.position = position;

                var delta = position - previous;
                var size = transform.lossyScale.x * 0.5f;
                if (delta.sqrMagnitude > 1e-8f && size > 0f)
                    transform.Rotate(Vector3.Cross(Vector3.up, delta.normalized), delta.magnitude / size * Mathf.Rad2Deg, Space.World);
            }

            var p = transform.position;
            Shader.SetGlobalVector(s_Interactor, new Vector4(p.x, p.y, p.z, radius));
        }

        void OnDisable() => Shader.SetGlobalVector(s_Interactor, new Vector4(0f, -1000f, 0f, 0f));
    }
}
