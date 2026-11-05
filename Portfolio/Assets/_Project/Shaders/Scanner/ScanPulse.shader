// Полноэкранный проход волны сканирования (ставит ScanRendererFeature)
Shader "Hidden/Portfolio/Scan Pulse"
{
    Properties
    {
        [HDR] _EdgeColor ("Front", Color) = (0.13, 0.59, 0.95, 1)
        _ShadeColor ("Scanned Shade", Color) = (0.1, 0.1, 0.1, 1)
        _EdgeWidth ("Front Width (m)", Float) = 0.6
        _Darken ("Darken Behind Front", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ScanPulse"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            half4 _EdgeColor;
            half4 _ShadeColor;
            float _EdgeWidth;
            half _Darken;

            // ставит ScanPulse: xyz — центр волны; x — радиус фронта, y — длина хвоста, z — общая яркость
            float4 _ScanOrigin;
            float4 _ScanParams;
            float4x4 _ScanInverseViewProjection; // ставит ScanRendererFeature

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (depth < 1e-5) return scene;
                #else
                    if (depth > 1.0 - 1e-5) return scene;
                #endif

                float3 positionWS = ComputeWorldSpacePosition(uv, depth, _ScanInverseViewProjection);
                float behind = _ScanParams.x - distance(positionWS, _ScanOrigin.xyz);
                if (behind < -_EdgeWidth)
                    return scene;

                // фронт — узкая яркая полоса, хвост за ним тянется на _ScanParams.y метров
                half front = exp(-behind * behind / (_EdgeWidth * _EdgeWidth) * 3.0);
                half trail = saturate(1.0 - behind / _ScanParams.y) * step(0.0, behind);

                half3 color = scene.rgb;
                color = lerp(color, color * 0.5 + _ShadeColor.rgb, trail * _Darken);
                // за фронтом — слабое синее свечение, которое гаснет вместе с хвостом
                color += _EdgeColor.rgb * (front * 2.5 + trail * trail * 0.12);
                return half4(lerp(scene.rgb, color, _ScanParams.z), scene.a);
            }
            ENDHLSL
        }
    }
}
