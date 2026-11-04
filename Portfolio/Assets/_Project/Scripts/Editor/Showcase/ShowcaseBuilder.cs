using System.Collections.Generic;
using System.IO;
using Portfolio.Diagnostics;
using Portfolio.VFX;
using Portfolio.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static Portfolio.Editor.Showcase.VfxTextureGenerator;

namespace Portfolio.Editor.Showcase
{
    public static class ShowcaseBuilder
    {
        const string Root = "Assets/_Project";
        const string VfxTextures = Root + "/VFX/Textures";
        const string VfxMaterials = Root + "/VFX/Materials";
        const string EnvMaterials = Root + "/Art/Environment/Materials";
        const string ScenePath = Root + "/Scenes/WeaponRange.unity";

        static Shader ParticleShader => Shader.Find("Portfolio/VFX/Particle");
        static Shader InstancedShader => Shader.Find("Portfolio/VFX/Instanced");
        static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");

        [MenuItem("Tools/Portfolio/Build Weapon Range")]
        public static void Build()
        {
            var particleAtlas = CreateParticleAtlas(VfxTextures + "/T_VFX_Atlas.png");
            var decalAtlas = CreateDecalAtlas(VfxTextures + "/T_Decal_BulletHoles.png");

            var flash = VfxMaterial("M_VFX_Flash", particleAtlas, ParticleFlash, new Color(5f, 3.4f, 1.8f), 1f);
            var spark = VfxMaterial("M_VFX_Spark", particleAtlas, ParticleSoft, new Color(6f, 3.2f, 1.2f), 1f);
            var smoke = VfxMaterial("M_VFX_Smoke", particleAtlas, ParticleSmoke, Color.white, 0f);
            var chip = VfxMaterial("M_VFX_Chip", particleAtlas, ParticleChip, Color.white, 0f);
            var tracer = VfxMaterial("M_VFX_Tracer", particleAtlas, ParticleSoft, new Color(8f, 5f, 2f), 1f, InstancedShader);

            var muzzle = SavePrefab(BuildMuzzle(flash, spark, smoke), Root + "/VFX/Muzzle/FX_Muzzle_AR.prefab");
            var impactMetal = SavePrefab(BuildImpactMetal(flash, spark, smoke), Root + "/VFX/Impacts/FX_Impact_Metal.prefab");
            var impactConcrete = SavePrefab(BuildImpactConcrete(spark, smoke, chip), Root + "/VFX/Impacts/FX_Impact_Concrete.prefab");
            var impactWood = SavePrefab(BuildImpactWood(smoke, chip), Root + "/VFX/Impacts/FX_Impact_Wood.prefab");

            var library = CreateOrReplace<ImpactLibrary>(Root + "/VFX/Impacts/ImpactLibrary.asset");
            library.entries = new[]
            {
                Impact(SurfaceType.Default, impactConcrete, DecalMaterial("M_Decal_Concrete", decalAtlas, DecalConcrete), 0.08f, 0.12f),
                Impact(SurfaceType.Concrete, impactConcrete, DecalMaterial("M_Decal_Concrete", decalAtlas, DecalConcrete), 0.08f, 0.12f),
                Impact(SurfaceType.Metal, impactMetal, DecalMaterial("M_Decal_Metal", decalAtlas, DecalMetal), 0.05f, 0.07f),
                Impact(SurfaceType.Wood, impactWood, DecalMaterial("M_Decal_Wood", decalAtlas, DecalWood), 0.06f, 0.09f),
            };
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            BuildScene(muzzle, library, tracer);
            Debug.Log("Weapon range built: " + ScenePath);
        }

        static GameObject BuildMuzzle(Material flash, Material spark, Material smoke)
        {
            var root = new GameObject("FX_Muzzle_AR");
            var core = Part(root, "Core", flash, false, 0.035f, 0.05f, 0f, 0f, 0.14f, 0.2f, Color.white);
            var petals = Part(root, "Petals", flash, false, 0.03f, 0.045f, 4f, 7f, 0.05f, 0.08f, Color.white, cone: 12f);
            Stretch(petals, 0.02f, 1.5f);
            var sparks = Part(root, "Sparks", spark, true, 0.06f, 0.12f, 10f, 18f, 0.006f, 0.012f, Color.white, cone: 20f);
            Stretch(sparks, 0.015f, 1f);
            var puff = Part(root, "Smoke", smoke, true, 0.35f, 0.6f, 0.8f, 1.6f, 0.04f, 0.06f, new Color(0.7f, 0.7f, 0.7f, 0.12f), cone: 15f);
            Grow(puff, 2.5f);
            Drag(puff, 1.5f);
            return Burst(root, (core, 1, 1), (petals, 3, 4), (sparks, 3, 5), (puff, 0, 1));
        }

