using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 同じ音が機械的に聞こえないようにするための層（2026-09-12）。
    ///
    /// **手法は Inscryption の `AudioParams` を参考にした。**
    /// あちらは一発物を鳴らすたびに「ピッチをばらす」「同じ変種を続けて出さない」
    /// 「同じ音が短時間に連発しないよう頻度を絞る」の3つを通していて、
    /// **音源を増やさずに生っぽさを作っている。**
    ///
    /// じゃんぱいあにはこの層が無く、`PlayOneShot` で毎回まったく同じ波形を出していた。
    /// 牌を置く音のように何十回も鳴るものは、それだけで作り物に聞こえる。
    ///
    /// **打牌音のピッチ階段（`PlayDiscardSE`）とは別物。** あちらは連打で音程が上がる
    /// 意図的な演出で、こちらは「同じ音に聞こえないようにする」ための微細なばらつき。
    /// </summary>
    public partial class AudioManager
    {
        /// <summary>
        /// ピッチのばらし幅。**上下に同じだけ振る。**
        /// 数値は Inscryption の区分に倣ったが、じゃんぱいあは牌の音が主体なので
        /// 既定を `VerySmall` 寄りにしてある（大きく振ると牌が別の素材に聞こえる）。
        /// </summary>
        public enum PitchJitter
        {
            /// <summary>ばらさない。音程そのものに意味がある音（能力の3段など）で使う</summary>
            None,
            /// <summary>±5%。牌・UI・紙など、素材感を保ちたいもの</summary>
            VerySmall,
            /// <summary>±10%。打撃・衝撃</summary>
            Small,
            /// <summary>±25%。当たり外れがあってよいもの</summary>
            Medium,
        }

        private static float JitterRange(PitchJitter j)
        {
            switch (j)
            {
                case PitchJitter.VerySmall: return 0.05f;
                case PitchJitter.Small: return 0.10f;
                case PitchJitter.Medium: return 0.25f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 同じ鍵で最後に鳴らした時刻。**連打を絞るために持つ。**
        /// 絞らないと、牌を連打されたとき同じ音が重なって機関銃のようになる。
        /// </summary>
        private readonly Dictionary<string, float> _lastPlayedAt = new Dictionary<string, float>();

        /// <summary>変種を選ぶとき、直前に選んだ添字。**続けて同じものを出さないために持つ。**</summary>
        private readonly Dictionary<string, int> _lastVariantIndex = new Dictionary<string, int>();

        /// <summary>
        /// 同じ鍵の音を、間隔を空けて鳴らしてよいか。
        /// `minInterval` 秒より短い間隔で来た2発目は捨てる。
        /// </summary>
        private bool PassesRateLimit(string key, float minInterval)
        {
            if (minInterval <= 0f || string.IsNullOrEmpty(key)) return true;

            float now = Time.unscaledTime;
            float last;
            if (_lastPlayedAt.TryGetValue(key, out last) && now - last < minInterval) return false;

            _lastPlayedAt[key] = now;
            return true;
        }

        /// <summary>
        /// 候補から1つ選ぶ。**直前と同じものは選ばない。**
        /// 候補が1つしかないときはそのまま返す（絞りようがない）。
        /// </summary>
        private int PickVariant(string key, int count)
        {
            if (count <= 1) return 0;

            int last;
            bool hasLast = _lastVariantIndex.TryGetValue(key, out last);

            int pick = Random.Range(0, hasLast ? count - 1 : count);
            if (hasLast && pick >= last) pick++;   // 直前の添字を飛ばす

            _lastVariantIndex[key] = pick;
            return pick;
        }

        /// <summary>
        /// 一発物を、ピッチをばらして鳴らす。
        ///
        /// **`PlayOneShot` はピッチを個別に持てない**（AudioSource のピッチが全体に効く）ので、
        /// 使い捨ての AudioSource を1つ作って鳴らし、終わったら消す。
        /// 数が増えても、鳴っている間しか存在しない。
        /// </summary>
        private void PlayOneShotWithPitch(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;

            if (Mathf.Approximately(pitch, 1f))
            {
                // ばらさないならわざわざ作らない。既存の経路をそのまま使う
                if (seSource != null) seSource.PlayOneShot(clip, volume);
                return;
            }

            var go = new GameObject("OneShot_" + clip.name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.volume = volume;
            src.pitch = pitch;
            src.playOnAwake = false;
            if (seSource != null) src.outputAudioMixerGroup = seSource.outputAudioMixerGroup;
            src.Play();

            // ピッチを下げると実時間が伸びるので、長さは pitch で割る
            Destroy(go, clip.length / Mathf.Max(0.01f, pitch) + 0.1f);
        }

        /// <summary>
        /// スティンガーを、ばらつきと連打制限つきで鳴らす。
        /// `PlayStinger(name)` はこれを既定値で呼ぶ薄い入口。
        /// </summary>
        /// <param name="minInterval">この秒数より短い間隔の2発目は捨てる。0で制限なし</param>
        public void PlayStinger(string name, PitchJitter jitter, float minInterval = 0f)
        {
            if (!CanPlay) return;
            if (string.IsNullOrEmpty(name) || seSource == null) return;
            if (!PassesRateLimit(name, minInterval)) return;

            AudioClip clip;
            if (!stingerClips.TryGetValue(name, out clip))
            {
                clip = Resources.Load<AudioClip>("Stingers/" + name);
                stingerClips[name] = clip;
                if (clip == null) Debug.LogWarning("[AudioManager] スティンガーが見つかりません: Resources/Stingers/" + name);
            }
            if (clip == null) return;

            float range = JitterRange(jitter);
            float pitch = range > 0f ? 1f + Random.Range(-range, range) : 1f;
            PlayOneShotWithPitch(clip, seVolume * masterVolume * stingerVolume, pitch);
        }

        /// <summary>
        /// 候補の中から1つ選んで鳴らす。**直前と同じものは選ばない。**
        /// 同じ場面で何度も鳴る音（牌を置く、カーソルを乗せる等）に使う。
        /// </summary>
        public void PlayStingerVariant(string key, string[] names, PitchJitter jitter, float minInterval = 0f)
        {
            if (!CanPlay) return;
            if (names == null || names.Length == 0) return;
            if (!PassesRateLimit(key, minInterval)) return;

            string chosen = names[PickVariant(key, names.Length)];

            AudioClip clip;
            if (!stingerClips.TryGetValue(chosen, out clip))
            {
                clip = Resources.Load<AudioClip>("Stingers/" + chosen);
                stingerClips[chosen] = clip;
            }
            if (clip == null) return;

            float range = JitterRange(jitter);
            float pitch = range > 0f ? 1f + Random.Range(-range, range) : 1f;
            PlayOneShotWithPitch(clip, seVolume * masterVolume * stingerVolume, pitch);
        }

        /// <summary>
        /// 通常SEを、ばらつきと連打制限つきで鳴らす。
        /// `PlaySE(clip)` はばらさない従来の経路のまま残してある。
        /// </summary>
        public void PlaySE(AudioClip clip, PitchJitter jitter, float minInterval = 0f)
        {
            if (!CanPlay) return;
            if (clip == null || seSource == null) return;
            if (!PassesRateLimit(clip.name, minInterval)) return;

            float range = JitterRange(jitter);
            float pitch = range > 0f ? 1f + Random.Range(-range, range) : 1f;
            PlayOneShotWithPitch(clip, seVolume * masterVolume, pitch);
        }
    }
}
