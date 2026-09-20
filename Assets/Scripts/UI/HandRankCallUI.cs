using System.Collections;
using KillingMahjong.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 即席満貫以上判定の**役名表示**（2026-09-20、MnoA さんの仕様書のフロー図）。
    ///
    /// 手牌を13枚そろえた時点で、その手が満貫以上になり得るなら、
    /// 届きうる中で**いちばん上の格**（満貫／跳満／倍満／役満）を画面へ一瞬出す。
    /// 出したら2秒後にフェードアウトで消える。
    ///
    /// **出ている最中にもう一度呼ばれたら、前の表示をフェードアウトで消してから新しく出す**
    /// （フロー図の「if既に役名表示が出ているか → YES → 前の役名表示がフェードアウトで消える」）。
    ///
    /// 対局シーンは本編とチュートリアルの2つあるので、**シーンには置かず実行時に組み立てる。**
    /// </summary>
    public class HandRankCallUI : MonoBehaviour
    {
        /// <summary>出てくるまで。速すぎると点滅に見えるので、少しだけ溜める。</summary>
        private const float FadeInSeconds = 0.18f;

        /// <summary>読ませる時間。**フロー図の「２秒後フェードアウト」。**</summary>
        private const float HoldSeconds = 2.0f;

        /// <summary>消えるまで。入りより緩やかにして、残像を残す。</summary>
        private const float FadeOutSeconds = 0.45f;

        /// <summary>差し替えのときに前の表示を消す時間。待たせすぎないよう短くする。</summary>
        private const float ReplaceFadeSeconds = 0.15f;

        /// <summary>
        /// 帯と文字の高さ（画面中央からの位置、800x600 基準）。
        /// **山牌の段（中央やや上）と手牌（下端）を隠さない高さ**に置く。
        /// </summary>
        private const float BandCenterY = 104f;

        private CanvasGroup _group;
        private TextMeshProUGUI _label;
        private RectTransform _labelRect;
        private Coroutine _routine;

        public static HandRankCallUI Create()
        {
            // 既存UIの親は画面全体ではないものが多く、子にするとその矩形で切られる。
            // 独立した Overlay Canvas にして、画面の中央に自分で置く。
            var go = new GameObject("HandRankCallUI", typeof(RectTransform));
            return go.AddComponent<HandRankCallUI>();
        }

        private void Awake()
        {
            Build();
            _group.alpha = 0f;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = UISortingOrders.HandRankCall;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // 立ち絵や吹き出しの上に出るので、文字だけだと読めない。暗い帯を敷く
            var band = new GameObject("Band", typeof(RectTransform), typeof(Image));
            band.transform.SetParent(transform, false);
            var bandRect = (RectTransform)band.transform;
            bandRect.anchorMin = new Vector2(0.5f, 0.5f);
            bandRect.anchorMax = new Vector2(0.5f, 0.5f);
            bandRect.pivot = new Vector2(0.5f, 0.5f);
            bandRect.anchoredPosition = new Vector2(0f, BandCenterY);
            bandRect.sizeDelta = new Vector2(300f, 88f);
            var bandImage = band.GetComponent<Image>();
            bandImage.color = new Color(0.05f, 0.02f, 0.06f, 0.88f);
            bandImage.raycastTarget = false;

            // 板のふち。役名だと分かる印で、盤面の色から浮かせる
            var bandOutline = band.AddComponent<Outline>();
            bandOutline.effectColor = new Color32(214, 40, 62, 210);
            bandOutline.effectDistance = new Vector2(2f, -2f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);

            _labelRect = (RectTransform)labelObject.transform;
            _labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _labelRect.pivot = new Vector2(0.5f, 0.5f);
            _labelRect.anchoredPosition = new Vector2(0f, BandCenterY);
            _labelRect.sizeDelta = new Vector2(560f, 96f);

            _label = labelObject.GetComponent<TextMeshProUGUI>();
            _label.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            _label.fontSize = 64f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.raycastTarget = false;
            _label.color = new Color32(255, 233, 168, 255);
            _label.fontStyle = FontStyles.Bold;
            // 盤面の緑や牌の白に埋もれないよう、濃い縁を付ける
            _label.outlineWidth = 0.28f;
            _label.outlineColor = new Color32(60, 12, 20, 255);
        }

        /// <summary>
        /// 役名を出す。出ている最中なら、前のものを消してから出し直す。
        /// </summary>
        public void ShowRank(string rankName)
        {
            if (string.IsNullOrEmpty(rankName)) return;

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(rankName));
        }

        /// <summary>出ているものを即座に片付ける（フェイズが変わったときなど）。</summary>
        public void HideImmediate()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_group != null) _group.alpha = 0f;
        }

        private IEnumerator ShowRoutine(string rankName)
        {
            // 前の表示が残っていれば、まずそれを消す（フロー図の分岐）
            if (_group.alpha > 0f)
            {
                yield return FadeTo(0f, ReplaceFadeSeconds);
            }

            _label.text = rankName;
            yield return FadeTo(1f, FadeInSeconds);
            // **実時間で待つ。** 決着演出などで timeScale を触られても、出したまま残らない
            yield return new WaitForSecondsRealtime(HoldSeconds);
            yield return FadeTo(0f, FadeOutSeconds);

            _routine = null;
        }

        /// <summary>
        /// **出しっぱなしを防ぐ保険（2026-09-20）。**
        ///
        /// コルーチンが外から止められる（対局が終わって別の演出がオブジェクトを触る等）と、
        /// 最後に当てた alpha のまま画面に残る。実際に決着画面へ役名が残ったことがある。
        /// 動いている手続きが無いのに見えていたら、ここで静かに消す。
        /// </summary>
        private void Update()
        {
            if (_routine != null || _group == null || _group.alpha <= 0f) return;

            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / FadeOutSeconds);
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            float from = _group.alpha;
            if (seconds <= 0f) { _group.alpha = target; yield break; }

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }
            _group.alpha = target;
        }
    }
}
