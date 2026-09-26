using UnityEngine;
using System.Collections;
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
            if (!CanPlay) return;
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
            if (!CanPlay) return;
            if (synth != null)
            {
                synth.PlayDual(type1, type2, true, startFreq, endFreq, duration, volume);
            }
        }

        // --- BGM Control ---
        // 将来的にBGMのBPM（テンポ）同期やビート検知処理をここに追加できます
        public void PlayBGM(AudioClip clip = null, bool restartIfSame = false)
        {
            if (!CanPlay) return;
            if (bgmSource == null) return;
            if (clip == null) clip = defaultBgm;
            if (clip == null) return;

            if (bgmSource.clip == clip && bgmSource.isPlaying && !restartIfSame) return;

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        /// <summary>
        /// 曲を <paramref name="seconds"/> 秒かけて小さくしてから止める（2026-09-20）。
        ///
        /// **場面の切れ目で「終わった」と聞かせたいときだけ使う。**
        /// 不意打ちで断ちたいときは <see cref="CutBgmImmediately"/>。
        /// </summary>
        public void FadeOutBgm(float seconds)
        {
            if (bgmSource == null || !bgmSource.isPlaying || seconds <= 0f)
            {
                StopBGM();
                return;
            }
            StartCoroutine(FadeOutBgmRoutine(seconds));
        }

        private IEnumerator FadeOutBgmRoutine(float seconds)
        {
            float from = bgmSource.volume;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (bgmSource == null) yield break;
                bgmSource.volume = Mathf.Lerp(from, 0f, t / seconds);
                yield return null;
            }

            StopBGM();
            // 次に鳴らすときのために音量を戻しておく
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
        }

        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();

            // **層とドラムも止める（2026-09-19）。** 場のBGMは bgmSource ではなく層（4本）で鳴り、
            // ドラムも別の音源でループしている。bgmSource だけ止めていたので、
            // 対局からタイトルへ戻っても対局の曲が鳴り続けていた
            StopLayeredBgm();
            StopPairBgm();   // 採用した2曲も別の音源で鳴っているので、同じ理由でここで止める
            if (drumSource != null && drumSource.isPlaying) drumSource.Stop();

            // 止めたあと同じフェイズで呼び直されても鳴らし直せるようにしておく。
            // これが無いと、タイトルへ戻ってもう一度対局を始めたときに
            // 「もうその曲を鳴らしている」と判定されて無音のままになる。
            ResetPhaseBgmState();
        }

        // --- SE Control ---
        public void PlaySE(AudioClip clip)
        {
            if (!CanPlay) return;
            if (clip != null && seSource != null)
            {
                seSource.PlayOneShot(clip, seVolume * masterVolume);
            }
        }

        // --- Discard SE Control ---
        public void PlayDiscardSE(AudioClip clip = null)
        {
            if (!CanPlay) return;
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

        // 以下、何度も鳴る音は**ピッチをばらして連打も絞る**（2026-09-12）。
        // 同じ波形を毎回そのまま出すと、数十回鳴るうちに作り物だと分かってしまう。
        // 手法は AudioManager.Variation.cs を参照（Inscryption の AudioParams を参考にした）。

        public void PlayPickTileSE()
        {
            // 牌に触れる音。素材感を保ちたいので振り幅は最小にする。
            // 連打を絞らないと、牌を高速で触られたとき機関銃のように鳴る。
            if (pickTileSE != null) PlaySE(pickTileSE, PitchJitter.VerySmall, 0.04f);
            else if (selectTileSE != null) PlaySE(selectTileSE, PitchJitter.VerySmall, 0.04f); // フォールバック
        }

        public void PlayDrawTileSE()
        {
            if (drawTileSE != null) PlaySE(drawTileSE, PitchJitter.VerySmall, 0.04f);
        }

        public void PlayUIPopupSE()
        {
            if (uiPopupSE != null) PlaySE(uiPopupSE, PitchJitter.VerySmall, 0.08f);
        }

        public void PlayPaperSlideSE()
        {
            if (paperSlideSE != null) PlaySE(paperSlideSE, PitchJitter.VerySmall, 0.10f);
        }

        public void PlayHoverSE()
        {
            // カーソルを乗せるだけで鳴るので、**一番強く絞る**。
            // 牌の上をなぞられると毎フレーム近く飛んでくる。
            if (hoverSE != null) PlaySE(hoverSE, PitchJitter.Small, 0.06f);
        }

    }
}

