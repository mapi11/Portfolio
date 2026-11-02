using System;
using UnityEngine;

namespace Portfolio.VFX
{
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
