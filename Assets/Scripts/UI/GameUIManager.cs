using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class GameUIManager : MonoBehaviour
    {
        [Header("Phase UI Containers")]
        [SerializeField] private GameObject handSelectionPhaseContainer;
        [SerializeField] private GameObject discardPhaseContainer;
        [SerializeField] private GameObject gameEndContainer;

        [SerializeField] private AgariSelectionUI agariSelectionUI;

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
        [SerializeField, Tooltip("場に出ている血（賭け金プール）の表示。未設定でも動作する")] private BetPotUI betPotUI;
        [SerializeField] private PhaseTransitionUI phaseTransitionUI;
        [SerializeField] private ConfirmationDialogUI confirmationDialogUI;
        [SerializeField] private RonAnimationUI ronAnimationUI;
        [SerializeField] private VictoryUI victoryUI;
        [SerializeField] private MatchmakingUI matchmakingUI;
        [SerializeField] private DoraDisplayUI doraDisplayUI;
        [SerializeField] private TurnIndicatorUI turnIndicatorUI;
        [SerializeField] private GameObject ronWaitPanel;
        [SerializeField] private OptionUI optionUI;

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

        public bool IsTutorialMode { get; set; } = false;
        public Managers.TutorialManager TutorialManager { get; set; }

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

            // チュートリアルモードでなければWebSocketに自動接続する
            if (!IsTutorialMode)
            {
                bool isDebugMode = false;
                if (NetworkMessageHandler.Instance != null && NetworkMessageHandler.Instance.UseDebugClient)
                {
                    isDebugMode = true;
                }

                if (!isDebugMode)
                {
                    var wsClient = UnityEngine.Object.FindFirstObjectByType<WebSocketGameClientSample>();
                    if (wsClient != null)
                    {
                        // UIの初期化が完全に終わった次のフレームで接続を開始する
                        StartCoroutine(ConnectWebSocketNextFrame(wsClient));
                    }
                }
            }
        }

        private System.Collections.IEnumerator ConnectWebSocketNextFrame(WebSocketGameClientSample wsClient)
        {
            yield return null; // 1フレーム待機
            if (wsClient != null)
            {
                _ = wsClient.ConnectAsync();
            }
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
            if (waitUI != null && enemyWaitUI != null)
            {
                enemyWaitUI.gameObject.SetActive(false);
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
            
            if (agariSelectionUI != null)
            {
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

            UpdateTurnIndicatorVisibility();
        }
        [SerializeField] private KillingMahjong.UI.Effects.MatchMomentumUI matchMomentumUI;

        // --- 戦況グラフ用HP履歴 ---
        private List<int> playerHpHistory = new List<int>();
        private List<int> enemyHpHistory = new List<int>();

        public void RecordHpHistory(int localHp, int enemyHp)
        {
            // 同じHPが連続する場合はスキップする（変化があった時のみ記録）
            if (playerHpHistory.Count > 0 && enemyHpHistory.Count > 0)
            {
                if (playerHpHistory[playerHpHistory.Count - 1] == localHp && 
                    enemyHpHistory[enemyHpHistory.Count - 1] == enemyHp)
                {
                    return;
                }
            }
            playerHpHistory.Add(localHp);
            enemyHpHistory.Add(enemyHp);
        }

        private void HandleGameEnded(int localScore, int enemyScore)
        {
            IsGameOver = true;
            LocalFinalScore = localScore;
            EnemyFinalScore = enemyScore;
            
            // 決着時の最終HPも記録しておく
            RecordHpHistory(localScore, enemyScore);
        }

        private bool gameResultShown = false;

        public void ShowGameResult()
        {
            // 呼び出し経路が2つ（ダイアログのOKと即時分岐）あるため、二重表示を防ぐ
            if (gameResultShown) return;
            gameResultShown = true;

            StartCoroutine(ShowGameResultRoutine());
        }

        private System.Collections.IEnumerator ShowGameResultRoutine()
        {
            // 決着したら瀕死ビネットを消す（結果画面より手前に描画されるため）
            if (playerInfoUI != null) playerInfoUI.StopHeartbeatEffect();

            // 戦況グラフの表示（履歴が2件以上あれば表示）
            if (matchMomentumUI != null && playerHpHistory.Count >= 2)
            {
                matchMomentumUI.ShowMomentum(playerHpHistory, enemyHpHistory);
                // グラフ演出が終わるまで待つ（表示時間2秒 + 前後フェード1秒 = 約3秒。MatchMomentumUI側の設定に合わせる）
                yield return new WaitForSeconds(3.0f);
            }

            bool isWin = LocalFinalScore > 0 && EnemyFinalScore <= 0;
            if (victoryUI != null)
            {
                victoryUI.PlayAnimation(
                    isWin ? VictoryType.NormalVictory : VictoryType.NormalDefeat,
                    LocalFinalScore, EnemyFinalScore);
            }
        }

        public void OpenOptionUI()
        {
            if (optionUI != null)
            {
                optionUI.Open();
            }
            else
            {
                Debug.LogWarning("[GameUIManager] OptionUI is not assigned!");
            }
        }

        // --- Component accessors ---
        public HandUI HandUI => handUI;
        public WallUI WallUI => wallUI;
        public RiverUI RiverUI => riverUI;
        public EnemyHandUI EnemyHandUI => enemyHandUI;
        public EnemyWallUI EnemyWallUI => enemyWallUI;
        public RiverUI EnemyRiverUI => enemyRiverUI;
        [SerializeField] private WaitUI enemyWaitUI;

        public WaitUI WaitUI => waitUI;
        public WaitUI EnemyWaitUI => enemyWaitUI;
        public TurnIndicatorUI TurnIndicatorUI => turnIndicatorUI;
        public PlayerInfoUI PlayerInfoUI => playerInfoUI;
        public EnemyInfoUI EnemyInfoUI => enemyInfoUI;
        public AbilityUI AbilityUI => abilityUI;
        public YakuListUI YakuListUI => yakuListUI;
        public BettingUI BettingUI => bettingUI;
        public BetPotUI BetPotUI => betPotUI;
        public DialogueUI DialogueUI => dialogueUI;
        public PhaseTransitionUI PhaseTransitionUI => phaseTransitionUI;
        public ConfirmationDialogUI ConfirmationDialogUI => confirmationDialogUI;
        public RonAnimationUI RonAnimationUI => ronAnimationUI;
        public VictoryUI VictoryUI => victoryUI;
        public MatchmakingUI MatchmakingUI => matchmakingUI;
        public DoraDisplayUI DoraDisplayUI => doraDisplayUI;
        public GameObject RonWaitPanel => ronWaitPanel;
        public OptionUI OptionUI => optionUI;
        public AgariSelectionUI AgariSelectionUI => agariSelectionUI;

        public GameObject TilePrefab => tilePrefab;
        public TileResourceManager TileResourceManager => tileResourceManager;
        public GameObject VictoryEffectPrefab => victoryEffectPrefab;
        public GameObject DamageEffectPrefab => damageEffectPrefab;

        public void SetCurrentPhaseStatus(RoundStatus status)
        {
            currentPhaseStatus = status;
            UpdateTurnIndicatorVisibility();
            
            // 通常対局時、打牌フェイズ以外はBGMをくぐもらせる（ローパス）
            if (!IsTutorialMode && KillingMahjong.Managers.AudioManager.Instance != null)
            {
                bool isMuffled = (status != RoundStatus.Discard);
                KillingMahjong.Managers.AudioManager.Instance.SetBgmFilter(isMuffled, 1.5f);
            }
        }

        public void SetIsTransitioning(bool value)
        {
            isTransitioning = value;
            UpdateTurnIndicatorVisibility();
        }

        // --- 演出中に届いたサーバーイベントの保留 ---
        //
        // サーバーメッセージは再送されないため、演出中だからと早期 return で捨てると
        // そのイベントは永久に失われる（流局の取りこぼしで進行が止まる等）。
        // 捨てる代わりにここへ積み、演出が明けてから実行する。

        private readonly List<KeyValuePair<string, Action>> deferredActions = new List<KeyValuePair<string, Action>>();
        private bool isFlushWatcherRunning = false;
        private bool ignoreBusyForForcedFlush = false;

        /// <summary>
        /// 何らかの演出が進行中で、UI を触ると壊れる状態かどうか。
        /// </summary>
        public bool IsBusyWithTransition =>
            !ignoreBusyForForcedFlush
            && (isTransitioning || (phaseTransitionUI != null && phaseTransitionUI.IsDarkenTransitioning));

        /// <summary>
        /// 演出が明けるまで処理を保留する。
        /// 同じ key の保留は後勝ちで上書きするので、連続して届いても積み上がらない。
        /// 上書きは元の位置で行う（末尾に付け直すと到着順が壊れるため）。
        /// </summary>
        public void DeferUntilIdle(string key, Action action)
        {
            if (action == null) return;

            var entry = new KeyValuePair<string, Action>(key, action);
            int existing = deferredActions.FindIndex(p => p.Key == key);
            if (existing >= 0) deferredActions[existing] = entry;
            else deferredActions.Add(entry);
            Debug.Log($"[GameUIManager] 演出中のため '{key}' を保留しました。演出完了後に実行します。");

            if (!isFlushWatcherRunning) StartCoroutine(FlushDeferredActionsRoutine());
        }

        private IEnumerator FlushDeferredActionsRoutine()
        {
            isFlushWatcherRunning = true;

            // 演出の途中で一瞬だけ isTransitioning が false に戻る箇所があるため
            // （TriggerBettingAnimationPhase の onMidpoint）、必ず1フレーム待ってから判定する。
            float waited = 0f;
            do
            {
                yield return null;
                waited += Time.deltaTime;
            }
            while (IsBusyWithTransition && waited < DeferredActionTimeoutSeconds);

            // 演出フラグが立ちっぱなしになると保留が永久に実行されず、
            // 取りこぼしと同じ「進行停止」になる。見た目の乱れより進行を優先する。
            bool forced = IsBusyWithTransition;
            if (forced)
            {
                Debug.LogWarning($"[GameUIManager] 演出が {DeferredActionTimeoutSeconds} 秒明けませんでした。保留していた処理を強制実行します。");
            }

            var toRun = new List<KeyValuePair<string, Action>>(deferredActions);
            deferredActions.Clear();
            isFlushWatcherRunning = false;

            // 強制実行のときはガードを一時的に無効化する。
            // そうしないと各処理が冒頭で再び「演出中」と判定して保留し直し、
            // 永久に実行されないまま警告だけ出し続ける。
            ignoreBusyForForcedFlush = forced;
            try
            {
                foreach (var entry in toRun)
                {
                    try
                    {
                        entry.Value?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[GameUIManager] 保留処理 '{entry.Key}' の実行に失敗: {ex.Message}\n{ex.StackTrace}");
                    }
                }
            }
            finally
            {
                ignoreBusyForForcedFlush = false;
            }
        }

        private const float DeferredActionTimeoutSeconds = 8f;

        private void UpdateTurnIndicatorVisibility()
        {
            if (turnIndicatorUI != null)
            {
                // 打牌フェイズで、かつ演出中（先行・後攻演出など）ではない時だけ表示する
                bool shouldShow = (currentPhaseStatus == RoundStatus.Discard) && !IsTransitioning;
                turnIndicatorUI.SetVisible(shouldShow);
            }
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
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) 
            {
                Debug.Log("[GameUIManager] MoveTileToHand aborted. HandUI is already submitted.");
                return;
            }
            
            if (IsTutorialMode && TutorialManager != null)
            {
                if (!TutorialManager.OnTryMoveTile(tileId, toHand: true)) return;
            }
            
            Debug.Log($"[GameUIManager] Executing BoardStateManager.MoveTileToHand({tileId})");
            BoardStateManager.Instance.TargetHandIndexes = null;

            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayPickTileSE();

            if (VisualController != null)
            {
                StartCoroutine(VisualController.PlayTransitionAnimationRoutine(() => 
                {
                    BoardStateManager.Instance.MoveTileToHand(tileId);
                    ClearSelection();
                }));
            }
            else
            {
                BoardStateManager.Instance.MoveTileToHand(tileId);
                ClearSelection();
            }
        }

        public void MoveTileToWall(int tileId)
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;
            
            if (IsTutorialMode && TutorialManager != null)
            {
                if (!TutorialManager.OnTryMoveTile(tileId, toHand: false)) return;
            }
            
            BoardStateManager.Instance.TargetHandIndexes = null;
            
            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayPickTileSE();

            if (VisualController != null)
            {
                StartCoroutine(VisualController.PlayTransitionAnimationRoutine(() => 
                {
                    BoardStateManager.Instance.MoveTileToWall(tileId);
                    ClearSelection();
                }));
            }
            else
            {
                BoardStateManager.Instance.MoveTileToWall(tileId);
                ClearSelection();
            }
        }

        public void SelectManganHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;
            
            if (IsTutorialMode && TutorialManager != null)
            {
                TutorialManager.ApplyMockAutoMangan();
                return;
            }
            
            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayPickTileSE();

            if (VisualController != null)
            {
                StartCoroutine(VisualController.PlayTransitionAnimationRoutine(() => 
                {
                    BoardStateManager.Instance.SelectManganHand();
                }));
            }
            else
            {
                BoardStateManager.Instance.SelectManganHand();
            }
        }

        public void SelectRandomHand()
        {
            if (currentPhaseStatus != RoundStatus.HandSelection) return;
            if (handUI != null && handUI.IsSubmitted) return;
            
            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayPickTileSE();

            if (VisualController != null)
            {
                StartCoroutine(VisualController.PlayTransitionAnimationRoutine(() => 
                {
                    BoardStateManager.Instance.SelectRandomHand();
                }));
            }
            else
            {
                BoardStateManager.Instance.SelectRandomHand();
            }
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

            if (IsTutorialMode && TutorialManager != null)
            {
                bool allowDiscard = TutorialManager.OnTryDiscardTile(tileToDiscard);
                if (!allowDiscard) 
                {
                    ClearSelection();
                    return; // 指定牌以外は打てない
                }

                if (playerInfoUI != null) playerInfoUI.StopTurnTimer();
                BoardStateManager.Instance.SetLocalTurn(false);
                ClearSelection();

                // 疑似的に河へ移動
                if (wallUI != null)
                {
                    RectTransform tileRt = wallUI.GrabTileById(tileToDiscard);
                    if (tileRt != null)
                    {
                        if (riverUI != null) riverUI.AddExistingTile(tileRt, tileToDiscard);
                    }
                    else
                    {
                        if (riverUI != null) riverUI.AddTile(tileToDiscard);
                    }
                    wallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
                }

                if (KillingMahjong.Managers.AudioManager.Instance != null)
                    KillingMahjong.Managers.AudioManager.Instance.PlayDiscardSE(KillingMahjong.Managers.AudioManager.Instance.discardSE);
                
                return;
            }

            if (playerInfoUI != null) playerInfoUI.StopTurnTimer();

            BoardStateManager.Instance.SetLocalTurn(false);

            int wallIndex = BoardStateManager.Instance.FindAvailableWallIndex(tileToDiscard);
            if (wallIndex < 0) wallIndex = tileToDiscard;

            BoardStateManager.Instance.MarkWallIndexAsDiscarded(wallIndex);
            SendActionToServer("discard", new ActionPayload { wall_index = wallIndex, tile = tileToDiscard });
            ClearSelection();
        }

        public void CompleteHandSelection()
        {
            if (IsTutorialMode && TutorialManager != null)
            {
                if (!TutorialManager.OnTryCompleteHandSelection()) return;
            }
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
            Debug.Log($"[GameUIManager] HandleAgariPendingReceived called. winner_id: {data.winner_id}, loser_id: {data.loser_id}, tile: {data.tile}");

            if (data.winner_id == NetworkMessageHandler.Instance.LocalPlayerId)
            {
                // 保留するかどうかに関わらず、自動打牌だけは先に止める。
                // AutoDiscardController は RonWaitPanel の表示有無でロン猶予を判定しているので、
                // パネルを出す前に保留すると、その隙に自動で打ってロンを取り逃す。
                var autoDiscard = GetComponent<AutoDiscardController>();
                if (autoDiscard != null) autoDiscard.CancelAutoDiscard();

                // 賭け金演出などの最中にロン猶予が届くと、演出を突き抜けてロンボタンだけが先に出る。
                // サーバーはロン入力を待ち続ける（手番のタイムアウトは無い）ので、
                // 演出が明けてから出しても取りこぼしにはならない。
                if (IsBusyWithTransition)
                {
                    DeferUntilIdle("agariPending", () => HandleAgariPendingReceived(data));
                    return;
                }

                if (BoardStateManager.Instance.NonManganWaitTiles.Contains(data.tile))
                {
                    Debug.Log($"[GameUIManager] Ignored agari_pending because tile {data.tile} is non-mangan.");
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
                    canvas.sortingOrder = UISortingOrders.RonWaitPanel;

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
            if (IsTransitioning) return; // アニメーション演出中は矢印を消さない
            if (wallUI != null)
            {
                wallUI.UpdateDiscardTurnIndicator(isLocalTurn, currentPhaseStatus == RoundStatus.Discard);
            }

            // 打牌フェイズ中にターンが変わった場合、タイマーをリセット・開始/停止する
            if (currentPhaseStatus == RoundStatus.Discard && playerInfoUI != null)
            {
                if (isLocalTurn)
                {
                    playerInfoUI.StartTurnTimer(10f); // 10秒でリセットして開始
                }
                else
                {
                    playerInfoUI.StopTurnTimer();
                }
            }
        }

        public void HandleDiscardEvent(int discardedTileId, bool isLocalPlayer)
        {
            BoardStateManager.Instance.LastDiscardedTileId = discardedTileId;

            if (KillingMahjong.Managers.AudioManager.Instance != null)
                KillingMahjong.Managers.AudioManager.Instance.PlayDiscardSE(KillingMahjong.Managers.AudioManager.Instance.discardSE);

            bool isGameEndPhase = currentPhaseStatus == RoundStatus.Agari || 
                                  currentPhaseStatus == RoundStatus.Ron || 
                                  currentPhaseStatus == RoundStatus.Result || 
                                  currentPhaseStatus == RoundStatus.Draw;
            if (isGameEndPhase) return;

            if (playerInfoUI != null) playerInfoUI.SetDiscardingState(false);
            if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);

            if (isLocalPlayer)
            {
                // 打牌した瞬間に自分のターンは終了したとみなしてUIを更新する
                BoardStateManager.Instance.SetLocalTurn(false);
                BoardStateManager.Instance.RemoveTileFromWall(discardedTileId);

                if (wallUI != null && riverUI != null)
                {
                    RectTransform tileRt = wallUI.GrabTile(discardedTileId);
                    if (tileRt != null)
                    {
                        riverUI.AddExistingTile(tileRt, discardedTileId);
                    }
                    else
                    {
                        riverUI.AddTile(discardedTileId); // fallback
                    }
                    
                    wallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, currentPhaseStatus == RoundStatus.Discard);
                }
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
                    else
                    {
                        enemyRiverUI.AddTile(discardedTileId); // fallback
                    }
                }
            }

            ReactionController.Instance.CheckDiscardConditions(discardedTileId, isLocalPlayer);
        }

        public void ClearAllTiles()
        {
            if (handUI != null)
            {
                foreach (RectTransform t in handUI.GetHandSlots().ToArray()) if (t != null) VisualController.ReturnTileToPool(t.gameObject);
                handUI.GetHandSlots().Clear();
            }
            if (wallUI != null)
            {
                foreach (Transform t in wallUI.GetWallSlots().ToArray()) if (t != null) VisualController.ReturnTileToPool(t.gameObject);
                wallUI.GetWallSlots().Clear();
            }
            if (enemyHandUI != null)
            {
                // ClearHand() はリストをクリアするだけなので、先にGameObjectをプールに返却する。
                // （返却しないと前局の敵手牌が孤児化して画面に残り続ける）
                foreach (RectTransform t in enemyHandUI.GetHandSlots().ToArray()) if (t != null) VisualController.ReturnTileToPool(t.gameObject);
                enemyHandUI.ClearHand();
            }
            if (enemyWallUI != null)
            {
                foreach (Transform t in enemyWallUI.GetEnemyWallSlots().ToArray()) if (t != null) VisualController.ReturnTileToPool(t.gameObject);
                enemyWallUI.GetEnemyWallSlots().Clear();
            }
            if (riverUI != null) riverUI.Clear();
            if (enemyRiverUI != null) enemyRiverUI.Clear();
            
            if (waitUI != null) waitUI.gameObject.SetActive(false);

            // 牌をすべてプールへ返したので、差分リビルドの前提（UIに前回の牌が残っている）が崩れる。
            // 無効化しておかないと、返却前と牌の構成が偶然一致したときに
            // 「変化なし＝再生成不要」と誤判定され、盤面が空のままになる。
            if (VisualController != null) VisualController.InvalidateRebuildCache();

            Managers.BoardStateManager.Instance.ClearAllBoardData();
        }

        // Methods invoked via Unity Events (e.g. Inspector Buttons)
        public void ExecuteRonAction()
        {
            Debug.Log($"[GameUIManager] ExecuteRonAction called. _isAgariPending={_isAgariPending}");
            
            if (KillingMahjong.Managers.AudioManager.Instance != null)
            {
                KillingMahjong.Managers.AudioManager.Instance.PlayVoice(KillingMahjong.Managers.AudioManager.Instance.ronVoice);
            }

            if (_isAgariPending)
            {
                _isAgariPending = false;
                if (RonWaitPanel != null) RonWaitPanel.SetActive(false);
                SendActionToServer("agari", new KillingMahjong.Network.ActionPayload { accept = true });
                Debug.Log("[GameUIManager] Sent 'agari' action to server. Waiting for server response to play animation.");
                return; // サーバーからの確定（役のデータ等）を待ってからアニメーションを再生するため、ここでは抜ける
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
            HandUI?.UpdateLayout(currentPhaseStatus);
        }

        public void StartMulliganSelection()
        {
            SkillController?.StartMulliganSelection();
            HandUI?.UpdateLayout(currentPhaseStatus);
        }

        public void OnMulliganTileSelected(int tileId, RectTransform slotRt)
        {
            SkillController?.OnMulliganTileSelected(tileId, slotRt);
            HandUI?.UpdateLayout(currentPhaseStatus);
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
