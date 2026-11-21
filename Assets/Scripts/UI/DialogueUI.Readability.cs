using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public partial class DialogueUI
    {
        private string reactionLogContext, lastLogSection;
        private int unreadLogCount;
        private float speechNoticeUntil;
        private Outline speechNotice;
        public int UnreadLogCount => unreadLogCount;
        // 文字送りのコルーチンが**いま回っているか**で見る（2026-11-05 のバグ報告「セリフシステムが機能停止しているきがする」）。
        // 以前は「出した字数 < 全体の字数」で見ていた。送りの途中で吹き出しごと伏せられるとコルーチンが死んで字数が
        // 途中（「大 の2字など）で残り、ずっと true のまま。反応の列（WaitWhileLogIsOpen）がそこで止まり、以後のセリフが全部出なくなった。
        private bool typingRunning;
        public bool IsTyping => typingRunning && isActiveAndEnabled;
        public int VisibleCharacterCount => dialogueText != null ? dialogueText.textInfo.characterCount : 0;
        public void SetReactionLogContext(string context) { reactionLogContext = context; }

        public bool IsPointerOverText()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            var touch = UnityEngine.InputSystem.Touchscreen.current;
            Vector2 point = touch != null && touch.primaryTouch.press.isPressed ? touch.primaryTouch.position.ReadValue()
                : mouse != null ? mouse.position.ReadValue() : new Vector2(-100000, -100000);
            var canvas = dialogueText != null ? dialogueText.GetComponentInParent<Canvas>() : null;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return dialogueText != null && RectTransformUtility.RectangleContainsScreenPoint(dialogueText.rectTransform, point, camera);
        }

        private string LogSection()
        {
            var ui = FindFirstObjectByType<GameUIManager>();
            if (ui == null || ui.IsTutorialMode) return null;
            string phase;
            switch (ui.CurrentPhaseStatus)
            {
                case KillingMahjong.EngineData.RoundStatus.HandSelection: phase = "手牌選択"; break;
                case KillingMahjong.EngineData.RoundStatus.Betting: phase = "掛け金"; break;
                case KillingMahjong.EngineData.RoundStatus.TurnDecision: phase = "先後決定"; break;
                case KillingMahjong.EngineData.RoundStatus.Discard: phase = "対局"; break;
                case KillingMahjong.EngineData.RoundStatus.Dealing: phase = "配牌"; break;
                case KillingMahjong.EngineData.RoundStatus.None: phase = "対局開始前"; break;
                case KillingMahjong.EngineData.RoundStatus.Draw: phase = "流局"; break;
                default: phase = "精算・結果"; break;
            }
            int round = ReactionController.Instance != null ? ReactionController.Instance.CurrentRound : 1;
            return "第" + round + "局 / " + phase;
        }

        private void AddLogSection()
        {
            string section = LogSection();
            if (section == null || section == lastLogSection || logContainer == null) return;
            lastLogSection = section;
            var go = new GameObject("LogSection", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            go.transform.SetParent(logContainer, false);
            var layout = go.GetComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = 30;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = dialogueText.font; label.fontSize = 16;
            label.color = new Color32(211, 190, 165, 255); label.text = section;
            label.margin = new Vector4(8, 6, 8, 4); label.raycastTarget = false;
        }

        private void StyleSpeechLog(GameObject row, TMP_Text text)
        {
            if (text == null) return;
            var content = (RectTransform)logContainer;
            var list = content.GetComponent<VerticalLayoutGroup>();
            if (list != null) { list.spacing = 10; list.padding = new RectOffset(12, 12, 12, 20); }
            float width = content.rect.width > 100 ? content.rect.width - 88 : 234;
            text.fontSize = 20; text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.alignment = TextAlignmentOptions.TopLeft;
            text.lineSpacing = 4; text.raycastTarget = false;
            text.color = new Color32(42, 22, 28, 255);
            // 黒い縁取り（PixelMplus10_Underlay）を外す。濃い字に黒い縁が重なって字が潰れていた
            // （2026-11-05 のバグ報告「ログの文字が潰れている」）。ピンクの吹き出しに濃い字なら縁は要らない。
            if (text.font != null && text.font.material != null) text.fontSharedMaterial = text.font.material;
            var shadow = text.GetComponent<Shadow>(); if (shadow != null) shadow.enabled = false;
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(32, 20); rect.offsetMax = new Vector2(-32, -20);
            float captionHeight = string.IsNullOrEmpty(reactionLogContext) ? 0 : 22;
            if (captionHeight > 0)
            {
                rect.offsetMax = new Vector2(-32, -20 - captionHeight);
                var go = new GameObject("Situation", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(row.transform, false);
                var caption = go.GetComponent<TextMeshProUGUI>(); caption.font = text.font;
                caption.fontSize = 14; caption.color = new Color32(106, 39, 49, 255);
                caption.text = reactionLogContext; caption.raycastTarget = false;
                var cr = caption.rectTransform; cr.anchorMin = new Vector2(0, 1); cr.anchorMax = Vector2.one;
                cr.pivot = new Vector2(.5f, 1); cr.offsetMin = new Vector2(32, -42); cr.offsetMax = new Vector2(-32, -20);
            }
            var layout = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = Mathf.Max(64, text.GetPreferredValues(text.text, width, Mathf.Infinity).y + 40 + captionHeight);
            layout.flexibleHeight = 0;
        }

        private void UpdateReadability()
        {
            if (IsLogOpen)
            {
                var scroll = logContainer != null ? logContainer.GetComponentInParent<ScrollRect>(true) : null;
                if (logScrollRoutine == null && IsNearLatestLog(scroll)) unreadLogCount = 0;
            }
            // Log ボタンの上に未読の数を出すのはやめた（2026-11-05 のバグ報告「セリフ下の数字は何用？」）。
            // 何の数か分からず、セリフの下に浮いて見えた。数はログの中の「最新へ」ボタンにだけ出す（LogLatestButtonUI）。
            if (speechNotice != null) speechNotice.enabled = Time.unscaledTime < speechNoticeUntil;
        }

        private void NoticeSpeech()
        {
            var ui = FindFirstObjectByType<GameUIManager>();
            if (ui == null || ui.IsTutorialMode) return;
            var panelImage = dialoguePanel != null ? dialoguePanel.GetComponent<Image>() : null;
            var target = panelImage != null && panelImage.enabled ? panelImage : textReadabilityBackdrop;
            if (target == null) return;
            if (speechNotice != null && speechNotice.gameObject != target.gameObject) speechNotice.enabled = false;
            if (speechNotice == null || speechNotice.gameObject != target.gameObject)
                speechNotice = target.gameObject.AddComponent<Outline>();
            speechNotice.effectDistance = new Vector2(1, -1);
            speechNotice.effectColor = new Color32(230, 200, 150, 125);
            speechNoticeUntil = Time.unscaledTime + .18f;
            speechNotice.enabled = true;
        }
    }
}
