using KillingMahjong.UI.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    /// <summary>設定アイコンと同じ青地・金色の印を使う案内ボタン。</summary>
    internal sealed class NavigationBadgeButton : MonoBehaviour
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
        private Vector2 badgePosition;

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
            textLabel.fontSize = 20f;
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

            // 当たり判定は元の矩形を使い、表示だけを置き換える。
            if (button.targetGraphic != null) button.targetGraphic.color = Color.clear;
            foreach (Transform child in button.transform)
            {
                if (child.name.StartsWith("PixelEdge", System.StringComparison.Ordinal))
                    child.gameObject.SetActive(false);
            }

            var skin = button.gameObject.AddComponent<NavigationBadgeButton>();
            skin.button = button;
            skin.label = label;
            skin.feedback = UIInteractionFeedback.Attach(button.gameObject);
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 20f;
            var labelRect = label.rectTransform;
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(.5f, .5f);
            float width = Mathf.Ceil(label.GetPreferredValues(label.text).x);
            labelRect.sizeDelta = new Vector2(width + 4f, 32f);

            var iconObject = new GameObject("NavigationBadge", typeof(RectTransform), typeof(PixelShapeGraphic));
            iconObject.transform.SetParent(button.transform, false);
            iconObject.transform.SetAsFirstSibling();
            skin.badge = iconObject.GetComponent<PixelShapeGraphic>();
            skin.badge.raycastTarget = false;
            skin.badge.rectTransform.sizeDelta = new Vector2(26f, 32f);
            skin.badgePosition = new Vector2(-Mathf.Ceil(width * .5f) - 3f, 0f);
            skin.DrawBadge(Marks[(int)symbol]);
            skin.LateUpdate();
        }

        private void DrawBadge(string[] mark)
        {
            var edge = new Color32(224, 216, 190, 255);
            var blue = new Color32(42, 52, 126, 255);
            var gold = new Color32(249, 213, 62, 255);
            badge.Begin();
            badge.Box(-12, -14, 12, 8, edge);
            badge.Box(-10, 8, 10, 12, edge);
            badge.Box(-8, 12, 8, 14, edge);
            badge.Box(-10, -12, 10, 8, blue);
            badge.Box(-8, 8, 8, 12, blue);
            for (int y = 0; y < mark.Length; y++)
                for (int x = 0; x < mark[y].Length; x++)
                    if (mark[y][x] == '#')
                        badge.Box(-7 + x * 2, 5 - y * 2, -5 + x * 2, 7 - y * 2, gold);
            badge.End();
        }

        private void LateUpdate()
        {
            if (button == null) return;
            bool available = button.IsInteractable();
            float offset = available && feedback.IsPressed ? -1f : 0f;
            badge.rectTransform.anchoredPosition = badgePosition + new Vector2(0f, offset);
            label.rectTransform.anchoredPosition = new Vector2(16f, offset);
            badge.canvasRenderer.SetAlpha(available ? 1f : .45f);
            label.color = available ? new Color32(247, 239, 222, 255) : new Color32(149, 142, 142, 255);
        }
    }
}
