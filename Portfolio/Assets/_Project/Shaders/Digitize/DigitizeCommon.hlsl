#ifndef PORTFOLIO_DIGITIZE_COMMON_INCLUDED
#define PORTFOLIO_DIGITIZE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// След шара: xyz — точка, w — текущий радиус (0 — слот пуст). Ставит DigitizeBall
float4 _DigitizeTrail[8];

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _BumpScale;
    half _Smoothness;
    half4 _DigitalColor;
    half4 _DarkColor;
    half4 _MidColor;
    half4 _LineColor;
    float _GridScale;
    half _GlitchStrength;
    half _ForceDigital;
    half _Pixelate;
CBUFFER_END

float Hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

// Насколько точка оцифрована: максимум по точкам следа, край слегка «рваный»
float DigitizeAmount(float3 positionWS)
{
    float amount = _ForceDigital;
    float wobble = Hash31(floor(positionWS * 6.0) + floor(_Time.y * 8.0)) * 0.25;
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float4 trail = _DigitizeTrail[i];
        float d = distance(positionWS, trail.xyz) / max(trail.w, 1e-4);
        amount = max(amount, trail.w > 0.0 ? 1.0 - smoothstep(0.3, 1.0, d + wobble) : 0.0);
    }
    return amount;
}

// Глитч геометрии: горизонтальные срезы рывками уезжают в сторону, вершины тянутся к пиксельной сетке
float3 GlitchVertex(float3 positionWS, float amount)
{
    if (amount <= 0.0)
        return positionWS;

    float tick = floor(_Time.y * 12.0);
    float slice = floor(positionWS.y * 14.0);
    float jump = Hash31(float3(slice, tick, 7.0));
    float shift = jump > 0.82 ? (Hash31(float3(slice, tick, 3.0)) - 0.5) * 8.0 * (jump - 0.82) : 0.0;
    positionWS.xz += float2(shift, shift * 0.5) * _GlitchStrength * amount;

    float cell = 1.0 / _GridScale;
    float3 snapped = round(positionWS / cell) * cell;
    return lerp(positionWS, snapped, amount * _Pixelate);
}

#endif
