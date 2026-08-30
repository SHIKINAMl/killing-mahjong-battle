using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {

        private IEnumerator DrawTransitionRoutine(Action onMidpoint, Action onComplete)
        {
            ResetVisuals();

            // === 1. 一本線イン + 「流局」テキスト ===
            Debug.Log("[DrawTransition] Step 1 - Line In");
            if (horizontalLineRt != null)
            {
                horizontalLineRt.gameObject.SetActive(true);
                horizontalLineRt.localScale = new Vector3(0, 2f, 1f);
            }

            float t = 0;
            while (t < lineInDuration)
            {
                if (horizontalLineRt != null)
                {
                    horizontalLineRt.localScale = new Vector3(Mathf.Lerp(0, 10f, t / lineInDuration), 2f, 1f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (horizontalLineRt != null) horizontalLineRt.localScale = new Vector3(10f, 2f, 1f);

            if (centerText != null)
            {
                centerText.text = "流局";
                centerText.gameObject.SetActive(true);
            }
            yield return new WaitForSeconds(textWaitDuration);

            // === 2. 市松模様フェードイン (暗転) ===
            Debug.Log("[DrawTransition] Step 2 - Checker Fade In");
            if (fullScreenCheckerImage != null) fullScreenCheckerImage.gameObject.SetActive(true);
            if (checkerMaterial != null)
            {
                checkerMaterial.SetFloat("_AspectRatio", (float)Screen.width / Screen.height);
                checkerMaterial.SetFloat("_Progress", 0f);
            }

            t = 0;
            while (t < checkerFadeDuration)
            {
                if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", t / checkerFadeDuration);
                t += Time.deltaTime;
                yield return null;
            }
            if (checkerMaterial != null) checkerMaterial.SetFloat("_Progress", 1f);

            // === 3. 暗転中のコールバック (UIリセットなど) ===
            Debug.Log("[DrawTransition] Midpoint invoked");
            onMidpoint?.Invoke();

            isDarkened = true; // 暗転状態を記録

            // 少し暗転状態で待機
            yield return new WaitForSeconds(1.0f);

            // フェードアウトせずに暗転状態を維持したまま完了とする
            Debug.Log("[DrawTransition] Complete (stay dark)");
            onComplete?.Invoke();
        }
    }
}
