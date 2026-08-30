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
    public partial class GameUIManager : MonoBehaviour
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

        // HP履歴・勝敗判定・結果画面 → GameUIManager.GameResult.cs

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

        // フェイズ状態・BGM・演出中の保留キュー → GameUIManager.Transitions.cs
        // 牌の選択・移動・打牌などの盤面操作 → GameUIManager.TileActions.cs
        // サーバーイベントの受け口・ロン待ちパネル → GameUIManager.ServerEvents.cs
        // 各コントローラへの単純な委譲 → GameUIManager.SkillBridge.cs
        //
        // 死んだコードとして削除 (2026-08-29): IsPointerInHandArea / SlideUIRoutine
        // （呼び出し元ゼロ・m_MethodName grep もゼロ。AGENTS.md §7 の3点確認済み）
    }
}
