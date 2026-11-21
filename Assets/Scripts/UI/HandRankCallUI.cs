using System.Collections;
using System.Collections.Generic;
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
    /// 役名はどれも2文字なので、1文字目を左・2文字目を右に置く。
    ///
    /// **見せ方（2026-11-21 に作り直し）。**
    /// 前は白く塗りつぶした特大の字＋画面全体の白い光で、点滴の袋・役一覧・セリフに
    /// 白いかたまりが被さり「ごちゃごちゃした印象」と言われた。暗幕で周りを落とす案は
    /// 「画面を暗くするというより表現をうまくして」と退けられ、**縁だけの字（中を透かす）**が選ばれた。
    /// そこから、このゲームになじむように次の形にした。
    ///
    ///   0.00s  象牙色の縁だけの字が、少し大きめ（1.7倍）から縮んで着く
    ///   0.28s  着弾。字の中が血の赤で満ち、縁が白く光る（画面全体は光らせない）。字の下から血が数滴たれる
    ///   〜0.85s 血が下へ抜けて、縁だけが残る
    ///   1.05s  消え始める（0.25秒）
    ///
    /// 縁の明るさは、ろうそく（<see cref="Effects.SceneLight"/>）のちらつきに合わせて少し揺らす。
    /// **全長は前と同じ約1.3秒。** この表示は手牌を1枚入れ替えるたびに出るので、長くすると待たされる。
    ///
    /// **出ている最中にもう一度呼ばれたら、前の表示をフェードアウトで消してから新しく出す**
    /// （フロー図の「if既に役名表示が出ているか → YES → 前の役名表示がフェードアウトで消える」）。
    ///
    /// 対局シーンは本編とチュートリアルの2つあるので、**シーンには置かず実行時に組み立てる。**
    /// </summary>
    public class HandRankCallUI : MonoBehaviour
    {
        /// <summary>少し大きめから定位置まで縮む時間。</summary>
        private const float SlamSeconds = 0.28f;

        /// <summary>出はじめの大きさ（定位置に対する倍率）。前の 2.6 は画面からはみ出して周りを覆っていた。</summary>
        private const float SlamStartScale = 1.7f;

        /// <summary>着弾してから血が抜け始めるまで（満ちた赤を一瞬見せる）。</summary>
        private const float FillHoldSeconds = 0.08f;

        /// <summary>血が下へ抜けきるまで。</summary>
        private const float DrainSeconds = 0.5f;

        /// <summary>着弾してから消え始めるまで（血が抜ける時間を含む）。</summary>
        private const float HoldSeconds = 0.77f;

        /// <summary>消えるまで。</summary>
        private const float FadeOutSeconds = 0.25f;

        /// <summary>差し替えのときに前の表示を消す時間。待たせすぎないよう短くする。</summary>
        private const float ReplaceFadeSeconds = 0.15f;

        /// <summary>着弾で縁が白く光ってから象牙色へ戻るまで。</summary>
        private const float GlintSeconds = 0.22f;

        /// <summary>
        /// 縁の太さ。字は `PixelMplus10` の 10px を21倍に伸ばしているので線が細く、
        /// 太くしすぎると縁どうしがくっついて「満」の中が埋まる（0.35 で潰れた、2026-09-29）。
        /// </summary>
        private const float OutlineWidth = 0.2f;

        /// <summary>
        /// 文字の高さ（800x600 基準）。**画面高の35%。**
        /// 参考動画は50%だが、それだと手牌と山牌に掛かって牌が選べない。
        /// </summary>
        private const float GlyphSize = 210f;

        /// <summary>
        /// 文字の中心の横位置（画面中央から左右へ）。画面の端に寄せて、中央（相手の立ち絵）を空ける。
        /// `PixelMplus10` の字は em 枠の中で左に寄っていて、276 より外だと左の字が画面端で切れる（2026-09-29）。
        /// </summary>
        private const float GlyphOffsetX = 276f;

        /// <summary>文字の中心の縦位置（中央から上へ）。山牌の段と手牌の段を避けられる高さ。</summary>
        private const float GlyphCenterY = 104f;

        /// <summary>縁の色。真っ白だと UI の白と同じ強さで目立つので、骨のような象牙色にする。</summary>
        private static readonly Color BoneColor = new Color(0.96f, 0.90f, 0.80f, 1f);

        /// <summary>字の中に満ちる血の色。点滴の袋の赤より暗く、壁の赤より明るい。</summary>
        private static readonly Color BloodColor = new Color(0.72f, 0.02f, 0.07f, 1f);

        /// <summary>たれる血の数（左右それぞれ）と、1滴の大きさ（800x600 基準）。字のドットの太さに合わせる。</summary>
        private const int DripsPerGlyph = 3;
        private static readonly Vector2 DripSize = new Vector2(10f, 20f);
        private const float DripSeconds = 0.6f;
        private const float DripFallDistance = 70f;

        private class Glyph
        {
            public RectTransform Root;
            public RectTransform Mask;
            public TextMeshProUGUI Fill;
            public TextMeshProUGUI Outline;
            public Material OutlineMaterial;
        }

        private CanvasGroup _group;
        private Glyph _left;
        private Glyph _right;
        private Coroutine _routine;
        private Color _outlineBase = BoneColor;
        private readonly List<Image> _drips = new List<Image>();

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

            var font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed");
            _left = BuildGlyph("Left", -GlyphOffsetX, font);
            _right = BuildGlyph("Right", GlyphOffsetX, font);
        }

        /// <summary>
        /// 1文字ぶん。奥から「血（下から切り抜く枠の中）」「縁だけの字」の順に重ねる。
        /// 大きさの演出は Root ごと縮めるので、血と縁はずれない。
        /// </summary>
        private Glyph BuildGlyph(string name, float offsetX, TMP_FontAsset font)
        {
            float box = GlyphSize * 1.4f; // 特大のときも切られないよう、1文字ぶんより広く取っておく

            var root = NewRect(name, transform);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(offsetX, GlyphCenterY);
            root.sizeDelta = new Vector2(box, box);

            // 血は、下端を固定した枠（RectMask2D）の高さを縮めて「下へ抜ける」ように見せる
            var mask = NewRect("BloodMask", root);
            mask.anchorMin = mask.anchorMax = mask.pivot = new Vector2(0.5f, 0f);
            mask.anchoredPosition = Vector2.zero;
            mask.sizeDelta = new Vector2(box, box);
            mask.gameObject.AddComponent<RectMask2D>();

            var fill = NewLabel("Blood", mask, font, box);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.anchoredPosition = Vector2.zero;
            fill.color = BloodColor;

            var outline = NewLabel("Outline", root, font, box);
            var outlineRect = outline.rectTransform;
            outlineRect.anchorMin = outlineRect.anchorMax = outlineRect.pivot = new Vector2(0.5f, 0.5f);
            outlineRect.anchoredPosition = Vector2.zero;
            outline.color = Color.white;
            var mat = ApplyOutlineOnly(outline);

            return new Glyph { Root = root, Mask = mask, Fill = fill, Outline = outline, OutlineMaterial = mat };
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI NewLabel(string name, Transform parent, TMP_FontAsset font, float box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.rectTransform.sizeDelta = new Vector2(box, box);
            label.font = font;
            label.fontSize = GlyphSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 縁だけの字にする（中は透明）。
        ///
        /// **`outlineWidth` / `outlineColor` に値を入れるだけでは1本も描かれない。**
        /// TMP はマテリアルの `OUTLINE_ON` キーワードが立っていないと縁を無視する（2026-09-29 に録画で確認）。
        /// `fontMaterial` は**この文字専用の複製**を返すので、共有のフォント資産は汚さない。
        /// </summary>
        private static Material ApplyOutlineOnly(TextMeshProUGUI label)
        {
            var mat = label.fontMaterial;
            if (mat == null) return null;

            mat.EnableKeyword("OUTLINE_ON");
            mat.SetColor(ShaderUtilities.ID_FaceColor, new Color(1f, 1f, 1f, 0f));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, BoneColor);
            label.UpdateMeshPadding();
            return mat;
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
            StopAllCoroutines();
            if (_group != null) _group.alpha = 0f;
            ClearDrips();
        }

        private IEnumerator ShowRoutine(string rankName)
        {
            // 前の表示が残っていれば、まずそれを消す（フロー図の分岐）
            if (_group.alpha > 0f)
            {
                yield return FadeTo(0f, ReplaceFadeSeconds);
            }
            ClearDrips();

            SplitRank(rankName);
            SetBlood(0f);
            _outlineBase = BoneColor;
            _group.alpha = 1f;

            // ---- 少し大きめから定位置へ ----
            float t = 0f;
            while (t < SlamSeconds)
            {
                t += Step();
                float u = Mathf.Clamp01(PresentationTiming.Sample(t) / SlamSeconds);
                // 最後で強く減速させて、止まった感じを出す
                float e = 1f - Mathf.Pow(1f - u, 4f);
                float scale = Mathf.Lerp(SlamStartScale, 1f, e);
                _left.Root.localScale = Vector3.one * scale;
                _right.Root.localScale = Vector3.one * scale;
                yield return null;
            }
            _left.Root.localScale = Vector3.one;
            _right.Root.localScale = Vector3.one;

            // ---- 着弾：血が満ち、縁が光り、血がたれる ----
            KillingMahjong.UI.Effects.HandSelectionFeedbackAudio.PlayRankImpact();
            SetBlood(1f);
            StartCoroutine(GlintRoutine());
            SpawnDrips(_left);
            SpawnDrips(_right);

            // ---- 血が下へ抜ける ----
            float elapsed = 0f;
            while (elapsed < FillHoldSeconds) { elapsed += Step(); yield return null; }
            for (float d = 0f; d < DrainSeconds; )
            {
                d += Step();
                elapsed += Step();
                float u = Mathf.Clamp01(PresentationTiming.Sample(d) / DrainSeconds);
                // はじめはゆっくり、だんだん速く抜ける
                SetBlood(1f - u * u);
                yield return null;
            }
            SetBlood(0f);

            while (elapsed < HoldSeconds) { elapsed += Step(); yield return null; }
            yield return FadeTo(0f, FadeOutSeconds);

            ClearDrips();
            _routine = null;
        }

        /// <summary>
        /// 役名を左右へ割る。役名は 満貫／跳満／倍満／役満 で**必ず2文字**だが、
        /// 差し替えで長さが変わっても落ちないよう、真ん中で割る作りにしておく。
        /// </summary>
        private void SplitRank(string rankName)
        {
            int half = Mathf.Max(1, rankName.Length / 2);
            string l = rankName.Substring(0, half);
            string r = half < rankName.Length ? rankName.Substring(half) : string.Empty;
            _left.Fill.text = _left.Outline.text = l;
            _right.Fill.text = _right.Outline.text = r;
        }

        /// <summary>字の中の血の量（1 で満ちている、0 で空）。下端を固定した枠の高さで切り抜く。</summary>
        private void SetBlood(float amount)
        {
            foreach (var g in new[] { _left, _right })
            {
                var size = g.Mask.sizeDelta;
                // 枠は字より広い（1.4倍）。0〜1 を字の下端〜上端（中心から ±0.4 字ぶん）に当てる。
                // 枠いっぱいに当てると、抜け始めの間は字の上の何もない所を削るだけで、止まって見える
                float a = Mathf.Clamp01(amount);
                float bottom = g.Root.sizeDelta.y * 0.5f - GlyphSize * 0.4f;
                size.y = a <= 0f ? 0f : bottom + GlyphSize * 0.8f * a;
                g.Mask.sizeDelta = size;
                g.Fill.enabled = amount > 0.001f;
            }
        }

        /// <summary>着弾の瞬間、縁を白く光らせて象牙色へ戻す。画面全体は光らせない（周りの UI を巻き込まない）。</summary>
        private IEnumerator GlintRoutine()
        {
            for (float t = 0f; t < GlintSeconds; )
            {
                t += Step();
                float u = Mathf.Clamp01(PresentationTiming.Sample(t) / GlintSeconds);
                _outlineBase = Color.Lerp(Color.white, BoneColor, u * u);
                yield return null;
            }
            _outlineBase = BoneColor;
        }

        /// <summary>字の下の縁から、血を数滴たらす。位置は毎回少し変える。</summary>
        private void SpawnDrips(Glyph g)
        {
            float half = GlyphSize * 0.32f;
            for (int i = 0; i < DripsPerGlyph; i++)
            {
                var rect = NewRect("Drip", g.Root);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = DripSize;
                // 字の下端あたり（左右に散らす）。ドットの格子に合わせて 10 刻みにする
                float x = Mathf.Round(Random.Range(-half, half) / 10f) * 10f;
                float y = -GlyphSize * 0.36f;
                rect.anchoredPosition = new Vector2(x, y);
                var img = rect.gameObject.AddComponent<Image>();
                img.color = BloodColor;
                img.raycastTarget = false;
                _drips.Add(img);
                StartCoroutine(DripRoutine(img, y, Random.Range(0f, 0.12f), Random.Range(0.7f, 1.15f)));
            }
        }

        private IEnumerator DripRoutine(Image img, float startY, float delay, float speed)
        {
            for (float w = 0f; w < delay; w += Step()) yield return null;
            var rect = img.rectTransform;
            for (float t = 0f; t < DripSeconds; )
            {
                t += Step();
                if (img == null) yield break;
                float u = Mathf.Clamp01(t / DripSeconds);
                // 垂れて伸びながら落ち、途中から薄れる。位置はドットの格子（5 刻み）に合わせる
                float fall = DripFallDistance * speed * u * u;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, Mathf.Round((startY - fall) / 5f) * 5f);
                rect.sizeDelta = new Vector2(DripSize.x, Mathf.Round(DripSize.y * (1f + u) / 5f) * 5f);
                var c = BloodColor;
                c.a = u < 0.5f ? 1f : 1f - (u - 0.5f) * 2f;
                img.color = c;
                yield return null;
            }
            if (img != null) img.enabled = false;
        }

        private void ClearDrips()
        {
            foreach (var d in _drips) if (d != null) Destroy(d.gameObject);
            _drips.Clear();
        }

        /// <summary>
        /// 毎フレーム：縁の明るさをろうそくのちらつきに合わせる。
        /// あわせて**出しっぱなしを防ぐ保険（2026-09-20）**。コルーチンが外から止められると、
        /// 最後に当てた alpha のまま画面に残る（決着画面へ役名が残ったことがある）。
        /// </summary>
        private void Update()
        {
            if (_group == null) return;

            if (_group.alpha > 0f)
            {
                var light = KillingMahjong.UI.Effects.SceneLight.Current;
                float flicker = light != null && light.isActiveAndEnabled ? light.ShadowFlicker : 1f;
                // ShadowFlicker は 1 の前後に数%揺れる。縁はそれより少し大きめに揺らして、炎に照らされて見えるようにする
                float k = Mathf.Clamp(0.94f + (flicker - 1f) * 1.5f, 0.82f, 1f);
                var c = _outlineBase * k;
                c.a = 1f;
                foreach (var g in new[] { _left, _right })
                    if (g.OutlineMaterial != null) g.OutlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, c);
            }

            if (_routine != null || _group.alpha <= 0f) return;
            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / FadeOutSeconds);
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            float from = _group.alpha;
            if (seconds <= 0f) { _group.alpha = target; yield break; }

            for (float t = 0f; t < seconds; t += Step())
            {
                _group.alpha = Mathf.Lerp(from, target, PresentationTiming.Sample(t) / seconds);
                yield return null;
            }
            _group.alpha = target;
        }

        /// <summary>
        /// 1フレームで進めてよい時間。**引っかかった直後に飛ばさないための頭打ち。**
        /// 画面が一度止まると、再開したフレームの `unscaledDeltaTime` に止まっていた分が
        /// まとめて乗り、短い演出が1〜2コマで終わってしまう（`TutorialButtonIntroUI` で実際に起きた）。
        /// </summary>
        private static float Step()
        {
            return Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        }
    }
}
