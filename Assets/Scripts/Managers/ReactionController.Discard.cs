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

        private static string SuitName(KillingMahjong.TileCategory category)
        {
            switch (category)
            {
                case KillingMahjong.TileCategory.Manzu: return "萬子";
                case KillingMahjong.TileCategory.Pinzu: return "筒子";
                case KillingMahjong.TileCategory.Souzu: return "索子";
                default: return "字牌";
            }
        }

        private void CheckDiscardConditionsCore(int tileId, bool isLocalPlayer)
        {
            bool playedSpecial = false;
            var tData = new KillingMahjong.TileData(tileId);

            if (_currentRound == 1)
            {
                _round1DiscardCount++;
                if (_round1DiscardCount == 1 && !isLocalPlayer) { playedSpecial = true; EnqueueCSVDialogue("相手が第１局目で先行"); }
                else if (_round1DiscardCount == 2 && !isLocalPlayer) { playedSpecial = true; EnqueueCSVDialogue("相手が第一局目で後攻"); }
                
                if (_lastDiscardedTileId >= 0)
                {
                    var lastTile = new KillingMahjong.TileData(_lastDiscardedTileId);
                    if (tData.Category == lastTile.Category && tData.Number == lastTile.Number)
                    {
                        if (isLocalPlayer && !_firstPlayerAwasePlayed) { _firstPlayerAwasePlayed = true; playedSpecial = true; EnqueueCSVDialogue("自分が第一局目で初めて合わせを行う"); }
                        else if (!isLocalPlayer && !_firstEnemyAwasePlayed) { _firstEnemyAwasePlayed = true; playedSpecial = true; EnqueueCSVDialogue("相手が第一局目で合わせ(敵の直前の打牌同じ牌を打つこと)を行う"); }
                    }
                }
            }

            bool isHonor = tData.Category == KillingMahjong.TileCategory.Honor;
            bool isMiddle = !isHonor && tData.Number >= 4 && tData.Number <= 6;
            bool isTerminalOrHonor = isHonor || tData.Number == 1 || tData.Number == 9;
            bool isSuji = false;
            bool isSameAsBefore = false;

            // **スジと同一牌の判定を、分岐の外へ出してある。**
            // ルールへ渡すには Publish の前に値が要るため。中身は元の計算そのまま
            if (isLocalPlayer)
            {
                if (!isHonor)
                {
                    int suji1 = tData.Number - 3;
                    int suji2 = tData.Number + 3;
                    foreach (var histId in _playerDiscardHistory)
                    {
                        var hTile = new KillingMahjong.TileData(histId);
                        if (hTile.Category == tData.Category && (hTile.Number == suji1 || hTile.Number == suji2))
                        {
                            isSuji = true; break;
                        }
                    }
                }

                isSameAsBefore = _playerDiscardHistory.Count > 0 &&
                                 new KillingMahjong.TileData(_playerDiscardHistory[_playerDiscardHistory.Count - 1]).Category == tData.Category &&
                                 new KillingMahjong.TileData(_playerDiscardHistory[_playerDiscardHistory.Count - 1]).Number == tData.Number;
            }

            // 字牌の連続数は「この牌を数に入れた」値で渡す。
            // 実際の加算は下で行うので、ここでは1つ先を読んでいる
            int honorStreakForRule = isLocalPlayer
                ? (isHonor ? _playerConsecutiveHonorCount + 1 : 0)
                : 0;

            float turnElapsed = 0f;
            if (isLocalPlayer && PlayerActivityWatcher.Instance != null)
                turnElapsed = PlayerActivityWatcher.Instance.TurnElapsedSeconds;

            bool ruleHandled = Publish(ReactionEvent.Discard, NewContext()
                .Set(ReactionVars.IsMyDiscard, isLocalPlayer)
                .Set(ReactionVars.TileSuit, SuitName(tData.Category))
                .Set(ReactionVars.TileNumber, tData.Number)
                .Set(ReactionVars.IsRedDora, tData.IsRedDora)
                .Set(ReactionVars.IsYakuhai, isHonor && tData.Number >= 5 && tData.Number <= 7)
                .Set(ReactionVars.IsOtakaze, isHonor && tData.Number >= 1 && tData.Number <= 4)
                .Set(ReactionVars.IsCenterTile, isMiddle)
                .Set(ReactionVars.IsSameAsPrev, isSameAsBefore)
                .Set(ReactionVars.IsSuji, isSuji)
                .Set(ReactionVars.HonorStreak, honorStreakForRule)
                .Set(ReactionVars.TurnElapsedSeconds, turnElapsed));

            if (ruleHandled) playedSpecial = true;

            if (isLocalPlayer)
            {
                // Discard_* のトリガーを先に試し、無ければ従来の CSV へ落とす。
                // 対応は REACTION_LINES.tsv のセリフから読み取ったもの:
                //   Discard_SafeTile … 「まずは無難な字牌から？」→ オタ風
                //   Discard_RawYakuhai … 「生牌の字牌を切るなんて」→ 役牌
                // スジ牌に対応するトリガーは無いので CSV のまま
                if (!ruleHandled)
                {
                    if (tData.IsRedDora) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_RedDora, ReactionPriority.Situation, "プレイヤーが赤ドラを切った時"); }
                    else if (isHonor && tData.Number >= 1 && tData.Number <= 4) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_SafeTile, ReactionPriority.Situation, "プレイヤーがオタ風を切った時"); }
                    else if (isHonor && tData.Number >= 5 && tData.Number <= 7) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_RawYakuhai, ReactionPriority.Situation, "プレイヤーが役牌を切った時"); }
                    else if (isMiddle) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_CenterTile, ReactionPriority.Situation, "プレイヤーがド真ん中の牌を切った時"); }
                    else if (isSameAsBefore) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_SameTileStreak, ReactionPriority.Situation, "プレイヤーが前の捨て牌と同じ牌を切った時"); }
                    else if (isSuji) { playedSpecial = true; EnqueueCSVDialogue("プレイヤーがスジ牌を切った時"); }
                }

                if (isHonor) _playerConsecutiveHonorCount++;
                else _playerConsecutiveHonorCount = 0;

                if (!ruleHandled && _playerConsecutiveHonorCount >= 3) { playedSpecial = true; PlayOrFallback(ReactionTrigger.Discard_HonorStreak, ReactionPriority.Situation, "プレイヤーが字牌を連続で切った時"); }

                _playerDiscardHistory.Add(tileId);
            }
            else
            {
                if (!ruleHandled && tData.IsRedDora) { playedSpecial = true; EnqueueCSVDialogue("敵が赤ドラを切る時"); }
                
                bool isTsumogiri = _enemyDiscardHistory.Count > 0 && tileId == _lastDiscardedTileId; // 厳密なツモ切り判定はサーバーから来る情報に依存するため簡易化
                
                _enemyDiscardHistory.Add(tileId);
            }

            if (isTerminalOrHonor && !_firstTerminalHonorPlayed)
            {
                _firstTerminalHonorPlayed = true;
                if (!playedSpecial) { playedSpecial = true; EnqueueCSVDialogue("初めて一九字牌を切った時のセリフ"); }
            }
            else if (!isHonor && tData.Number >= 2 && tData.Number <= 8 && !_firstMiddleTilePlayed)
            {
                _firstMiddleTilePlayed = true;
                if (!playedSpecial) { playedSpecial = true; EnqueueCSVDialogue("初めて2-8の牌をを切った時のセリフ"); }
            }

            _lastDiscardedTileId = tileId;

            if (!playedSpecial)
            {
                string tileName = tData.GetTileName();
                EnqueueDiscardReaction(tileId, isLocalPlayer, tileName);
            }
        }
    }
}
