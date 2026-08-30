using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // 対局の始まりと、局から局への繋ぎ。GameUIPhaseController から分離（partial）。
    public partial class GameUIPhaseController
    {

        public void OnGameStarted()
        {
            _currentRoundIndex = 1;
            ResetPhaseReadyMarks();

            // 前の対局で覚えたスキルコストは持ち越さない（特殊勝利の回数が 0 に戻るため）
            GameRules.ClearServerSkillCosts();

            try
            {
                // 血の初期値もサーバーが持っている（game_engine.py:61）。
                // ここではつなぎの値を置くだけにして、本物は status で取り直す。
                // つなぎを置かないと、前の対局の残り血（0 など）がゲージに残る。
                Managers.BoardStateManager.Instance.UpdateHp(
                    Managers.BoardStateManager.PlaceholderInitialHp,
                    Managers.BoardStateManager.PlaceholderInitialHp);
                if (!uiManager.IsTutorialMode)
                {
                    uiManager.SendActionToServer("status", null);
                }
                if (uiManager.BetPotUI != null) uiManager.BetPotUI.Clear();
                // 新しい対局なので獲得も賭け金も引き直す
                if (!uiManager.IsTutorialMode) uiManager.ScoreGauge.ResetScores();
                if (uiManager.PlayerInfoUI != null)
                {
                    uiManager.PlayerInfoUI.gameObject.SetActive(true);
                    // 前の対局で 20000 超えまで血を増やしているとメーターの分母が残るので引き直す
                    uiManager.PlayerInfoUI.ResetHpMeter(20000);
                    uiManager.PlayerInfoUI.SetHP(20000);
                }
                if (uiManager.EnemyInfoUI != null)
                {
                    uiManager.EnemyInfoUI.SetPanelVisible(true);
                    uiManager.EnemyInfoUI.ResetHpMeter(20000);
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

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayBGM(AudioManager.Instance.battleBgm);
                }
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
