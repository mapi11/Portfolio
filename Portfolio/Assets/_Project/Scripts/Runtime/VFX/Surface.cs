using UnityEngine;

namespace Portfolio.VFX
{
    public enum SurfaceType { Default, Metal, Concrete, Wood }

    [DisallowMultipleComponent]
    public class Surface : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Concrete;

        public static SurfaceType Of(Collider collider) =>
            collider != null && collider.TryGetComponent<Surface>(out var surface) ? surface.type : SurfaceType.Default;
    }
}
