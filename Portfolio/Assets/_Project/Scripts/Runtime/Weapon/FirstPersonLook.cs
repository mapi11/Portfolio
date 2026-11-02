using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.Weapon
{
    public class FirstPersonLook : MonoBehaviour
    {
        public float mouseSensitivity = 0.12f;
        public float touchSensitivity = 0.15f;
        public Vector2 pitchLimits = new(-60f, 60f);
        public float recoilReturnSpeed = 10f;

        public float Yaw { get; set; }
        public float Pitch { get; set; }

        Vector2 m_Recoil;

        void Start()
        {
            var euler = transform.localEulerAngles;
            Yaw = euler.y;
            Pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        public void AddRecoil(float pitch, float yaw) => m_Recoil += new Vector2(pitch, yaw);

        void Update()
        {
            var look = Vector2.zero;
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                look = Mouse.current.delta.ReadValue() * mouseSensitivity;
            else if (Touchscreen.current != null && Touchscreen.current.touches.Count > 1 && Touchscreen.current.touches[1].isInProgress)
                look = Touchscreen.current.touches[1].delta.ReadValue() * touchSensitivity;

            Yaw += look.x;
            Pitch = Mathf.Clamp(Pitch - look.y, pitchLimits.x, pitchLimits.y);

            m_Recoil = Vector2.Lerp(m_Recoil, Vector2.zero, 1f - Mathf.Exp(-recoilReturnSpeed * Time.deltaTime));
            transform.localRotation = Quaternion.Euler(Pitch - m_Recoil.x, Yaw + m_Recoil.y, 0f);
        }
    }
}
