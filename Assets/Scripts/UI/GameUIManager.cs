using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private HandUI handUI;
        [SerializeField] private WallUI wallUI;
        [SerializeField] private RiverUI riverUI;
        [SerializeField] private EnemyHandUI enemyHandUI;
        [SerializeField] private EnemyWallUI enemyWallUI;
        [SerializeField] private RiverUI enemyRiverUI;
        [SerializeField] private WaitUI waitUI;
        [SerializeField] private DialogueUI dialogueUI;
        [SerializeField] private PlayerInfoUI playerInfoUI;
        [SerializeField] private EnemyInfoUI enemyInfoUI;
        [SerializeField] private AbilityUI abilityUI;
        [SerializeField] private YakuListUI yakuListUI;
        [SerializeField] private BettingUI bettingUI;
        [SerializeField] private PhaseTransitionUI phaseTransitionUI;
        [SerializeField] private RonAnimationUI ronAnimationUI;
        [SerializeField] private MatchmakingUI matchmakingUI;

        [Header("Effects")]
        [SerializeField] private GameObject victoryEffectPrefab;
        [SerializeField] private GameObject damageEffectPrefab;

        [Header("Tile Prefab")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;

        [Header("Debug Client")]
        [SerializeField] private bool showEnemyHandDebug = true;

        private RoundStatus currentPhaseStatus = RoundStatus.None;
        public RoundStatus CurrentPhaseStatus => currentPhaseStatus;
        
        private bool isTransitioning = false;

        private void Start()
        {
            SetupManagers();
            SetupUI();
            SubscribeEvents();
        }

        private void SetupManagers()
        {
            if (BoardStateManager.Instance == null) gameObject.AddComponent<BoardStateManager>();
            if (ReactionController.Instance == null) 
            {
                var reaction = gameObject.AddComponent<ReactionController>();
                reaction.Setup(dialogueUI, enemyInfoUI, playerInfoUI);
            }
            if (NetworkMessageHandler.Instance == null) gameObject.AddComponent<NetworkMessageHandler>();
        }

        private void SubscribeEvents()
        {
            var board = BoardStateManager.Instance;
            board.OnBoardStateRebuilt += RebuildAllTilesFromState;
            board.OnSelectionChanged += UpdateSelectedTileVisuals;
            board.OnTileMovedToHand += HandleTileMovedToHand;
            board.OnTileMovedToWall += HandleTileMovedToWall;

            var net = NetworkMessageHandler.Instance;
            net.OnMatchmakingWaiting += ShowMatchmakingWaiting;
            net.OnGameStarted += OnGameStarted;
            net.OnPhaseStatusChanged += UpdatePhaseStatus;
            net.OnBettingComplete += OnBettingCompleteFromServer;
            net.OnTileDiscarded += HandleDiscardEvent;
            net.OnAgari += HandleAgari;
            net.OnHandSelectionAccepted += OnHandSelectionAccepted;
        }

        private void SetupUI()
        {
            if (handUI != null) handUI.Setup(this);
            if (wallUI != null) wallUI.Setup(this);
            if (enemyWallUI != null) enemyWallUI.Setup(this);
            if (waitUI != null) waitUI.gameObject.SetActive(false);
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
            
            SetMatchUIVisibility(false);
            if (riverUI != null) riverUI.gameObject.SetActive(false);
            if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(false);
            if (enemyHandUI != null) enemyHandUI.gameObject.SetActive(false);
            if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
            if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
            if (abilityUI != null) abilityUI.gameObject.SetActive(false);
            if (bettingUI != null) bettingUI.HideBettingPhase();
        }

        // --- Component accessors ---
        public HandUI HandUI => handUI;
        public DialogueUI DialogueUI => dialogueUI;
        public PlayerInfoUI PlayerInfoUI => playerInfoUI;
        public EnemyInfoUI EnemyInfoUI => enemyInfoUI;
        public AbilityUI AbilityUI => abilityUI;
        public YakuListUI YakuListUI => yakuListUI;
        public BettingUI BettingUI => bettingUI;
        public PhaseTransitionUI PhaseTransitionUI => phaseTransitionUI;

        // --- Entry points from external classes / old API ---
        
        public void ApplyGameStateFromJSON(string jsonString, string localPlayerId)
        {
            NetworkMessageHandler.Instance.SetLocalPlayerId(localPlayerId);
            NetworkMessageHandler.Instance.ProcessServerMessage(jsonString);
        }

        public void SendActionToServer(string actionType, ActionPayload dataPayload)
        {
            NetworkMessageHandler.Instance.SendActionToServer(actionType, dataPayload);
        }

        // --- UI interaction wrappers bridging to BoardStateManager ---

        public void MoveTileToHand(int tileId)
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            BoardStateManager.Instance.MoveTileToHand(tileId);
        }

        public void MoveTileToWall(int tileId)
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            BoardStateManager.Instance.MoveTileToWall(tileId);
            ClearSelection();
        }

        public void SelectManganHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            BoardStateManager.Instance.SelectManganHand();
        }

        public void SelectRandomHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            BoardStateManager.Instance.SelectRandomHand();
        }

        public void SelectTile(int tileId, bool isInHand, bool multiSelect)
        {
            if (currentPhaseStatus == RoundStatus.Discard && !BoardStateManager.Instance.IsLocalTurn) return;
            BoardStateManager.Instance.SelectTile(tileId, multiSelect);
            DeselectAbility();
        }

        public void SelectTiles(List<int> ids)
        {
            if (currentPhaseStatus == RoundStatus.Discard && !BoardStateManager.Instance.IsLocalTurn) return;
            BoardStateManager.Instance.SelectTiles(ids);
            DeselectAbility();
        }

        public void ClearSelection()
        {
            BoardStateManager.Instance.ClearSelection();
        }

        public bool IsTileSelected(int tileId)
        {
            return BoardStateManager.Instance.IsTileSelected(tileId);
        }

        public void DiscardSelectedTile()
        {
            if (currentPhaseStatus != RoundStatus.Discard) return;
            if (!BoardStateManager.Instance.IsLocalTurn) return;
            if (BoardStateManager.Instance.SelectedTileIds.Count == 0) return;
            if (dialogueUI != null && dialogueUI.IsLogOpen) return;

            int tileToDiscard = BoardStateManager.Instance.SelectedTileIds[0];
            Debug.Log($"Discarding tile: {tileToDiscard}");

            BoardStateManager.Instance.SetLocalTurn(false);

            int wallIndex = BoardStateManager.Instance.OriginalWallTiles.IndexOf(tileToDiscard);
            if (wallIndex < 0) wallIndex = tileToDiscard; // フォールバック

            SendActionToServer("discard", new ActionPayload { wall_index = wallIndex, tile = tileToDiscard });
            ClearSelection();
        }

        public void CompleteHandSelection()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (dialogueUI != null && dialogueUI.IsLogOpen) return;
            
            List<int> handIndexes = new List<int>();
            HashSet<int> usedIndexes = new HashSet<int>();
            foreach(int tileId in BoardStateManager.Instance.CurrentHandTiles) {
                 var wallTiles = BoardStateManager.Instance.OriginalWallTiles;
                 int idx = -1;
                 for (int i = 0; i < wallTiles.Count; i++)
                 {
                     if (wallTiles[i] == tileId && !usedIndexes.Contains(i))
                     {
                         idx = i;
                         break;
                     }
                 }
                 if (idx >= 0) {
                     handIndexes.Add(idx);
                     usedIndexes.Add(idx);
                 }
            }
            
            SendActionToServer("select", new ActionPayload { hand_indexes = handIndexes, hand = BoardStateManager.Instance.CurrentHandTiles });
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

        // --- Visual Event Handlers (Subscribed to managers) ---

        private void RebuildAllTilesFromState()
        {
            if (tilePrefab == null) return;
            var board = BoardStateManager.Instance;

            // 1. HandUI
            if (handUI != null)
            {
                for (int i = handUI.GetHandSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = handUI.GetHandSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                handUI.GetHandSlots().Clear();

                foreach (var id in board.CurrentHandTiles)
                {
                    GameObject obj = Instantiate(tilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                    if (rt != null) {
                        InitializeTileComponent(rt, id, true);
                        handUI.AddTileToHand(rt, id);
                    }
                }
            }

            // 2. WallUI
            if (wallUI != null)
            {
                for (int i = wallUI.GetWallSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = wallUI.GetWallSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                wallUI.GetWallSlots().Clear();

                List<RectTransform> wallGenerated = new List<RectTransform>();
                foreach (var id in board.CurrentWallTiles)
                {
                    GameObject obj = Instantiate(tilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                    if (rt != null) {
                        InitializeTileComponent(rt, id, false);
                        wallGenerated.Add(rt);
                    }
                }
                wallUI.LayoutWallTiles(wallGenerated, board.CurrentWallTiles, board.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
            }

            // 3. Enemy HandUI
            if (enemyHandUI != null)
            {
                enemyHandUI.ClearHand();
                foreach (var id in board.CurrentEnemyHandTiles)
                {
                    GameObject obj = Instantiate(tilePrefab, transform); 
                    RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                    if (rt != null) {
                        int visualId = showEnemyHandDebug ? id : 0;
                        InitializeTileComponent(rt, visualId, false);
                        enemyHandUI.AddEnemyTile(rt, visualId, id);
                    }
                }
            }

            // 4. Enemy WallUI
            if (enemyWallUI != null)
            {
                for (int i = enemyWallUI.GetEnemyWallSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = enemyWallUI.GetEnemyWallSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                enemyWallUI.GetEnemyWallSlots().Clear();

                List<RectTransform> enemyWallGenerated = new List<RectTransform>();
                foreach (var id in board.CurrentEnemyWallTiles)
                {
                    GameObject obj = Instantiate(tilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                    if (rt != null) {
                        int visualId = showEnemyHandDebug ? id : 0;
                        InitializeTileComponent(rt, visualId, false);
                        enemyWallGenerated.Add(rt);
                    }
                }
                enemyWallUI.LayoutEnemyWallTiles(enemyWallGenerated, board.CurrentEnemyWallTiles, currentPhaseStatus == RoundStatus.Discard);
            }

            if (waitUI != null && currentPhaseStatus == RoundStatus.Discard)
            {
                waitUI.DisplayWaits(board.CurrentWaitTiles);
            }
        }

        private void InitializeTileComponent(RectTransform rt, int id, bool inHand)
        {
            if (tileResourceManager != null)
            {
                var visual = rt.GetComponent<TileVisual>();
                if (visual != null) visual.SetTile(id, tileResourceManager.GetTileSprite(id));
            }

            var interaction = rt.GetComponent<TileInteraction>();
            if (interaction == null) interaction = rt.gameObject.AddComponent<TileInteraction>();
            
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            
            interaction.Initialize(id, inHand, this, canvas);
        }

        private void UpdateSelectedTileVisuals()
        {
            var selectedIds = BoardStateManager.Instance.SelectedTileIds;
            if (wallUI != null)
            {
                foreach (var t in wallUI.GetWallSlots())
                {
                    var interaction = t.GetComponent<TileInteraction>();
                    if (interaction != null)
                    {
                        t.localPosition = interaction.OriginalWallPosition;
                    }
                }
            }
        }

        private void HandleTileMovedToHand(int tileId)
        {
            if (wallUI != null && handUI != null)
            {
                RectTransform movedTile = wallUI.GrabTile(tileId);
                if (movedTile != null)
                {
                    handUI.AddTileToHand(movedTile, tileId);
                }
            }
        }

        private void HandleTileMovedToWall(int tileId)
        {
            if (handUI != null && wallUI != null)
            {
                RectTransform movedTile = null;
                foreach (RectTransform t in handUI.GetHandSlots())
                {
                    var interaction = t.GetComponent<TileInteraction>();
                    if (interaction != null && interaction.TileId == tileId)
                    {
                        movedTile = t;
                        break;
                    }
                }

                if (movedTile != null)
                {
                    handUI.RemoveTileFromHand(movedTile, tileId);
                    wallUI.ReturnTileToWall(movedTile, tileId);
                }
            }
        }

        public void HandleDiscardEvent(int discardedTileId, bool isLocalPlayer)
        {
            if (playerInfoUI != null) playerInfoUI.SetDiscardingState(false);
            if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);

            if (isLocalPlayer)
            {
                BoardStateManager.Instance.RemoveTileFromWall(discardedTileId);

                if (wallUI != null)
                {
                    RectTransform tileRt = wallUI.GrabTile(discardedTileId);
                    if (tileRt != null) Destroy(tileRt.gameObject);
                    
                    List<RectTransform> remainingTiles = new List<RectTransform>();
                    foreach (var st in wallUI.GetWallSlots()) if (st != null) remainingTiles.Add(st);
                    
                    wallUI.LayoutWallTiles(remainingTiles, BoardStateManager.Instance.CurrentWallTiles, BoardStateManager.Instance.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
                }

                if (riverUI != null) riverUI.AddTile(discardedTileId);
            }
            else
            {
                BoardStateManager.Instance.RemoveTileFromEnemyWall();

                if (enemyWallUI != null && enemyRiverUI != null)
                {
                    RectTransform tileRt = enemyWallUI.GrabEnemyTile();
                    if (tileRt != null)
                    {
                        enemyRiverUI.AddExistingTile(tileRt, discardedTileId);
                    }
                }
                else if (enemyRiverUI != null)
                {
                    enemyRiverUI.AddTile(discardedTileId);
                }
            }

            string tileName = new TileData(discardedTileId).GetTileName();
            ReactionController.Instance.EnqueueDiscardReaction(discardedTileId, isLocalPlayer, tileName);
        }

        // --- Phase and Visibility Handlers ---
        
        public void ShowMatchmakingWaiting()
        {
            if (matchmakingUI != null) matchmakingUI.ShowWaiting();
            SetMatchUIVisibility(false);
            
            if (riverUI != null) riverUI.gameObject.SetActive(false);
            if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(false);
            if (enemyHandUI != null) enemyHandUI.gameObject.SetActive(false);
            if (waitUI != null) waitUI.gameObject.SetActive(false);
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
            
            if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
            if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
            
            if (abilityUI != null) abilityUI.gameObject.SetActive(false);
            if (ronAnimationUI != null) ronAnimationUI.gameObject.SetActive(false);
            if (bettingUI != null) bettingUI.HideBettingPhase();
        }

        public void OnGameStarted()
        {
            if (matchmakingUI != null) matchmakingUI.Hide();
            if (dialogueUI != null) 
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText("Match Found! Game Starting...");
            }
            if (playerInfoUI != null) playerInfoUI.SetHP(20000);
            if (enemyInfoUI != null) enemyInfoUI.SetHP(20000);
        }

        private void UpdatePhaseStatus(RoundStatus newStatus)
        {
            currentPhaseStatus = newStatus;
            if (PhaseManager.Instance != null) PhaseManager.Instance.ChangeRoundStatus(newStatus);
            if (handUI != null) handUI.UpdateLayout(currentPhaseStatus);

            if (wallUI != null)
            {
                List<RectTransform> remainingTiles = new List<RectTransform>();
                foreach (var st in wallUI.GetWallSlots()) if (st != null) remainingTiles.Add(st);
                wallUI.LayoutWallTiles(remainingTiles, BoardStateManager.Instance.CurrentWallTiles, BoardStateManager.Instance.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
            }
            
            HandlePhaseVisibility(newStatus);
        }

        private void HandlePhaseVisibility(RoundStatus status)
        {
            if (isTransitioning) return;

            if (riverUI != null) riverUI.gameObject.SetActive(status == RoundStatus.Discard);
            if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(status == RoundStatus.Discard);
            if (enemyHandUI != null) enemyHandUI.gameObject.SetActive(status == RoundStatus.Discard);

            switch (status)
            {
                case RoundStatus.Betting:
                    SetMatchUIVisibility(false); 
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
                    if (waitUI != null) waitUI.gameObject.SetActive(false);
                    StartBettingPhase(20000);
                    break;
                case RoundStatus.Dealing:
                case RoundStatus.HandSelection:
                    SetMatchUIVisibility(true);
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (waitUI != null) waitUI.gameObject.SetActive(false);
                    if (abilityUI != null) abilityUI.gameObject.SetActive(true);
                    break;
                case RoundStatus.TurnDecision:
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
                    if (waitUI != null) waitUI.gameObject.SetActive(false);
                    break;
                case RoundStatus.Discard:
                    if (handUI != null) handUI.gameObject.SetActive(true);
                    if (wallUI != null) wallUI.gameObject.SetActive(true);
                    if (enemyWallUI != null) enemyWallUI.gameObject.SetActive(true);
                    
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    
                    if (waitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
                    {
                        waitUI.gameObject.SetActive(true);
                        waitUI.DisplayWaits(BoardStateManager.Instance.CurrentWaitTiles);
                    }
                    if (abilityUI != null) abilityUI.gameObject.SetActive(true);
                    break;
                case RoundStatus.Agari:
                case RoundStatus.Ron:
                case RoundStatus.Result:
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    
                    if (ronAnimationUI != null)
                    {
                        bool isLocalWin = BoardStateManager.Instance.LastIsLocalWin;
                        List<int> winningHand = isLocalWin ? new List<int>(BoardStateManager.Instance.CurrentHandTiles) : new List<int>(BoardStateManager.Instance.CurrentEnemyHandTiles);
                        
                        List<string> dummyYaku = new List<string> { "立直 (1飜)", "一発 (1飜)" };
                        string dummyFormula = "30符 2飜";
                        string dummyRank = "満貫";
                        int dummyRonTile = winningHand.Count > 0 ? winningHand[winningHand.Count - 1] : 0;
                        
                        if (riverUI != null) riverUI.gameObject.SetActive(false);
                        if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(false);
                        if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
                        SetMatchUIVisibility(false);

                        if (abilityUI != null) abilityUI.gameObject.SetActive(false);

                        ronAnimationUI.gameObject.SetActive(true);
                        ronAnimationUI.PlayRonSequence(winningHand, dummyRonTile, dummyYaku, dummyFormula, dummyRank, isLocalWin, () => OnRonAnimationComplete(isLocalWin));
                    }
                    break;
            }
        }

        public void SetMatchUIVisibility(bool visible)
        {
            if (handUI != null) handUI.gameObject.SetActive(visible);
            if (wallUI != null) wallUI.gameObject.SetActive(visible);
            if (enemyWallUI != null) enemyWallUI.gameObject.SetActive(visible);
            if (yakuListUI != null) yakuListUI.gameObject.SetActive(visible);
            
            if (waitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                if (!visible) waitUI.gameObject.SetActive(false);
            }
        }

        private void StartBettingPhase(int currentHealth)
        {
            if (bettingUI != null)
            {
                bettingUI.ShowBettingPhase(20000, currentHealth, OnBetConfirmed);
            }
        }

        private void OnBetConfirmed(int betAmount)
        {
            bettingUI.HideBettingPhase();
            SendActionToServer("bet", new ActionPayload { bet_amount = betAmount, amount = betAmount });
        }

        public void OnBettingCompleteFromServer(int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
            TriggerBettingAnimationPhase($"Round 1", playerBet, enemyBet, playerHp, enemyHp); 
        }

        public void TriggerBettingAnimationPhase(string roundString, int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
             if (isTransitioning) return;
             if (currentPhaseStatus != RoundStatus.Betting) return;

             if (phaseTransitionUI != null)
             {
                 isTransitioning = true;
                 
                 if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
                 if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
                 if (abilityUI != null) abilityUI.gameObject.SetActive(false);
                 
                 if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);

                 phaseTransitionUI.PlayTransition(roundString, playerInfoUI, playerBet, enemyBet, playerHp, enemyHp,
                    onMidpoint: () => {},
                    onComplete: () => {
                         isTransitioning = false;
                         SetMatchUIVisibility(true); 
                         
                         if (currentPhaseStatus == RoundStatus.Betting)
                         {
                             UpdatePhaseStatus(RoundStatus.Discard);
                         }

                         HandlePhaseVisibility(currentPhaseStatus);
                         
                         if (playerInfoUI != null) playerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
                         if (enemyInfoUI != null) enemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
                         
                         if (dialogueUI != null) dialogueUI.gameObject.SetActive(true);
                    }
                 );
             }
        }

        public void ShowDialogue(string text)
        {
            if (dialogueUI != null) dialogueUI.ShowText(text);
        }

        private void OnHandSelectionAccepted()
        {
            if (dialogueUI != null) dialogueUI.ShowText("相手の手牌選択を待っています...");
        }

        private void HandleAgari(bool isLocalWin)
        {
            // UpdatePhaseStatus is handled via Network message event routing
        }

        private void OnRonAnimationComplete(bool isLocalWin)
        {
            if (isLocalWin)
            {
                if (victoryEffectPrefab != null && playerInfoUI != null) 
                    Instantiate(victoryEffectPrefab, playerInfoUI.transform.position, Quaternion.identity);
                if (damageEffectPrefab != null && enemyInfoUI != null) 
                    Instantiate(damageEffectPrefab, enemyInfoUI.transform.position, Quaternion.identity);
            }
            else
            {
                if (victoryEffectPrefab != null && enemyInfoUI != null) 
                    Instantiate(victoryEffectPrefab, enemyInfoUI.transform.position, Quaternion.identity);
                if (damageEffectPrefab != null && playerInfoUI != null) 
                    Instantiate(damageEffectPrefab, playerInfoUI.transform.position, Quaternion.identity);
            }
        }
    }
}
