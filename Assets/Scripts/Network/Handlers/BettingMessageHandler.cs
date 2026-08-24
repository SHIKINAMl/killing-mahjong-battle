using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// "bet_completed": ベット確定。双方のベット額をHPへ反映する。
    /// "bet_accepted": 自分のベットが受理された合図（送った本人にだけ届く）。
    /// </summary>
    public class BettingMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "bet_completed", "bet_accepted" };
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            if (messageType == "bet_accepted")
            {
                // 「準備完了」の表示と、賭け金の上限・単位の取り込みに使う。盤面は動かさない
                BetAcceptedMessage aMsg = JsonUtility.FromJson<BetAcceptedMessage>(jsonString);
                if (aMsg != null && aMsg.data != null)
                {
                    Managers.BoardStateManager.Instance.SetServerBetRules(aMsg.data.max_bet, aMsg.data.bet_unit);
                }
                network.RaiseLocalBetAccepted();
                return;
            }

            BetCompletedMessage bMsg = JsonUtility.FromJson<BetCompletedMessage>(jsonString);
            if (bMsg == null || bMsg.data == null || bMsg.data.bets == null) return;

            var board = Managers.BoardStateManager.Instance;

            int pBet = 0; int eBet = 0;
            int pServerHp = 0; int eServerHp = 0;
            foreach (var b in bMsg.data.bets)
            {
                if (b.client_id == network.LocalPlayerId) { pBet = b.bet; pServerHp = b.health; }
                else                                     { eBet = b.bet; eServerHp = b.health; }
            }

            var info = new BettingCompletedInfo
            {
                LocalBet = pBet,
                EnemyBet = eBet,
                LocalHpBefore = board.LocalPlayerHp,
                EnemyHpBefore = board.EnemyPlayerHp,
            };

            // **血はサーバーが送ってきた値だけで動かす（2026-08-04 決定）。**
            // クライアントで賭け金を引き算して辻褄を合わせると、サーバー側の誤りが
            // 画面に出なくなる。詳細は BoardStateManager.UseServerHealth。
            //
            // サーバーはベットの時点で引いて（`game_engine.py: place_bet`）、
            // 引いた後の値を送ってきている（`game_session.py: on_bet` の `bets[].health`）。
            // **2026-08-24 まで `PlayerBetData` に health が無く、JsonUtility が捨てていた。**
            //
            // 両方 0 は「入っていない」の意味に取る（`StatusMessageHandler` と同じ約束）。
            // 素直に信じると、送ってこない相手と喋ったときに血が 0 へ飛ぶ。
            info.HasServerHealth = pServerHp > 0 || eServerHp > 0;

            if (info.HasServerHealth)
            {
                info.LocalHpAfter = pServerHp;
                info.EnemyHpAfter = eServerHp;
            }
            else if (Managers.BoardStateManager.UseServerHealth)
            {
                // 届かなかったときは動かさない。下の status の返事で追いつく
                info.LocalHpAfter = info.LocalHpBefore;
                info.EnemyHpAfter = info.EnemyHpBefore;
            }
            else
            {
                info.LocalHpAfter = info.LocalHpBefore - pBet;
                info.EnemyHpAfter = info.EnemyHpBefore - eBet;
            }

            // 盤面のHPは先に合わせておく。**演出には Before / After の両方を渡す**ので、
            // ここで動かしても「減る様子」は失われない
            if (info.HasServerHealth || !Managers.BoardStateManager.UseServerHealth)
            {
                board.UpdateHp(info.LocalHpAfter, info.EnemyHpAfter);
            }

            network.RaiseBettingComplete(info);

            // health が届かなかったときだけ、決済後の血を取り寄せる
            if (Managers.BoardStateManager.UseServerHealth && !info.HasServerHealth)
            {
                network.SendActionToServer("status", null);
            }
        }
    }
}
