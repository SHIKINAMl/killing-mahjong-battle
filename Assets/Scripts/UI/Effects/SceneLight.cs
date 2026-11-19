using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 場面の光（ろうそくのような1灯）。KM/LitArt を使う絵（壁・卓・女の子）だけを照らし、UI は照らさない。
    /// 毎フレーム、光の位置・強さ・ちらつきを全体用のシェーダー値として渡す。場面に1つ置く。
    /// </summary>
    [ExecuteAlways]
    public sealed class SceneLight : MonoBehaviour
    {
        [Header("光の位置（画面上。x は左から、y は上から 0〜1）")]
        [SerializeField] private Vector2 position = new Vector2(0.5f, 0.3f);
        [Tooltip("光の高さ（画面の高さを 600 とした px）。低いほど凹凸の影が長く濃くなる")]
        [SerializeField] private float height = 70f;

        [Header("明るさ")]
        [Tooltip("光が届かない所の明るさ。1 で元の絵のまま")]
        [SerializeField, Range(0f, 1.5f)] private float ambient = 0.5f;
        [Tooltip("光の強さ")]
        [SerializeField, Range(0f, 3f)] private float intensity = 1.4f;
        [Tooltip("光の届く半径（画面の高さを 600 とした px）")]
        [SerializeField] private float radius = 340f;
        [Tooltip("届く範囲の端の減り方。大きいほど境目がはっきりする")]
        [SerializeField] private float sharpness = 1.6f;
        [SerializeField] private Color color = new Color(1f, 0.93f, 0.82f, 1f);

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
                sharpness);

            ScreenPosition = new Vector2(pos.x * Screen.width, (1f - pos.y) * Screen.height);
            ShadowFlicker = 1f + 0.5f * flicker * wobble;

            Shader.SetGlobalVector(LightPosId, pos);
            Shader.SetGlobalVector(LightParamsId, prm);
            Shader.SetGlobalColor(LightColorId, color);
        }
    }
}
