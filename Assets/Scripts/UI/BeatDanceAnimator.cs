using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 対局の女の子を、曲の拍に合わせて動かす（2026-10-21、ユーザー指示。お試し）。
    ///
    /// - <see cref="Mode.Flip"/>：2拍ごとに左右を反転する。反転した瞬間に背丈の 1% ほど浮いて（2% 大きく）、
    ///   0.15 秒で戻る。傾けない（2026-10-21、ユーザーの参考の録画「これぐらいの揺れ」に合わせて小さくした）。
    /// - <see cref="Mode.Swap"/>：拍ごとに2枚の絵（いまの絵 ⇔ ピースした絵）を交互に出す。
    ///
    /// 拍は <see cref="Managers.AudioManager.TryGetMusicBeat"/> から取る。取れないときは、
    /// 対局の曲（賭けの合図 I）と同じ速さの時計で代わりに刻む。
    /// 止めると、反転も絵も元に戻す。
    /// </summary>
    public sealed class BeatDanceAnimator : MonoBehaviour
    {
        public enum Mode { None, Flip, Swap }

        private const float FallbackBpm = 135.10f;
        private const string SwapBodyPath = "女の子/ピース身体";
        private const string SwapFacePath = "女の子/どや顔";

        private SpriteRenderer body;
        private SpriteRenderer face;
        private Mode mode;
        private int lastBeat = int.MinValue;
        private float fallbackStart;

        // 反転の「ぴょこ」。参考の録画では約1秒ごとに絵が替わり、背丈の約 1% 浮いて 0.1〜0.15 秒で戻っていた
        private const int FlipEveryBeats = 2;
        private const float PopScale = 0.02f;
        private const float PopLiftRatio = 0.012f;
        private const float PopSeconds = 0.15f;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private float popStart = -1f;
        private Vector3 lastLift;
        private Vector3 lastWritten;
        private bool liftWritten;

        private Sprite swapBody, swapFace;
        private Sprite keptBody, keptFace;
        private bool swapped;

        public Mode Current => mode;

        /// <summary>立ち絵（体）に付けて動きを切り替える。顔は体の上に重ねている別の絵。</summary>
        public static void Set(SpriteRenderer bodyRenderer, SpriteRenderer faceRenderer, Mode newMode)
        {
            if (bodyRenderer == null) return;
            var anim = bodyRenderer.GetComponent<BeatDanceAnimator>();
            if (anim == null)
            {
                if (newMode == Mode.None) return;
                anim = bodyRenderer.gameObject.AddComponent<BeatDanceAnimator>();
            }
            anim.body = bodyRenderer;
            anim.face = faceRenderer;
            anim.SetMode(newMode);
        }

        private void SetMode(Mode newMode)
        {
            if (newMode == mode) return;
            Restore();
            if (mode == Mode.None)
            {
                baseScale = transform.localScale;
                baseRotation = transform.localRotation;
            }
            mode = newMode;
            lastBeat = int.MinValue;
            fallbackStart = Time.time;
            if (mode == Mode.Swap && swapBody == null)
            {
                swapBody = Resources.Load<Sprite>(SwapBodyPath);
                swapFace = Resources.Load<Sprite>(SwapFacePath);
            }
        }

        private void Update()
        {
            if (mode == Mode.None || body == null) return;

            double beats;
            var audio = Managers.AudioManager.Instance;
            if (audio == null || !audio.TryGetMusicBeat(out beats))
                beats = (Time.time - fallbackStart) * FallbackBpm / 60.0;
            int beat = (int)System.Math.Floor(beats);
            if (mode == Mode.Flip) beat = (int)System.Math.Floor(beats / FlipEveryBeats);
            if (beat != lastBeat)
            {
                bool first = lastBeat == int.MinValue;
                lastBeat = beat;
                bool odd = (beat & 1) == 1;
                if (mode == Mode.Flip)
                {
                    body.flipX = odd;
                    if (face != null) face.flipX = odd;
                    if (!first) popStart = Time.time;
                }
                else if (mode == Mode.Swap)
                {
                    if (odd) ShowSwap();
                    else Unswap();
                }
            }
            ApplyPop();
        }

        private void ApplyPop()
        {
            if (popStart < 0f) return;
            float k = Mathf.Clamp01((Time.time - popStart) / PopSeconds);
            float left = 1f - k * k * (3f - 2f * k); // 1 → 0 へなめらかに
            transform.localScale = baseScale * (1f + PopScale * left);
            float height = body != null && body.sprite != null ? body.sprite.bounds.size.y * baseScale.y : 0f;
            SetLift(new Vector3(0f, height * PopLiftRatio * left, 0f));
            if (k >= 1f) popStart = -1f;
        }

        /// <summary>
        /// 浮かせる分を位置に足す。ほかの処理（跳ねなど）が位置を決め直していたら、その位置に足す。
        /// </summary>
        private void SetLift(Vector3 lift)
        {
            Vector3 basePos = liftWritten && transform.localPosition == lastWritten
                ? transform.localPosition - lastLift
                : transform.localPosition;
            transform.localPosition = basePos + lift;
            lastLift = lift;
            lastWritten = transform.localPosition;
            liftWritten = lift != Vector3.zero;
        }

        private void ShowSwap()
        {
            if (swapped || swapBody == null) return;
            keptBody = body.sprite;
            keptFace = face != null ? face.sprite : null;
            body.sprite = swapBody;
            if (face != null && swapFace != null) face.sprite = swapFace;
            swapped = true;
        }

        /// <summary>
        /// 元の絵へ戻す。**ほかの処理（リアクションなど）が間に絵を替えていたら、そちらを残す。**
        /// </summary>
        private void Unswap()
        {
            if (!swapped) return;
            if (body.sprite == swapBody) body.sprite = keptBody;
            if (face != null && face.sprite == swapFace) face.sprite = keptFace;
            swapped = false;
        }

        private void Restore()
        {
            if (body == null) return;
            if (mode != Mode.None)
            {
                transform.localScale = baseScale;
                transform.localRotation = baseRotation;
                if (liftWritten) SetLift(Vector3.zero);
            }
            popStart = -1f;
            body.flipX = false;
            if (face != null) face.flipX = false;
            Unswap();
        }

        private void OnDisable() { Restore(); mode = Mode.None; }
    }
}
