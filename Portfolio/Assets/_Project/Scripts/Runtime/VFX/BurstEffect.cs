using System;
using UnityEngine;

namespace Portfolio.VFX
{
    /// <summary>
    /// Эффект из нескольких систем частиц, который запускается через Emit, а не Play.
    /// Emission у систем выключен: количество частиц на событие задаётся здесь явно
    /// и укладывается в бюджет из Docs/Budgets.md.
    ///
    /// Частицы симулируются в мировом пространстве, поэтому на каждую поверхность
    /// достаточно одного экземпляра: его переносят в точку попадания и делают Emit,
    /// а частицы прошлых попаданий остаются на месте. Без Instantiate во время стрельбы.
    /// </summary>
    public class BurstEffect : MonoBehaviour
    {
        [Serializable]
        public struct Part
        {
            public ParticleSystem system;
            public int min;
            public int max;
        }

        public Part[] parts = Array.Empty<Part>();

        public void Emit()
        {
            foreach (var part in parts)
                if (part.system != null)
                    part.system.Emit(UnityEngine.Random.Range(part.min, part.max + 1));
        }

        public void EmitAt(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            Emit();
        }

        public int MaxParticlesPerBurst
        {
            get
            {
                var total = 0;
                foreach (var part in parts)
                    total += part.max;
                return total;
            }
        }
    }
}
