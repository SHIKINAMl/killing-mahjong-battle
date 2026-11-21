using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 場面の光（ろうそくのような1灯）。KM/LitArt を使う絵（壁・卓・女の子）だけを照らし、UI は照らさない。
    /// 毎フレーム、光の位置・強さ・ちらつきを全体用のシェーダー値として渡す。場面に1つ置く。
    /// 明るさの計算は Pixel Art Lighter と同じ。項目名の括弧はツールの表示名で、ツールで決めた値をそのまま入れられる
    /// （Ambient の % は 0〜1 に直す。px は画面の高さを 600 としたときの値）。
    /// </summary>
    [ExecuteAlways]
    public sealed class SceneLight : MonoBehaviour
    {
        [Header("光の位置（画面上。x は左から、y は上から 0〜1）")]
        [SerializeField] private Vector2 position = new Vector2(0.5f, 0.2f);
        [Tooltip("光の高さ（Height。画面の高さを 600 とした px）。低いほど凹凸の影が長く濃くなる")]
        [SerializeField] private float height = 60f;

        [Header("明るさ")]
        [Tooltip("光が届かない所の明るさ（Ambient）。1 で元の絵のまま")]
        [SerializeField, Range(0f, 1.5f)] private float ambient = 0.35f;
        [Tooltip("光の強さ（Brightness）")]
        [SerializeField, Range(0f, 25f)] private float intensity = 2.5f;
        [Tooltip("光の届く距離（Falloff。画面の高さを 600 とした px）")]
        [SerializeField] private float radius = 200f;
        [Tooltip("光の色（Light Color）")]
        [SerializeField] private Color color = Color.white;

        [Header("ろうそくのちらつき")]
        [Tooltip("明るさのちらつきの大きさ。0 で止まる")]
        [SerializeField, Range(0f, 0.5f)] private float flicker = 0.12f;
        [Tooltip("位置の揺れ幅（画面に対する割合）")]
        [SerializeField] private Vector2 sway = new Vector2(0.012f, 0.008f);

        private static readonly int LightPosId = Shader.PropertyToID("_KMLightPos");
        private static readonly int LightParamsId = Shader.PropertyToID("_KMLightParams");
        private static readonly int LightColorId = Shader.PropertyToID("_KMLightColor");

        /// <summary>いま有効な光。無ければ null（影も出さない）。</summary>
        public static SceneLight Current { get; private set; }

        /// <summary>いまの光の位置（画面の px。左下が原点）。ちらつきの揺れも入る。</summary>
        public Vector2 ScreenPosition { get; private set; }

        /// <summary>ちらつきに合わせた影の濃さの倍率（明るいほど影も濃い）。</summary>
        public float ShadowFlicker { get; private set; } = 1f;

        private void OnEnable()
        {
            Current = this;
            Apply();
        }

        private void Update() => Apply();

        private void OnDisable()
        {
            if (Current == this) Current = null;
            // 光を消したら絵は元の色に戻る
            Shader.SetGlobalVector(LightPosId, Vector4.zero);
        }

        private void Apply()
        {
            float t = Application.isPlaying ? Time.time : 0f;
            float wobble = 0.5f * Mathf.Sin(t * 7.3f) + 0.3f * Mathf.Sin(t * 13.1f + 1.7f) + 0.2f * Mathf.Sin(t * 23.7f + 0.4f);
            float sx = 0.6f * Mathf.Sin(t * 1.7f) + 0.4f * Mathf.Sin(t * 3.1f + 2f);
            float sy = 0.6f * Mathf.Sin(t * 2.3f + 1f) + 0.4f * Mathf.Sin(t * 4.3f + 0.5f);

            var pos = new Vector4(position.x + sway.x * sx, position.y + sway.y * sy, height, 1f);
            var prm = new Vector4(
                ambient * (1f + 0.3f * flicker * wobble),
                intensity * (1f + flicker * wobble),
                radius * (1f + 0.5f * flicker * wobble),
                0f);

            ScreenPosition = new Vector2(pos.x * Screen.width, (1f - pos.y) * Screen.height);
            ShadowFlicker = 1f + 0.5f * flicker * wobble;

            Shader.SetGlobalVector(LightPosId, pos);
            Shader.SetGlobalVector(LightParamsId, prm);
            // 色はリニアの値にして渡す（Pixel Art Lighter と同じ。ツールは light = linear(Light Color)）。
            // SetGlobalColor のままだと赤い光の緑・青が 0.3 前後で掛かり、赤い光なのに白っぽくなった（2026-11-21、メインメニュー）。
            // 白い光（対局画面）はどちらでも同じ値なので見た目は変わらない
            Shader.SetGlobalVector(LightColorId, (Vector4)color.linear);
        }
    }
}
