using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro usage based on other files
using UnityEngine.EventSystems;

namespace KillingMahjong.UI
{
    public class AbilityItemUI : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private RectTransform descriptionContainer;
        [SerializeField] private Image background;

        [Header("Settings")]
        [SerializeField] private float scrollSpeed = 30f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color selectedColor = Color.yellow;

        private AbilityUI parentUI;
        private int abilityIndex;
        private float currentScrollX;
        private float descriptionWidth;
        private float containerWidth;

        public void Setup(AbilityUI parent, int index, string name, int cost, string description)
        {
            this.parentUI = parent;
            this.abilityIndex = index;

            if (nameText != null) nameText.text = name;
            if (costText != null) costText.text = $"-{cost}";
            if (descriptionText != null)
            {
                descriptionText.text = description;
                // Force update to get width
                Canvas.ForceUpdateCanvases();
                descriptionWidth = descriptionText.preferredWidth;
                containerWidth = descriptionContainer != null ? descriptionContainer.rect.width : 0;
                currentScrollX = 0;
            }
            
            Deselect();
        }

        private void Update()
        {
            // Auto-scroll description
            if (descriptionText != null && descriptionWidth > containerWidth && containerWidth > 0)
            {
                currentScrollX -= scrollSpeed * Time.deltaTime;
                if (currentScrollX < -descriptionWidth)
                {
                    currentScrollX = containerWidth; // Loop from right
                }
                descriptionText.rectTransform.anchoredPosition = new Vector2(currentScrollX, descriptionText.rectTransform.anchoredPosition.y);
            }
        }

        public void ResetScrollPosition()
        {
            if (descriptionText != null && containerWidth > 0)
            {
                // 1文字目が中央から始まるようにリセット
                currentScrollX = containerWidth / 2f;
                descriptionText.rectTransform.anchoredPosition = new Vector2(currentScrollX, descriptionText.rectTransform.anchoredPosition.y);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            parentUI.OnAbilitySelected(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (parentUI != null && descriptionText != null)
            {
                parentUI.ShowTooltip(descriptionText.text);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (parentUI != null)
            {
                parentUI.HideTooltip();
            }
        }

        public void Select()
        {
            if (background != null) background.color = selectedColor;
        }

        public void Deselect()
        {
            if (background != null) background.color = normalColor;
        }

        public int AbilityIndex => abilityIndex;
    }
}
