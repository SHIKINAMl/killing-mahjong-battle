using System;
using System.Collections;
using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class PhaseTransitionUI
    {
        private bool promptStyleApplied;

        private IEnumerator PlayPromptTextRoutine(string text, float duration)
        {
            if (promptText == null)
            {
                Debug.LogWarning("[PhaseTransitionUI] promptText is not assigned in the inspector!");
                yield break;
            }

            while (IsPhaseBackdropVisible)
                yield return null;

            if (!promptStyleApplied)
            {
                var material = promptText.fontMaterial;
                if (material != null)
                {
                    material.EnableKeyword("OUTLINE_ON");
                    material.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, .18f);
                    material.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, new Color32(18, 7, 10, 255));
                    material.EnableKeyword("UNDERLAY_ON");
                    material.SetColor("_UnderlayColor", Color.black);
                    material.SetFloat("_UnderlayOffsetX", .5f);
                    material.SetFloat("_UnderlayOffsetY", -.5f);
                    material.SetFloat("_UnderlaySoftness", 0f);
                    promptText.UpdateMeshPadding();
                    promptStyleApplied = true;
                }
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
                float progress = PresentationTiming.Sample(t) / 0.3f;
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
                float progress = PresentationTiming.Sample(t) / 0.3f;
                promptText.color = new Color(1, 1, 1, 1f - progress);
                yield return null;
            }

            promptText.gameObject.SetActive(false);
            _promptRoutine = null;
            ReleasePromptShowing();
        }
    }
}
