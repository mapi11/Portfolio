Shader "Portfolio/Lightning/Scorch"
{
    Properties
    {
        _MainTex ("Atlas (R branches, G heat halo, B char)", 2D) = "black" {}
        _CharStrength ("Char Strength", Range(0, 1)) = 0.88
        _CharColor ("Char Tint", Color) = (0.035, 0.03, 0.028, 1)
        _AshColor ("Ash Tint", Color) = (0.3, 0.285, 0.27, 1)
        _EmberIntensity ("Ember Intensity", Float) = 5
        _CoolingFromTips ("Cooling From Tips", Range(0, 2)) = 1.1
        _FlickerScale ("Flicker Scale", Float) = 3.5

        [HideInInspector] _Heat ("Heat", Float) = 1
        [HideInInspector] _Fade ("Fade", Float) = 1
        [HideInInspector] _AtlasRect ("Atlas Rect", Vector) = (0.5, 0.5, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Scorch"
            Tags { "LightMode" = "UniversalForward" }

            // premultiplied: альфа перекрывает землю гарью и пеплом, rgb добавляет свечение углей — один проход
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half _CharStrength;
                half4 _CharColor;
                half4 _AshColor;
                half _EmberIntensity;
                half _CoolingFromTips;
                float _FlickerScale;
                half _Heat;
                half _Fade;
                float4 _AtlasRect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 positionXZ : TEXCOORD1;
                half fog : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            // Цвет раскалённого тела: тёмно-красный → оранжевый → жёлто-белый
            half3 Blackbody(half t)
            {
                half3 color = lerp(half3(0.55, 0.04, 0.01), half3(1.0, 0.32, 0.05), smoothstep(0.0, 0.45, t));
                return lerp(color, half3(1.0, 0.82, 0.5), smoothstep(0.55, 1.0, t));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv * _AtlasRect.xy + _AtlasRect.zw;
                output.positionXZ = positionWS.xz;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half branches = mask.r;
                half halo = mask.g;
                half charMask = mask.b;

                half alpha = saturate(charMask * _CharStrength + branches * 0.85) * _Fade;

                // тонкие кончики остывают первыми, у центра и толстых веток жар держится дольше
                half heat = saturate(_Heat * (1.0 + _CoolingFromTips) - (1.0 - halo) * _CoolingFromTips);
                float2 p = input.positionXZ * _FlickerScale;
                float t = _Time.y;
                half flicker = ValueNoise(p + float2(t * 1.3, -t * 0.7)) * 0.7 + ValueNoise(p * 3.1 - t * 2.3) * 0.3;
                flicker = lerp(0.35, 1.4, flicker);

                half glow = branches * heat * heat * flicker + halo * _Heat * _Heat * _Heat * 0.25;
                half3 ember = Blackbody(heat * lerp(0.75, 1.1, flicker)) * glow * _EmberIntensity * _Fade;

                // гарь и пепел освещаются как земля: луной и вспышкой через основной свет и SH
                Light mainLight = GetMainLight();
                half3 lighting = mainLight.color * saturate(mainLight.direction.y) + SampleSH(half3(0, 1, 0));
                half3 surface = lerp(_CharColor.rgb, _AshColor.rgb, saturate(branches * 1.4 - charMask * 0.2)) * lighting;
                half3 color = surface * alpha + ember;
                color = MixFogColor(color, unity_FogColor.rgb * alpha, input.fog);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
