using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        /// <summary>
        /// プロシージャル音声（シンセサイザー）を1つの波形で再生する
        /// </summary>
        public void PlaySynthSound(SynthWaveType type, float startFreq, float endFreq, float duration, float volume = 1.0f)
        {
            if (synth != null)
            {
                synth.Play(type, startFreq, endFreq, duration, volume);
            }
        }

        /// <summary>
        /// プロシージャル音声（シンセサイザー）を2つの波形でミックスして再生する
        /// </summary>
        public void PlaySynthSoundDual(SynthWaveType type1, SynthWaveType type2, float startFreq, float endFreq, float duration, float volume = 1.0f)
        {
            if (synth != null)
            {
                synth.PlayDual(type1, type2, true, startFreq, endFreq, duration, volume);
            }
        }

        // --- BGM Control ---
        // 将来的にBGMのBPM（テンポ）同期やビート検知処理をここに追加できます
        public void PlayBGM(AudioClip clip = null, bool restartIfSame = false)
        {
            if (bgmSource == null) return;
            if (clip == null) clip = defaultBgm;
            if (clip == null) return;

            if (bgmSource.clip == clip && bgmSource.isPlaying && !restartIfSame) return;

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        // --- SE Control ---
        public void PlaySE(AudioClip clip)
        {
            if (clip != null && seSource != null)
            {
                seSource.PlayOneShot(clip, seVolume * masterVolume);
            }
        }

        // --- Discard SE Control ---
        public void PlayDiscardSE(AudioClip clip = null)
        {
            if (clip == null) clip = discardSE;
            if (clip == null || discardSeSource == null) return;

            // 一定時間経過していればピッチをリセット
            if (Time.time - lastDiscardTime > PITCH_RESET_TIME)
            {
                currentDiscardPitch = 1.0f;
            }
            else
            {
                // discardPitchStepSemitones 半音ぶん上げる（1オクターブ = 12半音）
                currentDiscardPitch *= Mathf.Pow(2f, discardPitchStepSemitones / 12f);
                if (currentDiscardPitch > MAX_PITCH)
                {
                    currentDiscardPitch = MAX_PITCH;
                }
            }

            discardSeSource.pitch = currentDiscardPitch;
            discardSeSource.volume = seVolume * masterVolume;
            discardSeSource.PlayOneShot(clip);

            lastDiscardTime = Time.time;
        }

        // --- ASMR Specific SE Control ---
        public void PlayHoverSE()
        {
            if (hoverSE != null) PlaySE(hoverSE);
        }

        public void PlayPickTileSE()
        {
            if (pickTileSE != null) PlaySE(pickTileSE);
            else if (selectTileSE != null) PlaySE(selectTileSE); // フォールバック
        }

        public void PlayDrawTileSE()
        {
            if (drawTileSE != null) PlaySE(drawTileSE);
        }

        public void PlayUIPopupSE()
        {
            if (uiPopupSE != null) PlaySE(uiPopupSE);
        }

        public void PlayPaperSlideSE()
        {
            if (paperSlideSE != null) PlaySE(paperSlideSE);
        }

    }
}

