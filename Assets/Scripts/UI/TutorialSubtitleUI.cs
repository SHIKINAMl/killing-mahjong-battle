using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KillingMahjong.Common;
using KillingMahjong.Managers.Tutorial;

namespace KillingMahjong.UI
{
    /// <summary>チュートリアルの字幕。通常セリフの本文・ログ・送り待ちには触れない。</summary>
    public sealed class TutorialSubtitleUI : MonoBehaviour
    {
        public const string RootName = "TutorialSubtitleCanvas";

        [SerializeField] private RectTransform panel;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI advanceMarker;
        [SerializeField] private Button advanceCatcher;
        [SerializeField] private float leftPadding = 10f;
        [SerializeField] private float rightPadding = 24f;

        private Action onAdvance;
        private readonly Dictionary<Material, Material> plainMaterials = new Dictionary<Material, Material>();

        public bool IsVisible => panel != null && panel.gameObject.activeSelf;

        /// <summary>指定された短い字幕を一行で表示する。文の分割は台本側で行う。</summary>
        public void Show(string speaker, string text, Action advance = null)
        {
            if (panel == null || subtitleText == null) return;

            onAdvance = advance;
            panel.gameObject.SetActive(true);
            string content = TutorialEmphasis.Apply(text);
            subtitleText.text = speaker + "「" + content.Replace("\r", "").Replace("\n", " ") + "」";
            subtitleText.maxVisibleCharacters = int.MaxValue;
            // 800x600の画面内に右の▼まで収める。行ごとに標準サイズへ戻してから測る。
            subtitleText.fontSize = 20f;
            Canvas.ForceUpdateCanvases();
            var canvasRect = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            float screenWidth = canvasRect.rect.width > 0f ? canvasRect.rect.width : 800f;
            float maxWidth = Mathf.Max(1f, screenWidth - 80f);
            float available = Mathf.Max(1f, maxWidth - leftPadding - rightPadding);
            float textWidth = subtitleText.GetPreferredValues(subtitleText.text, float.PositiveInfinity,
                float.PositiveInfinity).x;
            while (textWidth > available && subtitleText.fontSize > 1f)
            {
                subtitleText.fontSize -= 1f;
                textWidth = subtitleText.GetPreferredValues(subtitleText.text, float.PositiveInfinity,
                    float.PositiveInfinity).x;
            }
            float width = Mathf.Min(maxWidth, textWidth + leftPadding + rightPadding);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            subtitleText.ForceMeshUpdate();
            RemoveTextShadow();

            if (advanceMarker != null) advanceMarker.gameObject.SetActive(advance != null);
            if (advanceCatcher != null)
            {
                advanceCatcher.onClick.RemoveAllListeners();
                advanceCatcher.onClick.AddListener(Advance);
                advanceCatcher.gameObject.SetActive(advance != null);
            }
        }

        public void Hide()
        {
            onAdvance = null;
            if (panel != null) panel.gameObject.SetActive(false);
            if (advanceCatcher != null) advanceCatcher.gameObject.SetActive(false);
        }

        private void Advance()
        {
            var callback = onAdvance;
            onAdvance = null;
            if (advanceCatcher != null) advanceCatcher.gameObject.SetActive(false);
            if (advanceMarker != null) advanceMarker.gameObject.SetActive(false);
            callback?.Invoke();
        }

        /// <summary>自動試写もクリックと同じ送り処理を通し、待機用の入力とマークを片付ける。</summary>
        public void CompleteLine() { Advance(); }

        private Material PlainMaterial(Material source)
        {
            if (source == null || plainMaterials.ContainsValue(source)) return source;
            if (plainMaterials.TryGetValue(source, out var material)) return material;
            material = new Material(source);
            material.DisableKeyword("UNDERLAY_ON");
            material.DisableKeyword("UNDERLAY_INNER");
            if (material.HasProperty("_OutlineWidth")) material.SetFloat("_OutlineWidth", 0f);
            plainMaterials.Add(source, material);
            return material;
        }

