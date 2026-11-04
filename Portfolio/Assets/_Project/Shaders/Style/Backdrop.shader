Shader "Portfolio/Style/Backdrop"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.102, 0.102, 0.102, 1)
        _HorizonColor ("Horizon", Color) = (0.29, 0.29, 0.29, 1)
        _BottomColor ("Bottom", Color) = (0.102, 0.102, 0.102, 1)
        _Falloff ("Falloff", Range(0.5, 8)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float y = normalize(input.direction).y;
                half3 color = y >= 0.0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, 1.0 - pow(1.0 - saturate(y), _Falloff))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-y * 1.3), 0.8));

                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
