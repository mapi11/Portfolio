using UnityEngine;

namespace Portfolio.Editor.Showcase
{
    // Пропсы тира: узнаваемая форма без мелких деталей. Пивот у всех внизу по центру
    static class RangePropMeshes
    {
        // Сабмеши: 0 — доски, 1 — рама и тёмная основа в щелях
        public static void Crate(Mesh mesh, float size)
        {
            var b = new PropMeshBuilder(2);
            var half = size * 0.5f;
            var frame = size * 0.09f;
            var center = Vector3.up * half;

            b.Box(1, center, Vector3.one * (size - 0.06f));

            // три доски на каждой грани, между ними щели до тёмной основы
            var axes = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var normal in axes)
            {
                var up = Mathf.Abs(normal.y) > 0.5f ? Vector3.forward : Vector3.up;
                var rotation = Quaternion.LookRotation(normal, up);
                var plank = (size - 0.06f) / 3f;
                for (var k = 0; k < 3; k++)
                {
                    var position = center + normal * (half - 0.025f) + up * ((k - 1) * plank);
                    b.Box(0, Matrix4x4.TRS(position, rotation, Vector3.one), new Vector3(size - frame * 2f, plank - 0.012f, 0.03f));
                }
            }

            // рама по двенадцати рёбрам
            var edge = half - frame * 0.5f + 0.008f;
            for (var a = -1; a <= 1; a += 2)
            for (var c = -1; c <= 1; c += 2)
            {
                b.Box(1, center + new Vector3(0f, a * edge, c * edge), new Vector3(size + 0.016f, frame, frame));
                b.Box(1, center + new Vector3(a * edge, 0f, c * edge), new Vector3(frame, size + 0.016f, frame));
                b.Box(1, center + new Vector3(a * edge, c * edge, 0f), new Vector3(frame, frame, size + 0.016f));
            }

            // диагональные укосины на боковых гранях
            var inner = size - frame * 2f;
            for (var i = 0; i < 4; i++)
            {
                var normal = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                var rotation = Quaternion.LookRotation(normal, Vector3.up) * Quaternion.Euler(0f, 0f, i % 2 == 0 ? 45f : -45f);
                b.Box(1, Matrix4x4.TRS(center + normal * (half + 0.005f), rotation, Vector3.one),
                    new Vector3(inner * Mathf.Sqrt(2f) - frame * 1.6f, frame * 0.8f, 0.03f));
            }
            b.Apply(mesh);
        }

        // 20-футовый морской контейнер вдоль Z. Сабмеши: 0 — стенки с гофрой, 1 — рама, 2 — запорные штанги
        public static void ShippingContainer(Mesh mesh)
        {
            const float length = 6.06f, width = 2.44f, height = 2.59f;
            var b = new PropMeshBuilder(3);
            var hw = width * 0.5f;
            var hl = length * 0.5f;

            b.Box(0, new Vector3(0f, height * 0.5f, 0f), new Vector3(width - 0.12f, height - 0.1f, length - 0.12f));

            // гофра на длинных стенках
            for (var z = -hl + 0.3f; z < hl - 0.2f; z += 0.28f)
                for (var side = -1; side <= 1; side += 2)
                    b.Box(0, new Vector3(side * (hw - 0.04f), height * 0.5f, z), new Vector3(0.05f, height - 0.34f, 0.14f));
            // гофра на крыше и торцевой стенке
            for (var z = -hl + 0.3f; z < hl - 0.2f; z += 0.4f)
                b.Box(0, new Vector3(0f, height - 0.04f, z), new Vector3(width - 0.3f, 0.03f, 0.16f));
            for (var y = 0.35f; y < height - 0.25f; y += 0.28f)
                b.Box(0, new Vector3(0f, y, -hl + 0.04f), new Vector3(width - 0.34f, 0.14f, 0.05f));

            // двери на торце: две створки и четыре запорные штанги
            for (var side = -1; side <= 1; side += 2)
                b.Box(0, new Vector3(side * (width * 0.25f - 0.02f), height * 0.5f, hl - 0.04f), new Vector3(width * 0.5f - 0.18f, height - 0.34f, 0.05f));
            foreach (var x in new[] { -0.85f, -0.3f, 0.3f, 0.85f })
            {
                b.Cylinder(2, Matrix4x4.Translate(new Vector3(x, height * 0.5f, hl + 0.01f)), 0.022f, height - 0.4f, 8);
                b.Box(2, new Vector3(x + 0.07f, height * 0.45f, hl + 0.04f), new Vector3(0.16f, 0.04f, 0.03f));
            }

            // рама: стойки, верхние и нижние балки, угловые фитинги
            const float beam = 0.16f;
            for (var sx = -1; sx <= 1; sx += 2)
            {
                for (var sz = -1; sz <= 1; sz += 2)
                {
                    b.Box(1, new Vector3(sx * (hw - beam * 0.5f), height * 0.5f, sz * (hl - beam * 0.5f)), new Vector3(beam, height, beam));
                    for (var sy = 0; sy <= 1; sy++)
                        b.Box(1, new Vector3(sx * (hw - 0.09f), sy == 0 ? 0.07f : height - 0.07f, sz * (hl - 0.09f)), new Vector3(0.2f, 0.14f, 0.2f));
                }
                b.Box(1, new Vector3(sx * (hw - beam * 0.5f), beam * 0.5f, 0f), new Vector3(beam, beam, length));
                b.Box(1, new Vector3(sx * (hw - beam * 0.5f), height - beam * 0.5f, 0f), new Vector3(beam, beam, length));
            }
            for (var sz = -1; sz <= 1; sz += 2)
            {
                b.Box(1, new Vector3(0f, 0.12f, sz * (hl - beam * 0.5f)), new Vector3(width, 0.24f, beam));
                b.Box(1, new Vector3(0f, height - 0.1f, sz * (hl - beam * 0.5f)), new Vector3(width, 0.2f, beam));
            }
            b.Apply(mesh);
        }

