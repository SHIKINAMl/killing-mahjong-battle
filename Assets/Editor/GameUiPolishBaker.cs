using KillingMahjong.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.EditorTools
{
    /// <summary>設定の静的な見た目は保存済みの部品へ適用し、実行中には上書きしない。</summary>
    public static class GameUiPolishBaker
    {
        private static readonly Color Ink = new Color32(37, 27, 35, 255);
        private static readonly Color Cream = new Color32(239, 238, 216, 255);
        private static readonly Color Edge = new Color32(155, 131, 130, 255);
        private static readonly Color Quiet = new Color32(63, 57, 81, 255);

        public static void ApplyToPrefab()
        {
            const string path = "Assets/Prefabs/OptionUI.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Apply(root.GetComponentInChildren<OptionUI>(true));
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void BakeTutorialChoiceButton()
        {
            var root = new GameObject("TutorialChoiceButton", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(200f, 56f);
                Paint(root.GetComponent<Image>(), new Color32(83, 24, 34, 255));
                Frame(rect, new Color32(167, 129, 128, 255));
                var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(root.transform, false);
                var textRect = (RectTransform)label.transform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(16f, 6f);
                textRect.offsetMax = new Vector2(-16f, -6f);
                var text = label.GetComponent<TextMeshProUGUI>();
                ConfigureText(text);
                text.fontSize = 22f;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.text = "選択肢";
                UIInteractionFeedback.Attach(root);
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/UI/TutorialChoiceButton.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        public static int ApplyToLoadedScene()
        {
            int count = 0;
            foreach (var option in Object.FindObjectsByType<OptionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!option.gameObject.scene.IsValid()) continue;
                Apply(option);
                foreach (var component in option.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    EditorUtility.SetDirty(component);
                    if (PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(option.gameObject.scene);
                count++;
            }
            return count;
        }

        private static void Apply(OptionUI option)
        {
            var data = new SerializedObject(option);
            var dropdown = (TMP_Dropdown)data.FindProperty("resolutionDropdown").objectReferenceValue;
            if (dropdown != null) StyleDropdown(dropdown);
            foreach (string field in new[] { "returnToTitleButton", "closeButton" })
            {
                var button = (Button)data.FindProperty(field).objectReferenceValue;
                if (button == null || button.image == null) continue;
                button.image.sprite = null;
                button.image.type = Image.Type.Simple;
                button.image.color = Quiet;
                Frame((RectTransform)button.transform, Edge);
            }
        }

        private static void StyleDropdown(TMP_Dropdown dropdown)
        {
            var rect = (RectTransform)dropdown.transform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 300f);
            Paint(dropdown.GetComponent<Image>(), Ink);
            Frame(rect, Edge);
            ConfigureText(dropdown.captionText);
            ConfigureText(dropdown.itemText);

            var arrow = dropdown.transform.Find("Arrow") as RectTransform;
            if (arrow != null)
            {
                var old = arrow.GetComponent<Image>();
                if (old != null) old.enabled = false;
                for (int i = 0; i < 5; i++)
                {
                    var pixel = Part(arrow, "PixelArrow" + i, Cream);
                    pixel.anchorMin = pixel.anchorMax = new Vector2(.5f, .5f);
                    pixel.sizeDelta = new Vector2(2f, 2f);
                    pixel.anchoredPosition = new Vector2((i - 2) * 2f, Mathf.Abs(i - 2) * 2f - 2f);
                }
            }
            if (dropdown.template == null) return;
            Paint(dropdown.template.GetComponent<Image>(), Ink);
            dropdown.template.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 176f);
            var viewport = dropdown.template.Find("Viewport");
            if (viewport != null) Paint(viewport.GetComponent<Image>(), Ink);
            Frame(dropdown.template, Edge);
            if (dropdown.itemText != null)
            {
                var item = dropdown.itemText.GetComponentInParent<Toggle>(true);
                if (item != null)
                {
                    var itemRect = (RectTransform)item.transform;
                    itemRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);
                    if (item.targetGraphic is Image background) Paint(background, Quiet);
                    if (item.graphic is Image check)
                    {
                        Paint(check, Cream);
                        check.rectTransform.sizeDelta = new Vector2(6f, 6f);
                    }
                    var textRect = dropdown.itemText.rectTransform;
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = new Vector2(26f, 0f);
                    textRect.offsetMax = new Vector2(-8f, 0f);
                    var colors = item.colors;
                    colors.highlightedColor = new Color32(255, 205, 205, 255);
                    colors.selectedColor = colors.highlightedColor;
                    item.colors = colors;
                }
            }
            foreach (var bar in dropdown.template.GetComponentsInChildren<Scrollbar>(true))
            {
                Paint(bar.GetComponent<Image>(), Ink);
                if (bar.handleRect != null) Paint(bar.handleRect.GetComponent<Image>(), Edge);
            }
        }

        private static void ConfigureText(TMP_Text text)
        {
            if (text == null) return;
            text.font = GameUiTypography.Body;
            text.fontSize = 20f;
            text.color = Cream;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static void Paint(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
        }

        private static RectTransform Part(RectTransform parent, string name, Color color)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null)
            {
                rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }
            var image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static void Frame(RectTransform parent, Color color)
        {
            for (int i = 0; i < 4; i++)
            {
                var edge = Part(parent, "PixelEdge" + i, color);
                bool horizontal = i < 2;
                edge.anchorMin = horizontal ? new Vector2(0f, i) : new Vector2(i - 2, 0f);
                edge.anchorMax = horizontal ? new Vector2(1f, i) : new Vector2(i - 2, 1f);
                edge.pivot = new Vector2(i == 3 ? 1f : 0f, i == 1 ? 1f : 0f);
                edge.anchoredPosition = Vector2.zero;
                edge.sizeDelta = horizontal ? new Vector2(0f, 2f) : new Vector2(2f, 0f);
            }
        }
    }
}
