using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>演出の描画を60fps基準で更新する。実時間・音・入力・完了判定は刻まない。</summary>
    public static class PresentationTiming
    {
        public const float MotionFps = 60f;
        public const float TextFps = 60f;

        public static float Sample(float seconds, float fps = MotionFps)
        {
            return Mathf.Floor(seconds * fps + .00001f) / fps;
        }

        public static bool IsNewFrame(ref int previousFrame, float seconds, float fps = MotionFps)
        {
            int frame = Mathf.FloorToInt(seconds * fps);
            if (frame == previousFrame) return false;
            previousFrame = frame;
            return true;
        }

        /// <summary>描画の終点は正確に返す。短い演出も終了後に途中の姿勢を残さない。</summary>
        public static float Progress(float elapsed, float duration, float fps = MotionFps)
        {
            if (duration <= 0f || elapsed >= duration) return 1f;
            return Mathf.Clamp01(Sample(elapsed, fps) / duration);
        }
    }
}
