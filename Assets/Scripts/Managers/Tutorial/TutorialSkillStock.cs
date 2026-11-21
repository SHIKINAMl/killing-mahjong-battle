using System;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    // 通信しないチュートリアルの実演用。通常対局の判定や配牌には使わない。
    internal static class TutorialSkillStock
    {
        internal static List<int> Remaining(IList<int> wall, IList<int> enemyWall, int dora)
        {
            var pool = new List<int>();
            for (int kind = 0; kind <= TutorialTiles.MaxKind; kind++)
                for (int copy = 0; copy < 4; copy++) pool.Add(TutorialTiles.Encode(kind, kind == dora));
            Remove(pool, wall);
            Remove(pool, enemyWall);
            return pool;
        }

        private static void Remove(List<int> pool, IList<int> tiles)
        {
            if (tiles == null) return;
            foreach (int tile in tiles)
            {
                int kind = TutorialTiles.BaseOf(tile);
                int index = pool.FindIndex(value => TutorialTiles.BaseOf(value) == kind);
                if (index < 0) throw new InvalidOperationException("チュートリアルの牌が同種4枚を超えています。");
                pool.RemoveAt(index);
            }
        }

        internal static List<int> Draw(List<int> pool, int count, Random random)
        {
            if (count > pool.Count) throw new InvalidOperationException("実演用の予備牌が足りません。");
            var result = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int index = random.Next(pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        internal static bool MatchesSuit(int tile, string suit)
        {
            int kind = TutorialTiles.BaseOf(tile);
            if (suit == null) return true;
            if (suit == "manzu") return kind >= TutorialTiles.Man(1) && kind <= TutorialTiles.Man(9);
            if (suit == "pinzu") return kind >= TutorialTiles.Pin(1) && kind <= TutorialTiles.Pin(9);
            if (suit == "souzu") return kind >= TutorialTiles.Sou(1) && kind <= TutorialTiles.Sou(9);
            if (suit == "jihai") return kind == TutorialTiles.Ton || kind == TutorialTiles.Sha;
            return false;
        }
    }
}
