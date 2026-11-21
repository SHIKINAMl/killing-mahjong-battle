using System.Collections.Generic;
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        // 台本の満貫13枚は必ず残す。余分な同種牌があっても、位置で区別する。
        private static HashSet<int> GetManganReservedWallIndexes(TutorialRoundData round)
        {
            var needed = new List<int>(round.manganHandBaseIds);
            var reserved = new HashSet<int>();
            for (int i = 0; i < round.wallBaseIds.Count; i++)
            {
                int at = needed.IndexOf(round.wallBaseIds[i]);
                if (at < 0) continue;
                reserved.Add(i);
                needed.RemoveAt(at);
            }
            return reserved;
        }

        private bool CanPickIntroMulliganWallIndex(int index) => _round != null
            && index >= 0 && index < _round.wallBaseIds.Count
            && !GetManganReservedWallIndexes(_round).Contains(index);

        private List<int> GetIntroMulliganReplacements()
        {
            var board = BoardStateManager.Instance;
            if (_round == null || board == null
                || _round.mulliganReplacementBaseIds.Count != MulliganChoiceUI.KeepCount) return null;
            var reserve = TutorialSkillStock.Remaining(board.OriginalWallTiles,
                board.OriginalEnemyWallTiles, _round.doraBaseId);
            var result = TutorialTiles.EncodeAll(_round.mulliganReplacementBaseIds, _round.doraBaseId);
            foreach (int tile in result)
            {
                // 繰り返し練習して候補が尽きたら、5枚目を作らず発動前に止める。
                if (!reserve.Remove(tile)) return null;
            }
            return result;
        }
    }
}
