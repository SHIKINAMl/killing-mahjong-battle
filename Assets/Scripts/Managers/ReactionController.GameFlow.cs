using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KillingMahjong.UI;
using KillingMahjong.Managers.Reactions;

namespace KillingMahjong.Managers
{
    public partial class ReactionController
    {

        private void NotifyBetAmountChangedCore()
        {
            _betChangeCount++;
            if (_betChangeCount == betFidgetCount)
            {
                Trigger(ReactionTrigger.Bet_FidgetSpam, ReactionPriority.Ambient);
            }
        }

        private void CheckAndPlayBetReactionCore(int betAmount, int maxHp, bool isLocalPlayer)
        {
            bool max = betAmount >= maxBetThreshold;
            bool min = betAmount > 0 && betAmount <= minBetThreshold;

            if (Publish(ReactionEvent.BetConfirmed, NewContext()
                    .Set(ReactionVars.IsMyBet, isLocalPlayer)
                    .Set(ReactionVars.BetAmount, betAmount)
                    .Set(ReactionVars.BetMax, maxBetThreshold)
                    .Set(ReactionVars.IsMaxBet, max)
                    .Set(ReactionVars.IsMinBet, min)
                    .Set(ReactionVars.IsTenpai, IsLocalTenpai())
                    .Set(ReactionVars.BetChangeCount, _betChangeCount)
                    .Set(ReactionVars.BetDecideSeconds,
                         _betPhaseTimerActive ? Time.time - _betPhaseStartTime : 0f)))
            {
                // 「初めて」の記録だけは進めておく。次に CSV へ落ちたとき辻褄が合うように
                if (isLocalPlayer)
                {
                    if (max) _firstMaxBetPlayed = true;
                    if (min) _firstMinBetPlayed = true;
                }
                _betPhaseTimerActive = false;
                return;
            }

            if (isLocalPlayer)
            {
                // 「初めて」の記録はトリガーが喋ったかに関わらず進める。
                // ここを分岐の中に置くと、トリガーで喋った局が数えられず、
                // あとから「初めて限度額」の CSV が場違いなタイミングで出る
                bool isMax = betAmount >= maxBetThreshold;
                bool isMin = betAmount > 0 && betAmount <= minBetThreshold;
                bool firstMax = isMax && !_firstMaxBetPlayed;
                bool firstMin = isMin && !_firstMinBetPlayed;
                if (isMax) _firstMaxBetPlayed = true;
                if (isMin) _firstMinBetPlayed = true;

                bool instant = _betPhaseTimerActive && (Time.time - _betPhaseStartTime) < 2.0f;

                if (betAmount <= 0)
                {
                    // 現状 BettingUI は最小でも1単位を賭けるので、ここには来ない。
                    // サーバーが 0 を許すようになったときのために残してある
                    Trigger(ReactionTrigger.Bet_ZeroGiveUp, ReactionPriority.Situation);
                }
                else if (isMax)
                {
                    // 強い順に見る。仕返し > 迷った末 > テンパイ > ハッタリ。
                    // 落ちる先の CSV は元の分岐をそのまま残している
                    string csv = instant ? "プレイヤーが即座に限度額を賭けた時"
                               : _playerLostLastRound ? "プレイヤーが前の局で負けたのに限度額を賭けた時"
                               : firstMax ? "初めて限度額いっぱいまで賭けた時の開幕のセリフ"
                               : null;

                    if (_playerLostLastRound) PlayOrFallback(ReactionTrigger.Bet_RevengeMax, ReactionPriority.Situation, csv);
                    else if (_betChangeCount >= betHesitateCount) PlayOrFallback(ReactionTrigger.Bet_HesitateMax, ReactionPriority.Situation, csv);
                    else if (IsLocalTenpai()) PlayOrFallback(ReactionTrigger.Bet_TenpaiMax, ReactionPriority.Situation, csv);
                    else PlayOrFallback(ReactionTrigger.Bet_BluffMax, ReactionPriority.Situation, csv);
                }
                else if (isMin)
                {
                    string csv = firstMin ? "初めて最小単位で賭けた時の開幕のセリフ"
                                          : "プレイヤーが少額しか賭けなかった時";

                    if (IsLocalTenpai()) PlayOrFallback(ReactionTrigger.Bet_TenpaiMin, ReactionPriority.Situation, csv);
                    else PlayOrFallback(ReactionTrigger.Bet_NoTenMin, ReactionPriority.Situation, csv);
                }
                // 501〜4999 は元から無言。ここに反応を足すと毎局喋ることになるので触らない
            }
            else
            {
                if (betAmount >= maxBetThreshold)
                {
                    EnqueueCSVDialogue("自分が限度額を賭けた時");
                }
            }

            _betPhaseTimerActive = false;
        }

