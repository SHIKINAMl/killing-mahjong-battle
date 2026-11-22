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
        // 上からの補助光（SceneFillLight）の受け方。0 で受けない。いまは卓だけ 1
        _FillScale ("Fill Light Scale", Range(0,2)) = 0
        // ろうそくの小さな光（SceneCandleLights）の受け方。場面に置いていなければ何も起きない
        _CandleScale ("Candle Light Scale", Range(0,2)) = 1
        // 絵が画面の上で回る物（対局待ちの牌）用。1 にすると、法線を絵の回転・反転に合わせて回す
        [Toggle] _RotateNormals ("Rotate Normals With Sprite", Float) = 0

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
            float _FillScale;

            // SceneLight が渡す値。SceneLight が無いときは 0 になるので、そのときは光を当てない
            float4 _KMLightPos;     // x, y（上から）: 画面上の光の位置。z: 光の高さ(px)。w: 1 なら有効
            float4 _KMLightParams;  // x: 遠くの明るさ(Ambient), y: 光の強さ(Brightness), z: 光の届く距離(Falloff px), w: 使わない
            float4 _KMLightColor;
            float _KMLightDebug;    // 確認用。1 で法線の色を出す（ふだんは 0）

            // SceneFillLight が渡す値（上からの補助光）。並びは _KMLight* と同じ。遠くの明るさは足さない
            float4 _KMFillPos;
            float4 _KMFillParams;
            float4 _KMFillColor;

            // SceneCandleLights が渡す値（絵の中のろうそく。最大4つ）。並びは _KMLight* と同じ
            float _KMCandleCount;
            float4 _KMCandlePos[4];
            float4 _KMCandleParams[4];
            float4 _KMCandleColor[4];
            float _CandleScale;
            float _RotateNormals;

            // Pixel Art Lighter と同じ、1灯ぶんの光の当たり方（遠くの明るさは含まない）
            float3 DirectLight(float3 n, float2 uv, float4 pos, float4 prm, float4 col, float aspect)
            {
                float2 lightUv = float2(pos.x, 1.0 - pos.y);
                float2 d = (lightUv - uv) * float2(600.0 * aspect, 600.0);
                float3 toLight3 = float3(d, max(pos.z, 1.0));
                float dist3 = length(toLight3);
                float diffuse = saturate(dot(n, toLight3 / dist3));
                float r = dist3 / max(prm.z, 1.0);
                float atten = 1.0 / (1.0 + r * r);
                return prm.y * atten * diffuse * col.rgb;
            }

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
                float aspect = _ScreenParams.x / _ScreenParams.y;

                // 絵が回っている物（対局待ちの牌）は、法線の向き（絵の右・絵の上）を画面の向きへ直す（2026-11-22）。
                // UI は同じ Canvas の絵を1つのメッシュにまとめるので、物ごとの回転は行列から取れない。
                // 代わりに「画面を1画素動くと絵の座標がどれだけ動くか」（ddx/ddy）から、絵の右と上が画面のどちらかを求める。
                // 画面側も同じく ddx/ddy で測るので、上下の向きの違い（D3D と GL）を気にしなくてよい
                if (_RotateNormals > 0.5)
                {
                    float2 ux = ddx(IN.texcoord), uy = ddy(IN.texcoord);
                    float2 sx = ddx(uv), sy = ddy(uv);
                    float det = ux.x * uy.y - uy.x * ux.y;
                    if (abs(det) > 1e-12)
                    {
                        float2 tu = (sx * uy.y - sy * ux.y) / det;      // 絵の右（u が増える向き）を画面の uv で
                        float2 tv = (-sx * uy.x + sy * ux.x) / det;     // 絵の上（v が増える向き）
                        tu.x *= aspect; tv.x *= aspect;                  // 画面の縦横比をそろえてから向きだけ取る
                        float2 nxy = n.x * normalize(tu) + n.y * normalize(tv);
                        n = normalize(float3(nxy, n.z));
                    }
                }

                // Pixel Art Lighter（v3.6.1 renderPixels）と同じ式。プロジェクトはリニア色空間なので、
                // ここでの色はツールの albedoLinear と同じ扱いになる
                float3 light = _KMLightParams.x + _DirectScale * DirectLight(n, uv, _KMLightPos, _KMLightParams, _KMLightColor, aspect);
                if (_KMFillPos.w > 0.5 && _FillScale > 0)
                    light += _FillScale * DirectLight(n, uv, _KMFillPos, _KMFillParams, _KMFillColor, aspect);
                for (int ci = 0; ci < 4; ci++)
                {
                    if (ci < _KMCandleCount)
                        light += _CandleScale * DirectLight(n, uv, _KMCandlePos[ci], _KMCandleParams[ci], _KMCandleColor[ci], aspect);
                }
                light = min(light, _MaxLight);
                albedo.rgb *= light;
                return albedo;
            }
            ENDCG
        }
    }
}
