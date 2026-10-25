using System.Collections.Generic;
using System.Linq;

namespace KillingMahjong.Network
{
    /// <summary>対人戦スコアの1行（1人ぶん）。値が見つからなかった列は Has* が false。</summary>
    public sealed class VersusRankingRow
    {
        public int Rank;
        public string Name;
        public long Total;
        public long Rounds;
        public long MaxDamage;
        public bool HasRounds, HasMaxDamage;
    }

    /// <summary>
    /// 3つあったランキング（累計獲得点数・局数・最大打点）を、1人1行の「対人戦スコア」にまとめる（2026-10-25）。
    /// プランナー（yabazaemon さん）の指示と見本の絵。並びは
    ///   1. 累計獲得点数が高い順
    ///   2. 同点なら、局数が少ないほうが上
    ///   3. それでも同じなら、最大打点が大きいほうが上
    ///   4. それでも同じなら同じ順位にして「1、2、2、3」と詰める。並びはサーバーが返した順のまま
    ///
    /// **これはサーバーが1行にまとめた一覧を送ってくれるまでのつなぎ。**
    /// いまのサーバーは種類ごとに別の一覧（各人のそれぞれの最大値）しか返さないので、名前で突き合わせている。
    /// そのため、1行の3つの値は別々の対局の数字になりうるし、同じ名前の別の人は1人にまとまってしまう。
    /// サーバーがまとめた一覧を返すようになったら、ここを差し替える（表示側はこの行の形のままでよい）。
    /// </summary>
    public static class VersusRanking
    {
        public const string TotalKind = "match_points", RoundsKind = "rounds", DamageKind = "max_damage";

        /// <summary>取りに行く種類。</summary>
        public static readonly string[] Kinds = { TotalKind, RoundsKind, DamageKind };

        /// <summary>
        /// 種類ごとに取る件数。上位10人を組むのに、局数と最大打点の一覧からも同じ人を探すので、サーバーの上限（50）まで取る。
        /// </summary>
        public const int FetchLimit = 50;

        /// <summary><see cref="RankingFetcher.Latest"/> から上位 <paramref name="count"/> 人を組む。</summary>
        public static List<VersusRankingRow> Assemble(IDictionary<string, RankingData> latest, int count)
        {
            var rows = new List<VersusRankingRow>();
            if (latest == null || !latest.TryGetValue(TotalKind, out RankingData totals) || totals?.entries == null) return rows;
            latest.TryGetValue(RoundsKind, out RankingData rounds);
            latest.TryGetValue(DamageKind, out RankingData damage);
            var roundsLeft = rounds?.entries != null ? new List<RankingEntry>(rounds.entries) : new List<RankingEntry>();
            var damageLeft = damage?.entries != null ? new List<RankingEntry>(damage.entries) : new List<RankingEntry>();

            foreach (var e in totals.entries)
            {
                if (e == null) continue;
                var row = new VersusRankingRow { Name = e.name, Total = e.value };
                var r = TakeByName(roundsLeft, e.name);
                if (r != null) { row.Rounds = r.value; row.HasRounds = true; }
                var d = TakeByName(damageLeft, e.name);
                if (d != null) { row.MaxDamage = d.value; row.HasMaxDamage = true; }
                rows.Add(row);
            }

            // OrderBy は同じ値の並びを崩さない（＝サーバーが返した順のまま）
            var sorted = rows
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.HasRounds ? x.Rounds : long.MaxValue)
                .ThenByDescending(x => x.HasMaxDamage ? x.MaxDamage : long.MinValue)
                .Take(count)
                .ToList();

            int rank = 0;
            VersusRankingRow prev = null;
            foreach (var row in sorted)
            {
                if (prev == null || row.Total != prev.Total || row.Rounds != prev.Rounds || row.MaxDamage != prev.MaxDamage
                    || row.HasRounds != prev.HasRounds || row.HasMaxDamage != prev.HasMaxDamage) rank++;
                row.Rank = rank;
                prev = row;
            }
            return sorted;
        }

        /// <summary>同じ名前の最初の1件を取り出す（同じ名前が何件もあれば、上のものから順に使う）。</summary>
        private static RankingEntry TakeByName(List<RankingEntry> entries, string name)
        {
            int i = entries.FindIndex(x => x != null && x.name == name);
            if (i < 0) return null;
            var found = entries[i];
            entries.RemoveAt(i);
            return found;
        }
    }
}
