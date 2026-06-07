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
        
        private bool _isWaitingForEnemyRon = false;
        private bool _hasSentNextRoundForCurrentPhase = false;
        private int _currentRoundIndex = 1;
        private bool _isCarryOverNextRound = false;

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
        }

        public void ShowMatchmakingWaiting()
        {
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
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.ResetStateForNewGame();
                ReactionController.Instance.SetPlayerHp(20000);
                ReactionController.Instance.SetEnemyHp(20000);
                ReactionController.Instance.HandleRoundStart(1);
            }
            if (uiManager.MatchmakingUI != null)
            {
                uiManager.MatchmakingUI.Hide();
            }
            if (uiManager.DialogueUI != null) 
            {
                uiManager.DialogueUI.gameObject.SetActive(true);
                string introText = (uiManager.EnemyInfoUI != null) ? uiManager.EnemyInfoUI.PlayReaction(ReactionTrigger.GameStart) : null;
                if (string.IsNullOrEmpty(introText)) introText = "Match Found! Game Starting...";
                uiManager.DialogueUI.ShowText(introText);
            }
            if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(20000);
            if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(20000);
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

            if (!isGameEndPhase)
            {
                if (uiManager.HandUI != null) uiManager.HandUI.UpdateLayout(uiManager.CurrentPhaseStatus);

                if (uiManager.WallUI != null)
                {
                    uiManager.WallUI.UpdateContainerPosition(uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                    uiManager.WallUI.UpdateWallHighlights(BoardStateManager.Instance.CurrentWaitTiles, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                    uiManager.WallUI.UpdateDiscardTurnIndicator(BoardStateManager.Instance.IsLocalTurn, uiManager.CurrentPhaseStatus == RoundStatus.Discard);
                }
            }
            
            HandlePhaseVisibility(newStatus);
        }

        public void HandlePhaseVisibility(RoundStatus status)
        {
            if (uiManager.IsTransitioning) return;

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
                    uiManager.EnemyWallUI.gameObject.SetActive(showBoardElements);
                }
            }

            switch (status)
            {
                case RoundStatus.Betting:
                    SetMatchUIVisibility(false); 
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    StartBettingPhase(Managers.BoardStateManager.Instance.LocalPlayerHp);
                    break;
                case RoundStatus.Dealing:
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
                    break;
                case RoundStatus.HandSelection:
                    SetMatchUIVisibility(true);
                    if (uiManager.DialogueUI != null) uiManager.DialogueUI.SetBackgroundRaycast(false);
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(true);
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    if (uiManager.WaitUI != null) uiManager.WaitUI.gameObject.SetActive(false);
                    if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(true);
                    UpdateDoraDisplay();
                    if (ReactionController.Instance != null)
                    {
                        ReactionController.Instance.StartHandSelectionTimer();
                    }
                    break;
                case RoundStatus.TurnDecision:
                    if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
                    if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
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
                            if (uiManager.RonWaitPanel != null) uiManager.RonWaitPanel.SetActive(true);
                        }
                        else
                        {
                            _isWaitingForEnemyRon = true;
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
            if (uiManager.YakuListUI != null) uiManager.YakuListUI.gameObject.SetActive(visible);
            
            if (uiManager.WaitUI != null && BoardStateManager.Instance.CurrentWaitTiles != null && BoardStateManager.Instance.CurrentWaitTiles.Count > 0)
            {
                if (!visible) uiManager.WaitUI.gameObject.SetActive(false);
            }
        }

        private void StartBettingPhase(int currentHealth)
        {
            if (uiManager.BettingUI != null)
            {
                uiManager.BettingUI.ShowBettingPhase(20000, currentHealth, OnBetConfirmed);
                if (ReactionController.Instance != null)
                {
                    ReactionController.Instance.StartBetPhaseTimer();
                }
            }
        }

        private void OnBetConfirmed(int betAmount)
        {
            uiManager.BettingUI.HideBettingPhase();
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
                 SetMatchUIVisibility(false);
                 
                 if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);
                 if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
                 if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);
                 if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(false);

                 uiManager.PhaseTransitionUI.PlayTransition(roundString, uiManager.PlayerInfoUI, playerBet, enemyBet, playerHp, enemyHp,
                    onMidpoint: () => {
                         SetMatchUIVisibility(true); 
                         
                         uiManager.SetIsTransitioning(false);
                         
                         if (uiManager.CurrentPhaseStatus == RoundStatus.Betting)
                         {
                             UpdatePhaseStatus(RoundStatus.Discard);
                         }

                         HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
                         
                         uiManager.SetIsTransitioning(true);
                         
                         if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(Managers.BoardStateManager.Instance.LocalPlayerHp);
                         if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(Managers.BoardStateManager.Instance.EnemyPlayerHp);
                    },
                    onComplete: () => {
                         uiManager.SetIsTransitioning(false);
                         HandlePhaseVisibility(uiManager.CurrentPhaseStatus);
                         if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(true);
                    }
                 );
             }
        }

        public void HandleDraw()
        {
            Debug.Log("[GameUIManager] 流局処理開始");
            _isCarryOverNextRound = true;
            _currentRoundIndex++;
            if (ReactionController.Instance != null)
            {
                ReactionController.Instance.SetCurrentRound(_currentRoundIndex);
                ReactionController.Instance.CheckAndPlayDrawReaction();
                ReactionController.Instance.HandleRoundStart(_currentRoundIndex);
            }

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
                        Debug.Log("[GameUIManager] 流局演出完了 - 次ラウンド待ち承認送信");
                        SendNextRoundAction();
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

            Debug.Log("[GameUIManager] 流局演出完了 - 次ラウンド待ち承認送信");
            SendNextRoundAction();
        }

        public void ExecuteRonAction()
        {
            if (uiManager.RonWaitPanel != null) uiManager.RonWaitPanel.SetActive(false);

            SendNextRoundAction();

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

        public void HandleNextRoundWaitingReceived()
        {
            if (_isWaitingForEnemyRon)
            {
                _isWaitingForEnemyRon = false;
                
                bool isLocalWin = false;
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

            if (uiManager.RonAnimationUI != null)
            {
                uiManager.RonAnimationUI.PrepareForPreDialogue();
                uiManager.RonAnimationUI.gameObject.SetActive(true);
                uiManager.RonAnimationUI.ShowPlayerRonBubble(isLocalWin);
            }

            bool useBubble = isLocalWin && uiManager.RonAnimationUI != null && uiManager.RonAnimationUI.HasPlayerRonBubble();

            if (useBubble)
            {
            }
            else if (isLocalWin)
            {
                if (uiManager.DialogueUI != null)
                {
                    uiManager.DialogueUI.gameObject.SetActive(true);
                    string loseText = (uiManager.EnemyInfoUI != null) ? uiManager.EnemyInfoUI.PlayReaction(ReactionTrigger.Lose) : null;
                    if (string.IsNullOrEmpty(loseText)) loseText = "「ロン！」";
                    uiManager.DialogueUI.ShowText(loseText);
                }
            }
            else
            {
                if (uiManager.DialogueUI != null)
                {
                    uiManager.DialogueUI.gameObject.SetActive(true);
                    string winText = (uiManager.EnemyInfoUI != null) ? uiManager.EnemyInfoUI.PlayReaction(ReactionTrigger.Win) : null;
                    if (string.IsNullOrEmpty(winText)) winText = "「ロンよ！」";
                    uiManager.DialogueUI.ShowText(winText);
                }
            }

            if (isLocalWin && uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.PlayBounceAnimation(1.5f);
            if (!isLocalWin && uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.PlayBounceAnimation(1.5f);

            yield return new WaitForSeconds(1.5f);

            if (uiManager.RonAnimationUI != null) uiManager.RonAnimationUI.ShowPlayerRonBubble(false);
            if (uiManager.DialogueUI != null) uiManager.DialogueUI.gameObject.SetActive(false);
            if (uiManager.AbilityUI != null) uiManager.AbilityUI.gameObject.SetActive(false);

            if (uiManager.RonAnimationUI != null)
            {
                uiManager.RonAnimationUI.PlayRonSequence(winningHand, ronTile, yaku, formula, rank, isLocalWin, () => OnRonAnimationComplete(isLocalWin));
            }
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
            var liq = Managers.BoardStateManager.Instance.LastLiquidationData;
            int newLocalHp = Managers.BoardStateManager.Instance.LocalPlayerHp;
            int newEnemyHp = Managers.BoardStateManager.Instance.EnemyPlayerHp;

            int winnerGain = liq != null ? liq.winner_gain : 0;
            int loserLoss = liq != null ? liq.loser_loss : 0;
            int prevLocalHp = isLocalWin ? (newLocalHp - winnerGain) : (newLocalHp + loserLoss);
            int prevEnemyHp = isLocalWin ? (newEnemyHp + loserLoss) : (newEnemyHp - winnerGain);

            string rankLabel = "精算";
            if (liq != null)
            {
                if (liq.multiplier >= 4.0f) rankLabel = "役満";
                else if (liq.multiplier >= 3.0f) rankLabel = "三倍満";
                else if (liq.multiplier >= 2.0f) rankLabel = "倍満";
                else if (liq.multiplier >= 1.5f) rankLabel = "跳満";
                else rankLabel = "満貫";
            }

            if (uiManager.PhaseTransitionUI != null)
            {
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.gameObject.SetActive(false);
                if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetPanelVisible(false);

                uiManager.PhaseTransitionUI.PlayScoreSettlementAnimation(
                    isLocalWin, winnerGain, loserLoss,
                    prevLocalHp, prevEnemyHp,
                    newLocalHp, newEnemyHp,
                    rankLabel,
                    () => OnScoreSettlementComplete(isLocalWin)
                );
            }
            else
            {
                if (uiManager.PlayerInfoUI != null) uiManager.PlayerInfoUI.SetHP(newLocalHp);
                if (uiManager.EnemyInfoUI != null) uiManager.EnemyInfoUI.SetHP(newEnemyHp);
                OnScoreSettlementComplete(isLocalWin);
            }
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

            StartCoroutine(WaitAndSendNextRound(3.0f));
        }

        private IEnumerator WaitAndSendNextRound(float delay)
        {
            yield return new WaitForSeconds(delay);
            SendNextRoundAction();
        }

        private void SendNextRoundAction()
        {
            if (!_hasSentNextRoundForCurrentPhase)
            {
                _hasSentNextRoundForCurrentPhase = true;
                NetworkMessageHandler.Instance.SendActionToServer("next_round", new ActionPayload());
            }
        }

        public void HandleSpecialVictoryWon(string playerId)
        {
            bool isLocalPlayer = (playerId == NetworkMessageHandler.Instance.LocalPlayerId);
            string msg = isLocalPlayer ? "特殊勝利条件を達成しました！" : "相手が特殊勝利条件を達成しました...";
            if (uiManager.PhaseTransitionUI != null)
            {
                uiManager.PhaseTransitionUI.PlayCenterTextAnim(msg, 3.0f, null);
            }
        }
    }
}
