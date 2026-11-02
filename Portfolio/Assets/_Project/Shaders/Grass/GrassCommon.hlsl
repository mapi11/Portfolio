#ifndef PORTFOLIO_GRASS_COMMON_INCLUDED
#define PORTFOLIO_GRASS_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_GrassWindNoise);
SAMPLER(sampler_GrassWindNoise);
float4 _GrassWind;
float4 _GrassWindParams;
float4 _GrassInteractor;
float4 _GrassFade;

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
    float2 uv : TEXCOORD0;
    float4 blade : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GrassVertex
{
    float3 positionWS;
    float3 normalWS;
    float height01;
    float2 random;
    float patch;
};

float SampleGrassNoise(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_GrassWindNoise, sampler_GrassWindNoise, uv, 0).r;
}

GrassVertex DisplaceGrass(GrassAttributes input)
{
    float3 rootWS = TransformObjectToWorld(float3(input.blade.x, 0.0, input.blade.y));
    float3 offset = TransformObjectToWorld(input.positionOS.xyz) - rootWS;
    float h = input.uv.y;

    float patch = SampleGrassNoise(rootWS.xz * _PatchScale);
    float distanceToCamera = distance(rootWS, _WorldSpaceCameraPos);
    float fade = saturate((_GrassFade.y - distanceToCamera) / max(_GrassFade.y - _GrassFade.x, 1e-3));
    // вдали травинка сжимается к корню вместо альфа-фейда
    offset *= lerp(_PatchMinHeight, 1.0, patch) * fade;

    float2 direction = _GrassWind.xy;
    float gust = SampleGrassNoise(rootWS.xz * _GrassWindParams.x - direction * (_Time.y * _GrassWind.w));
    float phase = input.blade.z * 6.2831853;
    float sway = sin(_Time.y * _GrassWindParams.z + phase) * _GrassWindParams.y;
    float flutter = sin(_Time.y * _GrassWindParams.z * 1.7 + input.blade.w * 6.2831853) * _GrassWindParams.y * 0.5;
    float2 bend = direction * (gust * _GrassWind.z + sway * (0.4 + gust)) + float2(-direction.y, direction.x) * flutter;

    float2 away = rootWS.xz - _GrassInteractor.xz;
    float awayLength = length(away);
    float push = saturate(1.0 - awayLength / max(_GrassInteractor.w, 1e-3));
    bend += away / max(awayLength, 1e-3) * (push * push * _GrassWindParams.w);

    // кончик гнём сильнее корня и укорачиваем по высоте, чтобы длина травинки не росла
    float vertexHeight = max(offset.y, 1e-4);
    float2 move = bend * (_Bendiness * h * vertexHeight);
    offset.xz += move;
    offset.y *= rsqrt(1.0 + dot(move, move) / (vertexHeight * vertexHeight));

    GrassVertex output;
    output.positionWS = rootWS + offset;
    output.normalWS = normalize(lerp(TransformObjectToWorldNormal(input.normalOS), float3(0.0, 1.0, 0.0), 0.65));
    output.height01 = h;
    output.random = input.blade.zw;
    output.patch = patch;
    return output;
}

#endif
