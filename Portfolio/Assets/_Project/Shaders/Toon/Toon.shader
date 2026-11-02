Shader "Portfolio/Toon/Lit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _NoiseMap ("Noise (R smooth, G sparkle)", 2D) = "gray" {}

        [Header(Toon Lighting)]
        _ShadowColor ("Shadow Tint", Color) = (0.45, 0.55, 0.8, 1)
        _RampThreshold ("Ramp Threshold", Range(-1, 1)) = 0.05
        _RampSmooth ("Ramp Smoothness", Range(0.001, 0.5)) = 0.06
        _AmbientStrength ("Ambient", Range(0, 2)) = 0.6
        _RimColor ("Rim Color", Color) = (0.8, 0.9, 1, 1)
        _RimPower ("Rim Power", Range(1, 8)) = 4
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35

        [Header(Snow)]
        _SnowColor ("Snow Color", Color) = (0.96, 0.98, 1, 1)
        _SnowAmount ("Snow Amount", Range(0, 1)) = 1
        _SnowThreshold ("Snow Normal Threshold", Range(-1, 1)) = 0.55
        _SnowSmooth ("Snow Edge Smoothness", Range(0.001, 0.3)) = 0.04
        _SnowNoise ("Snow Edge Noise", Range(0, 1)) = 0.35
        _SnowNoiseScale ("Snow Noise Scale", Float) = 0.6
        _SnowVariation ("Snow Tone Variation", Range(0, 0.5)) = 0.12

        [Header(Sparkle)]
        _SparkleColor ("Sparkle Color", Color) = (1, 1, 1, 1)
        _SparkleStrength ("Sparkle Strength", Range(0, 4)) = 0
        _SparkleScale ("Sparkle Scale", Float) = 4
        _SparkleThreshold ("Sparkle Threshold", Range(0.5, 1)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_NoiseMap);
        SAMPLER(sampler_NoiseMap);

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadowColor;
            half _RampThreshold;
            half _RampSmooth;
            half _AmbientStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _SnowColor;
            half _SnowAmount;
            half _SnowThreshold;
            half _SnowSmooth;
            half _SnowNoise;
            float _SnowNoiseScale;
            half _SnowVariation;
            half4 _SparkleColor;
            half _SparkleStrength;
            float _SparkleScale;
            half _SparkleThreshold;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                half fog : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half4 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.positionWS.xz * _SnowNoiseScale);

                half snowEdge = normal.y + (noise.r - 0.5h) * _SnowNoise;
                half snow = smoothstep(_SnowThreshold - _SnowSmooth, _SnowThreshold + _SnowSmooth, snowEdge);
                // альфа vertex color: может ли тут лежать снег (ствол, низ веток = 0)
                snow *= _SnowAmount * input.color.a;
                half3 snowColor = _SnowColor.rgb * lerp(1.0h - _SnowVariation, 1.0h, noise.r);
                half3 albedo = lerp(input.color.rgb * _BaseColor.rgb, snowColor, snow);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = light.shadowAttenuation * light.distanceAttenuation;
                half ramp = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, dot(normal, light.direction));
                ramp *= shadow;
                half3 lighting = lerp(_ShadowColor.rgb, 1.0h, ramp) * light.color + SampleSH(normal) * _AmbientStrength;
                half3 color = albedo * lighting;

                half rim = pow(1.0h - saturate(dot(normal, view)), _RimPower) * _RimStrength;
                color += _RimColor.rgb * rim * (0.35h + 0.65h * ramp);

                float2 sparkleUV = input.positionWS.xz * _SparkleScale;
                half sparkleA = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, sparkleUV).g;
                half sparkleB = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, sparkleUV * 1.37 + view.xz * 0.35).g;
                half sparkle = step(_SparkleThreshold, sparkleA * sparkleB * 1.6h) * snow * ramp;
                color += _SparkleColor.rgb * sparkle * _SparkleStrength;

                color = MixFog(color, input.fog);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 Vert(float4 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
