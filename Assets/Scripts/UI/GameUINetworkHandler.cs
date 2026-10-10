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
        private bool receivedInitialResponse;
        private bool connectionFailed;
        private const float InitialResponseTimeout = 30f;

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
                NetworkMessageHandler.Instance.OnDealingStarted += HandleDealingStarted;
                NetworkMessageHandler.Instance.OnDealingCompleted += HandleDealingCompleted;
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
            if (uiManager != null && !uiManager.IsTutorialMode &&
                (NetworkMessageHandler.Instance == null || !NetworkMessageHandler.Instance.UseDebugClient))
                StartCoroutine(WatchInitialConnection());
        }

        private System.Collections.IEnumerator WatchInitialConnection()
        {
            float deadline = Time.realtimeSinceStartup + InitialResponseTimeout;
            while (!receivedInitialResponse && Time.realtimeSinceStartup < deadline) yield return null;
            if (receivedInitialResponse) yield break;
            connectionFailed = true;
            LoadingManager.Instance?.ForceHide();
            SessionPrompt prompt = null;
            prompt = SessionPrompt.Show("サーバーから応答がありません。\n通信環境を確認し、タイトルからやり直してください。",
                "タイトルへ戻る", () => prompt.ReturnToTitle());
            // 遅れて成立した対局を、このエラー画面の裏で開始させない。
            var client = WebSocketGameClientSample.Instance;
            if (client != null) _ = client.ResetConnectionAsync();
        }

        private void OnDestroy()
        {
            if (NetworkMessageHandler.Instance != null && isEventsRegistered)
            {
                NetworkMessageHandler.Instance.OnGameStarted -= HandleGameStarted;
                NetworkMessageHandler.Instance.OnMatchmakingWaiting -= HandleMatchmakingWaiting;
                NetworkMessageHandler.Instance.OnMatchCancelled -= HandleMatchCancelled;
                
                NetworkMessageHandler.Instance.OnPhaseStatusChanged -= HandlePhaseStatusChanged;
                NetworkMessageHandler.Instance.OnDealingStarted -= HandleDealingStarted;
                NetworkMessageHandler.Instance.OnDealingCompleted -= HandleDealingCompleted;
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
            if (connectionFailed) return;
            receivedInitialResponse = true;
            Debug.Log("[GameUINetworkHandler] HandleGameStarted called.");
            KillingMahjong.UI.LoadingManager.Instance.ForceHide();
            uiManager.PhaseController?.OnGameStarted();
        }

        private void HandleMatchmakingWaiting(KillingMahjong.EngineData.MatchingWaitingData data)
        {
            if (connectionFailed) return;
            receivedInitialResponse = true;
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

        private void HandleDealingStarted()
        {
            uiManager.PhaseController?.HandleDealingStarted();
        }

        private void HandleDealingCompleted()
        {
            uiManager.PhaseController?.HandleDealingCompleted();
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

        private void HandleBettingComplete(KillingMahjong.EngineData.BettingCompletedInfo info)
        {
            uiManager.PhaseController?.OnBettingCompleteFromServer(info);
        }

        private void HandleError(string message)
        {
            if (KillingMahjong.UI.LoadingManager.Instance != null)
            {
                KillingMahjong.UI.LoadingManager.Instance.ForceHide();
            }

            // 操作識別子のないerrorでは、応答待ちの要求だけを解除する。進行中の演出は触らない。
            uiManager.SkillController?.CancelPendingSkillRequest();

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
