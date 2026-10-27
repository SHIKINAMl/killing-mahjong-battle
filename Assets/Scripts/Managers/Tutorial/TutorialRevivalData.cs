using System;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    /// <summary>マルチモードへの導入。敗北後から復活までの台詞。文面は TutorialScenario.asset だけに置く。</summary>
    [Serializable]
    public sealed class TutorialRevivalData
    {
        public List<TutorialLine> enemyLines = new List<TutorialLine>();

        public List<TutorialLine> monologueLines = new List<TutorialLine>();

        public string skillName = "復活";

        public List<TutorialLine> beruruLines = new List<TutorialLine>();
    }
}
