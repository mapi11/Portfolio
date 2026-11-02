using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Portfolio.Editor.Validation
{
    public enum Severity { Warning, Error }

    public readonly struct Issue
    {
        public readonly Severity severity;
        public readonly string assetPath;
        public readonly string message;

        public Issue(Severity severity, string assetPath, string message)
        {
            this.severity = severity;
            this.assetPath = assetPath;
            this.message = message;
        }

        public override string ToString() => $"[{severity}] {assetPath}: {message}";
    }

    public static class AssetValidator
    {
        public static List<Issue> ValidateFolder(string folder)
        {
            var issues = new List<Issue>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model t:Texture2D t:Material", new[] { folder }))
                Validate(AssetDatabase.GUIDToAssetPath(guid), issues);
            return issues;
        }

        public static void Validate(string path, List<Issue> issues)
        {
            var budgets = AssetBudgets.Load();
            var rule = budgets.GetRule(path);
            if (rule == null)
                return;

            switch (AssetImporter.GetAtPath(path))
            {
                case ModelImporter:
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (root != null)
                        ValidateModel(path, root, rule, budgets, issues);
                    break;
                case TextureImporter texture:
                    ValidateTexture(path, texture, rule, budgets, issues);
                    break;
                default:
                    if (path.EndsWith(".mat"))
                        ValidateName(path, budgets.materialPrefix, issues);
                    break;
            }
        }

        public static void ValidateModel(string path, GameObject root, AssetBudgets.Rule rule, AssetBudgets budgets, List<Issue> issues)
        {
            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            ValidateName(path, skinned.Length > 0 ? budgets.skinnedMeshPrefix : budgets.staticMeshPrefix, issues);

            var lodGroup = root.GetComponentInChildren<LODGroup>(true);
            if (lodGroup != null)
            {
                ValidateLods(path, lodGroup, rule, issues);
            }
            else
            {
                if (rule.minLodCount > 1)
                    issues.Add(new Issue(Severity.Error, path, $"нет LODGroup, требуется минимум {rule.minLodCount} LOD (меши с суффиксами _LOD0.._LODn)"));

                var triangles = 0;
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    triangles += CountTriangles(GetMesh(renderer));
                CheckTriangles(path, "модель", triangles, rule, issues);
            }

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var count = renderer.sharedMaterials.Length;
                if (count > rule.maxMaterialsPerRenderer)
                    issues.Add(new Issue(Severity.Warning, path, $"{renderer.name}: {count} материалов (лимит {rule.maxMaterialsPerRenderer}), каждый — отдельный draw call"));
            }

            foreach (var smr in skinned)
                ValidateSkinning(path, smr, rule, issues);
        }

        static void ValidateLods(string path, LODGroup lodGroup, AssetBudgets.Rule rule, List<Issue> issues)
        {
            var lods = lodGroup.GetLODs();
            if (lods.Length < rule.minLodCount)
                issues.Add(new Issue(Severity.Error, path, $"{lods.Length} LOD, требуется {rule.minLodCount}"));

            var previous = 0;
            for (var i = 0; i < lods.Length; i++)
            {
                var triangles = 0;
                foreach (var renderer in lods[i].renderers)
                    if (renderer != null)
                        triangles += CountTriangles(GetMesh(renderer));

                if (i == 0)
                    CheckTriangles(path, "LOD0", triangles, rule, issues);
                else if (previous > 0 && triangles > previous * rule.maxLodTriangleRatio)
                    issues.Add(new Issue(Severity.Warning, path,
                        $"LOD{i}: {triangles} tris — это {(float)triangles / previous:P0} от LOD{i - 1}, ожидается не больше {rule.maxLodTriangleRatio:P0}"));

                previous = triangles;
            }
        }

        static void ValidateSkinning(string path, SkinnedMeshRenderer smr, AssetBudgets.Rule rule, List<Issue> issues)
        {
            if (smr.bones.Length > rule.maxBones)
                issues.Add(new Issue(Severity.Error, path, $"{smr.name}: {smr.bones.Length} костей (лимит {rule.maxBones})"));

            var mesh = smr.sharedMesh;
            if (mesh == null)
                return;

            if (!mesh.isReadable)
            {
                if (AssetImporter.GetAtPath(path) is ModelImporter importer &&
                    importer.skinWeights == ModelImporterSkinWeights.Custom &&
                    importer.maxBonesPerVertex > rule.maxBonesPerVertex)
                    issues.Add(new Issue(Severity.Error, path, $"Max Bones/Vertex в импорте {importer.maxBonesPerVertex} (лимит {rule.maxBonesPerVertex})"));
                return;
            }

            var maxInfluences = 0;
            foreach (var count in mesh.GetBonesPerVertex())
                if (count > maxInfluences)
                    maxInfluences = count;

            if (maxInfluences > rule.maxBonesPerVertex)
                issues.Add(new Issue(Severity.Error, path, $"{smr.name}: до {maxInfluences} весов на вершину (лимит {rule.maxBonesPerVertex})"));
        }

        static void ValidateTexture(string path, TextureImporter importer, AssetBudgets.Rule rule, AssetBudgets budgets, List<Issue> issues)
        {
            ValidateName(path, budgets.texturePrefix, issues);

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            if (!Mathf.IsPowerOfTwo(width) || !Mathf.IsPowerOfTwo(height))
                issues.Add(new Issue(Severity.Warning, path, $"размер {width}x{height} не степень двойки: ASTC/ETC2 сожмут хуже, mip-уровни потеряют качество"));

            if (importer.maxTextureSize > rule.maxTextureSize)
                issues.Add(new Issue(Severity.Error, path, $"Max Size {importer.maxTextureSize} больше бюджета {rule.maxTextureSize}"));

            var name = Path.GetFileNameWithoutExtension(path);
            var isNormal = name.EndsWith(budgets.normalSuffix);
            if (isNormal && importer.textureType != TextureImporterType.NormalMap)
                issues.Add(new Issue(Severity.Error, path, "суффикс нормали, но Texture Type не Normal Map"));

            if (HasSuffix(name, budgets.linearSuffixes) && !isNormal && importer.sRGBTexture)
                issues.Add(new Issue(Severity.Error, path, "упакованная/линейная карта импортирована как sRGB — значения каналов исказятся"));

            if (rule.requireMobileCompression)
            {
                foreach (var platform in new[] { "Android", "iPhone" })
                {
                    var settings = importer.GetPlatformTextureSettings(platform);
                    if (!settings.overridden || !IsAstc(settings.format))
                        issues.Add(new Issue(Severity.Warning, path, $"нет ASTC-оверрайда для {platform}"));
                }
            }
        }

        static void ValidateName(string path, string prefix, List<Issue> issues)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(prefix) && !name.StartsWith(prefix))
                issues.Add(new Issue(Severity.Warning, path, $"имя должно начинаться с {prefix}"));
            if (name.Contains(" "))
                issues.Add(new Issue(Severity.Error, path, "пробел в имени"));
        }

        static void CheckTriangles(string path, string label, int triangles, AssetBudgets.Rule rule, List<Issue> issues)
        {
            if (triangles > rule.maxTrianglesLod0)
                issues.Add(new Issue(Severity.Error, path, $"{label}: {triangles} tris (бюджет «{rule.name}» — {rule.maxTrianglesLod0})"));
        }

        public static bool HasSuffix(string name, string[] suffixes)
        {
            foreach (var suffix in suffixes)
                if (name.EndsWith(suffix))
                    return true;
            return false;
        }

        static bool IsAstc(TextureImporterFormat format) => format.ToString().StartsWith("ASTC");

        static Mesh GetMesh(Renderer renderer) => renderer switch
        {
            SkinnedMeshRenderer smr => smr.sharedMesh,
            MeshRenderer => renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null,
            _ => null
        };

        static int CountTriangles(Mesh mesh)
        {
            if (mesh == null)
                return 0;
            var indices = 0L;
            for (var i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    indices += mesh.GetIndexCount(i);
            return (int)(indices / 3);
        }
    }
}
