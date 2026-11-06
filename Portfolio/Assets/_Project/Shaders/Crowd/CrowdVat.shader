// Толпа на текстурах анимации (VAT): позы запечены в текстуру, скиннинга и Animator нет,
// весь отряд рисуется инстансингом. Фаза шага и оттенок у каждого свои — из хэша радиуса его дорожки
Shader "Portfolio/Crowd VAT"
{
    Properties
    {
        [NoScaleOffset] _PositionMap ("Positions (object space)", 2D) = "black" {}
        [NoScaleOffset] _NormalMap ("Normals", 2D) = "black" {}
        _Frames ("Frames", Float) = 30
        _FrameRate ("Frame Rate", Float) = 30
        _BaseColor ("Color", Color) = (0.38, 0.38, 0.38, 1)
        _Variation ("Brightness Variation", Range(0, 0.5)) = 0.18
        _RimColor ("Rim (VAT mode)", Color) = (0.13, 0.59, 0.95, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.45
        _RimPower ("Rim Power", Range(1, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_PositionMap);
        TEXTURE2D(_NormalMap);
        // sampler_PointClamp объявлен в URP глобально

        CBUFFER_START(UnityPerMaterial)
            float _Frames;
            float _FrameRate;
            half4 _BaseColor;
            half _Variation;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
        CBUFFER_END

        // вспышка при включении режима, 1 -> 0 за полсекунды; задаёт CrowdDemo через MaterialPropertyBlock
        half _ModeFlash;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 vertexU : TEXCOORD1;     // x — столбец вершины в текстурах анимации
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // целочисленный хэш: соседние ключи дают совершенно разные значения 0..1
        float AgentHash(uint h)
        {
            h ^= h >> 16;
            h *= 0x7feb352du;
            h ^= h >> 15;
            h *= 0x846ca68bu;
            h ^= h >> 16;
            return h * (1.0 / 4294967296.0);
        }

        // поза вершины: два соседних кадра и смешивание между ними, чтобы при 30 кадрах цикл не дёргался
        void SampleVat(Attributes input, out float3 positionOS, out float3 normalOS, out float random)
        {
            // Ключ агента — радиус его дорожки (CrowdDemo делает его кратным 1/4096 и уникальным).
            // Позицию брать нельзя: агент идёт, позиция меняется каждый кадр, и фаза прыгала бы
            float3 origin = UNITY_MATRIX_M._m03_m13_m23;
            random = AgentHash((uint)floor(length(origin.xz) * 4096.0 + 0.5));
            float time = _Time.y * _FrameRate + random * _Frames;
            float frame0 = fmod(floor(time), _Frames);
            float frame1 = fmod(frame0 + 1.0, _Frames);
            float blend = frac(time);

            float u = input.vertexU.x;
            float2 uv0 = float2(u, (frame0 + 0.5) / _Frames);
            float2 uv1 = float2(u, (frame1 + 0.5) / _Frames);
            positionOS = lerp(SAMPLE_TEXTURE2D_LOD(_PositionMap, sampler_PointClamp, uv0, 0).xyz,
                              SAMPLE_TEXTURE2D_LOD(_PositionMap, sampler_PointClamp, uv1, 0).xyz, blend);
            normalOS = normalize(lerp(SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_PointClamp, uv0, 0).xyz,
                                      SAMPLE_TEXTURE2D_LOD(_NormalMap, sampler_PointClamp, uv1, 0).xyz, blend));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half random : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS, normalOS;
                float random;
                SampleVat(input, positionOS, normalOS, random);

                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.random = random;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half ndl = saturate(dot(normal, light.direction));

                // у каждого чуть своя яркость серого, чтобы толпа не выглядела копией одного манекена
                half3 albedo = _BaseColor.rgb * (1.0 + (frac(input.random * 7.31) - 0.5) * 2.0 * _Variation);
                half3 color = albedo * (light.color * ndl * light.shadowAttenuation + SampleSH(normal));
                // тонкий синий контур по краю силуэта: сразу видно, что включён VAT-режим
                half rim = pow(1.0 - saturate(dot(normal, GetWorldSpaceNormalizeViewDir(input.positionWS))), _RimPower);
                color += _RimColor.rgb * rim * (_RimStrength + _ModeFlash * 2.0) + _RimColor.rgb * _ModeFlash * 0.15;
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS, normalOS;
                float random;
                SampleVat(input, positionOS, normalOS, random);
                float3 positionWS = TransformObjectToWorld(positionOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, TransformObjectToWorldNormal(normalOS), _LightDirection));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 Frag() : SV_Target
            {
                return 0;
            }
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
            #pragma target 3.5
            #pragma multi_compile_instancing

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionOS, normalOS;
                float random;
                SampleVat(input, positionOS, normalOS, random);
                return TransformObjectToHClip(positionOS);
            }

            half Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
