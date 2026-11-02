using Portfolio.VFX;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Portfolio.Weapon
{
    /// <summary>
    /// Автоматическая стрельба хитсканом. Оружие только сообщает о событиях
    /// (выстрел, попадание) системам VFX, сами эффекты настраиваются в их ассетах.
    /// Сокеты называются как в Docs/RigRequirements.md, поэтому заглушку можно
    /// заменить на модель с ригом без изменений в коде.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [Header("Сокеты")]
        public Transform muzzleSocket;
        public Transform shellSocket;
        [Tooltip("Модель, которая откатывается при отдаче")]
        public Transform kickRoot;

        [Header("Стрельба")]
        public Camera aimCamera;
        public float roundsPerMinute = 700f;
        public float range = 150f;
        public float spreadDegrees = 0.5f;
        [Tooltip("Трассер на каждый N-й выстрел, как в реальных магазинах")]
        public int tracerEvery = 2;
        public LayerMask hitMask = ~0;

        [Header("VFX")]
        public BurstEffect muzzleFlash;
        public ImpactSystem impacts;
        public TracerSystem tracers;
        public ShellSystem shells;

        [Header("Отдача")]
        public FirstPersonLook look;
        public Vector2 cameraKick = new(0.9f, 0.35f);
        public float kickBack = 0.035f;
        public float kickPitch = 3f;
        public float kickReturnSpeed = 20f;

        /// <summary>Внешнее удержание спуска (бенчмарк).</summary>
        public bool TriggerOverride { get; set; }
        public int ShotsFired { get; private set; }

        float m_Cooldown;
        float m_Kick;
        Vector3 m_KickRootPosition;
        Quaternion m_KickRootRotation;
        Vector3 m_LastPosition;

        void Start()
        {
            if (kickRoot != null)
            {
                m_KickRootPosition = kickRoot.localPosition;
                m_KickRootRotation = kickRoot.localRotation;
            }
            m_LastPosition = transform.position;
        }

        bool TriggerHeld =>
            TriggerOverride ||
            (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
            (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed);

        void Update()
        {
            m_Cooldown -= Time.deltaTime;
            if (TriggerHeld)
            {
                // Несколько выстрелов за кадр при низком FPS: темп стрельбы не зависит от кадра.
                // Предел на кадр — защита от пачки выстрелов после фриза (загрузка, пауза).
                for (var shots = 0; m_Cooldown <= 0f && shots < 3; shots++)
                {
                    Fire();
                    m_Cooldown += 60f / roundsPerMinute;
                }
                m_Cooldown = Mathf.Max(m_Cooldown, -60f / roundsPerMinute);
            }
            else if (m_Cooldown < 0f)
            {
                m_Cooldown = 0f;
            }

            UpdateKick();
        }

        void Fire()
        {
            ShotsFired++;
            var aim = aimCamera.transform;
            var spread = Random.insideUnitCircle * spreadDegrees;
            var direction = Quaternion.AngleAxis(spread.x, aim.up) * Quaternion.AngleAxis(spread.y, aim.right) * aim.forward;

            var end = aim.position + direction * range;
            if (Physics.Raycast(aim.position, direction, out var hit, range, hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                if (impacts != null)
                    impacts.Spawn(hit);
            }

            if (muzzleFlash != null)
                muzzleFlash.Emit();

            if (tracers != null && muzzleSocket != null && tracerEvery > 0 && ShotsFired % tracerEvery == 0)
                tracers.Fire(muzzleSocket.position, end);

            if (shells != null && shellSocket != null)
                shells.Eject(shellSocket, (transform.position - m_LastPosition) / Mathf.Max(Time.deltaTime, 1e-4f));

            m_Kick = 1f;
            if (look != null)
                look.AddRecoil(cameraKick.x, Random.Range(-cameraKick.y, cameraKick.y));
        }

        void UpdateKick()
        {
            m_LastPosition = transform.position;
            if (kickRoot == null)
                return;

            m_Kick = Mathf.Lerp(m_Kick, 0f, 1f - Mathf.Exp(-kickReturnSpeed * Time.deltaTime));
            kickRoot.localPosition = m_KickRootPosition + Vector3.back * (kickBack * m_Kick);
            kickRoot.localRotation = m_KickRootRotation * Quaternion.Euler(-kickPitch * m_Kick, 0f, 0f);
        }
    }
}
