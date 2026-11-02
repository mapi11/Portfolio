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
    /// <summary>
    /// Собирает тир для отладки VFX стрельбы: временные текстуры, материалы, префабы
    /// эффектов, библиотеку попаданий и сцену с заглушкой оружия. Повторный запуск
    /// перезаписывает сгенерированные ассеты, поэтому ручные правки делаются в копиях.
    /// Batchmode: -executeMethod Portfolio.Editor.Showcase.ShowcaseBuilder.Build
    /// </summary>
    public static class ShowcaseBuilder
    {
        const string Root = "Assets/_Project";
        const string VfxTextures = Root + "/VFX/Textures";
        const string VfxMaterials = Root + "/VFX/Materials";
        const string EnvMaterials = Root + "/Art/Environment/Materials";
        const string WeaponMaterials = Root + "/Art/Weapons/Placeholder/Materials";
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

        // --- Эффекты ---

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
            // Дым у дула — главный источник overdraw в FP: максимум одна частица на выстрел.
            return Burst(root, (core, 1, 1), (petals, 3, 4), (sparks, 3, 5), (puff, 0, 1));
        }

        static GameObject BuildImpactMetal(Material flash, Material spark, Material smoke)
        {
            var root = new GameObject("FX_Impact_Metal");
            var hitFlash = Part(root, "Flash", flash, true, 0.03f, 0.05f, 0f, 0f, 0.25f, 0.35f, Color.white);
            // Размеры с запасом: на 10–15 м искра тоньше 2 px пропадает на мобильном экране.
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

            // Система «играет» всегда, но сама не эмитит: частицы выпускает BurstEffect через Emit.
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

        // --- Материалы ---

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

        // --- Сцена ---

        static void BuildScene(GameObject muzzlePrefab, ImpactLibrary library, Material tracerMaterial)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var concrete = LitMaterial(EnvMaterials + "/M_Env_Concrete.mat", new Color(0.56f, 0.55f, 0.53f), 0f, 0.2f);
            var floor = LitMaterial(EnvMaterials + "/M_Env_Floor.mat", new Color(0.36f, 0.36f, 0.35f), 0f, 0.15f);
            var metal = LitMaterial(EnvMaterials + "/M_Env_Metal.mat", new Color(0.5f, 0.53f, 0.56f), 0.9f, 0.55f);
            var painted = LitMaterial(EnvMaterials + "/M_Env_MetalPainted.mat", new Color(0.18f, 0.3f, 0.22f), 0.4f, 0.4f);
            var wood = LitMaterial(EnvMaterials + "/M_Env_Wood.mat", new Color(0.5f, 0.35f, 0.21f), 0f, 0.25f);
            var gunMetal = LitMaterial(WeaponMaterials + "/M_Placeholder_GunMetal.mat", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.45f);
            var polymer = LitMaterial(WeaponMaterials + "/M_Placeholder_Polymer.mat", new Color(0.2f, 0.19f, 0.17f), 0f, 0.3f);
            var brass = LitMaterial(VfxMaterials + "/M_Shell_Brass.mat", new Color(0.85f, 0.64f, 0.3f), 1f, 0.75f, instancing: true);

            // Тир: камера в (0, 1.6, 0), мишени на 8–18 м по +Z.
            var environment = new GameObject("Environment").transform;
            Block(environment, "Floor", PrimitiveType.Plane, floor, SurfaceType.Concrete, new Vector3(0, 0, 18), new Vector3(3, 1, 4.5f));
            Block(environment, "Wall_Back", PrimitiveType.Cube, concrete, SurfaceType.Concrete, new Vector3(0, 2.5f, 24), new Vector3(16, 5, 0.5f));
            Block(environment, "Wall_Left", PrimitiveType.Cube, concrete, SurfaceType.Concrete, new Vector3(-8, 2.5f, 15), new Vector3(0.5f, 5, 18));
            Block(environment, "Wall_Right", PrimitiveType.Cube, concrete, SurfaceType.Concrete, new Vector3(8, 2.5f, 15), new Vector3(0.5f, 5, 18));

            Block(environment, "Concrete_Block_A", PrimitiveType.Cube, concrete, SurfaceType.Concrete, new Vector3(-4.5f, 1f, 11), new Vector3(1.2f, 2f, 1.2f));
            Block(environment, "Concrete_Block_B", PrimitiveType.Cube, concrete, SurfaceType.Concrete, new Vector3(-2.6f, 0.5f, 9), new Vector3(2f, 1f, 0.6f));

            Block(environment, "Metal_Plate_A", PrimitiveType.Cube, metal, SurfaceType.Metal, new Vector3(-0.8f, 1.3f, 12), new Vector3(1f, 1.4f, 0.04f), 8f);
            Block(environment, "Metal_Plate_B", PrimitiveType.Cube, metal, SurfaceType.Metal, new Vector3(1.6f, 1.1f, 15), new Vector3(1.2f, 1f, 0.04f), -12f);
            Block(environment, "Metal_Barrel", PrimitiveType.Cylinder, painted, SurfaceType.Metal, new Vector3(4.2f, 0.9f, 10), new Vector3(0.6f, 0.9f, 0.6f));
            Block(environment, "Metal_Container", PrimitiveType.Cube, painted, SurfaceType.Metal, new Vector3(5.5f, 1.3f, 18), new Vector3(3f, 2.6f, 2f), 20f);

            Block(environment, "Wood_Crate_A", PrimitiveType.Cube, wood, SurfaceType.Wood, new Vector3(2.4f, 0.5f, 8), Vector3.one);
            Block(environment, "Wood_Crate_B", PrimitiveType.Cube, wood, SurfaceType.Wood, new Vector3(3.4f, 0.45f, 8.6f), Vector3.one * 0.9f, 25f);
            Block(environment, "Wood_Crate_C", PrimitiveType.Cube, wood, SurfaceType.Wood, new Vector3(2.6f, 1.4f, 8.2f), Vector3.one * 0.8f, -10f);
            Block(environment, "Wood_Planks", PrimitiveType.Cube, wood, SurfaceType.Wood, new Vector3(-3.5f, 1.1f, 16), new Vector3(3f, 2.2f, 0.12f));

            var light = Object.FindFirstObjectByType<Light>();
            light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            light.color = new Color(1f, 0.95f, 0.87f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");

            // Камера и оружие.
            var camera = Object.FindFirstObjectByType<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0, 1.6f, 0), Quaternion.identity);
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.01f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var look = camera.gameObject.AddComponent<FirstPersonLook>();

            var weaponGo = new GameObject("Weapon");
            weaponGo.transform.SetParent(camera.transform, false);
            weaponGo.transform.localPosition = new Vector3(0.13f, -0.15f, 0.36f);
            var kickRoot = new GameObject("KickRoot").transform;
            kickRoot.SetParent(weaponGo.transform, false);
            BuildPlaceholderWeapon(kickRoot, gunMetal, polymer);

            // Имена сокетов — из Docs/RigRequirements.md.
            var muzzleSocket = Socket(kickRoot, "socket_muzzle", new Vector3(0, 0.012f, 0.47f));
            var shellSocket = Socket(kickRoot, "socket_shell_eject", new Vector3(0.03f, 0.02f, 0.04f));
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
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void BuildPlaceholderWeapon(Transform parent, Material gunMetal, Material polymer)
        {
            var model = new GameObject("SM_Placeholder_AR").transform;
            model.SetParent(parent, false);
            Piece(model, "Receiver", PrimitiveType.Cube, gunMetal, new Vector3(0, 0, 0), new Vector3(0.05f, 0.07f, 0.3f));
            Piece(model, "Handguard", PrimitiveType.Cube, polymer, new Vector3(0, 0.005f, 0.24f), new Vector3(0.045f, 0.05f, 0.2f));
            Piece(model, "Barrel", PrimitiveType.Cylinder, gunMetal, new Vector3(0, 0.012f, 0.4f), new Vector3(0.016f, 0.07f, 0.016f), new Vector3(90, 0, 0));
            Piece(model, "Magazine", PrimitiveType.Cube, gunMetal, new Vector3(0, -0.075f, 0.06f), new Vector3(0.028f, 0.1f, 0.055f), new Vector3(10, 0, 0));
            Piece(model, "Grip", PrimitiveType.Cube, polymer, new Vector3(0, -0.06f, -0.08f), new Vector3(0.03f, 0.08f, 0.035f), new Vector3(-15, 0, 0));
            Piece(model, "Sight", PrimitiveType.Cube, gunMetal, new Vector3(0, 0.05f, 0.02f), new Vector3(0.02f, 0.025f, 0.05f));
            Piece(model, "Stock", PrimitiveType.Cube, polymer, new Vector3(0, -0.01f, -0.22f), new Vector3(0.04f, 0.06f, 0.16f));
        }

        static void Piece(Transform parent, string name, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // иначе рейкаст попадёт в собственное оружие
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(euler));
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Block(Transform parent, string name, PrimitiveType type, Material material, SurfaceType surface,
            Vector3 position, Vector3 scale, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.isStatic = true;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<Surface>().type = surface;
        }

        static Transform Socket(Transform parent, string name, Vector3 position)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = position;
            return socket;
        }
    }
}
