using System.IO;
using Portfolio.Editor.Showcase;
using Portfolio.Grass;
using Portfolio.Lightning;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Portfolio.Editor.Lightning
{
    public static class LightningSceneBuilder
    {
        const string Root = "Assets/_Project/Art/Lightning";
        const string ScenePath = "Assets/_Project/Scenes/LightningStorm.unity";
        const string AtlasPath = "Assets/_Project/VFX/Textures/T_VFX_Atlas.png";
        const float FloorSize = 20f;
        const float FloorCorner = 2.5f;

        [MenuItem("Tools/Portfolio/Build Lightning Storm")]
        public static void Build()
        {
            var scorchAtlas = StormAssetGenerator.ScorchAtlas(Root + "/Textures/T_Scorch_Lichtenberg.png");
            var quad = MeshAsset("SM_Scorch_Quad");
            StormAssetGenerator.Quad(quad);

            var bolt = MaterialAsset("M_Lightning_Bolt", Shader.Find("Portfolio/Lightning/Bolt"));
            var scorch = MaterialAsset("M_Lightning_Scorch", Shader.Find("Portfolio/Lightning/Scorch"));
            scorch.SetTexture("_MainTex", scorchAtlas);

            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            var spark = ParticleMaterial("M_Storm_Spark", atlas, VfxTextureGenerator.ParticleSoft, new Color(6f, 3.2f, 1.4f, 1f), additive: true);
            var debris = ParticleMaterial("M_Storm_Debris", atlas, VfxTextureGenerator.ParticleChip, ProjectStyle.Surface, additive: false);
            var dust = ParticleMaterial("M_Storm_Dust", atlas, VfxTextureGenerator.ParticleSoft, WithAlpha(ProjectStyle.Line, 0.14f), additive: false);
            var smoke = ParticleMaterial("M_Storm_Smoke", atlas, VfxTextureGenerator.ParticleSoft, WithAlpha(ProjectStyle.Line, 0.09f), additive: false);
            var flash = ParticleMaterial("M_Storm_GroundFlash", atlas, VfxTextureGenerator.ParticleSoft, new Color(2f, 2.2f, 3f, 1f), additive: true);

            var profile = VolumeProfileAsset();
            AssetDatabase.SaveAssets();

            BuildScene(quad, bolt, scorch, spark, debris, dust, smoke, flash, profile);
            Debug.Log("Lightning storm built: " + ScenePath);
        }

        static void BuildScene(Mesh quad, Material bolt, Material scorch, Material spark, Material debris, Material dust,
            Material smoke, Material flash, VolumeProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floor = ProjectStyle.CreateFloor(FloorSize, FloorCorner);

            var key = Object.FindFirstObjectByType<Light>();
            key.name = "Key / Flash Light";
            key.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            key.color = Color.white;
            key.intensity = 0.15f;
            key.shadows = LightShadows.Soft;

            var flashLight = new GameObject("Strike Light").AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.range = 40f;
            flashLight.shadows = LightShadows.None;
            flashLight.enabled = false;

            var emberLight = new GameObject("Ember Light").AddComponent<Light>();
            emberLight.type = LightType.Point;
            emberLight.range = 6f;
            emberLight.color = new Color(1f, 0.42f, 0.12f);
            emberLight.shadows = LightShadows.None;
            emberLight.enabled = false;

            RenderSettings.skybox = ProjectStyle.CreateBackdrop();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ProjectStyle.Surface;
            RenderSettings.fog = false;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = ReflectionCube(Root + "/Textures/T_Storm_Reflection.cubemap");
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(RenderSettings.ambientLight.linear);
            RenderSettings.ambientProbe = probe;

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            var camera = Object.FindFirstObjectByType<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 200f;
            camera.allowHDR = true;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            // на плавном фоне свечение блума иначе ложится заметными кольцами
            cameraData.dithering = true;
            var orbit = camera.gameObject.AddComponent<OrbitCamera>();
            orbit.target = new Vector3(0f, 1.5f, 0f);
            orbit.distance = 21f;
            orbit.pitch = 32f;
            orbit.pitchLimits = new Vector2(-10f, 60f);
            orbit.yaw = 20f;
            orbit.autoRotateSpeed = 2f;
            orbit.distanceLimits = new Vector2(8f, 40f);

            var effects = new GameObject("Strike Effects").transform;
            var groundPlane = floor.transform;
            var sparks = Burst(effects, "Sparks", spark, 800, gravity: 1.3f, stretch: true, groundPlane, sizeOverLife: 0.3f, growth: false);
            var debrisSystem = Burst(effects, "Debris", debris, 400, gravity: 1.6f, stretch: false, groundPlane, sizeOverLife: 1f, growth: false);
            var dustSystem = Burst(effects, "Dust Ring", dust, 600, gravity: -0.02f, stretch: false, null, sizeOverLife: 3f, growth: true);
            var smokeSystem = Burst(effects, "Smoke", smoke, 3000, gravity: -0.02f, stretch: false, null, sizeOverLife: 7f, growth: true);
            SoftSmoke(smokeSystem);
            var flashSystem = Burst(effects, "Ground Flash", flash, 20, gravity: 0f, stretch: false, null, sizeOverLife: 1.3f, growth: true);
            Drag(dustSystem, 2.2f);
            Drag(debrisSystem, 0.4f);
            Drag(sparks, 0.6f);

            var storm = new GameObject("Lightning Storm").AddComponent<LightningStorm>();
            storm.boltMaterial = bolt;
            storm.scorchMaterial = scorch;
            storm.scorchQuad = quad;
            storm.keyLight = key;
            storm.flashLight = flashLight;
            storm.emberLight = emberLight;
            storm.sparks = sparks;
            storm.debris = debrisSystem;
            storm.dust = dustSystem;
            storm.smoke = smokeSystem;
            storm.groundFlash = flashSystem;
            storm.strikeRadius = FloorSize * 0.3f;
            storm.scorchSize = 6f;

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildScenes.Add(ScenePath);
        }

        // Дым из множества мелких полупрозрачных частиц: каждая быстро растёт, вихрится шумом и долго тает
        static void SoftSmoke(ParticleSystem system)
        {
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.3f;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var size = system.sizeOverLifetime;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f, 0f, 12f), new Keyframe(0.35f, 4.5f), new Keyframe(1f, 7f)));

            var color = system.colorOverLifetime;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            Drag(system, 0.8f);
            system.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.OldestInFront;
        }

        static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

        // Системы без собственной эмиссии: частицы выпускает LightningStorm через Emit в точке удара
        static ParticleSystem Burst(Transform parent, string name, Material material, int maxParticles, float gravity, bool stretch,
            Transform collisionPlane, float sizeOverLife, bool growth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.enabled = false;

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, growth
                ? AnimationCurve.EaseInOut(0f, 1f, 1f, sizeOverLife)
                : AnimationCurve.Linear(0f, 1f, 1f, sizeOverLife));

            var color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                growth
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            if (collisionPlane != null)
            {
                var collision = system.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, collisionPlane);
                collision.dampen = 0.45f;
                collision.bounce = 0.3f;
                collision.lifetimeLoss = 0.15f;
            }

            if (growth)
            {
                var rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.035f;
                renderer.lengthScale = 1.5f;
            }
            return system;
        }

        static void Drag(ParticleSystem system, float drag)
        {
            var limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = false;
        }

        // Однотонное окружение в тон фона: без него URP отражает дефолтное дневное небо и пол светится
        static Cubemap ReflectionCube(string path)
        {
            const int size = 8;
            var color = Color.Lerp(ProjectStyle.BackdropTop, ProjectStyle.BackdropHorizon, 0.5f).linear;
            var cube = new Cubemap(size, TextureFormat.RGBAHalf, false);
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY,
                         CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    cube.SetPixel(face, x, y, color);
            cube.Apply();
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        static VolumeProfile VolumeProfileAsset()
        {
            var path = Root + "/LightningVolumeProfile.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.7f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(true);

            // Neutral не уводит цвета палитры, в отличие от ACES
            var tonemapping = Add<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static Material ParticleMaterial(string name, Texture2D atlas, int tile, Color color, bool additive)
        {
            var material = MaterialAsset(name, Shader.Find("Portfolio/VFX/Particle"));
            material.SetTexture("_BaseMap", atlas);
            VfxTextureGenerator.SetTile(material, tile);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Additive", additive ? 1f : 0f);
            return material;
        }

        static Material MaterialAsset(string name, Shader shader)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            // сбрасываем к значениям по умолчанию из шейдера, иначе правки дефолтов не доходят до готового материала
            var defaults = new Material(shader);
            material.shader = shader;
            material.CopyPropertiesFromMaterial(defaults);
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh MeshAsset(string name)
        {
            var path = Root + "/Meshes/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                EditorUtility.SetDirty(mesh);
                return mesh;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            mesh = new Mesh { name = name };
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
