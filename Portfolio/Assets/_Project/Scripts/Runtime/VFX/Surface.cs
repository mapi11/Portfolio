using UnityEngine;

namespace Portfolio.VFX
{
    public enum SurfaceType { Default, Metal, Concrete, Wood }

    /// <summary>
    /// Тип поверхности для выбора эффекта попадания. Висит на объекте с коллайдером;
    /// без компонента используется <see cref="SurfaceType.Default"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class Surface : MonoBehaviour
    {
        public SurfaceType type = SurfaceType.Concrete;

        public static SurfaceType Of(Collider collider) =>
            collider != null && collider.TryGetComponent<Surface>(out var surface) ? surface.type : SurfaceType.Default;
    }
}
