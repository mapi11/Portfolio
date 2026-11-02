using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.Grass
{
    /// <summary>
    /// Камера для демо-сцен: медленно облетает точку, ЛКМ/палец — вращать, колесо — приблизить.
    /// </summary>
    public class OrbitCamera : MonoBehaviour
    {
        public Vector3 target = new(0f, 0.4f, 0f);
        public float distance = 9f;
        public Vector2 distanceLimits = new(2f, 25f);
        public float pitch = 14f;
        public Vector2 pitchLimits = new(3f, 60f);
        public float yaw;
        public float autoRotateSpeed = 6f;
        public float sensitivity = 0.2f;

        void Update()
        {
            var drag = Vector2.zero;
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.isPressed)
                    drag = Mouse.current.delta.ReadValue();
                distance -= Mouse.current.scroll.ReadValue().y * 0.01f;
            }
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                drag = Touchscreen.current.primaryTouch.delta.ReadValue();

            if (drag == Vector2.zero)
                yaw += autoRotateSpeed * Time.deltaTime;

            yaw += drag.x * sensitivity;
            pitch = Mathf.Clamp(pitch - drag.y * sensitivity, pitchLimits.x, pitchLimits.y);
            distance = Mathf.Clamp(distance, distanceLimits.x, distanceLimits.y);

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(target - rotation * Vector3.forward * distance, rotation);
        }
    }
}
