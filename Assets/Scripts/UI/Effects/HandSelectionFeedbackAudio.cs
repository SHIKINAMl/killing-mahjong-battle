using KillingMahjong.Managers;
using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>手牌選択の表示に同期する短いSE。本編とコレクションで共用する。</summary>
    public static class HandSelectionFeedbackAudio
    {
        private static AudioClip roleEntry, rankImpact, waitFlight, waitArrival;
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

        // 移送のオブジェクトに持たせることで、中断したときは音も一緒に止まる。
        public static AudioSource CreateWaitTransferSource(GameObject owner, bool arrival)
        {
            if (waitFlight == null) waitFlight = Resources.Load<AudioClip>("Se/HandSelection/wait_flight");
            if (waitArrival == null) waitArrival = Resources.Load<AudioClip>("Se/HandSelection/wait_arrival");
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = arrival ? waitArrival : waitFlight;
            UpdateWaitTransferVolume(source);
            return source;
        }

        public static void UpdateWaitTransferVolume(AudioSource source)
        {
            var audio = AudioManager.Instance;
            source.volume = audio != null && audio.AudioEnabled ? audio.masterVolume * audio.seVolume : 0f;
        }
    }
}
