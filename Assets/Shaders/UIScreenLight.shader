// 画面全体にかける「光の膜」。いちばん上の Canvas に全画面の RawImage として置き、
// 下に描かれた画面の色に明るさを掛け合わせる（Blend DstColor SrcColor = 2倍の乗算）。
// 光は画面の上の方を左右にゆっくり揺れる。明るさの曲線は Pixel Art Lighter の試し
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
        _SwayPeriod ("Left-Right Period (sec)", Float) = 7
        _BobPeriod ("Up-Down Period (sec)", Float) = 3.5
        _Center ("Path Center (x, y from top)", Vector) = (0.5, 0.34, 0, 0)
        _Amplitude ("Path Amplitude (x, y)", Vector) = (0.42, 0.16, 0, 0)
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
            float _SwayPeriod;
            float _BobPeriod;
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
                float t = _Time.y;
                float lx = _Center.x + _Amplitude.x * sin(6.2831853 * t / max(_SwayPeriod, 0.01));
                float lyTop = _Center.y + _Amplitude.y * sin(6.2831853 * t / max(_BobPeriod, 0.01) + 1.0);
                float2 light = float2(lx, 1.0 - lyTop);

                // 800x600 基準の px に直す（横は画面の縦横比に合わせる）
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d = (IN.uv - light) * float2(600.0 * aspect, 600.0);
                float dist = length(d);

                float glow = exp(-pow(dist / max(_Radius, 1.0), _Sharpness));
                float3 factor = _Ambient + _Peak * glow * _LightColor.rgb;
                factor = lerp(float3(1, 1, 1), factor, _Strength);

                return fixed4(saturate(factor * 0.5), 1);
            }
            ENDCG
        }
    }
}
