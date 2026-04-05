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
        [SerializeField] private CharacterData characterData; // キャラクター管理データ

        private int currentHp = 20000; // 暫定の初期HP
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
