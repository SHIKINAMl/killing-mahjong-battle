Shader "Custom/RetroCRT"
{
    Properties
    {
        _BlitTexture ("Texture", 2D) = "white" {}
        [Header(Distortion)]
        _Curvature ("Curvature", Range(0, 5)) = 3.5
        _VignetteWidth ("Vignette Width", Range(0, 50)) = 15.0
        
        [Header(Scanlines and Noise)]
        _ScanlineOpacity ("Scanline Opacity", Range(0, 1)) = 0.5
        _NoiseOpacity ("Noise Opacity", Range(0, 1)) = 0.15
        
        [Header(Coloring)]
        _Brightness ("Brightness", Range(0, 2)) = 1.0
        _Contrast ("Contrast", Range(0, 2)) = 1.2
        _GreenTint ("Green Tint", Color) = (0.8, 1.0, 0.8, 1.0)
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.02)) = 0.003
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "BlitPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // SRP Batcher compatible Constant Buffer
            CBUFFER_START(UnityPerMaterial)
                float _Curvature;
                float _VignetteWidth;
                float _ScanlineOpacity;
                float _NoiseOpacity;
                float _Brightness;
                float _Contrast;
                float4 _GreenTint;
                float _ChromaticAberration;
            CBUFFER_END

            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            float2 CurveUVs(float2 uv)
            {
                uv = uv * 2.0 - 1.0;
                float2 offset = abs(uv.yx) / float2(_Curvature, _Curvature);
                uv = uv + uv * offset * offset;
                uv = uv * 0.5 + 0.5;
                return uv;
            }

            half4 Frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); 
                
                float2 originalUV = input.texcoord;
                float2 uv = CurveUVs(originalUV);

                if(uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return half4(0, 0, 0, 1);
                }

                float2 uvR = uv + float2(_ChromaticAberration, 0.0);
                float2 uvB = uv - float2(_ChromaticAberration, 0.0);
                
                // Use URP native SAMPLE_TEXTURE2D_X and sampler_LinearClamp included from Blit.hlsl
                half colR = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvR).r;
                half colG = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).g;
                half colB = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvB).b;
                half4 col = half4(colR, colG, colB, 1.0);

                col.rgb = (col.rgb - 0.5) * _Contrast + 0.5;
                col.rgb *= _Brightness;
                col.rgb *= _GreenTint.rgb;

                float scanlines = 0.5 + 0.5 * sin(_Time.y * 10.0 + uv.y * 800.0);
                col.rgb -= col.rgb * (scanlines * _ScanlineOpacity);

                float noise = rand(uv + _Time.y);
                col.rgb += (noise - 0.5) * _NoiseOpacity;

                uv = uv * 2.0 - 1.0;
                float vignette = 1.0 - smoothstep(0.5, 1.5, length(uv) * 1.2);
                col.rgb *= vignette;
                
                float edgeVignette = smoothstep(0.0, _VignetteWidth / 100.0, 1.0 - abs(originalUV.x * 2.0 - 1.0)) * 
                                     smoothstep(0.0, _VignetteWidth / 100.0, 1.0 - abs(originalUV.y * 2.0 - 1.0));
                col.rgb *= edgeVignette;

                return col;
            }
            ENDHLSL
        }
    }
}
