using KillingMahjong.Managers;
using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>手牌選択の表示に同期する短いSE。本編とコレクションで共用する。</summary>
    public static class HandSelectionFeedbackAudio
    {
        private static AudioClip roleEntry, rankImpact;
        private static float lastRoleAt = float.NegativeInfinity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { lastRoleAt = float.NegativeInfinity; }

        public static void PlayRoleEntry()
        {
            var audio = AudioManager.Instance;
            if (audio == null || !audio.AudioEnabled || Time.unscaledTime - lastRoleAt < .04f) return;
            if (roleEntry == null) roleEntry = Resources.Load<AudioClip>("Se/HandSelection/role_entry");
            if (roleEntry == null) return;
            lastRoleAt = Time.unscaledTime;
            audio.PlaySE(roleEntry);
        }

        public static void PlayRankImpact()
        {
            var audio = AudioManager.Instance;
            if (audio == null || !audio.AudioEnabled) return;
            if (rankImpact == null) rankImpact = Resources.Load<AudioClip>("Se/HandSelection/rank_impact");
            if (rankImpact != null) audio.PlaySE(rankImpact);
        }
    }
}
