// Декали и трассеры через Graphics.RenderMeshInstanced: один draw call на материал.
// Depth offset вместо depth prepass/DBuffer у URP Decal Projector.
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
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return PremultipliedColor(input.uv, half4(1, 1, 1, 1));
            }
            ENDHLSL
        }
    }
}
