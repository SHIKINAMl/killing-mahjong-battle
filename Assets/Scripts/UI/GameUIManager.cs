using UnityEngine;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("Phase UI Containers")]
        [SerializeField] private GameObject handSelectionPhaseContainer;
        [SerializeField] private GameObject discardPhaseContainer;
        [SerializeField] private GameObject gameEndContainer;

        private AgariSelectionUI agariSelectionUI;

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
        [SerializeField] private ConfirmationDialogUI confirmationDialogUI;
        [SerializeField] private RonAnimationUI ronAnimationUI;
        [SerializeField] private VictoryUI victoryUI;
        [SerializeField] private MatchmakingUI matchmakingUI;
        [SerializeField] private DoraDisplayUI doraDisplayUI;
        [SerializeField] private GameObject ronWaitPanel;

        [Header("Effects")]
        [SerializeField] private GameObject victoryEffectPrefab;
        [SerializeField] private GameObject damageEffectPrefab;

        public bool IsOpponentSkillProcessing { get; private set; } = false;
        public bool IsGameOver { get; private set; } = false;
        public int LocalFinalScore { get; private set; } = 0;
        public int EnemyFinalScore { get; private set; } = 0;

        [Header("Tile Prefab")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;

        private RoundStatus currentPhaseStatus = RoundStatus.None;
        public RoundStatus CurrentPhaseStatus => currentPhaseStatus;
        
        private bool isTransitioning = false;
        public bool IsTransitioning => isTransitioning;

        // Sub-controllers
        public GameUIPhaseController PhaseController { get; private set; }
        public GameUIVisualController VisualController { get; private set; }
        public GameUIHandSelectionController HandSelectionController { get; private set; }
        public GameUISkillController SkillController { get; private set; }
        public GameUINetworkHandler NetworkHandler { get; private set; }

        public bool IsMulliganSelection => SkillController != null && SkillController.IsMulliganSelection;

        private void Start()
        {
            SetupManagers();
            SetupControllers();
            SetupUI();
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
            if (Managers.DialogueManager.Instance == null) gameObject.AddComponent<Managers.DialogueManager>();
        }

        private void SetupControllers()
        {
            PhaseController = GetComponent<GameUIPhaseController>();
            if (PhaseController != null) PhaseController.Setup(this);

            VisualController = GetComponent<GameUIVisualController>();
            if (VisualController != null) VisualController.Setup(this);

            HandSelectionController = GetComponent<GameUIHandSelectionController>();
            if (HandSelectionController != null) HandSelectionController.Setup(this);

            SkillController = GetComponent<GameUISkillController>();
            if (SkillController != null) SkillController.Setup(this);

            NetworkHandler = GetComponent<GameUINetworkHandler>();
            if (NetworkHandler != null) NetworkHandler.Setup(this);
            
            var board = BoardStateManager.Instance;
            board.OnTurnChanged += HandleTurnChanged;
            if (NetworkMessageHandler.Instance != null)
            {
                NetworkMessageHandler.Instance.OnTileDiscarded += HandleDiscardEvent;
                NetworkMessageHandler.Instance.OnGameEnded += HandleGameEnded;
                NetworkMessageHandler.Instance.OnStatusReceived += HandleStatusReceived;
                NetworkMessageHandler.Instance.OnAgariPendingReceived += HandleAgariPendingReceived;
                NetworkMessageHandler.Instance.OnOpeningBoostAssigned += HandleOpeningBoostAssigned;
            }
        }

        private void OnDestroy()
        {
            if (BoardStateManager.Instance != null)
            {
                BoardStateManager.Instance.OnTurnChanged -= HandleTurnChanged;
            }
            if (NetworkMessageHandler.Instance != null)
            {
                NetworkMessageHandler.Instance.OnTileDiscarded -= HandleDiscardEvent;
                NetworkMessageHandler.Instance.OnGameEnded -= HandleGameEnded;
                NetworkMessageHandler.Instance.OnStatusReceived -= HandleStatusReceived;
                NetworkMessageHandler.Instance.OnAgariPendingReceived -= HandleAgariPendingReceived;
                NetworkMessageHandler.Instance.OnOpeningBoostAssigned -= HandleOpeningBoostAssigned;
            }
        }

        private void SetupUI()
        {
            if (waitUI != null && enemyWaitUI == null)
            {
                // 相手用のWaitUIを生成する
                GameObject enemyWaitObj = Instantiate(waitUI.gameObject, waitUI.transform.parent);
                enemyWaitObj.name = "EnemyWaitUI";
                enemyWaitUI = enemyWaitObj.GetComponent<WaitUI>();
                
                // 画面中央に大きく配置するため、RectTransformのアンカーとスケールを調整
                RectTransform rt = enemyWaitObj.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(0, 150); // 中央より少し上
                    rt.localScale = new Vector3(1.5f, 1.5f, 1.5f); // 1.5倍に大きくして目立たせる
                }
                enemyWaitObj.SetActive(false);
            }

            if (confirmationDialogUI == null)
            {
                confirmationDialogUI = FindFirstObjectByType<ConfirmationDialogUI>(FindObjectsInactive.Include);
                if (confirmationDialogUI == null)
                {
                    Debug.LogError("[GameUIManager] ConfirmationDialogUI is not assigned and not found in the scene! This will cause hand confirmation to be skipped.");
                    if (dialogueUI != null) dialogueUI.ShowText("「警告：ConfirmationDialogUIがシーンに見つかりません！決定が即座に確定されます。」");
                }
            }

            if (handUI != null) handUI.Setup(this);
            if (wallUI != null) wallUI.Setup(this);
            if (enemyWallUI != null) enemyWallUI.Setup(this);
            if (enemyHandUI != null) 
            {
                enemyHandUI.Setup(this);
                enemyHandUI.gameObject.SetActive(false);
            }
            if (waitUI != null) waitUI.gameObject.SetActive(false);
            
            if (agariSelectionUI == null)
            {
                var go = new GameObject("AgariSelectionUI");
                go.transform.SetParent(transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                agariSelectionUI = go.AddComponent<AgariSelectionUI>();
                agariSelectionUI.Hide();
            }

            if (PhaseController != null) PhaseController.SetMatchUIVisibility(false);
            if (riverUI != null) riverUI.gameObject.SetActive(false);
            if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(false);
            if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
            if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(false);
            if (abilityUI != null) abilityUI.gameObject.SetActive(false);
            if (bettingUI != null) bettingUI.HideBettingPhase(true);
            if (doraDisplayUI != null) doraDisplayUI.Hide();
            if (ronWaitPanel != null) ronWaitPanel.SetActive(false);
        }

        private void HandleGameEnded(int localScore, int enemyScore)
        {
            IsGameOver = true;
            LocalFinalScore = localScore;
            EnemyFinalScore = enemyScore;
        }

        public void ShowGameResult()
        {
            bool isWin = LocalFinalScore > 0 && EnemyFinalScore <= 0;
            if (victoryUI != null)
            {
                victoryUI.PlayAnimation(isWin ? VictoryType.NormalVictory : VictoryType.NormalDefeat);
            }
        }

        // --- Component accessors ---
        public HandUI HandUI => handUI;
        public WallUI WallUI => wallUI;
        public RiverUI RiverUI => riverUI;
        public EnemyHandUI EnemyHandUI => enemyHandUI;
        public EnemyWallUI EnemyWallUI => enemyWallUI;
        public RiverUI EnemyRiverUI => enemyRiverUI;
        private WaitUI enemyWaitUI;

        public WaitUI WaitUI => waitUI;
        public WaitUI EnemyWaitUI => enemyWaitUI;
        public DialogueUI DialogueUI => dialogueUI;
        public PlayerInfoUI PlayerInfoUI => playerInfoUI;
        public EnemyInfoUI EnemyInfoUI => enemyInfoUI;
        public AbilityUI AbilityUI => abilityUI;
        public YakuListUI YakuListUI => yakuListUI;
        public BettingUI BettingUI => bettingUI;
        public PhaseTransitionUI PhaseTransitionUI => phaseTransitionUI;
        public ConfirmationDialogUI ConfirmationDialogUI => confirmationDialogUI;
        public RonAnimationUI RonAnimationUI => ronAnimationUI;
        public VictoryUI VictoryUI => victoryUI;
        public MatchmakingUI MatchmakingUI => matchmakingUI;
        public DoraDisplayUI DoraDisplayUI => doraDisplayUI;
        public GameObject RonWaitPanel => ronWaitPanel;

        public GameObject TilePrefab => tilePrefab;
        public TileResourceManager TileResourceManager => tileResourceManager;
        public GameObject VictoryEffectPrefab => victoryEffectPrefab;
        public GameObject DamageEffectPrefab => damageEffectPrefab;

        public void SetCurrentPhaseStatus(RoundStatus status)
        {
            currentPhaseStatus = status;
        }

        public void SetIsTransitioning(bool value)
        {
            isTransitioning = value;
        }

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
            Debug.Log($"[GameUIManager] MoveTileToHand called. tileId: {tileId}, currentPhaseStatus: {currentPhaseStatus}");
            if (currentPhaseStatus != RoundStatus.HandSelection) 
            {
                Debug.Log($"[GameUIManager] MoveTileToHand aborted. Not HandSelection phase. current: {currentPhaseStatus}");
                return;
            }
            if (handUI != null && handUI.IsSubmitted) 
            {
                Debug.Log("[GameUIManager] MoveTileToHand aborted. HandUI is already submitted.");
                return;
            }
            
            Debug.Log($"[GameUIManager] Executing BoardStateManager.MoveTileToHand({tileId})");
            BoardStateManager.Instance.TargetHandIndexes = null;
            BoardStateManager.Instance.MoveTileToHand(tileId);
        }

        public void MoveTileToWall(int tileId)
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;
            BoardStateManager.Instance.TargetHandIndexes = null;
            BoardStateManager.Instance.MoveTileToWall(tileId);
            ClearSelection();
        }

        public void SelectManganHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;
            BoardStateManager.Instance.SelectManganHand();
        }

        public void SelectRandomHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;
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

            BoardStateManager.Instance.SetLocalTurn(false);

            int wallIndex = BoardStateManager.Instance.FindAvailableWallIndex(tileToDiscard);
            if (wallIndex < 0) wallIndex = tileToDiscard;

            BoardStateManager.Instance.MarkWallIndexAsDiscarded(wallIndex);
            SendActionToServer("discard", new ActionPayload { wall_index = wallIndex, tile = tileToDiscard });
            ClearSelection();
        }

        public void CompleteHandSelection()
        {
            HandSelectionController?.CompleteHandSelection();
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

        public void OnTileHoverEnter(TileInteraction interaction)
        {
            if (currentPhaseStatus == RoundStatus.Discard && BoardStateManager.Instance.IsLocalTurn && !interaction.IsInHand && interaction.TileId != -1)
            {
                if (wallUI != null) wallUI.SetActiveDiscardTile(interaction);
            }
        }

        public void OnTileHoverExit(TileInteraction interaction)
        {
            // Do nothing
        }


        private void HandleStatusReceived(KillingMahjong.EngineData.StatusData data)
        {
            if (yakuListUI != null)
            {
                yakuListUI.UpdateBoostData(BoardStateManager.Instance.LocalBoostHandBonus, BoardStateManager.Instance.EnemyBoostHandBonus);
            }
        }

        private void HandleOpeningBoostAssigned()
        {
            if (yakuListUI != null)
            {
                yakuListUI.UpdateBoostData(BoardStateManager.Instance.LocalBoostHandBonus, BoardStateManager.Instance.EnemyBoostHandBonus);
            }
        }

        private bool _isAgariPending = false;

        private void HandleAgariPendingReceived(KillingMahjong.EngineData.AgariPendingData data)
        {
            Debug.Log($"[GameUIManager] HandleAgariPendingReceived called. winner_id: {data.winner_id}, loser_id: {data.loser_id}, tile_id: {data.tile_id}");
            
            if (data.winner_id == NetworkMessageHandler.Instance.LocalPlayerId)
            {
                if (BoardStateManager.Instance.NonManganWaitTiles.Contains(data.tile_id))
                {
                    Debug.Log($"[GameUIManager] Ignored agari_pending because tile {data.tile_id} is non-mangan.");
                    SendActionToServer("agari", new KillingMahjong.Network.ActionPayload { accept = false });
                    return;
                }

                Debug.Log("[GameUIManager] I am the winner! Showing RonWaitPanel.");
                _isAgariPending = true;
                
                if (RonWaitPanel != null)
                {
                    RonWaitPanel.SetActive(true);
                    RonWaitPanel.transform.SetAsLastSibling();

                    // 最前面に表示するためにCanvasを追加してソート順を強制する
                    Canvas canvas = RonWaitPanel.GetComponent<Canvas>();
                    if (canvas == null)
                    {
                        canvas = RonWaitPanel.AddComponent<Canvas>();
                    }
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 50; // DialogueUI や BloodMeter より高い値

                    if (RonWaitPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    {
                        RonWaitPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                    }
                    
                    var images = RonWaitPanel.GetComponentsInChildren<UnityEngine.UI.Image>();
                    foreach (var img in images)
                    {
                        if (img.GetComponent<UnityEngine.UI.Button>() == null && 
                            !img.gameObject.name.ToLower().Contains("button"))
                        {
                            var c = img.color;
                            c.a = 0.1f;
                            img.color = c;
                        }
                    }
                }
            }
        }

        private void HandleTurnChanged(bool isLocalTurn)
        {
            if (IsTransitioning) return; // アニメーション演出中は矢印を出さない
            if (wallUI != null)
            {
                wallUI.UpdateDiscardTurnIndicator(isLocalTurn, currentPhaseStatus == RoundStatus.Discard);
            }
        }

        public void HandleDiscardEvent(int discardedTileId, bool isLocalPlayer)
        {
            BoardStateManager.Instance.LastDiscardedTileId = discardedTileId;

            bool isGameEndPhase = currentPhaseStatus == RoundStatus.Agari || 
                                  currentPhaseStatus == RoundStatus.Ron || 
                                  currentPhaseStatus == RoundStatus.Result || 
                                  currentPhaseStatus == RoundStatus.Draw;
            if (isGameEndPhase) return;

            if (playerInfoUI != null) playerInfoUI.SetDiscardingState(false);
            if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);

            if (isLocalPlayer)
            {
                BoardStateManager.Instance.RemoveTileFromWall(discardedTileId);

                if (wallUI != null)
                {
                    RectTransform tileRt = wallUI.GrabTile(discardedTileId);
                    if (tileRt != null) Destroy(tileRt.gameObject);
                    
                    wallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
                }

                if (riverUI != null) riverUI.AddTile(discardedTileId);
            }
            else
            {
                BoardStateManager.Instance.RemoveTileFromEnemyWall();

                if (enemyRiverUI != null)
                {
                    enemyRiverUI.AddTile(discardedTileId);
                }
            }

            ReactionController.Instance.CheckDiscardConditions(discardedTileId, isLocalPlayer);
        }

        public void ClearAllTiles()
        {
            if (handUI != null)
            {
                foreach (RectTransform t in handUI.GetHandSlots().ToArray()) if (t != null) Destroy(t.gameObject);
                handUI.GetHandSlots().Clear();
            }
            if (wallUI != null)
            {
                foreach (Transform t in wallUI.GetWallSlots().ToArray()) if (t != null) Destroy(t.gameObject);
                wallUI.GetWallSlots().Clear();
            }
            if (enemyHandUI != null) enemyHandUI.ClearHand();
            if (enemyWallUI != null)
            {
                foreach (Transform t in enemyWallUI.GetEnemyWallSlots().ToArray()) if (t != null) Destroy(t.gameObject);
                enemyWallUI.GetEnemyWallSlots().Clear();
            }
            if (riverUI != null) riverUI.Clear();
            if (enemyRiverUI != null) enemyRiverUI.Clear();
            
            if (waitUI != null) waitUI.gameObject.SetActive(false);
            
            Managers.BoardStateManager.Instance.ClearAllBoardData();
        }

        // Methods invoked via Unity Events (e.g. Inspector Buttons)
        public void ExecuteRonAction()
        {
            Debug.Log($"[GameUIManager] ExecuteRonAction called. _isAgariPending={_isAgariPending}");
            if (_isAgariPending)
            {
                _isAgariPending = false;
                if (RonWaitPanel != null) RonWaitPanel.SetActive(false);
                SendActionToServer("agari", new KillingMahjong.Network.ActionPayload { accept = true });
                Debug.Log("[GameUIManager] Sent 'agari' action to server.");
            }
            
            PhaseController?.ExecuteRonAction();
        }

        public void ShowMatchmakingWaiting()
        {
            PhaseController?.ShowMatchmakingWaiting();
        }

        public void ShowDialogue(string text)
        {
            if (dialogueUI != null) dialogueUI.ShowText(text);
        }

        public void CancelSkillSelection()
        {
            SkillController?.CancelSkillSelection();
        }

        public void StartMulliganSelection()
        {
            SkillController?.StartMulliganSelection();
        }

        public void OnMulliganTileSelected(int tileId, RectTransform slotRt)
        {
            SkillController?.OnMulliganTileSelected(tileId, slotRt);
        }

        public void StartBoostHandSelection()
        {
            SkillController?.StartBoostHandSelection();
        }
        
        public void CancelHandSelection()
        {
            HandSelectionController?.CancelHandSelection();
        }

        // --- ダイエジェティック演出用（全体のスライド＆フェードアウト） ---
        public System.Collections.IEnumerator SlideUIRoutine(float duration, float uiXOffset, float worldXOffset, bool isLocalPlayer)
        {
            Transform[] elements = new Transform[] { 
                handUI?.transform, wallUI?.transform, riverUI?.transform,
                enemyHandUI?.transform, enemyWallUI?.transform, enemyRiverUI?.transform,
                dialogueUI?.transform, abilityUI?.transform, yakuListUI?.transform, bettingUI?.transform,
                isLocalPlayer ? enemyInfoUI?.transform : playerInfoUI?.transform // 相手のHPも消す
            };

            List<Transform> validElements = new List<Transform>();
            List<Vector3> startPositions = new List<Vector3>();
            List<Vector3> targetPositions = new List<Vector3>();
            List<CanvasGroup> canvasGroups = new List<CanvasGroup>();

            foreach (var t in elements)
            {
                if (t != null)
                {
                    validElements.Add(t);
                    startPositions.Add(t.localPosition);
                    
                    bool isUI = t.GetComponent<RectTransform>() != null;
                    targetPositions.Add(t.localPosition + new Vector3(isUI ? uiXOffset : worldXOffset, 0, 0));

                    if (isUI)
                    {
                        CanvasGroup cg = t.GetComponent<CanvasGroup>();
                        if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
                        canvasGroups.Add(cg);
                    }
                    else
                    {
                        canvasGroups.Add(null);
                    }
                }
            }

            float time = 0;
            // 往復の方向を判定（スライドアウトならフェードアウト、スライドインならフェードイン）
            bool isSlideOut = (uiXOffset < 0);
            float startAlpha = isSlideOut ? 1f : 0f;
            float targetAlpha = isSlideOut ? 0f : 1f;

            while (time < duration)
            {
                float progress = time / duration;
                float eased = progress * progress * (3f - 2f * progress);

                for (int i = 0; i < validElements.Count; i++)
                {
                    validElements[i].localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], eased);
                    
                    if (canvasGroups[i] != null)
                    {
                        canvasGroups[i].alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                    }
                }

                time += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < validElements.Count; i++)
            {
                validElements[i].localPosition = targetPositions[i];
                if (canvasGroups[i] != null)
                {
                    canvasGroups[i].alpha = targetAlpha;
                }
            }
        }
    }
}
