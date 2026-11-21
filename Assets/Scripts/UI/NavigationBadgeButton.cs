using KillingMahjong.UI.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KillingMahjong.UI
{
    /// <summary>設定アイコンと同じ青地・金色の印を使う案内ボタン。</summary>
    internal sealed class NavigationBadgeButton : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        internal enum Symbol { Back, Next, Close, Match, Book }

        private static readonly string[][] Marks =
        {
            new[] { "...#...", "..##...", ".##....", "######.", ".##....", "..##...", "...#..." },
            new[] { "...#...", "...##..", "....##.", ".######", "....##.", "...##..", "...#..." },
            new[] { "#.....#", "##...##", ".##.##.", "..###..", ".##.##.", "##...##", "#.....#" },
            new[] { ".#####.", "##...##", "#..#..#", "#.....#", "#..#..#", "##...##", ".#####." },
            new[] { "###.###", "#..#..#", "#..#..#", "##.#.##", "#..#..#", "#..#..#", "###.###" }
        };

        private Button button;
        private TMP_Text label;
        private PixelShapeGraphic badge;
        private UIInteractionFeedback feedback;
        private bool focused;
        private Symbol symbol;
        private int visualState = -1;
        private const float LabelSize = 24f;
        private const float IconWidth = 30f;
        private const float IconGap = 8f;

        internal static Button Create(Transform parent, string name, string text, TMP_FontAsset font,
            Vector2 position, Vector2 size, Symbol symbol)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(rect, false);
            var textLabel = labelObject.GetComponent<TextMeshProUGUI>();
            if (font != null) textLabel.font = font;
            textLabel.text = text;
            var button = go.GetComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            Apply(button, symbol);
            return button;
        }

        internal static void Apply(Button button, Symbol symbol)
        {
            if (button == null) return;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;

            button.transition = Selectable.Transition.None;
            if (button.targetGraphic != null)
            {
                var face = button.targetGraphic;
                face.color = Color.clear;
                var border = face.GetComponent<Outline>();
                if (border != null) border.enabled = false;
            }
            foreach (Transform child in button.transform)
            {
                if (child.name.StartsWith("PixelEdge", System.StringComparison.Ordinal))
                    child.gameObject.SetActive(false);
            }

            var skin = button.gameObject.AddComponent<NavigationBadgeButton>();
            skin.button = button;
            skin.symbol = symbol;
            skin.label = label;
            skin.feedback = UIInteractionFeedback.Attach(button.gameObject);
            foreach (string frameName in new[] { "InteractionFrame", "HoverFrameArt", "SelectedFrameArt" })
            {
                var frame = button.transform.Find(frameName);
                if (frame != null) frame.gameObject.SetActive(false);
            }
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = LabelSize;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
            float width = Mathf.Ceil(label.GetPreferredValues(label.text).x);
            labelRect.sizeDelta = new Vector2(width + 4f, 38f);

            var buttonRect = (RectTransform)button.transform;
            float hitWidth = Mathf.Max(buttonRect.rect.width, width + IconWidth + IconGap + 24f);
            buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, hitWidth);
            buttonRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(buttonRect.rect.height, 56f));

            var iconObject = new GameObject("NavigationBadge", typeof(RectTransform), typeof(PixelShapeGraphic));
            iconObject.transform.SetParent(button.transform, false);
            iconObject.transform.SetAsFirstSibling();
            skin.badge = iconObject.GetComponent<PixelShapeGraphic>();
            skin.badge.raycastTarget = false;
            skin.badge.rectTransform.sizeDelta = buttonRect.rect.size;
            skin.LateUpdate();
        }

        private void DrawBadge(string[] mark, int state)
        {
            var edge = state == 0 ? new Color32(99, 99, 113, 255) : new Color32(224, 216, 190, 255);
            var blue = state == 0 ? new Color32(28, 32, 63, 255)
                : state == 2 ? new Color32(55, 69, 161, 255) : new Color32(42, 52, 126, 255);
            var gold = state == 0 ? new Color32(119, 111, 76, 255) : new Color32(249, 213, 62, 255);
            float halfWidth = Mathf.Floor(badge.rectTransform.rect.width * .5f) - 1f;
            float markLeft = -Mathf.Ceil((label.rectTransform.rect.width - 4f + IconGap) * .5f) - 10f;
            badge.Begin();
            // 扉の肩を段状に切り、横幅に合わせて青い面を広げる。
            badge.Box(-halfWidth, -27, halfWidth, 17, Color.black);
            badge.Box(-halfWidth + 4, 17, halfWidth - 4, 23, Color.black);
            badge.Box(-halfWidth + 10, 23, halfWidth - 10, 27, Color.black);
            badge.Box(-halfWidth + 2, -25, halfWidth - 2, 17, edge);
            badge.Box(-halfWidth + 6, 17, halfWidth - 6, 23, edge);
            badge.Box(-halfWidth + 12, 23, halfWidth - 12, 25, edge);
            badge.Box(-halfWidth + 3, -24, halfWidth - 3, 16, blue);
            badge.Box(-halfWidth + 7, 16, halfWidth - 7, 22, blue);
            badge.Box(-halfWidth + 13, 22, halfWidth - 13, 24, blue);
            for (int y = 0; y < mark.Length; y++)
                for (int x = 0; x < mark[y].Length; x++)
                    if (mark[y][x] == '#')
                        badge.Box(markLeft + x * 3, 4 - y * 3, markLeft + 3 + x * 3, 7 - y * 3, gold);
            badge.End();
        }

        private void LateUpdate()
        {
            if (button == null) return;
            bool available = button.IsInteractable();
            int state = !available ? 0 : feedback.IsHovered || focused ? 2 : 1;
            if (visualState != state)
            {
                visualState = state;
                DrawBadge(Marks[(int)symbol], state);
            }
            float offset = available && feedback.IsPressed ? -1f : 0f;
            badge.rectTransform.anchoredPosition = new Vector2(0f, offset);
            label.rectTransform.anchoredPosition = new Vector2((IconWidth + IconGap) * .5f, -3f + offset);
            label.color = available ? new Color32(247, 239, 222, 255) : new Color32(149, 142, 142, 255);
        }

        public void OnSelect(BaseEventData eventData) { focused = !(eventData is PointerEventData); }
        public void OnDeselect(BaseEventData eventData) { focused = false; }
        private void OnDisable() { focused = false; }
    }
}
