using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>勝敗演出案の共通画面。分岐の判定やエンディング内容は呼び出し側が渡す。</summary>
    public sealed class EndingSequenceUI : MonoBehaviour
    {
        public enum EndingStage { Dialogue, Result, Credits }
        public EndingStage Stage { get; private set; }
        public int PageIndex { get; private set; }

        private const string Credits = "じゃんぱいあ\n\nCREDITS\n\nプランナー：ヤバシコウ\n\nプログラマー：マエダアキラ\n\nサーバーエンジニア：シキナミケイスケ\n\nサーバーアシスタント：コシヨシヒロ\n\nイラスト：あずにゃん";
        private TextMeshProUGUI dialogue, resultTitle, endingName, credits, returnLabel;
        private Button advanceButton, returnButton;
        private Image background;
        private string[] pages;
        private Action returnToTitle;
        private bool returned;

        public void Show(VictoryConfig config, TMP_FontAsset font, Action onReturnToTitle)
        {
            if (background == null) Build(font);
            returnToTitle = onReturnToTitle;
            returned = false;
            pages = config != null && config.dialoguePages != null && config.dialoguePages.Length > 0
                ? config.dialoguePages : new[] { config != null ? config.text ?? "" : "" };
            background.sprite = config != null ? config.image : null;
            background.color = background.sprite != null ? Color.white : Color.black;
            // 新しい本文・結果名が未設定の既存シーンでは、従来の結果文を残す。
            resultTitle.text = config != null
                ? (!string.IsNullOrEmpty(config.resultTitle) ? config.resultTitle : config.text ?? "") : "";
            endingName.text = config != null ? config.endingName ?? "" : "";
            PageIndex = 0;
            Stage = EndingStage.Dialogue;
            gameObject.SetActive(true);
            Render();
        }

        /// <summary>本文は1クリック1ページ。結果名の次のクリックでクレジットへ進む。</summary>
        public void Advance()
        {
            if (Stage == EndingStage.Dialogue)
            {
                if (++PageIndex >= pages.Length) Stage = EndingStage.Result;
            }
            else if (Stage == EndingStage.Result) Stage = EndingStage.Credits;
            Render();
        }

        public void ReturnToTitle()
        {
            if (Stage != EndingStage.Credits || returned) return;
            returned = true;
            returnButton.interactable = false;
            returnToTitle?.Invoke();
        }

        private void Render()
        {
            dialogue.gameObject.SetActive(Stage == EndingStage.Dialogue);
            resultTitle.gameObject.SetActive(Stage == EndingStage.Result);
            endingName.gameObject.SetActive(Stage == EndingStage.Result);
            credits.gameObject.SetActive(Stage == EndingStage.Credits);
            returnButton.gameObject.SetActive(Stage == EndingStage.Credits);
            advanceButton.gameObject.SetActive(Stage != EndingStage.Credits);
            returnButton.interactable = !returned;
            if (Stage == EndingStage.Dialogue) dialogue.text = (pages[PageIndex] ?? "") + " ▼";
        }

        private void Build(TMP_FontAsset font)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(800, 600);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            background = CreateRect("EndingPicture", Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            // 画面を覆う背景で対局のボタンへのクリックを遮断する。
            background.raycastTarget = true;
            background.preserveAspect = false;
            dialogue = CreateText("Dialogue", font, new Vector2(.52f, .20f), new Vector2(.94f, .42f), 22, TextAlignmentOptions.TopLeft);
            resultTitle = CreateText("ResultTitle", font, new Vector2(.51f, .36f), new Vector2(.95f, .55f), 80, TextAlignmentOptions.Center);
            endingName = CreateText("EndingName", font, new Vector2(.51f, .29f), new Vector2(.95f, .39f), 22, TextAlignmentOptions.Center);
            credits = CreateText("Credits", font, new Vector2(.44f, .22f), new Vector2(.98f, .81f), 22, TextAlignmentOptions.Center);
            credits.text = Credits;
            advanceButton = CreateButton("Advance", new Vector2(.50f, .18f), new Vector2(.98f, .82f), Advance);
            returnButton = CreateButton("ReturnToTitle", new Vector2(.58f, .07f), new Vector2(.94f, .16f), ReturnToTitle);
            returnLabel = CreateText("ReturnLabel", font, new Vector2(.58f, .07f), new Vector2(.94f, .16f), 22, TextAlignmentOptions.Center);
            returnLabel.transform.SetParent(returnButton.transform, false);
            var labelRect = returnLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            returnLabel.text = "タイトルへ戻る▼";
        }

        private RectTransform CreateRect(string name, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private TextMeshProUGUI CreateText(string name, TMP_FontAsset font, Vector2 min, Vector2 max, float size, TextAlignmentOptions alignment)
        {
            var text = CreateRect(name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableAutoSizing = false;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var go = CreateRect(name, min, max).gameObject;
            var image = go.AddComponent<Image>();
            image.color = Color.clear;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(action);
            return button;
        }
    }
}
