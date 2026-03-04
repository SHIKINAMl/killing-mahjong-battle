using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

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

        private void Start()
        {
            panelRect = GetComponent<RectTransform>();
            decideButton.onClick.AddListener(OnDecideClicked);
            autoManganButton.onClick.AddListener(OnAutoManganClicked);
            UpdateCursorPosition();
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
                Vector2 localPointerPosition;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    panelRect.parent as RectTransform, eventData.position, eventData.pressEventCamera, out localPointerPosition))
                {
                    panelRect.localPosition = localPointerPosition - dragOffset;
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

            if (gameUIManager.CurrentPhaseStatus == "discard")
            {
                gameUIManager.DiscardSelectedTile();
            }
            else if (gameUIManager.CurrentPhaseStatus == "hand_selection")
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
            Debug.Log("Auto Mangan Clicked");
            // Notify Game logic to auto-complete hand
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
    }
}
