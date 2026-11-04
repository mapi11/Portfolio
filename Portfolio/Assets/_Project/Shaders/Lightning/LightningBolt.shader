Shader "Portfolio/Lightning/Bolt"
{
    Properties
    {
        [HDR] _CoreColor ("Core", Color) = (5, 5.3, 7, 1)
        [HDR] _GlowColor ("Glow", Color) = (0.55, 0.65, 1.6, 1)
        [HDR] _LeaderColor ("Leader", Color) = (1.2, 1, 2.4, 1)
        _Width ("Glow Half Width (m)", Float) = 1
        _CoreWidth ("Core Half Width (m)", Float) = 0.07
        _CloudBase ("Cloud Base Height", Float) = 34
        _CloudFade ("Cloud Fade Length", Float) = 14

        [HideInInspector] _Reveal ("Leader Front", Float) = 10
        [HideInInspector] _Leader ("Leader Brightness", Float) = 0
        [HideInInspector] _Stroke ("Stroke Brightness", Float) = 0
        [HideInInspector] _BranchGlow ("Branch Glow", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Bolt"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _GlowColor;
                half4 _LeaderColor;
                float _Width;
                float _CoreWidth;
                float _CloudBase;
                float _CloudFade;
                float _Reveal;
                half _Leader;
                half _Stroke;
                half _BranchGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 tangentOS : NORMAL;     // направление канала
                float4 uv : TEXCOORD0;          // x сторона ленты, y время прихода лидера, z метры вдоль канала, w ветка
                half4 color : COLOR;            // r ширина, g яркость
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                half4 data : TEXCOORD1;         // x яркость, y доля ядра в ширине ленты, z выход из облака
                half fog : TEXCOORD2;
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Noise1D(float x)
            {
                float i = floor(x);
                float f = frac(x);
                return lerp(Hash11(i), Hash11(i + 1.0), f * f * (3.0 - 2.0 * f));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 tangentWS = normalize(TransformObjectToWorldDir(input.tangentOS));
                float3 toCamera = GetCameraPositionWS() - positionWS;
                float distance = length(toCamera);
                float3 side = normalize(cross(tangentWS, toCamera / distance));

                // размер пикселя в метрах на этой дистанции: вдали канал не должен становиться тоньше пикселя и мерцать
                float pixel = distance * 2.0 / (UNITY_MATRIX_P._m11 * _ScreenParams.y);
                float halfWidth = max(_Width * input.color.r, pixel * 8.0);
                float core = max(_CoreWidth * input.color.r, pixel * 0.8);

                positionWS += side * input.uv.x * halfWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.data = half4(input.color.g, core / halfWidth, saturate((_CloudBase - positionWS.y) / _CloudFade), 0);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float d = abs(input.uv.x);
                float coreFrac = input.data.y;
                float aa = fwidth(d);

                half core = 1.0 - smoothstep(coreFrac - aa, coreFrac + aa, d);
                // плотное свечение плазмы вокруг ядра и широкий ореол в дожде
                half glow = exp(-d / (coreFrac * 3.0)) * 0.9 + exp(-d * 8.0) * 0.1 * (1.0 - d);

                // ступенчатый лидер: виден канал до фронта, у самого кончика ярче
                float arrival = input.uv.y;
                half shown = step(arrival, _Reveal);
                half tip = exp(-max(_Reveal - arrival, 0.0) * 25.0);
                half leader = _Leader * (0.15 + tip) * shown;

                // главный удар: ветки гаснут быстрее основного канала
                half stroke = _Stroke * lerp(1.0, _BranchGlow, input.uv.w);
                half flicker = 0.7 + 0.6 * Noise1D(input.uv.z * 0.4 + _Time.y * 31.0);
                half energy = input.data.x * input.data.z * flicker;

                half3 color = (_CoreColor.rgb * core + _GlowColor.rgb * glow) * stroke
                            + (_LeaderColor.rgb * (core * 2.0 + glow * 0.5)) * leader;
                color *= energy;
                color = MixFogColor(color, half3(0, 0, 0), input.fog);
                return half4(color, 0);
            }
            ENDHLSL
        }
    }
}
