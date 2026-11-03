using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

namespace Portfolio.Weapon
{
    public class WeaponAnimator : MonoBehaviour
    {
        public Animator animator;
        public WeaponController weapon;
        public TwoBoneIKConstraint leftHandIK;
        public AnimationClip reloadClip;
        public int magazineSize = 30;
        public float ikBlendSpeed = 12f;

        static readonly int s_Reload = Animator.StringToHash("Reload");

        int m_Ammo;
        float m_ReloadTimer;

        public int Ammo => m_Ammo;
        public bool Reloading => m_ReloadTimer > 0f;

        void Start() => m_Ammo = magazineSize;

        void OnEnable()
        {
            if (weapon != null)
                weapon.Fired += OnFired;
        }

        void OnDisable()
        {
            if (weapon != null)
                weapon.Fired -= OnFired;
        }

        void OnFired()
        {
            if (--m_Ammo <= 0)
                Reload();
        }

        public void Reload()
        {
            if (Reloading || m_Ammo == magazineSize)
                return;
            animator.SetTrigger(s_Reload);
            m_ReloadTimer = reloadClip != null ? reloadClip.length : 2f;
            weapon.Blocked = true;
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                Reload();

            if (Reloading)
            {
                m_ReloadTimer -= Time.deltaTime;
                if (m_ReloadTimer <= 0f)
                {
                    m_Ammo = magazineSize;
                    weapon.Blocked = false;
                }
            }

            // на перезарядке левая рука уходит к магазину, IK к цевью на это время гасим
            if (leftHandIK != null)
            {
                var target = Reloading ? 0f : 1f;
                leftHandIK.weight = Mathf.MoveTowards(leftHandIK.weight, target, ikBlendSpeed * Time.deltaTime);
            }
        }
    }
}
