Shader "UI/KillingMahjong/SilhouetteFill"
{
    // 絵の形だけを、頂点色（Image.color）の1色で塗る。ロンのカットインで、立ち絵の後ろの赤い影に使う（2026-10-20）。
    // 縁はぼかさず、透明度 0.5 で切る（ドットの輪郭を崩さない）。Mask の中でも使えるよう Stencil を持つ。
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
            float4 _ClipRect;
            fixed4 _Color;

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

            fixed4 frag(v2f input) : SV_Target
            {
                fixed shape = step(.5, tex2D(_MainTex, input.uv).a);
                fixed4 c = fixed4(input.color.rgb, input.color.a * shape);
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
