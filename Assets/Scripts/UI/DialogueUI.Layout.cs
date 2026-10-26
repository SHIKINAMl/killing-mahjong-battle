using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class DialogueUI
    {
        private bool controlLayoutCaptured;
        private Vector2 originalTextPosition, originalLogPosition;
        private float originalTextHeight;
        private bool readabilityBackdropDirty;

        private void LateUpdate()
        {
            if (!readabilityBackdropDirty) return;
            readabilityBackdropDirty = false;
            UpdateDialogueControlLayout();
        }

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
            var settings = KillingMahjong.Core.SettingsManager.Instance;
            bool framed = settings != null && settings.ShowDialogueBubble;
            // 枠を出さないときも、文字が相手の顔へかからない位置で読む。
            textRect.anchoredPosition = originalTextPosition + Vector2.right * (framed ? 0f : 48f)
                + Vector2.up * ((height - originalTextHeight) * textRect.pivot.y);
            buttonRect.anchoredPosition = originalLogPosition - Vector2.up * 12f;
            dialogueText.ForceMeshUpdate();
            UpdateDialogueTextBackdrop();
            UpdateAdvanceMarkerPosition();
        }

        private void UpdateDialogueTextBackdrop()
        {
            if (!Application.isPlaying || textReadabilityBackdrop == null || dialogueText == null) return;
            var settings = KillingMahjong.Core.SettingsManager.Instance;
            bool showBubble = settings != null && settings.ShowDialogueBubble;
            textReadabilityBackdrop.gameObject.SetActive(!showBubble && !string.IsNullOrEmpty(dialogueText.text));
            if (showBubble) return;

            var textRect = dialogueText.rectTransform;
            var backdropRect = textReadabilityBackdrop.rectTransform;
            backdropRect.anchorMin = textRect.anchorMin;
            backdropRect.anchorMax = textRect.anchorMax;
            backdropRect.pivot = textRect.pivot;
            backdropRect.anchoredPosition = textRect.anchoredPosition;
            backdropRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textRect.rect.width + 12f);
            backdropRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Min(textRect.rect.height, dialogueText.preferredHeight) + 8f);
        }

        private void UpdateAdvanceMarkerPosition()
        {
            if (advanceMarkerObj == null || !controlLayoutCaptured || toggleLogButton == null) return;
            float shift = ((RectTransform)toggleLogButton.transform).anchoredPosition.y - originalLogPosition.y;
            var settings = KillingMahjong.Core.SettingsManager.Instance;
            float textShift = settings != null && settings.ShowDialogueBubble ? 0f : 48f;
            ((RectTransform)advanceMarkerObj.transform).anchoredPosition = new Vector2(-14f + textShift, 10f + shift);
        }
    }
}
