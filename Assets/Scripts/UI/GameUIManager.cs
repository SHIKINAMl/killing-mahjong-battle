using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.UI
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private HandUI handUI;
        [SerializeField] private WallUI wallUI;
        [SerializeField] private RiverUI riverUI;
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private PlayerInfoUI playerInfoUI;
        [SerializeField] private AbilityUI abilityUI;
        [SerializeField] private YakuListUI yakuListUI;

        private void Start()
        {
            // Initialization if needed
            SetupUI();
        }

        private void SetupUI()
        {
            // Initial setup logic
            if (handUI != null) handUI.Setup(this);
            if (wallUI != null) wallUI.Setup(this);
            dialogueUI.ShowText("Game Start!");
        }
        
        // Data State
        private System.Collections.Generic.List<int> currentHandTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> currentWallTiles = new System.Collections.Generic.List<int>();
        private System.Collections.Generic.List<int> selectedTileIds = new System.Collections.Generic.List<int>();

        public void InitializeGame(System.Collections.Generic.List<int> initialWall)
        {
            currentWallTiles = new System.Collections.Generic.List<int>(initialWall);
            currentHandTiles.Clear();
            selectedTileIds.Clear();
            RefreshUI();
        }

        public void MoveTileToHand(int tileId)
        {
            if (selectedTileIds.Contains(tileId))
            {
                MoveSelectedFiles(true);
            }
            else
            {
                if (currentWallTiles.Contains(tileId))
                {
                    currentWallTiles.Remove(tileId);
                    currentHandTiles.Add(tileId);
                    RefreshUI();
                }
            }
            ClearSelection();
        }

        public void MoveTileToWall(int tileId)
        {
             if (selectedTileIds.Contains(tileId))
            {
                MoveSelectedFiles(false);
            }
            else
            {
                if (currentHandTiles.Contains(tileId))
                {
                    currentHandTiles.Remove(tileId);
                    currentWallTiles.Add(tileId);
                    RefreshUI();
                }
            }
            ClearSelection();
        }
        
        private void MoveSelectedFiles(bool toHand)
        {
            List<int> targetList = toHand ? currentHandTiles : currentWallTiles;
            List<int> sourceList = toHand ? currentWallTiles : currentHandTiles;
            
            foreach (var id in selectedTileIds)
            {
                if (sourceList.Contains(id))
                {
                    sourceList.Remove(id);
                    targetList.Add(id);
                }
            }
            RefreshUI();
        }

        public void SelectTile(int tileId, bool isInHand, bool multiSelect)
        {
            if (!multiSelect) selectedTileIds.Clear();
            if (!selectedTileIds.Contains(tileId)) selectedTileIds.Add(tileId);
            
            Debug.Log($"Selected Tiles Count: {selectedTileIds.Count}");
            DeselectAbility();
        }
        
        public void SelectTiles(System.Collections.Generic.List<int> ids)
        {
            selectedTileIds = new System.Collections.Generic.List<int>(ids);
            Debug.Log($"Box Selected Tiles Count: {selectedTileIds.Count}");
            DeselectAbility();
        }
        
        private void ClearSelection()
        {
            selectedTileIds.Clear();
            // TODO: Visual Update
        }

        public void DeselectAbility()
        {
            if (abilityUI != null) abilityUI.DeselectAll();
        }

        public bool IsPointerInHandArea(Vector2 screenPos)
        {
            if (handUI != null) return handUI.IsPointInHandArea(screenPos);
            return false;
        }

        private void RefreshUI()
        {
            if (handUI != null) handUI.SetHand(currentHandTiles);
            if (wallUI != null) wallUI.SetWall(currentWallTiles);
            
            Debug.Log($"UI Refreshed. Hand: {currentHandTiles.Count}, Wall: {currentWallTiles.Count}");
        }
        
        // Public methods to access UI components if needed
        public HandUI HandUI => handUI;
        public DialogueUI DialogueUI => dialogueUI;
        public PlayerInfoUI PlayerInfoUI => playerInfoUI;
        public AbilityUI AbilityUI => abilityUI;
        public YakuListUI YakuListUI => yakuListUI;
    }
}
