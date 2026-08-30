using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {

        private IEnumerator PlayPromptTextRoutine(string text, float duration)
        {
            if (promptText == null)
            {
                Debug.LogWarning("[PhaseTransitionUI] promptText is not assigned in the inspector!");
                yield break;
            }

            promptText.text = text;
            promptText.gameObject.SetActive(true);
            promptText.color = new Color(1, 1, 1, 0); // 初期は透明

            RectTransform tmpRt = promptText.GetComponent<RectTransform>();

            // フェードイン＆少しスケールダウンしてドスッという感じにする
            float t = 0;
            while(t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = t / 0.3f;
                promptText.color = new Color(1, 1, 1, progress);
                tmpRt.localScale = Vector3.Lerp(new Vector3(1.5f, 1.5f, 1f), Vector3.one, progress);
                yield return null;
            }
            promptText.color = Color.white;
            tmpRt.localScale = Vector3.one;

            // 待機
            yield return new WaitForSeconds(duration);

            // フェードアウト
            t = 0;
            while(t < 0.3f)
            {
                t += Time.deltaTime;
                float progress = t / 0.3f;
                promptText.color = new Color(1, 1, 1, 1f - progress);
                yield return null;
            }

            promptText.gameObject.SetActive(false);
        }
    }
}
