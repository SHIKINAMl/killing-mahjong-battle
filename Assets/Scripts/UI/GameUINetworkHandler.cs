using UnityEngine;
using KillingMahjong.Network;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    [RequireComponent(typeof(GameUIManager))]
    public class GameUINetworkHandler : MonoBehaviour
    {
        private GameUIManager uiManager;

        public void Setup(GameUIManager manager)
        {
            this.uiManager = manager;
        }

        private void Start()
        {
            if (NetworkMessageHandler.Instance != null)
            {
                NetworkMessageHandler.Instance.OnGameStarted += HandleGameStarted;
                NetworkMessageHandler.Instance.OnMatchmakingWaiting += HandleMatchmakingWaiting;
                NetworkMessageHandler.Instance.OnMatchCancelled += HandleMatchCancelled;
                
                NetworkMessageHandler.Instance.OnPhaseStatusChanged += HandlePhaseStatusChanged;
                NetworkMessageHandler.Instance.OnIsTenpaiReceived += HandleIsTenpaiReceived;
                NetworkMessageHandler.Instance.OnNotTenpaiReceived += HandleNotTenpaiReceived;
                NetworkMessageHandler.Instance.OnNextRoundWaitingReceived += HandleNextRoundWaitingReceived;
                NetworkMessageHandler.Instance.OnHandSelectionConfirmation += HandleHandSelectionConfirmation;
                NetworkMessageHandler.Instance.OnHandSelectionAccepted += HandleHandSelectionAccepted;
                NetworkMessageHandler.Instance.OnSkillCasted += HandleSkillCasted;

                NetworkMessageHandler.Instance.OnAgari += HandleAgari;
                NetworkMessageHandler.Instance.OnDraw += HandleDraw;
                NetworkMessageHandler.Instance.OnBettingComplete += HandleBettingComplete;
                NetworkMessageHandler.Instance.OnError += HandleError;
                NetworkMessageHandler.Instance.OnGameEnded += HandleGameEnded;
                NetworkMessageHandler.Instance.OnSpecialVictoryWon += HandleSpecialVictoryWon;
            }
        }

        private void OnDestroy()
        {
            if (NetworkMessageHandler.Instance != null)
            {
                NetworkMessageHandler.Instance.OnGameStarted -= HandleGameStarted;
                NetworkMessageHandler.Instance.OnMatchmakingWaiting -= HandleMatchmakingWaiting;
                NetworkMessageHandler.Instance.OnMatchCancelled -= HandleMatchCancelled;
                
                NetworkMessageHandler.Instance.OnPhaseStatusChanged -= HandlePhaseStatusChanged;
                NetworkMessageHandler.Instance.OnIsTenpaiReceived -= HandleIsTenpaiReceived;
                NetworkMessageHandler.Instance.OnNotTenpaiReceived -= HandleNotTenpaiReceived;
                NetworkMessageHandler.Instance.OnNextRoundWaitingReceived -= HandleNextRoundWaitingReceived;
                NetworkMessageHandler.Instance.OnHandSelectionConfirmation -= HandleHandSelectionConfirmation;
                NetworkMessageHandler.Instance.OnHandSelectionAccepted -= HandleHandSelectionAccepted;
                NetworkMessageHandler.Instance.OnSkillCasted -= HandleSkillCasted;

                NetworkMessageHandler.Instance.OnAgari -= HandleAgari;
                NetworkMessageHandler.Instance.OnDraw -= HandleDraw;
                NetworkMessageHandler.Instance.OnBettingComplete -= HandleBettingComplete;
                NetworkMessageHandler.Instance.OnError -= HandleError;
                NetworkMessageHandler.Instance.OnGameEnded -= HandleGameEnded;
                NetworkMessageHandler.Instance.OnSpecialVictoryWon -= HandleSpecialVictoryWon;
            }
        }

        private void HandleGameEnded(int localScore, int enemyScore)
        {
            if (uiManager.PhaseController != null)
            {
                uiManager.PhaseController.HandleGameEnded();
            }
        }

        private void HandleGameStarted()
        {
            uiManager.PhaseController?.OnGameStarted();
        }

        private void HandleMatchmakingWaiting()
        {
            uiManager.PhaseController?.ShowMatchmakingWaiting();
        }

        private void HandleMatchCancelled(string reason)
        {
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

        private void HandleNextRoundWaitingReceived()
        {
            uiManager.PhaseController?.HandleNextRoundWaitingReceived();
        }

        private void HandleHandSelectionConfirmation(HandSelectionConfirmationData data)
        {
            uiManager.HandSelectionController?.HandleHandSelectionConfirmation(data);
        }

        private void HandleHandSelectionAccepted()
        {
            uiManager.HandSelectionController?.OnHandSelectionAccepted();
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
