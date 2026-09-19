using System;
using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 拍の時計と、拍に合わせて鳴らす仕組み（2026-09-12）。いわゆる「音ハメ」。
    ///
    /// **手法は Inscryption を参考にした。** あちらは Koreographer を噛ませて
    /// `GetMusicBeatTime()` / `SubscribeToMusicEvent()` を持ち、
    /// 演出を曲の拍に乗せている。じゃんぱいあは曲を自前で作っていて
    /// テンポ表（<see cref="Tempos"/>）を既に持っているので、外部ライブラリは要らない。
    ///
    /// **待たせてよいものと、いけないものがある。**
    /// 牌を置いた音のように「プレイヤーの操作に対する反応」を拍まで待たせると、
    /// 操作が重くなったように感じる。**待ってよいのは演出側だけ。**
    /// そのため <see cref="PlayStingerSnapped"/> は
    /// **次の拍が近いときだけ合わせ、遠ければ即座に鳴らす。**
    /// </summary>
    public partial class AudioManager
    {
        /// <summary>拍が変わるたび。引数は曲の先頭から数えた拍番号。</summary>
        public event Action<int> BeatPassed;

        /// <summary>小節が変わるたび。引数は曲の先頭から数えた小節番号。</summary>
        public event Action<int> BarPassed;

        /// <summary>
        /// これより近い拍には合わせる。遠ければ待たずに鳴らす。
        /// 1拍が約0.5秒（118BPM）なので、0.18秒は「ほぼ今」の範囲。
        /// ここを大きくすると、演出が目に見えて遅れ始める。
        /// </summary>
        private const double SnapWindowSeconds = 0.18;

        private int _lastBeatIndex = -1;
        private int _lastBarIndex = -1;

        /// <summary>
        /// いま拍の基準になっている音源。**層で鳴っているときは層の土台を見る。**
        /// 層と1本ものでは鳴っている AudioSource が違うので、ここで吸収する。
        /// </summary>
        private AudioSource BeatClockSource
        {
            get
            {
                if (_layersRunning && _layerSources != null && _layerSources.Length > 0
                    && _layerSources[0] != null && _layerSources[0].isPlaying)
                    return _layerSources[0];

                if (bgmSource != null && bgmSource.isPlaying && bgmSource.clip != null)
                    return bgmSource;

                return null;
            }
        }

        /// <summary>いま鳴っている曲の拍の長さ[秒]と1小節の拍数。取れなければ false。</summary>
        public bool TryGetTempo(out double secondsPerBeat, out int beatsPerBar)
        {
            secondsPerBeat = 0; beatsPerBar = 4;

            var src = BeatClockSource;
            if (src == null || src.clip == null) return false;

            Tempo t = TempoOf(src.clip.name);
            if (t.Bpm <= 0f) return false;

            secondsPerBeat = 60.0 / t.Bpm;
            beatsPerBar = t.BeatsPerBar;
            return true;
        }

        /// <summary>
        /// 曲の先頭から数えて、いま何拍目か（小数を含む）。取れなければ false。
        /// **曲はどれも1拍目がサンプル0から始まるように作ってある**ので、先頭のずれは引かない。
        /// </summary>
        public bool TryGetBeatPosition(out double beats)
        {
            // 揺れ物が何十個も毎フレーム聞きに来るので、1フレームに1回だけ計算する
            // （clip.name は呼ぶたびに文字列を作る）
            if (_beatCacheFrame == Time.frameCount)
            {
                beats = _beatCacheValue;
                return _beatCacheOk;
            }
            _beatCacheFrame = Time.frameCount;
            _beatCacheOk = false;
            _beatCacheValue = 0;
            beats = 0;

            double spb; int bpb;
            if (!TryGetTempo(out spb, out bpb)) return false;

            var src = BeatClockSource;
            if (src == null) return false;

            beats = SmoothedTime(src) / spb;
            _beatCacheValue = beats;
            _beatCacheOk = true;
            return true;
        }

        private int _beatCacheFrame = -1;
        private double _beatCacheValue;
        private bool _beatCacheOk;

        private AudioSource _smoothSource;
        private double _smoothTime;
        private int _smoothFrame = -1;

        /// <summary>
        /// 曲の再生位置を、**フレームごとに滑らかに進めたもの**（2026-09-19）。
        ///
        /// `AudioSource.time` は音声の処理単位（約20ms）ごとにしか進まない。
        /// 60fps で測ると**4フレームに1回は同じ値のまま止まり**、残りは一気に飛ぶ。
        /// これで拍に合わせた揺れ（敵のセリフの吹き出しなど）がカクついて、
        /// 「セリフの動きを変えてから重い」と言われた。
        /// フレームの経過時間で先読みし、本物の値へ少しずつ寄せる。
        /// ループや頭出しで大きく離れたときだけ、本物の値へ飛ばす。
        /// </summary>
        private double SmoothedTime(AudioSource src)
        {
            double raw = src.time;
            if (src != _smoothSource || _smoothFrame < 0 || Time.frameCount - _smoothFrame > 5)
            {
                _smoothTime = raw;
            }
            else
            {
                double predicted = _smoothTime + Time.unscaledDeltaTime * src.pitch;
                double error = raw - predicted;
                _smoothTime = (error > 0.1 || error < -0.1) ? raw : predicted + error * 0.1;
            }
            _smoothSource = src;
            _smoothFrame = Time.frameCount;
            return _smoothTime;
        }

        /// <summary>
        /// 次の拍までの秒数。`subdivision` に 2 を渡すと8分、4を渡すと16分に合わせる。
        /// 曲が鳴っていない、またはテンポが分からないときは 0（＝待たない）。
        /// </summary>
        public double SecondsToNextBeat(int subdivision = 1)
        {
            double spb; int bpb;
            if (!TryGetTempo(out spb, out bpb)) return 0.0;

            var src = BeatClockSource;
            if (src == null) return 0.0;

            double unit = spb / Mathf.Max(1, subdivision);
            double into = src.time % unit;
            return unit - into;
        }

        /// <summary>次の小節頭までの秒数。分からなければ 0。</summary>
        public double SecondsToNextBarBeat()
        {
            double spb; int bpb;
            if (!TryGetTempo(out spb, out bpb)) return 0.0;

            var src = BeatClockSource;
            if (src == null) return 0.0;

            double bar = spb * bpb;
            double into = src.time % bar;
            return bar - into;
        }

        /// <summary>
        /// 拍に合わせてスティンガーを鳴らす。**ただし近いときだけ。**
        ///
        /// 次の拍が <see cref="SnapWindowSeconds"/> より遠ければ、待たずに即座に鳴らす。
        /// 待たせると演出が遅れて見えるので、「合えば気持ちよく、合わなければ普通に」
        /// という落としどころにしてある。
        /// </summary>
        public void PlayStingerSnapped(string name, PitchJitter jitter, int subdivision = 2,
                                       float minInterval = 0f)
        {
            double wait = SecondsToNextBeat(subdivision);
            if (wait <= 0.0 || wait > SnapWindowSeconds)
            {
                PlayStinger(name, jitter, minInterval);
                return;
            }
            StartCoroutine(PlayAfter(name, jitter, minInterval, (float)wait));
        }

        private System.Collections.IEnumerator PlayAfter(string name, PitchJitter jitter,
                                                         float minInterval, float wait)
        {
            yield return new WaitForSecondsRealtime(wait);
            PlayStinger(name, jitter, minInterval);
        }

        /// <summary>
        /// 拍の見張り。**Update から毎フレーム呼ぶ。**
        /// 拍番号が変わった瞬間にイベントを出す。曲が止まったら番号を捨てて、
        /// 次に鳴り始めたとき最初の拍から数え直す。
        /// </summary>
        private void TickBeatClock()
        {
            double beats;
            if (!TryGetBeatPosition(out beats))
            {
                _lastBeatIndex = -1;
                _lastBarIndex = -1;
                return;
            }

            double spb; int bpb;
            TryGetTempo(out spb, out bpb);

            int beat = (int)beats;
            int bar = beat / Mathf.Max(1, bpb);

            // ループで先頭へ戻ったときは番号が減る。そこも「変わった」として出す
            if (beat != _lastBeatIndex)
            {
                _lastBeatIndex = beat;
                if (BeatPassed != null) BeatPassed(beat);
            }
            if (bar != _lastBarIndex)
            {
                _lastBarIndex = bar;
                if (BarPassed != null) BarPassed(bar);
            }
        }

        private void Update()
        {
            // 音を止めているあいだは、配下の AudioSource を毎フレーム黙らせる。
            // 一発物は再生のたびに AudioSource を作るので、ここで掃除しないと取りこぼす。
            EnforceMute();

            TickBeatClock();
        }
    }
}
