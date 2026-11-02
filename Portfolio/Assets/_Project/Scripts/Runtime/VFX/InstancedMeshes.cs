using UnityEngine;

namespace Portfolio.VFX
{
    static class InstancedMeshes
    {
        static Mesh s_Decal;
        static Mesh s_Tracer;

        /// <summary>Квад в плоскости XY, лицевая сторона смотрит в -Z (как у встроенного Quad).</summary>
        public static Mesh Decal => s_Decal ??= Create("Decal",
            new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) });

        /// <summary>Квад в плоскости XZ от z=0 до z=1: растягивается по длине трассера.</summary>
        public static Mesh Tracer => s_Tracer ??= Create("Tracer",
            new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(-0.5f, 0, 1), new Vector3(0.5f, 0, 1) });

        static Mesh Create(string name, Vector3[] vertices)
        {
            var mesh = new Mesh { name = name, vertices = vertices };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
