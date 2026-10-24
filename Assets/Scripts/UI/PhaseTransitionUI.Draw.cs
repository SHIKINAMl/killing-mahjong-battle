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
            if (horizontalLineRt != null) PlayTransitionStinger("br_band_open");
            yield return OpenBand();

            // 流局専用SEは鳴らさず、BGMと黒帯・暗転の短い音だけを残す。
            yield return RevealBandText("流局");
            yield return new WaitForSeconds(textWaitDuration);

            float t;

            // === 2. 市松模様フェードイン (暗転) ===
            Debug.Log("[DrawTransition] Step 2 - Checker Fade In");
            PlayTransitionStinger("br_fall");
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
