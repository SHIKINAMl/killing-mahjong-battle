using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 上からの補助光（卓の真上に吊るした明かりのような1灯）。ろうそく（SceneLight）とは別に、
    /// マテリアルの Fill Light Scale が 0 より大きい絵だけを照らす（いまは卓だけ）。ちらつかない。
    /// 計算は SceneLight と同じ Pixel Art Lighter の式で、遠くの明るさ（Ambient）は足さない。
    /// 影（LitArtDropShadow）はろうそくの光だけで決まり、この光では出ない。
    /// </summary>
    [ExecuteAlways]
    public sealed class SceneFillLight : MonoBehaviour
    {
        [Header("光の位置（画面上。x は左から、y は上から 0〜1）")]
        [SerializeField] private Vector2 position = new Vector2(0.5f, 0.82f);
        [Tooltip("光の高さ（Height。画面の高さを 600 とした px）。高いほどむらなく平らに当たる")]
        [SerializeField] private float height = 260f;

        [Header("明るさ")]
        [Tooltip("光の強さ（Brightness）")]
        [SerializeField, Range(0f, 25f)] private float intensity = 1.6f;
        [Tooltip("光の届く距離（Falloff。画面の高さを 600 とした px）")]
        [SerializeField] private float radius = 420f;
        [Tooltip("光の色（Light Color）")]
        [SerializeField] private Color color = new Color(1f, 0.96f, 0.88f, 1f);

        private static readonly int FillPosId = Shader.PropertyToID("_KMFillPos");
        private static readonly int FillParamsId = Shader.PropertyToID("_KMFillParams");
        private static readonly int FillColorId = Shader.PropertyToID("_KMFillColor");

        private void OnEnable() => Apply();

        private void Update() => Apply();

#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif

        private void OnDisable() => Shader.SetGlobalVector(FillPosId, Vector4.zero);

        private void Apply()
        {
            if (!isActiveAndEnabled) return;
            Shader.SetGlobalVector(FillPosId, new Vector4(position.x, position.y, height, 1f));
            Shader.SetGlobalVector(FillParamsId, new Vector4(0f, intensity, radius, 0f));
            Shader.SetGlobalColor(FillColorId, color);
        }
    }
}