        // Стальная бочка на 200 л. Сабмеши: 0 — корпус, 1 — обручи и закатки
        public static void Drum(Mesh mesh)
        {
            const float radius = 0.29f, height = 0.88f;
            const int segments = 28;
            var b = new PropMeshBuilder(2);
            b.Cylinder(0, Matrix4x4.Translate(Vector3.up * height * 0.5f), radius, height - 0.01f, segments);
            foreach (var y in new[] { 0.015f, height - 0.015f })
                b.Cylinder(1, Matrix4x4.Translate(Vector3.up * y), radius + 0.008f, 0.03f, segments, caps: false);
            foreach (var y in new[] { height / 3f, height * 2f / 3f })
                b.Cylinder(1, Matrix4x4.Translate(Vector3.up * y), radius + 0.012f, 0.035f, segments, caps: false);
            b.Cylinder(1, Matrix4x4.Translate(new Vector3(0.16f, height + 0.005f, 0.05f)), 0.03f, 0.02f, 10);
            b.Cylinder(1, Matrix4x4.Translate(new Vector3(-0.17f, height + 0.005f, -0.02f)), 0.02f, 0.02f, 10);
            b.Apply(mesh);
        }

        // Бетонный отбойник «нью-джерси», вдоль X
        public static void JerseyBarrier(Mesh mesh, float length)
        {
            var b = new PropMeshBuilder(1);
            var profile = new[]
            {
                new Vector2(-0.3f, 0f), new Vector2(0.3f, 0f), new Vector2(0.3f, 0.08f), new Vector2(0.13f, 0.33f),
                new Vector2(0.08f, 0.81f), new Vector2(-0.08f, 0.81f), new Vector2(-0.13f, 0.33f), new Vector2(-0.3f, 0.08f)
            };
            b.Extrude(0, Matrix4x4.identity, profile, length);
            b.Apply(mesh);
        }

        // Бетонный блок с фаской по вертикальным рёбрам
        public static void ConcretePillar(Mesh mesh, float size, float height)
        {
            var b = new PropMeshBuilder(1);
            var h = size * 0.5f;
            const float chamfer = 0.07f;
            var profile = new[]
            {
                new Vector2(-h + chamfer, -h), new Vector2(h - chamfer, -h), new Vector2(h, -h + chamfer), new Vector2(h, h - chamfer),
                new Vector2(h - chamfer, h), new Vector2(-h + chamfer, h), new Vector2(-h, h - chamfer), new Vector2(-h, -h + chamfer)
            };
            // профиль в ZY вытягивается по X, поворот ставит вытяжку вертикально
            b.Extrude(0, Matrix4x4.TRS(Vector3.up * height * 0.5f, Quaternion.Euler(0f, 0f, 90f), Vector3.one), profile, height);
            b.Apply(mesh);
        }

        // Стальная мишень на стойке. Сабмеши: 0 — пластина, 1 — стойка
        public static void SteelTarget(Mesh mesh, float width, float height, float bottom)
        {
            var b = new PropMeshBuilder(2);
            b.Box(0, new Vector3(0f, bottom + height * 0.5f, 0f), new Vector3(width, height, 0.03f));
            for (var side = -1; side <= 1; side += 2)
            {
                var x = side * (width * 0.5f - 0.12f);
                b.Box(1, new Vector3(x, (bottom + 0.15f) * 0.5f, -0.04f), new Vector3(0.05f, bottom + 0.15f, 0.05f));
                b.Box(1, new Vector3(x, 0.025f, -0.04f), new Vector3(0.07f, 0.05f, 0.7f));
            }
            b.Box(1, new Vector3(0f, bottom - 0.05f, -0.04f), new Vector3(width - 0.2f, 0.05f, 0.05f));
            b.Apply(mesh);
        }

        // Щит из вертикальных досок на двух столбах. Сабмеши: 0 — доски, 1 — столбы и поперечины
        public static void PlankWall(Mesh mesh, float width, float height)
        {
            var b = new PropMeshBuilder(2);
            const float plank = 0.2f, gap = 0.015f;
            var count = Mathf.FloorToInt(width / plank);
            var start = -count * plank * 0.5f + plank * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var jitter = Mathf.Sin(i * 12.9898f) * 0.03f;
                b.Box(0, new Vector3(start + i * plank, 0.1f + (height + jitter) * 0.5f, 0f), new Vector3(plank - gap, height + jitter, 0.035f));
            }
            foreach (var y in new[] { 0.45f, height - 0.25f })
                b.Box(1, new Vector3(0f, y, -0.045f), new Vector3(count * plank, 0.12f, 0.05f));
            for (var side = -1; side <= 1; side += 2)
                b.Box(1, new Vector3(side * (count * plank * 0.5f + 0.05f), (height + 0.2f) * 0.5f, -0.03f), new Vector3(0.1f, height + 0.2f, 0.1f));
            b.Apply(mesh);
        }
    }
}
