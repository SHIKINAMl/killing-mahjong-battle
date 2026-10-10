using System;
using System.Collections;
using UnityEngine;
using KillingMahjong.Managers;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// スキルの演出のあいだの「音の段取り」（2026-10-09）。
    ///
    /// もとは透視スキルだけが持っていた（<see cref="PerspectiveSkillEffect"/>）。
    /// ユーザーの指示で、**自分が使うすべてのスキルの演出**に同じ段取りを掛けるため、ここへ切り出した。
    ///
    ///   始まる    … BGM が水の中に入ったように沈む。深く沈んだ音「ずーーーん」が流れ続ける
    ///   終わる    … 白く光り、集中が抜ける音「さーーーっ」。沈んだ音は切れる
    ///   そのあと  … 約1秒おいて心音「どくっ どくっ どく」。それに合わせて BGM が元の音へ戻る
    ///
    /// 使い方は2通り:
    ///
    ///     var trance = SkillTranceAudio.Begin();
    ///     …演出…
    ///     yield return trance.Release();     // 心音が終わるまで待つ（透視）
    ///     trance.ReleaseDetached();          // 光ったら先へ進む。心音はこの部品が鳴らしきる（ほかのスキル）
    ///
    /// **音を戻すのはこの部品の役目。** 演出が途中で打ち切られても、シーンが変わっても、
    /// この入れ物が消えるときに必ず BGM の沈みと沈んだ音を抜く。
    /// 演出側は、途中でやめるときに <see cref="Dispose"/> を呼ぶだけでよい。
    ///
    /// 続けてスキルを使うと、前の「心音で戻る途中」と次の「沈む」が重なる。
    /// あとから始めたほうが音を握り、前のものは音に触らずに消える（<see cref="s_owner"/>）。
    /// </summary>
    public class SkillTranceAudio : MonoBehaviour
    {
        /// <summary>フラッシュの長さ。<see cref="ScreenFlash"/> の短い合図より少し長くする。</summary>
        private const float FlashDuration = 0.35f;

        /// <summary>光が乗りきるまでの間。これが過ぎてから画面の飾りを消すと、戻った画面が一瞬見えるのを防げる。</summary>
        private const float FlashSettle = 0.06f;

        /// <summary>
        /// 白フラッシュの「さーーーーっ」から、心音3拍を鳴らし始めるまでの間（秒）。
        /// 受け取った連結プレビュー（`clairvoyance_sequence_preview_v1.wav`）を測ると、
        /// 抜ける音が 0.00 秒、最初の「どくっ」が 0.95 秒だった。
        /// </summary>
        private const float FlashToHeartbeat = 0.95f;

        /// <summary>
        /// 心音と一緒に BGM を戻すのにかける秒数。
        /// 心音の素材は 1.84 秒・3拍なので、だいたい最後の拍で戻りきる長さにする。
        /// </summary>
        private const float BgmReturnDuration = 1.7f;

        /// <summary>心音3拍の間合い（秒）。SE素材が挿さっていないときの代替。</summary>
        private static readonly float[] HeartbeatGaps = { 0.00f, 0.42f, 0.36f };

        /// <summary>いま音を握っている1つ。あとから始めたものが握る。</summary>
        private static SkillTranceAudio s_owner;

        /// <summary>もう「終わる」を始めたか。始めていれば、演出側は触らなくてよい。</summary>
        public bool IsReleased { get; private set; }

        /// <summary>音を沈める。</summary>
        /// <param name="fadeIn">沈んだ音が鳴りきるまでの秒数</param>
        public static SkillTranceAudio Begin(float fadeIn = 0.45f)
        {
            if (!Application.isPlaying) return null;

            var go = new GameObject("SkillTranceAudio");
            var trance = go.AddComponent<SkillTranceAudio>();
            s_owner = trance;

            var audio = AudioManager.Instance;
            if (audio != null)
            {
                audio.SetBgmDeepMuffle(true);

                // BGMのこもりだけだと「沈んだ」が音として立たない。沈んだ音を流し続ける
                audio.StartClairvoyanceLoop(fadeIn);
            }
            return trance;
        }

        /// <summary>
        /// 光らせて、心音とともに音を戻す。**心音が終わるまで待つ。** 終わったら自分を片付ける。
        /// </summary>
        /// <param name="onFlashSettled">光が乗りきった瞬間に呼ぶ。画面の飾りを消すのに使う</param>
        public IEnumerator Release(Action onFlashSettled = null)
        {
            if (IsReleased) yield break;
            IsReleased = true;

            var audio = AudioManager.Instance;
            bool hasSe = audio != null && audio.HasClairvoyanceSe;

            // **光ると同時に、集中が抜ける音を鳴らして沈んだ音を切る。**
            // 沈んだ音を先に切ると、無音の一拍があってから光ることになる。
            // 専用の「さーーーーっ」があるときは、フラッシュ共通のキーンと重ねない
            ScreenFlash.Play(FlashDuration, 0.85f, playSound: !hasSe);
            if (audio != null && s_owner == this)
            {
                audio.StopClairvoyanceLoop();
                if (hasSe) audio.PlayClairvoyanceFlashReturn();
            }

            yield return new WaitForSeconds(FlashSettle);
            if (onFlashSettled != null) onFlashSettled();

            if (hasSe)
            {
                // 抜ける音が鳴っているあいだは待つ
                yield return new WaitForSeconds(FlashToHeartbeat - FlashSettle);
                if (s_owner == this)
                {
                    audio.PlayClairvoyanceHeartbeat();
                    audio.SetBgmDeepMuffle(false, BgmReturnDuration);
                }
                yield return new WaitForSeconds(BgmReturnDuration);
            }
            else
            {
                // SE素材が挿さっていないときの段取り。合成の心音で間に合わせる
                if (audio != null && s_owner == this) audio.SetBgmDeepMuffle(false);

                var strengths = new[]
                {
                    HeartbeatStrength.Medium,
                    HeartbeatStrength.Medium,
                    HeartbeatStrength.Strong,
                };
                for (int i = 0; i < strengths.Length; i++)
                {
                    if (HeartbeatGaps[i] > 0f) yield return new WaitForSeconds(HeartbeatGaps[i]);
                    if (audio != null && s_owner == this) audio.PlayHeartbeat(strengths[i], HeartbeatSpacing.Compact);
                }
                yield return new WaitForSeconds(0.25f);
            }

            Dispose();
        }

        /// <summary>
        /// 光らせて、すぐ返る。**心音と BGM の戻りは、この部品が自分で鳴らしきる。**
        /// 演出のあとにゲームを待たせたくないときに使う。
        /// </summary>
        public void ReleaseDetached(Action onFlashSettled = null)
        {
            if (IsReleased) return;
            StartCoroutine(Release(onFlashSettled));
        }

        /// <summary>途中でやめる。音を元へ戻して消える。</summary>
        public void Dispose()
        {
            if (this == null || gameObject == null) return;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDisable() { RestoreAudio(); }

        private void OnDestroy() { RestoreAudio(); }

        /// <summary>
        /// BGM の沈みと沈んだ音を抜く。**音を握っているときだけ。**
        /// 次のスキルがもう音を握っていたら、そちらの沈みを消してしまうので触らない。
        /// </summary>
        private void RestoreAudio()
        {
            if (s_owner != this) return;
            s_owner = null;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBgmDeepMuffle(false);
                AudioManager.Instance.StopClairvoyanceLoop();
            }
        }
    }
}
