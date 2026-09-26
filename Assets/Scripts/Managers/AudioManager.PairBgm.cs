using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 2曲をフェイズで切り替える（2026-09-26）。
    ///
    /// プランナー採用の2曲を、フェイズに応じてクロスフェードで行き来させる。
    ///   通常               … bgm_phase_normal （元 phase_01_foundation）
    ///   盛り上がり・切替後 … bgm_phase_turn   （元 phase_04_turn）
    ///
    /// **2本を最初から同時に鳴らし、音量だけを入れ替える。**
    /// 差し替え方式（PhaseBgm.cs の SwapBgmAtNextBar）は一度 0 まで落として曲を入れ替えるので、
    /// どれだけ短くしても「切れた」と分かる。2本走らせておけば本当に重なるので、
    /// 通る音が入れ替わっただけに聞こえる。
    ///
    /// これが成立するのは**2曲が完全に同尺・同テンポ・同調だから**。
    ///   136.000 BPM / 4拍子 / 80小節 / 141.1765秒 / 48kHz / D minor
    /// 曲を差し替えるときはこの3つを必ずそろえること。ずれると重ねた瞬間に濁る。
    ///
    /// **必ず PlayScheduled で同時刻に始める。** Play() を並べると呼んだ順にずれる
    /// （Layers.cs の StartLayeredBgm と同じ理由）。
    /// </summary>
    public partial class AudioManager
    {
        /// <summary>2曲方式を使うか。切ると従来のフェイズ差し替え（曲ごと入れ替え）に戻る。</summary>
        [Header("Pair BGM")]
        [Tooltip("採用した2曲をフェイズでクロスフェードする。切ると従来のフェイズ差し替えに戻る")]
        public bool UsePairBgm = true;

        private const string PairNormalClip = "bgm_phase_normal";
        private const string PairTurnClip = "bgm_phase_turn";

        /// <summary>
        /// どのフェイズでどちらを鳴らすか。**true が「盛り上がり・切替後」側**。
        ///
        /// 載っていないフェイズ（Draw / Result など）はこの2曲の担当外で、
        /// 従来どおり PhaseBgmNames の曲が鳴る。
        /// 割り当てを変えたいときはこの表だけを直せばよい。
        /// </summary>
        private static readonly Dictionary<RoundStatus, bool> PairIsTurnPhase = new Dictionary<RoundStatus, bool>
        {
            // 通常 … 局が進んでいるあいだ
            { RoundStatus.Dealing,       false },
            { RoundStatus.HandSelection, false },
            { RoundStatus.Betting,       false },
            { RoundStatus.TurnDecision,  false },
            { RoundStatus.Discard,       false },

            // 盛り上がり・切替後 … 和了って精算に入ってから
            { RoundStatus.Liquidation,   true  },
            { RoundStatus.Agari,         true  },
            { RoundStatus.Ron,           true  },
        };

        /// <summary>クロスフェードに使う小節数。小節頭から数えるので、曲の拍に乗る。</summary>
        private const int PairCrossfadeBars = 1;

        /// <summary>先に鳴らし始めるまでの余裕。PlayScheduled が間に合うようにする。</summary>
        private const double PairStartLead = 0.10;

        private AudioSource _pairNormalSource;
        private AudioSource _pairTurnSource;
        private Coroutine _pairFade;
        private bool _pairRunning;

        /// <summary>いま「盛り上がり」側を鳴らしているか。-1 は未確定。</summary>
        private int _pairHot = -1;

        public bool IsPairBgmRunning { get { return _pairRunning; } }

        /// <summary>
        /// 直近のクロスフェードを始めた曲位置（秒）。小節頭に乗っているかを外から確かめるために出している。
        /// 曲の長さで割り切れる位置＝小節頭のはずで、ずれていたら拍の計算が合っていない。
        /// </summary>
        public float LastPairCrossfadeStart { get; private set; }

        /// <summary>このフェイズを2曲方式で扱えるか。</summary>
        private static bool PairHandles(RoundStatus status)
        {
            return PairIsTurnPhase.ContainsKey(status);
        }

        private bool EnsurePairSources()
        {
            if (_pairNormalSource != null && _pairTurnSource != null) return true;

            var normal = Resources.Load<AudioClip>("Bgm/" + PairNormalClip);
            var turn = Resources.Load<AudioClip>("Bgm/" + PairTurnClip);
            if (normal == null || turn == null)
            {
                Debug.LogWarning("[AudioManager] 2曲が見つかりません: Resources/Bgm/"
                                 + PairNormalClip + " / " + PairTurnClip);
                return false;
            }

            // **長さが違うと重ねた瞬間にずれていく。** 気づきにくいので必ず弾く。
            if (normal.samples != turn.samples)
            {
                Debug.LogWarning("[AudioManager] 2曲の長さが違うのでクロスフェードできません: "
                                 + normal.samples + " vs " + turn.samples);
                return false;
            }

            var host = new GameObject("BGM_Pair");
            host.transform.SetParent(transform, false);

            _pairNormalSource = host.AddComponent<AudioSource>();
            _pairNormalSource.clip = normal;
            _pairNormalSource.loop = true;
            _pairNormalSource.playOnAwake = false;
            _pairNormalSource.volume = 0f;

            _pairTurnSource = host.AddComponent<AudioSource>();
            _pairTurnSource.clip = turn;
            _pairTurnSource.loop = true;
            _pairTurnSource.playOnAwake = false;
            _pairTurnSource.volume = 0f;

            return true;
        }

        /// <summary>
        /// 2曲の再生を始める。以後フェイズが変わっても曲は止めず、音量だけを入れ替える。
        /// </summary>
        private void StartPairBgm(bool hot)
        {
            if (!CanPlay) return;
            if (!UsePairBgm || !EnsurePairSources()) return;

            // 2曲方式に移るので、ほかの鳴らし方は畳む
            if (AreLayersRunning) StopLayeredBgm();
            if (bgmSource != null && bgmSource.isPlaying) bgmSource.Stop();
            currentPhaseBgmName = null;

            // **2本を同じ時刻に予約する。** ここがずれると重ならない
            double at = AudioSettings.dspTime + PairStartLead;
            _pairNormalSource.timeSamples = 0;
            _pairTurnSource.timeSamples = 0;
            _pairNormalSource.volume = 0f;
            _pairTurnSource.volume = 0f;
            _pairNormalSource.PlayScheduled(at);
            _pairTurnSource.PlayScheduled(at);

            _pairRunning = true;
            _pairHot = -1;                 // 必ず当て直す
            ApplyPairMix(hot, instant: true);
        }

        public void StopPairBgm()
        {
            if (_pairFade != null) { StopCoroutine(_pairFade); _pairFade = null; }
            if (_pairNormalSource != null) _pairNormalSource.Stop();
            if (_pairTurnSource != null) _pairTurnSource.Stop();
            _pairRunning = false;
            _pairHot = -1;
        }

        /// <summary>
        /// どちらを前に出すか当てる。**小節頭まで待ってから**入れ替える。
        /// 拍の途中で始めると、入れ替わりがリズムから外れて気持ち悪い。
        /// </summary>
        private void ApplyPairMix(bool hot, bool instant)
        {
            if (!_pairRunning) return;

            int want = hot ? 1 : 0;
            if (want == _pairHot) return;
            _pairHot = want;

            if (_pairFade != null) { StopCoroutine(_pairFade); _pairFade = null; }

            float master = bgmVolume * masterVolume;
            if (instant)
            {
                _pairNormalSource.volume = hot ? 0f : master;
                _pairTurnSource.volume = hot ? master : 0f;
                return;
            }
            _pairFade = StartCoroutine(CrossfadePairAtNextBar(hot, master));
        }

        private IEnumerator CrossfadePairAtNextBar(bool hot, float master)
        {
            float bar = PairBarSeconds();
            float clipLen = _pairNormalSource.clip.length;

            // **どこまで進んだかは、経過時間ではなく曲の再生位置から引く。**
            //
            // 秒を足していく作り方だと、フレームが詰まった（重い演出、シーン読み込み、
            // エディタの停止）ぶんだけフェードが後ろへずれて、小節から外れる。
            // 実際、MCP から測っていたときに 0.06 秒と 0.69 秒のずれが混在した。
            // 音は timeScale にもフレーム落ちにも影響されずに進むので、
            // **音の位置を時計にすれば、詰まっても曲に対する位置は狂わない。**
            // 詰まったフレームでは曲線を飛ばすだけで、着地点は変わらない。
            float startPos = NextPairBarPosition();
            float duration = Mathf.Max(bar * PairCrossfadeBars, 0.05f);
            LastPairCrossfadeStart = startPos;   // 音ハメの確認用

            float fromNormal = _pairNormalSource.volume;
            float fromTurn = _pairTurnSource.volume;
            float toNormal = hot ? 0f : master;
            float toTurn = hot ? master : 0f;

            // 小節頭まで待つ（届くまでは負の値）
            while (PairElapsedFrom(startPos, clipLen) < 0f) yield return null;

            // **フェードは直線ではなく等パワーで配る。** 両方が同じ編曲の別ミックスなので、
            // 音量を直線で入れ替えると交差点で合計が下がり、一瞬へこんで聞こえる。
            // sin/cos なら二乗和が常に 1 になり、通る量が変わらない。
            while (true)
            {
                float elapsed = PairElapsedFrom(startPos, clipLen);
                if (elapsed >= duration) break;

                float u = Mathf.Clamp01(elapsed / duration);
                float a = Mathf.Cos(u * Mathf.PI * 0.5f);   // 1 → 0
                float b = Mathf.Sin(u * Mathf.PI * 0.5f);   // 0 → 1
                _pairNormalSource.volume = fromNormal * a + toNormal * b;
                _pairTurnSource.volume = fromTurn * a + toTurn * b;
                yield return null;
            }
            _pairNormalSource.volume = toNormal;
            _pairTurnSource.volume = toTurn;
            _pairFade = null;
        }

        /// <summary>次の小節頭の、曲の中での位置（秒）。</summary>
        private float NextPairBarPosition()
        {
            float bar = PairBarSeconds();
            if (bar <= 0f) return _pairNormalSource.time;

            float pos = _pairNormalSource.time;
            float next = (Mathf.Floor(pos / bar) + 1f) * bar;
            // 小節頭に張り付いているときは、その小節頭をそのまま使う
            if (next - pos < 0.02f) next += bar;
            return next % _pairNormalSource.clip.length;
        }

        /// <summary>
        /// 指定した曲位置から、いまどれだけ進んだか（秒）。まだ届いていなければ負。
        ///
        /// **ループの継ぎ目をまたぐと、ただの引き算では曲の長さぶん跳ねる。**
        /// 曲の半分を超える差は「またいだ」とみなして畳み、
        /// 必ず -長さ/2 〜 +長さ/2 に収める。フェードは1小節（曲の1/80）なので、
        /// これで取り違えることはない。
        /// </summary>
        private float PairElapsedFrom(float startPos, float clipLen)
        {
            float d = _pairNormalSource.time - startPos;
            float half = clipLen * 0.5f;
            if (d > half) d -= clipLen;         // まだ届いていないのに継ぎ目の先を見ている
            else if (d < -half) d += clipLen;   // 継ぎ目をまたいで進んだ
            return d;
        }

        private float PairBarSeconds()
        {
            Tempo t = TempoOf(PairNormalClip);
            if (t.Bpm <= 0f) return 0f;
            return 60f / t.Bpm * t.BeatsPerBar;
        }

        /// <summary>音量設定が変わったとき、鳴っている2曲にも反映する。</summary>
        private void ApplyPairVolumes()
        {
            if (!_pairRunning || _pairFade != null) return;   // フェード中は触らない
            float master = bgmVolume * masterVolume;
            bool hot = _pairHot == 1;
            if (_pairNormalSource != null) _pairNormalSource.volume = hot ? 0f : master;
            if (_pairTurnSource != null) _pairTurnSource.volume = hot ? master : 0f;
        }
    }
}
