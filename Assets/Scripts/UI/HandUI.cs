using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class HandUI : MonoBehaviour
    {
        [Header("Hand Slots")]
        [SerializeField] private Transform handSlotContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private List<Transform> handSlots; // Changed from Image to Transform

        [Header("Cursor")]
        [SerializeField] private Transform cursor; // Changed from RectTransform to Transform
        
        [Header("Buttons")]
        [SerializeField] private Button decideButton;
        [SerializeField] private Button autoManganButton;

        private int currentSelectionIndex = 0;

        private void Start()
        {
            decideButton.onClick.AddListener(OnDecideClicked);
            autoManganButton.onClick.AddListener(OnAutoManganClicked);
            UpdateCursorPosition();
        }

        public void SetHand(List<int> tileIds)
        {
            // Logic to populate hand slots
            Debug.Log($"Hand set with {tileIds.Count} tiles.");
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
            Debug.Log($"Selected index: {currentSelectionIndex}");
            // Notify Game logic
        }

        private void OnAutoManganClicked()
        {
            Debug.Log("Auto Mangan Clicked");
            // Notify Game logic to auto-complete hand
        }
    }
}
