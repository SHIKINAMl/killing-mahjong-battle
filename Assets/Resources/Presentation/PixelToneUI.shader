Shader "UI/KillingMahjong/PixelTone"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _PixelSize ("Source pixels per dot", Range(1,8)) = 2
        _ColorSteps ("Color steps", Range(2,32)) = 8
        _SilhouetteOnly ("Keep only black silhouette", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 localPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _ClipRect;
            fixed4 _Color;
            float _PixelSize, _ColorSteps, _SilhouetteOnly;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.localPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }
            float Bayer2(float2 p) { return 2 * p.x + 3 * p.y - 4 * p.x * p.y; }

            fixed4 frag(v2f input) : SV_Target
            {
                // 元画像の座標で揃えるため、目の切り抜きも背景と同じドットになる。
                float2 dot = floor(input.uv * _MainTex_TexelSize.zw / _PixelSize);
                float2 uv = (dot + .5) * _PixelSize * _MainTex_TexelSize.xy;
                fixed4 c = tex2D(_MainTex, uv);
                float2 p = fmod(dot, 2);
                float threshold = (Bayer2(p) + .5) / 4.0;
                // 静止した市松模様（Bayer 2x2）で階調をつなぐ。黒は黒のまま、ちらつかせない。
                float value = max(c.r, max(c.g, c.b));
                float tone = saturate(floor(value * _ColorSteps + threshold) / _ColorSteps);
                // RGBを別々に丸めると黄色い点が出るため、元の色相を維持する。
                c.rgb *= tone / max(value, .00001);
                // 元絵の黒い人物・卓だけを残し、別レイヤーの背景へ重ねられる。
                if (_SilhouetteOnly > .5)
                    c = fixed4(0, 0, 0, c.a * (1 - step(.02, value)));
                c *= input.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(input.localPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - .001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
