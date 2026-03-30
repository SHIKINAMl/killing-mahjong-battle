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
        [SerializeField] private CharacterData characterData; // キャラクター管理データ

        [Header("Enemy Panel Settings")]
        [SerializeField] private GameObject enemyPanel; // 敵パネルの参照

        private Sprite normalSprite;
        private Sprite discardSprite;

        private void Awake()
        {
            if (characterData != null)
            {
                normalSprite = characterData.normalSprite;
                discardSprite = characterData.discardSprite;
                
                if (characterImage != null && normalSprite != null)
                {
                    characterImage.sprite = normalSprite;
                }
            }
            else if (characterImage != null)
            {
                normalSprite = characterImage.sprite;
            }
        }

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

        /// <summary>
        /// 打牌する時の画像（あるいは元の画像）に切り替える
        /// </summary>
        public void SetDiscardingState(bool isDiscarding)
        {
            if (characterImage != null)
            {
                if (isDiscarding && discardSprite != null)
                {
                    characterImage.sprite = discardSprite;
                }
                else if (!isDiscarding && normalSprite != null)
                {
                    characterImage.sprite = normalSprite;
                }
            }
        }
    }
}
