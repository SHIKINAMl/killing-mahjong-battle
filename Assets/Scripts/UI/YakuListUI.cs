using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public class YakuListUI : MonoBehaviour
    {
        [Header("Yaku List")]
        [SerializeField] private GameObject yakuListPanel;
        [SerializeField] private Button toggleButton;

        private void Start()
        {
            if (toggleButton != null)
                toggleButton.onClick.AddListener(ToggleYakuList);
            
            if (yakuListPanel != null)
                yakuListPanel.SetActive(false);
        }

        public void ToggleYakuList()
        {
            if (yakuListPanel != null)
                yakuListPanel.SetActive(!yakuListPanel.activeSelf);
        }
    }
}
