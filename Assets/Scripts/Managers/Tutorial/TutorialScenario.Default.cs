using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class TutorialScenario
    {
        public const string ResourcePath = "Tutorial/TutorialScenario";

        /// <summary>参照未設定時も、通常プレイと同じ正本の複製を使う。コード内に別の台本を持たない。</summary>
        public static TutorialScenario BuildDefault()
        {
            var source = Resources.Load<TutorialScenario>(ResourcePath);
            if (source == null)
                throw new System.InvalidOperationException("チュートリアル台本がありません: " + ResourcePath);
            return Instantiate(source);
        }

        /// <summary>旧5局構成で保存された再開位置も、現在ある局へ収める。</summary>
        public static int ClampStartRound(int roundIndex)
        {
            var source = Resources.Load<TutorialScenario>(ResourcePath);
            return Mathf.Clamp(roundIndex, 0, source != null ? Mathf.Max(0, source.rounds.Count - 1) : 0);
        }
    }
}
