using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class AbilityUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button triggerButton; // The Red Button on the right
        [SerializeField] private RectTransform abilityWindow; // The window on the left
        [SerializeField] private Vector2 hiddenPosition = new Vector2(-500, -500); // Off-screen or hidden
        [SerializeField] private Vector2 showPosition = new Vector2(100, 100); // On-screen
        [SerializeField] private float animationDuration = 0.2f;

        [Header("Button Sprites")]
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite pressedSprite;
        [SerializeField] private float buttonPressDuration = 0.2f; // インスペクターで時間を指定できるように追加
        private Image triggerButtonImage;

        [Header("Window Components")]
        [SerializeField] private AbilityItemUI itemPrefab;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private Button closeButton; // The ▼ button
        [SerializeField] private Button activateButton; // The Activate button
        
        // 行の寸法（itemOffsetX / itemOffsetY / itemHeight / itemSpacing）は
        // 2026-08-24 に削除。**巻物の紙の面を実測して求めた定数**（下の RowWidth ほか）に
        // 一本化した。シーンの値はプレハブと食い違っていて、どちらが効いているか読めなかった。

        [Header("Tooltip Settings")]
        [SerializeField] private GameObject tooltipPanel;
        [SerializeField] private TMPro.TextMeshProUGUI tooltipText;

        [Header("巻物のコマ送り")]
        [Tooltip("開くときの順番でコマを入れる（UI_Anim5 → UI_Anim10）。" +
                 "閉じるときはこの逆順で再生する。空ならコマ送りをせず、" +
                 "従来どおりスライドだけで開閉する。")]
        [SerializeField] private Sprite[] openFrames;

        [Tooltip("1コマあたりの秒数。6コマなら 0.06 で全体 0.36 秒。")]
        [SerializeField] private float frameSeconds = 0.06f;

        [Tooltip("コマを表示する Image。未設定なら abilityWindow の Image を使う。")]
        [SerializeField] private Image windowFrameImage;

        [Tooltip("開き切るまで伏せておく中身（一覧・閉じるボタン・説明欄）。\n" +
                 "**巻物が小さいコマの上に行が浮くのを防ぐ。** " +
                 "閉じるときは先にここを消してから巻き取る。")]
        [SerializeField] private GameObject[] contentsShownWhenOpen;

        // Internal State
        private bool isWindowVisible = false;
        private Coroutine currentAnimationCoroutine;
        private Coroutine buttonPressCoroutine;
        private System.Collections.Generic.List<AbilityItemUI> instantiatedItems = new System.Collections.Generic.List<AbilityItemUI>();
        private AbilityItemUI currentSelection;

        // Real Data Class matching Python
        //
        // コストはここに持たない。額はサーバーが決めるものなので、
        // 表示のたびに GameRules.GetSkillCost() から取り直す（PopulateList / OnActivateClicked）。
        // 以前はここにも 1200 などの実数が書いてあったが、実際には使われておらず、
        // 「Unity 側にもコスト表がある」と誤解させるだけだった。
        [System.Serializable]
        public class AbilityData
        {
            public string skillType;
            public string name;
            public string description;

            public AbilityData(string type, string n, string d)
            {
                skillType = type;
                name = n;
                description = d;
            }
        }
        
        private System.Collections.Generic.List<AbilityData> realAbilities;

        private void Start()
        {
            if (triggerButton != null)
            {
                triggerButton.onClick.AddListener(OnTriggerClicked);
                triggerButtonImage = triggerButton.GetComponent<Image>();
                if (triggerButtonImage != null && normalSprite != null)
                {
                    triggerButtonImage.sprite = normalSprite;
                }
            }
            
            if (closeButton != null)
                closeButton.onClick.AddListener(() => CloseWindow());

            // **閉じるボタンと同じ物なら発動を繋がない。**
            // 両シーンとも `activateButton` に `CloseButton` が入っていて、X を押すと
            // `CloseWindow` → `DeselectAll` → `OnActivateClicked` の順に走っていた。
            // 選択が消えたあとなので発動は空振りだが、繋がっていること自体が誤解のもと。
            // 発動は「選んだ行をもう一度押す」（OnAbilitySelected）に一本化してある。
            if (activateButton != null && activateButton != closeButton)
                activateButton.onClick.AddListener(OnActivateClicked);

            if (abilityWindow != null)
                abilityWindow.anchoredPosition = hiddenPosition;

            animationDuration = 0.2f; // 強制的に0.2秒にする

            EnsureAbilities();

            StyleCloseButton();

            // Populate List
            PopulateList();

            // 起動時にツールチップを非表示にする
            HideTooltip();
        }

        /// <summary>
        /// アビリティ一覧を用意する。
        /// 非アクティブから有効化された直後に外部から開かれると Start より先に
        /// PopulateList が走るため、生成は Start 任せにせずここで担保する。
        /// </summary>
        // ---- パネルの内側の寸法（実測値。シーンではなくここを触る）----
        //
        // 枠の絵 `UI_Anim10` は 1010x1570 を 202x314 で表示している＝**1ドット＝2UI単位**。
        // 絵の中で「紙の面」になっているのは元画像の x 140..939 / y 250..1459 で、
        // UI 単位に直すと **160 x 242、中心は矩形の中心から (+7, -14)**。
        // 行をこの内側に収める。はみ出すと巻物の枠に食い込む。

        private const float PanelInnerWidth = 160f;
        private const float PanelInnerHeight = 242f;
        private static readonly Vector2 PanelInnerCenter = new Vector2(7f, -14f);

        /// <summary>行の幅。内枠 160 の左右に 12 ずつ余白（6ドット）。</summary>
        private const float RowWidth = 136f;

        /// <summary>行の高さ。20ドット。名前の段＋コストの帯が収まる最小。</summary>
        private const float RowHeight = 40f;

        /// <summary>行と行の間。3ドット。</summary>
        private const float RowSpacing = 6f;

        /// <summary>内枠の上端から一覧までの余白。2ドット。</summary>
        private const float ListTopMargin = 4f;

        /// <summary>説明欄の高さ。4行の一覧を引いた残り。</summary>
        private const float DescBoxHeight = 50f;

        public void CloseWindow(bool cancelSkill = true)
        {
            if (isWindowVisible) ToggleAbilityWindow(cancelSkill);
        }

        /// <summary>チュートリアルの実演用。閉じていれば開く。</summary>
        public void OpenWindow()
        {
            if (!isWindowVisible) ToggleAbilityWindow(false);
        }

        /// <summary>
        /// チュートリアルの誘導用。指定した skillType の行の RectTransform を返す。
        /// ウィンドウを開く前は行が生成されていないため null を返す。
        /// </summary>
        public RectTransform GetAbilityItemRect(string skillType)
        {
            if (realAbilities == null) return null;

            for (int i = 0; i < realAbilities.Count && i < instantiatedItems.Count; i++)
            {
                if (realAbilities[i].skillType != skillType) continue;
                return instantiatedItems[i] != null
                    ? instantiatedItems[i].GetComponent<RectTransform>()
                    : null;
            }
            return null;
        }

        public void ToggleAbilityWindow(bool cancelSkill = true)
        {
            isWindowVisible = !isWindowVisible;

            if (isWindowVisible)
            {
                PopulateList();
                HideTooltip();
            }
            else
            {
                DeselectAll(); // Deselect on close
                var uiMgr = FindFirstObjectByType<GameUIManager>();
                if (uiMgr != null && cancelSkill) uiMgr.CancelSkillSelection();

                Canvas rootCanvas = this.GetComponent<Canvas>();
                if (rootCanvas != null)
                {
                    rootCanvas.sortingOrder = _transitionSuppressed ? UISortingOrders.AbilityDuringTransition : UISortingOrders.InfoPanelNormal;
                }
            }

            if (currentAnimationCoroutine != null) StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = StartCoroutine(AnimateWindow(isWindowVisible ? showPosition : hiddenPosition, isWindowVisible));
        }

        /// <summary>
        /// 実演モード。チュートリアルで能力を「見せている」あいだ true にすると、
        /// 一覧は表示されるが選択も発動もできなくなる。
        ///
        /// これが無いと、実演中に光っている行を押されたときに
        /// DialogueUI.ShowText でチュートリアルのセリフが上書きされ、
        /// 送りボタン待ちのまま進行が止まってしまう。
        /// </summary>
        public bool IsDisplayOnly { get; set; }

        public void OnAbilitySelected(AbilityItemUI item)
        {
            if (IsDisplayOnly) return;

            if (currentSelection == item)
            {
                // すでに選択されているものをもう一度クリックしたら発動とする
                OnActivateClicked();
            }
            else
            {
                if (currentSelection != null) currentSelection.Deselect();
                currentSelection = item;
                currentSelection.Select();

                // **選んだら説明を出したままにする。**
                // 説明欄はホバーで出し引きしているので、これが無いと
                // 選んだ直後にカーソルを外しただけで、いま何を選んでいるのかの
                // 説明が読めなくなる（説明文を行から追い出したぶんの穴）
                ShowTooltip(currentSelection.Description);
            }
        }

        public void DeselectAll()
        {
            if (currentSelection != null)
            {
                currentSelection.Deselect();
                currentSelection = null;
            }
            HideTooltip();
        }
        private const string DescBoxName = "AbilityDescriptionBox";
        private const string DescFillName = "AbilityDescriptionFill";
        private const string DescTextName = "AbilityDescriptionText";

        /// <summary>説明欄の面。行のタイルと同じ暗い紺。</summary>
        private static readonly Color DescBoxColor = new Color32(0x1B, 0x23, 0x38, 0xE6);

        /// <summary>説明欄の縁。**行と同じ色にする。**
        /// 4行が縁を持つと、縁の有無が「一覧の仲間かどうか」の合図になる。
        /// 説明欄だけ縁が無いと、別の透明なパネルが割り込んで見える。</summary>
        private static readonly Color DescBorderColor = new Color32(0x35, 0x42, 0x70, 0xFF);

        /// <summary>縁の太さ。行のタイルと同じ1ドット。</summary>
        private const float DescBorder = 2f;

        /// <summary>閉じるボタンの縁。他の赤（コスト・HPバッジ）と同じ赤に揃える。</summary>
        private static readonly Color CloseBorderColor = new Color32(0xFF, 0x59, 0x59, 0xFF);

        private TMPro.TextMeshProUGUI _descText;

        /// <summary>何も選んでいないときの案内。空欄にすると欄が壊れて見える。</summary>
        private const string DescPlaceholder = "能力にカーソルを合わせると説明が出ます";

        /// <summary>説明欄を用意する。**シーンには置かない**（対局シーンが2つあるため）。</summary>
        /// <summary>
        /// 説明欄に全文を出す。名前はホバーの経路（`AbilityItemUI`）に合わせて残している。
        /// </summary>
        public void ShowTooltip(string description)
        {
            HideLegacyTooltip();

            var text = EnsureDescriptionBox();
            if (text == null) return;
            text.text = string.IsNullOrEmpty(description) ? DescPlaceholder : description;

            // AbilityUI 全体を最前面化する。中身の表示順が壊れないようルート(this)のみ設定する
            Canvas rootCanvas = this.GetComponent<Canvas>();
            if (rootCanvas == null)
            {
                rootCanvas = this.gameObject.AddComponent<Canvas>();
                this.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = _transitionSuppressed ? UISortingOrders.AbilityDuringTransition : UISortingOrders.InfoPanelHighlight;
        }
        private const string CloseFillName = "CloseButtonFill";
        // ---- フェーズ演出中の退避 ----
        //
        // 「決定！」などの帯（PhaseTransitionCanvas）は sortingOrder 19 なのに、
        // 能力パネルは 20、説明ツールチップは 25 で、**構造上かならず帯の上に出る**。
        // 演出のあいだだけ 18 へ落とし、明けたら元へ戻す。
        // 演出中に ShowTooltip が走っても 20/25 へ戻らないよう、
        // sortingOrder を書く箇所はすべてこのフラグを見ている。
        private bool _transitionSuppressed;

        /// <summary>フェーズ演出中だけ帯より下へ退避する。GameUIManager.SetIsTransitioning から呼ぶ。</summary>
        public void SetSuppressedForTransition(bool suppressed)
        {
            if (_transitionSuppressed == suppressed) return;
            _transitionSuppressed = suppressed;

            Canvas rootCanvas = this.GetComponent<Canvas>();
            if (rootCanvas != null)
            {
                rootCanvas.sortingOrder = suppressed
                    ? UISortingOrders.AbilityDuringTransition
                    : (isWindowVisible ? UISortingOrders.InfoPanelHighlight : UISortingOrders.InfoPanelNormal);
            }

            // 旧ツールチップ用の Canvas 退避はここにあったが、2026-08-24 に削除。
            // 説明欄はパネルの中に入ったので、ルートの sortingOrder だけで前後が決まる。
        }

        /// <summary>
        /// 説明欄を案内文に戻す。**欄そのものは消さない。**
        /// カーソルを外すたびに欄が消えると、一覧の下でパネルがちらつく。
        ///
        /// **選んでいる行があるときは、その説明に戻す。**
        /// ホバーより選択を優先しないと、選んだ能力の説明がカーソルを外した
        /// 瞬間に読めなくなる。
        /// </summary>
        public void HideTooltip()
        {
            HideLegacyTooltip();

            var text = EnsureDescriptionBox();
            if (text != null)
            {
                text.text = currentSelection != null && !string.IsNullOrEmpty(currentSelection.Description)
                    ? currentSelection.Description
                    : DescPlaceholder;
            }

            Canvas rootCanvas = this.GetComponent<Canvas>();
            if (rootCanvas != null)
            {
                rootCanvas.sortingOrder = _transitionSuppressed ? UISortingOrders.AbilityDuringTransition : UISortingOrders.InfoPanelNormal;
            }
        }
    }
}
