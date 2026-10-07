using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 対局BGM「第3案」（2026-10-08）。
    ///
    /// 16曲のフルミックスを、フェイズと濃度に合わせて切り替える。
    /// 置き場所は `Resources/Bgm/Proposal3/`、名前は既存の曲名の頭に `p3_` を付けたもの。
    /// **どのフェイズでどの曲かは、既存の割当（<see cref="PhaseBgmNames"/>）をそのまま使う。**
    /// 割当を二重に持つと、片方だけ直して食い違う。
    ///
    /// **フルミックスだけを鳴らす。** 第3案を選んでいるあいだ、従来の2曲方式も
    /// 4層のステムも鳴らさない（二重に鳴ると音が濁る）。
    ///
    /// **音源を2本持ち、切り替えは必ず重ねて行う。** 1本で差し替えると一度 0 まで
    /// 落とすしかなく、切れ目が聞こえる。
    ///
    /// 切り替え方は相手によって2通り:
    ///   同じテンポ・同じ長さ（濃度1〜4 など）
    ///       … **再生位置を保ったまま**、次の小節頭から1小節かけて入れ替える。
    ///         伴奏の展開（基本→余白→返答→ひらく）もそのまま続く。
    ///         主旋律は全曲でサンプル単位に同じ波形なので、**直線**で入れ替える
    ///         （等パワーにすると、同じ波形どうしが足し合わさって山ができる）。
    ///   テンポや長さが違う（配牌、和了、既存案との行き来 など）
    ///       … いまの曲の小節頭で、次の曲を**1拍目から**入れる。
    ///         再生位置は引き継がない（テンポが違うと拍の途中に落ちる）。
    ///         前の曲は 0.25 秒で下げる。
    ///
    /// **予約は PlayScheduled。位置は音声の時計（dspTime）から計算する。**
    /// `AudioSource.timeSamples` は約 20ms 刻みでしか進まないので、それを元に
    /// 次の曲の頭出し位置を決めると、同じ主旋律が 10ms ずれて重なり、
    /// 入れ替えの1小節のあいだ音が濁る。
    ///
    /// **切り替えは「監督」のコルーチン1本が順に片付ける。**
    /// 設定を続けて変えられても、予約を積まない。監督は小節頭の直前まで待ってから
    /// **その時点の最新の行き先**を読むので、最後に選んだものだけが鳴る。
    /// </summary>
    public partial class AudioManager
    {
        [Header("Proposal 3 BGM")]
        [Tooltip("対局BGMを第3案（Resources/Bgm/Proposal3 のフルミックス16曲）で鳴らす。設定画面から切り替わる")]
        public bool UseProposal3Bgm = false;

        private const string P3Prefix = "p3_";
        private const string P3Folder = "Bgm/Proposal3/";

        /// <summary>予約してから鳴り出すまでに要る余裕（秒）。これより近い小節頭には間に合わない。</summary>
        private const double P3ScheduleLead = 0.12;

        /// <summary>
        /// 小節頭までこれ以下になったら、行き先を確定して予約する（秒）。
        /// 早く確定すると、そのあと届いた変更を取りこぼす。
        /// </summary>
        private const double P3CommitWindow = 0.22;

        /// <summary>テンポの違う曲へ移るとき、前の曲を下げきるまでの秒数。</summary>
        private const double P3ShortFade = 0.25;

        private AudioSource[] _p3Decks;
        private int _p3Active;
        private bool _p3Running;
        private string _p3ClipName;
        private Coroutine _p3Director;

        /// <summary>第3案の2本を入れ替えている最中か。音量の当て直しに上書きさせないために持つ。</summary>
        private bool _p3Fading;

        /// <summary>既存案の音を下げている最中か。同じ理由。</summary>
        private bool _p3FadingLegacy;

        /// <summary>曲が見つからないなど、これ以上進めない。監督を回し続けないための印。</summary>
        private bool _p3Stuck;

        // いま鳴っている側の「この時刻に、この位置だった」。位置は dspTime から計算する
        private double _p3AnchorDsp;
        private int _p3AnchorSample;

        /// <summary>設定で選ばれている案の番号。-1 は「まだ一度も当てていない」。</summary>
        private int _matchBgmSet = -1;

        /// <summary>既存案の音が、どの案の鳴らし方で鳴っているか。-1 は未確定。</summary>
        private int _legacySoundingKind = -1;

        /// <summary>
        /// 結果画面で勝敗の曲へ替えたか（"bgm_win" / "bgm_lose"）。
        /// 第3案へ切り替えたときも同じ場面の曲を選べるように覚えておく。
        /// </summary>
        private string _resultBgmOverride;

        private readonly Dictionary<string, AudioClip> _p3Clips = new Dictionary<string, AudioClip>();
        private readonly List<AudioSource> _p3LegacyFadeSources = new List<AudioSource>();
        private readonly List<float> _p3LegacyFadeFrom = new List<float>();

        public bool IsProposal3Running { get { return _p3Running; } }

        /// <summary>いま鳴っている第3案の曲名（`p3_` 付き）。鳴っていなければ null。確認用。</summary>
        public string Proposal3ClipName { get { return _p3Running ? _p3ClipName : null; } }

        /// <summary>切り替えの途中か（小節頭を待っている、または入れ替えている）。確認用。</summary>
        public bool IsProposal3Switching { get { return _p3Director != null; } }

        /// <summary>
        /// BGMの行き先を第3案の監督が握っているか。
        /// 握っているあいだ、既存案の経路（<see cref="ApplyLegacyPhaseBgm"/>）へは流さない。
        /// 第3案から既存案へ戻る途中もここに入る。流してしまうと、第3案が鳴っている上に
        /// 既存案が重なって二重に鳴る。
        /// </summary>
        private bool Proposal3Owns
        {
            get { return UseProposal3Bgm || _p3Running || _p3Director != null; }
        }

        private float P3Master { get { return bgmVolume * masterVolume; } }

        private bool LegacyBgmAudible
        {
            get
            {
                return IsPairBgmRunning || AreLayersRunning
                       || (bgmSource != null && bgmSource.isPlaying);
            }
        }

        /// <summary>いまのフェイズと濃度で、第3案のどの曲を鳴らすか。</summary>
        private string ResolveProposal3Name()
        {
            string baseName = (currentBgmPhase == RoundStatus.Result && _resultBgmOverride != null)
                ? _resultBgmOverride
                : ResolveBgmName(currentBgmPhase);
            return P3Prefix + baseName;
        }

        private AudioClip GetProposal3Clip(string name)
        {
            AudioClip clip;
            if (!_p3Clips.TryGetValue(name, out clip))
            {
                clip = Resources.Load<AudioClip>(P3Folder + name);
                _p3Clips[name] = clip;
                if (clip == null)
                    Debug.LogWarning("[AudioManager] 第3案の曲が見つかりません: Resources/" + P3Folder + name);
            }

            // **予約より前に読み込みを済ませる。** 取り込み設定が「先読みしない」なので、
            // 放っておくと鳴らす瞬間に読みに行き、予約した時刻に間に合わないことがある
            if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            return clip;
        }

        private void EnsureProposal3Decks()
        {
            if (_p3Decks != null) return;

            var host = new GameObject("BGM_Proposal3");
            host.transform.SetParent(transform, false);

            _p3Decks = new AudioSource[2];
            for (int i = 0; i < _p3Decks.Length; i++)
            {
                var src = host.AddComponent<AudioSource>();
                src.loop = true;
                src.playOnAwake = false;
                src.volume = 0f;
                _p3Decks[i] = src;
            }
        }

        /// <summary>行き先が変わったことを監督に知らせる。監督が居なければ起こす。</summary>
        private void NotifyProposal3()
        {
            if (!CanPlay) return;
            _p3Stuck = false;
            if (_p3Director == null) _p3Director = StartCoroutine(Proposal3Director());
        }

        /// <summary>
        /// 第3案を止める。**監督ごと止める**ので、待っていた切り替えも消える。
        /// タイトルやチュートリアルの曲を鳴らす前、BGMを止めるときに呼ぶ。
        /// </summary>
        private void StopProposal3()
        {
            if (_p3Director != null) { StopCoroutine(_p3Director); _p3Director = null; }

            // 既存案を下げている途中で止められたら、下げかけの音量を残さない
            if (_p3FadingLegacy) FinishLegacyFadeOut();

            if (_p3Decks != null)
            {
                for (int i = 0; i < _p3Decks.Length; i++)
                {
                    _p3Decks[i].Stop();
                    _p3Decks[i].volume = 0f;
                }
            }
            _p3Running = false;
            _p3Fading = false;
            _p3ClipName = null;
        }

        /// <summary>音量設定が変わったとき、鳴っている第3案にも反映する。</summary>
        private void ApplyProposal3Volumes()
        {
            // 入れ替え中は毎フレーム音量を計算し直しているので、ここでは触らない
            if (!_p3Running || _p3Fading || _p3Decks == null) return;
            _p3Decks[_p3Active].volume = P3Master;
        }

        // ------------------------------------------------------------
        //  監督
        // ------------------------------------------------------------

        private IEnumerator Proposal3Director()
        {
            while (!_p3Stuck)
            {
                if (UseProposal3Bgm)
                {
                    if (!_p3Running)
                    {
                        if (LegacyBgmAudible) yield return P3EnterFromLegacy();
                        else P3StartNow();
                    }
                    else if (ResolveProposal3Name() != _p3ClipName)
                    {
                        yield return P3ChangeClip();
                    }
                    else break;   // もう行き先の曲が鳴っている
                }
                else
                {
                    if (_p3Running)
                    {
                        yield return P3LeaveToLegacy();
                    }
                    else
                    {
                        // 第3案へ行きかけて取り消された場合など。既存案どうしの
                        // 選び直しがそのあいだに入っていたら、ここで鳴らし方を合わせる
                        if (LegacyBgmAudible && _legacySoundingKind != _matchBgmSet) ReapplyLegacySet();
                        _legacySoundingKind = _matchBgmSet;
                        break;
                    }
                }
            }
            _p3Director = null;
        }

        /// <summary>何も鳴っていない所から第3案を始める。待つ相手が居ないのですぐ鳴らす。</summary>
        private void P3StartNow()
        {
            string want = ResolveProposal3Name();
            var clip = GetProposal3Clip(want);
            if (clip == null) { _p3Stuck = true; return; }

            EnsureProposal3Decks();
            _p3Decks[1 - _p3Active].Stop();

            double at = AudioSettings.dspTime + P3ScheduleLead;
            var deck = _p3Decks[_p3Active];
            deck.Stop();
            deck.clip = clip;
            deck.loop = true;
            deck.timeSamples = 0;
            deck.volume = P3Master;
            deck.PlayScheduled(at);

            _p3AnchorDsp = at;
            _p3AnchorSample = 0;
            _p3ClipName = want;
            _p3Running = true;
        }

        /// <summary>
        /// 第3案の中で曲を替える（濃度が変わった、フェイズが変わった）。
        /// 次の小節頭から。同じテンポ・同じ長さなら位置を保ち、違えば1拍目から入れる。
        /// </summary>
        private IEnumerator P3ChangeClip()
        {
            // 鳴り出す前（予約しただけ）の音源からは小節が数えられない
            while (AudioSettings.dspTime < _p3AnchorDsp)
            {
                if (!UseProposal3Bgm) yield break;
                yield return null;
            }

            double dspAt, barPos, barSeconds;
            while (true)
            {
                if (!UseProposal3Bgm) yield break;
                if (ResolveProposal3Name() == _p3ClipName) yield break;   // 元へ戻された

                P3NextBar(out dspAt, out barPos, out barSeconds);
                if (dspAt - AudioSettings.dspTime <= P3CommitWindow) break;
                yield return null;
            }

            // **ここで初めて行き先を確定する。** 待っているあいだに変わっていれば新しい方になる
            string want = ResolveProposal3Name();
            var clip = GetProposal3Clip(want);
            if (clip == null) { _p3Stuck = true; yield break; }

            var cur = _p3Decks[_p3Active];
            var next = _p3Decks[1 - _p3Active];

            bool sameFamily = cur.clip != null
                              && cur.clip.samples == clip.samples
                              && SameTempo(cur.clip.name, clip.name);
            int startSample = 0;
            if (sameFamily)
            {
                long s = (long)Math.Round(barPos * clip.frequency);
                startSample = (int)(s % clip.samples);
            }

            next.Stop();
            next.clip = clip;
            next.loop = true;
            next.timeSamples = startSample;
            next.volume = sameFamily ? 0f : P3Master;   // 予約の時刻までは鳴らないので、先に上げておいてよい
            next.PlayScheduled(dspAt);

            _p3Fading = true;
            while (AudioSettings.dspTime < dspAt)
            {
                if (!sameFamily) next.volume = P3Master;
                yield return null;
            }

            // ここから新しい曲が鳴っている。拍の時計もこちらを見る
            _p3Active = 1 - _p3Active;
            _p3AnchorDsp = dspAt;
            _p3AnchorSample = startSample;
            _p3ClipName = want;

            double fade = sameFamily ? Math.Max(barSeconds, P3ShortFade) : P3ShortFade;
            while (true)
            {
                double u = (AudioSettings.dspTime - dspAt) / fade;
                if (u >= 1.0) break;

                // 音量スライダーを動かされても付いていくよう、毎フレーム読み直す
                float master = P3Master;
                cur.volume = master * (float)(1.0 - u);
                next.volume = sameFamily ? master * (float)u : master;
                yield return null;
            }

            cur.Stop();
            cur.volume = 0f;
            next.volume = P3Master;
            _p3Fading = false;
        }

        /// <summary>
        /// 既存案（2曲・層・1本もの、タイトルや部屋の曲も含む）から第3案へ。
        /// いまの曲の小節頭で第3案を1拍目から入れ、前の音は短く下げて止める。
        /// </summary>
        private IEnumerator P3EnterFromLegacy()
        {
            double dspAt;
            while (true)
            {
                if (!UseProposal3Bgm) yield break;      // 待っているあいだに戻された。まだ何も変えていない
                if (!LegacyBgmAudible) yield break;     // 止められた。監督が「何も無い所から」で始め直す

                double wait = LegacySecondsToNextBar();
                if (wait <= 0.0)
                {
                    // テンポの分からない曲（タイトル・部屋・チュートリアル）は小節を待てない
                    dspAt = AudioSettings.dspTime + P3ScheduleLead;
                    break;
                }
                if (wait >= P3ScheduleLead && wait <= P3CommitWindow)
                {
                    dspAt = AudioSettings.dspTime + wait;
                    break;
                }
                yield return null;
            }

            string want = ResolveProposal3Name();
            var clip = GetProposal3Clip(want);
            if (clip == null) { _p3Stuck = true; yield break; }

            EnsureProposal3Decks();
            _p3Decks[1 - _p3Active].Stop();
            var deck = _p3Decks[_p3Active];
            deck.Stop();
            deck.clip = clip;
            deck.loop = true;
            deck.timeSamples = 0;
            deck.volume = P3Master;
            deck.PlayScheduled(dspAt);

            while (AudioSettings.dspTime < dspAt)
            {
                deck.volume = P3Master;
                yield return null;
            }

            _p3AnchorDsp = dspAt;
            _p3AnchorSample = 0;
            _p3ClipName = want;
            _p3Running = true;

            // 前の音を下げる。**向こうのフェードを止めてからでないと、音量を取り合う**
            BeginLegacyFadeOut();
            while (true)
            {
                double u = (AudioSettings.dspTime - dspAt) / P3ShortFade;
                if (u >= 1.0) break;
                for (int i = 0; i < _p3LegacyFadeSources.Count; i++)
                {
                    if (_p3LegacyFadeSources[i] != null)
                        _p3LegacyFadeSources[i].volume = _p3LegacyFadeFrom[i] * (float)(1.0 - u);
                }
                yield return null;
            }
            FinishLegacyFadeOut();
        }

        /// <summary>
        /// 第3案から既存案へ。第3案の小節頭で既存案を1拍目から入れ、第3案は短く下げて止める。
        /// </summary>
        private IEnumerator P3LeaveToLegacy()
        {
            while (AudioSettings.dspTime < _p3AnchorDsp)
            {
                if (UseProposal3Bgm) yield break;
                yield return null;
            }

            double dspAt, barPos, barSeconds;
            while (true)
            {
                if (UseProposal3Bgm) yield break;   // 戻された。まだ何も変えていない

                P3NextBar(out dspAt, out barPos, out barSeconds);
                if (dspAt - AudioSettings.dspTime <= P3CommitWindow) break;
                yield return null;
            }

            // 2曲方式と層は、呼んでから少し先の時刻に予約して鳴り出す。
            // そのぶん早く呼んで、鳴り出しを小節頭に合わせる
            double callAt = LegacyStartIsScheduled() ? dspAt - PairStartLead : dspAt;
            while (AudioSettings.dspTime < callAt)
            {
                yield return null;
            }

            if (UseProposal3Bgm) yield break;

            currentPhaseBgmName = null;   // 「もうその曲を鳴らしている」と誤判定させない
            if (CanPlay && UsePhaseBgm)
            {
                ApplyLegacyPhaseBgm();
                if (_resultBgmOverride != null && currentBgmPhase == RoundStatus.Result) ApplyLegacyResultBgm();
            }
            _legacySoundingKind = _matchBgmSet;

            while (AudioSettings.dspTime < dspAt) yield return null;

            var deck = _p3Decks[_p3Active];
            _p3Fading = true;
            while (true)
            {
                double u = (AudioSettings.dspTime - dspAt) / P3ShortFade;
                if (u >= 1.0) break;
                deck.volume = P3Master * (float)(1.0 - u);
                yield return null;
            }

            for (int i = 0; i < _p3Decks.Length; i++)
            {
                _p3Decks[i].Stop();
                _p3Decks[i].volume = 0f;
            }
            _p3Fading = false;
            _p3Running = false;
            _p3ClipName = null;
        }

        // ------------------------------------------------------------
        //  位置の計算
        // ------------------------------------------------------------

        private static double PositiveMod(double value, double length)
        {
            if (length <= 0.0) return 0.0;
            double m = value % length;
            return m < 0.0 ? m + length : m;
        }

        /// <summary>
        /// 指定した時刻（dspTime）に、いま鳴っている第3案が曲のどこに居るか（秒）。
        ///
        /// 鳴らし始めた時刻と位置から計算する。エディタの一時停止などで
        /// 実際の位置と離れていたら、実際の位置へ合わせ直す。
        /// </summary>
        private double P3PositionAt(double dsp)
        {
            var deck = _p3Decks[_p3Active];
            var clip = deck.clip;
            double freq = clip.frequency;
            double length = clip.samples / freq;

            double now = AudioSettings.dspTime;
            if (now > _p3AnchorDsp + 0.05 && deck.isPlaying)
            {
                double estimated = PositiveMod(_p3AnchorSample / freq + (now - _p3AnchorDsp), length);
                double actual = deck.timeSamples / freq;
                double diff = Math.Abs(estimated - actual);
                if (diff > length * 0.5) diff = length - diff;   // 継ぎ目をまたいだ差
                if (diff > 0.08)
                {
                    _p3AnchorDsp = now;
                    _p3AnchorSample = deck.timeSamples;
                }
            }

            return PositiveMod(_p3AnchorSample / freq + (dsp - _p3AnchorDsp), length);
        }

        /// <summary>
        /// いま鳴っている第3案の、次に予約が間に合う小節頭。
        /// </summary>
        /// <param name="dspAt">その小節頭が来る時刻（dspTime）</param>
        /// <param name="barPos">その小節頭の、曲の中での位置（秒）。曲の長さを超えることがある</param>
        /// <param name="barSeconds">1小節の長さ（秒）。テンポが分からなければ 0</param>
        private void P3NextBar(out double dspAt, out double barPos, out double barSeconds)
        {
            double now = AudioSettings.dspTime;
            double pos = P3PositionAt(now);

            Tempo t = TempoOf(_p3ClipName);
            if (t.Bpm <= 0f)
            {
                barSeconds = 0.0;
                dspAt = now + P3ScheduleLead;
                barPos = pos + P3ScheduleLead;
                return;
            }

            barSeconds = 60.0 / t.Bpm * t.BeatsPerBar;
            double index = Math.Floor(pos / barSeconds + 1e-9) + 1.0;
            double wait = index * barSeconds - pos;
            if (wait < P3ScheduleLead)
            {
                // 近すぎて予約が間に合わない。その次の小節頭にする
                index += 1.0;
                wait += barSeconds;
            }
            dspAt = now + wait;
            barPos = index * barSeconds;
        }

        /// <summary>
        /// 既存案の、次の小節頭までの秒数。テンポが分からない曲や、何も鳴っていないときは 0。
        /// </summary>
        private double LegacySecondsToNextBar()
        {
            AudioSource src = null;
            string name = null;

            if (IsPairBgmRunning && _pairNormalSource != null)
            {
                src = _pairNormalSource;
                name = PairNormalClip;
            }
            else if (AreLayersRunning && _layerSources != null && _layerSources[0] != null
                     && _layerSources[0].clip != null)
            {
                src = _layerSources[0];
                name = src.clip.name;
            }
            else if (bgmSource != null && bgmSource.isPlaying && bgmSource.clip != null)
            {
                src = bgmSource;
                name = src.clip.name;
            }
            if (src == null) return 0.0;

            Tempo t = TempoOf(name);
            if (t.Bpm <= 0f) return 0.0;

            double bar = 60.0 / t.Bpm * t.BeatsPerBar;
            double into = src.time % bar;
            return bar - into;
        }

        /// <summary>
        /// いまのフェイズで既存案を鳴らし始めると、予約（少し先の時刻）で鳴り出すか。
        /// 2曲方式と層はそう。1本ものは呼んだ瞬間に鳴る。
        /// </summary>
        private bool LegacyStartIsScheduled()
        {
            if (UsePairBgm && PairHandles(currentBgmPhase)) return true;
            if (UseBgmLayers)
            {
                string want = ResolveBgmName(currentBgmPhase);
                if (want != null && want.StartsWith("bgm_field_")) return true;
            }
            return false;
        }

        // ------------------------------------------------------------
        //  既存案の音を下げて止める
        // ------------------------------------------------------------

        private void BeginLegacyFadeOut()
        {
            _p3FadingLegacy = true;

            // 向こうで動いているフェードを止める。動かしたままだと音量を取り合う
            if (_pairFade != null) { StopCoroutine(_pairFade); _pairFade = null; }
            if (_layerFades != null)
            {
                for (int i = 0; i < _layerFades.Length; i++)
                {
                    if (_layerFades[i] != null) { StopCoroutine(_layerFades[i]); _layerFades[i] = null; }
                }
            }
            if (bgmSwapCoroutine != null) { StopCoroutine(bgmSwapCoroutine); bgmSwapCoroutine = null; }

            _p3LegacyFadeSources.Clear();
            _p3LegacyFadeFrom.Clear();
            AddLegacyFadeSource(_pairNormalSource);
            AddLegacyFadeSource(_pairTurnSource);
            if (_layerSources != null)
            {
                for (int i = 0; i < _layerSources.Length; i++) AddLegacyFadeSource(_layerSources[i]);
            }
            AddLegacyFadeSource(bgmSource);
        }

        private void AddLegacyFadeSource(AudioSource src)
        {
            if (src == null || !src.isPlaying) return;
            _p3LegacyFadeSources.Add(src);
            _p3LegacyFadeFrom.Add(src.volume);
        }

        private void FinishLegacyFadeOut()
        {
            StopPairBgm();
            StopLayeredBgm();
            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.volume = bgmVolume * masterVolume;   // 次に鳴らすときのために戻す
            }
            currentPhaseBgmName = null;

            _p3LegacyFadeSources.Clear();
            _p3LegacyFadeFrom.Clear();
            _p3FadingLegacy = false;
        }

        // ------------------------------------------------------------
        //  設定からの切り替え
        // ------------------------------------------------------------

        private int MatchBgmKindFromLegacyFlags()
        {
            if (UsePairBgm) return (int)Core.SettingsManager.MatchBgmSetKind.Pair;
            return UseBgmLayers
                ? (int)Core.SettingsManager.MatchBgmSetKind.PerPhase
                : (int)Core.SettingsManager.MatchBgmSetKind.PerPhaseNoLayers;
        }

        /// <summary>
        /// 既存案どうしの選び直しを、いまの音に反映する。
        /// 行き先で使わない鳴らし方を先に畳んでから、いまのフェイズで鳴らし直す。
        /// 畳まずに鳴らし直すと、前の鳴らし方が残ったまま上に重なる。
        /// </summary>
        private void ReapplyLegacySet()
        {
            if (!UsePairBgm) StopPairBgm();
            if (!UseBgmLayers && AreLayersRunning) StopLayeredBgm();
            if (UsePairBgm && bgmSource != null && bgmSource.isPlaying) bgmSource.Stop();

            // 「もうその曲を鳴らしている」と誤判定されて無音のままになるのを防ぐ
            currentPhaseBgmName = null;

            if (CanPlay && UsePhaseBgm) ApplyLegacyPhaseBgm();
        }
    }
}
