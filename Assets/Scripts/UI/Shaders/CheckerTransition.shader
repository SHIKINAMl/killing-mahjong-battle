Shader "UI/CheckerboardTransition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0,0,1)
        _Progress ("Progress", Range(0, 1)) = 0
        _CheckerSize ("Checker Size", Float) = 50.0
        
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
            float _CheckerSize;

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
                // UI Coordinate mapping for Checkers
                float2 pos = IN.worldPosition.xy / _CheckerSize;
                
                // Determine if this checker square is "on" or "off"
                float checker = fmod(floor(pos.x) + floor(pos.y), 2.0);
                
                // For a diagonal sweep from bottom-left (0,0) to top-right (1,1) in relative screen space
                // We need relative screen coordinates.
                // Depending on the canvas, IN.texcoord is 0..1 across the image
                float diagonal = (IN.texcoord.x + IN.texcoord.y) / 2.0;

                // Pattern evolution based on _Progress
                // As progress goes 0 -> 1, the diagonal threshold moves 0 -> 1
                // We want the fade to spread diagonally.
                // Let's create a soft threshold around the diagonal.
                
                float fadeEdge = 0.3; // How soft the diagonal diagonal edge is
                // Mapping progress to allow full coverage
                float mappedProgress = _Progress * (1.0 + fadeEdge) - (fadeEdge / 2.0);
                
                float localP = saturate((mappedProgress - diagonal + (fadeEdge / 2.0)) / fadeEdge);
                
                float alpha = 0;
                
                // The checker pattern should reveal first, then the remaining negative space
                // Checker is 0 or 1
                if (checker < 0.5) {
                    alpha = localP; // Checker squares fade in first along the diagonal
                } else {
                    // Non-checker squares fade in a bit later along the same diagonal
                    float delayedP = saturate((mappedProgress - 0.1 - diagonal + (fadeEdge / 2.0)) / fadeEdge);
                    alpha = delayedP;
                }

                fixed4 color = IN.color;
                color.a *= alpha;
                clip (color.a - 0.001);

                return color;
            }
            ENDCG
        }
    }
}
