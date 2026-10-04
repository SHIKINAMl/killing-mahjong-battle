using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// ロンを押したあとの「どん！どん！どん！」（2026-10-04、`ロン演出.pdf` より）。
    ///
    ///   ロン！（どん！どん！どん！と順番にでる）
    ///   このとき敵の体力や自分の体力の UI を大きく揺らしたい
    ///
    /// 3発を**間を置いて**叩き、そのたびに画面を揺らし、体力のUIを大きく振る。
    /// 3発目が終わったら、これまでどおり <see cref="RonAnimationUI"/> の
    /// カットイン（「ロン！」）へ渡す。
    ///
    /// **揺らすのは体力のUIだけ。** 盤面ごと揺らすのは <see cref="ScreenQuake"/> が
    /// やっているので、ここで一緒に振ると二重に効いて何が揺れているか分からなくなる。
    /// </summary>
    public static class RonImpactEffect
    {
        /// <summary>何発叩くか。</summary>
        private const int BeatCount = 3;

        /// <summary>叩く間隔（秒）。**詰めすぎない。** 1発に見えてしまう。</summary>
        private const float BeatInterval = 0.30f;

        /// <summary>最後の1発のあと、カットインへ渡すまでの間（秒）。</summary>
        private const float TailWait = 0.24f;

        /// <summary>体力UIが振れる幅（px）。発を追うごとに大きくする。</summary>
        private const float ShakeFirst = 10f;
        private const float ShakeLast = 26f;

        /// <summary>1発ぶんの揺れの長さ（秒）。次の発までに収まる長さにする。</summary>
        private const float ShakeDuration = 0.26f;

        /// <summary>画面の揺れの強さ。こちらも発を追うごとに上げる。</summary>
        private const float QuakeFirst = 9f;
        private const float QuakeLast = 22f;

        /// <summary>
        /// 3発叩く。終わるまで待てるようにコルーチンで返す。
        /// </summary>
        /// <param name="runner">コルーチンを回す相手（呼び出し元の MonoBehaviour）</param>
        /// <param name="hpPanels">大きく揺らす体力のUI。null や空でもよい</param>
        public static IEnumerator Play(MonoBehaviour runner, IList<RectTransform> hpPanels)
        {
            if (runner == null) yield break;

            for (int i = 0; i < BeatCount; i++)
            {
                float t = BeatCount <= 1 ? 1f : i / (float)(BeatCount - 1);

                ScreenQuake.Play(Mathf.Lerp(QuakeFirst, QuakeLast, t), ShakeDuration);

                // 光は薄く短く。ここで白く飛ばすと、このあとのカットインが霞む
                ScreenFlash.Play(0.10f, 0.35f, playSound: false);

                if (AudioManager.Instance != null)
                {
                    // 叩くごとに体力が減っていく想定の音。発が進むほど低く重くなる
                    AudioManager.Instance.PlayHitSE(Mathf.Lerp(0.8f, 0.2f, t));
                }

                if (hpPanels != null)
                {
                    float amplitude = Mathf.Lerp(ShakeFirst, ShakeLast, t);
                    foreach (var panel in hpPanels)
                    {
                        if (panel != null) runner.StartCoroutine(ShakeRoutine(panel, amplitude));
                    }
                }

                if (i < BeatCount - 1) yield return new WaitForSeconds(BeatInterval);
            }

            yield return new WaitForSeconds(TailWait);
        }

        /// <summary>
        /// 1つのUIを振る。**元の位置は呼ばれた時点で控える。**
        /// 振っている最中に重ねて呼ばれても、最後に終わったものが元へ戻す。
        /// </summary>
        private static IEnumerator ShakeRoutine(RectTransform target, float amplitude)
        {
            Vector2 home = target.anchoredPosition;

            float elapsed = 0f;
            while (elapsed < ShakeDuration)
            {
                elapsed += Time.deltaTime;
                if (target == null) yield break;

                // 減衰させる。最後まで同じ幅で振ると、止まった瞬間が不自然になる
                float decay = 1f - Mathf.Clamp01(elapsed / ShakeDuration);
                float power = amplitude * decay * decay;

                target.anchoredPosition = home + new Vector2(
                    Random.Range(-power, power),
                    Random.Range(-power, power));

                yield return null;
            }

            if (target != null) target.anchoredPosition = home;
        }
    }
}
