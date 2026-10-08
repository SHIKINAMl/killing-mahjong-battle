using System;
using System.Collections;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 対局BGM「第4案」（2026-10-08、「回路圧」音量・強弱調整 v3 の16曲）。
    ///
    /// 置き場所は `Resources/Bgm/Proposal4/`、名前は既存の曲名の頭に `p4_` を付けたもの。
    /// どのフェイズでどの曲かは、第3案と同じく既存の割当（<see cref="PhaseBgmNames"/>）を使う。
    ///
    /// **音源2本・小節頭の予約・監督のコルーチンは、第3案のもの（AudioManager.Proposal3.cs）を共用する。**
    /// そちらの `_p3〜` という名前は「第3案・第4案の共用」と読むこと。
    /// 第3案と第4案はテンポも長さも違うので、行き来は「いまの曲の小節頭で、次を1拍目から入れる」になる
    /// （既存案との行き来と同じ扱い）。
    ///
    /// **第4案だけの決まり（第3案の前提を持ち込まないこと）:**
    ///
    ///   全曲 136BPM・4拍子・64小節・同じ長さ
    ///       … 第4案の中では、どの曲へ移るときも**再生位置を保つ**。
    ///
    ///   主旋律は曲ごとに違う。濃度1〜4だけが共通の土台を持つ
    ///       … 濃度どうしは1小節かけて**直線**で入れ替える（共通の土台が足し合わさって膨らまないように）。
    ///         それ以外は、小節頭で**短く**（0.25秒）入れ替える。長く重ねると主旋律どうしが濁る。
    ///
    ///   流局は2本に分けて鳴らす（土台 base ＋ モチーフ motif）
    ///       … 同じ時刻・同じ位置・同じ大きさ。足すと完成版 `p4_bgm_draw` と同じ音になる。
    ///         **完成版をこの2本に重ねてはいけない。** 完成版は自動では鳴らさない。
    ///         **モチーフは、流局でなくなった瞬間に止める**（小節頭も入れ替えの終わりも待たない）。
    ///         土台は、ほかの曲と同じく小節頭で短くつなぐ。
    ///
    ///   1小節は 77,823.5 サンプル（4小節で 311,294）
    ///       … テンポから計算した長さとわずかに違う。小節頭は**実際の曲の長さ**から割り出す。
    ///
    ///   音量は曲ごとに違えてある（静かな場面は小さく、盛り上がりは大きく）
    ///       … **そろえ直さない。** 全曲を同じ BGM 音量 × 全体音量で鳴らすだけ。
    ///
    /// 自動では鳴らない曲（取り込みだけ）: p4_bgm_phase_normal / p4_bgm_phase_turn /
    /// p4_bgm_discard / p4_bgm_discard_hot / p4_bgm_draw。
    /// 既存の割当に出番が無いためで、第3案の同名の曲も同じ扱い。
    /// </summary>
    public partial class AudioManager
    {
        [Header("Proposal 4 BGM")]
        [Tooltip("対局BGMを第4案（Resources/Bgm/Proposal4 の16曲）で鳴らす。設定画面から切り替わる")]
        public bool UseProposal4Bgm = false;

        private const string P4Prefix = "p4_";
        private const string P4Folder = "Bgm/Proposal4/";
        private const string P4FieldPrefix = "p4_bgm_field_";
        private const string P4DrawBaseName = "p4_bgm_draw_base";
        private const string P4DrawMotifName = "p4_bgm_draw_motif";

        /// <summary>主旋律の違う曲どうしを入れ替える長さ（秒）。長いと主旋律どうしが濁る。</summary>
        private const double P4PhaseFade = 0.25;

        private AudioSource _p4Motif;
        private DrawMotifGate _p4MotifGate;

        /// <summary>モチーフを鳴らしている（予約済みを含む）か。</summary>
        private bool _p4MotifAlive;

        /// <summary>モチーフが音量を合わせる相手（流局の土台を鳴らしている側の音源）。</summary>
        private AudioSource _p4MotifDeck;

        /// <summary>モチーフが鳴り出す時刻。これより前なら、まだ音は出ていない。</summary>
        private double _p4MotifStartDsp;

        /// <summary>止める処理の通し番号。止めたあとに鳴らし直されたら、古い「止める」を無効にする。</summary>
        private int _p4MotifGen;

        /// <summary>第3案か第4案を選んでいるか。</summary>
        private bool ProposalWanted { get { return UseProposal3Bgm || UseProposal4Bgm; } }

        /// <summary>いま鳴っているのが第何案か（3 / 4）。どちらでもなければ 0。確認用。</summary>
        public int PlayingProposal
        {
            get
            {
                if (!_p3Running || _p3ClipName == null) return 0;
                return _p3ClipName.StartsWith(P4Prefix) ? 4 : 3;
            }
        }

        /// <summary>流局のモチーフが鳴っているか（予約済みを含む）。確認用。</summary>
        public bool IsDrawMotifAlive { get { return _p4MotifAlive; } }

        /// <summary>モチーフの音源が実際に再生中か。止めたあとに残っていないかの確認用。</summary>
        public bool IsDrawMotifSourcePlaying { get { return _p4Motif != null && _p4Motif.isPlaying; } }

        private static bool P4IsField(string name)
        {
            return name != null && name.StartsWith(P4FieldPrefix);
        }

        /// <summary>
        /// 第4案でのこの曲名。流局だけは完成版ではなく土台（base）を返す
        /// （モチーフは <see cref="P4StartMotifWith"/> が横で鳴らす）。
        /// </summary>
        private static string P4NameFor(string baseName)
        {
            return baseName == "bgm_draw" ? P4DrawBaseName : P4Prefix + baseName;
        }

        /// <summary>
        /// 1小節の長さ（秒）。第4案は実際の曲の長さから割り出す
        /// （64小節が整数サンプルで書き出されていて、テンポからの計算とは 1 周で約2サンプルずれる）。
        /// </summary>
        private static double ProposalBarSeconds(string clipName, AudioClip clip)
        {
            Tempo t = TempoOf(clipName);
            if (t.Bpm <= 0f) return 0.0;

            double bar = 60.0 / t.Bpm * t.BeatsPerBar;
            if (clip != null && clipName != null && clipName.StartsWith(P4Prefix) && clip.frequency > 0)
            {
                double length = (double)clip.samples / clip.frequency;
                double bars = Math.Round(length / bar);
                if (bars >= 1.0) bar = length / bars;
            }
            return bar;
        }

        /// <summary>
        /// 入れ替えにかける長さ（秒）。
        /// </summary>
        /// <param name="keepsPosition">再生位置を保って入れ替えるか（同じテンポ・同じ長さ）</param>
        private static double ProposalFadeSeconds(string from, string to, bool keepsPosition, double barSeconds)
        {
            if (!keepsPosition) return P3ShortFade;

            if (to != null && to.StartsWith(P4Prefix))
            {
                // 濃度どうしは共通の土台が続くので、1小節かけてゆっくり。
                // 主旋律の違う曲は、重ねる時間を短くする
                return (P4IsField(from) && P4IsField(to)) ? Math.Max(barSeconds, P3ShortFade) : P4PhaseFade;
            }

            // 第3案: 主旋律が全曲同じなので、1小節かけて入れ替える
            return Math.Max(barSeconds, P3ShortFade);
        }

        // ------------------------------------------------------------
        //  流局のモチーフ
        // ------------------------------------------------------------

        private void EnsureP4Motif()
        {
            if (_p4Motif != null) return;

            // **専用の GameObject に置く。** 弁（DrawMotifGate）は同じ GameObject の音にだけ掛かる。
            // ほかの音源と同居させると、そちらまで絞ってしまう
            var host = new GameObject("BGM_Proposal4_DrawMotif");
            host.transform.SetParent(transform, false);

            _p4Motif = host.AddComponent<AudioSource>();
            _p4Motif.loop = true;
            _p4Motif.playOnAwake = false;
            _p4Motif.volume = 0f;

            _p4MotifGate = host.AddComponent<DrawMotifGate>();
        }

        /// <summary>
        /// 流局の土台を予約したのと同じ時刻・同じ位置で、モチーフも予約する。
        /// 土台以外の曲のときは何もしない。
        /// </summary>
        private void P4StartMotifWith(AudioSource deck, string clipName, double dspAt, int startSample)
        {
            if (clipName != P4DrawBaseName || deck == null) return;

            var clip = GetProposal3Clip(P4DrawMotifName);
            if (clip == null) return;   // 見つからない旨は読み込み側が出している。土台だけで鳴らす

            EnsureP4Motif();
            _p4MotifGen++;

            _p4Motif.Stop();
            _p4Motif.clip = clip;
            _p4Motif.loop = true;
            _p4Motif.timeSamples = clip.samples > 0 ? startSample % clip.samples : 0;
            _p4Motif.volume = deck.volume;
            _p4MotifGate.OpenNow();
            _p4Motif.PlayScheduled(dspAt);

            _p4MotifAlive = true;
            _p4MotifDeck = deck;
            _p4MotifStartDsp = dspAt;
        }

        /// <summary>モチーフの大きさを、土台を鳴らしている音源に合わせる。入れ替えの最中は毎コマ呼ぶ。</summary>
        private void P4SyncMotifVolume()
        {
            if (!_p4MotifAlive || _p4Motif == null || _p4MotifDeck == null) return;
            _p4Motif.volume = _p4MotifDeck.volume;
        }

        /// <summary>
        /// いまモチーフが鳴っていてよいか。第4案を選んでいて、流局の場面のときだけ。
        /// </summary>
        private bool P4MotifWanted
        {
            get
            {
                if (!UseProposal4Bgm) return false;
                if (currentBgmPhase == RoundStatus.Result && _resultBgmOverride != null) return false;
                return ResolveBgmName(currentBgmPhase) == "bgm_draw";
            }
        }

        /// <summary>
        /// 行き先が変わったときに呼ぶ。流局でなくなっていたら、**その場で**モチーフを止める。
        /// 土台のほうは監督が小節頭でつなぐので、ここでは触らない。
        /// </summary>
        private void P4OnDestinationChanged()
        {
            if (_p4MotifAlive && !P4MotifWanted) P4KillMotif();
        }

        /// <summary>
        /// モチーフを止める。鳴っている最中なら、音声処理の側で約8ミリ秒かけて絞ってから止める
        /// （絞り始めは次の音声ブロックから。<see cref="DrawMotifGate"/>）。
        /// </summary>
        private void P4KillMotif()
        {
            if (!_p4MotifAlive || _p4Motif == null) return;

            _p4MotifAlive = false;
            _p4MotifDeck = null;
            int gen = ++_p4MotifGen;

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL は音声処理に割り込めない（OnAudioFilterRead が呼ばれない）。その場で止める
            _p4Motif.volume = 0f;
            _p4Motif.Stop();
#else
            // まだ鳴り出していない（予約しただけ）なら、絞るものが無い
            if (AudioSettings.dspTime < _p4MotifStartDsp || !_p4Motif.isPlaying)
            {
                _p4Motif.Stop();
                _p4Motif.volume = 0f;
                return;
            }

            _p4MotifGate.Close();
            StartCoroutine(P4StopMotifAfterRamp(gen));
#endif
        }

        private IEnumerator P4StopMotifAfterRamp(int gen)
        {
            // 絞りきるのを待つ。弁が動かない環境に備えて、待つのは長くても 0.15 秒
            double giveUp = AudioSettings.dspTime + 0.15;
            while (gen == _p4MotifGen && !_p4MotifGate.IsSilent && AudioSettings.dspTime < giveUp)
            {
                yield return null;
            }

            // 待っているあいだに鳴らし直されていたら、止めてはいけない
            if (gen != _p4MotifGen || _p4Motif == null) yield break;
            _p4Motif.Stop();
            _p4Motif.volume = 0f;
        }

        /// <summary>BGMを丸ごと止めるとき用。絞らずに止める（ほかの音も同時に切れるので）。</summary>
        private void P4StopMotifNow()
        {
            _p4MotifGen++;
            _p4MotifAlive = false;
            _p4MotifDeck = null;
            if (_p4Motif != null)
            {
                _p4Motif.Stop();
                _p4Motif.volume = 0f;
            }
        }

        /// <summary>
        /// 流局の土台は鳴っているのに、モチーフが止まっている。次の小節頭から、同じ位置で鳴らし直す。
        ///
        /// 流局 → 別の場面 → すぐ流局、と土台が入れ替わる前に戻ってきたときにだけ起きる。
        /// </summary>
        private IEnumerator P4ResumeMotif()
        {
            // 鳴り出す前（予約しただけ）の音源からは小節が数えられない
            while (AudioSettings.dspTime < _p3AnchorDsp)
            {
                if (!P4MotifWanted) yield break;
                yield return null;
            }

            double dspAt, barPos, barSeconds;
            while (true)
            {
                if (!P4MotifWanted || !_p3Running || _p3ClipName != P4DrawBaseName) yield break;

                if (GetProposal3Clip(P4DrawMotifName) == null) { _p3Stuck = true; yield break; }

                P3NextBar(out dspAt, out barPos, out barSeconds);
                if (dspAt - AudioSettings.dspTime <= P3CommitWindow) break;
                yield return null;
            }

            var deck = _p3Decks[_p3Active];
            long s = (long)Math.Round(barPos * deck.clip.frequency);
            P4StartMotifWith(deck, _p3ClipName, dspAt, (int)(s % deck.clip.samples));
            if (!_p4MotifAlive) { _p3Stuck = true; yield break; }

            // 土台は鳴りっぱなしの所へ途中から足すので、鳴り出しだけ短く持ち上げる
            _p4Motif.volume = 0f;
            int gen = _p4MotifGen;
            while (AudioSettings.dspTime < dspAt)
            {
                if (gen != _p4MotifGen) yield break;
                yield return null;
            }
            while (gen == _p4MotifGen && _p4MotifAlive)
            {
                double u = (AudioSettings.dspTime - dspAt) / 0.05;
                if (u >= 1.0) break;
                _p4Motif.volume = deck.volume * (float)u;
                yield return null;
            }
            if (gen == _p4MotifGen) P4SyncMotifVolume();
        }
    }
}
