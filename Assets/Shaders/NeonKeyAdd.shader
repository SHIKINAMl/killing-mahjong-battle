// 指定した色の部分だけを、光を「足して」明るくするシェーダ（2026-09-30）。
//
// **`Image.color` や `SpriteRenderer.color` では明るくできない。** あれは絵に色を
// 掛け算するので、白(1,1,1)より明るい方向へは動かない。血や髪を蛍光色に光らせたいのに、
// 同じ絵を色付きで重ねても「暗く染まる」だけだった（2026-09-30 に実測で確認。
// 血の色が (248,126,126) から (247,118,118) にしか動かなかった）。
//
// ここでは Blend One One（加算）で、下に描いてある絵へ光を足す。
//
// **_KeyColor と _Tolerance で「どこを光らせるか」を選べる。**
//   - 血のように絵の全部を光らせたいときは _Tolerance を 2 にする（全画素が一致する）
//   - 髪の黄色だけを光らせたいときは _KeyColor に髪の色、_Tolerance に 0.27 くらいを入れる
//     （0〜1 に正規化した RGB 空間での距離。実測では 255 階調で 70 が髪だけを拾えた）
//
// 濃さは頂点カラーのアルファ（＝Image.color.a / SpriteRenderer.color.a）で調整する。
Shader "KillingMahjong/NeonKeyAdd"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NeonColor ("Neon Color", Color) = (1, 0.33, 0.38, 1)
        _KeyColor ("Key Color", Color) = (1,1,1,1)
        _Tolerance ("Tolerance", Range(0, 2)) = 2
        _Intensity ("Intensity", Range(0, 4)) = 1

        // UI から使うときに Unity が要求する項目。無いと Image のマテリアルとして弾かれる
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
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
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
        // **加算。** ここが本体。下の絵へ光を足すので、白より明るくできる
        Blend One One
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
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _NeonColor;
            fixed4 _KeyColor;
            float _Tolerance;
            float _Intensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);

                // 指定色にどれだけ近いか。近い所だけ光らせる
                float d = distance(tex.rgb, _KeyColor.rgb);
                float hit = step(d, _Tolerance);

                // 透明な所は光らせない。濃さは頂点カラーのアルファで決める
                float a = tex.a * IN.color.a * hit;

                // 加算なので、アルファは 0 のままでよい（足す色だけを返す）
                return fixed4(_NeonColor.rgb * _Intensity * a, 0);
            }
            ENDCG
        }
    }

    Fallback Off
}