        private void HandleSkillCastCore(string skillType, bool isLocalPlayer, int costPaid, int hpAfter)
        {
            if (Publish(ReactionEvent.SkillCast, NewContext()
                    .Set(ReactionVars.SkillType, skillType)
                    .Set(ReactionVars.IsMySkill, isLocalPlayer)
                    .Set(ReactionVars.SkillCost, costPaid)
                    .Set(ReactionVars.HpAfterSkill, hpAfter))) return;

            if (isLocalPlayer)
            {
                switch (skillType)
                {
                    case "perspective":
                        Trigger(ReactionTrigger.Skill_PlayerClairvoyance, ReactionPriority.Situation);
                        break;
                    case "boost_hand":
                        Trigger(ReactionTrigger.Skill_PlayerEnhance, ReactionPriority.Situation);
                        break;
                    case "special_victory":
                        Trigger(ReactionTrigger.Skill_PlayerSpecialWin, ReactionPriority.Progress);
                        break;
                }
                return;
            }

            if (hpAfter > 0 && hpAfter <= nearDeathHp
                && Trigger(ReactionTrigger.Skill_NearDeathByCost, ReactionPriority.Progress)) return;
            if (costPaid >= highSkillCost
                && Trigger(ReactionTrigger.Skill_HighCostPaid, ReactionPriority.Situation)) return;
            if (skillType == "perspective")
                Trigger(ReactionTrigger.Skill_EnemyClairvoyance, ReactionPriority.Situation);
        }

        private void CheckPlayerNearDeath()
        {
            if (_playerNearDeathPlayed) return;
            if (_playerHp <= 0 || _playerHp > nearDeathHp) return;
            if (Trigger(ReactionTrigger.Result_PlayerNearDeath, ReactionPriority.Situation))
            {
                _playerNearDeathPlayed = true;
            }
        }

        private void CheckAndPlayDrawReactionCore()
        {
            _drawCount++;

            if (Publish(ReactionEvent.Draw, NewContext()
                    .Set(ReactionVars.DrawCount, _drawCount))) return;

            if (_drawCount >= 2)
            {
                EnqueueCSVDialogue("流局が2回以上続いた時");
            }
            else if (!_firstDrawPlayed)
            {
                _firstDrawPlayed = true;
                EnqueueCSVDialogue("初めて流局した時の最後のセリフ");
            }
        }

        private void PlayDealingReactionCore()
        {
            EnqueueCSVDialogue("山牌構築中のセリフ", false);
        }

        private void HandleRoundStartCore(int round)
        {
            SetCurrentRound(round);

            // 局が変わったので「1局に1回」の枠を戻す
            ReactionRuleEngine.ResetRound(ReactionRuleSet.Load());

            if (Publish(ReactionEvent.RoundStart, NewContext()
                    .Set(ReactionVars.PrevWasDraw, _drawCount > 0 && round > 1)
                    .Set(ReactionVars.PrevWasLoss, _playerLostLastRound)))
                return;

            if (round == 1)
            {
                EnqueueCSVDialogue("1局目のゲーム開始時");
            }
            else
            {
                if (_playerHp <= 2000) EnqueueCSVDialogue("プレイヤーのHPが残りわずかな時の開幕");
                else if (_enemyHp <= 2000) EnqueueCSVDialogue("敵のHPが残りわずかな時の開幕");
                else if (_playerHp >= _enemyHp + 5000) EnqueueCSVDialogue("プレイヤーが圧倒的有利な時の開幕");
                else if (_enemyHp >= _playerHp + 5000) EnqueueCSVDialogue("敵が圧倒的有利な時の開幕");
                else EnqueueCSVDialogue("2局目以降の開幕時");
            }
        }

