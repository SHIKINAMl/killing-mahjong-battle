using UnityEngine;
using System.Collections.Generic;

namespace KillingMahjong.Managers
{
    public partial class AudioManager
    {
        // ------------------------------------------------------------
        //  もう一段深いこもり（2026-10-03）
        //
        //  透視スキル中だけ、BGMを**深い水の底に沈んだような音**にする。
        //
        //  **フェイズのこもり（1000Hz）とは別の段。** 透視を撃つのは手牌選択
        //  フェイズで、その時点ですでに 1000Hz までこもっている。同じ値へ
        //  動かしても耳には何も起きないので、透視ぶんの深さを足して戻す形にした。
        //
        //  行き先は <see cref="ApplyFilterTarget"/> が一本で決める。透視中に
        //  フェイズが変わっても、透視が抜けるまでは深い方を保つ。
        //
        //  **参考にした質感はローパス1本では再現できない**ことが実測で分かっている
        //  （中域だけ 10dB 前後ごっそり抜け、低音と最高域が残る形だった）。
        //  まずは手持ちで出せる所まで寄せる案として、カットオフを下げたうえに
        //  リバーブを足している。物足りなければ、こもり版のBGMを別に用意する。
        // ------------------------------------------------------------

        /// <summary>
        /// 透視中のカットオフ。
        ///
        /// **600Hz では耳に届かなかった（2026-10-03 にユーザー報告「音変わってないです」）。**
        /// 実測すると値そのものは 1000→600 へ動いていたので、仕組みは効いていた。
        /// 届かなかった理由は幅で、1000→600 は 0.74 オクターブしかない。
        /// 人の耳は対数で聞くので、**すでにこもっている所からさらに沈めるには
        /// オクターブで稼ぐ必要がある。** 260Hz なら 1000 から約1.9オクターブ下がる。
        /// </summary>
        private const float DeepMuffledCutoff = 260f;

        /// <summary>沈むまでの秒数。長いと「いつの間にか変わっていた」になるので短めに。</summary>
        private const float DeepMuffleDuration = 0.6f;

        /// <summary>戻るまでの秒数。心音3拍のあいだに戻りきるくらい。</summary>
        private const float DeepOpenDuration = 1.2f;

        /// <summary>
        /// リバーブを効かせたときの room（ミリベル）。0 が最大。
        /// こちらも -600 では物足りなかったので、ほぼ全開まで上げている。
        /// </summary>
        private const float ReverbRoomOn = -200f;

        /// <summary>リバーブを切ったときの room。これ以下は事実上無音。</summary>
        private const float ReverbRoomOff = -10000f;

        /// <summary>フェイズが求めているこもり。透視が抜けたときの戻り先になる。</summary>
        private bool _phaseMuffled;

        /// <summary>透視中かどうか。</summary>
        private bool _deepMuffle;

        private AudioReverbFilter bgmReverbFilter;
        private Coroutine reverbFadeCoroutine;

        /// <summary>打牌フェイズかどうかで、BGMのこもりを切り替える。</summary>
        public void SetBgmFilter(bool isMuffled, float fadeDuration = -1f)
        {
            _phaseMuffled = isMuffled;
            if (fadeDuration < 0f) fadeDuration = isMuffled ? FilterMuffleDuration : FilterOpenDuration;
            ApplyFilterTarget(fadeDuration);
        }

        /// <summary>
        /// 透視スキル中の「深い水の底」。フェイズのこもりに重ねて、もう一段沈める。
        /// </summary>
        public void SetBgmDeepMuffle(bool on, float fadeDuration = -1f)
        {
            if (_deepMuffle == on) return;
            _deepMuffle = on;

            if (fadeDuration < 0f) fadeDuration = on ? DeepMuffleDuration : DeepOpenDuration;

            ApplyFilterTarget(fadeDuration);
            FadeReverb(on, fadeDuration);
        }

