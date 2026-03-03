using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class EnemyInfoUI : MonoBehaviour
    {
        [Header("Enemy HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        
        [Header("Character Portrait")]
        [SerializeField] private UnityEngine.UI.Image characterImage;

        [Header("Enemy Panel Settings")]
        [SerializeField] private GameObject enemyPanel; // 敵パネルの参照

        public void SetHP(int hp)
        {
            if (hpText != null)
            {
                // MaxHPの概念は一旦表示せず、現在HPのみそのまま表示します
                hpText.text = $"Enemy HP: {hp}";
            }
        }

        public void SetPanelVisible(bool visible)
        {
            if (enemyPanel != null)
            {
                enemyPanel.SetActive(visible);
            }
        }

        public void SetCharacterSprite(Sprite sprite)
        {
            if (characterImage != null && sprite != null)
            {
                characterImage.sprite = sprite;
            }
        }
    }
}
