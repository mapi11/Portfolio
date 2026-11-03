using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.LodDemo
{
    public class LODDemoCamera : MonoBehaviour
    {
        public Vector3 target = new(0f, 2f, 0f);
        public float nearDistance = 22f;
        public float farDistance = 420f;
        public float height = 2.5f;
        [Tooltip("Секунд на отъезд от ближней точки до дальней")]
        public float travelTime = 16f;
        public float pauseAtEnds = 1.5f;

        float m_Time;
        bool m_Paused;

        public float Distance { get; private set; }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                m_Paused = !m_Paused;
            if (!m_Paused)
                m_Time += Time.deltaTime;

            // туда-обратно с паузами на концах; по дистанции — экспонента, чтобы вблизи камера не пролетала
            var cycle = (travelTime + pauseAtEnds) * 2f;
            var t = m_Time % cycle;
            float k;
            if (t < pauseAtEnds) k = 0f;
            else if (t < pauseAtEnds + travelTime) k = (t - pauseAtEnds) / travelTime;
            else if (t < pauseAtEnds * 2f + travelTime) k = 1f;
            else k = 1f - (t - pauseAtEnds * 2f - travelTime) / travelTime;
            k = Mathf.SmoothStep(0f, 1f, k);

            Distance = nearDistance * Mathf.Pow(farDistance / nearDistance, k);
            var position = target + new Vector3(0f, height - target.y + Distance * 0.12f, -Distance);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        }
    }
}
