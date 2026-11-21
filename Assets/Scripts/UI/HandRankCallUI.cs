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
    /// 白いかたまりが被さり「ごちゃごちゃした印象」「かぶってる感」と言われた。ユーザーが選んだ形:
    ///   ・**象牙色の縁だけの字**（中を透かす）。暗幕で画面全体を落とす案は退けられた
    ///   ・**字の下に重なっている UI だけを薄くして道を空ける**（案 A）。字が消えるのに合わせて戻す
    ///   ・**字は位置も大きさも変えず、その場でフェードインする**（「血が抜ける演出よりは、
    ///     文字の位置が変わらないフェードインしてほしい」）。特大から縮む動き・血の演出はやめた
    ///
    ///   0.00s  字がその場でフェードイン（0.2秒）。同時に、字の下の UI が薄くなる
    ///   1.05s  字が消え始め（0.25秒）、薄くした UI も戻る
    ///
    /// **字にはろうそく（<see cref="Effects.SceneLight"/>）の光を当てる（2026-11-21「この即席満貫の文字には光を当てて、
    /// いい感じの影を作ってほしい」）。** 字ごとに3枚重ねる:
    ///   ・影: 光と反対側へずらした、ぼかした暗い縁（光から遠いほど長い）
    ///   ・地: 少し暗い象牙色の縁（光の当たらない側）
    ///   ・光: 明るい縁を、光の来る側だけ柔らかく切り抜いて重ねる（RectMask2D のぼかし）
    /// 光の向き・影のずれ・明るさは毎フレーム光の位置とちらつきから決めるので、炎が揺れると一緒に揺れる。
    /// 場面に光が無いとき（チュートリアルなど）は、画面の上の中央から照らしているものとして描く。
    /// **全長は前と同じ約1.3秒。** この表示は手牌を1枚入れ替えるたびに出るので、長くすると待たされる。
    ///
    /// **出ている最中にもう一度呼ばれたら、前の表示をフェードアウトで消してから新しく出す**
    /// （フロー図の「if既に役名表示が出ているか → YES → 前の役名表示がフェードアウトで消える」）。
    ///
    /// 対局シーンは本編とチュートリアルの2つあるので、**シーンには置かず実行時に組み立てる。**
    /// 薄くする UI も決め打ちせず、出すたびに字と重なっている物をその場で探す。
    /// </summary>
    public class HandRankCallUI : MonoBehaviour
    {
        /// <summary>その場でフェードインする時間。</summary>
        private const float FadeInSeconds = 0.2f;

        /// <summary>出始めてから消え始めるまで（フェードインを含む）。</summary>
        private const float HoldSeconds = 1.05f;

        /// <summary>消えるまで。</summary>
        private const float FadeOutSeconds = 0.25f;

        /// <summary>差し替えのときに前の表示を消す時間。待たせすぎないよう短くする。</summary>
        private const float ReplaceFadeSeconds = 0.15f;

        /// <summary>字の下に重なっている UI を、どこまで薄くするか（1 で元のまま）。</summary>
        private const float CoveredAlpha = 0.18f;

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

        /// <summary>
        /// 薄くしない Canvas。自分・マウスの指先・クリックの反応は、字と重なっても薄くしない。
        /// </summary>
        private static readonly HashSet<string> NeverDimCanvases = new HashSet<string>
        {
            "HandRankCallUI", "UICursorCanvas", "ClickFeedbackCanvas",
        };

        /// <summary>これより幅の広い物（背景・卓・画面全体の幕）は薄くしない（800x600 基準の画面に対する割合）。</summary>
        private const float MaxDimWidthRatio = 0.8f;

        private CanvasGroup _group;
        /// <summary>1文字ぶん。奥から 影・地・光（切り抜き枠の中）。</summary>
        private class Glyph
        {
            public RectTransform Root;
            public TextMeshProUGUI Shadow, Base, Lit;
            public Material ShadowMat, BaseMat, LitMat;
            public RectTransform LitMask;
        }

        private Glyph _leftGlyph;
        private Glyph _rightGlyph;
        private TextMeshProUGUI _left => _leftGlyph.Base;
        private TextMeshProUGUI _right => _rightGlyph.Base;

        /// <summary>光の当たらない側の縁の明るさ（象牙色に掛ける）。</summary>
        private const float ShadeSide = 0.45f;
        /// <summary>光の当たる側の縁の色（ろうそくの暖かい白）。</summary>
        private static readonly Color LitColor = new Color(1f, 0.97f, 0.86f, 1f);
        /// <summary>影の色と濃さ。</summary>
        private static readonly Color ShadowColor = new Color(0.06f, 0f, 0.02f, 1f);
        private const float ShadowOpacity = 0.9f;
        /// <summary>影のずれの長さ（800x600 基準）。光から遠い字ほど伸ばす。</summary>
        private const float ShadowLength = 20f;
        /// <summary>影のぼかし（TMP の縁のやわらかさ）。</summary>
        private const float ShadowSoftness = 0.35f;
        private const float ShadowOutlineWidth = 0.32f;
        /// <summary>光の切り抜きを光の側へずらす量と、境目のぼかし（文字の高さに対する割合）。</summary>
        private const float LitShift = 0.42f;
        private const float LitSoftness = 0.55f;
        private Coroutine _routine;

        /// <summary>薄くした UI と、薄くする前の濃さ。</summary>
        private readonly Dictionary<Graphic, float> _dimmed = new Dictionary<Graphic, float>();
        /// <summary>薄くした UI に、いまどれだけ効かせているか（0 で元のまま、1 で <see cref="CoveredAlpha"/>）。</summary>
        private float _dimAmount;

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

        private void OnDisable() { RestoreDimmed(); }
        private void OnDestroy() { RestoreDimmed(); }

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
            _leftGlyph = BuildGlyph("Left", -GlyphOffsetX, font);
            _rightGlyph = BuildGlyph("Right", GlyphOffsetX, font);
        }

        private Glyph BuildGlyph(string name, float offsetX, TMP_FontAsset font)
        {
            float box = GlyphSize * 1.4f;
            var root = NewRect(name, transform);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(offsetX, GlyphCenterY);
            root.sizeDelta = new Vector2(box, box);

            var g = new Glyph { Root = root };
            g.Shadow = BuildLabel("Shadow", root, font, out g.ShadowMat);
            g.Base = BuildLabel("Base", root, font, out g.BaseMat);

            // 光は、光の側へずらした枠（ぼかし付き）で切り抜く。字は枠と逆へずらして元の位置に留める
            g.LitMask = NewRect("LitMask", root);
            g.LitMask.anchorMin = g.LitMask.anchorMax = g.LitMask.pivot = new Vector2(0.5f, 0.5f);
            g.LitMask.sizeDelta = new Vector2(box, box);
            var mask = g.LitMask.gameObject.AddComponent<RectMask2D>();
            int soft = Mathf.RoundToInt(GlyphSize * LitSoftness);
            mask.softness = new Vector2Int(soft, soft);
            g.Lit = BuildLabel("Lit", g.LitMask, font, out g.LitMat);

            if (g.ShadowMat != null)
            {
                // 影は縁より少し太く、ぼかす（細いままだと字の縁に隠れて見えない）
                g.ShadowMat.SetFloat(ShaderUtilities.ID_OutlineWidth, ShadowOutlineWidth);
                g.ShadowMat.SetFloat(ShaderUtilities.ID_OutlineSoftness, ShadowSoftness);
            }
            g.Shadow.UpdateMeshPadding();
            return g;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private TextMeshProUGUI BuildLabel(string name, Transform parent, TMP_FontAsset font, out Material material)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(GlyphSize * 1.4f, GlyphSize * 1.4f);

            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = GlyphSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.color = Color.white;
            material = ApplyOutlineOnly(label);
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
            if (_group != null) _group.alpha = 0f;
            RestoreDimmed();
        }

        private IEnumerator ShowRoutine(string rankName)
        {
            // 前の表示が残っていれば、まずそれを消す（フロー図の分岐）。薄くした UI はそのまま
            if (_group.alpha > 0f)
            {
                yield return FadeTo(0f, ReplaceFadeSeconds, keepDim: true);
            }

            int half = Mathf.Max(1, rankName.Length / 2);
            SetGlyphText(_leftGlyph, rankName.Substring(0, half));
            SetGlyphText(_rightGlyph, half < rankName.Length ? rankName.Substring(half) : string.Empty);
            ApplyLighting();

            // 字と重なっている UI を探して、字と一緒に薄くする
            CollectCovered();
            KillingMahjong.UI.Effects.HandSelectionFeedbackAudio.PlayRankImpact();

            float elapsed = 0f;
            for (float t = 0f; t < FadeInSeconds; )
            {
                float step = Step();
                t += step; elapsed += step;
                float u = Mathf.Clamp01(PresentationTiming.Sample(t) / FadeInSeconds);
                _group.alpha = u;
                SetDim(Mathf.Max(_dimAmount, u));
                yield return null;
            }
            _group.alpha = 1f;
            SetDim(1f);

            while (elapsed < HoldSeconds) { elapsed += Step(); yield return null; }
            yield return FadeTo(0f, FadeOutSeconds, keepDim: false);

            RestoreDimmed();
            _routine = null;
        }

        private static void SetGlyphText(Glyph g, string text)
        {
            g.Shadow.text = g.Base.text = g.Lit.text = text;
            g.Shadow.ForceMeshUpdate();
            g.Base.ForceMeshUpdate();
            g.Lit.ForceMeshUpdate();
        }

        /// <summary>
        /// 光を当てる。光の位置（画面の px）から、字ごとに光の向き・影のずれ・明るさを決める。
        /// </summary>
        private void ApplyLighting()
        {
            var light = KillingMahjong.UI.Effects.SceneLight.Current;
            bool hasLight = light != null && light.isActiveAndEnabled;
            Vector2 lightScreen = hasLight ? light.ScreenPosition : new Vector2(Screen.width * 0.5f, Screen.height * 0.8f);
            float flicker = hasLight ? light.ShadowFlicker : 1f;
            // ShadowFlicker は 1 の前後に数%揺れる。縁はそれより少し大きめに揺らして、炎に照らされて見えるようにする
            float k = Mathf.Clamp(0.94f + (flicker - 1f) * 1.5f, 0.82f, 1.06f);

            var root = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, lightScreen, null, out Vector2 lightLocal)) return;
            LightGlyph(_leftGlyph, lightLocal, k, flicker);
            LightGlyph(_rightGlyph, lightLocal, k, flicker);
        }

        private static void LightGlyph(Glyph g, Vector2 lightLocal, float k, float flicker)
        {
            Vector2 center = g.Root.anchoredPosition;
            Vector2 toLight = lightLocal - center;
            float dist = toLight.magnitude;
            Vector2 dir = dist > 1e-3f ? toLight / dist : Vector2.up;

            // 光: 枠を光の側へずらし、字は逆へずらして元の位置に留める
            Vector2 shift = dir * GlyphSize * LitShift;
            g.LitMask.anchoredPosition = shift;
            g.Lit.rectTransform.anchoredPosition = -shift;

            // 影: 光と反対へ。光から遠い字ほど少し長く（LitArtDropShadow と同じ考え方）
            float stretch = Mathf.Clamp(dist / 300f, 0.7f, 1.6f);
            Vector2 off = -dir * ShadowLength * stretch;
            g.Shadow.rectTransform.anchoredPosition = new Vector2(Mathf.Round(off.x), Mathf.Round(off.y));

            var shade = BoneColor * ShadeSide * k; shade.a = 1f;
            var lit = LitColor * k; lit.a = 1f;
            var sh = ShadowColor; sh.a = ShadowOpacity * Mathf.Clamp01(flicker);
            if (g.BaseMat != null) g.BaseMat.SetColor(ShaderUtilities.ID_OutlineColor, shade);
            if (g.LitMat != null) g.LitMat.SetColor(ShaderUtilities.ID_OutlineColor, lit);
            if (g.ShadowMat != null) g.ShadowMat.SetColor(ShaderUtilities.ID_OutlineColor, sh);
        }

        /// <summary>
        /// 字の矩形と重なっている UI を集める。自分・マウスの指先・画面いっぱいの物（背景・卓・幕）は除く。
        /// 前の表示で薄くしたままの物は、薄くする前の濃さを覚え直さない（薄いまま覚えると戻らなくなる）。
        /// </summary>
        private void CollectCovered()
        {
            var areas = new List<Rect>(2);
            AddScreenRect(_left, areas);
            AddScreenRect(_right, areas);
            if (areas.Count == 0) return;

            float maxWidth = Screen.width * MaxDimWidthRatio;
            var corners = new Vector3[4];
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            {
                if (g == null || !g.isActiveAndEnabled || g.canvas == null || _dimmed.ContainsKey(g)) continue;
                var root = g.canvas.rootCanvas;
                if (root == null || NeverDimCanvases.Contains(root.name) || g.transform.IsChildOf(transform)) continue;

                Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                g.rectTransform.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
                var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                if (r.width > maxWidth) continue;

                bool overlaps = false;
                foreach (var area in areas) if (area.Overlaps(r)) { overlaps = true; break; }
                if (!overlaps) continue;

                _dimmed[g] = g.canvasRenderer.GetAlpha();
            }
        }

        /// <summary>字の形（文字の縁）が実際に占める画面上の矩形。</summary>
        private static void AddScreenRect(TextMeshProUGUI label, List<Rect> areas)
        {
            if (label == null || string.IsNullOrEmpty(label.text)) return;
            var b = label.textBounds;
            if (b.size.x <= 0f) return;
            var rt = label.rectTransform;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(b.min));
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(b.max));
            areas.Add(Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y)));
        }

        /// <summary>薄くした UI の効き具合を変える（0 で元のまま、1 で <see cref="CoveredAlpha"/> 倍）。</summary>
        private void SetDim(float amount)
        {
            _dimAmount = Mathf.Clamp01(amount);
            float k = Mathf.Lerp(1f, CoveredAlpha, _dimAmount);
            foreach (var pair in _dimmed)
                if (pair.Key != null) pair.Key.canvasRenderer.SetAlpha(pair.Value * k);
        }

        /// <summary>薄くした UI を元の濃さに戻して、覚えていた物を忘れる。</summary>
        private void RestoreDimmed()
        {
            foreach (var pair in _dimmed)
                if (pair.Key != null) pair.Key.canvasRenderer.SetAlpha(pair.Value);
            _dimmed.Clear();
            _dimAmount = 0f;
        }

        /// <summary>
        /// 毎フレーム：縁の明るさをろうそくのちらつきに合わせる。
        /// あわせて**出しっぱなしを防ぐ保険（2026-09-20）**。コルーチンが外から止められると、
        /// 最後に当てた alpha のまま画面に残る（決着画面へ役名が残ったことがある）。
        /// </summary>
        private void Update()
        {
            if (_group == null) return;

            if (_group.alpha > 0f) ApplyLighting();

            if (_routine != null || _group.alpha <= 0f)
            {
                if (_routine == null && _dimmed.Count > 0) RestoreDimmed();
                return;
            }
            _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.unscaledDeltaTime / FadeOutSeconds);
            SetDim(Mathf.Min(_dimAmount, _group.alpha));
            if (_group.alpha <= 0f) RestoreDimmed();
        }

        /// <summary>字を薄くする。<paramref name="keepDim"/> が false なら、薄くした UI も同じだけ戻す。</summary>
        private IEnumerator FadeTo(float target, float seconds, bool keepDim)
        {
            float from = _group.alpha;
            float dimFrom = _dimAmount;
            if (seconds <= 0f) { _group.alpha = target; if (!keepDim) SetDim(target); yield break; }

            for (float t = 0f; t < seconds; t += Step())
            {
                float u = PresentationTiming.Sample(t) / seconds;
                _group.alpha = Mathf.Lerp(from, target, u);
                if (!keepDim) SetDim(Mathf.Lerp(dimFrom, target, u));
                yield return null;
            }
            _group.alpha = target;
            if (!keepDim) SetDim(target);
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
