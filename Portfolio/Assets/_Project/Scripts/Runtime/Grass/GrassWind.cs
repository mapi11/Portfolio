using UnityEngine;

namespace Portfolio.Grass
{
    [ExecuteAlways]
    public class GrassWind : MonoBehaviour
    {
        public Texture2D noise;
        [Range(0f, 360f)] public float direction = 35f;
        [Range(0f, 2f)] public float gustStrength = 0.55f;
        public float gustSpeed = 2.5f;
        public float gustScale = 0.045f;
        [Range(0f, 0.5f)] public float sway = 0.08f;
        public float swaySpeed = 2.2f;
        public float interactorStrength = 1.4f;

        static readonly int s_Noise = Shader.PropertyToID("_GrassWindNoise");
        static readonly int s_Wind = Shader.PropertyToID("_GrassWind");
        static readonly int s_WindParams = Shader.PropertyToID("_GrassWindParams");

        void OnEnable() => Apply();
        void OnValidate() => Apply();
        void Update() => Apply();

        void Apply()
        {
            var radians = direction * Mathf.Deg2Rad;
            if (noise != null)
                Shader.SetGlobalTexture(s_Noise, noise);
            Shader.SetGlobalVector(s_Wind, new Vector4(Mathf.Sin(radians), Mathf.Cos(radians), gustStrength, gustSpeed * gustScale));
            Shader.SetGlobalVector(s_WindParams, new Vector4(gustScale, sway, swaySpeed, interactorStrength));
        }
    }
}
