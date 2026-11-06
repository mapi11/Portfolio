using System.Collections.Generic;
using UnityEngine;

namespace Portfolio.Editor.Showcase
{
    // Тренировочный манекен ростом 1.8 м, пивот под ногами. Общий для сцен сканера и толпы.
    // Фигура собрана из жёстких частей, поэтому для анимации каждая вершина целиком принадлежит одной кости
    public static class MannequinMesh
    {
        public const int Hips = 0, Spine = 1, Head = 2, UpperArmL = 3, ForeArmL = 4, UpperArmR = 5, ForeArmR = 6,
            ThighL = 7, ShinL = 8, ThighR = 9, ShinR = 10;

        public static readonly string[] BoneNames =
        {
            "Hips", "Spine", "Head", "UpperArm.L", "ForeArm.L", "UpperArm.R", "ForeArm.R", "Thigh.L", "Shin.L", "Thigh.R", "Shin.R"
        };

        public static readonly int[] BoneParents = { -1, Hips, Spine, Spine, UpperArmL, Spine, UpperArmR, Hips, ThighL, Hips, ThighR };

        // положение кости в модели в позе привязки; повороты у всех костей нулевые
        public static readonly Vector3[] BonePositions =
        {
            new(0f, 0.9f, 0f), new(0f, 1.05f, 0f), new(0f, 1.55f, 0f),
            new(-0.22f, 1.46f, 0f), new(-0.27f, 1.18f, -0.02f), new(0.22f, 1.46f, 0f), new(0.27f, 1.18f, -0.02f),
            new(-0.1f, 0.9f, 0f), new(-0.11f, 0.48f, 0.02f), new(0.1f, 0.9f, 0f), new(0.11f, 0.48f, 0.02f)
        };

        public static void Build(Mesh mesh, bool skinned)
        {
            var b = new PropMeshBuilder(1);
            var bones = new List<int>();
            void Part(int bone)
            {
                while (bones.Count < b.VertexCount)
                    bones.Add(bone);
            }

            // торс из трёх частей: широкая грудь, узкая талия, таз — уже не «столб»
            var flat = new Vector3(1f, 1f, 0.62f);
            b.Cylinder(0, Matrix4x4.TRS(new Vector3(0f, 1.36f, 0f), Quaternion.identity, flat), 0.18f, 0.3f, 12);
            b.Cylinder(0, Matrix4x4.TRS(new Vector3(0f, 1.13f, 0f), Quaternion.identity, flat), 0.14f, 0.2f, 12);
            b.Sphere(0, new Vector3(0f, 1.5f, 0f), 0.11f, 10, 6);
            Part(Spine);
            b.Cylinder(0, Matrix4x4.TRS(new Vector3(0f, 0.96f, 0f), Quaternion.identity, new Vector3(1f, 1f, 0.7f)), 0.16f, 0.16f, 12);
            Part(Hips);
            b.Cylinder(0, Matrix4x4.Translate(new Vector3(0f, 1.57f, 0f)), 0.045f, 0.1f, 8);
            b.Sphere(0, new Vector3(0f, 1.71f, 0.01f), 0.11f, 14, 10);
            Part(Head);

            for (var side = -1; side <= 1; side += 2)
            {
                var left = side < 0;
                var shoulder = new Vector3(side * 0.22f, 1.46f, 0f);
                var elbow = new Vector3(side * 0.27f, 1.18f, -0.02f);
                var wrist = new Vector3(side * 0.28f, 0.92f, 0.04f);
                Joint(b, shoulder, 0.065f);
                Limb(b, shoulder, elbow, 0.05f);
                Part(left ? UpperArmL : UpperArmR);
                Joint(b, elbow, 0.046f);
                Limb(b, elbow, wrist, 0.042f);
                Joint(b, wrist + Vector3.down * 0.05f, 0.05f);
                Part(left ? ForeArmL : ForeArmR);

                var hip = new Vector3(side * 0.1f, 0.9f, 0f);
                var knee = new Vector3(side * 0.11f, 0.48f, 0.02f);
                var ankle = new Vector3(side * 0.11f, 0.08f, 0f);
                Joint(b, hip, 0.08f);
                Limb(b, hip, knee, 0.072f);
                Part(left ? ThighL : ThighR);
                Joint(b, knee, 0.062f);
                Limb(b, knee, ankle, 0.056f);
                b.Box(0, new Vector3(side * 0.11f, 0.035f, 0.04f), new Vector3(0.09f, 0.07f, 0.22f));
                Part(left ? ShinL : ShinR);
            }
            b.Apply(mesh);

            if (!skinned)
                return;
            var weights = new BoneWeight[bones.Count];
            for (var i = 0; i < weights.Length; i++)
                weights[i] = new BoneWeight { boneIndex0 = bones[i], weight0 = 1f };
            mesh.boneWeights = weights;
            var bindposes = new Matrix4x4[BonePositions.Length];
            for (var i = 0; i < bindposes.Length; i++)
                bindposes[i] = Matrix4x4.Translate(-BonePositions[i]);
            mesh.bindposes = bindposes;
        }

        static void Limb(PropMeshBuilder builder, Vector3 from, Vector3 to, float radius)
        {
            var direction = to - from;
            var transform = Matrix4x4.TRS((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, direction), Vector3.one);
            builder.Cylinder(0, transform, radius, direction.magnitude, 10, caps: false);
        }

        static void Joint(PropMeshBuilder builder, Vector3 center, float radius) => builder.Sphere(0, center, radius, 10, 6);
    }
}
