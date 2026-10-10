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
        ///
        /// 2026-10-10 に 200Hz へ下げた（ユーザー「水に沈んだ感じが少し弱い。もっと沈めてよい」）。
        /// ただし弱く聞こえた主な理由はここではなく、音量だった（<see cref="DeepSinkGain"/>）。
        /// </summary>
        private const float DeepMuffledCutoff = 200f;

        /// <summary>
        /// 沈んでいるあいだ、BGM の音量に掛ける倍率（2026-10-10）。
        ///
        /// **それまでの「沈み」は、沈むどころか BGM を大きくしていた。** 実機で測ると、高い音は消えるが
        /// 残響が低い音を足すので、全体では 7〜14dB 上がっていた（第4案の手牌選択で -26.8 → -19.9dB、
        /// 賭けの合図で -20.8 → -6.7dB）。低音が前に出てきて「沈んだ」に聞こえない。
        /// 音源の音量そのものを下げ、残響も絞って（<see cref="DeepReverbLevel"/>）、沈むと小さくなるようにした。
        ///
        /// **Web版ではこの倍率だけが効く。** Unity の Web 版は音のフィルター（ローパス・残響）を
        /// 使えないので、上のカットオフも残響も何も起きない。音量だけは下げられる。
        /// </summary>
        private const float DeepSinkGain = 0.4f;

        /// <summary>沈んでいるあいだの残響の量（ミリベル）。もとは 800 で、これが低音を大きくしていた。</summary>
        private const float DeepReverbLevel = -400f;

        /// <summary>いまの沈みの倍率。1 で等倍。</summary>
        private float _bgmSink = 1f;
        private Coroutine _bgmSinkFade;

        /// <summary>曲を小さくして止めている最中か（<see cref="FadeOutBgm"/>）。そのあいだは音量を当て直さない。</summary>
        private bool _bgmFadingOut;

        /// <summary>
        /// BGM の音源に入れる音量。設定の音量に、沈みの倍率を掛けたもの。
        /// **BGM の音量を決める所は、必ずこれを使う。** `bgmVolume * masterVolume` を直に書くと、
        /// 沈んでいるあいだに鳴り始めた曲だけが大きいままになる。
        /// </summary>
        private float BgmMaster { get { return bgmVolume * masterVolume * _bgmSink; } }

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
            FadeDeepExtras(on, fadeDuration);
            FadeBgmSink(on ? DeepSinkGain : 1f, fadeDuration);
        }

        /// <summary>沈みの倍率を動かす。動いているあいだは、鳴っている BGM の音量を毎コマ当て直す。</summary>
        private void FadeBgmSink(float target, float duration)
        {
            if (_bgmSinkFade != null) StopCoroutine(_bgmSinkFade);
            _bgmSinkFade = StartCoroutine(BgmSinkFadeRoutine(target, duration));
        }

        private System.Collections.IEnumerator BgmSinkFadeRoutine(float target, float duration)
        {
            // 音量も耳には対数で聞こえるので、倍率は log で補間する
            float logStart = Mathf.Log(Mathf.Max(_bgmSink, 0.01f));
            float logTarget = Mathf.Log(Mathf.Max(target, 0.01f));

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                t = t * t * (3f - 2f * t);
                _bgmSink = Mathf.Exp(Mathf.Lerp(logStart, logTarget, t));
                ApplyBgmVolumes();
                yield return null;
            }
            _bgmSink = target;
            ApplyBgmVolumes();

            // 層や2曲のフェードは、始めたときの音量を行き先に持っている（フェード中は当て直しが触らない）。
            // 倍率が動いているあいだに始まったフェードが古い音量で終わるので、少しのあいだ当て直し続ける
            float tail = 2.5f;
            while (tail > 0f)
            {
                tail -= Time.deltaTime;
                ApplyBgmVolumes();
                yield return null;
            }
            _bgmSinkFade = null;
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
                bgmReverbFilter.reverbLevel = DeepReverbLevel;
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

        // ------------------------------------------------------------
        //  深いこもりを、ほかの対局BGMの音源にも掛ける（2026-10-09）
        //
        //  **上のフィルターは BGM_Source（1本ものの曲）にしか付いていない。**
        //  Unity のフィルターは「同じ GameObject に付いている音源」にしか効かないので、
        //  別の GameObject で鳴る対局BGM（新2曲＝BGM_Pair、層＝BGM_Layers、
        //  第3案・第4案＝BGM_Proposal3、流局のモチーフ）は、透視を使っても沈まなかった。
        //  スキルの演出すべてに「水の中」を掛ける指示を受けて調べ、分かった。
        //
        //  **掛けるのは深いこもりだけ。** フェイズのこもり（打牌以外で 1000Hz）は足さない。
        //  あちらまで足すと、新2曲・第3案・第4案の普段の聞こえ方が変わってしまう
        //  （これらは今までこもり無しで鳴っていて、その音で採用が決まっている）。
        //  だからここのフィルターは、深いこもりの間だけ入れて、抜けたら切る。
        // ------------------------------------------------------------

        private readonly List<AudioLowPassFilter> _deepExtraLowPass = new List<AudioLowPassFilter>();
        private readonly List<AudioReverbFilter> _deepExtraReverb = new List<AudioReverbFilter>();
        private Coroutine _deepExtraFade;

        /// <summary>深いこもりを掛けたフィルターの数。確認用。</summary>
        public int DeepMuffleExtraCount { get { return _deepExtraLowPass.Count; } }

        /// <summary>いま深いこもりの最中か。確認用。</summary>
        public bool IsBgmDeepMuffled { get { return _deepMuffle; } }

        /// <summary>
        /// 対局BGMを鳴らしている、BGM_Source 以外の置き場にフィルターを用意する。
        /// 置き場は鳴らし始めたときに作られるので、沈めるたびに見直す。
        /// </summary>
        private void EnsureDeepExtraFilters()
        {
            _deepExtraLowPass.RemoveAll(f => f == null);
            _deepExtraReverb.RemoveAll(f => f == null);

            AddDeepExtraHost(_pairNormalSource);
            if (_layerSources != null && _layerSources.Length > 0) AddDeepExtraHost(_layerSources[0]);
            if (_p3Decks != null && _p3Decks.Length > 0) AddDeepExtraHost(_p3Decks[0]);
            AddDeepExtraHost(_p4Motif);
        }

        private void AddDeepExtraHost(AudioSource source)
        {
            if (source == null) return;
            GameObject host = source.gameObject;
            if (bgmSource != null && host == bgmSource.gameObject) return;   // こちらは上のフィルターが受け持つ

            var lowPass = host.GetComponent<AudioLowPassFilter>();
            if (lowPass == null)
            {
                lowPass = host.AddComponent<AudioLowPassFilter>();
                lowPass.cutoffFrequency = OpenCutoff;
                lowPass.enabled = false;
            }
            if (!_deepExtraLowPass.Contains(lowPass)) _deepExtraLowPass.Add(lowPass);

            var reverb = host.GetComponent<AudioReverbFilter>();
            if (reverb == null)
            {
                reverb = host.AddComponent<AudioReverbFilter>();
                // 値は BGM_Source のものと同じ（FadeReverb）
                reverb.reverbPreset = AudioReverbPreset.User;
                reverb.dryLevel = 0f;
                reverb.room = ReverbRoomOff;
                reverb.roomHF = -3500f;
                reverb.decayTime = 3.5f;
                reverb.reverbLevel = DeepReverbLevel;
                reverb.reverbDelay = 0.02f;
                reverb.diffusion = 100f;
                reverb.density = 100f;
                reverb.enabled = false;
            }
            if (!_deepExtraReverb.Contains(reverb)) _deepExtraReverb.Add(reverb);
        }

        private void FadeDeepExtras(bool on, float duration)
        {
            if (on) EnsureDeepExtraFilters();
            if (_deepExtraLowPass.Count == 0 && _deepExtraReverb.Count == 0) return;

            if (_deepExtraFade != null) StopCoroutine(_deepExtraFade);
            _deepExtraFade = StartCoroutine(DeepExtraFadeRoutine(on, duration));
        }

        private System.Collections.IEnumerator DeepExtraFadeRoutine(bool on, float duration)
        {
            float targetFreq = on ? DeepMuffledCutoff : OpenCutoff;
            float targetRoom = on ? ReverbRoomOn : ReverbRoomOff;

            // いまの値から動かす。沈みきる前に抜ける（その逆も）ことがある
            float startFreq = OpenCutoff;
            float startRoom = ReverbRoomOff;
            foreach (var f in _deepExtraLowPass)
            {
                if (f == null) continue;
                if (f.enabled) startFreq = Mathf.Max(f.cutoffFrequency, 20f);
                f.cutoffFrequency = startFreq;
                f.enabled = true;
            }
            foreach (var r in _deepExtraReverb)
            {
                if (r == null) continue;
                if (r.enabled) startRoom = r.room;
                r.room = startRoom;
                r.enabled = true;
            }

            float logStart = Mathf.Log(startFreq);
            float logTarget = Mathf.Log(targetFreq);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                t = t * t * (3f - 2f * t);
                float freq = Mathf.Exp(Mathf.Lerp(logStart, logTarget, t));
                float room = Mathf.Lerp(startRoom, targetRoom, t);
                foreach (var f in _deepExtraLowPass) if (f != null) f.cutoffFrequency = freq;
                foreach (var r in _deepExtraReverb) if (r != null) r.room = room;
                yield return null;
            }

            foreach (var f in _deepExtraLowPass)
            {
                if (f == null) continue;
                f.cutoffFrequency = targetFreq;
                // 抜けきったら切る。入れたままだと、普段の音まで少し変わる
                if (!on) f.enabled = false;
            }
            foreach (var r in _deepExtraReverb)
            {
                if (r == null) continue;
                r.room = targetRoom;
                if (!on) r.enabled = false;
            }
            _deepExtraFade = null;
        }
    }
}
