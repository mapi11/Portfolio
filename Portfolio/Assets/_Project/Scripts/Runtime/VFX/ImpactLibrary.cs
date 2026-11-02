using System;
using UnityEngine;

namespace Portfolio.VFX
{
    [CreateAssetMenu(menuName = "Portfolio/Impact Library", fileName = "ImpactLibrary")]
    public class ImpactLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public SurfaceType surface;
            public BurstEffect effect;
            public Material decal;
            public Vector2 decalSize = new(0.07f, 0.1f);
        }

        public Entry[] entries = Array.Empty<Entry>();

        public Entry Get(SurfaceType surface)
        {
            Entry fallback = null;
            foreach (var entry in entries)
            {
                if (entry.surface == surface)
                    return entry;
                if (entry.surface == SurfaceType.Default)
                    fallback = entry;
            }
            return fallback;
        }
    }
}
