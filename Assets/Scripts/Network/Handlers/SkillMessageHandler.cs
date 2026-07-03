using System.Collections.Generic;
using KillingMahjong.EngineData;
using UnityEngine;

namespace KillingMahjong.Network.Handlers
{
    /// <summary>
    /// スキル/特殊勝利関連:
    /// "skill_casted"（スキル発動）, "special_victory_won"（特殊勝利）,
    /// "opening_boost_assigned"（開局時の役ブースト付与）。
    /// </summary>
    public class SkillMessageHandler : IServerMessageHandler
    {
        private static readonly string[] Types = { "skill_casted", "special_victory_won", "opening_boost_assigned" };
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
                        network.RaiseSkillCasted(scMsg.data);

                        // スキル使用直後に、役の強化状況や最新のHPを確実に取り寄せる
                        network.SendActionToServer("status", null);
                    }
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
