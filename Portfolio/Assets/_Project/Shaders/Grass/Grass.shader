// Трава для мобильных: форма травинки задана геометрией, а не альфа-текстурой.
// Шейдер непрозрачный, без alpha test: на TBDR GPU (Mali, Adreno, Apple) clip()
// отключает раннюю проверку глубины и сильно повышает стоимость перерисовки.
// Без ShadowCaster: трава только принимает тень, а не отбрасывает (минус проход
// по тысячам травинок в shadow map). Жёсткие тени без soft-вариантов.
Shader "Portfolio/Grass"
{
    Properties
    {
        [Header(Color)]
        _RootColor ("Root", Color) = (0.06, 0.16, 0.04, 1)
        _TipColor ("Tip", Color) = (0.45, 0.62, 0.18, 1)
        _DryColor ("Dry", Color) = (0.62, 0.55, 0.28, 1)
        _DryAmount ("Dry Amount", Range(0, 1)) = 0.15
        _ColorVariation ("Color Variation", Range(0, 1)) = 0.25
        _RootAO ("Root Occlusion", Range(0, 1)) = 0.35

        [Header(Translucency)]
        _TranslucencyColor ("Color", Color) = (0.55, 0.75, 0.15, 1)
        _Translucency ("Strength", Range(0, 3)) = 1.2

        [Header(Shape)]
        _PatchScale ("Patch Noise Scale", Float) = 0.02
        _PatchMinHeight ("Patch Min Height", Range(0, 1)) = 0.45
        _Bendiness ("Bendiness", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog

            #include "GrassCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 data : TEXCOORD2;     // x — высота, yz — случайные, w — туман
                half patch : TEXCOORD3;
            };

            Varyings Vert(GrassAttributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                GrassVertex grass = DisplaceGrass(input);

                Varyings output;
                output.positionWS = grass.positionWS;
                output.positionCS = TransformWorldToHClip(grass.positionWS);
                output.normalWS = grass.normalWS;
                output.data = half4(grass.height01, grass.random, ComputeFogFactor(output.positionCS.z));
                output.patch = grass.patch;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half height = input.data.x;
                half2 random = input.data.yz;

                // Цвет: корень → кончик, часть травинок подсохшая, разброс яркости.
                half3 albedo = lerp(_RootColor.rgb, _TipColor.rgb, height);
                half dry = step(1.0h - _DryAmount, random.x) * height;
                albedo = lerp(albedo, _DryColor.rgb, dry);
                albedo *= 1.0h + (random.y - 0.5h) * _ColorVariation;
                // Крупные пятна: высокие поляны светлее и сочнее, низкие — темнее.
                albedo *= lerp(0.8h, 1.15h, input.patch);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = light.shadowAttenuation * light.distanceAttenuation;
                half3 normal = normalize(input.normalWS);

                // Wrap-освещение мягче Ламберта: трава не чернеет на теневой стороне.
                half wrap = saturate(dot(normal, light.direction) * 0.5h + 0.5h);

                // Просвет против солнца: дешёвая замена subsurface scattering, сильнее у кончиков.
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half back = saturate(dot(viewDirection, -light.direction));
                back *= back * height * _Translucency;

                half3 color = albedo * (light.color * (wrap * shadow) + SampleSH(normal))
                            + _TranslucencyColor.rgb * light.color * (back * shadow);
                color *= lerp(_RootAO, 1.0h, height);
                color = MixFog(color, input.data.w);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Нужен для depth prepass / depth priming (PC-рендерер) и для depth texture.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "GrassCommon.hlsl"

            float4 Vert(GrassAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformWorldToHClip(DisplaceGrass(input).positionWS);
            }

            half Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
