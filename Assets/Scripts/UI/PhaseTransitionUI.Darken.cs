using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {

        private IEnumerator RoundStartDarkenRoutine(string text, Action onDarkened)
        {
            ResetVisuals();

            // 市松模様フェードイン (暗転)
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

            // ドン！とテキスト表示
            if (centerText != null)
            {
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

            // 市松模様フェードアウト (晴れる)
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
