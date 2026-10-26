using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class DialogueUI
    {
        private bool controlLayoutCaptured;
        private Vector2 originalTextPosition, originalLogPosition;
        private float originalTextHeight;

        // 操作欄は3行分の余白を確保した位置に固定する。
        // さらに長いセリフは文字欄を上へ広げ、ボタンは動かさない。
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
                + Vector2.up * ((height - originalTextHeight) * textRect.pivot.y);
            buttonRect.anchoredPosition = originalLogPosition - Vector2.up * 12f;
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
