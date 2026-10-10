using System;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>どの通知経路でも、清算前を保存してからサーバーの確定値を一度だけ適用する。</summary>
    internal static class LiquidationMessageApplier
    {
        internal static bool TryApply(LiquidationData data, string json, NetworkMessageHandler network,
            Action beforeApply = null)
        {
            if (network.AgariProcessed) return false;
            if (data == null || string.IsNullOrEmpty(data.winner_id))
                data = ServerJsonParser.ParseLiquidationFromJson(json);
            // 欠けた通知で処理済みにしない。後から届く有効な清算を受け取れるようにする。
            if (data == null || string.IsNullOrEmpty(data.winner_id)) return false;

            var board = BoardStateManager.Instance;
            bool isLocalWin = data.winner_id == network.LocalPlayerId;
            network.AgariProcessed = true; // 有効性確認後、通知の再入による二重適用を防ぐ。
            beforeApply?.Invoke();
            board.LastIsLocalWin = isLocalWin;
            board.LastLiquidationData = data;
            board.RememberHpBeforeLiquidation();
            board.UpdateHp(isLocalWin ? data.winner_health : data.loser_health,
                isLocalWin ? data.loser_health : data.winner_health);
            network.RaisePhaseStatusChanged(RoundStatus.Agari);
            network.RaiseAgari(isLocalWin);
            return true;
        }
    }
}
