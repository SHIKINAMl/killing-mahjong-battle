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

            // **エラーで戻ってきたときは必ず操作ロックを解除する（2026-08-23）。**
            //
            // スキルは送信の直前に IsTransitioning を立てるが、倒すのは skill_casted だけだった。
            // サーバーに弾かれると error しか返らないため、フラグが立ちっぱなしになり
            // TileInteraction が全経路で早期 return して**盤面が二度と押せなくなる**
            // （強襲の「1局1回」はサーバー側だけの制限で、クライアントに同じ制限が無いので
            // 2発目を押すと確実に踏む）。
            // 手牌選択・打牌側も送信前にロックを立てるので、同じ事故はスキル以外でも起きうる。
            // 演出の途中で倒すことになっても、押せないまま固まるより軽い。
            uiManager.SetIsTransitioning(false);

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
