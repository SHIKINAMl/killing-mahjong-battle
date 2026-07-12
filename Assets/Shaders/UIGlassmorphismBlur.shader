Shader "UI/StylishPattern"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _LineColor ("Line Color", Color) = (1, 1, 1, 0.05)
        _LineThickness ("Line Thickness", Range(0, 1)) = 0.5
        _LineDensity ("Line Density", Range(10, 500)) = 150
        
        // UGUI必須プロパティ
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
            "CanUseSpriteAtlas"="True"
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _LineColor;
            float _LineThickness;
            float _LineDensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tintCol = tex2D(_MainTex, IN.texcoord) * IN.color;

                // 斜線パターンの計算
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float diagonal = (screenUV.x * _ScreenParams.x / _ScreenParams.y - screenUV.y) * _LineDensity;
                
                // フラクショナル部分を使ってストライプを生成
                float linePattern = step(_LineThickness, frac(diagonal));

                // ベースカラーと斜線の色を合成
                fixed4 finalColor = tintCol;
                finalColor.rgb = lerp(tintCol.rgb, _LineColor.rgb, linePattern * _LineColor.a);
                finalColor.a = tintCol.a;

                return finalColor;
            }
            ENDCG
        }
    }
}
