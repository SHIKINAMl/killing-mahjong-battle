using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class DialogueUI
    {
        private bool controlLayoutCaptured;
        private Vector2 originalTextPosition, originalLogPosition;
        private float originalTextHeight;

        // 全文を解析してから、3行以上の場合だけ操作欄を下へ逃がす。
        // 短いセリフへ戻ったら、シーンに保存された配置へ戻す。
        private void UpdateDialogueControlLayout()
        {
            if (dialogueText == null || toggleLogButton == null) return;
            var textRect = dialogueText.rectTransform;
            var buttonRect = (RectTransform)toggleLogButton.transform;
            if (!controlLayoutCaptured)
            {
                controlLayoutCaptured = true;
                originalTextPosition = textRect.anchoredPosition;
                originalTextHeight = textRect.rect.height;
                originalLogPosition = buttonRect.anchoredPosition;
            }

            float height = dialogueText.textInfo.lineCount >= 3
                ? Mathf.Max(originalTextHeight, dialogueText.preferredHeight) : originalTextHeight;
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            textRect.anchoredPosition = originalTextPosition
                - Vector2.up * ((height - originalTextHeight) * (1f - textRect.pivot.y));
            buttonRect.anchoredPosition = originalLogPosition;
            if (dialogueText.textInfo.lineCount >= 3)
            {
                var textBottom = buttonRect.parent.InverseTransformPoint(
                    textRect.TransformPoint(new Vector3(0f, textRect.rect.yMin, 0f)));
                var buttonTop = buttonRect.parent.InverseTransformPoint(
                    buttonRect.TransformPoint(new Vector3(0f, buttonRect.rect.yMax, 0f)));
                float shift = Mathf.Min(0f, textBottom.y - 8f - buttonTop.y);
                buttonRect.anchoredPosition += Vector2.up * Mathf.Floor(shift);
            }
            dialogueText.ForceMeshUpdate();
            UpdateAdvanceMarkerPosition();
        }

        private void UpdateAdvanceMarkerPosition()
        {
            if (advanceMarkerObj == null || !controlLayoutCaptured || toggleLogButton == null) return;
            float shift = ((RectTransform)toggleLogButton.transform).anchoredPosition.y - originalLogPosition.y;
            ((RectTransform)advanceMarkerObj.transform).anchoredPosition = new Vector2(-14f, 10f + shift);
        }
    }
}
