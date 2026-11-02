#ifndef PORTFOLIO_GRASS_COMMON_INCLUDED
#define PORTFOLIO_GRASS_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Глобальные параметры задают GrassWind / GrassInteractor / GrassField:
// одна настройка ветра на всю растительность сцены.
TEXTURE2D(_GrassWindNoise);
SAMPLER(sampler_GrassWindNoise);
float4 _GrassWind;          // xy — направление, z — сила порывов, w — скорость
float4 _GrassWindParams;    // x — масштаб шума, y — амплитуда покачивания, z — частота покачивания, w — сила отталкивания
float4 _GrassInteractor;    // xyz — позиция, w — радиус
float4 _GrassFade;          // x — начало, y — конец исчезновения по дистанции

CBUFFER_START(UnityPerMaterial)
    half4 _RootColor;
    half4 _TipColor;
    half4 _DryColor;
    half4 _TranslucencyColor;
    half _DryAmount;
    half _ColorVariation;
    half _RootAO;
    half _Translucency;
    float _PatchScale;
    half _PatchMinHeight;
    half _Bendiness;
CBUFFER_END

struct GrassAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;      // x — сторона травинки, y — высота 0..1
    float4 blade : TEXCOORD1;   // xy — корень травинки (object XZ), z, w — случайные числа травинки
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GrassVertex
{
    float3 positionWS;
    float3 normalWS;
    float height01;
    float2 random;
    float patch;        // значение шума полян: для пятен оттенка
};

float SampleGrassNoise(float2 uv)
{
    // Вершинная выборка: mip 0 явно, на мобильных GPU без производных в вершинном шейдере.
    return SAMPLE_TEXTURE2D_LOD(_GrassWindNoise, sampler_GrassWindNoise, uv, 0).r;
}

GrassVertex DisplaceGrass(GrassAttributes input)
{
    float3 rootWS = TransformObjectToWorld(float3(input.blade.x, 0.0, input.blade.y));
    float3 offset = TransformObjectToWorld(input.positionOS.xyz) - rootWS;
    float h = input.uv.y;

    // Поляны разной высоты и плавное исчезновение вдали: травинка сжимается к корню,
    // поэтому на границе дальности нет «выскакивания» и альфа-блендинга.
    float patch = SampleGrassNoise(rootWS.xz * _PatchScale);
    float distanceToCamera = distance(rootWS, _WorldSpaceCameraPos);
    float fade = saturate((_GrassFade.y - distanceToCamera) / max(_GrassFade.y - _GrassFade.x, 1e-3));
    offset *= lerp(_PatchMinHeight, 1.0, patch) * fade;

    // Ветер: бегущий шум даёт волны порывов по полю, синус — мелкое покачивание
    // с индивидуальной фазой травинки (blade.z), поперечная составляющая — трепет.
    float2 direction = _GrassWind.xy;
    float gust = SampleGrassNoise(rootWS.xz * _GrassWindParams.x - direction * (_Time.y * _GrassWind.w));
    float phase = input.blade.z * 6.2831853;
    float sway = sin(_Time.y * _GrassWindParams.z + phase) * _GrassWindParams.y;
    float flutter = sin(_Time.y * _GrassWindParams.z * 1.7 + input.blade.w * 6.2831853) * _GrassWindParams.y * 0.5;
    float2 bend = direction * (gust * _GrassWind.z + sway * (0.4 + gust)) + float2(-direction.y, direction.x) * flutter;

    // Отталкивание от персонажа или объекта.
    float2 away = rootWS.xz - _GrassInteractor.xz;
    float awayLength = length(away);
    float push = saturate(1.0 - awayLength / max(_GrassInteractor.w, 1e-3));
    bend += away / max(awayLength, 1e-3) * (push * push * _GrassWindParams.w);

    // Изгиб растёт к кончику (h * высота вершины), корень неподвижен.
    // Высота уменьшается так, чтобы длина травинки примерно сохранялась.
    float vertexHeight = max(offset.y, 1e-4);
    float2 move = bend * (_Bendiness * h * vertexHeight);
    offset.xz += move;
    offset.y *= rsqrt(1.0 + dot(move, move) / (vertexHeight * vertexHeight));

    GrassVertex output;
    output.positionWS = rootWS + offset;
    // Нормаль ближе к вертикали: поле освещается ровно, без «шахматки» от граней травинок.
    output.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0.0, 1.0, 0.0), 0.65));
    output.height01 = h;
    output.random = input.blade.zw;
    output.patch = patch;
    return output;
}

#endif
