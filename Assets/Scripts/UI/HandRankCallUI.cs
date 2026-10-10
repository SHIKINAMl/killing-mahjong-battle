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
    ///
    /// **出し方は『逆転裁判』の「無罪」を写した**（2026-09-29 のユーザー指示）。
    /// 役名はどれも2文字なので、1文字目を左・2文字目を右に置き、
    /// **特大から縮んで定位置に着弾**させ、着いた瞬間に白く光らせる。
    ///
    /// 参考動画（ユーザー提供・30fps）から測った元の構成:
    ///
    ///   0.00s  1文字目が左から特大で出る
    ///   0.55s  縮んで着弾
    ///   0.60s  白フラッシュ（2コマ・画面の88%が白）／2文字目が右から出る
    ///   1.23s  2文字目が着弾
    ///   1.30s  白フラッシュ（1コマ・94%）
    ///   1.37s  1.5秒ほど静止
    ///
    /// **そのままの尺（約3秒）では使えない。** この表示は手牌を1枚入れ替えるたびに
    /// 出るので、3秒の演出が毎回入ると牌を選び直すのが待たされる。
    /// ユーザーの指示で**左右を同時に入れて全長1.3秒**へ詰めてある。
    /// 元の「片方ずつ・2回光る」に戻したくなったら <see cref="SlamSeconds"/> 付近を見ること。
    ///
    /// **文字は画面高の35%**。参考動画は50%だが、そのままだと手牌と山牌が隠れて
    /// 牌が選べなくなる（この表示は手牌選択中に出る）。
    ///
    /// **出ている最中にもう一度呼ばれたら、前の表示をフェードアウトで消してから新しく出す**
    /// （フロー図の「if既に役名表示が出ているか → YES → 前の役名表示がフェードアウトで消える」）。
    ///
    /// 対局シーンは本編とチュートリアルの2つあるので、**シーンには置かず実行時に組み立てる。**
    /// </summary>
    public class HandRankCallUI : MonoBehaviour
    {
        /// <summary>特大から定位置まで縮む時間。参考動画の 0.55 秒を詰めた値。</summary>
        private const float SlamSeconds = 0.40f;

        /// <summary>出はじめの大きさ（定位置に対する倍率）。画面からはみ出す大きさにする。</summary>
        private const float SlamStartScale = 2.6f;

        /// <summary>着弾してから消え始めるまで。</summary>
        private const float HoldSeconds = 0.90f;

        /// <summary>消えるまで。</summary>
        private const float FadeOutSeconds = 0.25f;

        /// <summary>差し替えのときに前の表示を消す時間。待たせすぎないよう短くする。</summary>
        private const float ReplaceFadeSeconds = 0.15f;

        /// <summary>
        /// 着弾の白フラッシュ。参考動画では**2コマだけほぼ真っ白**（画面の88%が235以上）で、
        /// そのあと4コマほどかけて戻っていた。30fps の2コマ＝0.067秒。
        /// </summary>
        private const float FlashHoldSeconds = 0.067f;
        private const float FlashFadeSeconds = 0.13f;
        /// <summary>
        /// **0.9 では文字まで白に溶ける。** 参考動画では光っている最中も
        /// 「無」「罪」がはっきり読めたままだった。黒縁を付けたうえで 0.78 に抑える。
        /// </summary>
        private const float FlashPeakAlpha = 0.78f;

        /// <summary>
        /// 黒縁の太さ。
        ///
        /// **0.35 では字が潰れる（2026-09-29 に録画で確認）。**
        /// 参考動画の「無罪」は縁が太いが、あちらは線の太い明朝体。
        /// こちらは `PixelMplus10` で、10px の字を21倍に伸ばしているため線が細く、
        /// 0.35 だと縁どうしがくっついて「役」「満」が塗りつぶれた塊になっていた。
        /// </summary>
        private const float OutlineWidth = 0.16f;

        /// <summary>
        /// 文字の高さ（800x600 基準）。**画面高の35%。**
        /// 参考動画は50%だが、それだと手牌と山牌に掛かって牌が選べない。
        /// </summary>
        private const float GlyphSize = 210f;

        /// <summary>
        /// 文字の中心の横位置（画面中央から左右へ）。
        /// **画面の端に寄せて、中央を空ける。** 中央には相手の立ち絵が居る。
        /// </summary>
        /// <remarks>
        /// **288 では左の字が画面端で切れた（2026-09-29 に実測）。**
        /// `PixelMplus10` の字は em 枠の中で 11px ほど左に寄っていて、左右対称に置いても
        /// 左だけがはみ出す。276 まで内側へ寄せると、4つの役名すべてが枠に収まる。
        /// </remarks>
        private const float GlyphOffsetX = 276f;

        /// <summary>
        /// 文字の中心の縦位置（中央から上へ）。**帯を出していた頃と同じ高さ。**
        /// 山牌の段と手牌の段を避けられることが確認済みの値。
        /// </summary>
        private const float GlyphCenterY = 104f;

        private CanvasGroup _group;
        private Image _flash;
        private TextMeshProUGUI _leftLabel;
        private TextMeshProUGUI _rightLabel;
        private RectTransform _leftRect;
        private RectTransform _rightRect;
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

            // **フラッシュは文字より先に作る＝文字の奥に来る。**
            // 参考動画では、白くなっている最中も「無」「罪」は読めたままだった。
            // 手前に被せると、いちばん見せたい瞬間に文字が消える。
            var flashObject = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashObject.transform.SetParent(transform, false);
            var flashRect = (RectTransform)flashObject.transform;
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.offsetMin = Vector2.zero;
            flashRect.offsetMax = Vector2.zero;
            _flash = flashObject.GetComponent<Image>();
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.raycastTarget = false;

            _leftLabel = BuildLabel("Left", -GlyphOffsetX, out _leftRect);
            _rightLabel = BuildLabel("Right", GlyphOffsetX, out _rightRect);
        }

        private TextMeshProUGUI BuildLabel(string name, float offsetX, out RectTransform rect)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);

            rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(offsetX, GlyphCenterY);
            // 特大のときも切られないよう、1文字ぶんより広く取っておく
            rect.sizeDelta = new Vector2(GlyphSize * 1.4f, GlyphSize * 1.4f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            label.fontSize = GlyphSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.fontStyle = FontStyles.Bold;
            // **白抜き＋太い黒縁。** 参考動画の「無罪」と同じ。
            // 盤面の緑・牌の白・立ち絵のどれに重なっても輪郭が残る。
            label.color = Color.white;
            ApplyOutline(label);
            return label;
        }

        /// <summary>
        /// 太い黒縁を付ける。
        ///
        /// **`outlineWidth` / `outlineColor` に値を入れるだけでは1本も描かれない。**
        /// TMP はマテリアルの `OUTLINE_ON` キーワードが立っていないと縁を無視する
        /// （`DialogueUI.ApplyShadow` の `UNDERLAY_ON` と同じ罠）。
        /// 2026-09-29 に録画で確認した — 値は 0.35 が入っているのに文字は真っ白のままで、
        /// 白フラッシュに重なった瞬間、文字が画面から消えていた。
        ///
        /// `fontMaterial` は**この文字専用の複製**を返すので、共有のフォント資産は汚さない。
        /// </summary>
        private static void ApplyOutline(TextMeshProUGUI label)
        {
            var mat = label.fontMaterial;
            if (mat == null) return;

            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            label.UpdateMeshPadding();
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

        /// <summary>役名を出している最中か（消えきるまで true）。演出の順番待ちが、終わりを知るのに使う。</summary>
        public bool IsShowing { get { return _routine != null; } }

        /// <summary>出ているものを即座に片付ける（フェイズが変わったときなど）。</summary>
        public void HideImmediate()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_group != null) _group.alpha = 0f;
            if (_flash != null) _flash.color = new Color(1f, 1f, 1f, 0f);
        }

        private IEnumerator ShowRoutine(string rankName)
        {
            // 前の表示が残っていれば、まずそれを消す（フロー図の分岐）
            if (_group.alpha > 0f)
            {
                yield return FadeTo(0f, ReplaceFadeSeconds);
            }

            SplitRank(rankName);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _group.alpha = 1f;

            // ---- 特大から定位置へ ----
            // **中心は動かさず、大きさだけ落とす。** 参考動画の「無」も、
            // 左端に掛かったまま右端だけが内側へ寄ってきていた（＝その場で縮んでいた）。
            float t = 0f;
            while (t < SlamSeconds)
            {
                t += Step();
                float u = Mathf.Clamp01(t / SlamSeconds);
                // 最後で強く減速させて、止まった感じを出す
                float e = 1f - Mathf.Pow(1f - u, 4f);
                float scale = Mathf.Lerp(SlamStartScale, 1f, e);
                _leftRect.localScale = Vector3.one * scale;
                _rightRect.localScale = Vector3.one * scale;
                yield return null;
            }
            _leftRect.localScale = Vector3.one;
            _rightRect.localScale = Vector3.one;

            // ---- 着弾の白フラッシュ ----
            StartCoroutine(FlashRoutine());

            yield return new WaitForSecondsRealtime(HoldSeconds);
            yield return FadeTo(0f, FadeOutSeconds);

            _routine = null;
        }

        /// <summary>
        /// 役名を左右へ割る。役名は 満貫／跳満／倍満／役満 で**必ず2文字**だが、
        /// 差し替えで長さが変わっても落ちないよう、真ん中で割る作りにしておく。
        /// </summary>
        private void SplitRank(string rankName)
        {
            int half = Mathf.Max(1, rankName.Length / 2);
            _leftLabel.text = rankName.Substring(0, half);
            _rightLabel.text = half < rankName.Length ? rankName.Substring(half) : string.Empty;
        }

        private IEnumerator FlashRoutine()
        {
            _flash.color = new Color(1f, 1f, 1f, FlashPeakAlpha);

            // **秒だけで保持すると、重い環境では一度も描かれずに終わる。**
            // ScreenFlash が同じ理由で必ず yield しているのに倣う。
            float held = 0f;
            do
            {
                yield return null;
                held += Time.unscaledDeltaTime;
            }
            while (held < FlashHoldSeconds);

            float t = 0f;
            while (t < FlashFadeSeconds)
            {
                t += Step();
                float u = Mathf.Clamp01(t / FlashFadeSeconds);
                if (_flash == null) yield break;
                _flash.color = new Color(1f, 1f, 1f, FlashPeakAlpha * (1f - u));
                yield return null;
            }
            _flash.color = new Color(1f, 1f, 1f, 0f);
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

            for (float t = 0f; t < seconds; t += Step())
            {
                _group.alpha = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }
            _group.alpha = target;
        }

        /// <summary>
        /// 1フレームで進めてよい時間。**引っかかった直後に飛ばさないための頭打ち。**
        /// 画面が一度止まると、再開したフレームの `unscaledDeltaTime` に止まっていた分が
        /// まとめて乗り、0.4秒の着弾が1〜2コマで終わってしまう
        /// （`TutorialButtonIntroUI` で実際に起きたのと同じ）。
        /// </summary>
        private static float Step()
        {
            return Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        }
    }
}
