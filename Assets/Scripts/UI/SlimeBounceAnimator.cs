using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 立ち絵をスライムのように「ポヨン、ポヨン」と跳ねさせる（2026-10-21、ユーザー指示）。
    /// チュートリアルでプレイヤーが牌を13枚選んでいるあいだ、手持ち無沙汰な先輩を動かす。
    ///
    /// 1回の跳ねは「沈む（横に潰れる）→ 縦に伸びて跳ぶ → 空中で元の形 → 着地で潰れる → ぷるんと戻る」。
    /// 潰れても足もとが動かないよう、絵の下の端（<see cref="feetFromTop"/>）を軸に縮める。
    ///
    /// **位置は足し算で動かす。** 同じ物に喋るときの跳ね（<see cref="TalkBobAnimator"/>）が付いていて、
    /// あちらは毎フレーム位置を決め直す。こちらは LateUpdate で、前に足した分を引いてから足し直す。
    /// 止めると（<see cref="Play"/> に false）、跳ねの途中でもその瞬間に元の形・位置へ戻す
    /// （2026-10-21、ユーザー指示「揃った瞬間に戻ってほしい」）。
    /// </summary>
    public sealed class SlimeBounceAnimator : MonoBehaviour
    {
        [Tooltip("1回の跳ねにかける秒数")]
        [SerializeField] private float cycleSeconds = 0.9f;

        [Tooltip("跳ぶ高さ。絵の背丈に対する割合")]
        [SerializeField] private float jumpRatio = 0.06f;

        [Tooltip("潰れる強さ（0.18 なら縦に 18% 縮んで横に広がる）")]
        [SerializeField] private float squash = 0.18f;

        [Tooltip("絵の上の端から足もとまでの割合。ドット絵の先輩は体の下の端が 0.65 あたり")]
        [SerializeField] private float feetFromTop = 0.65f;

        /// <summary>先輩の待機中の跳ねだけは、ほかの演出より少ないコマ数で出す。</summary>
        public const float BounceFps = 24f;

        private SpriteRenderer sprite;
        private Vector3 baseScale;
        private Vector3 lastOffset;
        private Vector3 lastWritten;
        private bool hasWritten;
        private bool playing;
        private float phase;

        public bool IsPlaying => playing;

        /// <summary>立ち絵に付けて（二重には付けない）、跳ねさせる・止める。</summary>
        public static SlimeBounceAnimator Set(SpriteRenderer target, bool on)
        {
            if (target == null) return null;
            var anim = target.GetComponent<SlimeBounceAnimator>();
            if (anim == null)
            {
                if (!on) return null;
                anim = target.gameObject.AddComponent<SlimeBounceAnimator>();
            }
            anim.Play(on);
            return anim;
        }

        public void Play(bool on)
        {
            if (on)
            {
                if (!playing) { baseScale = transform.localScale; phase = 0f; presentationFrame = -1; }
                playing = true;
            }
            else
            {
                ResetNow();
            }
        }

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            baseScale = transform.localScale;
        }

        private int presentationFrame = -1;

        private void LateUpdate()
        {
            if (!playing) return;

            phase += Time.deltaTime / Mathf.Max(0.1f, cycleSeconds);
            if (!PresentationTiming.IsNewFrame(ref presentationFrame, Time.time, BounceFps)) return;
            Shape(Frac(phase), out float sy, out float lift);
            float sx = 1f / Mathf.Max(0.5f, sy); // 体積を保つように横へ広がる

            float height = BodyHeight();
            // 絵の中心から足もとまで（ローカル単位、下向きが負）
            float feet = (0.5f - feetFromTop) * height;
            var offset = new Vector3(0f, feet * (1f - sy) * baseScale.y + lift * height * baseScale.y, 0f);

            // 前のフレームに足した分を引いてから足し直す。ほかが位置を決め直していたら、その位置に足す
            Vector3 basePos = hasWritten && transform.localPosition == lastWritten
                ? transform.localPosition - lastOffset
                : transform.localPosition;
            transform.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);
            transform.localPosition = basePos + offset;
            lastOffset = offset;
            lastWritten = transform.localPosition;
            hasWritten = true;
        }

        private void OnDisable() { ResetNow(); }

        /// <summary>跳ねを止めて、その場で元の形・位置へ戻す。</summary>
        private void ResetNow()
        {
            if (!playing) return;
            transform.localScale = baseScale;
            if (hasWritten && transform.localPosition == lastWritten) transform.localPosition -= lastOffset;
            lastOffset = Vector3.zero;
            hasWritten = false;
            playing = false;
        }

        private static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>1回の跳ねの中の位置 t（0〜1）での、縦の伸び縮みと浮いている高さ（背丈に対する割合）。</summary>
        private void Shape(float t, out float sy, out float lift)
        {
            lift = 0f;
            if (t < 0.18f)
            {
                // 跳ぶ前に沈む
                sy = 1f - squash * Smooth(t / 0.18f);
            }
            else if (t < 0.3f)
            {
                // 縦に伸びて飛び出す
                float k = (t - 0.18f) / 0.12f;
                sy = Mathf.Lerp(1f - squash, 1f + squash * 0.7f, Smooth(k));
                lift = jumpRatio * 0.35f * k;
            }
            else if (t < 0.6f)
            {
                // 空中。放物線で上がって落ちる。形は元へ近づく
                float k = (t - 0.3f) / 0.3f;
                // 飛び出した高さ（0.35）から、山なりに上がって 0 へ落ちる
                lift = jumpRatio * (Mathf.Lerp(0.35f, 0f, k) + 0.9f * Mathf.Sin(Mathf.PI * k));
                sy = Mathf.Lerp(1f + squash * 0.7f, 1f, Smooth(Mathf.Min(1f, k * 2f)));
            }
            else if (t < 0.7f)
            {
                // 着地でべちゃっと潰れる
                sy = 1f - squash * Smooth((t - 0.6f) / 0.1f);
            }
            else
            {
                // ぷるんと戻る（だんだん小さくなる揺れ）
                float k = (t - 0.7f) / 0.3f;
                sy = 1f - squash * Mathf.Cos(k * Mathf.PI * 1.5f) * (1f - k);
            }
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>絵の背丈（ローカル単位、伸び縮みする前）。</summary>
        private float BodyHeight()
        {
            if (sprite != null && sprite.sprite != null) return sprite.sprite.bounds.size.y;
            return 1f;
        }
    }
}