        /// <summary>いま出ているべきカットオフへ向かわせる。</summary>
        private void ApplyFilterTarget(float fadeDuration)
        {
            if (bgmLowPassFilter == null) return;

            float targetFreq = _deepMuffle
                ? DeepMuffledCutoff
                : (_phaseMuffled ? MuffledCutoff : OpenCutoff);

            // 同じ行き先へ改めて頼まれただけなら、進行中のフェードを潰さない。
            // 潰すと**フェードが毎回やり直しになって伸びる**（フェーズ変更で2か所から呼ばれていた）
            if (filterFadeCoroutine != null)
            {
                if (Mathf.Approximately(_filterTargetFreq, targetFreq)) return;
                StopCoroutine(filterFadeCoroutine);
            }

            filterFadeCoroutine = StartCoroutine(FilterFadeRoutine(targetFreq, fadeDuration));
        }

        private System.Collections.IEnumerator FilterFadeRoutine(float targetFreq, float duration)
        {
            _filterTargetFreq = targetFreq;

            float startFreq = Mathf.Max(bgmLowPassFilter.enabled ? bgmLowPassFilter.cutoffFrequency : OpenCutoff, 20f);
            bgmLowPassFilter.enabled = true; // 確実にオンにする

            float logStart = Mathf.Log(startFreq);
            float logTarget = Mathf.Log(targetFreq);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 端をなめらかにする。始まりと終わりの「動き出し・止まり」が耳に付きにくくなる
                t = t * t * (3f - 2f * t);
                bgmLowPassFilter.cutoffFrequency = Mathf.Exp(Mathf.Lerp(logStart, logTarget, t));
                yield return null;
            }

            bgmLowPassFilter.cutoffFrequency = targetFreq;
            filterFadeCoroutine = null;

            // 全帯域まで開いたらフィルター自体をオフにして負荷軽減＆音質劣化防止
            if (targetFreq >= OpenCutoff)
            {
                bgmLowPassFilter.enabled = false;
            }
        }

        // ------------------------------------------------------------

        /// <summary>
        /// リバーブを出し入れする。**プリセットの切り替えではなく room を動かす。**
        /// プリセットを変えると効き具合が一段で変わってブツッと鳴るので、
        /// User にしておいて濡れ具合だけをなめらかに動かす。
        /// </summary>
        private void FadeReverb(bool on, float duration)
        {
            if (bgmSource == null) return;

            if (bgmReverbFilter == null)
            {
                bgmReverbFilter = bgmSource.gameObject.GetComponent<AudioReverbFilter>();
                if (bgmReverbFilter == null)
                {
                    bgmReverbFilter = bgmSource.gameObject.AddComponent<AudioReverbFilter>();
                }

                bgmReverbFilter.reverbPreset = AudioReverbPreset.User;
                bgmReverbFilter.dryLevel = 0f;      // 元の音はそのまま通す
                bgmReverbFilter.room = ReverbRoomOff;
                bgmReverbFilter.roomHF = -3500f;    // 高い所ほど残響に乗せない。水の中らしくなる
                bgmReverbFilter.decayTime = 3.5f;
                bgmReverbFilter.reverbLevel = 800f;
                bgmReverbFilter.reverbDelay = 0.02f;
                bgmReverbFilter.diffusion = 100f;
                bgmReverbFilter.density = 100f;
                bgmReverbFilter.enabled = false;
            }

            if (reverbFadeCoroutine != null) StopCoroutine(reverbFadeCoroutine);
            reverbFadeCoroutine = StartCoroutine(ReverbFadeRoutine(on ? ReverbRoomOn : ReverbRoomOff, duration));
        }

        private System.Collections.IEnumerator ReverbFadeRoutine(float targetRoom, float duration)
        {
            bgmReverbFilter.enabled = true;

            float startRoom = bgmReverbFilter.room;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                bgmReverbFilter.room = Mathf.Lerp(startRoom, targetRoom, t);
                yield return null;
            }

            bgmReverbFilter.room = targetRoom;
            reverbFadeCoroutine = null;

            // 切りきったらフィルターごと止める。通したままだと常に少し残響が乗る
            if (targetRoom <= ReverbRoomOff + 1f)
            {
                bgmReverbFilter.enabled = false;
            }
        }
    }
}
