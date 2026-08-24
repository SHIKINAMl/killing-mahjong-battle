using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class AbilityUI : MonoBehaviour
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
        private void EnsureAbilities()
        {
            if (realAbilities != null) return;

            // スキルの種類（skillType）はサーバーの SkillType enum と対になっている。
            // 表示名は SkillNames に一本化してあるので、ここでは文字列を書かない。
            realAbilities = new System.Collections.Generic.List<AbilityData>
            {
                new AbilityData(SkillNames.Mulligan, SkillNames.GetDisplayName(SkillNames.Mulligan),
                    "手牌か山牌から不要な牌を選び、山札と交換する。"),
                new AbilityData(SkillNames.Perspective, SkillNames.GetDisplayName(SkillNames.Perspective),
                    "相手の手牌をランダムに3枚公開する。"),
                new AbilityData(SkillNames.BoostHand, SkillNames.GetDisplayName(SkillNames.BoostHand),
                    "指定した役の翻数を+1する。"),
                new AbilityData(SkillNames.Assault, SkillNames.GetDisplayName(SkillNames.Assault),
                    "この局は上がっても点を得ない。代わりに、得るはずだった額を相手への追加ダメージにする。1局1回。")

                // **特殊勝利は載せない（2026-08-14 に廃止の指示）。**
                // サーバー側の enum・HP_COST_TABLE・special_victory_won の処理は残っているので、
                // ここから外すだけでプレイヤーは選べなくなる。
                // `special_victory_count` はコスト表のどの段を使うかの添字として今も生きているため、
                // BoardStateManager / GameRules 側は触っていない。
                // 完全に消すなら Python 側の対応が要る（担当外）。
            };
        }

        /// <summary>現在の所持HP（＝スキルの支払い原資）。BoardStateManager が無い場合は 0 扱い。</summary>
        private int CurrentLocalHp =>
            KillingMahjong.Managers.BoardStateManager.Instance != null
                ? KillingMahjong.Managers.BoardStateManager.Instance.LocalPlayerHp
                : 0;

        private int CurrentSpecialVictoryCount =>
            KillingMahjong.Managers.BoardStateManager.Instance != null
                ? KillingMahjong.Managers.BoardStateManager.Instance.LocalPlayerSpecialVictoryCount
                : 0;

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

        private void PopulateList()
        {
            EnsureAbilities();
            if (itemPrefab == null || contentContainer == null) return;

            // clear existing
            foreach(Transform child in contentContainer) Destroy(child.gameObject);
            instantiatedItems.Clear();

            // **選択は行と一緒に消える。** 行を作り直しているので、
            // 参照を残すと破棄済みのオブジェクトを掴んだままになる
            currentSelection = null;

            LayoutContentContainer();

            int svCount = CurrentSpecialVictoryCount;
            int currentHp = CurrentLocalHp;

            float currentY = 0f;
            for (int i = 0; i < realAbilities.Count; i++)
            {
                var data = realAbilities[i];
                int currentCost = GameRules.GetSkillCost(data.skillType, svCount);
                bool affordable = currentHp >= currentCost;

                var itemObj = Instantiate(itemPrefab, contentContainer);
                itemObj.Setup(this, i, data.name, currentCost, data.description, affordable);

                // 行の寸法と位置はここで決め切る。
                //
                // **プレハブの値は当てにしない。** 行の器は 138x40 なのに中の板は
                // 120x45 で、縦が 2.5 ずつはみ出して `RectMask2D` に切られていた。
                // 器そのものを板として使い、子は器いっぱいに張る（AbilityItemUI.BuildTile）。
                RectTransform rt = itemObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // 行からはみ出した子が隣の行のクリック判定を奪うのを防ぐ
                    if (itemObj.GetComponent<RectMask2D>() == null)
                    {
                        itemObj.gameObject.AddComponent<RectMask2D>();
                    }

                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;

                    // 上端そろえ。pivot も固定する（プレハブ任せにすると
                    // 位置の式が pivot に依存して読めなくなる）
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(RowWidth, RowHeight);
                    rt.anchoredPosition3D = new Vector3(0f, -currentY, 0f);
                }

                currentY += RowHeight + RowSpacing;
                instantiatedItems.Add(itemObj);
            }
        }

        /// <summary>
        /// 一覧の器を、巻物の紙の面（内枠）の上側へ合わせる。
        ///
        /// シーンの値は 138x186 @(6.5,112) で、幅が内枠より 22 狭く、
        /// 上端が内枠より 4 だけ外に出ていた。両シーンあるのでコードから当てる。
        /// </summary>
        private void LayoutContentContainer()
        {
            var rect = contentContainer as RectTransform;
            if (rect == null) return;

            float listHeight = PanelInnerHeight - DescBoxHeight - ListTopMargin;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(PanelInnerWidth, listHeight);
            rect.anchoredPosition = new Vector2(
                PanelInnerCenter.x,
                PanelInnerCenter.y + PanelInnerHeight * 0.5f - ListTopMargin);
            rect.localScale = Vector3.one;
        }

        private void OnTriggerClicked()
        {
            ToggleAbilityWindow();
            CancelOpponentReady();
            
            if (buttonPressCoroutine != null) StopCoroutine(buttonPressCoroutine);
            buttonPressCoroutine = StartCoroutine(ButtonPressRoutine());
        }

        private System.Collections.IEnumerator ButtonPressRoutine()
        {
            if (triggerButtonImage != null && pressedSprite != null)
            {
                triggerButtonImage.sprite = pressedSprite;
            }
            
            yield return new WaitForSeconds(buttonPressDuration);
            
            if (triggerButtonImage != null && normalSprite != null)
            {
                triggerButtonImage.sprite = normalSprite;
            }
        }

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

        private void OnActivateClicked()
        {
            if (IsDisplayOnly) return;

            if (currentSelection != null)
            {
                int index = currentSelection.AbilityIndex;
                if (index >= 0 && index < realAbilities.Count)
                {
                    var data = realAbilities[index];
                    Debug.Log($"Activting Ability: {data.name} " +
                              $"(Cost: {GameRules.GetSkillCost(data.skillType, CurrentSpecialVictoryCount)}, " +
                              $"Type: {data.skillType})");
                    
                    var uiMgr = FindFirstObjectByType<GameUIManager>();
                    if (uiMgr != null)
                    {
                        // チュートリアルはサーバーに接続しないため、発動要求を送っても無反応になる。
                        // 制約「チュートリアル中はプレイヤーの能力使用は不可」に合わせて明示的に弾く。
                        if (uiMgr.IsTutorialMode)
                        {
                            if (uiMgr.DialogueUI != null)
                                uiMgr.DialogueUI.ShowText("「今は見てるだけでいいわ。能力の使い方は後で教えてあげる」");
                            DeselectAll();
                            ToggleAbilityWindow(false);
                            return;
                        }

                        if (uiMgr.CurrentPhaseStatus != KillingMahjong.EngineData.RoundStatus.HandSelection)
                        {
                            if (uiMgr.DialogueUI != null) uiMgr.DialogueUI.ShowText("「今はスキルを使えないわ！」");
                            DeselectAll();
                            ToggleAbilityWindow(false);
                            return;
                        }

                        // HP不足のスキルは押せてしまうと無反応で終わるため、理由を示して弾く
                        if (!currentSelection.IsAffordable)
                        {
                            int requiredCost = GameRules.GetSkillCost(data.skillType, CurrentSpecialVictoryCount);
                            if (uiMgr.DialogueUI != null)
                            {
                                uiMgr.DialogueUI.ShowText(
                                    $"「{data.name}には{requiredCost}必要よ。今のあなたには{CurrentLocalHp}しかないわ」");
                            }
                            DeselectAll();
                            ToggleAbilityWindow(false);
                            return;
                        }

                        if (data.skillType == "mulligan")
                        {
                            uiMgr.StartMulliganSelection();
                        }
                        else if (data.skillType == "boost_hand")
                        {
                            uiMgr.StartBoostHandSelection();
                        }
                        else
                        {
                            // サーバーへスキル発動リクエストを直接送信
                            uiMgr.SendActionToServer("skill", new KillingMahjong.Network.ActionPayload { skill_type = data.skillType });
                        }
                    }
                }
                DeselectAll();
                ToggleAbilityWindow(false); // 発動後にウィンドウを閉じる (キャンセルはしない)
            }
            else
            {
                Debug.Log("No ability selected.");
            }
        }

        private System.Collections.IEnumerator AnimateWindow(Vector2 targetPos, bool isOpening)
        {
            if (abilityWindow == null) yield break;

            // 巻物のコマが入っていればコマ送り、無ければ従来のスライド
            if (HasFrames)
            {
                yield return FrameAnimation(isOpening);
                yield break;
            }

            if (isOpening) abilityWindow.gameObject.SetActive(true);

            Vector2 startPos = abilityWindow.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < animationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / animationDuration);
                t = t * t * (3f - 2f * t);

                abilityWindow.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            abilityWindow.anchoredPosition = targetPos;

            if (!isOpening) abilityWindow.gameObject.SetActive(false);
        }

        private bool HasFrames => openFrames != null && openFrames.Length > 0;

        /// <summary>
        /// 巻物が開く／巻き取られるコマ送り。
        ///
        /// **スライドはしない。** 巻物はその場で開く絵なので、位置まで動かすと
        /// 「飛んできながら開く」ことになって二重の動きになる。
        /// 開いた位置（showPosition）に置いたままコマだけ送る。
        ///
        /// **中身は開き切ってから出す。** 小さく丸まったコマの上に能力の行が
        /// 浮いていると、何が起きているのか読めない。
        /// 閉じるときは逆で、先に中身を消してから巻き取る。
        /// </summary>
        private System.Collections.IEnumerator FrameAnimation(bool isOpening)
        {
            var img = ResolveFrameImage();

            if (isOpening)
            {
                abilityWindow.anchoredPosition = showPosition;
                abilityWindow.gameObject.SetActive(true);
            }
            SetContentsVisible(false);

            int last = openFrames.Length - 1;
            float wait = Mathf.Max(0f, frameSeconds);

            for (int step = 0; step <= last; step++)
            {
                // 開くときは 0→last、閉じるときは last→0
                int i = isOpening ? step : last - step;
                if (img != null && openFrames[i] != null) img.sprite = openFrames[i];

                // **最後のコマだけは待たない。** 開いた瞬間に操作できてほしいし、
                // 閉じるときも最後のコマを見せてから消す必要がない
                if (step < last) yield return new WaitForSeconds(wait);
            }

            if (isOpening)
            {
                SetContentsVisible(true);
            }
            else
            {
                abilityWindow.gameObject.SetActive(false);
                abilityWindow.anchoredPosition = hiddenPosition;

                // 次に開くときは最初のコマから始める
                if (img != null && openFrames[0] != null) img.sprite = openFrames[0];
            }
        }

        /// <summary>コマを描く Image。未設定なら abilityWindow の Image を使う。</summary>
        private Image ResolveFrameImage()
        {
            if (windowFrameImage != null) return windowFrameImage;
            if (abilityWindow == null) return null;

            windowFrameImage = abilityWindow.GetComponent<Image>();
            return windowFrameImage;
        }

        private void SetContentsVisible(bool visible)
        {
            // 説明欄は一覧と一緒に出し入れする
            var box = abilityWindow != null ? abilityWindow.Find(DescBoxName) : null;
            if (box != null && box.gameObject.activeSelf != visible) box.gameObject.SetActive(visible);

            if (contentsShownWhenOpen == null) return;
            for (int i = 0; i < contentsShownWhenOpen.Length; i++)
            {
                var go = contentsShownWhenOpen[i];
                if (go == null) continue;

                // **旧ツールチップはここで出さない。**
                // シーンの `contentsShownWhenOpen` に `TooltipPanel` が入っているため、
                // 巻物が開き切るたびにダミー文字列の箱が盤面の上に出ていた（2026-08-24 に判明）。
                if (tooltipPanel != null && go == tooltipPanel) continue;

                if (go.activeSelf != visible) go.SetActive(visible);
            }
        }

        private void CancelOpponentReady()
        {
            Debug.Log("Ability Triggered: Opponent's Ready State Cancelled!");
        }

        // ==================== 説明欄 ====================
        //
        // **浮いていたツールチップは使わない。**
        // シーンの `TooltipPanel` は 150x100 をメニューの右 176 に置いた固定の箱で、
        // 盤面の上に単独で浮いていた。しかも `contentsShownWhenOpen` に入っているので
        // **開くたびに勝手に出て**、中身は編集時のダミー文字列 "aaaa…" のままだった。
        //
        // 代わりに、巻物の紙の面の下側に説明欄を作って全文をそこへ出す。
        // 行の中を流れていたマーキーも、この欄があるので要らなくなった。

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
        private TMPro.TextMeshProUGUI EnsureDescriptionBox()
        {
            if (_descText != null) return _descText;
            if (abilityWindow == null) return null;

            var boxTr = abilityWindow.Find(DescBoxName) as RectTransform;
            if (boxTr == null)
            {
                var go = new GameObject(DescBoxName, typeof(RectTransform), typeof(Image));
                boxTr = (RectTransform)go.transform;
                boxTr.SetParent(abilityWindow, false);
            }

            // 縁は箱そのものの地の色。内側に面を1枚敷く（行のタイルと同じ作り）
            var boxImg = boxTr.GetComponent<Image>();
            boxImg.sprite = null;
            boxImg.color = DescBorderColor;
            boxImg.raycastTarget = false;

            var fillTr = boxTr.Find(DescFillName) as RectTransform;
            if (fillTr == null)
            {
                var fillGo = new GameObject(DescFillName, typeof(RectTransform), typeof(Image));
                fillTr = (RectTransform)fillGo.transform;
                fillTr.SetParent(boxTr, false);
            }
            var fillImg = fillTr.GetComponent<Image>();
            fillImg.sprite = null;
            fillImg.color = DescBoxColor;
            fillImg.raycastTarget = false;
            fillTr.anchorMin = Vector2.zero;
            fillTr.anchorMax = Vector2.one;
            fillTr.offsetMin = new Vector2(DescBorder, DescBorder);
            fillTr.offsetMax = new Vector2(-DescBorder, -DescBorder);
            fillTr.localScale = Vector3.one;
            fillTr.SetAsFirstSibling();

            boxTr.anchorMin = new Vector2(0.5f, 0.5f);
            boxTr.anchorMax = new Vector2(0.5f, 0.5f);
            boxTr.pivot = new Vector2(0.5f, 0f);
            boxTr.sizeDelta = new Vector2(RowWidth, DescBoxHeight);
            boxTr.anchoredPosition = new Vector2(
                PanelInnerCenter.x,
                PanelInnerCenter.y - PanelInnerHeight * 0.5f + ListTopMargin);
            boxTr.localScale = Vector3.one;

            var textTr = boxTr.Find(DescTextName) as RectTransform;
            TMPro.TextMeshProUGUI text;
            if (textTr == null)
            {
                var go = new GameObject(DescTextName, typeof(RectTransform));
                textTr = (RectTransform)go.transform;
                textTr.SetParent(boxTr, false);
                text = go.AddComponent<TMPro.TextMeshProUGUI>();
                if (tooltipText != null && tooltipText.font != null) text.font = tooltipText.font;
            }
            else
            {
                text = textTr.GetComponent<TMPro.TextMeshProUGUI>();
            }

            textTr.anchorMin = Vector2.zero;
            textTr.anchorMax = Vector2.one;
            textTr.offsetMin = new Vector2(4f, 3f);
            textTr.offsetMax = new Vector2(-4f, -3f);
            textTr.localScale = Vector3.one;

            // **折り返して全文を出す。** 46文字の「強襲」が最長で、
            // 128 幅・44 高に 9px で 4 行なら収まる。収まらない分は自動で縮む。
            //
            // **`Overflow` にしないこと。** 自動縮小の下限(7px)でも入らない文言を
            // 足したとき、はみ出した行が巻物の枠を突き抜けて盤面に出てしまう。
            // `Truncate` なら最悪でも欄の中で切れて止まる。
            text.margin = Vector4.zero;
            text.color = Color.white;
            text.alignment = TMPro.TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TMPro.TextWrappingModes.Normal;
            text.overflowMode = TMPro.TextOverflowModes.Truncate;
            text.enableAutoSizing = true;
            text.fontSizeMax = 10f;
            text.fontSizeMin = 7f;
            text.raycastTarget = false;

            _descText = text;
            return _descText;
        }

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

        /// <summary>
        /// シーンに残っている旧ツールチップを畳む。**出番はもう無い。**
        ///
        /// 中身も空にしておく。シーンには編集時のダミー文字列（`aaaa…`）が
        /// 入ったままで、**別の経路がうっかりこのパネルを出すと同じ事故が再発する**。
        /// </summary>
        private void HideLegacyTooltip()
        {
            if (tooltipText != null && !string.IsNullOrEmpty(tooltipText.text)) tooltipText.text = "";
            if (tooltipPanel != null && tooltipPanel.activeSelf) tooltipPanel.SetActive(false);
        }

        private const string CloseFillName = "CloseButtonFill";

        /// <summary>
        /// 閉じるボタンを行のタイルと同じ2層（縁＋面）に揃える。
        ///
        /// 単色のベタ赤 22x22 のままだと、右上のHPの赤いバッジと**同じ形・同じ意味**に見える。
        /// 内側に紺の面を覗かせて「メニューの部品」であることを出す。
        /// </summary>
        private void StyleCloseButton()
        {
            if (closeButton == null) return;
            var img = closeButton.GetComponent<Image>();
            if (img == null) return;

            img.sprite = null;
            img.color = CloseBorderColor;

            var rt = img.rectTransform;
            var fillTr = rt.Find(CloseFillName) as RectTransform;
            if (fillTr == null)
            {
                var go = new GameObject(CloseFillName, typeof(RectTransform), typeof(Image));
                fillTr = (RectTransform)go.transform;
                fillTr.SetParent(rt, false);
            }
            var fill = fillTr.GetComponent<Image>();
            fill.sprite = null;
            fill.color = DescBoxColor;
            fill.raycastTarget = false;
            fillTr.anchorMin = Vector2.zero;
            fillTr.anchorMax = Vector2.one;
            fillTr.offsetMin = new Vector2(DescBorder, DescBorder);
            fillTr.offsetMax = new Vector2(-DescBorder, -DescBorder);
            fillTr.localScale = Vector3.one;
            fillTr.SetAsFirstSibling();
        }

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
