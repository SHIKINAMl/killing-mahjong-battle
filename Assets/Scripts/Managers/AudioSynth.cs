using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public enum SynthWaveType
    {
        Sine,
        Triangle,
        Square,
        Sawtooth,
        Noise
    }

    /// <summary>
    /// プロシージャル音声（シンセサイザー）を生成・再生するクラス。
    /// OnAudioFilterRead を使わず AudioClip を動的に生成して PlayOneShot で再生するため、
    /// WebGL 環境でも安全に動作する。
    /// 同一パラメータの音はキャッシュして再利用する。
    /// </summary>
    public class AudioSynth : MonoBehaviour
    {
        private double sampleRate;
        private System.Random rnd = new System.Random();
        private AudioSource audioSource;

        // 同一パラメータのAudioClipをキャッシュして再利用する
        private Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate == 0) sampleRate = 48000;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        /// <summary>
        /// 波形を1つ使って音を鳴らす
        /// </summary>
        public void Play(SynthWaveType type, float startFreq, float endFreq, float duration, float vol = 1.0f)
        {
            PlayDual(type, type, false, startFreq, endFreq, duration, vol);
        }

        /// <summary>
        /// 波形を2つミックスして音を鳴らす（例：Sine + Square など）
        /// </summary>
        public void PlayDual(SynthWaveType type1, SynthWaveType type2, bool isDual, float startFreq, float endFreq, float duration, float vol = 1.0f)
        {
            if (audioSource == null) return;

            // キャッシュキー生成（Noiseは毎回異なるのでキャッシュしない）
            bool useCache = (type1 != SynthWaveType.Noise && (!isDual || type2 != SynthWaveType.Noise));
            string cacheKey = useCache ? $"{type1}_{type2}_{isDual}_{startFreq:F1}_{endFreq:F1}_{duration:F4}" : null;

            AudioClip clip = null;
            if (useCache && cacheKey != null && clipCache.TryGetValue(cacheKey, out clip))
            {
                // キャッシュヒット：既存のClipを再利用
            }
            else
            {
                // 新規生成
                clip = GenerateClip(type1, type2, isDual, startFreq, endFreq, duration);
                if (clip == null) return;

                if (useCache && cacheKey != null)
                {
                    clipCache[cacheKey] = clip;
                }
            }

            float masterVol = AudioManager.Instance != null ? AudioManager.Instance.seVolume * AudioManager.Instance.masterVolume : 1f;
            audioSource.PlayOneShot(clip, vol * masterVol);
        }

        private AudioClip GenerateClip(SynthWaveType type1, SynthWaveType type2, bool isDual, float startFreq, float endFreq, float duration)
        {
            int sampleCount = Mathf.CeilToInt((float)(sampleRate * duration));
            if (sampleCount <= 0) return null;

            float[] data = new float[sampleCount];
            float phase1 = 0f;
            float phase2 = 0f;
            float noiseValue = 0f;
            float noiseLpfAlpha = 0.5f;
            float attackTime = 0.01f;
            double sampleDur = 1.0 / sampleRate;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)(i * sampleDur);
                float tRate = time / duration;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, tRate);

                // エンベロープの計算
                float env = 1.0f;
                if (time < attackTime)
                {
                    env = time / attackTime;
                }
                else
                {
                    float decayTime = time - attackTime;
                    float totalDecay = duration - attackTime;
                    if (totalDecay > 0)
                    {
                        env = Mathf.Exp(-5.0f * (decayTime / totalDecay));
                    }
                }

                // 波形1のサンプリング
                float sample1 = GenerateSample(type1, currentFreq, ref phase1, sampleDur, ref noiseValue, noiseLpfAlpha);

                float mixedSample = sample1;
                if (isDual)
                {
                    float sample2 = GenerateSample(type2, currentFreq, ref phase2, sampleDur, ref noiseValue, noiseLpfAlpha);
                    mixedSample = (sample1 + sample2) * 0.5f;
                }

                // 音量調整（全体のボリュームを下げる）
                data[i] = Mathf.Clamp(mixedSample * env * 0.2f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("SynthTone", sampleCount, 1, (int)sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private float GenerateSample(SynthWaveType type, float currentFreq, ref float phase, double sampleDur, ref float noiseValue, float noiseLpfAlpha)
        {
            float s = 0f;
            switch (type)
            {
                case SynthWaveType.Sine:
                    s = Mathf.Sin(phase * 2f * Mathf.PI);
                    break;
                case SynthWaveType.Triangle:
                    s = Mathf.PingPong(phase * 2f, 1f) * 2f - 1f;
                    break;
                case SynthWaveType.Square:
                    s = (phase % 1.0f) < 0.5f ? 1f : -1f;
                    break;
                case SynthWaveType.Sawtooth:
                    s = (phase % 1.0f) * 2f - 1f;
                    break;
                case SynthWaveType.Noise:
                    float rawNoise = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    noiseValue = noiseValue + noiseLpfAlpha * (rawNoise - noiseValue);
                    s = noiseValue;
                    break;
            }

            phase += (float)(currentFreq * sampleDur);
            if (phase > 1f) phase -= 1f;

            return s;
        }
    }
}
