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
        [Header("生成コスト対策")]
        [SerializeField, Tooltip("ノイズ入りの音を何種類まで作って使い回すか。多いほど音の変化は残るが、ロード時の生成が増える")]
        private int noiseVariantCount = 2;

        [SerializeField, Tooltip("キャッシュしておく音の上限。超えたら古い順に破棄する")]
        private int maxCacheEntries = 64;

        private double sampleRate;
        private System.Random rnd = new System.Random();
        private AudioSource audioSource;

        /// <summary>
        /// 同一パラメータの AudioClip をキャッシュして再利用する。
        ///
        /// ノイズ入りの音は「毎回違う波形になるから」という理由でキャッシュ対象外だったが、
        /// 生成に 48kHz で 0.55秒ぶん＝約4〜6ms かかるため、スキル発動やダメージ表示のたびに
        /// メインスレッドが数ミリ秒止まっていた（実測：透視5.9ms / 特殊勝利6.2ms / ダメージ2.1ms）。
        /// さらに未キャッシュのクリップは破棄されないので、鳴らすたびに AudioClip が漏れていた。
        ///
        /// 対策として、ノイズ入りの音も数種類だけ作って順番に使い回す。
        /// バリアントは必要になった時に1本ずつ作るので、最初の数回で作り終えたあとは生成コストが0になる。
        /// </summary>
        private class CachedTone
        {
            public AudioClip[] variants;
            public int cursor;
        }

        private readonly Dictionary<string, CachedTone> clipCache = new Dictionary<string, CachedTone>();
        private readonly List<string> cacheOrder = new List<string>();

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

            AudioClip clip = GetOrCreateClip(type1, type2, isDual, startFreq, endFreq, duration);
            if (clip == null) return;

            float masterVol = AudioManager.Instance != null ? AudioManager.Instance.seVolume * AudioManager.Instance.masterVolume : 1f;
            audioSource.PlayOneShot(clip, vol * masterVol);
        }

        /// <summary>
        /// 鳴らす前にバリアントを全部作っておく。
        /// ロード中など、数十ミリ秒止まっても構わない場所で呼ぶこと（AudioManager.Awake から呼んでいる）。
        /// </summary>
        public void Prewarm(SynthWaveType type1, SynthWaveType type2, bool isDual, float startFreq, float endFreq, float duration)
        {
            CachedTone tone = ResolveTone(type1, type2, isDual, ref startFreq, ref endFreq, ref duration);
            if (tone == null) return;

            for (int i = 0; i < tone.variants.Length; i++)
            {
                if (tone.variants[i] == null)
                    tone.variants[i] = GenerateClip(type1, type2, isDual, startFreq, endFreq, duration);
            }
            tone.cursor = 0;
        }

        /// <summary>
        /// キャッシュから取り出す。ノイズ入りの音は複数のバリアントを順番に使い回す。
        ///
        /// 未生成のバリアントに当たったときは、そこで作らずに既にあるバリアントで代用する。
        /// プレイ中にシンセ生成（数ミリ秒）が走らないようにするため。
        /// バリアントを増やすのは Prewarm（ロード時）の役目。
        /// </summary>
        private AudioClip GetOrCreateClip(SynthWaveType type1, SynthWaveType type2, bool isDual, float startFreq, float endFreq, float duration)
        {
            CachedTone tone = ResolveTone(type1, type2, isDual, ref startFreq, ref endFreq, ref duration);
            if (tone == null) return null;

            int index = tone.cursor;
            if (index < 0 || index >= tone.variants.Length) index = 0;
            tone.cursor = (index + 1) % tone.variants.Length;

            if (tone.variants[index] != null) return tone.variants[index];

            // 別のバリアントが既にあるならそれで代用する（生成しない）
            for (int i = 0; i < tone.variants.Length; i++)
            {
                if (tone.variants[i] != null) return tone.variants[i];
            }

            // この音を初めて鳴らす場合だけ生成する
            tone.variants[index] = GenerateClip(type1, type2, isDual, startFreq, endFreq, duration);
            return tone.variants[index];
        }

        /// <summary>
        /// パラメータを丸めてキャッシュの枠を引く。丸めた値は ref で呼び出し元へ返し、生成にも使う。
        ///
        /// ダメージ音のように周波数が連続値で渡されるとキーが毎回変わってキャッシュが効かず、
        /// しかもキャッシュが無限に増えてしまうため、キーは丸めた値で作る。
        /// </summary>
        private CachedTone ResolveTone(SynthWaveType type1, SynthWaveType type2, bool isDual,
                                       ref float startFreq, ref float endFreq, ref float duration)
        {
            startFreq = QuantizeFreq(startFreq);
            endFreq = QuantizeFreq(endFreq);
            duration = QuantizeDuration(duration);
            if (duration <= 0f) return null;

            bool hasNoise = type1 == SynthWaveType.Noise || (isDual && type2 == SynthWaveType.Noise);
            string key = string.Format("{0}_{1}_{2}_{3:F0}_{4:F0}_{5:F2}", type1, type2, isDual, startFreq, endFreq, duration);

            CachedTone tone;
            if (!clipCache.TryGetValue(key, out tone))
            {
                tone = new CachedTone();
                tone.variants = new AudioClip[hasNoise ? Mathf.Max(1, noiseVariantCount) : 1];
                clipCache[key] = tone;
                cacheOrder.Add(key);
                TrimCache();
            }
            return tone;
        }

        // 25Hz / 10ms 刻み。この程度の差は短いSEでは聴き分けられない
        private static float QuantizeFreq(float f) { return Mathf.Round(f / 25f) * 25f; }
        private static float QuantizeDuration(float d) { return Mathf.Round(d / 0.01f) * 0.01f; }

        /// <summary>上限を超えた分を古い順に破棄する。AudioClip は明示的に消さないと残る。</summary>
        private void TrimCache()
        {
            while (cacheOrder.Count > Mathf.Max(1, maxCacheEntries))
            {
                string oldest = cacheOrder[0];
                cacheOrder.RemoveAt(0);

                CachedTone old;
                if (clipCache.TryGetValue(oldest, out old))
                {
                    if (old.variants != null)
                    {
                        for (int i = 0; i < old.variants.Length; i++)
                        {
                            if (old.variants[i] != null) Destroy(old.variants[i]);
                        }
                    }
                    clipCache.Remove(oldest);
                }
            }
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
