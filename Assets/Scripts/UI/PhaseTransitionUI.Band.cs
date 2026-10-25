using System.Collections;
using TMPro;
using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// 黒い帯の開け閉めと、帯に載せる文字の出し入れ（2026-10-25）。
    ///
    /// それまでは帯が一定の速さで伸び縮みし、文字は SetActive でパッと出てパッと消えていた。
    /// ユーザーの決まり「ものをパッて表示させてはいけない」（2026-10-24）に合わせ、
    /// 次の2つの技法で出し入れする:
    ///
    /// ・**イージング**（ease-out / ease-in）: 帯は速く伸びて減速しながら止まり、閉じるときは
    ///   ゆっくり動き出して速く閉じる。止まった・閉じたが一定速度より分かりやすい。
    ///   短所: 付けすぎると軽く見えるので、行き過ぎ（バック）は帯には使わない。
    ///
    /// **文字は、その場でぱっと出す（2026-10-26 のユーザー指示「移動するのはあれなので、その場でぱっとでる感じにしてほしい」）。**
    /// 以前は1字ずつ時間差で、下から浮かせながら出していた（ステッガー）。消すときのコマ打ちの薄れはそのまま。
    /// </summary>
    public partial class PhaseTransitionUI
    {
        /// <summary>帯の伸び切った横の倍率。画面外まで確実に届かせる。</summary>
        private const float BandFullScaleX = 10f;

        /// <summary>帯の高さの倍率（シーンの HorizontalLine は高さ 120）。</summary>
        private const float BandScaleY = 2f;

        /// <summary>
        /// **動きはコマ打ちにする（2026-10-25 のユーザー指示「動きがヌルっとしすぎてAIっぽい。少しドットっぽく動いて」）。**
        /// 毎フレームなめらかに動かさず、1秒に 12 コマだけ絵を変えて、その間は止める。
        /// </summary>
        internal const float PixelFps = 12f;

        /// <summary>時間をコマの頭にそろえる。</summary>
        internal static float StepTime(float t) => Mathf.Floor(t * PixelFps) / PixelFps;

        /// <summary>文字を消すときの時間。3コマ（濃い → 2/3 → 1/3 → 消える）。</summary>
        private const float BandTextFadeOut = 3f / PixelFps;

        private static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        private static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }

        /// <summary>帯を伸ばす。速く出て、減速して止まる。</summary>
        private IEnumerator OpenBand()
        {
            if (horizontalLineRt == null) yield break;
            horizontalLineRt.gameObject.SetActive(true);
            for (float t = 0f; t < lineInDuration; t += Time.deltaTime)
            {
                horizontalLineRt.localScale = new Vector3(BandFullScaleX * EaseOutCubic(StepTime(t) / lineInDuration), BandScaleY, 1f);
                yield return null;
            }
            horizontalLineRt.localScale = new Vector3(BandFullScaleX, BandScaleY, 1f);
        }

        /// <summary>帯を閉じる。ゆっくり動き出して、速く閉じ切る（閉じる音の打点が終端にある）。</summary>
        private IEnumerator CloseBand()
        {
            if (horizontalLineRt == null) yield break;
            for (float t = 0f; t < lineInDuration; t += Time.deltaTime)
            {
                horizontalLineRt.localScale = new Vector3(BandFullScaleX * (1f - EaseInCubic(StepTime(t) / lineInDuration)), BandScaleY, 1f);
                yield return null;
            }
            horizontalLineRt.localScale = new Vector3(0f, BandScaleY, 1f);
            horizontalLineRt.gameObject.SetActive(false);
        }

        /// <summary>帯の文字を、その場でぱっと出す（動かさない）。</summary>
        private IEnumerator RevealBandText(string text)
        {
            if (centerText == null) yield break;
            centerText.text = text;
            centerText.alpha = 1f;
            centerText.gameObject.SetActive(true);
            // 有効にした次のフレームで形を渡し直す（無効のまま渡した字は形がまだ無いことがある。Darken の 2026-10-24 の注記と同じ）
            yield return null;
            if (centerText != null && centerText.gameObject.activeInHierarchy) centerText.ForceMeshUpdate();
        }

        /// <summary>帯の文字を薄くして消す。</summary>
        private IEnumerator HideBandText()
        {
            if (centerText == null || !centerText.gameObject.activeSelf) yield break;
            yield return FadeText(centerText, centerText.alpha, 0f, BandTextFadeOut);
            centerText.gameObject.SetActive(false);
            centerText.alpha = 1f;
        }

        /// <summary>文字全体の透明度を動かす。</summary>
        internal static IEnumerator FadeText(TextMeshProUGUI tmp, float from, float to, float duration)
        {
            if (tmp == null) yield break;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (tmp == null) yield break;
                tmp.alpha = Mathf.Lerp(from, to, StepTime(t) / duration);
                yield return null;
            }
            if (tmp != null) tmp.alpha = to;
        }
    }
}
