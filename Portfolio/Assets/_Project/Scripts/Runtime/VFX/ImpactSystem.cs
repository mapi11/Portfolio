using System.Collections.Generic;
using UnityEngine;

namespace Portfolio.VFX
{
    public class ImpactSystem : MonoBehaviour
    {
        public ImpactLibrary library;
        public DecalSystem decals;

        readonly Dictionary<SurfaceType, BurstEffect> m_Effects = new();

        void OnEnable()
        {
            if (m_Effects.Count > 0)
                return;
            // после перекомпиляции в Play Mode словарь пустой, а копии эффектов остались дочерними объектами
            foreach (var old in GetComponentsInChildren<BurstEffect>(true))
                Destroy(old.gameObject);
            foreach (var entry in library.entries)
                if (entry.effect != null && !m_Effects.ContainsKey(entry.surface))
                    m_Effects.Add(entry.surface, Instantiate(entry.effect, transform));
        }

        public void Spawn(in RaycastHit hit)
        {
            var surface = Surface.Of(hit.collider);
            var entry = library.Get(surface);
            if (entry == null)
                return;

            if (m_Effects.TryGetValue(entry.surface, out var effect))
                effect.EmitAt(hit.point, Quaternion.LookRotation(hit.normal));

            if (decals != null)
                decals.Add(entry.decal, hit.point, hit.normal, Random.Range(entry.decalSize.x, entry.decalSize.y));
        }
    }
}
