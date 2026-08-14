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
        
        [Header("Layout Settings")]
        [SerializeField] private float itemOffsetX = 0f; 
        [SerializeField] private float itemOffsetY = 0f; 
        [SerializeField] private float itemHeight = 70f; // デフォルト100から縮小
        [SerializeField] private float itemSpacing = 5f;

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

            if (activateButton != null)
                activateButton.onClick.AddListener(OnActivateClicked);

            if (abilityWindow != null)
                abilityWindow.anchoredPosition = hiddenPosition;

            animationDuration = 0.2f; // 強制的に0.2秒にする

            EnsureAbilities();

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

        private void PopulateList()
        {
            EnsureAbilities();
            if (itemPrefab == null || contentContainer == null) return;

            // clear existing
            foreach(Transform child in contentContainer) Destroy(child.gameObject);
            instantiatedItems.Clear();

            int svCount = CurrentSpecialVictoryCount;
            int currentHp = CurrentLocalHp;

            float currentY = itemOffsetY;
            for (int i = 0; i < realAbilities.Count; i++)
            {
                var data = realAbilities[i];
                int currentCost = GameRules.GetSkillCost(data.skillType, svCount);
                bool affordable = currentHp >= currentCost;

                var itemObj = Instantiate(itemPrefab, contentContainer);
                itemObj.Setup(this, i, data.name, currentCost, data.description, affordable);

                // Manual Layout
                RectTransform rt = itemObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // はみ出した子要素（巨大な背景やテキスト枠など）が他のボタンのクリック判定を奪うのを防ぐため、
                    // RectMask2Dを追加して、指定サイズ（itemHeight）外の描画とクリック判定を完全にカットします。
                    if (itemObj.GetComponent<RectMask2D>() == null)
                    {
                        itemObj.gameObject.AddComponent<RectMask2D>();
                    }

                    // Force Top-Stretch Layout horizontally
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;

                    // Set Anchors to Top-Stretch
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(1, 1);
                    
                    // 以前は pivot を (0.5, 1) に強制変更していましたが、
                    // これによりPrefab内の子要素（背景など）がズレてはみ出し、クリック判定が重なる原因になっていました。
                    // pivot はPrefabの設定をそのまま維持し、位置計算で補正します。

                    // Set SizeDelta (X=0 means stretch to fill width, Y=Height)
                    rt.sizeDelta = new Vector2(0, itemHeight);

                    // Set Position based on the existing pivot
                    float posY = -currentY - (1f - rt.pivot.y) * itemHeight;
                    rt.anchoredPosition3D = new Vector3(itemOffsetX, posY, 0);

                    currentY += itemHeight + itemSpacing;
                }
                else
                {
                    currentY += itemHeight + itemSpacing;
                }

                instantiatedItems.Add(itemObj);
            }

            // Resize Container
            RectTransform contentRect = contentContainer.GetComponent<RectTransform>();
            if (contentRect != null)
            {
                 contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, currentY);
            }
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

                // ウィンドウが開いた瞬間に説明文のスクロール位置を中央にリセットする
                foreach (var item in instantiatedItems)
                {
                    item.ResetScrollPosition();
                }
            }
            else
            {
                DeselectAll(); // Deselect on close
                var uiMgr = FindFirstObjectByType<GameUIManager>();
                if (uiMgr != null && cancelSkill) uiMgr.CancelSkillSelection();

                Canvas rootCanvas = this.GetComponent<Canvas>();
                if (rootCanvas != null)
                {
                    rootCanvas.sortingOrder = UISortingOrders.InfoPanelNormal;
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
            }
        }

        public void DeselectAll()
        {
            if (currentSelection != null)
            {
                currentSelection.Deselect();
                currentSelection = null;
            }
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
            if (contentsShownWhenOpen == null) return;
            for (int i = 0; i < contentsShownWhenOpen.Length; i++)
            {
                if (contentsShownWhenOpen[i] != null &&
                    contentsShownWhenOpen[i].activeSelf != visible)
                {
                    contentsShownWhenOpen[i].SetActive(visible);
                }
            }
        }

        private void CancelOpponentReady()
        {
            Debug.Log("Ability Triggered: Opponent's Ready State Cancelled!");
        }

        public void ShowTooltip(string description)
        {
            if (tooltipPanel != null && tooltipText != null)
            {
                tooltipText.text = description;
                
                // tooltipPanel自身にCanvasが無ければ追加
                Canvas tooltipCanvas = tooltipPanel.GetComponent<Canvas>();
                if (tooltipCanvas == null)
                {
                    tooltipCanvas = tooltipPanel.AddComponent<Canvas>();
                    tooltipPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
                tooltipCanvas.overrideSorting = true;
                tooltipCanvas.sortingOrder = UISortingOrders.AbilityTooltip;

                // AbilityUI全体を最前面化するが、中身の表示順が壊れないようにルート(this)のみ設定する
                Canvas rootCanvas = this.GetComponent<Canvas>();
                if (rootCanvas == null)
                {
                    rootCanvas = this.gameObject.AddComponent<Canvas>();
                    this.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
                rootCanvas.overrideSorting = true;
                rootCanvas.sortingOrder = UISortingOrders.InfoPanelHighlight;
                
                // Z座標は0
                Vector3 localPos = tooltipPanel.transform.localPosition;
                tooltipPanel.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);

                tooltipPanel.SetActive(true);
            }
        }

        public void HideTooltip()
        {
            if (tooltipPanel != null)
            {
                tooltipPanel.SetActive(false);
            }
            Canvas rootCanvas = this.GetComponent<Canvas>();
            if (rootCanvas != null)
            {
                rootCanvas.sortingOrder = UISortingOrders.InfoPanelNormal;
            }
        }
    }
}
