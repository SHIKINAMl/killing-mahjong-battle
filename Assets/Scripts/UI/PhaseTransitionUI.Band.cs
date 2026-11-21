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
    /// 文字は移動させず、その場で短くフェードする。帯と文字はどちらも60fps基準で出し入れする。
    /// </summary>
    public partial class PhaseTransitionUI
    {
        /// <summary>帯の伸び切った横の倍率。画面外まで確実に届かせる。</summary>
        private const float BandFullScaleX = 10f;

        /// <summary>帯の高さの倍率（シーンの HorizontalLine は高さ 120）。</summary>
        private const float BandScaleY = 2f;

        /// <summary>
        /// 帯も文字も共通の60fps基準へ合わせる。
        /// </summary>
        internal const float PixelFps = PresentationTiming.MotionFps;

        /// <summary>時間をコマの頭にそろえる。</summary>
        internal static float StepTime(float t) => PresentationTiming.Sample(t);

        /// <summary>文字も帯と同じ更新頻度にする。</summary>
        internal const float TextFps = PresentationTiming.TextFps;
        internal static float StepTextTime(float t) => PresentationTiming.Sample(t, TextFps);

        private const float BandTextFadeIn = .10f;

        /// <summary>文字を消す時間は従来の0.25秒を維持する。</summary>
        private const float BandTextFadeOut = .25f;

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

        /// <summary>帯の文字を、位置を動かさず短くフェードして出す。</summary>
        private IEnumerator RevealBandText(string text)
        {
            if (centerText == null) yield break;
            centerText.text = text;
            centerText.alpha = 0f;
            centerText.gameObject.SetActive(true);
            // 有効にした次のフレームで形を渡し直す（無効のまま渡した字は形がまだ無いことがある。Darken の 2026-10-24 の注記と同じ）
            yield return null;
            if (centerText != null && centerText.gameObject.activeInHierarchy) centerText.ForceMeshUpdate();
            yield return FadeText(centerText, 0f, 1f, BandTextFadeIn);
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
                tmp.alpha = Mathf.Lerp(from, to, StepTextTime(t) / duration);
                yield return null;
            }
            if (tmp != null) tmp.alpha = to;
        }
    }
}
