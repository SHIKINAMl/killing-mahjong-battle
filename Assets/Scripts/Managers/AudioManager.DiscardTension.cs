using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        private readonly Dictionary<int, float> discardTensionOwners = new Dictionary<int, float>();
        private float discardTensionGain = 1f;
        public float DiscardTensionGain => discardTensionGain;

        // 設定値を変えず、演出の持ち主が生きている間だけBGMを静める。
        public void SetDiscardTension(Object owner, float tension)
        {
            if (owner == null) return;
            discardTensionOwners[owner.GetInstanceID()] = Mathf.Lerp(1f, .75f, Mathf.Clamp01(tension));
            RefreshDiscardTension();
        }

        public void ReleaseDiscardTension(Object owner)
        {
            if (owner == null) return;
            discardTensionOwners.Remove(owner.GetInstanceID());
            RefreshDiscardTension();
        }

        private void RefreshDiscardTension()
        {
            float gain = 1f;
            foreach (float value in discardTensionOwners.Values) gain = Mathf.Min(gain, value);
            if (Mathf.Approximately(gain, discardTensionGain)) return;
            discardTensionGain = gain;
            ApplyBgmVolumes();
        }

        public void PlayVoltageBreakSE() => PlayStingerPitched("se_voltage_up", .65f, PitchJitter.None, .45f);
    }
}
