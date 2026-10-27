using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        private readonly Dictionary<int, float> readingFocusOwners = new Dictionary<int, float>();
        public float ReadingFocusGain { get; private set; } = 1f;

        public void SetReadingFocus(Object owner, float gain)
        {
            if (owner == null) return;
            readingFocusOwners[owner.GetInstanceID()] = Mathf.Clamp01(gain);
            RefreshReadingFocus();
        }

        public void ReleaseReadingFocus(Object owner)
        {
            if (owner == null) return;
            readingFocusOwners.Remove(owner.GetInstanceID());
            RefreshReadingFocus();
        }

        private void RefreshReadingFocus()
        {
            float gain = 1f;
            foreach (float value in readingFocusOwners.Values) gain = Mathf.Min(gain, value);
            if (Mathf.Approximately(gain, ReadingFocusGain)) return;
            ReadingFocusGain = gain;
            ApplyBgmVolumes();
        }

        public void PlayReadingBoardPlaceSE()
        {
            if (!CanPlay || discardSE == null) return;
            PlayOneShotWithPitch(discardSE, seVolume * masterVolume * .35f, .8f);
        }
    }
}
