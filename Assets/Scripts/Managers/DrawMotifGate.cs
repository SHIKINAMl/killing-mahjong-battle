using UnityEngine;

namespace KillingMahjong.Managers
{
    /// <summary>
    /// 流局のモチーフ（高い波状の音）を、**音声処理の側で**すばやく絞るための弁（2026-10-08）。
    ///
    /// 第4案の流局は、土台（base）とモチーフ（motif）の2本を重ねて鳴らす。
    /// 次の対局へ進んだら、モチーフだけはその場で止める決まり（指示書）。
    /// いきなり `Stop()` すると波形の途中で切れてプチッと鳴るので、約8ミリ秒で絞ってから止める。
    ///
    /// **メインスレッドのコルーチンでは8ミリ秒を刻めない**（1コマが16ミリ秒前後ある）。
    /// だから音声スレッドで、1サンプルずつ音量を掛ける。
    ///
    /// **絞り始めるのは「次の音声ブロック」から。** 閉じる合図はメインスレッドが出し、
    /// 音声スレッドは次に呼ばれたときにそれを読む。ブロックの長さ（数〜20ミリ秒ほど）ぶんの
    /// 遅れは残る。「合図から8ミリ秒以内に無音」ではなく、「絞り始めたら約8ミリ秒で無音」。
    ///
    /// **WebGL では動かない**（`OnAudioFilterRead` が呼ばれない）。
    /// そちらは AudioManager.Proposal4.cs が、この弁を使わずにその場で止める。
    ///
    /// モチーフの AudioSource と同じ GameObject に、AudioSource より後に付けること。
    /// </summary>
    public class DrawMotifGate : MonoBehaviour
    {
        /// <summary>絞りきるまでの長さ（秒）。</summary>
        public const float RampSeconds = 0.008f;

        // メインスレッドが書き、音声スレッドが読む
        private volatile bool _open = true;
        private volatile bool _snapOpen;

        // 音声スレッドだけが触る
        private float _gain = 1f;

        // 音声スレッドが書き、メインスレッドが読む
        private volatile bool _silent;

        private float _step = 1f / 384f;

        /// <summary>絞りきって無音になったか。</summary>
        public bool IsSilent { get { return _silent; } }

        private void Awake()
        {
            // 出力のサンプルレートはメインスレッドでしか読めないので、ここで刻みを決めておく
            int rate = AudioSettings.outputSampleRate;
            if (rate <= 0) rate = 48000;
            _step = 1f / Mathf.Max(1f, rate * RampSeconds);
        }

        /// <summary>全開にする。鳴らし始める前に呼ぶ（土台と同じ大きさで最初から鳴らすため、絞りは掛けない）。</summary>
        public void OpenNow()
        {
            _open = true;
            _snapOpen = true;
            _silent = false;
        }

        /// <summary>絞り始める。</summary>
        public void Close()
        {
            _open = false;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (_snapOpen)
            {
                _gain = 1f;
                _snapOpen = false;
            }

            float target = _open ? 1f : 0f;
            if (channels < 1) channels = 1;

            for (int i = 0; i < data.Length; i += channels)
            {
                if (_gain < target) _gain = Mathf.Min(target, _gain + _step);
                else if (_gain > target) _gain = Mathf.Max(target, _gain - _step);

                for (int c = 0; c < channels && i + c < data.Length; c++) data[i + c] *= _gain;
            }

            _silent = _gain <= 0f;
        }
    }
}
