using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {
        /// <summary>
        /// 暗転まわりの一発物を鳴らす（2026-09-11）。`Resources/Stingers/` から読む。
        ///
        /// **繋ぎの尺は `checkerFadeDuration` に合わせて作ってある。**
        /// `br_riser` は 1.0 秒かけて上がり切った瞬間にキックが着地する。
        /// 市松模様が晴れる瞬間と打点を合わせるためなので、
        /// `checkerFadeDuration` を変えたら音も作り直すこと。ずれると打点だけ遅れて聞こえる。
        /// </summary>
        private void PlayTransitionStinger(string name)
        {
            var audio = KillingMahjong.Managers.AudioManager.Instance;
            if (audio == null) return;

            // **黒帯と暗転は局ごとに何度も鳴る。** 毎回まったく同じ波形だと、
            // 3局目あたりで「また同じ音」と感じられてしまうので、わずかにばらす。
            // 音程そのものに意味がある音（能力の3段、決めさせられていたの音型）は
            // ばらしてはいけないので、そちらは None にする。
            bool pitched = name != null &&
                           (name.StartsWith("se_ability") || name == "se_collapse" ||
                            name == "se_choice" || name == "se_choice_dark");

            audio.PlayStinger(name,
                pitched ? KillingMahjong.Managers.AudioManager.PitchJitter.None
                        : KillingMahjong.Managers.AudioManager.PitchJitter.VerySmall,
                0.05f);
        }

        private IEnumerator RoundStartDarkenRoutine(string text, Action onDarkened)
        {
            ResetVisuals();

            // 市松模様フェードイン (暗転)。落ちていくスイープを重ねる
            PlayTransitionStinger("br_fall");
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(true);
            if (checkerMaterial != null)
            {
                checkerMaterial.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);
                checkerMaterial.SetFloat("_Progress", 0f);
            }

            float t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", t / checkerFadeDuration);
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f);

            IsDarkenTransitioning = false;
            
            // 暗転完了のコールバック（ここで盤面をクリアする）
            onDarkened?.Invoke();

            // ドン！とテキスト表示。画面揺れと同じ「着弾」なので打撃音を当てる
            if (centerText != null)
            {
                PlayTransitionStinger("br_blackout");
                centerText.text = text;
                centerText.gameObject.SetActive(true);
                centerText.color = Color.white;
                
                t = 0;
                float duration = 0.4f;
                Vector3 initialScale = new Vector3(3f, 3f, 1f);
                Vector3 targetScale = Vector3.one;
                
                while (t < duration)
                {
                    float progress = t / duration;
                    float scaleProgress = 1f - Mathf.Pow(1f - progress, 4f); 
                    centerText.transform.localScale = Vector3.LerpUnclamped(initialScale, targetScale, scaleProgress);
                    t += Time.deltaTime;
                    yield return null;
                }
                centerText.transform.localScale = targetScale;
                
                // 画面揺れ（着弾の衝撃）
                StartCoroutine(ScreenShakeRoutine(0.2f, 20f));
            }
        }

        private IEnumerator RoundStartFadeOutRoutine(Action onComplete)
        {
            // テキストを隠す
            if (centerText != null) centerText.gameObject.SetActive(false);
            if (horizontalLineRt != null) horizontalLineRt.gameObject.SetActive(false);

            // 市松模様フェードアウト (晴れる)。上がり切ったところでキックが着地する
            PlayTransitionStinger("br_riser");
            float t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f - (t / checkerFadeDuration));
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 0f);
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(false);

            onComplete?.Invoke();
        }
    }
}
