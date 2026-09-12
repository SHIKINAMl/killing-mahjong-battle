using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {

        private IEnumerator CenterTextAnimRoutine(string text, float duration, Action onComplete = null)
        {
            if (horizontalLineRt != null)
            {
                PlayTransitionStinger("br_band_open");
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
                centerText.text = text;
                centerText.gameObject.SetActive(true);
            }
            
            yield return new WaitForSeconds(duration);
            
            if (centerText != null) centerText.gameObject.SetActive(false);

            // 閉じる音は打点が終端にあるので、閉じ始めと同時に鳴らして帯が閉じ切る瞬間に合わせる
            PlayTransitionStinger("br_band_close");

            t = 0;
            while (t < lineInDuration)
            {
                if (horizontalLineRt != null)
                {
                    horizontalLineRt.localScale = new Vector3(Mathf.Lerp(10f, 0, t / lineInDuration), 2f, 1f);
                }
                t += Time.deltaTime;
                yield return null;
            }
            if (horizontalLineRt != null) horizontalLineRt.gameObject.SetActive(false);

            onComplete?.Invoke();
        }
    }
}
