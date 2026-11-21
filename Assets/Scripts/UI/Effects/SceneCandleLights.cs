using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 絵の中のろうそく（燭台）から出る小さな光（2026-11-21「光は、後ろのろうそくからも少しだけ出してほしい」）。
    /// メインメニュー（書斎）の窓の両脇の燭台に置いている。最大4つ。
    ///
    /// 主な光（<see cref="SceneLight"/>）とは別に、KM/LitArt の絵を少しだけ照らす。ろうそくごとに別々にちらつく。
    /// 影（<see cref="LitArtDropShadow"/>）は主な光だけで決まり、この光では出ない。
    /// 置いていない場面（対局など）では数が 0 のままなので、何も変わらない。
    /// </summary>
    [ExecuteAlways]
    public sealed class SceneCandleLights : MonoBehaviour
    {
        [System.Serializable]
        public struct Candle
        {
            [Tooltip("画面上の位置（x は左から、y は上から 0〜1）")]
            public Vector2 position;
        }

        [SerializeField] private Candle[] candles = { new Candle { position = new Vector2(0.37f, 0.40f) }, new Candle { position = new Vector2(0.62f, 0.40f) } };
        [Tooltip("光の高さ（画面の高さを 600 とした px）。低いほど近くの凹凸がくっきりする")]
        [SerializeField] private float height = 30f;
        [Tooltip("光の強さ。少しだけ（主な光より弱く）")]
        [SerializeField, Range(0f, 10f)] private float intensity = 1.2f;
        [Tooltip("光の届く距離（画面の高さを 600 とした px）")]
        [SerializeField] private float radius = 70f;
        [SerializeField] private Color color = new Color(1f, 0.55f, 0.30f, 1f);
        [Tooltip("ちらつきの大きさ。0 で止まる")]
        [SerializeField, Range(0f, 0.5f)] private float flicker = 0.18f;

        private const int Max = 4;
        private static readonly int CountId = Shader.PropertyToID("_KMCandleCount");
        private static readonly int PosId = Shader.PropertyToID("_KMCandlePos");
        private static readonly int ParamsId = Shader.PropertyToID("_KMCandleParams");
        private static readonly int ColorId = Shader.PropertyToID("_KMCandleColor");
        private readonly Vector4[] _pos = new Vector4[Max];
        private readonly Vector4[] _params = new Vector4[Max];
        private readonly Vector4[] _color = new Vector4[Max];

        private void OnEnable() => Apply();
        private void Update() => Apply();
#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif
        private void OnDisable() => Shader.SetGlobalFloat(CountId, 0f);

        private void Apply()
        {
            if (!isActiveAndEnabled || candles == null) return;
            int n = Mathf.Min(candles.Length, Max);
            float t = Application.isPlaying ? Time.time : 0f;
            for (int i = 0; i < Max; i++)
            {
                if (i >= n) { _pos[i] = Vector4.zero; _params[i] = Vector4.zero; _color[i] = Vector4.zero; continue; }
                // ろうそくごとに違う速さで揺らす（同じ揺れだと2本が同時に明滅して作り物っぽい）
                float seed = i * 13.7f;
                float f = 1f + flicker * (Mathf.PerlinNoise(t * 3.1f + seed, seed) - 0.5f) * 2f;
                var p = candles[i].position;
                _pos[i] = new Vector4(p.x, p.y, height, 1f);
                _params[i] = new Vector4(0f, intensity * f, radius, 0f);
                _color[i] = color.linear;   // ツールと同じくリニアの値で掛ける（SceneLight と同じ）
            }
            Shader.SetGlobalVectorArray(PosId, _pos);
            Shader.SetGlobalVectorArray(ParamsId, _params);
            Shader.SetGlobalVectorArray(ColorId, _color);
            Shader.SetGlobalFloat(CountId, n);
        }
    }
}
