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

    public class AudioSynth : MonoBehaviour
    {
        private class SynthVoice
        {
            public bool isActive;
            public SynthWaveType type1;
            public SynthWaveType type2;
            public bool isDualWave;
            
            public float startFreq;
            public float endFreq;
            public float duration;
            public float time;
            public float phase1;
            public float phase2;
            
            public float attackTime = 0.01f; // 10msで立ち上げ
            public float volume = 1.0f;
            
            // Noise用シンプルLPF状態
            public float noiseValue = 0f;
            public float noiseLpfAlpha = 0.5f; 
        }

        private List<SynthVoice> voices = new List<SynthVoice>();
        private int maxVoices = 16;
        private double sampleRate;
        private System.Random rnd = new System.Random();

        private void Awake()
        {
            sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate == 0) sampleRate = 48000;
            
            for (int i = 0; i < maxVoices; i++)
            {
                voices.Add(new SynthVoice { isActive = false });
            }
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
            SynthVoice voice = GetFreeVoice();
            if (voice == null) return;

            voice.isActive = true;
            voice.type1 = type1;
            voice.type2 = type2;
            voice.isDualWave = isDual;
            voice.startFreq = startFreq;
            voice.endFreq = endFreq;
            voice.duration = duration;
            voice.time = 0f;
            voice.phase1 = 0f;
            voice.phase2 = 0f;
            voice.volume = vol;
            voice.noiseValue = 0f;
        }

        private SynthVoice GetFreeVoice()
        {
            foreach (var v in voices)
            {
                if (!v.isActive) return v;
            }
            // 空きがない場合は一番古いものを強制停止して再利用
            float maxTime = -1f;
            SynthVoice oldest = null;
            foreach (var v in voices)
            {
                if (v.time > maxTime)
                {
                    maxTime = v.time;
                    oldest = v;
                }
            }
            return oldest;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            int dataLen = data.Length / channels;
            double sampleDur = 1.0 / sampleRate;

            // バッファをゼロクリア
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = 0f;
            }

            foreach (var v in voices)
            {
                if (!v.isActive) continue;

                for (int i = 0; i < dataLen; i++)
                {
                    if (v.time >= v.duration)
                    {
                        v.isActive = false;
                        break;
                    }

                    // 周波数の計算（線形補間）
                    float tRate = v.time / v.duration;
                    float currentFreq = Mathf.Lerp(v.startFreq, v.endFreq, tRate);

                    // エンベロープの計算
                    float env = 1.0f;
                    if (v.time < v.attackTime)
                    {
                        // 10msで立ち上げ
                        env = v.time / v.attackTime;
                    }
                    else
                    {
                        // 指数減衰：徐々に減衰するが、最後は0に近づくようにする
                        float decayTime = v.time - v.attackTime;
                        float totalDecay = v.duration - v.attackTime;
                        if (totalDecay > 0)
                        {
                            env = Mathf.Exp(-5.0f * (decayTime / totalDecay)); // e^-5 でほぼ0になる
                        }
                    }

                    // 波形1のサンプリング
                    float sample1 = GenerateSample(v, v.type1, currentFreq, ref v.phase1, sampleDur);
                    
                    float mixedSample = sample1;
                    if (v.isDualWave)
                    {
                        // デュアル波形の場合は合成。
                        float sample2 = GenerateSample(v, v.type2, currentFreq, ref v.phase2, sampleDur);
                        mixedSample = (sample1 + sample2) * 0.5f;
                    }

                    // 音量調整（全体のボリュームを下げる、マスターボリュームにも合わせる）
                    float masterVol = AudioManager.Instance != null ? AudioManager.Instance.seVolume * AudioManager.Instance.masterVolume : 1f;
                    float finalSample = mixedSample * env * v.volume * 0.2f * masterVol;

                    // チャンネルに書き込み
                    for (int c = 0; c < channels; c++)
                    {
                        data[i * channels + c] += finalSample;
                    }

                    v.time += (float)sampleDur;
                }
            }
            
            // 全体のクリッピング防止
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
            }
        }

        private float GenerateSample(SynthVoice v, SynthWaveType type, float currentFreq, ref float phase, double sampleDur)
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
                    v.noiseValue = v.noiseValue + v.noiseLpfAlpha * (rawNoise - v.noiseValue);
                    s = v.noiseValue;
                    break;
            }

            phase += (float)(currentFreq * sampleDur);
            if (phase > 1f) phase -= 1f;

            return s;
        }
    }
}
