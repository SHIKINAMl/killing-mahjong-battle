using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        private AudioSource voltageIgnitionSource;

        public void PlayVoltageIgnitionSE()
        {
            if (!CanPlay || seSource == null) return;
            var clip = Resources.Load<AudioClip>("Stingers/se_voltage_ignite");
            if (clip == null) return;
            if (voltageIgnitionSource == null)
            {
                var root = new GameObject("VoltageIgnitionSE");
                root.transform.SetParent(transform, false);
                voltageIgnitionSource = root.AddComponent<AudioSource>();
                voltageIgnitionSource.playOnAwake = false;
                voltageIgnitionSource.spatialBlend = 0f;
            }
            voltageIgnitionSource.outputAudioMixerGroup = seSource.outputAudioMixerGroup;
            voltageIgnitionSource.clip = clip;
            voltageIgnitionSource.volume = seVolume * masterVolume * .7f;
            voltageIgnitionSource.pitch = 1f;
            voltageIgnitionSource.Play();
        }

        public void StopVoltageIgnitionSE()
        {
            if (voltageIgnitionSource != null) voltageIgnitionSource.Stop();
        }
    }
}
