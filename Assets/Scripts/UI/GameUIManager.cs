using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;

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
        [SerializeField] private BettingUI bettingUI;
        [SerializeField] private PhaseTransitionUI phaseTransitionUI;

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
        public BettingUI BettingUI => bettingUI;
        public PhaseTransitionUI PhaseTransitionUI => phaseTransitionUI;

        // Engine Integration
        public void ApplyGameStateFromJSON(string jsonString, string localPlayerId)
        {
            try
            {
                GameStateData state = JsonUtility.FromJson<GameStateData>(jsonString);
                if (state != null)
                {
                    ApplyGameState(state, localPlayerId);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse GameStateData JSON: {e.Message}\nJSON:\n{jsonString}");
            }
        }

        public void ApplyGameState(GameStateData state, string localPlayerId)
        {
            // 1. Find Local Player
            PlayerStateData localPlayer = null;
            if (state.players != null)
            {
                foreach (var p in state.players)
                {
                    if (p.id == localPlayerId)
                    {
                        localPlayer = p;
                        break;
                    }
                }
            }

            if (localPlayer == null)
            {
                Debug.LogWarning($"Local player {localPlayerId} not found in state data.");
                return;
            }

            // 2. Update HP
            if (playerInfoUI != null)
            {
                playerInfoUI.SetHP(localPlayer.health);
            }

            // 3. Update internal tracking lists and UI
            if (localPlayer.hand != null)
            {
                currentHandTiles = new List<int>(localPlayer.hand);
                if (handUI != null) handUI.SetHand(currentHandTiles);
            }

            if (localPlayer.wall != null)
            {
                currentWallTiles = new List<int>(localPlayer.wall);
                if (wallUI != null) wallUI.SetWall(currentWallTiles);
            }

            if (localPlayer.discards != null)
            {
                if (riverUI != null) riverUI.SetRiver(new List<int>(localPlayer.discards));
            }
            
            // 4. Update Game Status Text
            string statusMsg = $"Round {state.round} - {state.honba} Honba\nTarget: {state.status}";
            if (state.current_player == localPlayerId)
                statusMsg += "\nYour Turn!";
            
            if (dialogueUI != null) dialogueUI.ShowText(statusMsg);
            
            ClearSelection();

            // 5. Handle Phase Logic (Mocking status numbers: 1=Play, 2=Betting, etc depending on Python Engine)
            // Assuming status 2 is Betting Phase. Adjust if python engine uses different enums.
            if (state.status == 2) 
            {
                StartBettingPhase(localPlayer);
            }
        }

        private void StartBettingPhase(PlayerStateData localPlayer)
        {
            SetMatchUIVisibility(false); // Hide irrelevant UI
            if (bettingUI != null)
            {
                Debug.Log($"Starting Betting Phase for {localPlayer.id}");
                bettingUI.ShowBettingPhase(20000, localPlayer.health, OnBetConfirmed);
            }
            else
            {
                Debug.LogError("BettingUI reference is missing in GameUIManager! Please assign it in the Inspector.");
            }
        }

        public void SetMatchUIVisibility(bool visible)
        {
            // The components to hide during betting phase
            if (handUI != null) handUI.gameObject.SetActive(visible);
            if (wallUI != null) wallUI.gameObject.SetActive(visible);
            if (riverUI != null) riverUI.gameObject.SetActive(visible);
            if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(visible); // Assuming enemy HP is somewhere else or part of dialogue?
            if (yakuListUI != null) yakuListUI.gameObject.SetActive(visible);
            
            // DialogueUI (enemy comments) and BettingUI remain active
        }

        private void OnBetConfirmed(int betAmount)
        {
            Debug.Log($"Bet confirmed: {betAmount}");
            bettingUI.HideBettingPhase();

            // Wait for all players to confirm in real game, then trigger animation phase.
            // For now, simulate all players confirming immediately.
            TriggerBettingAnimationPhase($"Round 1"); // We will pass actual round string
        }

        public void TriggerBettingAnimationPhase(string roundString)
        {
             if (phaseTransitionUI != null)
             {
                 Debug.Log("Triggering Phase Transition Animation.");
                 
                 // 画面に横線が入る瞬間で敵の会話UIが消えるようにする
                 if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);

                 // Start transition
                 phaseTransitionUI.PlayTransition(roundString, 
                    onMidpoint: () => {
                         // Swap UI to Match UI here behind the dark screen if necessary
                         // This is where you might enable HandUI, WallUI, etc, if they were hidden during betting
                         Debug.Log("Midpoint of Transition (Screen is Dark)");

                         // なお、ここでは会話UIは非表示のまま維持する
                    },
                    onComplete: () => {
                         Debug.Log("Transition Complete, Match Phase begins.");
                         SetMatchUIVisibility(true); // Restore match UI
                         
                         // 対局（手牌フェイズ）が始まったら会話UIを出す
                         if (dialogueUI != null) dialogueUI.gameObject.SetActive(true);
                    }
                 );
             }
             else
             {
                 Debug.LogError("PhaseTransitionUI reference is missing in GameUIManager! Please assign it in the Inspector.");
             }
        }
    }
}
