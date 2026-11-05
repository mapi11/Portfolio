// Оцифровка любого меша: всё считается в мировых координатах, UV модели нужны только для её обычной текстуры
Shader "Portfolio/Digitize"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3

        [Header(Digital)]
        [HDR] _DigitalColor ("Lines and Edge", Color) = (0.13, 0.59, 0.95, 1)
        _DarkColor ("Dark", Color) = (0.1, 0.1, 0.1, 1)
        _MidColor ("Mid", Color) = (0.13, 0.13, 0.13, 1)
        _LineColor ("Noise Blocks", Color) = (0.38, 0.38, 0.38, 1)
        _GridScale ("Grid Cells per Meter", Float) = 10
        _GlitchStrength ("Glitch Strength", Range(0, 0.3)) = 0.08
        _Pixelate ("Pixelate", Range(0, 1)) = 0.45
        [Toggle] _ForceDigital ("Always Digital", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "DigitizeCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 originWS : TEXCOORD2;   // до глитча: узор и граница не дрожат вместе с геометрией
                half3 normalWS : TEXCOORD3;
                half4 tangentWS : TEXCOORD4;
                half fog : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 originWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionWS = GlitchVertex(originWS, DigitizeAmount(originWS));
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = positionWS;
                output.originWS = originWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Линия сетки на каждом целом значении v·_GridScale, сглаженная по экрану
            half GridLine(float v)
            {
                float x = v * _GridScale;
                float d = 0.5 - abs(frac(x) - 0.5);
                return 1.0 - smoothstep(0.0, fwidth(x) * 1.2 + 0.02, d);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = input.originWS;
                float amount = DigitizeAmount(p);

                // обычный вид модели
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 vertexNormal = normalize(input.normalWS);
                half3 tangent = normalize(input.tangentWS.xyz);
                half3 bitangent = cross(vertexNormal, tangent) * input.tangentWS.w;
                half3 normalWS = normalize(mul(normalTS, half3x3(tangent, bitangent, vertexNormal)));

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = light.shadowAttenuation;
                half ndl = saturate(dot(normalWS, light.direction));
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half spec = pow(saturate(dot(normalWS, normalize(light.direction + viewDir))), exp2(10.0 * _Smoothness + 1.0)) * _Smoothness;
                half3 lit = albedo.rgb * (light.color * ndl * shadow + SampleSH(normalWS)) + light.color * spec * shadow;

                // переход блоками: каждая ячейка сетки «переключается» на своём пороге, у порога — светящийся край
                float threshold = Hash31(floor(p * _GridScale * 0.5));
                float level = amount * 1.15;
                half digital = step(threshold, level) * step(0.001, amount);
                half edge = saturate(1.0 - abs(level - threshold) / 0.07) * step(0.001, amount);

                // цифровой вид: тёмная поверхность, синяя сетка в трёх проекциях, развёртка и мерцающие блоки
                half3 w = abs(vertexNormal);
                w /= w.x + w.y + w.z;
                half lines = w.x * max(GridLine(p.y), GridLine(p.z))
                           + w.y * max(GridLine(p.x), GridLine(p.z))
                           + w.z * max(GridLine(p.x), GridLine(p.y));
                half sweep = pow(frac(p.y * 1.5 - _Time.y * 0.6), 24.0);
                half blocks = step(0.92, Hash31(floor(p * _GridScale * 0.5) + floor(_Time.y * 6.0)));
                half rim = pow(1.0 - saturate(dot(vertexNormal, viewDir)), 3.0);
                half scanline = 0.85 + 0.15 * sin(input.positionCS.y * 1.6);

                half3 digitalColor = lerp(_DarkColor.rgb, _MidColor.rgb, ndl * shadow);
                digitalColor = lerp(digitalColor, _LineColor.rgb, blocks * 0.7);
                digitalColor += _DigitalColor.rgb * (lines * 0.9 + rim * 1.1 + sweep * 1.2);
                digitalColor *= scanline;

                half3 color = lerp(lit, digitalColor, digital) + _DigitalColor.rgb * edge * 3.0;
                color = MixFog(color, input.fog);
                return half4(color, 1.0);
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
            #pragma target 3.0

            #include "DigitizeCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 originWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionWS = GlitchVertex(originWS, DigitizeAmount(originWS));
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, TransformObjectToWorldNormal(input.normalOS), _LightDirection));
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
            #pragma target 3.0

            #include "DigitizeCommon.hlsl"

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                float3 originWS = TransformObjectToWorld(positionOS.xyz);
                return TransformWorldToHClip(GlitchVertex(originWS, DigitizeAmount(originWS)));
            }

            half Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
