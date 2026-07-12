using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public class HandUI : HandBaseUI, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private RectTransform handAreaRect; // For drag detection

        // --- Dragging the Hand Panel ---
        private RectTransform panelRect;
        private Vector2 dragOffset;

        [Header("Cursor")]
        [SerializeField] private Transform cursor; // Changed from RectTransform to Transform
        
        [Header("Buttons")]
        [SerializeField] private Button decideButton;
        [SerializeField] private Button autoManganButton;

        private int currentSelectionIndex = 0;

        private Button reselectButton;
        private Button autoDiscardButton;
        private bool isAutoDiscardEnabled = false;

        public bool IsAutoDiscardEnabled
        {
            get => isAutoDiscardEnabled;
            set
            {
                isAutoDiscardEnabled = value;
                UpdateAutoDiscardButtonText();
            }
        }

        private void Start()
        {
            panelRect = GetComponent<RectTransform>();
            decideButton.onClick.AddListener(OnDecideClicked);
            autoManganButton.onClick.AddListener(OnAutoManganClicked);
            UpdateCursorPosition();

            if (decideButton != null)
            {
                reselectButton = Instantiate(decideButton, decideButton.transform.parent);
                reselectButton.name = "ReselectButton";
                reselectButton.onClick.RemoveAllListeners();
                reselectButton.onClick.AddListener(OnReselectClicked);
                
                var tmp = reselectButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.text = "選び直す";
                var txt = reselectButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null) txt.text = "選び直す";

                reselectButton.gameObject.SetActive(false);

                autoDiscardButton = Instantiate(decideButton, decideButton.transform.parent);
                autoDiscardButton.name = "AutoDiscardButton";
                autoDiscardButton.onClick.RemoveAllListeners();
                autoDiscardButton.onClick.AddListener(OnAutoDiscardClicked);
                
                RectTransform rt = autoDiscardButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, rt.anchoredPosition.y + 80);
                }
                UpdateAutoDiscardButtonText();
                autoDiscardButton.gameObject.SetActive(false);
            }
        }

        private void OnAutoDiscardClicked()
        {
            IsAutoDiscardEnabled = !IsAutoDiscardEnabled;
            if (IsAutoDiscardEnabled && gameUIManager != null && gameUIManager.CurrentPhaseStatus == RoundStatus.Discard)
            {
                var autoDiscard = gameUIManager.GetComponent<AutoDiscardController>();
                if (autoDiscard == null)
                {
                    autoDiscard = gameUIManager.gameObject.AddComponent<AutoDiscardController>();
                }
                autoDiscard.CheckAndExecuteAutoDiscard();
            }
        }

        private void UpdateAutoDiscardButtonText()
        {
            if (autoDiscardButton != null)
            {
                string t = IsAutoDiscardEnabled ? "自動: ON" : "自動: OFF";
                var tmp = autoDiscardButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.text = t;
                var txt = autoDiscardButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null) txt.text = t;

                var img = autoDiscardButton.GetComponent<Image>();
                if (img != null) img.color = IsAutoDiscardEnabled ? Color.green : Color.red;
            }
        }

        private void OnReselectClicked()
        {
            if (gameUIManager != null && gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                gameUIManager.CancelHandSelection();
            }
        }

        // --- Drag Panel Implementation ---
        public void OnBeginDrag(PointerEventData eventData)
        {
            // パネル移動用 (タイル自体のドラッグの妨げにならないよう必要に応じて背景などをターゲットにします)
            if (panelRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    panelRect, eventData.position, eventData.pressEventCamera, out dragOffset);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (panelRect != null)
            {
                RectTransform parentRect = panelRect.parent as RectTransform;
                if (parentRect != null)
                {
                    Vector2 localPointerPosition;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect, eventData.position, eventData.pressEventCamera, out localPointerPosition))
                    {
                        panelRect.localPosition = localPointerPosition - dragOffset;
                    }
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // 必要に応じてスナップ処理等
        }

        private int GetCategoryPriority(List<TileData> list)
        {
            if (list.Count == 0) return 99;
            var cat = list[0].Category;
            switch (cat)
            {
                case TileCategory.Souzu: return 1;
                case TileCategory.Manzu: return 2;
                case TileCategory.Pinzu: return 3;
                case TileCategory.Honor: return 4;
                default: return 99;
            }
        }

        public void MoveCursor(int direction)
        {
            currentSelectionIndex += direction;
            if (currentSelectionIndex < 0) currentSelectionIndex = 0;
            if (currentSelectionIndex >= handSlots.Count) currentSelectionIndex = handSlots.Count - 1;
            
            UpdateCursorPosition();
        }

        private void UpdateCursorPosition()
        {
            if (handSlots.Count > 0 && currentSelectionIndex < handSlots.Count)
            {
                // Uses World Position now
                if (handSlots[currentSelectionIndex] != null)
                    cursor.position = handSlots[currentSelectionIndex].position;
            }
        }

        private void OnDecideClicked()
        {
            if (gameUIManager == null) return;
            if (isSubmitted || gameUIManager.IsTransitioning) return;

            if (gameUIManager.CurrentPhaseStatus == RoundStatus.Discard)
            {
                gameUIManager.DiscardSelectedTile();
            }
            else if (gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                Debug.Log($"Decide Clicked. Current Hand Count: {handSlots.Count}");
                if (handSlots.Count == 13)
                {
                    gameUIManager.CompleteHandSelection();
                }
                else
                {
                    Debug.LogWarning("Hand must have exactly 13 tiles to proceed!");
                }
            }
        }

        private void OnAutoManganClicked()
        {
            Debug.Log("Auto Mangan Hand Clicked");
            if (gameUIManager == null) return;
            if (isSubmitted || gameUIManager.IsTransitioning) return;

            if (gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                gameUIManager.SelectManganHand();
            }
        }
        public bool IsPointInHandArea(Vector2 screenPoint)
        {
            if (handAreaRect == null) 
            {
                // Fallback to container if not assigned
                 var rt = handSlotContainer as RectTransform;
                 if (rt != null) return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint);
                 return false;
            }
            return RectTransformUtility.RectangleContainsScreenPoint(handAreaRect, screenPoint);
        }
        private bool isSubmitted = false;
        public bool IsSubmitted => isSubmitted;

        public void SetSubmittedState(bool submitted)
        {
            isSubmitted = submitted;
            if (gameUIManager != null) UpdateLayout(gameUIManager.CurrentPhaseStatus);
        }

        public override void SortHandSlots()
        {
            handSlots.Sort((a, b) =>
            {
                var ia = a.GetComponent<TileInteraction>();
                var ib = b.GetComponent<TileInteraction>();
                int idA = (ia != null) ? ia.TileId : 0;
                int idB = (ib != null) ? ib.TileId : 0;

                int baseA = idA & 0x1F;
                int baseB = idB & 0x1F;
                if (baseA != baseB) return baseA.CompareTo(baseB);
                return idA.CompareTo(idB);
            });

            for (int i = 0; i < handSlots.Count; i++)
            {
                handSlots[i].SetSiblingIndex(i);
            }

            if (gameUIManager != null) UpdateLayout(gameUIManager.CurrentPhaseStatus);
        }

        public override void UpdateLayout(RoundStatus phaseStatus)
        {
            base.UpdateLayout(phaseStatus);

            bool showButtons = (phaseStatus == RoundStatus.HandSelection) && !isSubmitted && (gameUIManager == null || (!gameUIManager.IsMulliganSelection && !gameUIManager.IsOpponentSkillProcessing));

            if (decideButton != null)
            {
                decideButton.gameObject.SetActive(showButtons);
            }
            if (autoManganButton != null)
            {
                autoManganButton.gameObject.SetActive(showButtons);
            }
            if (reselectButton != null)
            {
                bool canReselect = (phaseStatus == RoundStatus.HandSelection) && isSubmitted && (gameUIManager != null && !gameUIManager.IsTransitioning && !gameUIManager.IsMulliganSelection);
                reselectButton.gameObject.SetActive(canReselect);
            }
            if (autoDiscardButton != null)
            {
                autoDiscardButton.gameObject.SetActive(phaseStatus == RoundStatus.Discard);
            }
        }
    }
}