        static GameObject BuildImpactMetal(Material flash, Material spark, Material smoke)
        {
            var root = new GameObject("FX_Impact_Metal");
            var hitFlash = Part(root, "Flash", flash, true, 0.03f, 0.05f, 0f, 0f, 0.25f, 0.35f, Color.white);
            var sparks = Part(root, "Sparks", spark, true, 0.12f, 0.3f, 3f, 9f, 0.02f, 0.035f, Color.white, gravity: 1.2f, cone: 45f);
            Stretch(sparks, 0.02f, 1f);
            var puff = Part(root, "Smoke", smoke, true, 0.4f, 0.7f, 0.2f, 0.6f, 0.06f, 0.1f, new Color(0.6f, 0.6f, 0.6f, 0.2f), cone: 30f);
            Grow(puff, 3f);
            return Burst(root, (hitFlash, 1, 1), (sparks, 8, 14), (puff, 1, 2));
        }

        static GameObject BuildImpactConcrete(Material spark, Material smoke, Material chip)
        {
            var root = new GameObject("FX_Impact_Concrete");
            var hitFlash = Part(root, "Flash", spark, true, 0.03f, 0.04f, 0f, 0f, 0.12f, 0.18f, Color.white);
            var dust = Part(root, "Dust", smoke, true, 0.6f, 1.1f, 0.6f, 2.2f, 0.15f, 0.25f, new Color(0.62f, 0.6f, 0.57f, 0.55f), cone: 25f);
            Grow(dust, 3f);
            Drag(dust, 3f);
            var chips = Part(root, "Chips", chip, true, 0.5f, 0.9f, 2f, 5f, 0.02f, 0.04f, new Color(0.5f, 0.48f, 0.45f), gravity: 2f, cone: 35f);
            Spin(chips);
            return Burst(root, (hitFlash, 1, 1), (dust, 3, 5), (chips, 4, 8));
        }

        static GameObject BuildImpactWood(Material smoke, Material chip)
        {
            var root = new GameObject("FX_Impact_Wood");
            var chips = Part(root, "Splinters", chip, true, 0.5f, 1f, 2f, 6f, 0.025f, 0.05f, new Color(0.6f, 0.45f, 0.28f), gravity: 2.2f, cone: 35f);
            Spin(chips);
            var dust = Part(root, "Dust", smoke, true, 0.5f, 0.9f, 0.4f, 1.4f, 0.12f, 0.2f, new Color(0.55f, 0.45f, 0.35f, 0.4f), cone: 25f);
            Grow(dust, 2.5f);
            Drag(dust, 3f);
            return Burst(root, (chips, 6, 10), (dust, 2, 3));
        }

        static ParticleSystem Part(GameObject root, string name, Material material, bool worldSpace,
            float lifeMin, float lifeMax, float speedMin, float speedMax, float sizeMin, float sizeMax, Color color,
            float gravity = 0f, float cone = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.maxParticles = 64;

            var emission = system.emission;
            emission.enabled = false;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone;
            shape.radius = 0.005f;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 1f;
            return system;
        }

        static void Stretch(ParticleSystem system, float velocityScale, float lengthScale)
        {
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = velocityScale;
            renderer.lengthScale = lengthScale;
            var main = system.main;
            main.startRotation = 0f;
        }

        static void Grow(ParticleSystem system, float endScale)
        {
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(endScale, new AnimationCurve(
                new Keyframe(0f, 1f / endScale, 0f, 3f), new Keyframe(1f, 1f)));
        }

        static void Drag(ParticleSystem system, float drag)
        {
            var limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
        }

        static void Spin(ParticleSystem system)
        {
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
        }

        static GameObject Burst(GameObject root, params (ParticleSystem system, int min, int max)[] parts)
        {
            var burst = root.AddComponent<BurstEffect>();
            var list = new List<BurstEffect.Part>();
            foreach (var (system, min, max) in parts)
                list.Add(new BurstEffect.Part { system = system, min = min, max = max });
            burst.parts = list.ToArray();
            return root;
        }

        static GameObject SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static ImpactLibrary.Entry Impact(SurfaceType surface, GameObject prefab, Material decal, float sizeMin, float sizeMax) => new()
        {
            surface = surface,
            effect = prefab.GetComponent<BurstEffect>(),
            decal = decal,
            decalSize = new Vector2(sizeMin, sizeMax)
        };

