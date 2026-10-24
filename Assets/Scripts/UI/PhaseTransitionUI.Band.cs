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
    /// ・**ステッガー**（時間差の連鎖）: 文字を1字ずつ少し遅らせて、下から浮かせながら出す。
    ///   目線が左から右へ流れる。短所: 全部出るまで待たされるので、1字 0.04 秒に抑える。
    ///
    /// **局名（「第N局目」）には使わない。** 2026-10-11 のユーザー指示で「中央にぱっと出すだけ」。
    /// </summary>
    public partial class PhaseTransitionUI
    {
        /// <summary>帯の伸び切った横の倍率。画面外まで確実に届かせる。</summary>
        private const float BandFullScaleX = 10f;

        /// <summary>帯の高さの倍率（シーンの HorizontalLine は高さ 120）。</summary>
        private const float BandScaleY = 2f;

        /// <summary>1字ずつずらす時間。</summary>
        private const float BandTextCharDelay = 0.04f;

        /// <summary>1字が出きるまでの時間。</summary>
        private const float BandTextCharDuration = 0.22f;

        /// <summary>1字が浮き上がる距離。ドットが粗く見えないよう小さく。</summary>
        private const float BandTextRise = 8f;

        /// <summary>文字を消すときの時間。</summary>
        private const float BandTextFadeOut = 0.15f;

        private static float EaseOutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }
        private static float EaseInCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }

        /// <summary>帯を伸ばす。速く出て、減速して止まる。</summary>
        private IEnumerator OpenBand()
        {
            if (horizontalLineRt == null) yield break;
            horizontalLineRt.gameObject.SetActive(true);
            for (float t = 0f; t < lineInDuration; t += Time.deltaTime)
            {
                horizontalLineRt.localScale = new Vector3(BandFullScaleX * EaseOutCubic(t / lineInDuration), BandScaleY, 1f);
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
                horizontalLineRt.localScale = new Vector3(BandFullScaleX * (1f - EaseInCubic(t / lineInDuration)), BandScaleY, 1f);
                yield return null;
            }
            horizontalLineRt.localScale = new Vector3(0f, BandScaleY, 1f);
            horizontalLineRt.gameObject.SetActive(false);
        }

        /// <summary>帯の文字を、1字ずつ時間差で浮かせながら出す。</summary>
        private IEnumerator RevealBandText(string text)
        {
            if (centerText == null) yield break;
            centerText.text = text;
            centerText.alpha = 0f;
            centerText.gameObject.SetActive(true);
            // 有効にした次のフレームで形を取る（無効のまま渡した字は形がまだ無いことがある。Darken の 2026-10-24 の注記と同じ）
            yield return null;
            if (centerText == null || !centerText.gameObject.activeInHierarchy) yield break;
            centerText.alpha = 1f;
            yield return StaggerIn(centerText, BandTextCharDelay, BandTextCharDuration, BandTextRise);
        }

        /// <summary>帯の文字を薄くして消す。</summary>
        private IEnumerator HideBandText()
        {
            if (centerText == null || !centerText.gameObject.activeSelf) yield break;
            yield return FadeText(centerText, centerText.alpha, 0f, BandTextFadeOut);
            centerText.gameObject.SetActive(false);
            centerText.alpha = 1f;
        }

        /// <summary>
        /// 文字を1字ずつ時間差で出す（ステッガー）。頂点の位置と透明度だけを動かし、
        /// 文字列・色タグ・フォントには触らない。最後に必ず元の形へ戻す。
        /// </summary>
        internal static IEnumerator StaggerIn(TextMeshProUGUI tmp, float charDelay, float charDuration, float rise)
        {
            if (tmp == null) yield break;
            tmp.ForceMeshUpdate();
            var info = tmp.textInfo;
            int count = info.characterCount;
            if (count == 0) yield break;
            var source = info.CopyMeshInfoVertexData();
            float total = (count - 1) * charDelay + charDuration;

            for (float t = 0f; ; t += Time.deltaTime)
            {
                if (tmp == null) yield break;
                bool last = t >= total;
                for (int i = 0; i < count; i++)
                {
                    var ch = info.characterInfo[i];
                    if (!ch.isVisible) continue;
                    float p = last ? 1f : Mathf.Clamp01((t - i * charDelay) / charDuration);
                    float rise01 = 1f - EaseOutCubic(p);
                    float alpha = Mathf.Clamp01(p * 1.6f);
                    int m = ch.materialReferenceIndex;
                    int v = ch.vertexIndex;
                    var dstV = info.meshInfo[m].vertices;
                    var srcV = source[m].vertices;
                    var dstC = info.meshInfo[m].colors32;
                    var srcC = source[m].colors32;
                    var offset = new Vector3(0f, -rise * rise01, 0f);
                    for (int k = 0; k < 4; k++)
                    {
                        dstV[v + k] = srcV[v + k] + offset;
                        var c = srcC[v + k];
                        c.a = (byte)Mathf.RoundToInt(c.a * alpha);
                        dstC[v + k] = c;
                    }
                }
                tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                if (last) yield break;
                yield return null;
            }
        }

        /// <summary>文字全体の透明度を動かす。</summary>
        internal static IEnumerator FadeText(TextMeshProUGUI tmp, float from, float to, float duration)
        {
            if (tmp == null) yield break;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (tmp == null) yield break;
                tmp.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            if (tmp != null) tmp.alpha = to;
        }
    }
}
