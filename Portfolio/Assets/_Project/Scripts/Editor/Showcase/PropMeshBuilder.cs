using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Portfolio.Editor.Showcase
{
    // Собирает меш пропа из простых тел. Каждое тело кладётся в свой сабмеш (= свой материал),
    // UV у граней в метрах, поэтому сетка и текстуры не растягиваются на телах разного размера
    class PropMeshBuilder
    {
        readonly List<Vector3> m_Vertices = new();
        readonly List<Vector3> m_Normals = new();
        readonly List<Vector2> m_Uvs = new();
        readonly List<int>[] m_Submeshes;

        public int VertexCount => m_Vertices.Count;

        public PropMeshBuilder(int submeshes)
        {
            m_Submeshes = new List<int>[submeshes];
            for (var i = 0; i < submeshes; i++)
                m_Submeshes[i] = new List<int>();
        }

        public void Box(int submesh, Vector3 center, Vector3 size) => Box(submesh, Matrix4x4.TRS(center, Quaternion.identity, Vector3.one), size);

        public void Box(int submesh, Matrix4x4 transform, Vector3 size)
        {
            var h = size * 0.5f;
            Face(submesh, transform, Vector3.right, Vector3.forward, Vector3.up, h.x, size.z, size.y);
            Face(submesh, transform, Vector3.left, Vector3.back, Vector3.up, h.x, size.z, size.y);
            Face(submesh, transform, Vector3.up, Vector3.right, Vector3.forward, h.y, size.x, size.z);
            Face(submesh, transform, Vector3.down, Vector3.right, Vector3.back, h.y, size.x, size.z);
            Face(submesh, transform, Vector3.forward, Vector3.left, Vector3.up, h.z, size.x, size.y);
            Face(submesh, transform, Vector3.back, Vector3.right, Vector3.up, h.z, size.x, size.y);
        }

        // Грань коробки: normal — наружу, u и v — оси грани, distance — расстояние от центра
        void Face(int submesh, Matrix4x4 transform, Vector3 normal, Vector3 u, Vector3 v, float distance, float width, float height)
        {
            var start = m_Vertices.Count;
            var center = normal * distance;
            for (var j = 0; j < 2; j++)
            for (var i = 0; i < 2; i++)
            {
                var local = center + u * ((i - 0.5f) * width) + v * ((j - 0.5f) * height);
                m_Vertices.Add(transform.MultiplyPoint3x4(local));
                m_Normals.Add(transform.MultiplyVector(normal).normalized);
                m_Uvs.Add(new Vector2(i * width, j * height));
            }
            m_Submeshes[submesh].AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
        }

        // Цилиндр вдоль локальной Y с крышками
        public void Cylinder(int submesh, Matrix4x4 transform, float radius, float height, int segments, bool caps = true)
        {
            var start = m_Vertices.Count;
            var circumference = Mathf.PI * 2f * radius;
            for (var s = 0; s <= segments; s++)
            {
                var angle = (float)s / segments * Mathf.PI * 2f;
                var normal = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                for (var j = 0; j < 2; j++)
                {
                    m_Vertices.Add(transform.MultiplyPoint3x4(normal * radius + Vector3.up * ((j - 0.5f) * height)));
                    m_Normals.Add(transform.MultiplyVector(normal).normalized);
                    m_Uvs.Add(new Vector2((float)s / segments * circumference, j * height));
                }
            }
            for (var s = 0; s < segments; s++)
            {
                var a = start + s * 2;
                m_Submeshes[submesh].AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            }
            if (!caps)
                return;
            for (var side = -1; side <= 1; side += 2)
            {
                var center = m_Vertices.Count;
                var normal = transform.MultiplyVector(Vector3.up * side).normalized;
                m_Vertices.Add(transform.MultiplyPoint3x4(Vector3.up * (side * height * 0.5f)));
                m_Normals.Add(normal);
                m_Uvs.Add(Vector2.zero);
                for (var s = 0; s <= segments; s++)
                {
                    var angle = (float)s / segments * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    m_Vertices.Add(transform.MultiplyPoint3x4(p + Vector3.up * (side * height * 0.5f)));
                    m_Normals.Add(normal);
                    m_Uvs.Add(new Vector2(p.x, p.z));
                }
                for (var s = 0; s < segments; s++)
                    m_Submeshes[submesh].AddRange(side > 0
                        ? new[] { center, center + s + 2, center + s + 1 }
                        : new[] { center, center + s + 1, center + s + 2 });
            }
        }

        // UV-сфера: кольца по широте, сегменты по долготе
        public void Sphere(int submesh, Vector3 center, float radius, int segments, int rings)
        {
            var start = m_Vertices.Count;
            for (var r = 0; r <= rings; r++)
            {
                var latitude = Mathf.PI * r / rings;
                for (var s = 0; s <= segments; s++)
                {
                    var longitude = Mathf.PI * 2f * s / segments;
                    var normal = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(longitude));
                    m_Vertices.Add(center + normal * radius);
                    m_Normals.Add(normal);
                    m_Uvs.Add(new Vector2((float)s / segments, 1f - (float)r / rings));
                }
            }
            for (var r = 0; r < rings; r++)
            for (var s = 0; s < segments; s++)
            {
                var a = start + r * (segments + 1) + s;
                var b = a + segments + 1;
                m_Submeshes[submesh].AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
            }
        }

        // Выпуклый профиль в плоскости ZY, вытянутый вдоль локальной X на length
        public void Extrude(int submesh, Matrix4x4 transform, Vector2[] profile, float length)
        {
            var n = profile.Length;
            var perimeter = 0f;
            for (var i = 0; i < n; i++)
            {
                var a = profile[i];
                var b = profile[(i + 1) % n];
                var start = m_Vertices.Count;
                var edge = b - a;
                var normal = transform.MultiplyVector(new Vector3(0f, -edge.x, edge.y)).normalized;
                for (var j = 0; j < 2; j++)
                {
                    var x = (j - 0.5f) * length;
                    m_Vertices.Add(transform.MultiplyPoint3x4(new Vector3(x, a.y, a.x)));
                    m_Vertices.Add(transform.MultiplyPoint3x4(new Vector3(x, b.y, b.x)));
                    m_Normals.Add(normal);
                    m_Normals.Add(normal);
                    m_Uvs.Add(new Vector2(j * length, perimeter));
                    m_Uvs.Add(new Vector2(j * length, perimeter + edge.magnitude));
                }
                perimeter += edge.magnitude;
                m_Submeshes[submesh].AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }

            for (var side = -1; side <= 1; side += 2)
            {
                var start = m_Vertices.Count;
                var normal = transform.MultiplyVector(Vector3.right * side).normalized;
                foreach (var p in profile)
                {
                    m_Vertices.Add(transform.MultiplyPoint3x4(new Vector3(side * length * 0.5f, p.y, p.x)));
                    m_Normals.Add(normal);
                    m_Uvs.Add(new Vector2(p.x, p.y));
                }
                for (var i = 1; i < n - 1; i++)
                    m_Submeshes[submesh].AddRange(side > 0
                        ? new[] { start, start + i + 1, start + i }
                        : new[] { start, start + i, start + i + 1 });
            }
        }

        public void Apply(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = m_Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(m_Vertices);
            mesh.SetNormals(m_Normals);
            mesh.SetUVs(0, m_Uvs);
            mesh.subMeshCount = m_Submeshes.Length;
            for (var i = 0; i < m_Submeshes.Length; i++)
                mesh.SetTriangles(m_Submeshes[i], i);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }
    }
}
