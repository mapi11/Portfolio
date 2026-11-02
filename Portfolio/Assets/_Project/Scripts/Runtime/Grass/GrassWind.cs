using UnityEngine;

namespace Portfolio.Grass
{
    /// <summary>
    /// Глобальный ветер для всей растительности: шейдерные глобалы вместо свойств
    /// материала, так что трава, кусты и деревья качаются согласованно.
    /// </summary>
    [ExecuteAlways]
    public class GrassWind : MonoBehaviour
    {
        [Tooltip("Тайлящийся шум (канал R): порывы ветра и поляны разной высоты")]
        public Texture2D noise;
        [Range(0f, 360f)] public float direction = 35f;
        [Range(0f, 2f)] public float gustStrength = 0.55f;
        [Tooltip("Скорость бега порывов по полю, м/с")]
        public float gustSpeed = 2.5f;
        [Tooltip("Размер порывов: чем меньше, тем шире волны по полю")]
        public float gustScale = 0.045f;
        [Range(0f, 0.5f)] public float sway = 0.08f;
        public float swaySpeed = 2.2f;
        [Tooltip("Сила отталкивания травы от GrassInteractor")]
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
            // Шейдер сдвигает UV шума: переводим м/с в UV/с, чтобы скорость порывов
            // не зависела от gustScale.
            Shader.SetGlobalVector(s_Wind, new Vector4(Mathf.Sin(radians), Mathf.Cos(radians), gustStrength, gustSpeed * gustScale));
            Shader.SetGlobalVector(s_WindParams, new Vector4(gustScale, sway, swaySpeed, interactorStrength));
        }
    }
}
