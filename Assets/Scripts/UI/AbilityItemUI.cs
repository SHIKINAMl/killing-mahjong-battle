using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro usage based on other files
using UnityEngine.EventSystems;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 能力メニューの1行。**縁 → 面 → 下の帯（コスト）**の3層のドット絵タイル。
    ///
    /// 作りは `YakuListUI.BuildTile` と同じ考え方に揃えてある（2026-08-24）。
    /// 参考は本人が提示した「スイスイ株式会社」のアップグレードタイル。
    ///
    /// **面・帯・コストの器は実行時に組み立てる。** シーンに置くと対局シーンが
    /// 2つ（`OpeningScene` / `UIテストシーン`）あるので、片方だけ直す事故が起きる。
    ///
    /// **説明文はここには出さない。** 以前は 120x20 の枠に長文を横スクロール
    /// （マーキー）で流していたが、どの瞬間も文の途中しか読めず、4行が各々の
    /// 周期でズレて動くので画面が落ち着かなかった。全文は親の説明欄に出す。
    /// </summary>
    public class AbilityItemUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private RectTransform descriptionContainer;
        [SerializeField] private Image background;

        [Header("Settings")]
        [Tooltip("HPが足りないときのコスト文字色")]
        [SerializeField] private Color unaffordableCostColor = new Color(1f, 0.35f, 0.35f, 1f);

        // `normalColor` / `selectedColor` / `unaffordableColor` / `scrollSpeed` は 2026-08-24 に削除。
        // 色は下の定数に一本化し、説明文の横スクロールごと廃止した。

        // ---- タイルの見た目（調整値。シーンではなくここを触る）----
        //
        // **寸法はすべて 1ドット＝2UI単位の格子に乗せる。**
        // パネルの枠 `UI_Anim10` が 1010px を 202 で表示＝10px のドットが 2UI単位。
        // 半端な値を入れるとドットの目が合わない。

        /// <summary>縁の太さ。1ドット。</summary>
        private const float TileBorder = 2f;

        /// <summary>コストを出す帯の高さ。7ドット。</summary>
        private const float TileStripHeight = 14f;

        /// <summary>面の色。パネルの内側が暗い赤の紙なので、面は暗い紺で沈める。</summary>
        private static readonly Color TileFill = new Color32(0x1B, 0x23, 0x38, 0xFF);

        /// <summary>縁の色。暗い面に細い明色の縁、が参考動画に共通していた作り。</summary>
        private static readonly Color TileBorderColor = new Color32(0x35, 0x42, 0x70, 0xFF);

        /// <summary>選んでいる行の縁。**面ではなく縁を変える。**
        /// 面を黄色にすると白文字が読めなくなる。</summary>
        private static readonly Color TileBorderSelected = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        /// <summary>選んでいる行の面。縁だけだと弱いので、少しだけ明るくする。</summary>
        private static readonly Color TileFillSelected = new Color32(0x2A, 0x3E, 0x6E, 0xFF);

        /// <summary>発動できない行の縁と面。彩度を落として沈める。</summary>
        private static readonly Color TileBorderDim = new Color32(0x3A, 0x3A, 0x42, 0xFF);
        private static readonly Color TileFillDim = new Color32(0x26, 0x26, 0x2C, 0xFF);

        /// <summary>払える額。YakuListUI の帯と同じ黄色に揃える。</summary>
        private static readonly Color CostAffordableColor = new Color32(0xFF, 0xD3, 0x4D, 0xFF);

        /// <summary>能力名の色。発動できないときは沈める。</summary>
        private static readonly Color NameColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color NameDimColor = new Color32(0x8A, 0x8A, 0x93, 0xFF);

        private const float NameFontSize = 14f;
        private const float NameMinFontSize = 9f;
        private const float CostFontSize = 11f;
        private const float CostMinFontSize = 8f;

        /// <summary>子オブジェクトの名前。作り直しを避けるため、これで在庫を探す。</summary>
        private const string TileFillName = "TileFill";
        private const string TileStripName = "TileStrip";

        private AbilityUI parentUI;
        private int abilityIndex;

        private Image tileFill;
        private Image tileStrip;

        /// <summary>この行の説明文。親の説明欄に出すために持っておく。</summary>
        public string Description { get; private set; } = "";

        /// <summary>現在のHPで発動できるか。false のときはグレーアウトして発動要求も弾く。</summary>
        public bool IsAffordable { get; private set; } = true;

        private bool isSelected;

        public void Setup(AbilityUI parent, int index, string name, int cost, string description, bool affordable = true)
        {
            this.parentUI = parent;
            this.abilityIndex = index;
            this.IsAffordable = affordable;
            this.Description = description ?? "";

            BuildTile();

            if (nameText != null) nameText.text = name;
            if (costText != null) costText.text = $"-{cost}";

            // 説明文はタイルには出さない。器がシーンに残っていても畳んでおく
            if (descriptionText != null) descriptionText.gameObject.SetActive(false);
            if (descriptionContainer != null) descriptionContainer.gameObject.SetActive(false);

            Deselect();
        }

        /// <summary>
        /// 縁・面・帯を組み立てる。**作り直さない**（`FindOrCreate` で在庫を使う）。
        ///
        /// 縁は行そのものの `Image` の地色。その内側に面を1枚敷き、
        /// 面の下辺に沿ってコストの帯を貼る。9スライスの素材は要らない。
        /// </summary>
        private void BuildTile()
        {
            var row = transform as RectTransform;
            if (row == null) return;

            // 縁 = タイルの地。**プレハブの `AbilityItemPanel` をそのまま土台に使う。**
            // 行(136x40)の中に 120x45 の板が浮いていて、面と帯をこの板の裏に作ると
            // まるごと隠れる（左右 8 ずつだけ黒く覗く）。板を行いっぱいに張り直す。
            if (background == null) background = GetComponent<Image>();
            if (background == null) background = gameObject.AddComponent<Image>();
            background.sprite = null;

            var rt = background.rectTransform;
            if (rt != row)
            {
                rt.SetParent(row, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.SetAsFirstSibling();
            }

            // 面
            tileFill = FindOrCreateGraphic<Image>(rt, TileFillName);
            var fillRect = tileFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(TileBorder, TileBorder);
            fillRect.offsetMax = new Vector2(-TileBorder, -TileBorder);
            fillRect.SetAsFirstSibling();

            // コストの帯（面の下辺に沿わせる）
            tileStrip = FindOrCreateGraphic<Image>(rt, TileStripName);
            var stripRect = tileStrip.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 0f);
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.offsetMin = new Vector2(TileBorder, TileBorder);
            stripRect.offsetMax = new Vector2(-TileBorder, TileBorder + TileStripHeight);
            stripRect.SetSiblingIndex(1);

            // 能力名は帯の上の残りいっぱいに置く
            if (nameText != null)
            {
                var nr = nameText.rectTransform;
                nr.SetParent(rt, false);
                nr.anchorMin = new Vector2(0f, 0f);
                nr.anchorMax = new Vector2(1f, 1f);
                nr.offsetMin = new Vector2(TileBorder + 2f, TileBorder + TileStripHeight);
                nr.offsetMax = new Vector2(-(TileBorder + 2f), -TileBorder);
                nr.localScale = Vector3.one;
                ApplyText(nameText, NameFontSize, NameMinFontSize, TextAlignmentOptions.Center);
                nameText.transform.SetAsLastSibling();
            }

            // コストは帯の中で右寄せ
            if (costText != null)
            {
                var cr = costText.rectTransform;
                cr.SetParent(stripRect, false);
                cr.anchorMin = Vector2.zero;
                cr.anchorMax = Vector2.one;
                cr.offsetMin = new Vector2(3f, 0f);
                cr.offsetMax = new Vector2(-3f, 0f);
                cr.localScale = Vector3.one;
                ApplyText(costText, CostFontSize, CostMinFontSize, TextAlignmentOptions.Right);
            }
        }

        /// <summary>
        /// 文字を枠いっぱいに張り直して自動縮小させる。
        ///
        /// **シーン上の TMP の矩形とフォント値は当てにならない**（能力名の矩形は
        /// 220 幅で、120 幅のタイルの倍あった）。自動縮小は文字の矩形を基準にするので、
        /// はみ出した矩形のままだと一向に縮まない。`margin` も残っていると中央揃えでもずれる。
        /// </summary>
        private static void ApplyText(TextMeshProUGUI text, float max, float min, TextAlignmentOptions align)
        {
            text.margin = Vector4.zero;
            text.alignment = align;
            text.enableAutoSizing = true;
            text.fontSizeMax = max;
            text.fontSizeMin = min;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        /// <summary>子を名前で探し、無ければ作る。**作り直さない**のが肝心。</summary>
        private static T FindOrCreateGraphic<T>(RectTransform parent, string childName) where T : Graphic
        {
            var existing = parent.Find(childName);
            if (existing != null)
            {
                var found = existing.GetComponent<T>();
                if (found != null) return found;
            }

            var go = new GameObject(childName, typeof(RectTransform), typeof(T));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            var graphic = go.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            parentUI.OnAbilitySelected(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (parentUI != null) parentUI.ShowTooltip(Description);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (parentUI != null) parentUI.HideTooltip();
        }

        public void Select()
        {
            isSelected = true;
            ApplyColors();
        }

        public void Deselect()
        {
            isSelected = false;
            ApplyColors();
        }

        /// <summary>
        /// 縁・面・文字の色を状態から決める。
        ///
        /// **発動できない行も、選んだかどうかが分かるようにする。**
        /// 以前は `Select` も `Deselect` も同じ灰色を返していたので、
        /// 押せない行はクリックが効いたのかどうか画面から読めなかった。
        /// </summary>
        private void ApplyColors()
        {
            Color border;
            Color fill;

            // **帯の色は状態で変えない。** 選択の色（黄）を帯にも塗ると、
            // 同じ黄のコストの数字が帯に溶けて消える。変えるのは外の縁だけ。
            Color strip = IsAffordable ? TileBorderColor : TileBorderDim;

            if (!IsAffordable)
            {
                border = isSelected ? unaffordableCostColor : TileBorderDim;
                fill = TileFillDim;
            }
            else if (isSelected)
            {
                border = TileBorderSelected;
                fill = TileFillSelected;
            }
            else
            {
                border = TileBorderColor;
                fill = TileFill;
            }

            if (background != null) background.color = border;
            if (tileFill != null) tileFill.color = fill;
            if (tileStrip != null) tileStrip.color = strip;

            if (nameText != null) nameText.color = IsAffordable ? NameColor : NameDimColor;
            if (costText != null) costText.color = IsAffordable ? CostAffordableColor : unaffordableCostColor;
        }

        public int AbilityIndex => abilityIndex;
    }
}
