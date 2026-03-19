using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class PlayerInfoUI : MonoBehaviour
    {
        [Header("HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("Character Portrait")]
        [SerializeField] private UnityEngine.UI.Image characterImage;

        private int currentHp = 20000; // 暫定の初期HP

        public void SetHP(int hp)
        {
            currentHp = hp;
            if (hpText != null)
            {
                // MaxHPの概念は一旦表示せず、現在HPのみそのまま表示します
                hpText.text = $"HP: {currentHp}";
            }
        }
        
        public void ReduceHp(int amount)
        {
            currentHp -= amount;
            SetHP(currentHp);
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