        private void StartHandSelectionTimerCore()
        {
            _handSelectionStartTime = Time.time;
            _handSelectionTimerActive = true;
        }

        private void StopHandSelectionTimerCore(bool isLocalPlayer)
        {
            if (isLocalPlayer && _handSelectionTimerActive)
            {
                float duration = Time.time - _handSelectionStartTime;

                if (Publish(ReactionEvent.HandConfirmed, NewContext()
                        .Set(ReactionVars.HandDecideSeconds, duration)))
                {
                    _handSelectionTimerActive = false;
                    return;
                }

                if (duration > 15.0f) EnqueueCSVDialogue("プレイヤーが手牌決定に時間をかけている時");
                else if (duration < 3.0f) EnqueueCSVDialogue("プレイヤーが手牌を即決した時");
            }
            _handSelectionTimerActive = false;
        }

        private void StartBetPhaseTimerCore()
        {
            _betPhaseStartTime = Time.time;
            _betPhaseTimerActive = true;
            // 迷った回数は局ごとに数え直す。持ち越すと2局目以降が必ず「散々迷った」になる
            _betChangeCount = 0;
        }

        private void HandleEnemyHandSelectionCore(bool isYakuman, bool isMangan, bool isCheap)
        {
            if (isYakuman) EnqueueCSVDialogue("敵の手が役満の時");
            else if (isMangan) EnqueueCSVDialogue("敵の手が満貫以上の時");
            else if (isCheap) EnqueueCSVDialogue("敵の手が安い時");
        }

        private void HandleAgariCore(bool isLocalPlayerWin, bool isYakuman, bool isDoraBaku, bool isCheap)
        {
            if (Publish(ReactionEvent.Agari, NewContext()
                    .Set(ReactionVars.IsMyWin, isLocalPlayerWin)
                    .Set(ReactionVars.IsYakuman, isYakuman)
                    .Set(ReactionVars.IsDoraBomb, isDoraBaku)
                    .Set(ReactionVars.IsCheapHand, isCheap))) return;

            if (isLocalPlayerWin)
            {
                if (isYakuman) PlayOrFallback(ReactionTrigger.Result_EnemyHitYakuman, ReactionPriority.Progress, "敵が役満に放銃した時");
                else if (isDoraBaku) EnqueueCSVDialogue("ドラ爆でアガった時");
                else if (isCheap) EnqueueCSVDialogue("敵が安い手に放銃した時");
                else EnqueueCSVDialogue("敵が放銃した時");
            }
            else
            {
                if (isYakuman) PlayOrFallback(ReactionTrigger.Result_PlayerHitYakuman, ReactionPriority.Progress, "プレイヤーが役満に放銃した時");
                else if (isDoraBaku) PlayOrFallback(ReactionTrigger.Result_EnemyDoraBomb, ReactionPriority.Progress, "プレイヤーが放銃した時");
                else if (isCheap) EnqueueCSVDialogue("プレイヤーが安い手に放銃した時");
                else EnqueueCSVDialogue("プレイヤーが放銃した時");
            }
        }

        private void HandleGameEndCore(bool isLocalPlayerWin)
        {
            if (Publish(ReactionEvent.MatchEnd, NewContext()
                    .Set(ReactionVars.IsMyWin, isLocalPlayerWin))) return;

            if (isLocalPlayerWin)
            {
                // 女の子が倒れる場面。Result_EnemyKO が無ければ旧 Lose、それも無ければ CSV
                if (Trigger(ReactionTrigger.Result_EnemyKO, ReactionPriority.Progress)) return;
                PlayOrFallback(ReactionTrigger.Lose, ReactionPriority.Progress, "敵のHPが0になった時");
            }
            else
            {
                PlayOrFallback(ReactionTrigger.Win, ReactionPriority.Progress, "プレイヤーのHPが0になった時");
            }
        }
    }
}
