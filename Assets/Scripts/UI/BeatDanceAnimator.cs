using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 対局の女の子を、曲の拍に合わせて動かす（2026-10-21、ユーザー指示。お試し）。
    /// **2026-10-22 に没案にした。本編では使っていない**（演出コレクションの没案「character.beat_dance」だけで動く）。
    ///
    /// - <see cref="Mode.Bob"/>：絵はそのまま、2拍ごとに「ぴょこ」と弾む。
    /// - <see cref="Mode.Swap"/>：それに加えて、2拍ごとに顔を「いまの顔 ⇔ 目を閉じた笑顔」で交互に出す。
    ///
    /// 弾むのは、背丈の 1% ほど浮いて（2% 大きく）0.15 秒で戻る動き。傾けない
    /// （ユーザーの参考の録画「これぐらいの揺れ」に合わせた）。
    /// **拍と拍の間も、ゆっくり左右に揺れ続ける**（2拍で片側へ、拍の頭で端に着く）。
    /// 最初は左右反転とピース＋どや顔だったが「少し不自然」と言われた。参考と比べると、
    /// 小さな弾みでは反転の鏡写しが瞬間移動に見える／拍の間に止まっていて機械っぽい／
    /// 2枚の顔の気分が違って表情がころころ変わる、の3つが原因だったので、こうした。
    ///
    /// **曲のドラムが入るまでは動かない**（ユーザー指示「ドラムが入るまで女の子は揺れないでほしい」）。
    /// ドラムの入る拍は <see cref="Managers.AudioManager.MusicGrooveStartBeat"/>。
    /// 拍は <see cref="Managers.AudioManager.TryGetMusicBeat"/> から取る。取れないときは、
    /// 対局の曲（賭けの合図 I）と同じ速さの時計で代わりに刻む（そのときはドラムを待たない）。
    /// 止めると、位置も顔も元に戻す。
    /// </summary>
    public sealed class BeatDanceAnimator : MonoBehaviour
    {
        public enum Mode { None, Bob, Swap }

        private const float FallbackBpm = 135.10f;
        private const string SwapFacePath = "女の子/笑顔";

        private SpriteRenderer body;
        private SpriteRenderer face;
        private Mode mode;
        private int lastBeat = int.MinValue;
        private float fallbackStart;
        private System.Func<bool> canSwap;

        // 切り替えの「ぴょこ」。参考の録画では約1秒ごとに絵が替わり、背丈の約 1% 浮いて 0.1〜0.15 秒で戻っていた
        private const int ChangeEveryBeats = 2;
        private const float PopScale = 0.02f;
        private const float PopLiftRatio = 0.012f;
        private const float PopSeconds = 0.15f;
        // 拍の間の左右の揺れ。背丈に対する片側の幅。動き出しから SwayRampBeats 拍かけて大きくする
        private const float SwayRatio = 0.01f;
        private const double SwayRampBeats = 4.0;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private float popStart = -1f;
        private float popLeft;
        private double swayStartBeat = double.NaN;
        private Vector3 lastLift;
        private Vector3 lastWritten;
        private bool liftWritten;

        private Sprite swapFace;
        private Sprite keptFace;
        private bool swapped;

        public Mode Current => mode;

        /// <summary>
        /// 立ち絵（体）に付けて動きを切り替える。顔は体の上に重ねている別の絵。
        /// <paramref name="canSwapFace"/> が false の間（リアクションの顔を出している間など）は顔を替えない。
        /// </summary>
        public static void Set(SpriteRenderer bodyRenderer, SpriteRenderer faceRenderer, Mode newMode, System.Func<bool> canSwapFace = null)
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
            anim.canSwap = canSwapFace;
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
            swayStartBeat = double.NaN;
            fallbackStart = Time.time;
            if (mode == Mode.Swap && swapFace == null) swapFace = Resources.Load<Sprite>(SwapFacePath);
        }

        private void Update()
        {
            if (mode == Mode.None || body == null) return;

            double beats;
            var audio = Managers.AudioManager.Instance;
            if (audio != null && audio.TryGetMusicBeat(out beats))
            {
                // イントロ（ドラムが入る前）は動かさない
                if (beats < audio.MusicGrooveStartBeat())
                {
                    Hold();
                    return;
                }
            }
            else
            {
                beats = (Time.time - fallbackStart) * FallbackBpm / 60.0;
            }
            if (double.IsNaN(swayStartBeat)) swayStartBeat = beats;

            int beat = (int)System.Math.Floor(beats / ChangeEveryBeats);
            if (beat != lastBeat)
            {
                bool first = lastBeat == int.MinValue;
                lastBeat = beat;
                if (mode == Mode.Swap)
                {
                    if ((beat & 1) == 1) ShowSwap();
                    else Unswap();
                }
                // 拍の途中から動き出したときは弾ませない（拍の頭でないところで跳ねて見える）
                if (!first) popStart = Time.time;
            }
            if (!PresentationTiming.IsNewFrame(ref presentationFrame, Time.time)) return;
            ApplyPop();
            ApplyOffset(beats);
        }

        /// <summary>じっとさせる（絵は元のまま、弾ませない・揺らさない）。</summary>
        private void Hold()
        {
            Unswap();
            if (popStart >= 0f)
            {
                popStart = -1f;
                transform.localScale = baseScale;
            }
            popLeft = 0f;
            if (liftWritten) SetLift(Vector3.zero);
            lastBeat = int.MinValue;
            swayStartBeat = double.NaN;
        }

        private int presentationFrame = -1;

        private void ApplyPop()
        {
            if (popStart < 0f) return;
            float k = Mathf.Clamp01((Time.time - popStart) / PopSeconds);
            popLeft = 1f - k * k * (3f - 2f * k); // 1 → 0 へなめらかに
            transform.localScale = baseScale * (1f + PopScale * popLeft);
            if (k >= 1f) popStart = -1f;
        }

        /// <summary>弾みで浮かせる分と、左右の揺れを位置に足す。</summary>
        private void ApplyOffset(double beats)
        {
            float height = body.sprite != null ? body.sprite.bounds.size.y * baseScale.y : 0f;
            // 拍の頭（2拍ごと）で左右の端に着き、その間はなめらかに行き来する
            double ramp = System.Math.Min(1.0, (beats - swayStartBeat) / SwayRampBeats);
            float sway = (float)(System.Math.Cos(System.Math.PI * beats / ChangeEveryBeats) * ramp);
            SetLift(new Vector3(height * SwayRatio * sway, height * PopLiftRatio * popLeft, 0f));
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
            if (swapped || face == null || swapFace == null) return;
            if (canSwap != null && !canSwap()) return;
            keptFace = face.sprite;
            face.sprite = swapFace;
            swapped = true;
        }

        /// <summary>
        /// 元の顔へ戻す。**ほかの処理（リアクションなど）が間に顔を替えていたら、そちらを残す。**
        /// </summary>
        private void Unswap()
        {
            if (!swapped) return;
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
            popLeft = 0f;
            Unswap();
        }

        private void OnDisable() { Restore(); mode = Mode.None; }
    }
}
