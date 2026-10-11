using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// スキル/特殊勝利関連:
    /// "skill_casted"（スキル発動）, "special_victory_won"（特殊勝利）,
    /// "opening_boost_assigned"（開局時の役ブースト付与）、
    /// "skill_accepted"（本人への受付。牌交換の候補が入る）, "mulligan_selected"（牌交換の結果）。
    /// </summary>
    public class SkillMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types =
        {
            "skill_casted", "special_victory_won", "opening_boost_assigned", "skill_accepted", "mulligan_selected",
        };

        /// <summary>
        /// JSON の mulliganResult が null か。**JsonUtility は null を「中身が全部 0 の物」として作る**ので、
        /// 型を見ても区別できない。新しい牌交換では skill_casted の mulliganResult は常に null で、
        /// そのまま読むと「位置0の牌0を牌0に替える」という嘘の交換になる。
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex NullMulliganResult =
            new System.Text.RegularExpressions.Regex("\"mulliganResult\"\\s*:\\s*null");
        public IReadOnlyList<string> MessageTypes => Types;

        public void Handle(string messageType, string jsonString, NetworkMessageHandler network)
        {
            var board = Managers.BoardStateManager.Instance;

            switch (messageType)
            {
                case "skill_casted":
                    SkillCastedMessage scMsg = JsonUtility.FromJson<SkillCastedMessage>(jsonString);
                    if (scMsg != null && scMsg.data != null)
                    {
                        scMsg.data.exposedHandIndexesByPlayer = ServerJsonParser.ParseExposedHandIndexesByPlayer(jsonString);
                        if (NullMulliganResult.IsMatch(jsonString)) scMsg.data.mulliganResult = null;

                        // 実際に引かれた額をサーバーから教わる唯一の機会。控えておくと、
                        // 次から能力一覧が複製の表ではなく本物の額を出せる。
                        //
                        // **自分の発動だけを覚える。** コストは特殊勝利の回数で段が変わるが、
                        // 相手が何回使ったかは分からないので、相手のぶんを自分の回数で覚えると
                        // 違う段の額を混ぜてしまう。
                        //
                        // ここは status で回数が更新される前なので、
                        // LocalPlayerSpecialVictoryCount は発動時点の回数のまま
                        // （特殊勝利そのものを撃ったときも、支払ったのは増える前の段の額）。
                        if (scMsg.data.player_id == network.LocalPlayerId && board != null)
                        {
                            GameRules.SetServerSkillCost(
                                scMsg.data.skillType, board.LocalPlayerSpecialVictoryCount, scMsg.data.cost);
                        }

                        network.RaiseSkillCasted(scMsg.data);

                        // スキル使用直後に、役の強化状況や最新のHPを確実に取り寄せる
                        network.SendActionToServer("status", null);
                    }
                    break;

                case "skill_accepted":
                    SkillAcceptedMessage saMsg = JsonUtility.FromJson<SkillAcceptedMessage>(jsonString);
                    if (saMsg != null && saMsg.data != null)
                    {
                        // 候補が無いとき（牌交換以外）は null にそろえる。JsonUtility は空の物を作ってしまう
                        var options = saMsg.data.mulliganOptions;
                        if (options == null || options.options == null || options.options.Count == 0)
                        {
                            saMsg.data.mulliganOptions = null;
                        }
                        if (board != null && saMsg.data.cost > 0)
                        {
                            GameRules.SetServerSkillCost(
                                saMsg.data.skillType, board.LocalPlayerSpecialVictoryCount, saMsg.data.cost);
                        }
                        network.RaiseSkillAccepted(saMsg.data);
                    }
                    break;

                case "mulligan_selected":
                    MulliganSelectedMessage msMsg = JsonUtility.FromJson<MulliganSelectedMessage>(jsonString);
                    if (msMsg != null && msMsg.data != null) network.RaiseMulliganSelected(msMsg.data);
                    break;

                case "special_victory_won":
                    SpecialVictoryWonMessage svwMsg = JsonUtility.FromJson<SpecialVictoryWonMessage>(jsonString);
                    if (svwMsg != null && svwMsg.data != null)
                    {
                        network.RaiseSpecialVictoryWon(svwMsg.data.player_id);
                    }
                    break;

                case "opening_boost_assigned":
                    OpeningBoostAssignedMessage obMsg = JsonUtility.FromJson<OpeningBoostAssignedMessage>(jsonString);
                    if (obMsg != null && obMsg.data != null && obMsg.data.boosts != null)
                    {
                        foreach (var boost in obMsg.data.boosts)
                        {
                            if (boost.client_id == network.LocalPlayerId)
                            {
                                if (board.LocalBoostHandBonus == null)
                                    board.LocalBoostHandBonus = new Dictionary<string, int>();
                                board.LocalBoostHandBonus[boost.yaku_name] = boost.bonus_han;
                            }
                            else
                            {
                                if (board.EnemyBoostHandBonus == null)
                                    board.EnemyBoostHandBonus = new Dictionary<string, int>();
                                board.EnemyBoostHandBonus[boost.yaku_name] = boost.bonus_han;
                            }
                        }
                        network.RaiseOpeningBoostAssigned();
                    }
                    break;
            }
        }
    }
}
