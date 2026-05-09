using UnityEngine;
using System.Collections;
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
        [SerializeField] private ConfirmationDialogUI confirmationDialogUI;
        [SerializeField] private RonAnimationUI ronAnimationUI;
        [SerializeField] private MatchmakingUI matchmakingUI;

        [Header("Effects")]
        [SerializeField] private GameObject victoryEffectPrefab;
        [SerializeField] private GameObject damageEffectPrefab;

        [Header("Tile Prefab")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private TileResourceManager tileResourceManager;

        [Header("Debug Client")]
        // Inspector value ignored, enemy tiles are always hidden in actual gameplay
        [SerializeField] private bool showEnemyHandDebug = false;

        private RoundStatus currentPhaseStatus = RoundStatus.None;
        public RoundStatus CurrentPhaseStatus => currentPhaseStatus;
        
        private bool isTransitioning = false;
        public bool IsTransitioning => isTransitioning;
        private bool _autoConfirmNextHandSelection = false;

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
            net.OnDraw += HandleDraw;
            net.OnHandSelectionAccepted += OnHandSelectionAccepted;
            net.OnError += HandleError;
            net.OnHandSelectionConfirmation += HandleHandSelectionConfirmation;
            net.OnIsTenpaiReceived += HandleIsTenpaiReceived;
            net.OnNotTenpaiReceived += HandleNotTenpaiReceived;
        }

        private void HandleHandSelectionConfirmation(KillingMahjong.EngineData.HandSelectionConfirmationData data)
        {
            if (_autoConfirmNextHandSelection)
            {
                // すでに予想点数ダイアログでOKを押しているので自動的に select_confirm を送る
                _autoConfirmNextHandSelection = false;
                if (handUI != null) handUI.SetSubmittedState(true);
                SendActionToServer("select_confirm", new ActionPayload { hand_indexes = data.hand_indexes, hand = _pendingHandTiles });
                return;
            }

            // 確認ダイアログを表示（演出の前に表示される）
            if (confirmationDialogUI != null)
            {
                confirmationDialogUI.ShowDialog(
                    data.message,
                    () => {
                        // OK → すぐに select_confirm を送信。演出は OnHandSelectionAccepted で行われる
                        if (handUI != null) handUI.SetSubmittedState(true);
                        SendActionToServer("select_confirm", new ActionPayload { hand_indexes = data.hand_indexes, hand = _pendingHandTiles });
                    },
                    () => {
                        // キャンセル → 決定ボタンを押す前の状態に戻す
                        if (handUI != null) handUI.SetSubmittedState(false);
                        BoardStateManager.Instance.ClearWaitTiles();
                        if (waitUI != null) waitUI.Hide();
                    }
                );
            }
            else
            {
                // ConfirmationDialogUI が未設定の場合は自動確認
                if (handUI != null) handUI.SetSubmittedState(true);
                SendActionToServer("select_confirm", new ActionPayload { hand_indexes = data.hand_indexes, hand = _pendingHandTiles });
            }
        }

        private void HandleIsTenpaiReceived(KillingMahjong.EngineData.IsTenpaiData data)
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;

            string message = "【予想役・点数】\n";
            bool hasMangan = false;
            
            if (data.waits != null && data.waits.Length > 0)
            {
                message += "待ち牌:\n\n\n\n";
                foreach (var wait in data.waits)
                {
                    string yakuText = (wait.yaku != null && wait.yaku.Length > 0) ? string.Join(" / ", wait.yaku) : "役なし";
                    string manganText = wait.mangan_or_more ? "満貫以上" : "満貫未満";
                    message += $"-> {yakuText} ({manganText})\n";
                    if (wait.mangan_or_more) hasMangan = true;
                }
            }
            message += "\nこの手牌で決定しますか？";

            if (waitUI != null) waitUI.MoveToCenter();

            if (confirmationDialogUI != null)
            {
                confirmationDialogUI.ShowDialog(
                    message,
                    () => {
                        _autoConfirmNextHandSelection = true;
                        if (handUI != null) handUI.SetSubmittedState(true);
                        if (waitUI != null) waitUI.MoveToOriginalPosition();
                        SendActionToServer("select", new ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                    },
                    () => {
                        _autoConfirmNextHandSelection = false;
                        if (handUI != null) handUI.SetSubmittedState(false);
                        BoardStateManager.Instance.ClearWaitTiles();
                        if (waitUI != null) 
                        {
                            waitUI.MoveToOriginalPosition();
                            waitUI.Hide();
                        }
                    }
                );
            }
            else
            {
                if (waitUI != null) waitUI.MoveToOriginalPosition();
                _autoConfirmNextHandSelection = true;
                if (handUI != null) handUI.SetSubmittedState(true);
                SendActionToServer("select", new ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
            }
        }

        private void HandleNotTenpaiReceived(string reason)
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;

            string message = $"ノーテン（聴牌していません）\n\nこのまま決定しますか？";
            if (confirmationDialogUI != null)
            {
                confirmationDialogUI.ShowDialog(
                    message,
                    () => {
                        _autoConfirmNextHandSelection = true;
                        if (handUI != null) handUI.SetSubmittedState(true);
                        SendActionToServer("select", new ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
                    },
                    () => {
                        _autoConfirmNextHandSelection = false;
                        if (handUI != null) handUI.SetSubmittedState(false);
                    }
                );
            }
            else
            {
                _autoConfirmNextHandSelection = true;
                if (handUI != null) handUI.SetSubmittedState(true);
                SendActionToServer("select", new ActionPayload { hand_indexes = _pendingHandIndexes, hand = _pendingHandTiles });
            }
        }

        private void HandleError(string errorMsg)
        {
            if (handUI != null) handUI.SetSubmittedState(false);
        }

        public void CancelHandSelection()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (isTransitioning) return; // 演出中はキャンセル不可

            if (handUI != null) handUI.SetSubmittedState(false);
            if (waitUI != null) waitUI.gameObject.SetActive(false);
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
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

            int wallIndex = BoardStateManager.Instance.FindAvailableWallIndex(tileToDiscard);
            if (wallIndex < 0) wallIndex = tileToDiscard; // フォールバック

            BoardStateManager.Instance.MarkWallIndexAsDiscarded(wallIndex);
            SendActionToServer("discard", new ActionPayload { wall_index = wallIndex, tile = tileToDiscard });
            ClearSelection();
        }

        // 手牌決定フローで使用するキャッシュ
        private List<int> _pendingHandIndexes;
        private List<int> _pendingHandTiles;

        public void CompleteHandSelection()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (dialogueUI != null && dialogueUI.IsLogOpen) return;
            
            // 二重押し防止
            if (handUI != null) handUI.SetSubmittedState(true);

            // hand_indexes を事前計算してキャッシュ
            _pendingHandIndexes = new List<int>();
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
                     _pendingHandIndexes.Add(idx);
                     usedIndexes.Add(idx);
                 }
            }
            _pendingHandTiles = new List<int>(BoardStateManager.Instance.CurrentHandTiles);

            // まずサーバーに is_tenpai を送信して予想点数情報を取得する
            // ユーザーがOKを押してから select を送信する
            SendActionToServer("is_tenpai", new ActionPayload { wall_indexes = _pendingHandIndexes });
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
            
            bool isGameEndPhase = currentPhaseStatus == RoundStatus.Agari || 
                                  currentPhaseStatus == RoundStatus.Ron || 
                                  currentPhaseStatus == RoundStatus.Result || 
                                  currentPhaseStatus == RoundStatus.Draw;
            if (isGameEndPhase) return;

            var board = BoardStateManager.Instance;

            // 1. HandUI / WallUI を一括クリア
            if (handUI != null)
            {
                for (int i = handUI.GetHandSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = handUI.GetHandSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                handUI.GetHandSlots().Clear();
            }
            if (wallUI != null)
            {
                for (int i = wallUI.GetWallSlots().Count - 1; i >= 0; i--)
                {
                    Transform t = wallUI.GetWallSlots()[i];
                    if (t != null) Destroy(t.gameObject);
                }
                wallUI.GetWallSlots().Clear();
            }

            // 2. 壁牌＋手牌を合算してレイアウトし、全牌に OriginalWallPosition を設定する
            //    これにより「手牌として生成された牌」も正しい壁座標を持ち、
            //    壁に戻す際に元の位置へ戻れるようになる
            if (wallUI != null)
            {
                // 壁+手牌の合算IDリスト・RectTransformリストを作成
                List<int> combinedIds = new List<int>(board.CurrentWallTiles);
                combinedIds.AddRange(board.CurrentHandTiles);

                List<RectTransform> combinedGenerated = new List<RectTransform>();
                foreach (var id in combinedIds)
                {
                    GameObject obj = Instantiate(tilePrefab, transform);
                    RectTransform rt = obj.GetComponent<RectTransform>() ?? obj.transform as RectTransform;
                    if (rt != null)
                    {
                        InitializeTileComponent(rt, id, false);
                        combinedGenerated.Add(rt);
                    }
                }

                // 合算リストをレイアウト → 全牌の OriginalWallPosition が設定される
                wallUI.LayoutWallTiles(combinedGenerated, combinedIds, board.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);

                // 手牌IDの牌を wallSlots から取り出して handUI へ移動する
                // (OriginalWallPosition は保持されたまま)
                if (handUI != null)
                {
                    foreach (var id in board.CurrentHandTiles)
                    {
                        RectTransform rt = wallUI.GrabTileById(id);
                        if (rt != null)
                        {
                            InitializeTileComponent(rt, id, true);
                            handUI.AddTileToHand(rt, id);
                        }
                    }
                }
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
                        int visualId = -1; // Force hidden (-1)
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
                        int visualId = -1; // Force hidden (-1)
                        InitializeTileComponent(rt, visualId, false);
                        enemyWallGenerated.Add(rt);
                    }
                }
                enemyWallUI.LayoutEnemyWallTiles(enemyWallGenerated, board.CurrentEnemyWallTiles, currentPhaseStatus == RoundStatus.Discard);
            }

            if (waitUI != null && (currentPhaseStatus == RoundStatus.Discard || currentPhaseStatus == RoundStatus.HandSelection))
            {
                if (board.CurrentWaitTiles != null && board.CurrentWaitTiles.Count > 0)
                {
                    waitUI.gameObject.SetActive(true);
                    waitUI.DisplayWaits(board.CurrentWaitTiles);
                }
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
            if (handUI == null || wallUI == null) return;

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
                // OriginalWallPosition は RebuildAllTilesFromState で全牌に設定済みのため
                // ReturnTileToWall で元の位置へ正しく戻せる（再ソートなし）
                wallUI.ReturnTileToWall(movedTile, tileId);
            }
        }

        public void HandleDiscardEvent(int discardedTileId, bool isLocalPlayer)
        {
            // ロン演出用に最後の打牌を記録（フェーズチェック前に実行して確実に記録する）
            BoardStateManager.Instance.LastDiscardedTileId = discardedTileId;

            // ゲーム終了フェーズ中は打牌イベントを無視（盤面を動かさない）
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

            if (newStatus == RoundStatus.HandSelection && handUI != null)
            {
                handUI.SetSubmittedState(false);
            }

            bool isGameEndPhase = newStatus == RoundStatus.Agari || 
                                  newStatus == RoundStatus.Ron || 
                                  newStatus == RoundStatus.Result || 
                                  newStatus == RoundStatus.Draw;

            if (!isGameEndPhase)
            {
                if (handUI != null) handUI.UpdateLayout(currentPhaseStatus);

                if (wallUI != null)
                {
                    List<RectTransform> remainingTiles = new List<RectTransform>();
                    foreach (var st in wallUI.GetWallSlots()) if (st != null) remainingTiles.Add(st);
                    wallUI.LayoutWallTiles(remainingTiles, BoardStateManager.Instance.CurrentWallTiles, BoardStateManager.Instance.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
                }
            }
            
            HandlePhaseVisibility(newStatus);
        }

        private void HandlePhaseVisibility(RoundStatus status)
        {
            if (isTransitioning) return;

            bool showBoardElements = status == RoundStatus.Discard || 
                                     status == RoundStatus.Agari || 
                                     status == RoundStatus.Ron || 
                                     status == RoundStatus.Result || 
                                     status == RoundStatus.Draw;

            bool isGameEndPhase = status == RoundStatus.Agari || 
                                  status == RoundStatus.Ron || 
                                  status == RoundStatus.Result || 
                                  status == RoundStatus.Draw;

            if (riverUI != null) riverUI.gameObject.SetActive(showBoardElements);
            if (enemyRiverUI != null) enemyRiverUI.gameObject.SetActive(showBoardElements);
            if (enemyHandUI != null)
            {
                // ゲーム終了フェーズでは LayoutGroup を無効化して牌の位置を固定する
                if (isGameEndPhase)
                {
                    var layoutGroup = enemyHandUI.GetComponentInChildren<UnityEngine.UI.LayoutGroup>();
                    if (layoutGroup != null) layoutGroup.enabled = false;
                }
                enemyHandUI.gameObject.SetActive(showBoardElements);
            }
            if (enemyWallUI != null)
            {
                if (isGameEndPhase)
                {
                    var layoutGroup = enemyWallUI.GetComponentInChildren<UnityEngine.UI.LayoutGroup>();
                    if (layoutGroup != null) layoutGroup.enabled = false;
                }
                enemyWallUI.gameObject.SetActive(showBoardElements);
            }

            switch (status)
            {
                case RoundStatus.Betting:
                    SetMatchUIVisibility(false); 
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(false);
                    if (waitUI != null) waitUI.gameObject.SetActive(false);
                    StartBettingPhase(Managers.BoardStateManager.Instance.LocalPlayerHp);
                    break;
                case RoundStatus.Dealing:
                    ClearAllTiles();
                    SetMatchUIVisibility(false);
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (waitUI != null) waitUI.gameObject.SetActive(false);
                    if (abilityUI != null) abilityUI.gameObject.SetActive(false);
                    break;
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
                    if (waitUI != null) waitUI.gameObject.SetActive(false);
                    if (abilityUI != null) abilityUI.gameObject.SetActive(false);
                    if (ronAnimationUI != null)
                    {
                        bool isLocalWin = BoardStateManager.Instance.LastIsLocalWin;
                        List<int> winningHand = isLocalWin ? new List<int>(BoardStateManager.Instance.CurrentHandTiles) : new List<int>(BoardStateManager.Instance.CurrentEnemyHandTiles);
                        
                        var liq = BoardStateManager.Instance.LastLiquidationData;
                        
                        List<string> actualYaku = new List<string>();
                        string actualFormula = "0飜";
                        string actualRank = "満貫";
                        
                        if (liq != null)
                        {
                            if (liq.yaku != null)
                            {
                                actualYaku = new List<string>(liq.yaku);
                            }
                            else
                            {
                                actualYaku.Add("不明な役");
                            }
                            
                            actualFormula = $"{liq.han}飜";
                            
                            if (liq.multiplier >= 4.0f) actualRank = "役満";
                            else if (liq.multiplier >= 3.0f) actualRank = "三倍満";
                            else if (liq.multiplier >= 2.0f) actualRank = "倍満";
                            else if (liq.multiplier >= 1.5f) actualRank = "跳満";
                            else actualRank = "満貫";
                        }
                        
                        int ronTile = BoardStateManager.Instance.LastDiscardedTileId >= 0
                            ? BoardStateManager.Instance.LastDiscardedTileId
                            : (winningHand.Count > 0 ? winningHand[winningHand.Count - 1] : 0);
                        
                        StartCoroutine(PlayRonWithPreDialogue(isLocalWin, winningHand, ronTile, actualYaku, actualFormula, actualRank));
                    }
                    break;
                case RoundStatus.Draw:
                    // 流局演出
                    if (waitUI != null) waitUI.gameObject.SetActive(false);
                    if (abilityUI != null) abilityUI.gameObject.SetActive(false);
                    // SetMatchUIVisibility(false) などの牌を隠す処理を削除（演出中も盤面を表示したままにするため）
                    
                    if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
                    if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);
                    
                    if (dialogueUI != null)
                    {
                        dialogueUI.gameObject.SetActive(true);
                        dialogueUI.ShowText("流局…次の対局へ");
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
            _autoConfirmNextHandSelection = false;
            // 満貫以上の場合はサーバーが直接 hand_selection_accepted を返すため、
            // 確認ダイアログを飛ばして「手牌決定！」演出を再生する
            if (phaseTransitionUI != null)
            {
                isTransitioning = true;
                phaseTransitionUI.PlayCenterTextAnim("手牌決定！", 2.0f, () =>
                {
                    isTransitioning = false;
                    // キャンセルされていなければ待機テキストを出す
                    if (handUI != null && handUI.IsSubmitted) 
                    {
                        if (dialogueUI != null) dialogueUI.ShowText("相手の手牌選択を待っています...");
                    }
                    HandlePhaseVisibility(currentPhaseStatus);
                    if (handUI != null) handUI.UpdateLayout(currentPhaseStatus);
                });
            }
            else
            {
                if (dialogueUI != null) dialogueUI.ShowText("相手の手牌選択を待っています...");
            }
        }

        private void HandleAgari(bool isLocalWin)
        {
            // UpdatePhaseStatus is handled via Network message event routing
        }

        private void HandleDraw()
        {
            Debug.Log("[GameUIManager] 流局処理開始");
            StartCoroutine(DrawSequence());
        }

        private IEnumerator DrawSequence()
        {
            // 流局メッセージを3秒間表示
            yield return new WaitForSeconds(3.0f);

            // 流局表示を消してUIをクリア（次ラウンドの配牌待ちに備える）
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
            
            // キューに残っているリアクションをクリア
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.Setup(dialogueUI, enemyInfoUI, playerInfoUI);
            }

            Debug.Log("[GameUIManager] 流局演出完了 - 次ラウンド待ち承認送信");
            NetworkMessageHandler.Instance.SendActionToServer("next_round", new ActionPayload());
        }

        private IEnumerator PlayRonWithPreDialogue(bool isLocalWin, List<int> winningHand, int ronTile, List<string> yaku, string formula, string rank)
        {
            // 打牌時の不要なリアクション（セリフ）をキャンセルしてロン演出を即座に最優先にする
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.ClearReactions();
            }

            if (playerInfoUI != null) playerInfoUI.gameObject.SetActive(true);
            if (enemyInfoUI != null) enemyInfoUI.SetPanelVisible(true);

            // 吹き出しを表示するためにCanvas(RonAnimationUI)を先にONにする
            // 黒背景のロン演出(ronPanel)は、後でPlayRonSequenceが呼ばれた時にONになる
            if (ronAnimationUI != null)
            {
                ronAnimationUI.PrepareForPreDialogue(); // 前回の演出が残らないように黒背景を消し、吹き出しも初期化する
                ronAnimationUI.gameObject.SetActive(true);
                ronAnimationUI.ShowPlayerRonBubble(isLocalWin); // 自分がロンした時だけ吹き出しを表示
            }

            bool useBubble = isLocalWin && ronAnimationUI != null && ronAnimationUI.HasPlayerRonBubble();

            if (useBubble)
            {
                // 吹き出しが表示されているので従来のダイアログは出さない
            }
            else if (isLocalWin)
            {
                if (dialogueUI != null)
                {
                    dialogueUI.gameObject.SetActive(true);
                    dialogueUI.ShowText("「ロン！」");
                }
            }
            else
            {
                // 敵がロンした場合は相手のダイアログを出す
                if (dialogueUI != null)
                {
                    dialogueUI.gameObject.SetActive(true);
                    dialogueUI.ShowText("「ロンよ！」");
                }
            }

            if (isLocalWin && playerInfoUI != null) playerInfoUI.PlayBounceAnimation(1.5f);
            if (!isLocalWin && enemyInfoUI != null) enemyInfoUI.PlayBounceAnimation(1.5f);

            yield return new WaitForSeconds(1.5f);

            if (ronAnimationUI != null) ronAnimationUI.ShowPlayerRonBubble(false);
            if (dialogueUI != null) dialogueUI.gameObject.SetActive(false);
            if (abilityUI != null) abilityUI.gameObject.SetActive(false);

            if (ronAnimationUI != null)
            {
                ronAnimationUI.PlayRonSequence(winningHand, ronTile, yaku, formula, rank, isLocalWin, () => OnRonAnimationComplete(isLocalWin));
            }
        }

        private void OnRonAnimationComplete(bool isLocalWin)
        {
            // ロン演出完了時に最新のHP（お金）情報をUIに反映する
            if (playerInfoUI != null) 
                playerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
            if (enemyInfoUI != null) 
                enemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);

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

            // 余韻を残すために3秒待ってから次局へ進行する
            StartCoroutine(WaitAndSendNextRound(3.0f));
        }

        private IEnumerator WaitAndSendNextRound(float delay)
        {
            yield return new WaitForSeconds(delay);
            Debug.Log("[GameUIManager] ロン演出完了 - 次ラウンド進行用の承認を送信");
            NetworkMessageHandler.Instance.SendActionToServer("next_round", new ActionPayload());
        }
    }
}
