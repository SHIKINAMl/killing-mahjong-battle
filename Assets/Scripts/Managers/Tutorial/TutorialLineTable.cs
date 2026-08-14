using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KillingMahjong.Managers.Tutorial
{
    /// <summary>
    /// チュートリアル台本の**セリフだけ**を差し替える表。
    ///
    /// 台本の構造（何局あるか、どこで能力を実演するか、どのボタンを開放するか）は
    /// <see cref="TutorialScenario.BuildDefault"/> のコードが持ったままで、
    /// **文字列だけをここから上書きする。**
    ///
    /// **セリフごと ScriptableObject に移さないのはなぜか。**
    /// 台本アセットを作って全部そちらへ移すと、`BuildDefault()` と二重管理になる。
    /// スキルのコスト表で同じことをして実際にズレた（`SERVER_REQUESTS_20260801.md` A-7）。
    /// 構造はコード、文字はこの表、と持ち場を分ければ食い違いようがない。
    ///
    /// **ID は手で振らない。** 121行に手でラベルを付けると、行を1つ増やしただけで
    /// ずれるか、付け忘れて無言になる。リスト名と添字から機械的に作る:
    ///
    ///     r0.introLines[2]                     … 第1局の導入3行目
    ///     r3.abilityShowcases[1].afterLines[0] … 第4局の2つ目の能力実演のあと
    ///     ending[0]                            … 全局終了後
    ///
    /// 表に無い ID はコードの文字列がそのまま使われる。
    /// **消えて無言になることはない**ので、途中まで書いた TSV でも安全に読める。
    /// </summary>
    public class TutorialLineTable : ScriptableObject
    {
        /// <summary>`Resources.Load` で引く場所。Editor の取り込みツールが同じ名前で書く。</summary>
        public const string ResourcePath = "TutorialLines";

        [Serializable]
        public class Row
        {
            [Tooltip("リスト名と添字から作った識別子。手で書き換えない")]
            public string id;

            [Tooltip("話者。Enemy / Player / System のいずれか")]
            public TutorialSpeaker speaker = TutorialSpeaker.Enemy;

            [TextArea(1, 4)]
            public string text;
        }

        public List<Row> rows = new List<Row>();

        /// <summary>
        /// 表の中身を台本へ流し込む。ID が一致した行だけ差し替える。
        /// </summary>
        public void ApplyTo(TutorialScenario scenario)
        {
            if (scenario == null || rows == null || rows.Count == 0) return;

            var map = new Dictionary<string, Row>(rows.Count);
            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrEmpty(row.id)) continue;
                map[row.id] = row;   // 同じ ID が重複したら後勝ち
            }

            Walk(scenario, (id, line) =>
            {
                Row row;
                if (!map.TryGetValue(id, out row)) return;
                if (row.text != null) line.text = row.text;
                line.speaker = row.speaker;
            });
        }

        /// <summary>
        /// 台本の全セリフを ID 付きで数え上げる。取り込みツールの書き出しに使う。
        /// </summary>
        public static List<Row> Dump(TutorialScenario scenario)
        {
            var list = new List<Row>();
            Walk(scenario, (id, line) => list.Add(new Row
            {
                id = id,
                speaker = line.speaker,
                text = line.text
            }));
            return list;
        }

        /// <summary>
        /// 台本の中の <see cref="TutorialLine"/> を、決まった順番で全部たどる。
        ///
        /// **リストは名前で引く。** 順番で引くと、フィールドを1つ足しただけで
        /// 既存の ID が全部ずれて、書いてもらった TSV が丸ごと効かなくなる。
        /// </summary>
        private static void Walk(TutorialScenario scenario, Action<string, TutorialLine> visit)
        {
            if (scenario == null || visit == null) return;

            if (scenario.rounds != null)
            {
                for (int r = 0; r < scenario.rounds.Count; r++)
                {
                    var round = scenario.rounds[r];
                    if (round == null) continue;

                    string prefix = "r" + r + ".";

                    foreach (var f in LineListFields(round.GetType()))
                    {
                        var lines = f.GetValue(round) as List<TutorialLine>;
                        VisitList(prefix + f.Name, lines, visit);
                    }

                    // 能力の実演は入れ子。局のリストと ID がぶつからないよう名前を挟む
                    if (round.abilityShowcases != null)
                    {
                        for (int s = 0; s < round.abilityShowcases.Count; s++)
                        {
                            var show = round.abilityShowcases[s];
                            if (show == null) continue;

                            string sp = prefix + "abilityShowcases[" + s + "].";
                            VisitList(sp + "beforeLines", show.beforeLines, visit);
                            VisitList(sp + "afterLines", show.afterLines, visit);
                        }
                    }
                }
            }

            VisitList("ending", scenario.endingLines, visit);
        }

        private static void VisitList(string listId, List<TutorialLine> lines, Action<string, TutorialLine> visit)
        {
            if (lines == null) return;
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] == null) continue;
                visit(listId + "[" + i + "]", lines[i]);
            }
        }

        /// <summary>
        /// 局データの中の `List&lt;TutorialLine&gt;` なフィールドを名前順に返す。
        /// 名前順にするのは、宣言順に依存すると並べ替えで ID が変わるため。
        /// </summary>
        private static List<FieldInfo> LineListFields(Type type)
        {
            List<FieldInfo> cached;
            if (_fieldCache.TryGetValue(type, out cached)) return cached;

            cached = new List<FieldInfo>();
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType == typeof(List<TutorialLine>)) cached.Add(f);
            }
            cached.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            _fieldCache[type] = cached;
            return cached;
        }

        private static readonly Dictionary<Type, List<FieldInfo>> _fieldCache =
            new Dictionary<Type, List<FieldInfo>>();
    }
}
