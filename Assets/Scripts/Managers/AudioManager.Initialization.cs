using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        private void InitializeSources()
        {
            // BGM専用の子オブジェクトを作成（ローパスフィルターがSEやボイスに影響しないように分離）
            if (bgmSource == null || bgmSource.gameObject == this.gameObject)
            {
                GameObject bgmObj = new GameObject("BGM_Source");
                bgmObj.transform.SetParent(this.transform);
                AudioSource newBgmSource = bgmObj.AddComponent<AudioSource>();
                
                if (bgmSource != null)
                {
                    newBgmSource.volume = bgmSource.volume;
                    newBgmSource.loop = bgmSource.loop;
                    newBgmSource.playOnAwake = bgmSource.playOnAwake;
                    newBgmSource.clip = bgmSource.clip;
                    Destroy(bgmSource); // 元のAudioSourceを削除
                }
                bgmSource = newBgmSource;
            }

            if (seSource == null) seSource = gameObject.AddComponent<AudioSource>();
            if (voiceSource == null) voiceSource = gameObject.AddComponent<AudioSource>();
            if (discardSeSource == null) discardSeSource = gameObject.AddComponent<AudioSource>();

            // BGM用のローパスフィルター設定
            if (bgmLowPassFilter == null)
            {
                bgmLowPassFilter = bgmSource.gameObject.GetComponent<AudioLowPassFilter>();
                if (bgmLowPassFilter == null) bgmLowPassFilter = bgmSource.gameObject.AddComponent<AudioLowPassFilter>();
            }
            // 初期状態はフィルターをかけておく（重低音のみ）
            bgmLowPassFilter.cutoffFrequency = 1000f; // こもった音の周波数
            bgmLowPassFilter.enabled = true;

            // AudioSynth用の専用GameObjectを作成
            if (synth == null)
            {
                GameObject synthObj = new GameObject("AudioSynthEngine");
                synthObj.transform.SetParent(this.transform);

                synth = synthObj.AddComponent<AudioSynth>();
            }


            bgmSource.loop = true;
            ApplyVolumes();

            PrewarmSynthSounds();
        }

        /// <summary>
        /// シンセSEを先に生成しておく。
        ///
        /// 生成は 48kHz で 1秒あたり 6〜7ms かかるメインスレッド処理なので、
        /// スキル発動やダメージ表示のタイミングで初めて作るとそこでフレームが飛ぶ。
        /// 鳴る場所が決まっている音はここで作っておき、実プレイ中はキャッシュだけを使う。
        ///
        /// ダメージ音・打撃音はHP割合で周波数が変わるので、代表的な数段だけ用意しておく
        /// （AudioSynth 側でキーを丸めているため、近い値はこれに吸収される）。
        /// </summary>
        private void PrewarmSynthSounds()
        {
            if (synth == null) return;

            // スキル発動音（PlaySkillSE と同じパラメータ）
            synth.Prewarm(SynthWaveType.Triangle, SynthWaveType.Square, true, 880f, 1174f, 0.18f);
            synth.Prewarm(SynthWaveType.Sine, SynthWaveType.Noise, true, 300f, 1800f, 0.55f);
            synth.Prewarm(SynthWaveType.Sawtooth, SynthWaveType.Square, true, 160f, 640f, 0.6f);
            synth.Prewarm(SynthWaveType.Sawtooth, SynthWaveType.Noise, true, 220f, 1760f, 0.28f);
            synth.Prewarm(SynthWaveType.Square, SynthWaveType.Noise, true, 1200f, 90f, 0.9f);

            // 賭け金確定・回復
            synth.Prewarm(SynthWaveType.Square, SynthWaveType.Sine, true, 523f, 1046f, 0.28f);
            synth.Prewarm(SynthWaveType.Sine, SynthWaveType.Triangle, true, 440f, 1320f, 0.35f);

            // ダメージ音・打撃音は残HP割合で周波数と長さが変わるので、代表的な5段を用意する。
            // PlayDamageSE / PlayHitSE と同じ式で作ること（式を変えたらここも合わせる）。
            for (int i = 0; i <= 4; i++)
            {
                float ratio = i * 0.25f;

                float dmgStart = Mathf.Lerp(260f, 600f, ratio);
                float dmgDuration = Mathf.Lerp(0.42f, 0.28f, ratio);
                synth.Prewarm(SynthWaveType.Sawtooth, SynthWaveType.Noise, true, dmgStart, dmgStart * 0.25f, dmgDuration);

                float hitStart = Mathf.Lerp(520f, 900f, ratio);
                synth.Prewarm(SynthWaveType.Square, SynthWaveType.Noise, true, hitStart, hitStart * 0.35f, 0.22f);
            }
        }

    }
}

