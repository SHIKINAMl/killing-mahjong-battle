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
        [SerializeField] private TileResourceManager tileResourceManager;
        [SerializeField] private RectTransform handAreaRect; // For drag detection

        private GameUIManager gameUIManager;

        public void Setup(GameUIManager manager)
        {
            this.gameUIManager = manager;
        }

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
            // 1. Convert to TileData
            List<TileData> allTiles = new List<TileData>();
            foreach (var id in tileIds)
            {
                allTiles.Add(new TileData(id));
            }

            // 2. Group by Category
            var manzu = new List<TileData>();
            var pinzu = new List<TileData>();
            var souzu = new List<TileData>();
            var honors = new List<TileData>();

            foreach (var t in allTiles)
            {
                switch (t.Category)
                {
                    case TileCategory.Manzu: manzu.Add(t); break;
                    case TileCategory.Pinzu: pinzu.Add(t); break;
                    case TileCategory.Souzu: souzu.Add(t); break;
                    case TileCategory.Honor: honors.Add(t); break;
                }
            }

            // 3. Sort Categories
            var categoryLists = new List<List<TileData>> { souzu, manzu, pinzu, honors };
            categoryLists.Sort((a, b) =>
            {
                int countCompare = b.Count.CompareTo(a.Count);
                if (countCompare != 0) return countCompare;
                int priorityA = GetCategoryPriority(a);
                int priorityB = GetCategoryPriority(b);
                return priorityA.CompareTo(priorityB);
            });

            // 4. Fill Slots (Layout handled by HorizontalLayoutGroup component)
            int flatIndex = 0;
            
            for (int i = 0; i < categoryLists.Count; i++)
            {
                var list = categoryLists[i];
                if (list.Count == 0) continue;

                // Sort inside category
                list.Sort((a, b) => a.Id.CompareTo(b.Id));

                foreach (var tile in list)
                {
                    Transform slot = null;
                    if (flatIndex < handSlots.Count) slot = handSlots[flatIndex];
                    else
                    {
                        if (tilePrefab != null && handSlotContainer != null)
                        {
                            var obj = Instantiate(tilePrefab, handSlotContainer);
                            slot = obj.transform;
                            handSlots.Add(slot);
                        }
                    }

                    if (slot != null)
                    {
                         // No manual position set
                         slot.localRotation = Quaternion.identity;
                         slot.gameObject.SetActive(true);

                         if (tileResourceManager != null)
                         {
                             var visual = slot.GetComponent<TileVisual>();
                             if (visual != null) visual.SetTile(tile.Id, tileResourceManager.GetTileSprite(tile.Id));
                         }

                         // Interaction
                         var interaction = slot.GetComponent<TileInteraction>();
                         if (interaction == null) interaction = slot.gameObject.AddComponent<TileInteraction>();
                         // Ensure canvas is found. HandUI should be under canvas?
                         Canvas canvas = GetComponentInParent<Canvas>();
                         if (gameUIManager != null) interaction.Initialize(tile.Id, true, gameUIManager, canvas);
                    }
                    flatIndex++;
                }
            }
            
            // Hide unused slots
            for (int k = flatIndex; k < handSlots.Count; k++)
            {
                if (handSlots[k] != null)
                    handSlots[k].gameObject.SetActive(false);
            }
            Debug.Log($"Hand set. Total tiles: {flatIndex}");
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
            Debug.Log($"Selected index: {currentSelectionIndex}");
            // Notify Game logic
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
