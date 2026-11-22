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
    ///   ・象牙色の字（前は縁だけで中を透かしていたが、のちに中も塗った。下の「段」）。暗幕で画面全体を落とす案は退けられた
    ///   ・**字の下に重なっている UI だけを薄くして道を空ける**（案 A）。字が消えるのに合わせて戻す
    ///     （2026-11-22 から、重なった部品の入っている**まとまりごと**薄くする。部品ごとだと同じ物の中で半端に残った）
    ///   ・**字は位置も大きさも変えず、その場でフェードインする**（「血が抜ける演出よりは、
    ///     文字の位置が変わらないフェードインしてほしい」）。特大から縮む動き・血の演出はやめた
    ///
    ///   0.00s  字がその場でフェードイン（0.2秒）。同時に、字の下の UI が薄くなる
    ///   1.05s  字が消え始め（0.25秒）、薄くした UI も戻る
    ///
    /// **字にはろうそく（<see cref="Effects.SceneLight"/>）の光を当てる（2026-11-21「この即席満貫の文字には光を当てて、
    /// いい感じの影を作ってほしい」）。**
    /// **中は塗る。字の層を光と反対へ段々にずらして重ねる（2026-11-21「中身空白ではなく、
    /// 階層がずれてちゃんと塗られているようにして」）。** 字ごとに奥から:
    ///   ・影: いちばん奥。ぼかした暗い字（光から遠いほど長い）
    ///   ・段: 血の赤から暗い赤へ、光と反対へ1段ずつずらして塗った字（<see cref="DepthLayers"/> 枚）
    ///   ・面: 少し暗い象牙色で塗った字（光の当たらない側）
    ///   ・光: 明るい象牙色の字を、光の来る側だけ柔らかく切り抜いて重ねる（RectMask2D のぼかし）
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
        /// <summary>1文字ぶん。奥から 影・段（奥→手前）・面・光（切り抜き枠の中）。</summary>
        private class Glyph
        {
            public RectTransform Root;
            public TextMeshProUGUI Shadow, Base, Lit;
            public Material ShadowMat, BaseMat, LitMat;
            public TextMeshProUGUI[] Depth;
            public Material[] DepthMats;
            public RectTransform LitMask;
        }

        /// <summary>面の後ろに重ねる段の数と、1段のずれ（800x600 基準）。</summary>
        private const int DepthLayers = 3;
        private const float DepthStep = 7f;
        /// <summary>段の色。手前ほど明るい血の赤、奥ほど暗い赤。</summary>
        private static readonly Color DepthNearColor = new Color(0.72f, 0.06f, 0.10f, 1f);
        private static readonly Color DepthFarColor = new Color(0.26f, 0.01f, 0.04f, 1f);

        private Glyph _leftGlyph;
        private Glyph _rightGlyph;
        private TextMeshProUGUI _left => _leftGlyph.Base;
        private TextMeshProUGUI _right => _rightGlyph.Base;

        /// <summary>光の当たらない側の面の明るさ（象牙色に掛ける）。段の赤と分かれる程度に明るく残す。</summary>
        private const float ShadeSide = 0.7f;
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
            // 段は奥（ずれの大きい物）から並べ、手前の段が上に描かれるようにする
            g.Depth = new TextMeshProUGUI[DepthLayers];
            g.DepthMats = new Material[DepthLayers];
            for (int i = DepthLayers - 1; i >= 0; i--)
                g.Depth[i] = BuildLabel("Depth" + (i + 1), root, font, out g.DepthMats[i]);
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
            material = ApplySolid(label);
            return label;
        }

        /// <summary>
        /// 中まで塗った字にする（色は <see cref="SetSolidColor"/>）。
        ///
        /// **`outlineWidth` / `outlineColor` に値を入れるだけでは1本も描かれない。**
        /// TMP はマテリアルの `OUTLINE_ON` キーワードが立っていないと縁を無視する（2026-09-29 に録画で確認）。
        /// `fontMaterial` は**この文字専用の複製**を返すので、共有のフォント資産は汚さない。
        /// </summary>
        private static Material ApplySolid(TextMeshProUGUI label)
        {
            var mat = label.fontMaterial;
            if (mat == null) return null;

            mat.EnableKeyword("OUTLINE_ON");
            // 中を塗ると、縁で太らせた分だけ字のすき間（「貫」の口、「満」の線の間）がつぶれる（2026-11-21 に撮って確認）。
            // 塗った字は太らせない。縁を使うのは影だけ（ぼかしのため）
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            SetSolidColor(mat, BoneColor);
            label.UpdateMeshPadding();
            return mat;
        }

        private static void SetSolidColor(Material mat, Color color)
        {
            if (mat == null) return;
            mat.SetColor(ShaderUtilities.ID_FaceColor, color);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, color);
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
            foreach (var d in g.Depth) { d.text = text; d.ForceMeshUpdate(); }
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

            // 段: 光と反対へ1段ずつずらす（字が奥へ積み重なって見える）
            for (int i = 0; i < g.Depth.Length; i++)
            {
                Vector2 d = -dir * DepthStep * (i + 1);
                g.Depth[i].rectTransform.anchoredPosition = new Vector2(Mathf.Round(d.x), Mathf.Round(d.y));
                float f = g.Depth.Length > 1 ? i / (float)(g.Depth.Length - 1) : 0f;
                var dc = Color.Lerp(DepthNearColor, DepthFarColor, f) * k; dc.a = 1f;
                SetSolidColor(g.DepthMats[i], dc);
            }

            // 影: いちばん奥の段のさらに先へ。光から遠い字ほど少し長く（LitArtDropShadow と同じ考え方）
            float stretch = Mathf.Clamp(dist / 300f, 0.7f, 1.6f);
            Vector2 off = -dir * (DepthStep * DepthLayers + ShadowLength * stretch);
            g.Shadow.rectTransform.anchoredPosition = new Vector2(Mathf.Round(off.x), Mathf.Round(off.y));

            var shade = BoneColor * ShadeSide * k; shade.a = 1f;
            var lit = LitColor * k; lit.a = 1f;
            var sh = ShadowColor; sh.a = ShadowOpacity * Mathf.Clamp01(flicker);
            SetSolidColor(g.BaseMat, shade);
            SetSolidColor(g.LitMat, lit);
            SetSolidColor(g.ShadowMat, sh);
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
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            {
                if (!CanDim(g) || _dimmed.ContainsKey(g)) continue;
                if (!TryScreenRect(g, out Rect r) || r.width > maxWidth) continue;

                bool overlaps = false;
                foreach (var area in areas) if (area.Overlaps(r)) { overlaps = true; break; }
                if (!overlaps) continue;

                // **重なった部品だけでなく、その部品の入っているまとまりごと薄くする**（2026-11-22
                // 「半透明になっているものとなっていないものがある。これの徹底をちゃんとして」）。
                // 前は部品1つずつ字と重なるかで決めていたので、役一覧の板は薄いのに上の札と見出しは濃い、
                // 体力の袋は薄いのに数字と管は濃い、のように同じ物の中で半端になっていた
                Transform unit = DimUnitOf(g);
                if (!_dimUnits.Contains(unit)) _dimUnits.Add(unit);
                DimUnder(unit);
            }
        }

        /// <summary>薄くした まとまり。出ている間に表示された部品も、ここから拾って同じに薄くする。</summary>
        private readonly List<Transform> _dimUnits = new List<Transform>();

        /// <summary>まとまりの大きさの上限（画面に対する割合）。これより大きい親はまとまりにしない（手牌・卓など）。</summary>
        private const float MaxUnitWidthRatio = 0.45f;
        private const float MaxUnitHeightRatio = 0.7f;

        private bool CanDim(Graphic g)
        {
            if (g == null || !g.isActiveAndEnabled || g.canvas == null) return false;
            var root = g.canvas.rootCanvas;
            return root != null && !NeverDimCanvases.Contains(root.name) && !g.transform.IsChildOf(transform);
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static bool TryScreenRect(Graphic g, out Rect r)
        {
            r = default;
            var root = g.canvas != null ? g.canvas.rootCanvas : null;
            if (root == null) return false;
            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            g.rectTransform.GetWorldCorners(Corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
            r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            return true;
        }

        /// <summary>
        /// 部品の入っている「まとまり」（役一覧・相手の体力・自分の体力・台詞など）。
        /// 親をたどり、中に見えている部品を全部囲んでも画面の一部（幅45%・高さ70%まで）に収まる、いちばん上の親。
        /// 大きすぎる親（手牌や卓まで入る入れ物）まで上がると、字と関係ない物まで薄くなるので止める。
        /// </summary>
        private Transform DimUnitOf(Graphic g)
        {
            Transform unit = g.transform;
            Transform rootCanvas = g.canvas.rootCanvas.transform;
            float maxW = Screen.width * MaxUnitWidthRatio, maxH = Screen.height * MaxUnitHeightRatio;
            for (Transform p = g.transform.parent; p != null && p != rootCanvas; p = p.parent)
            {
                bool any = false; Rect u = default;
                foreach (var c in p.GetComponentsInChildren<Graphic>(false))
                {
                    if (!c.isActiveAndEnabled || !TryScreenRect(c, out Rect r)) continue;
                    u = any ? Rect.MinMaxRect(Mathf.Min(u.xMin, r.xMin), Mathf.Min(u.yMin, r.yMin),
                        Mathf.Max(u.xMax, r.xMax), Mathf.Max(u.yMax, r.yMax)) : r;
                    any = true;
                }
                if (!any || u.width > maxW || u.height > maxH) break;
                unit = p;
            }
            return unit;
        }

        /// <summary>まとまりの中の、見えている部品を薄くする側に入れる（まだ入っていない物だけ）。</summary>
        private void DimUnder(Transform unit)
        {
            if (unit == null) return;
            foreach (var g in unit.GetComponentsInChildren<Graphic>(false))
            {
                if (!CanDim(g) || _dimmed.ContainsKey(g)) continue;
                _dimmed[g] = g.canvasRenderer.GetAlpha();
                g.canvasRenderer.SetAlpha(_dimmed[g] * Mathf.Lerp(1f, CoveredAlpha, _dimAmount));
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
            _dimUnits.Clear();
            _dimAmount = 0f;
        }

        /// <summary>
        /// 薄くしている間、毎フレーム当て直す。ボタンの色の切り替え（押せる/押せない・カーソル）は
        /// 濃さも一緒に戻してしまうので、1回当てただけだとボタンだけ濃く戻ることがある。
        /// 出ている間に表示された部品（札が増えた・台詞が出た）も、同じまとまりなら薄くする。
        /// </summary>
        private void KeepDimmed()
        {
            if (_dimmed.Count == 0 || _dimAmount <= 0f) return;
            foreach (var unit in _dimUnits) DimUnder(unit);
            SetDim(_dimAmount);
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
            KeepDimmed();

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
