using System.Collections;
using System.Collections.Generic;
using KillingMahjong.Common;
using KillingMahjong.EngineData;
using KillingMahjong.Network;

namespace KillingMahjong.UI.Effects
{
    /// <summary>相手の待機スキルを1枚のカットインへまとめる。効果や対局状態は処理しない。</summary>
    public static class EnemySkillBatchPresentation
    {
        public const string EffectId = "skill.cutin.enemy_batch";

        public static string BuildLabel(IReadOnlyList<SkillCastedData> casts)
        {
            var order = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (var cast in casts)
            {
                if (cast == null || string.IsNullOrEmpty(cast.skillType)) continue;
                if (!counts.ContainsKey(cast.skillType)) { order.Add(cast.skillType); counts.Add(cast.skillType, 0); }
                counts[cast.skillType]++;
            }
            var lines = new List<string>();
            foreach (string skill in order)
                lines.Add(SkillNames.GetDisplayName(skill) + (counts[skill] > 1 ? " × " + counts[skill] : ""));
            return string.Join("\n", lines);
        }

        public static IEnumerator Play(PhaseTransitionUI phase, CharacterData character, IReadOnlyList<SkillCastedData> casts)
        {
            if (phase != null)
                yield return phase.PlaySkillCutinAnimationRoutine(BuildLabel(casts), false, character);
        }
    }
}
