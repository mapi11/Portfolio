using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
    // Blender-экспорт привязывает к каждому мешу все deform-кости, даже без весов.
    // Каждая лишняя кость — лишняя матрица скиннинга, поэтому вырезаем их на импорте.
    public class SkinnedBonePruner : AssetPostprocessor
    {
        public override int GetPostprocessOrder() => 0;

        void OnPostprocessModel(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Prune(renderer);
        }

        static void Prune(SkinnedMeshRenderer renderer)
        {
            var mesh = renderer.sharedMesh;
            var bones = renderer.bones;
            if (mesh == null || bones.Length == 0)
                return;

            var weights = mesh.GetAllBoneWeights();
            var used = new bool[bones.Length];
            foreach (var w in weights)
                if (w.weight > 0f)
                    used[w.boneIndex] = true;

            var remap = new int[bones.Length];
            var keptBones = new List<Transform>();
            var keptBindposes = new List<Matrix4x4>();
            var bindposes = mesh.bindposes;
            for (var i = 0; i < bones.Length; i++)
            {
                if (!used[i])
                    continue;
                remap[i] = keptBones.Count;
                keptBones.Add(bones[i]);
                keptBindposes.Add(bindposes[i]);
            }
            if (keptBones.Count == bones.Length || keptBones.Count == 0)
                return;

            var remapped = new NativeArray<BoneWeight1>(weights.Length, Allocator.Temp);
            for (var i = 0; i < weights.Length; i++)
            {
                var w = weights[i];
                w.boneIndex = used[w.boneIndex] ? remap[w.boneIndex] : 0;
                remapped[i] = w;
            }
            mesh.SetBoneWeights(mesh.GetBonesPerVertex(), remapped);
            remapped.Dispose();

            mesh.bindposes = keptBindposes.ToArray();
            renderer.bones = keptBones.ToArray();
        }
    }
}
