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
        [Tooltip("HPが足りずに発動できないときの背景色")]
        [SerializeField] private Color unaffordableColor = new Color(0.45f, 0.45f, 0.45f, 1f);
        [Tooltip("HPが足りないときのコスト文字色")]
        [SerializeField] private Color unaffordableCostColor = new Color(1f, 0.35f, 0.35f, 1f);

        private AbilityUI parentUI;
        private int abilityIndex;
        private float currentScrollX;
        private float descriptionWidth;
        private float containerWidth;

        private Color defaultCostColor = Color.white;
        private bool defaultCostColorCaptured = false;

        /// <summary>現在のHPで発動できるか。false のときはグレーアウトして発動要求も弾く。</summary>
        public bool IsAffordable { get; private set; } = true;

        public void Setup(AbilityUI parent, int index, string name, int cost, string description, bool affordable = true)
        {
            this.parentUI = parent;
            this.abilityIndex = index;
            this.IsAffordable = affordable;

            if (nameText != null) nameText.text = name;

            if (costText != null)
            {
                if (!defaultCostColorCaptured)
                {
                    defaultCostColor = costText.color;
                    defaultCostColorCaptured = true;
                }

                costText.text = $"-{cost}";
                costText.color = affordable ? defaultCostColor : unaffordableCostColor;
            }

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
            // 発動できないスキルは選択しても黄色くしない（押せる見た目にしないため）
            if (background != null) background.color = IsAffordable ? selectedColor : unaffordableColor;
        }

        public void Deselect()
        {
            if (background != null) background.color = IsAffordable ? normalColor : unaffordableColor;
        }

        public int AbilityIndex => abilityIndex;
    }
}
