using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// BGMを層で鳴らす（2026-09-12）。
    ///
    /// **手法は Inscryption を参考にした。** あちらは `AudioController` が
    /// ループ用の AudioSource を複数持ち、`FadeOutLoop(sourceIndices)` のように
    /// **層ごとに音量だけを動かしている。** 曲は変わらないまま厚みだけが変わる。
    ///
    /// じゃんぱいあはこれまで「濃さが変わるたびに曲ごと差し替える」方式だった。
    /// 差し替えは、どれだけ丁寧に繋いでも「曲が変わった」と気づかれる。
    /// 層なら**曲は一度も変わらず、鳴っている楽器の数だけが変わる。**
    ///
    /// 素材は `Resources/Bgm/field_*.wav` の4本。同じ編曲を層ごとに書き出したもので、
    /// **4本を足すと元のミックスに戻る**（実測 69.5dB SNR）。
    /// 個別に正規化していないので、勝手に音量をいじると合算が崩れる。
    ///
    /// **必ず PlayScheduled で同時刻に始める。** Play() を並べて呼ぶと
    /// 呼んだ順にずれて、層どうしが微妙にずれた「もや」になる。
    /// </summary>
    public partial class AudioManager
    {
        /// <summary>層の並び。添字がそのまま `_layerSources` の添字になる。</summary>
        public enum BgmLayer
        {
            /// <summary>低音とパッド。土台なので常に鳴らす</summary>
            Base = 0,
            /// <summary>旋律</summary>
            Melody = 1,
            /// <summary>打楽器</summary>
            Drums = 2,
            /// <summary>倍音。打牌フェイズで開く層</summary>
            Sparkle = 3,
        }

        private static readonly string[] LayerClipNames =
        {
            "field_base", "field_melody", "field_drums", "field_sparkle",
        };

        /// <summary>
        /// 濃さごとの、各層の音量。**行が濃さ（0〜4）、列が層。**
        /// 曲を差し替える代わりにこの表を移動する。
        /// 0段は土台だけ、4段は全部を最大で鳴らす。
        /// </summary>
        private static readonly float[,] LayerMix =
        {
            //           Base  Melody Drums Sparkle
            /* 0 */ {    0.75f, 0f,    0f,   0f    },
            /* 1 */ {    0.90f, 0.55f, 0f,   0f    },
            /* 2 */ {    1.00f, 0.85f, 0.60f, 0f   },
            /* 3 */ {    1.00f, 1.00f, 0.90f, 0.55f },
            /* 4 */ {    1.00f, 1.00f, 1.00f, 1.00f },
        };

        /// <summary>層の切り替えにかける秒数。**曲は変わらないので、ゆっくりでよい。**</summary>
        private const float LayerFadeSeconds = 1.6f;

        /// <summary>先に鳴らし始めるまでの余裕。PlayScheduled が間に合うようにする。</summary>
        private const double LayerStartLead = 0.10;

        [Header("Bgm Layers")]
        [Tooltip("場のBGMを層で鳴らす。切ると曲ごと差し替える旧方式に戻る")]
        public bool UseBgmLayers = true;

        private AudioSource[] _layerSources;
        private Coroutine[] _layerFades;
        private bool _layersRunning;
        private int _layerIntensity = -1;

        public bool AreLayersRunning { get { return _layersRunning; } }

        private bool EnsureLayerSources()
        {
            if (_layerSources != null) return true;

            var host = new GameObject("BGM_Layers");
            host.transform.SetParent(transform, false);

            _layerSources = new AudioSource[LayerClipNames.Length];
            _layerFades = new Coroutine[LayerClipNames.Length];

            for (int i = 0; i < LayerClipNames.Length; i++)
            {
                var clip = Resources.Load<AudioClip>("Bgm/" + LayerClipNames[i]);
                if (clip == null)
                {
                    Debug.LogWarning("[AudioManager] 層が見つかりません: Resources/Bgm/" + LayerClipNames[i]);
                    _layerSources = null;
                    Destroy(host);
                    return false;
                }

                var src = host.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.playOnAwake = false;
                src.volume = 0f;
                _layerSources[i] = src;
            }
            return true;
        }

        /// <summary>
        /// 層での再生を始める。曲の差し替えは以後行わず、濃さは
        /// <see cref="SetBgmIntensity"/> が層の音量だけを動かす。
        /// </summary>
        public void StartLayeredBgm(int intensity)
        {
            if (!UseBgmLayers || !EnsureLayerSources()) return;

            // 層に切り替えるので、1本もので鳴っていたものは止める
            if (bgmSource != null && bgmSource.isPlaying) bgmSource.Stop();
            currentPhaseBgmName = null;

            // **全部を同じ時刻に予約する。** ここがずれると層が重ならない
            double at = AudioSettings.dspTime + LayerStartLead;
            for (int i = 0; i < _layerSources.Length; i++)
            {
                _layerSources[i].timeSamples = 0;
                _layerSources[i].volume = 0f;
                _layerSources[i].PlayScheduled(at);
            }

            _layersRunning = true;
            _layerIntensity = -1;              // 必ず当て直す
            ApplyLayerMix(Mathf.Clamp(intensity, 0, LayerMix.GetLength(0) - 1), instant: true);
        }

        public void StopLayeredBgm()
        {
            if (_layerSources == null) return;
            for (int i = 0; i < _layerSources.Length; i++)
            {
                if (_layerFades[i] != null) { StopCoroutine(_layerFades[i]); _layerFades[i] = null; }
                _layerSources[i].Stop();
            }
            _layersRunning = false;
            _layerIntensity = -1;
        }

        /// <summary>濃さを当てる。曲は変えず、層の音量だけを動かす。</summary>
        private void ApplyLayerMix(int intensity, bool instant)
        {
            if (!_layersRunning || _layerSources == null) return;

            intensity = Mathf.Clamp(intensity, 0, LayerMix.GetLength(0) - 1);
            if (intensity == _layerIntensity) return;
            _layerIntensity = intensity;

            float master = bgmVolume * masterVolume;
            for (int i = 0; i < _layerSources.Length; i++)
            {
                float target = LayerMix[intensity, i] * master;
                if (_layerFades[i] != null) { StopCoroutine(_layerFades[i]); _layerFades[i] = null; }

                if (instant) _layerSources[i].volume = target;
                else _layerFades[i] = StartCoroutine(FadeLayer(i, target, LayerFadeSeconds));
            }
        }

        private IEnumerator FadeLayer(int index, float target, float duration)
        {
            var src = _layerSources[index];
            float from = src.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                u = u * u * (3f - 2f * u);          // 端をなめらかに
                src.volume = Mathf.Lerp(from, target, u);
                yield return null;
            }
            src.volume = target;
            _layerFades[index] = null;
        }

        /// <summary>音量設定が変わったとき、鳴っている層にも反映する。</summary>
        private void ApplyLayerVolumes()
        {
            if (!_layersRunning || _layerSources == null || _layerIntensity < 0) return;

            float master = bgmVolume * masterVolume;
            for (int i = 0; i < _layerSources.Length; i++)
            {
                if (_layerFades[i] != null) continue;   // フェード中は触らない
                _layerSources[i].volume = LayerMix[_layerIntensity, i] * master;
            }
        }
    }
}
