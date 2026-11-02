Shader "Portfolio/VFX/Instanced"
{
    Properties
    {
        [MainTexture] _BaseMap ("Atlas", 2D) = "white" {}
        [MainColor][HDR] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Additive ("Additive (0 = alpha, 1 = add)", Range(0, 1)) = 0
        _OffsetFactor ("Depth Offset Factor", Float) = -1
        _OffsetUnits ("Depth Offset Units", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset [_OffsetFactor], [_OffsetUnits]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing

            #include "PremultipliedVFX.hlsl"

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float, _FadeOut)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.alpha = 1.0h - UNITY_ACCESS_INSTANCED_PROP(PerInstance, _FadeOut);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return PremultipliedColor(input.uv, half4(1, 1, 1, input.alpha));
            }
            ENDHLSL
        }
    }
}
