using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 対局の女の子を、曲の拍に合わせて動かす（2026-10-21、ユーザー指示。お試し）。
    ///
    /// - <see cref="Mode.Flip"/>：拍ごとに左右を反転する。反転した瞬間に少し大きく・少し傾いて、
    ///   0.2 秒で元の大きさ・まっすぐへ戻る（ポンと弾む）。傾きの向きは反転ごとに入れ替わる。
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

        // 反転の「ポン」
        private const float PopScale = 0.10f;
        private const float PopTiltDegrees = 7f;
        private const float PopSeconds = 0.2f;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private float popStart = -1f;
        private float popDirection = 1f;

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
            if (beat != lastBeat)
            {
                bool first = lastBeat == int.MinValue;
                lastBeat = beat;
                bool odd = (beat & 1) == 1;
                if (mode == Mode.Flip)
                {
                    body.flipX = odd;
                    if (face != null) face.flipX = odd;
                    if (!first) { popStart = Time.time; popDirection = odd ? -1f : 1f; }
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
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, PopTiltDegrees * popDirection * left);
            if (k >= 1f) popStart = -1f;
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
            }
            popStart = -1f;
            body.flipX = false;
            if (face != null) face.flipX = false;
            Unswap();
        }

        private void OnDisable() { Restore(); mode = Mode.None; }
    }
}
