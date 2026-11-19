// 画面全体にかける「光の膜」。いちばん上の Canvas に全画面の RawImage として置き、
// 下に描かれた画面の色に明るさを掛け合わせる（Blend DstColor SrcColor = 2倍の乗算）。
// 光は画面の上の方の真ん中に置き、ろうそくの火のように少しだけ揺れて明るさがちらつく。明るさの曲線は Pixel Art Lighter の試し
// （km-docs screenshots/20261119/pixel_lighter_compare）で、元の画面と光を当てた画面の比を測って合わせた。
// 距離は 800x600 の画面を基準にした px で考え、画面の高さに合わせて伸び縮みする。
Shader "UI/ScreenLight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LightColor ("Light Color", Color) = (1,1,1,1)
        _Ambient ("Far Brightness", Range(0,1)) = 0.59
        _Peak ("Extra Brightness At Light", Range(0,1.5)) = 0.69
        _Radius ("Light Radius (px at 600 high)", Float) = 88
        _Sharpness ("Falloff Sharpness", Float) = 1.4
        _Center ("Light Position (x, y from top)", Vector) = (0.5, 0.3, 0, 0)
        _Amplitude ("Sway Amount (x, y)", Vector) = (0.012, 0.008, 0, 0)
        _Flicker ("Flicker Amount", Range(0,0.5)) = 0.12
        _Strength ("Effect Strength", Range(0,1)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

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
        // 結果 = 下の色 × 出力 × 2。出力 0.5 で変化なし、0.5 未満で暗く、0.5 より上で明るく
        Blend DstColor SrcColor
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
            };

            fixed4 _LightColor;
            float _Ambient;
            float _Peak;
            float _Radius;
            float _Sharpness;
            float _Flicker;
            float4 _Center;
            float4 _Amplitude;
            float _Strength;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // ろうそくの火: 位置はほぼ固定で、少しだけ揺れる。明るさは周期の違う波を重ねて不規則にちらつく
                float t = _Time.y;
                float2 sway = float2(
                    0.6 * sin(t * 1.7) + 0.4 * sin(t * 3.1 + 2.0),
                    0.6 * sin(t * 2.3 + 1.0) + 0.4 * sin(t * 4.3 + 0.5));
                float2 light = float2(_Center.x + _Amplitude.x * sway.x, 1.0 - (_Center.y + _Amplitude.y * sway.y));
                float flicker = 0.5 * sin(t * 7.3) + 0.3 * sin(t * 13.1 + 1.7) + 0.2 * sin(t * 23.7 + 0.4);

                // 800x600 基準の px に直す（横は画面の縦横比に合わせる）
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d = (IN.uv - light) * float2(600.0 * aspect, 600.0);
                float dist = length(d);

                float radius = max(_Radius * (1.0 + 0.5 * _Flicker * flicker), 1.0);
                float glow = exp(-pow(dist / radius, _Sharpness));
                float3 factor = _Ambient * (1.0 + 0.3 * _Flicker * flicker) + _Peak * (1.0 + _Flicker * flicker) * glow * _LightColor.rgb;
                factor = lerp(float3(1, 1, 1), factor, _Strength);

                return fixed4(saturate(factor * 0.5), 1);
            }
            ENDCG
        }
    }
}
