using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 対局の女の子を、曲の拍に合わせて動かす（2026-10-21、ユーザー指示。お試し）。
    ///
    /// - <see cref="Mode.Flip"/>：拍ごとに左右を反転する。
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
            if (beat == lastBeat) return;
            lastBeat = beat;
            bool odd = (beat & 1) == 1;

            if (mode == Mode.Flip)
            {
                body.flipX = odd;
                if (face != null) face.flipX = odd;
            }
            else if (mode == Mode.Swap)
            {
                if (odd) ShowSwap();
                else Unswap();
            }
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
            body.flipX = false;
            if (face != null) face.flipX = false;
            Unswap();
        }

        private void OnDisable() { Restore(); mode = Mode.None; }
    }
}
