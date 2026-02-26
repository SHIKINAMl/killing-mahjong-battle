using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class EnemyInfoUI : MonoBehaviour
    {
        [Header("Enemy HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private int maxHP = 100000;
        
        [Header("Enemy Panel Settings")]
        [SerializeField] private GameObject enemyPanel; // 敵パネルの参照

        private int currentHP;

        private void Start()
        {
            currentHP = maxHP;
            UpdateHPDisplay();
        }

        public void SetHP(int hp)
        {
            if (hp > maxHP)
            {
                maxHP = hp; // 最大HPの更新が必要な場合
            }
            currentHP = Mathf.Clamp(hp, 0, maxHP);
            UpdateHPDisplay();
        }

        public void SetPanelVisible(bool visible)
        {
            if (enemyPanel != null)
            {
                enemyPanel.SetActive(visible);
            }
        }

        private void UpdateHPDisplay()
        {
            if (hpText != null)
            {
                hpText.text = $"Enemy HP: {currentHP} / {maxHP}";
            }
        }
    }
}
