using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUIPhaseController : MonoBehaviour
    {
        private GameUIManager uiManager;
        
        private bool _hasShownHandSelectionPrompt = false;
        private bool _hasSentNextRoundForCurrentPhase = false;
        private int _currentRoundIndex = 1;
        private bool _waitingForOpponentRonAnimation = false;
        private bool _isCarryOverNextRound = false;
        private float _fallbackTimer = 0f;

        private void Update()
        {
            if (_waitingForOpponentRonAnimation)
            {
                _fallbackTimer += Time.deltaTime;
                if (_fallbackTimer > 5f)
                {
                    Debug.LogWarning("[GameUIPhaseController] 相手のロン進行メッセージが届きませんでした。フォールバックで強制進行します。");
                    HandleNextRoundWaitingReceived(null);
                }
            }
            else
            {
                _fallbackTimer = 0f;
            }
        }

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
        }

        public void ShowMatchmakingWaiting()
        {
            Debug.Log("[GameUIPhaseController] ShowMatchmakingWaiting called.");
            if (uiManager != null && uiManager.IsTutorialMode) return; // チュートリアル中は非表示

            if (uiManager.MatchmakingUI != null) uiManager.MatchmakingUI.ShowWaiting();
            SetMatchUIVisibility(false);
            
            if (uiManager.RiverUI != null) uiManager.RiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.gameObject.SetActive(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
            
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.RonAnimationUI != null) uiManager.RonAnimationUI.gameObject.SetActive(false);
            if (uiManager.BettingUI != null) uiManager.BettingUI.HideBettingPhase(true);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
        }

        public void ShowMatchCancelled(string reason)
        {
            uiManager.ClearAllTiles();
            BoardStateManager.Instance.ClearAllBoardData();
            uiManager.SetCurrentPhaseStatus(RoundStatus.None);

            if (uiManager.MatchmakingUI != null) uiManager.MatchmakingUI.ShowWaiting(reason);
            SetMatchUIVisibility(false);
            
            if (uiManager.RiverUI != null) uiManager.RiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.gameObject.SetActive(false);
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.gameObject.SetActive(false);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            
            if (uiManager.DialogueUI != null) 
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                uiManager.DialogueUI.ShowText(reason);
            }
            
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
            
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            if (uiManager.RonAnimationUI != null) uiManager.RonAnimationUI.gameObject.SetActive(false);
            if (uiManager.BettingUI != null) uiManager.BettingUI.HideBettingPhase(true);
            if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
        }

        public void OnGameStarted()
        {
            _currentRoundIndex = 1;
            
            try
            {
                Managers.BoardStateManager.Instance.UpdateHp(20000, 20000);
                if (uiManager.PlayerInfoUI != null) 
                {
                    uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    uiManager.PlayerInfoUI.SetHP(20000);
                }
                if (uiManager.EnemyInfoUI != null) 
                {
                    uiManager.EnemyInfoUI.SetPanelVisible(true);
                    uiManager.EnemyInfoUI.SetHP(20000);
                    uiManager.EnemyInfoUI.ShowReadyBox(false);
                }
                
                if (uiManager.PhaseTransitionUI != null)
                {
                    uiManager.PhaseTransitionUI.PlayRoundStartDarken("対局開始");
                }

                if (ReactionController.Instance != null)
                {
                    ReactionController.Instance.ResetStateForNewGame();
                    ReactionController.Instance.SetPlayerHp(20000);
                    ReactionController.Instance.SetEnemyHp(20000);
                    ReactionController.Instance.HandleRoundStart(1);
                }
                
                if (uiManager.DialogueUI != null) 
                {
                    uiManager.DialogueUI.gameObject.SetActive(true);
                    string introText = (uiManager.EnemyInfoUI != null) ? uiManager.EnemyInfoUI.PlayReaction(ReactionTrigger.GameStart) : null;
                    if (string.IsNullOrEmpty(introText)) introText = "Match Found! Game Starting...";
                    uiManager.DialogueUI.ShowText(introText);
                }
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(20000);
                
                SetMatchUIVisibility(true);
                uiManager.SetCurrentPhaseStatus(RoundStatus.None);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameUIPhaseController] OnGameStarted Error: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                // 何があっても必ずマッチング画面は閉じる
                if (uiManager.MatchmakingUI != null)
                {
                    uiManager.MatchmakingUI.Hide();
                }
            }
        }

        public void UpdatePhaseStatus(RoundStatus newStatus)
        {
            if (uiManager.CurrentPhaseStatus == newStatus) return;

            _hasSentNextRoundForCurrentPhase = false;

            uiManager.SetCurrentPhaseStatus(newStatus);
            if (PhaseManager.Instance != null) PhaseManager.Instance.ChangeRoundStatus(newStatus);

            if (newStatus == RoundStatus.HandSelection && uiManager.HandUI != null)
            {
                uiManager.HandUI.SetSubmittedState(false);
            }

            bool isGameEndPhase = newStatus == RoundStatus.Agari || 
                                  newStatus == RoundStatus.Ron || 
                                  newStatus == RoundStatus.Result || 
                                  newStatus == RoundStatus.Draw;

            if (!isGameEndPhase && !uiManager.IsTransitioning)
            {
                // コンテナ切り替えはBetting時にHandBaseUI.UpdateLayout内部でガードされる。
                // ここでは呼び出しをスキップしない（ボタン表示の更新のために必要）。
                if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);

                if (uiManager.WallUI != null)
                {
                    uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                    uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                }
            }
            
            HandlePhaseVisibility(newStatus);
        }

        public void HandlePhaseVisibility(RoundStatus status)
        {
            if (uiManager.IsTransitioning) return;

            if (status != RoundStatus.Betting && uiManager.PlayerInfoUI != null)
            {
                if (uiManager.PlayerInfoUI.gameObject.activeInHierarchy)
                {
                    uiManager.PlayerInfoUI.StartCoroutine(uiManager.PlayerInfoUI.ResetZoomRoutine(0.3f));
                }
                else
                {
                    uiManager.PlayerInfoUI.ResetZoomImmediate();
                }
            }

            if (status != RoundStatus.Betting && uiManager.BettingUI != null)
            {
                uiManager.BettingUI.HideBettingPhase(true);
            }

            bool showBoardElements = status == RoundStatus.Discard || 
                                     status == RoundStatus.Agari || 
                                     status == RoundStatus.Ron || 
                                     status == RoundStatus.Result || 
                                     status == RoundStatus.Draw;

            bool isGameEndPhase = status == RoundStatus.Agari || 
                                  status == RoundStatus.Ron || 
                                  status == RoundStatus.Result || 
                                  status == RoundStatus.Draw;

            if (uiManager.RiverUI != null) uiManager.RiverUI.gameObject.SetActive(showBoardElements);
            if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.gameObject.SetActive(showBoardElements);
            if (uiManager.EnemyHandUI != null)
            {
                if (isGameEndPhase)
                {
                    var layoutGroup = uiManager.EnemyHandUI.GetComponentInChildren<UnityEngine.UI.LayoutGroup>();
                    if (layoutGroup != null) layoutGroup.enabled = false;
                }
                uiManager.EnemyHandUI.gameObject.SetActive(showBoardElements);
            }
            if (uiManager.EnemyWallUI != null)
            {
                if (isGameEndPhase)
                {
                    var layoutGroup = uiManager.EnemyWallUI.GetComponentInChildren<UnityEngine.UI.LayoutGroup>();
                    if (layoutGroup != null) layoutGroup.enabled = false;
                    uiManager.EnemyWallUI.gameObject.SetActive(false);
                }
                else
                {
                    uiManager.EnemyWallUI.gameObject.SetActive(false);
                }
            }

            switch (status)
            {
                case RoundStatus.Betting:
                    SetMatchUIVisibility(true); 
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    if (uiManager.PlayerInfoUI != null) 
                    {
                        uiManager.PlayerInfoUI.gameObject.SetActive(true);
                        uiManager.PlayerInfoUI.StartCoroutine(uiManager.PlayerInfoUI.ZoomInRoutine(0.4f, 4.5f));
                    }
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                    StartBettingPhase(Managers.BoardStateManager.Instance.LocalPlayerHp);
                    break;
                case RoundStatus.Dealing:
                    _hasShownHandSelectionPrompt = false; // 次の局のためにフラグをリセット
                    _hasExecutedRonAnimation = false; // ロン演出の二重再生防止フラグをリセット
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(false);
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(false);

                    if (_pendingDrawTransition)
                    {
                        _pendingDrawTransition = false;
                        ExecuteDrawTransitionForDealing();
                    }
                    else
                    {
                        StartNextRoundTransitionForDealing();
                    }
                    break;
                case RoundStatus.HandSelection:
                    SetMatchUIVisibility(true);
                    if (uiManager.DialogueUI != null) uiManager.DialogueUI.SetBackgroundRaycast(false);
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    if (uiManager.WaitUI != null && Managers.BoardStateManager.Instance.CurrentWaitTiles != null && Managers.BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
                    {
                        uiManager.WaitUI.gameObject.SetActive(true);
                        uiManager.WaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentWaitTiles);
                    }
                    else if (uiManager.WaitUI != null)
                    {
                        uiManager.WaitUI.gameObject.SetActive(false);
                    }
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(true);
                    UpdateDoraDisplay();
                    if (ReactionController.Instance != null)
                    {
                        ReactionController.Instance.StartHandSelectionTimer();
                    }
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.StartTurnTimer(15f);
                    
                    // 黒幕が晴れて手牌フェイズに入った時に表示（1局につき1回のみ）
                    if (uiManager.PhaseTransitionUI != null && !_hasShownHandSelectionPrompt)
                    {
                        uiManager.PhaseTransitionUI.PlayPromptText("手牌を選んでください", 1.5f);
                        _hasShownHandSelectionPrompt = true;
                    }
                    break;
                case RoundStatus.TurnDecision:
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
                    if (uiManager.PlayerInfoUI != null)
                    {
                        uiManager.PlayerInfoUI.gameObject.SetActive(false);
                        uiManager.PlayerInfoUI.StopTurnTimer();
                    }
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    break;
                case RoundStatus.Discard:
                    if (uiManager.DialogueUI != null) uiManager.DialogueUI.SetBackgroundRaycast(true);
                    if (uiManager.HandUI != null) uiManager.HandUI.gameObject.SetActive(true);
                    if (uiManager.WallUI != null) uiManager.WallUI.gameObject.SetActive(true);
                    if (uiManager.EnemyWallUI != null) uiManager.EnemyWallUI.gameObject.SetActive(false);
                    
                    if (uiManager.RiverUI != null) uiManager.RiverUI.UpdateTurnText();
                    if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.UpdateTurnText();
                    
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    
                    if (uiManager.WaitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
                    {
                        uiManager.WaitUI.gameObject.SetActive(true);
                        uiManager.WaitUI.DisplayWaits(BoardStateManager.Instance.CurrentWaitTiles);
                    }
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                    UpdateDoraDisplay();

                    if (uiManager.PlayerInfoUI != null)
                    {
                        if (Managers.BoardStateManager.Instance.IsLocalTurn)
                        {
                            uiManager.PlayerInfoUI.StartTurnTimer(10f); // 10秒
                        }
                        else
                        {
                            uiManager.PlayerInfoUI.StopTurnTimer();
                        }
                    }
                    break;
                case RoundStatus.Agari:
                case RoundStatus.Ron:
                case RoundStatus.Result:
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                    if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
                    if (uiManager.RonAnimationUI != null)
                    {
                        bool isLocalWin = BoardStateManager.Instance.LastIsLocalWin;
                        
                        if (isLocalWin)
                        {
                            uiManager.ExecuteRonAction();
                        }
                    }
                    break;
                case RoundStatus.Draw:
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.DoraDisplayUI != null) uiManager.DoraDisplayUI.Hide();
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                    
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    
                    if (uiManager.DialogueUI != null)
                    {
                        uiManager.DialogueUI.gameObject.SetActive(true);
                        uiManager.DialogueUI.ShowText("流局…次の対局へ");
                    }
                    break;
            }
        }

        private void UpdateDoraDisplay()
        {
            if (uiManager.DoraDisplayUI != null)
            {
                int doraId = Managers.BoardStateManager.Instance.CurrentDoraId;
                if (doraId >= 0)
                {
                    uiManager.DoraDisplayUI.ShowDora(doraId);
                }
                else
                {
                    uiManager.DoraDisplayUI.Hide();
                }
            }
        }

        public void SetMatchUIVisibility(bool visible)
        {
            if (uiManager.HandUI != null) uiManager.HandUI.gameObject.SetActive(visible);
            if (uiManager.WallUI != null) uiManager.WallUI.gameObject.SetActive(visible);
            if (uiManager.EnemyWallUI != null) uiManager.EnemyWallUI.gameObject.SetActive(visible);
            if (uiManager.YakuListUI != null) 
            {
                uiManager.YakuListUI.gameObject.SetActive(visible);
                uiManager.YakuListUI.CloseYakuList();
            }
            
            if (uiManager.WaitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                if (!visible) uiManager.WaitUI.gameObject.SetActive(false);
            }
        }

        private void StartBettingPhase(int currentHealth)
        {
            if (uiManager.BettingUI != null)
            {
                int svCount = Managers.BoardStateManager.Instance.LocalPlayerSpecialVictoryCount;
                uiManager.BettingUI.ShowBettingPhase(20000, currentHealth, svCount, OnBetConfirmed);
                if (ReactionController.Instance != null)
                {
                    ReactionController.Instance.StartBetPhaseTimer();
                }
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.StartTurnTimer(10f); // 10秒
            }
        }

        private void OnBetConfirmed(int betAmount)
        {
            uiManager.BettingUI.HideBettingPhase();
            
            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.StopTurnTimer();
                if (uiManager.PlayerInfoUI.gameObject.activeInHierarchy)
                {
                    uiManager.PlayerInfoUI.StartCoroutine(uiManager.PlayerInfoUI.ResetZoomRoutine(0.3f));
                }
            }

            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.CheckAndPlayBetReaction(betAmount, Managers.BoardStateManager.Instance.LocalPlayerHp, true);
            }
            uiManager.SendActionToServer("bet", new ActionPayload { bet_amount = betAmount, amount = betAmount });
        }

        public void OnBettingCompleteFromServer(int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
            string roundTitle = $"第{_currentRoundIndex}局目";
            if (_isCarryOverNextRound) 
            {
                roundTitle += "\n自動ベット";
            }

            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.CheckAndPlayBetReaction(enemyBet, enemyHp, false);
                ReactionController.Instance.SetPlayerHp(playerHp);
                ReactionController.Instance.SetEnemyHp(enemyHp);
            }
            
            TriggerBettingAnimationPhase(roundTitle, playerBet, enemyBet, playerHp, enemyHp); 
            _isCarryOverNextRound = false;
        }

        public void TriggerBettingAnimationPhase(string roundString, int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
             if (uiManager.IsTransitioning) return;

             if (uiManager.PhaseTransitionUI != null)
             {
                 uiManager.SetIsTransitioning(true);
                 
                 // スマホは消さないようにする（ユーザー要望）
                 // if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
                 if (uiManager.PlayerInfoUI != null) 
                 {
                     uiManager.PlayerInfoUI.ResetZoomImmediate();
                     // uiManager.PlayerInfoUI.gameObject.SetActive(false);
                 }
                 if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                 if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(false);

                 uiManager.PhaseTransitionUI.PlayTransition(roundString, uiManager.PlayerInfoUI, playerBet, enemyBet, playerHp, enemyHp,
                    onMidpoint: () => {
                         uiManager.SetIsTransitioning(false);
                         
                         // UIのみクリア（BoardStateManagerのデータを消さないようにする）
                         if (uiManager.RiverUI != null) uiManager.RiverUI.Clear();
                         if (uiManager.EnemyRiverUI != null) uiManager.EnemyRiverUI.Clear();
                         if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                         
                         // 牌を再構築する前にフェーズをDiscardへ進める
                         if (uiManager.CurrentPhaseStatus == RoundStatus.Betting)
                         {
                             UpdatePhaseStatus(RoundStatus.Discard);
                         }
                         
                         if (uiManager.VisualController != null) uiManager.VisualController.RebuildAllTilesFromState();
                         
                         if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.WallUI != null)
                         {
                             uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                         }
                         
                         SetMatchUIVisibility(true); 
                         HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
                         
                         uiManager.SetIsTransitioning(true);
                         
                         if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
                         if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
                    },
                    onComplete: () => {
                         uiManager.SetIsTransitioning(false);
                         
                         // Catch any missed updates
                         if (uiManager.VisualController != null) uiManager.VisualController.RebuildAllTilesFromState();
                         
                         if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);
                         if (uiManager.WallUI != null)
                         {
                             uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                             uiManager.WallUI.UpdateDiscardTurnIndicator(BoardStateManager.Instance.IsLocalTurn, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                         }
                         
                         HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
                         if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(true);
                         if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                         if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    }
                 );
             }
        }

        private bool _pendingDrawTransition = false;

        public void HandleDraw(KillingMahjong.EngineData.DrawPlayerData[] drawData = null)
        {
            if (uiManager.IsTransitioning) return;

            if (uiManager.PhaseTransitionUI != null && uiManager.PhaseTransitionUI.IsDarkenTransitioning) return;

            // 待機中などの表示は消す
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(false);
            
            Debug.Log("[GameUIManager] 流局処理開始");
            _isCarryOverNextRound = true;
            _currentRoundIndex++;
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.SetCurrentRound(_currentRoundIndex);
                ReactionController.Instance.CheckAndPlayDrawReaction();
                ReactionController.Instance.HandleRoundStart(_currentRoundIndex);
            }

            // 自分の待ち牌表示
            if (uiManager.WaitUI != null && Managers.BoardStateManager.Instance.CurrentWaitTiles != null && Managers.BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                uiManager.WaitUI.gameObject.SetActive(true);
                uiManager.WaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentWaitTiles);
            }

            // 相手の待ち牌表示
            if (uiManager.EnemyWaitUI != null && Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles != null && Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles.Count > 0)
            {
                uiManager.EnemyWaitUI.gameObject.SetActive(true);
                uiManager.EnemyWaitUI.DisplayWaits(Managers.BoardStateManager.Instance.CurrentEnemyWaitTiles);
            }

            // 既存の手牌データソート
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentHandTiles);
            Managers.BoardStateManager.Instance.SortTileIds(Managers.BoardStateManager.Instance.CurrentEnemyHandTiles);
            
            if (uiManager.HandUI != null) uiManager.HandUI.SortHandSlots();
            if (uiManager.EnemyHandUI != null) uiManager.EnemyHandUI.SortHandSlots();

            // 相手の手牌強制公開
            if (uiManager.EnemyHandUI != null)
            {
                uiManager.EnemyHandUI.RevealAllHands(uiManager.TileResourceManager);
            }

            // このOKが出ている時点で、準備完了という文字を出してほしい
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            // ダイアログを出してOKボタンを待つ
            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                uiManager.DialogueUI.ShowText("流局しました。\nお互いの手牌と待ちを確認してください。");
                uiManager.DialogueUI.ShowNextRoundButton(() => {
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.EnemyWaitUI != null) uiManager.EnemyWaitUI.gameObject.SetActive(false);
                    
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetReadyCheck(true);
                    _pendingDrawTransition = true;
                    SendNextRoundAction();
                });
            }
            else
            {
                _pendingDrawTransition = true;
                SendNextRoundAction();
            }
        }

        private void ExecuteDrawTransitionForDealing()
        {
            if (uiManager.PhaseTransitionUI != null)
            {
                uiManager.SetIsTransitioning(true);
                uiManager.PhaseTransitionUI.PlayDrawTransition(
                    onMidpoint: () => {
                        BoardStateManager.Instance.ClearAllBoardData();
                        uiManager.ClearAllTiles();
                        SetMatchUIVisibility(false);
                        
                        if (ReactionController.Instance != null)
                        {
                            ReactionController.Instance.Setup(uiManager.DialogueUI, uiManager.EnemyInfoUI, uiManager.PlayerInfoUI);
                        }
                    },
                    onComplete: () => {
                        uiManager.SetIsTransitioning(false);
                        StartCoroutine(DealingRoutine());
                    }
                );
            }
            else
            {
                StartCoroutine(DrawSequence());
            }
        }

        private IEnumerator DrawSequence()
        {
            yield return new WaitForSeconds(3.0f);
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.Setup(uiManager.DialogueUI, uiManager.EnemyInfoUI, uiManager.PlayerInfoUI);
            }

            if (uiManager.PlayerInfoUI != null) 
            {
                uiManager.PlayerInfoUI.ShowReadyBox(true);
                uiManager.PlayerInfoUI.SetReadyCheck(true);
            }
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            Debug.Log("[GameUIManager] 流局演出完了 - 次ラウンド待ち承認自動送信");
            SendNextRoundAction();
        }

        private bool _hasExecutedRonAnimation = false;

        public void ExecuteRonAction()
        {
            if (_hasExecutedRonAnimation) return;
            _hasExecutedRonAnimation = true;

            if (uiManager.RonWaitPanel != null) uiManager.RonWaitPanel.SetActive(false);

            bool isLocalWin = true;
            List<int> winningHand = new List<int>(BoardStateManager.Instance.CurrentHandTiles);
            var liq = BoardStateManager.Instance.LastLiquidationData;
            
            List<string> actualYaku = new List<string>();
            string actualFormula = "0飜";
            string actualRank = "満貫";
            
            if (liq != null)
            {
                if (liq.yaku != null) actualYaku = new List<string>(liq.yaku);
                else actualYaku.Add("不明な役");
                
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

        public void HandleAgari(bool isLocalWin)
        {
            if (!isLocalWin)
            {
                Debug.Log("[GameUIPhaseController] 相手のロン成立。相手のロン演出を開始します。");

                List<int> winningHand = new List<int>(BoardStateManager.Instance.CurrentEnemyHandTiles);
                var liq = BoardStateManager.Instance.LastLiquidationData;
                
                List<string> actualYaku = new List<string>();
                string actualFormula = "0飜";
                string actualRank = "満貫";
                
                if (liq != null)
                {
                    if (liq.yaku != null) actualYaku = new List<string>(liq.yaku);
                    else actualYaku.Add("不明な役");
                    
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
        }

        public void HandleGameEnded()
        {
            if (_waitingForOpponentRonAnimation)
            {
                Debug.Log("[GameUIPhaseController] ゲーム終了を受信しました。相手のロンアクション送信を待たずに即座にロン演出を開始します。");
                HandleNextRoundWaitingReceived(null);
            }
        }
        
        private bool _isStartingNextRound = false;

        public void HandleNextRoundWaitingReceived(NextRoundWaitingData data = null)
        {
            Debug.Log("[GameUIPhaseController] HandleNextRoundWaitingReceived: 相手が次ラウンド準備完了（またはロンボタン押下）しました。");
            
            if (data != null && data.ready_players != null)
            {
                // 自分以外のIDが ready_players に含まれているか確認
                string localId = NetworkMessageHandler.Instance.LocalPlayerId;
                bool enemyIsReady = false;
                bool localIsReady = false;
                foreach (var playerId in data.ready_players)
                {
                    if (playerId != localId) enemyIsReady = true;
                    if (playerId == localId) localIsReady = true;
                }
                
                if (uiManager.EnemyInfoUI != null)
                {
                    uiManager.EnemyInfoUI.SetReadyCheck(enemyIsReady);
                }
                
                if (uiManager.PlayerInfoUI != null)
                {
                    uiManager.PlayerInfoUI.SetReadyCheck(localIsReady);
                }
            }
        }

        private void StartNextRoundTransitionForDealing()
        {
            if (_isStartingNextRound) return;
            _isStartingNextRound = true;

            if (uiManager.PhaseTransitionUI != null)
            {
                uiManager.PhaseTransitionUI.PlayRoundStartDarken($"第{_currentRoundIndex}局...", () => {
                    BoardStateManager.Instance.ClearAllBoardData();
                    uiManager.ClearAllTiles();
                    StartCoroutine(DealingRoutine());
                });
            }
            else
            {
                BoardStateManager.Instance.ClearAllBoardData();
                uiManager.ClearAllTiles();
                StartCoroutine(DealingRoutine());
            }
        }

        private IEnumerator PlayRonWithPreDialogue(bool isLocalWin, List<int> winningHand, int ronTile, List<string> yaku, string formula, string rank)
        {
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.ClearReactions();
                
                bool isYakuman = rank == "役満";
                bool isDoraBaku = false;
                foreach (var y in yaku) if (y.Contains("ドラ") && y.Contains("3") || y.Contains("4") || y.Contains("5")) isDoraBaku = true;
                bool isCheap = formula == "1飜" || formula == "2飜";
                
                ReactionController.Instance.HandleAgari(isLocalWin, isYakuman, isDoraBaku, isCheap);
                ReactionController.Instance.SetPlayerLostLastRound(!isLocalWin);
            }

            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);

            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

            if (uiManager.RonAnimationUI != null)
            {
                uiManager.RonAnimationUI.gameObject.SetActive(true);

                var liq = BoardStateManager.Instance.LastLiquidationData;
                int score = liq != null ? liq.winner_gain : 0;
                
                int newLocalHp = Managers.BoardStateManager.Instance.LocalPlayerHp;
                int newEnemyHp = Managers.BoardStateManager.Instance.EnemyPlayerHp;
                int loserLoss = liq != null ? liq.loser_loss : 0;
                int prevLocalHp = isLocalWin ? (newLocalHp - score) : (newLocalHp + loserLoss);
                int prevEnemyHp = isLocalWin ? (newEnemyHp + loserLoss) : (newEnemyHp - score);

                uiManager.RonAnimationUI.PlayRonSequence(winningHand, ronTile, yaku, formula, rank, score, isLocalWin, 
                    uiManager.PlayerInfoUI, uiManager.EnemyInfoUI, prevLocalHp, newLocalHp, prevEnemyHp, newEnemyHp,
                    () => OnRonAnimationComplete(isLocalWin));
            }
            yield break;
        }

        private void OnRonAnimationComplete(bool isLocalWin)
        {
            _currentRoundIndex++;
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.SetCurrentRound(_currentRoundIndex);
                ReactionController.Instance.HandleGameEnd(isLocalWin);
                ReactionController.Instance.HandleRoundStart(_currentRoundIndex);
            }

            // HPのアニメーションはRonAnimationUIで完了しているため、
            // そのまま結果エフェクトを表示して次局へ進む
            OnScoreSettlementComplete(isLocalWin);
        }

        private void OnScoreSettlementComplete(bool isLocalWin)
        {
            if (uiManager.PlayerInfoUI != null)
            {
                uiManager.PlayerInfoUI.gameObject.SetActive(true);
                uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
            }
            if (uiManager.EnemyInfoUI != null)
            {
                uiManager.EnemyInfoUI.SetPanelVisible(true);
                uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
            }

            if (isLocalWin)
            {
                if (uiManager.VictoryEffectPrefab != null && uiManager.PlayerInfoUI != null) 
                    Instantiate(uiManager.VictoryEffectPrefab, uiManager.PlayerInfoUI.transform.position, Quaternion.identity);
                if (uiManager.DamageEffectPrefab != null && uiManager.EnemyInfoUI != null) 
                    Instantiate(uiManager.DamageEffectPrefab, uiManager.EnemyInfoUI.transform.position, Quaternion.identity);
            }
            else
            {
                if (uiManager.VictoryEffectPrefab != null && uiManager.EnemyInfoUI != null) 
                    Instantiate(uiManager.VictoryEffectPrefab, uiManager.EnemyInfoUI.transform.position, Quaternion.identity);
                if (uiManager.DamageEffectPrefab != null && uiManager.PlayerInfoUI != null) 
                    Instantiate(uiManager.DamageEffectPrefab, uiManager.PlayerInfoUI.transform.position, Quaternion.identity);
            }

            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.ShowReadyBox(true);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.ShowReadyBox(true);

            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.ShowNextRoundButton(() => {
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetReadyCheck(true);

                    if (uiManager.IsGameOver)
                    {
                        uiManager.ShowGameResult();
                    }
                    else
                    {
                        SendNextRoundAction();
                    }
                });
            }
            else
            {
                if (uiManager.IsGameOver)
                {
                    uiManager.ShowGameResult();
                }
                else
                {
                    SendNextRoundAction();
                }
            }
        }

        private void SendNextRoundAction()
        {
            if (uiManager.IsGameOver) return;

            // ゲームプレイ中のフェーズでは次局への進行リクエストを送らない
            // （遅延コルーチンが新局開始後に発火して二重送信になるのを防ぐため）
            if (uiManager.CurrentPhaseStatus == RoundStatus.Dealing ||
                uiManager.CurrentPhaseStatus == RoundStatus.HandSelection ||
                uiManager.CurrentPhaseStatus == RoundStatus.Betting ||
                uiManager.CurrentPhaseStatus == RoundStatus.Discard ||
                uiManager.CurrentPhaseStatus == RoundStatus.TurnDecision)
            {
                Debug.Log($"[GameUIPhaseController] SendNextRoundAction aborted. Current phase is {uiManager.CurrentPhaseStatus}");
                return;
            }

            if (!_hasSentNextRoundForCurrentPhase)
            {
                _hasSentNextRoundForCurrentPhase = true;
                NetworkMessageHandler.Instance.SendActionToServer("next_round", new ActionPayload());
            }
        }

        public void HandleSpecialVictoryWon(string playerId)
        {
            bool isLocalPlayer = (playerId == NetworkMessageHandler.Instance.LocalPlayerId);
            
            if (uiManager.VictoryUI != null)
            {
                uiManager.VictoryUI.PlayAnimation(isLocalPlayer ? VictoryType.SpecialVictory : VictoryType.SpecialDefeat);
            }
            else if (uiManager.PhaseTransitionUI != null)
            {
                string msg = isLocalPlayer ? "特殊勝利条件を達成しました！" : "相手が特殊勝利条件を達成しました...";
                uiManager.PhaseTransitionUI.PlayCenterTextAnim(msg, 3.0f, null);
            }
        }

        private IEnumerator DealingRoutine()
        {
            _isStartingNextRound = false;

            if (uiManager.PhaseTransitionUI != null)
            {
                while (uiManager.PhaseTransitionUI.IsDarkenTransitioning)
                {
                    yield return null;
                }
                uiManager.PhaseTransitionUI.ChangeDarkenText($"第{_currentRoundIndex}局進行中...");
            }
            
            uiManager.ClearAllTiles();
            SetMatchUIVisibility(false);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
            if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
            
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.PlayDealingReaction();
            }
        }
    }
}
