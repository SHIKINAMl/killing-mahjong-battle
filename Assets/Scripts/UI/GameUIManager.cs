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
                // 獲得ポイントのゲージは0でも出す。ここで触っておかないと
                // 初回の獲得までゲージ自体が作られず、対局開始時に何も見えない。
                ScoreGauge.ResetScores();

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

            // 2つの河を同じ中心線に乗せる。**シーンではなくここから当てる**
            // （対局シーンが2つあり、河も自分・相手で2つずつあるため）。
            // 詳しくは RiverUI.AlignToOpponentRiver のコメント。
            if (enemyRiverUI != null && riverUI != null) enemyRiverUI.AlignToOpponentRiver(riverUI);

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

        /// <summary>直近の game_end。勝敗を決めるときに決着理由を見るため保持する。</summary>
        private GameEndInfo lastGameEndInfo;

        private void HandleGameEnded(GameEndInfo info)
        {
            IsGameOver = true;
            lastGameEndInfo = info;

            var board = BoardStateManager.Instance;

            // final_scores に自分（相手）の ID が無かった場合、スコアは 0 のまま届く。
            // **その 0 をそのまま勝敗に使うと、勝った側にも敗北画面が出る。**
            // 最後に status で届いた HP の方がまだ実態に近いので、そちらで補う。
            int localScore = info.LocalScoreFound ? info.LocalScore : (board != null ? board.LocalPlayerHp : 0);
            int enemyScore = info.EnemyScoreFound ? info.EnemyScore : (board != null ? board.EnemyPlayerHp : 0);

            LocalFinalScore = localScore;
            EnemyFinalScore = enemyScore;
            
            // 決着時の最終HPも記録しておく
            RecordHpHistory(localScore, enemyScore);
        }

        /// <summary>
        /// 自分が勝ったのかを決める。**サーバーは勝者 ID を送ってこない**ので、
        /// 決着理由（`victory_method`）と手元の値から組み立てるしかない（2026-08-23）。
        ///
        /// 以前は `LocalFinalScore > 0 && EnemyFinalScore <= 0` だった。
        /// **HP は 0 でクランプされ、しかも決着の大半は「最低賭け金を払えない」（HP は 0 より上）**
        /// なので、この条件はほぼ成立せず、**勝者の画面にも「敗北」が出ていた**。
        /// 起票済みの A-9（`winner_id` を送ってほしい）が入れば、ここは読み替えるだけで済む。
        /// </summary>
        private bool DetermineLocalWin()
        {
            var board = BoardStateManager.Instance;
            string method = lastGameEndInfo != null ? lastGameEndInfo.VictoryMethod : "";

            // 累計30000到達の決着だけは、HP を見ても勝者が分からない（勝者の HP が低いこともある）。
            if (method == "cumulative_earned_points" && board != null)
            {
                bool localReached = board.LocalCumulativeEarnedPoints >= BoardStateManager.CumulativeVictoryPoints;
                bool enemyReached = board.EnemyCumulativeEarnedPoints >= BoardStateManager.CumulativeVictoryPoints;

                if (localReached != enemyReached) return localReached;

                // 最後の status が届く前に game_end が来ると、どちらも未到達に見える。
                // その場合でも直前の局で稼いだ側の方が多いはずなので、大小で決める
                if (board.LocalCumulativeEarnedPoints != board.EnemyCumulativeEarnedPoints)
                {
                    return board.LocalCumulativeEarnedPoints > board.EnemyCumulativeEarnedPoints;
                }
            }

            // hp_zero / max_rounds / unknown（最低賭け金を払えない）は、**残った血が多い方が勝ち**。
            // unknown が一番多い経路であることに注意（サーバーは払えなくなった側を負けにしている）
            if (LocalFinalScore != EnemyFinalScore) return LocalFinalScore > EnemyFinalScore;

            // HP が同点。累計獲得で割る
            if (board != null && board.LocalCumulativeEarnedPoints != board.EnemyCumulativeEarnedPoints)
            {
                return board.LocalCumulativeEarnedPoints > board.EnemyCumulativeEarnedPoints;
            }

            // 完全に同点。**引き分けの結果画面が無い**ので敗北として出す（実戦ではまず起きない）
            Debug.LogWarning($"[GameUIManager] 勝敗を決められませんでした（HP {LocalFinalScore} 対 {EnemyFinalScore} / 決着理由 '{method}'）。敗北として表示します");
            return false;
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

            bool isWin = DetermineLocalWin();
            Debug.Log($"[GameUIManager] 決着: {(isWin ? "勝ち" : "負け")} / HP 自分 {LocalFinalScore} 相手 {EnemyFinalScore} / 理由 '{(lastGameEndInfo != null ? lastGameEndInfo.VictoryMethod : "")}'");

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
            
            // 通常対局時、打牌フェイズ以外はBGMをくぐもらせる（ローパス）。
            // 開くとき・こもるときの秒数は AudioManager 側の既定に任せる
            // （開く 2.0 秒 / こもる 1.5 秒。log 補間なので端から端まで動いて聞こえる）
            if (!IsTutorialMode && KillingMahjong.Managers.AudioManager.Instance != null)
            {
                bool isMuffled = (status != RoundStatus.Discard);
                KillingMahjong.Managers.AudioManager.Instance.SetBgmFilter(isMuffled);
            }
        }

        public void SetIsTransitioning(bool value)
        {
            isTransitioning = value;
            UpdateTurnIndicatorVisibility();

            // 能力パネルと説明ツールチップは通常 20/25 で、フェーズ演出の帯(19)より手前に出る。
            // 演出のあいだだけ帯より下へ退避させる（2026-08-19 のプランナー要望 R-2）。
            if (abilityUI != null) abilityUI.SetSuppressedForTransition(value);
        }

        // --- 演出中に届いたサーバーイベントの保留 ---
        //
        // サーバーメッセージは再送されないため、演出中だからと早期 return で捨てると
        // そのイベントは永久に失われる（流局の取りこぼしで進行が止まる等）。
        // 捨てる代わりにここへ積み、演出が明けてから実行する。

        private readonly List<KeyValuePair<string, Action>> deferredActions = new List<KeyValuePair<string, Action>>();
        private bool ignoreBusyForForcedFlush = false;

        /// <summary>
        /// 保留を流す見張り。**bool ではなく Coroutine のハンドルで持つ。**
        ///
        /// 以前は `isFlushWatcherRunning` という bool で二重起動を防いでいたが、
        /// コルーチンが外から止められると true のまま取り残され、
        /// `if (!isFlushWatcherRunning)` が二度と通らなくなる。
        /// そうなると保留は永久に実行されず、8秒の強制実行という安全網ごと死ぬ
        /// （実際にロン猶予が保留されたまま対局が停止した）。
        /// ハンドルなら StopCoroutine されても null 判定と併せて張り直せる。
        /// </summary>
        private Coroutine flushWatcher;

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

            EnsureFlushWatcher();
        }

        /// <summary>
        /// 見張りが動いていなければ張り直す。保留がある限り、何度呼んでも安全。
        /// </summary>
        private void EnsureFlushWatcher()
        {
            if (flushWatcher != null) return;
            if (!isActiveAndEnabled) return;
            flushWatcher = StartCoroutine(FlushDeferredActionsRoutine());
        }

        private void Update()
        {
            // コルーチンが外から止められても、保留が残っていれば必ず拾い直す。
            // これが最後の砦で、ここが無いと「進行が止まったまま何も起きない」に戻る。
            if (deferredActions.Count > 0) EnsureFlushWatcher();
        }

        private IEnumerator FlushDeferredActionsRoutine()
        {

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
            flushWatcher = null;

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
            // 打牌フェイズで、かつ演出中（先行・後攻演出など）ではない時だけ表示する
            bool shouldShow = (currentPhaseStatus == RoundStatus.Discard) && !IsTransitioning;

            if (turnIndicatorUI != null)
            {
                turnIndicatorUI.SetVisible(shouldShow);
            }

            // 文字だけでは「YOUR / ENEMY」を読むまで分からないので、
            // 手番の側の体力表示（自分＝スマホ／相手＝点滴）も光らせて位置で示す。
            // 相手の番は、あわせて女の子の立ち絵も赤く光る（EnemyInfoUI.SetTurnGlow の中）。
            // 以前あった画面ふちの枠（TurnVignette）は、盤面が狭く見えるのでやめた
            bool isLocalTurn = KillingMahjong.Managers.BoardStateManager.Instance != null
                            && KillingMahjong.Managers.BoardStateManager.Instance.IsLocalTurn;
            if (playerInfoUI != null) playerInfoUI.SetTurnGlow(shouldShow && isLocalTurn);
            if (enemyInfoUI != null) enemyInfoUI.SetTurnGlow(shouldShow && !isLocalTurn);
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

                // 牌の上を行ったり来たりしている（Tile_HoverHesitation）。
                // 自分の手番の打牌フェイズだけ数える。それ以外は普通にカーソルが通るだけ
                var watcher = KillingMahjong.Managers.PlayerActivityWatcher.Instance;
                if (watcher != null) watcher.NotifyTileHover();
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

                ShowRonWaitPanel();
            }
        }

        /// <summary>
        /// ロン待ちパネルを最前面に出す。
        /// 対局とチュートリアルで同じボタンを見せたいので、両方からここを通す。
        /// </summary>
        private void ShowRonWaitPanel()
        {
            if (RonWaitPanel == null) return;

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

        private System.Action _tutorialRonCallback;

        /// <summary>
        /// チュートリアルで、対局とまったく同じロンボタンを出して押されるのを待つ。
        /// サーバーには何も送らず、押されたら渡されたコールバックを呼ぶだけ。
        /// </summary>
        public void ShowRonWaitPanelForTutorial(System.Action onPressed)
        {
            _tutorialRonCallback = onPressed;
            ShowRonWaitPanel();
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

            // 手番の側の体力表示を光らせ直す。
            // UpdateTurnIndicatorVisibility はフェイズ・演出の切り替えでしか呼ばれないので、
            // 打牌フェイズ中の手番交代はここで拾う
            UpdateTurnIndicatorVisibility();
        }

        /// <summary>
        /// 「この牌が通った」の推理表示。無ければ実行時に作る。
        /// 判定はサーバー任せで、こちらは見えている打牌から候補を数えるだけ。
        /// </summary>
        private WaitDeductionUI _waitDeduction;
        public WaitDeductionUI WaitDeduction
        {
            get
            {
                if (_waitDeduction == null) _waitDeduction = GetComponentInChildren<WaitDeductionUI>(true);
                if (_waitDeduction == null)
                {
                    var go = new GameObject("WaitDeduction");
                    go.transform.SetParent(transform, false);
                    _waitDeduction = go.AddComponent<WaitDeductionUI>();
                }
                return _waitDeduction;
            }
        }

        /// <summary>
        /// 獲得ポイントのゲージ（左＝相手／右＝自分）。無ければ実行時に作る。
        /// 「30000で勝ち」の判定自体はサーバーの担当で、ここは積み上げを見せるだけ。
        /// </summary>
        private ScoreGaugeUI _scoreGauge;
        public ScoreGaugeUI ScoreGauge
        {
            get
            {
                if (_scoreGauge == null) _scoreGauge = GetComponentInChildren<ScoreGaugeUI>(true);
                if (_scoreGauge == null)
                {
                    var go = new GameObject("ScoreGauge");
                    go.transform.SetParent(transform, false);
                    _scoreGauge = go.AddComponent<ScoreGaugeUI>();
                }
                return _scoreGauge;
            }
        }

        public void HandleDiscardEvent(int discardedTileId, bool isLocalPlayer)
        {
            BoardStateManager.Instance.LastDiscardedTileId = discardedTileId;

            // 通った牌・相手が切った牌のどちらも「相手の待ちではない」情報になる。
            // ロン成立時は局が終わって次局でリセットされるので、ここで弾く必要はない。
            if (!IsTutorialMode) WaitDeduction.RegisterDiscard(discardedTileId, isLocalPlayer);

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

            // 手番が来てから切るまでの速さを見る（Tile_InstantDiscard）。
            // CheckDiscardConditions より先に伝えて、判定に間に合わせる
            if (isLocalPlayer)
            {
                var watcher = KillingMahjong.Managers.PlayerActivityWatcher.Instance;
                if (watcher != null) watcher.NotifyLocalDiscard();
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

            // チュートリアルはサーバーに繋がっていないので、進行役へ返すだけ
            if (_tutorialRonCallback != null)
            {
                var cb = _tutorialRonCallback;
                _tutorialRonCallback = null;
                if (RonWaitPanel != null) RonWaitPanel.SetActive(false);
                cb();
                return;
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
