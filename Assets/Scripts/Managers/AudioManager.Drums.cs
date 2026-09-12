using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// フェイズごとのドラム層（2026-09-10）。
    ///
    /// **BGMは土台として鳴らし続け、ドラムだけを差し替える。**
    /// 曲が2本しかないので、曲そのものを切り替えると局中に何度も曲が変わって落ち着かない。
    /// ドラムの密度と強さでフェイズの表情を作る方が、素材が少なくても成立する。
    ///
    /// **ドラムはBGMの拍に合わせて入る。** 曲の実測テンポと1拍目の位置から
    /// 次の小節頭を計算し、そこで差し替える。適当に鳴らすと拍がずれて気持ち悪くなる。
    /// </summary>
    public partial class AudioManager
    {
        /// <summary>
        /// BGMごとの拍の情報。**波形を解析して実測した値**（2026-09-10）。
        /// 曲を差し替えたら測り直すこと。目分量で入れるとドラムがずれる。
        /// </summary>
        private struct BeatGrid
        {
            public float Bpm;
            public float FirstBeatSec;
            public BeatGrid(float bpm, float firstBeatSec) { Bpm = bpm; FirstBeatSec = firstBeatSec; }
        }

        private static readonly Dictionary<string, BeatGrid> BeatGrids = new Dictionary<string, BeatGrid>
        {
            { "1賭けの合図", new BeatGrid(135.10f, 0.283f) },
            { "2賭けの合図", new BeatGrid(134.25f, 0.152f) },
        };

        /// <summary>フェイズごとに鳴らすドラム。`Resources/Drums/` から読む。空文字は「鳴らさない」。</summary>
        private static readonly Dictionary<RoundStatus, string> PhaseDrums = new Dictionary<RoundStatus, string>
        {
            { RoundStatus.Dealing,       "drum_prepare" },
            { RoundStatus.HandSelection, "drum_prepare" },
            { RoundStatus.Betting,       "drum_betting" },
            { RoundStatus.TurnDecision,  "drum_discard" },
            { RoundStatus.Discard,       "drum_discard" },
            { RoundStatus.Liquidation,   "drum_ron" },
            { RoundStatus.Agari,         "drum_ron" },
            { RoundStatus.Ron,           "drum_ron" },
            { RoundStatus.Draw,          "" },
            { RoundStatus.Result,        "drum_result" },
            { RoundStatus.None,          "" },
        };

        private AudioSource drumSource;
        private readonly Dictionary<string, AudioClip> drumClips = new Dictionary<string, AudioClip>();
        private string currentDrumName = null;
        private Coroutine drumSwapCoroutine;

        [Header("Drums")]
        [Tooltip("ドラム層の音量。BGMに対する相対値")]
        [Range(0f, 1f)] public float drumVolume = 0.7f;

        private void EnsureDrumSource()
        {
            if (drumSource != null) return;
            drumSource = gameObject.AddComponent<AudioSource>();
            drumSource.playOnAwake = false;
            drumSource.loop = true;
            drumSource.volume = drumVolume * bgmVolume * masterVolume;
        }

        private AudioClip GetDrumClip(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (drumClips.TryGetValue(name, out AudioClip cached)) return cached;
            var clip = Resources.Load<AudioClip>("Drums/" + name);
            drumClips[name] = clip;
            if (clip == null) Debug.LogWarning($"[AudioManager] ドラムが見つかりません: Resources/Drums/{name}");
            return clip;
        }

        /// <summary>
        /// フェイズに合わせてドラムを差し替える。**次の小節頭まで待ってから切り替える。**
        /// すぐ切り替えると拍の途中で音が途切れ、繋ぎ目が耳につく。
        /// </summary>
        public void SetPhaseDrum(RoundStatus status)
        {
            if (!CanPlay) return;
            // **フェイズBGMを使っているあいだは何もしない（2026-09-11）。**
            // `Resources/Bgm/` の曲には最初からドラムが入っているので、
            // ここで層をもう1枚重ねると打楽器が二重になる。
            // 仕組みは残してあるので、`UsePhaseBgm` を切れば旧方式に戻る。
            if (UsePhaseBgm)
            {
                if (drumSource != null && drumSource.isPlaying) drumSource.Stop();
                currentDrumName = "";
                return;
            }

            string want = PhaseDrums.TryGetValue(status, out string n) ? n : "";
            if (want == currentDrumName) return;

            EnsureDrumSource();
            currentDrumName = want;

            if (drumSwapCoroutine != null) StopCoroutine(drumSwapCoroutine);

            if (string.IsNullOrEmpty(want))
            {
                drumSource.Stop();
                drumSource.clip = null;
                return;
            }

            var clip = GetDrumClip(want);
            if (clip == null) return;

            drumSwapCoroutine = StartCoroutine(SwapDrumAtNextBar(clip));
        }

        private IEnumerator SwapDrumAtNextBar(AudioClip clip)
        {
            float wait = SecondsToNextBar();
            if (wait > 0f) yield return new WaitForSeconds(wait);

            drumSource.clip = clip;
            drumSource.volume = drumVolume * bgmVolume * masterVolume;
            drumSource.loop = true;
            drumSource.Play();
            drumSwapCoroutine = null;
        }

        /// <summary>
        /// いま鳴っているBGMの、次の小節頭までの秒数。
        /// **拍の情報が無い曲、あるいはBGMが止まっているときは 0**（すぐ切り替える）。
        /// </summary>
        private float SecondsToNextBar()
        {
            if (bgmSource == null || bgmSource.clip == null || !bgmSource.isPlaying) return 0f;
            if (!BeatGrids.TryGetValue(bgmSource.clip.name, out BeatGrid grid)) return 0f;

            float beat = 60f / grid.Bpm;
            float bar = beat * 4f;
            float pos = bgmSource.time - grid.FirstBeatSec;
            if (pos < 0f) return -pos;

            float intoBar = pos % bar;
            return bar - intoBar;
        }

        /// <summary>音量設定が変わったときにドラムにも反映する。</summary>
        private void ApplyDrumVolume()
        {
            if (drumSource != null) drumSource.volume = drumVolume * bgmVolume * masterVolume;
        }
    }
}
