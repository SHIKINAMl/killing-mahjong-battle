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
