using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class PlayerInfoUI : MonoBehaviour
    {
        [Header("HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private int maxHP = 100;
        private int currentHP;

        private void Start()
        {
            currentHP = maxHP;
            UpdateHPDisplay();
        }

        public void SetHP(int hp)
        {
            currentHP = Mathf.Clamp(hp, 0, maxHP);
            UpdateHPDisplay();
        }

        private void UpdateHPDisplay()
        {
            if (hpText != null)
            {
                hpText.text = $"HP: {currentHP} / {maxHP}";
            }
        }
    }
}
