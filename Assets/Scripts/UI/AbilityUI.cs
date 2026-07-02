using UnityEngine;
using UnityEngine.UI;

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

        // Internal State
        private bool isWindowVisible = false;
        private Coroutine currentAnimationCoroutine;
        private Coroutine buttonPressCoroutine;
        private System.Collections.Generic.List<AbilityItemUI> instantiatedItems = new System.Collections.Generic.List<AbilityItemUI>();
        private AbilityItemUI currentSelection;

        // Real Data Class matching Python
        [System.Serializable]
        public class AbilityData
        {
            public string skillType;
            public string name;
            public int cost;
            public string description;
            
            public AbilityData(string type, string n, int c, string d)
            {
                skillType = type;
                name = n;
                cost = c;
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

            // Pythonの設定に合わせたアビリティ一覧
            realAbilities = new System.Collections.Generic.List<AbilityData>
            {
                new AbilityData("mulligan", "牌交換", 1200, "手牌か山牌から不要な牌を選び、山札と交換する。"),
                new AbilityData("perspective", "透視", 1500, "相手の手牌をランダムに3枚公開する。"),
                new AbilityData("boost_hand", "役強化", 10000, "指定した役の翻数を+1する。"),
                new AbilityData("special_victory", "特殊勝利", 30000, "3回発動すると無条件で勝利する。")
            };

            // Populate List
            PopulateList();
            
            // 起動時にツールチップを非表示にする
            HideTooltip();
        }

        private void PopulateList()
        {
            if (itemPrefab == null || contentContainer == null) return;

            // clear existing
            foreach(Transform child in contentContainer) Destroy(child.gameObject);
            instantiatedItems.Clear();

            int svCount = 0;
            if (KillingMahjong.Managers.BoardStateManager.Instance != null)
            {
                svCount = KillingMahjong.Managers.BoardStateManager.Instance.LocalPlayerSpecialVictoryCount;
            }

            float currentY = itemOffsetY;
            for (int i = 0; i < realAbilities.Count; i++)
            {
                var data = realAbilities[i];
                int currentCost = GameRules.GetSkillCost(data.skillType, svCount);
                
                var itemObj = Instantiate(itemPrefab, contentContainer);
                itemObj.Setup(this, i, data.name, currentCost, data.description);
                
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
            }

            if (currentAnimationCoroutine != null) StopCoroutine(currentAnimationCoroutine);
            currentAnimationCoroutine = StartCoroutine(AnimateWindow(isWindowVisible ? showPosition : hiddenPosition, isWindowVisible));
        }

        public void OnAbilitySelected(AbilityItemUI item)
        {
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
            if (currentSelection != null)
            {
                int index = currentSelection.AbilityIndex;
                if (index >= 0 && index < realAbilities.Count)
                {
                    var data = realAbilities[index];
                    Debug.Log($"Activting Ability: {data.name} (Cost: {data.cost}, Type: {data.skillType})");
                    
                    var uiMgr = FindFirstObjectByType<GameUIManager>();
                    if (uiMgr != null)
                    {
                        if (uiMgr.CurrentPhaseStatus != KillingMahjong.EngineData.RoundStatus.HandSelection)
                        {
                            if (uiMgr.DialogueUI != null) uiMgr.DialogueUI.ShowText("「今はスキルを使えないわ！」");
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
                tooltipCanvas.sortingLayerName = "UI";
                tooltipCanvas.sortingOrder = 25; // ツールチップをAbilityUI本体より少し手前に

                // AbilityUI全体を最前面化するが、中身の表示順が壊れないようにルート(this)のみ設定する
                Canvas rootCanvas = this.GetComponent<Canvas>();
                if (rootCanvas == null)
                {
                    rootCanvas = this.gameObject.AddComponent<Canvas>();
                    this.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                }
                rootCanvas.overrideSorting = true;
                rootCanvas.sortingLayerName = "UI";
                rootCanvas.sortingOrder = 20; // ユーザー指定の20
                
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
        }
    }
}