        static Material VfxMaterial(string name, Texture2D atlas, int tile, Color tint, float additive, Shader shader = null)
        {
            var material = MaterialAsset(VfxMaterials + "/" + name + ".mat", shader ?? ParticleShader);
            material.SetTexture("_BaseMap", atlas);
            SetTile(material, tile);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Additive", additive);
            material.SetFloat("_OffsetFactor", 0f);
            material.SetFloat("_OffsetUnits", 0f);
            material.enableInstancing = shader == InstancedShader;
            return material;
        }

        static Material DecalMaterial(string name, Texture2D atlas, int tile)
        {
            var material = MaterialAsset(VfxMaterials + "/" + name + ".mat", InstancedShader);
            material.SetTexture("_BaseMap", atlas);
            SetTile(material, tile);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Additive", 0f);
            material.SetFloat("_OffsetFactor", -1f);
            material.SetFloat("_OffsetUnits", -1f);
            material.enableInstancing = true;
            return material;
        }

        static Material LitMaterial(string path, Color color, float metallic, float smoothness, bool instancing = false)
        {
            var material = MaterialAsset(path, LitShader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = instancing;
            return material;
        }

        static Material MaterialAsset(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static T CreateOrReplace<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void BuildScene(GameObject muzzlePrefab, ImpactLibrary library, Material tracerMaterial)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var concrete = LitMaterial(EnvMaterials + "/M_Env_Concrete.mat", new Color(0.42f, 0.42f, 0.41f), 0f, 0.2f);
            var metal = LitMaterial(EnvMaterials + "/M_Env_Metal.mat", new Color(0.5f, 0.53f, 0.56f), 0.9f, 0.55f);
            var steel = LitMaterial(EnvMaterials + "/M_Env_Steel.mat", new Color(0.24f, 0.25f, 0.26f), 0.8f, 0.4f);
            var painted = LitMaterial(EnvMaterials + "/M_Env_MetalPainted.mat", new Color(0.18f, 0.3f, 0.22f), 0.4f, 0.4f);
            var paintedDark = LitMaterial(EnvMaterials + "/M_Env_MetalPaintedDark.mat", new Color(0.11f, 0.19f, 0.14f), 0.4f, 0.35f);
            var wood = LitMaterial(EnvMaterials + "/M_Env_Wood.mat", new Color(0.5f, 0.35f, 0.21f), 0f, 0.25f);
            var woodDark = LitMaterial(EnvMaterials + "/M_Env_WoodDark.mat", new Color(0.3f, 0.2f, 0.12f), 0f, 0.2f);
            var brass = LitMaterial(VfxMaterials + "/M_Shell_Brass.mat", new Color(0.85f, 0.64f, 0.3f), 1f, 0.75f, instancing: true);

            // пол и стены в общей сетке проекта
            var environment = new GameObject("Environment").transform;
            var floor = ProjectStyle.CreateFloor(new Vector2(18f, 30f), 2.5f);
            floor.transform.SetParent(environment, false);
            floor.transform.position = new Vector3(0f, 0f, 11f);
            floor.AddComponent<MeshCollider>();
            floor.AddComponent<Surface>().type = SurfaceType.Concrete;
            Wall(environment, "Wall_Back", new Vector3(0f, 0f, 24f), new Vector3(16.5f, 5f, 0.5f));
            Wall(environment, "Wall_Left", new Vector3(-8f, 0f, 15f), new Vector3(0.5f, 5f, 18f));
            Wall(environment, "Wall_Right", new Vector3(8f, 0f, 15f), new Vector3(0.5f, 5f, 18f));

            var pillar = PropMesh("SM_Range_ConcretePillar", m => RangePropMeshes.ConcretePillar(m, 1.2f, 2f));
            var barrier = PropMesh("SM_Range_JerseyBarrier", m => RangePropMeshes.JerseyBarrier(m, 2f));
            var targetTall = PropMesh("SM_Range_SteelTarget_Tall", m => RangePropMeshes.SteelTarget(m, 1f, 1.4f, 0.6f));
            var targetWide = PropMesh("SM_Range_SteelTarget_Wide", m => RangePropMeshes.SteelTarget(m, 1.2f, 1f, 0.6f));
            var drum = PropMesh("SM_Range_Drum", RangePropMeshes.Drum);
            var container = PropMesh("SM_Range_ShippingContainer", RangePropMeshes.ShippingContainer);
            var crate = PropMesh("SM_Range_Crate", m => RangePropMeshes.Crate(m, 1f));
            var planks = PropMesh("SM_Range_PlankWall", m => RangePropMeshes.PlankWall(m, 3f, 2f));

            Prop(environment, "Concrete_Pillar", pillar, new[] { concrete }, SurfaceType.Concrete, new Vector3(-4.5f, 0f, 11f));
            Prop(environment, "Concrete_Barrier", barrier, new[] { concrete }, SurfaceType.Concrete, new Vector3(-2.6f, 0f, 9f));

            Prop(environment, "Steel_Target_A", targetTall, new[] { metal, steel }, SurfaceType.Metal, new Vector3(-0.8f, 0f, 12f), 8f);
            Prop(environment, "Steel_Target_B", targetWide, new[] { metal, steel }, SurfaceType.Metal, new Vector3(1.6f, 0f, 15f), -12f);
            Prop(environment, "Drum_A", drum, new[] { painted, steel }, SurfaceType.Metal, new Vector3(4.2f, 0f, 10f), 30f);
            Prop(environment, "Drum_B", drum, new[] { painted, steel }, SurfaceType.Metal, new Vector3(4.9f, 0f, 10.6f), 140f);
            Prop(environment, "Shipping_Container", container, new[] { painted, paintedDark, steel }, SurfaceType.Metal, new Vector3(5.5f, 0f, 18f), 4f);

            Prop(environment, "Wood_Crate_A", crate, new[] { wood, woodDark }, SurfaceType.Wood, new Vector3(2.4f, 0f, 8f));
            Prop(environment, "Wood_Crate_B", crate, new[] { wood, woodDark }, SurfaceType.Wood, new Vector3(3.4f, 0f, 8.6f), 25f, 0.9f);
            Prop(environment, "Wood_Crate_C", crate, new[] { wood, woodDark }, SurfaceType.Wood, new Vector3(2.5f, 1f, 8.05f), -10f, 0.8f);
            Prop(environment, "Wood_Planks", planks, new[] { wood, woodDark }, SurfaceType.Wood, new Vector3(-3.5f, 0f, 16f));

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            light.color = new Color(1f, 0.97f, 0.93f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            ProjectStyle.ApplyEnvironment(ProjectStyle.Line);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/Settings/SampleSceneProfile.asset");

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.01f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var look = camera.gameObject.AddComponent<FirstPersonLook>();

            var weaponGo = new GameObject("Weapon");
            weaponGo.transform.SetParent(camera.transform, false);
            weaponGo.transform.localPosition = Ak47Weapon.HoldPosition;
            var kickRoot = new GameObject("KickRoot").transform;
            kickRoot.SetParent(weaponGo.transform, false);
            var (muzzleSocket, shellSocket) = Ak47Weapon.Attach(kickRoot);
            var muzzle = (GameObject)PrefabUtility.InstantiatePrefab(muzzlePrefab, muzzleSocket);

            var systems = new GameObject("VFX Systems");
            var decals = systems.AddComponent<DecalSystem>();
            var tracers = systems.AddComponent<TracerSystem>();
            tracers.material = tracerMaterial;
            var shells = systems.AddComponent<ShellSystem>();
            shells.mesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            shells.material = brass;
            var impacts = systems.AddComponent<ImpactSystem>();
            impacts.library = library;
            impacts.decals = decals;

            var weapon = weaponGo.AddComponent<WeaponController>();
            weapon.muzzleSocket = muzzleSocket;
            weapon.shellSocket = shellSocket;
            weapon.kickRoot = kickRoot;
            weapon.aimCamera = camera;
            weapon.muzzleFlash = muzzle.GetComponent<BurstEffect>();
            weapon.impacts = impacts;
            weapon.tracers = tracers;
            weapon.shells = shells;
            weapon.look = look;

            var benchmark = systems.AddComponent<Benchmark>();
            benchmark.weapon = weapon;
            benchmark.look = look;
            benchmark.decals = decals;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
        }

        static void Wall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var wall = ProjectStyle.CreateGridBox(name, size);
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            var collider = wall.AddComponent<BoxCollider>();
            collider.center = Vector3.up * size.y * 0.5f;
            collider.size = size;
            wall.AddComponent<Surface>().type = SurfaceType.Concrete;
        }

        static Mesh PropMesh(string name, System.Action<Mesh> build)
        {
            var path = Root + "/Art/Environment/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            build(mesh);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static void Prop(Transform parent, string name, Mesh mesh, Material[] materials, SurfaceType surface,
            Vector3 position, float yaw = 0f, float scale = 1f)
        {
            var go = new GameObject(name) { isStatic = true };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<Surface>().type = surface;
        }
    }
}
