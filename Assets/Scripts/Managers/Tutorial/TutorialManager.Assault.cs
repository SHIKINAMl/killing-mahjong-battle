using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Common;
using KillingMahjong.Network;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        private bool _tutorialEnemyAssaultActive;

        /// <summary>能力についての図の末尾。ここでは発動だけ見せ、効果の説明はロン後に行う。</summary>
        private IEnumerator RunEnemyAssaultIntroduction(TutorialRoundData data)
        {
            if (_tutorialEnemyAssaultActive) yield break;
            yield return PlaySceneBreakAndWait();
            yield return PlayLines(data.beforeEnemyAssaultLines);
            if (_aborted) yield break;
            int cost = GameRules.GetSkillCost(SkillNames.Assault, 0);
            _enemyHp = Mathf.Max(0, _enemyHp - cost);
            _tutorialEnemyAssaultActive = true;
            var controller = gameUIManager != null ? gameUIManager.SkillController : null;
            var phase = gameUIManager != null ? gameUIManager.PhaseTransitionUI : null;
            bool wasActive = phase != null && phase.gameObject.activeSelf;
            if (phase != null && !wasActive) { phase.gameObject.SetActive(true); yield return null; }
            try
            {
                if (controller != null)
                    yield return controller.PlayTutorialSkillRoutine(new SkillCastedData {
                        player_id = "tutorial-enemy", skillType = SkillNames.Assault,
                        cost = cost, health = _enemyHp,
                    });
                ApplyHpToUI();
            }
            finally { if (phase != null && !wasActive) phase.gameObject.SetActive(false); }
        }

        private IEnumerator ExplainTutorialAssault(TutorialDefeatRonData ron, bool deadEnd, int damage)
        {
            yield return PlayLines(deadEnd ? ron.deadEndRonLines : ron.revealRonLines);
            if (deadEnd) yield return PlaySceneBreakAndWait();
            yield return PlayLines(ron.assaultExplainLines);
            var final = deadEnd ? ron.deadEndAssaultFinalLines : ron.revealAssaultFinalLines;
            var resolved = new List<TutorialLine>();
            foreach (var line in final)
                resolved.Add(new TutorialLine(string.Format(line.text, damage), line.speaker) { id = line.id });
            yield return PlayLines(resolved);
        }
    }
}
