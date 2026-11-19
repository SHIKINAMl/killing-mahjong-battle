// ゲームの世界の絵（壁・卓・女の子）だけを、ノーマルマップで照らすシェーダー。UI には使わない。
// UI の Image にも SpriteRenderer にも使える。
// 光の位置・強さは場面に1つ置いた SceneLight が毎フレーム全体用の値（_KMLight*）として渡す。
// 光の位置は画面上の座標（x は左から、y は上から 0〜1）で、距離は画面の高さを 600 とした px で測る。
// ノーマルマップは Pixel Art Lighter で作ったもの（平らな所が (128,128,255)、緑が上向き）。
Shader "KM/LitArt"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _NormalMap ("Normal Map (Pixel Art Lighter)", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0,2)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestMode ("ZTest", Float) = 4
        // UIEffect の Sampling: Pixelation と同じ計算（壁はもともと強さ 0.4 でドット化していた）。0 で使わない
        _Pixelation ("Pixelation (UIEffect compatible, 0 = off)", Range(0,1)) = 0
        // 絵ごとの光の受け方。女の子は光の真下で白飛びしたので、ここで弱める
        _DirectScale ("Direct Light Scale", Range(0,2)) = 1
        _MaxLight ("Max Brightness (multiplier)", Range(0.5,10)) = 10

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
        ZTest [_ZTestMode]
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
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _NormalMap;
            fixed4 _Color;
            float _NormalStrength;
            float _Pixelation;
            float _DirectScale;
            float _MaxLight;

            // SceneLight が渡す値。SceneLight が無いときは 0 になるので、そのときは光を当てない
            float4 _KMLightPos;     // x, y（上から）: 画面上の光の位置。z: 光の高さ(px)。w: 1 なら有効
            float4 _KMLightParams;  // x: 遠くの明るさ(Ambient), y: 光の強さ(Brightness), z: 光の届く距離(Falloff px), w: 使わない
            float4 _KMLightColor;
            float _KMLightDebug;    // 確認用。1 で法線の色を出す（ふだんは 0）

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 texUv = IN.texcoord;
                if (_Pixelation > 0)
                {
                    float2 pixelSize = max(2, (1 - lerp(0.5, 0.95, _Pixelation)) * _MainTex_TexelSize.zw);
                    texUv = round(texUv * pixelSize) / pixelSize;
                }

                fixed4 albedo = tex2D(_MainTex, texUv) * IN.color;
                // 確認用: 全体用の値 _KMLightDebug を 1 にすると、法線が載っている所をノーマルマップの色で描く
                if (_KMLightDebug > 0.5) return fixed4(tex2D(_NormalMap, texUv).rgb, albedo.a);
                if (_KMLightPos.w < 0.5) return albedo;

                float3 n = tex2D(_NormalMap, texUv).rgb * 2.0 - 1.0;
                n.xy *= _NormalStrength;
                n = normalize(n);

                float2 uv = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
                float2 lightUv = float2(_KMLightPos.x, 1.0 - _KMLightPos.y);
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d = (lightUv - uv) * float2(600.0 * aspect, 600.0);
                float dist = length(d);

                // Pixel Art Lighter（v3.6.1 renderPixels）と同じ式。プロジェクトはリニア色空間なので、
                // ここでの色はツールの albedoLinear と同じ扱いになる
                float3 toLight3 = float3(d, max(_KMLightPos.z, 1.0));
                float dist3 = length(toLight3);
                float diffuse = saturate(dot(n, toLight3 / dist3));
                float r = dist3 / max(_KMLightParams.z, 1.0);
                float atten = 1.0 / (1.0 + r * r);

                float3 light = _KMLightParams.x + _DirectScale * _KMLightParams.y * atten * diffuse * _KMLightColor.rgb;
                light = min(light, _MaxLight);
                albedo.rgb *= light;
                return albedo;
            }
            ENDCG
        }
    }
}