        private void RemoveTextShadow()
        {
            // 字幕専用の材質を使い、通常セリフやフォントの共有材質を変更しない。
            subtitleText.fontSharedMaterial = PlainMaterial(subtitleText.fontSharedMaterial);
            foreach (var subMesh in subtitleText.GetComponentsInChildren<TMP_SubMeshUI>(true))
                subMesh.sharedMaterial = PlainMaterial(subMesh.sharedMaterial);
        }

        private void OnDisable() { Hide(); }

        private void OnDestroy()
        {
            foreach (var material in plainMaterials.Values) Destroy(material);
            plainMaterials.Clear();
        }

        /// <summary>OpeningScene に足りない部品だけを置く。既存の見た目はシーンが正。</summary>
        public static TutorialSubtitleUI BakeForEditor()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "OpeningScene")
                throw new InvalidOperationException("Tutorial subtitles belong to OpeningScene.");

            return Create();
        }

        /// <summary>チュートリアルと演出試写だけが明示的に作る独立した字幕。</summary>
        public static TutorialSubtitleUI Create()
        {

            var root = SceneFirst.Root(RootName, out bool created, typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TutorialSubtitleUI));
            var ui = root.GetComponent<TutorialSubtitleUI>();
            if (created)
            {
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = UISortingOrders.TutorialSubtitle;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(800f, 600f);
                scaler.matchWidthOrHeight = 1f;
            }

            var catcher = SceneFirst.Child(root.transform, "SubtitleAdvanceCatcher", out created,
                typeof(RectTransform), typeof(Image), typeof(Button));
            ui.advanceCatcher = catcher.GetComponent<Button>();
            if (created)
            {
                Stretch(catcher.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
                catcher.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
                ui.advanceCatcher.transition = Selectable.Transition.None;
            }

            var panel = SceneFirst.Child(root.transform, "SubtitlePanel", out created,
                typeof(RectTransform), typeof(Image));
            ui.panel = panel.GetComponent<RectTransform>();
            if (created)
            {
                ui.panel.anchorMin = ui.panel.anchorMax = new Vector2(0.5f, 0f);
                ui.panel.pivot = new Vector2(0.5f, 0f);
                ui.panel.anchoredPosition = new Vector2(0f, 35f);
                ui.panel.sizeDelta = new Vector2(360f, 42f);
                panel.GetComponent<Image>().color = new Color32(43, 43, 43, 230);
                panel.GetComponent<Image>().raycastTarget = false;
            }

            var label = SceneFirst.Child(panel.transform, "SubtitleText", out created,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            ui.subtitleText = label.GetComponent<TextMeshProUGUI>();
            if (created)
            {
                Stretch(label.GetComponent<RectTransform>(), new Vector2(ui.leftPadding, 0f),
                    new Vector2(-ui.rightPadding, 0f));
                ui.subtitleText.font = Resources.Load<TMP_FontAsset>("PixelMplus10_DynamicFixed") ?? TMP_Settings.defaultFontAsset;
                ui.subtitleText.fontSize = 20f;
                ui.subtitleText.color = Color.white;
                ui.subtitleText.alignment = TextAlignmentOptions.MidlineLeft;
                ui.subtitleText.textWrappingMode = TextWrappingModes.NoWrap;
                ui.subtitleText.enableAutoSizing = false;
                ui.subtitleText.richText = true;
                ui.subtitleText.raycastTarget = false;
                ui.subtitleText.text = "";
            }

            var marker = SceneFirst.Child(panel.transform, "SubtitleAdvanceMarker", out created,
                typeof(RectTransform), typeof(TextMeshProUGUI));
            ui.advanceMarker = marker.GetComponent<TextMeshProUGUI>();
            if (created)
            {
                var rect = marker.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0f, 0f);
                rect.anchoredPosition = new Vector2(20f, 0f);
                rect.sizeDelta = new Vector2(20f, 24f);
                ui.advanceMarker.font = ui.subtitleText.font;
                ui.advanceMarker.text = "▼";
                ui.advanceMarker.fontSize = 20f;
                ui.advanceMarker.color = new Color32(224, 40, 40, 255);
                ui.advanceMarker.alignment = TextAlignmentOptions.Center;
                ui.advanceMarker.raycastTarget = false;
            }

            ui.Hide();
            SceneFirst.Report(nameof(TutorialSubtitleUI));
            return ui;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}
