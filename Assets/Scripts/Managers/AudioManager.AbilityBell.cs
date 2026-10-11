using UnityEngine;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        private AudioClip abilityBellClip;
        private AudioSource abilityBellSource;

        /// <summary>能力ベルを押して一覧を開いた合図。BGMや他のSEを止めず、一定の音程で鳴らす。</summary>
        public void PlayAbilityBellSE()
        {
            if (!CanPlay || seSource == null) return;
            if (abilityBellClip == null) abilityBellClip = Resources.Load<AudioClip>("Stingers/se_ability_bell");
            if (abilityBellClip == null)
            {
                Debug.LogWarning("[AudioManager] 能力ベル音がありません: Resources/Stingers/se_ability_bell");
                return;
            }
            if (abilityBellSource == null)
            {
                var go = new GameObject("AbilityBellSource");
                go.transform.SetParent(transform, false);
                abilityBellSource = go.AddComponent<AudioSource>();
                abilityBellSource.playOnAwake = false;
                abilityBellSource.loop = false;
                abilityBellSource.spatialBlend = 0f;
                abilityBellSource.outputAudioMixerGroup = seSource.outputAudioMixerGroup;
            }
            abilityBellSource.clip = abilityBellClip;
            abilityBellSource.pitch = 1f;
            abilityBellSource.volume = seVolume * masterVolume;
            // 連打で余韻が無制限に重ならないよう、ベル専用の1声だけを鳴らし直す。
            abilityBellSource.Play();
        }

        /// <summary>演出コレクションの試写中断用。他のSEやBGMには触れない。</summary>
        public void StopAbilityBellSE()
        {
            if (abilityBellSource != null) abilityBellSource.Stop();
        }
    }
}
