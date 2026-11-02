// Cartoon-лёд для мобильных: непрозрачный, без depth/opaque texture и без отражений.
// Глубину изображают слои трещин и пузырьков, сдвинутые параллаксом от направления
// взгляда (мировые XZ как UV — лёд горизонтальный, касательные не нужны).
// Иней у берегов — по UV.y поперёк русла, который пишет генератор меша реки.
Shader "Portfolio/Toon/Ice"
{
    Properties
    {
        [NoScaleOffset] _CrackMap ("Cracks (R lines, G bubbles)", 2D) = "black" {}
        [NoScaleOffset] _NoiseMap ("Noise (R smooth)", 2D) = "gray" {}

        [Header(Color)]
        _DeepColor ("Deep", Color) = (0.12, 0.42, 0.62, 1)
        _ShallowColor ("Shallow", Color) = (0.55, 0.88, 0.97, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 6)) = 2.5

        [Header(Inner Cracks)]
        _CrackScale ("Crack Scale (per meter)", Float) = 0.3
        _CrackColor ("Inner Crack Color", Color) = (0.75, 0.95, 1, 1)
        _SurfaceCrack ("Surface Crack Strength", Range(0, 1)) = 0.5
        _InnerDepth ("Inner Depth", Range(0, 1)) = 0.25
        _BubbleStrength ("Bubbles", Range(0, 1)) = 0.35
        _StreakStrength ("Highlight Streaks", Range(0, 1)) = 0.35
        _StreakFrequency ("Streak Frequency", Float) = 0.9

        [Header(Frost)]
        _FrostColor ("Frost", Color) = (0.93, 0.97, 1, 1)
        _FrostWidth ("Frost Width", Range(0, 0.5)) = 0.16

        [Header(Light)]
        _ShadowColor ("Shadow Tint", Color) = (0.55, 0.65, 0.9, 1)
        _SpecColor ("Highlight", Color) = (1, 1, 1, 1)
        _Glossiness ("Glossiness", Range(8, 256)) = 90
        _SpecThreshold ("Highlight Threshold", Range(0.01, 1)) = 0.5
        _AmbientStrength ("Ambient", Range(0, 2)) = 0.5
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

        TEXTURE2D(_CrackMap);
        SAMPLER(sampler_CrackMap);
        TEXTURE2D(_NoiseMap);
        SAMPLER(sampler_NoiseMap);

        CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor;
            half4 _ShallowColor;
            half _FresnelPower;
            float _CrackScale;
            half4 _CrackColor;
            half _SurfaceCrack;
            half _InnerDepth;
            half _BubbleStrength;
            half _StreakStrength;
            float _StreakFrequency;
            half4 _FrostColor;
            half _FrostWidth;
            half4 _ShadowColor;
            half4 _SpecColor;
            half _Glossiness;
            half _SpecThreshold;
            half _AmbientStrength;
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
                float2 uv : TEXCOORD0;      // x — метры вдоль русла, y — 0..1 поперёк
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half Crack(float2 uv) { return SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, uv).r; }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Цвет толщи: на скользящих углах лёд светлее (как отражение неба).
                half fresnel = pow(1.0h - saturate(dot(normal, view)), _FresnelPower);
                half3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(fresnel + 0.2h));

                // Параллакс: точка «под поверхностью» на глубине d видна смещённой
                // против проекции взгляда. Три слоя на разной глубине дают объём.
                float2 uv = input.positionWS.xz * _CrackScale;
                float2 parallax = -view.xz / max(view.y, 0.25h) * (_InnerDepth * _CrackScale);
                half deep = Crack(uv * 1.7 + parallax * 2.0 + 0.37);
                half middle = Crack(uv + parallax);
                // Пузырьки островками: маска из крупного шума, иначе они рябят по всей реке.
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.positionWS.xz * 0.8).r;
                half bubbleMask = smoothstep(0.55h, 0.75h, SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.positionWS.xz * 0.12).r);
                half bubbles = SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, uv * 2.3 + parallax * 0.6).g * bubbleMask;
                color += _CrackColor.rgb * (deep * 0.12h + middle * 0.3h) + bubbles * _BubbleStrength;
                color = lerp(color, _FrostColor.rgb, Crack(uv * 0.6) * _SurfaceCrack);

                // Мультяшные блики-полосы: пара диагональных белых штрихов, как на нарисованном льду.
                half streakWave = sin(dot(input.positionWS.xz, float2(0.55, 0.35)) * _StreakFrequency + noise * 2.0h);
                half streaks = smoothstep(0.86h, 0.9h, streakWave) + smoothstep(0.975h, 0.985h, streakWave) * 0.6h;
                color += streaks * _StreakStrength * (0.4h + fresnel);

                // Иней у берегов с рваным краем.
                half bank = min(input.uv.y, 1.0h - input.uv.y) + (noise - 0.5h) * 0.08h;
                half frost = 1.0h - smoothstep(_FrostWidth * 0.4h, _FrostWidth, bank);
                color = lerp(color, _FrostColor.rgb, frost);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = light.shadowAttenuation * light.distanceAttenuation;
                half lit = saturate(dot(normal, light.direction)) > 0.0h ? shadow : 0.0h;
                color *= lerp(_ShadowColor.rgb, 1.0h, lit) * light.color + SampleSH(normal) * _AmbientStrength;

                // Мультяшный блик: жёсткая граница вместо плавного Блинн-Фонга.
                half3 halfVector = normalize(light.direction + view);
                half specular = pow(saturate(dot(normal, halfVector)), _Glossiness);
                specular = smoothstep(_SpecThreshold - 0.03h, _SpecThreshold + 0.03h, specular) * lit * (1.0h - frost);
                color += _SpecColor.rgb * specular;

                color = MixFog(color, input.fog);
                return half4(color, 1.0h);
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
