#ifndef PORTFOLIO_PREMULTIPLIED_VFX_INCLUDED
#define PORTFOLIO_PREMULTIPLIED_VFX_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"


TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Additive;
    half _OffsetFactor;
    half _OffsetUnits;
CBUFFER_END

// premultiplied alpha: _Additive = 0 обычное смешивание, 1 сложение, блендинг один и тот же
half4 PremultipliedColor(float2 uv, half4 tint)
{
    half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * tint * _BaseColor;
    color.rgb *= color.a;
    color.a *= 1.0h - _Additive;
    return color;
}

#endif
