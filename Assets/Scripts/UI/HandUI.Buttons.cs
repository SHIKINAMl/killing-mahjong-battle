using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.EngineData;

namespace KillingMahjong.UI
{
    public partial class HandUI
    {
        private void OnAutoDiscardClicked()
        {
            IsAutoDiscardEnabled = !IsAutoDiscardEnabled;
            if (IsAutoDiscardEnabled && gameUIManager != null && gameUIManager.CurrentPhaseStatus == RoundStatus.Discard)
            {
                var autoDiscard = gameUIManager.GetComponent<AutoDiscardController>();
                if (autoDiscard == null)
                {
                    autoDiscard = gameUIManager.gameObject.AddComponent<AutoDiscardController>();
                }
                autoDiscard.CheckAndExecuteAutoDiscard();
            }
        }

        private void UpdateAutoDiscardButtonText()
        {
            if (autoDiscardButton != null)
            {
                string t = IsAutoDiscardEnabled ? "自動: ON" : "自動: OFF";
                var tmp = autoDiscardButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.text = t;
                var txt = autoDiscardButton.GetComponentInChildren<UnityEngine.UI.Text>();
                if (txt != null) txt.text = t;

                var img = autoDiscardButton.GetComponent<Image>();
                if (img != null) img.color = IsAutoDiscardEnabled ? Color.green : Color.red;
            }
        }
        private void NormalizeActionButtons()
        {
            var decideRect = decideButton != null ? decideButton.GetComponent<RectTransform>() : null;
            var autoRect = autoManganButton != null ? autoManganButton.GetComponent<RectTransform>() : null;
            if (decideRect == null || autoRect == null) return;

            var parent = decideRect.parent as RectTransform;
            var canvas = GetComponentInParent<Canvas>();
            if (parent == null || canvas == null) return;

            var canvasRect = canvas.rootCanvas.GetComponent<RectTransform>();
            if (canvasRect == null) return;

            // 親が引き伸ばしアンカーだと anchoredPosition が中心からのずれを表さない。
            // その場合はずれを 0 とみなす（少なくとも大きさと高さは揃う）。
            bool parentIsPointAnchored = Mathf.Approximately(parent.anchorMin.x, parent.anchorMax.x);
            float parentOffsetX = parentIsPointAnchored ? parent.anchoredPosition.x : 0f;

            float halfCanvasWidth = canvasRect.rect.width * 0.5f;
            float centerFromMiddle = halfCanvasWidth - ActionButtonEdgeMargin - ActionButtonWidth * 0.5f;

            ApplyActionButtonStyle(decideRect, centerFromMiddle - parentOffsetX);
            ApplyActionButtonStyle(autoRect, -centerFromMiddle - parentOffsetX);
        }

        private static void ApplyActionButtonStyle(RectTransform rect, float anchoredX)
        {
            rect.sizeDelta = new Vector2(ActionButtonWidth, ActionButtonHeight);
            rect.anchoredPosition = new Vector2(anchoredX, rect.anchoredPosition.y);

            foreach (var tmp in rect.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
            {
                var textRect = tmp.rectTransform;

                // **TMP の margin を必ず 0 に戻す。** シーンの「おまかせ」には
                // `margin = (0, 1.24, 0, -30.30)` が入っていた。下マージンが負の値だと
                // 中央揃えでも文字が約15px下へ押し出され、枠から落ちて見える。
                // これが「おまかせだけ変」の正体で、矩形や位置をいくら揃えても直らない。
                tmp.margin = Vector4.zero;

                // 文字はボタンいっぱいに広げたうえで内側に余白を取る。
                // 「おまかせ」は等倍だと必要幅がボタン幅と同じで、縁に文字が触れていた。
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(ActionButtonTextPadding, 0f);
                textRect.offsetMax = new Vector2(-ActionButtonTextPadding, 0f);

                // 「手牌を見る」「選び直す」など長いラベルの複製もここから作られるので、
                // 収まらないときは縮むようにしておく。折り返すと2行になって崩れる
                tmp.enableAutoSizing = true;
                tmp.fontSizeMax = tmp.fontSize;
                tmp.fontSizeMin = 10f;
                tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
            }

            foreach (var txt in rect.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                txt.resizeTextForBestFit = true;
                txt.alignment = TextAnchor.MiddleCenter;
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var tmp = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmp != null) tmp.text = label;
            var txt = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (txt != null) txt.text = label;
        }

        private void OnReselectClicked()
        {
            if (gameUIManager != null && gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                gameUIManager.CancelHandSelection();
            }
        }
        private void UpdateCursorPosition()
        {
            if (handSlots.Count > 0 && currentSelectionIndex < handSlots.Count)
            {
                // Uses World Position now
                if (handSlots[currentSelectionIndex] != null)
                    cursor.position = handSlots[currentSelectionIndex].position;
            }
        }

        private void OnDecideClicked()
        {
            if (gameUIManager == null) return;
            if (isSubmitted || gameUIManager.IsTransitioning) return;

            if (gameUIManager.CurrentPhaseStatus == RoundStatus.Discard)
            {
                gameUIManager.DiscardSelectedTile();
            }
            else if (gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                Debug.Log($"Decide Clicked. Current Hand Count: {handSlots.Count}");
                if (handSlots.Count == 13)
                {
                    gameUIManager.CompleteHandSelection();
                }
                else
                {
                    Debug.LogWarning("Hand must have exactly 13 tiles to proceed!");
                }
            }
        }

        private void OnAutoManganClicked()
        {
            Debug.Log("Auto Mangan Hand Clicked");
            if (gameUIManager == null) return;
            if (isSubmitted || gameUIManager.IsTransitioning) return;

            if (gameUIManager.CurrentPhaseStatus == RoundStatus.HandSelection)
            {
                if (gameUIManager.IsTutorialMode && gameUIManager.TutorialManager != null)
                {
                    gameUIManager.TutorialManager.ApplyMockAutoMangan();
                }
                else
                {
                    gameUIManager.SelectManganHand();
                }
            }
        }
    }
}
