using UnityEngine;
using KillingMahjong.Network;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUINetworkHandler : MonoBehaviour
    {
        private GameUIManager uiManager;
        private bool isEventsRegistered = false;

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
            RegisterEvents();
        }

        private void RegisterEvents()
        {
            if (isEventsRegistered) return;
            if (NetworkMessageHandler.Instance != null)
            {
                NetworkMessageHandler.Instance.OnGameStarted += HandleGameStarted;
                NetworkMessageHandler.Instance.OnMatchmakingWaiting += HandleMatchmakingWaiting;
                NetworkMessageHandler.Instance.OnMatchCancelled += HandleMatchCancelled;
                
                NetworkMessageHandler.Instance.OnPhaseStatusChanged += HandlePhaseStatusChanged;
                NetworkMessageHandler.Instance.OnIsTenpaiReceived += HandleIsTenpaiReceived;
                NetworkMessageHandler.Instance.OnNotTenpaiReceived += HandleNotTenpaiReceived;
                NetworkMessageHandler.Instance.OnNextRoundWaitingReceived += HandleNextRoundWaitingReceived;
                NetworkMessageHandler.Instance.OnPhaseCompletedNotice += HandlePhaseCompletedNotice;
                NetworkMessageHandler.Instance.OnLocalBetAccepted += HandleLocalBetAccepted;
                NetworkMessageHandler.Instance.OnHandSelectionConfirmation += HandleHandSelectionConfirmation;
                NetworkMessageHandler.Instance.OnHandSelectionAccepted += HandleHandSelectionAccepted;
                NetworkMessageHandler.Instance.OnSkillCasted += HandleSkillCasted;

                NetworkMessageHandler.Instance.OnAgari += HandleAgari;
                NetworkMessageHandler.Instance.OnDraw += HandleDraw;
                NetworkMessageHandler.Instance.OnBettingComplete += HandleBettingComplete;
                NetworkMessageHandler.Instance.OnError += HandleError;
                NetworkMessageHandler.Instance.OnSpecialVictoryWon += HandleSpecialVictoryWon;
                isEventsRegistered = true;
            }
        }

        private void Start()
        {
            // Setup()が呼ばれなかった場合のフェイルセーフとしてここでも呼ぶ
            RegisterEvents();
        }

        private void OnDestroy()
        {
            if (NetworkMessageHandler.Instance != null && isEventsRegistered)
            {
                NetworkMessageHandler.Instance.OnGameStarted -= HandleGameStarted;
                NetworkMessageHandler.Instance.OnMatchmakingWaiting -= HandleMatchmakingWaiting;
                NetworkMessageHandler.Instance.OnMatchCancelled -= HandleMatchCancelled;
                
                NetworkMessageHandler.Instance.OnPhaseStatusChanged -= HandlePhaseStatusChanged;
                NetworkMessageHandler.Instance.OnIsTenpaiReceived -= HandleIsTenpaiReceived;
                NetworkMessageHandler.Instance.OnNotTenpaiReceived -= HandleNotTenpaiReceived;
                NetworkMessageHandler.Instance.OnNextRoundWaitingReceived -= HandleNextRoundWaitingReceived;
                NetworkMessageHandler.Instance.OnPhaseCompletedNotice -= HandlePhaseCompletedNotice;
                NetworkMessageHandler.Instance.OnLocalBetAccepted -= HandleLocalBetAccepted;
                NetworkMessageHandler.Instance.OnHandSelectionConfirmation -= HandleHandSelectionConfirmation;
                NetworkMessageHandler.Instance.OnHandSelectionAccepted -= HandleHandSelectionAccepted;
                NetworkMessageHandler.Instance.OnSkillCasted -= HandleSkillCasted;

                NetworkMessageHandler.Instance.OnAgari -= HandleAgari;
                NetworkMessageHandler.Instance.OnDraw -= HandleDraw;
                NetworkMessageHandler.Instance.OnBettingComplete -= HandleBettingComplete;
                NetworkMessageHandler.Instance.OnError -= HandleError;
                NetworkMessageHandler.Instance.OnSpecialVictoryWon -= HandleSpecialVictoryWon;
            }
        }

        private void HandleGameStarted()
        {
            Debug.Log("[GameUINetworkHandler] HandleGameStarted called.");
            KillingMahjong.UI.LoadingManager.Instance.ForceHide();
            uiManager.PhaseController?.OnGameStarted();
        }

        private void HandleMatchmakingWaiting(KillingMahjong.EngineData.MatchingWaitingData data)
        {
            Debug.Log("[GameUINetworkHandler] HandleMatchmakingWaiting called.");

            // 暗転を明けさせつつ、マッチング待機画面を出す
            if (KillingMahjong.UI.LoadingManager.Instance != null)
            {
                // 暗転中（フェード）を徐々に透明にして解除する
                KillingMahjong.UI.LoadingManager.Instance.FadeInScreen(() => 
                {
                    // 必要ならコールバック内で追加処理
                });
            }
            
            uiManager.PhaseController?.ShowMatchmakingWaiting(data);
        }

        private void HandleMatchCancelled(string reason)
        {
            KillingMahjong.UI.LoadingManager.Instance.ForceHide();
            uiManager.PhaseController?.ShowMatchCancelled(reason);
        }

        private void HandlePhaseStatusChanged(RoundStatus newStatus)
        {
            uiManager.PhaseController?.UpdatePhaseStatus(newStatus);
        }

        private void HandleIsTenpaiReceived(IsTenpaiData data)
        {
            uiManager.HandSelectionController?.HandleIsTenpaiReceived(data);
        }

        private void HandleNotTenpaiReceived(string reason)
        {
            uiManager.HandSelectionController?.HandleNotTenpaiReceived(reason);
        }

        private void HandleNextRoundWaitingReceived(NextRoundWaitingData data)
        {
            uiManager.PhaseController?.HandleNextRoundWaitingReceived(data);
        }

        private void HandlePhaseCompletedNotice(PhaseCompletedNoticeData data)
        {
            uiManager.PhaseController?.HandlePhaseCompletedNotice(data);
        }

        private void HandleHandSelectionConfirmation(HandSelectionConfirmationData data)
        {
            uiManager.HandSelectionController?.HandleHandSelectionConfirmation(data);
        }

        private void HandleHandSelectionAccepted()
        {
            uiManager.HandSelectionController?.OnHandSelectionAccepted();
            // 自分の手牌が確定した合図。相手ぶんは phase_completed_notice を待つ
            uiManager.PhaseController?.MarkLocalPhaseReady(RoundStatus.HandSelection);
        }

        private void HandleLocalBetAccepted()
        {
            uiManager.PhaseController?.MarkLocalPhaseReady(RoundStatus.Betting);
        }

        private void HandleSkillCasted(SkillCastedData data)
        {
            uiManager.SkillController?.HandleSkillCasted(data);
        }

        private void HandleAgari(bool isLocalWin)
        {
            uiManager.PhaseController?.HandleAgari(isLocalWin);
        }

        private void HandleDraw(DrawPlayerData[] drawData)
        {
            uiManager.PhaseController?.HandleDraw(drawData);
        }

        private void HandleBettingComplete(int playerBet, int enemyBet, int playerHp, int enemyHp)
        {
            uiManager.PhaseController?.OnBettingCompleteFromServer(playerBet, enemyBet, playerHp, enemyHp);
        }

        private void HandleError(string message)
        {
            if (KillingMahjong.UI.LoadingManager.Instance != null)
            {
                KillingMahjong.UI.LoadingManager.Instance.ForceHide();
            }
            if (uiManager.DialogueUI != null)
            {
                uiManager.DialogueUI.ShowText($"エラー: {message}");
            }
        }

        private void HandleSpecialVictoryWon(string playerId)
        {
            uiManager.PhaseController?.HandleSpecialVictoryWon(playerId);
        }
    }
}
