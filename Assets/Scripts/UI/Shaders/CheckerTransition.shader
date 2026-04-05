Shader "UI/CheckerboardTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0,0,1)
        _Progress ("Progress", Range(0, 1)) = 0
        _MaxRows ("Max Rows", Float) = 20.0
        _AspectRatio ("Aspect Ratio", Float) = 1.777778
        
        // --- Required for UI Masking ---
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
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Progress;
            float _MaxRows;
            float _AspectRatio;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // UI Coordinate mapping for Checkers: SCREE SPACE
                // SV_Position (IN.vertex) には物理スクリーンのピクセル座標が入っているため、
                // _ScreenParams.xy を使って 0～1 の完全なスクリーンUVを取得する。
                // これにより、UIのRectがどれだけ巨大/変形していても確実に画面端から端まで均等なマス目になる。
                float2 uv = IN.vertex.xy / _ScreenParams.xy;
                
                // Yを画面中心(0.5)からの距離とし、指定した行数(MaxRows)で分割
                float rowIdx = floor(abs(uv.y - 0.5) * 2.0 * _MaxRows);
                
                // Xは画面の物理アスペクト比を掛けて正方形サイズとして扱う
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float colIdx = floor((uv.x - 0.5) * 2.0 * _MaxRows * aspect);
                
                // 市松模様の判定 (0 or 1)
                float checker = abs(fmod(rowIdx + colIdx, 2.0));
                
                // アニメーション進行度（_Progressを各行のタイムラインにマッピング）
                // 最後の行が完全に黒くなるまで余裕を持たせる（+8.0に増やし、端まで確実にカバー）
                float t = _Progress * (_MaxRows + 8.0);
                
                float alpha = 0.0;
                
                // 指定行+3のタイミングで全体が黒になる
                if (t >= rowIdx + 3.0) 
                {
                    alpha = 1.0;
                } 
                // その前は市松模様（半分だけ黒）になる
                else if (t >= rowIdx) 
                {
                    alpha = (checker < 0.5) ? 1.0 : 0.0;
                }
                
                fixed4 color = IN.color;
                color.a *= alpha;
                
                // alphaがほぼ0のピクセルはカリングする（透過部分）
                clip (color.a - 0.001);

                return color;
            }
            ENDCG
        }
    }
}
