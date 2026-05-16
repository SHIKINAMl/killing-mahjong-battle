using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class EnemyInfoUI : MonoBehaviour
    {
        [Header("Enemy HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        
        [Header("Character Portrait")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private CharacterData characterData; // キャラクター管理データ

        [Header("Available Enemies")]
        [SerializeField] private CharacterData[] availableEnemies; // インスペクターで登録する敵キャラクターリスト
        private int currentEnemyIndex = -1; // -1 = デフォルトの characterData を使用中

        [Header("Enemy Panel Settings")]
        [SerializeField] private GameObject enemyPanel; // 敵パネルの参照

        private Sprite normalSprite;
        private Sprite discardSprite;
        
        private Coroutine bounceCoroutine;
        private Vector3 originalPosition;

        /// <summary>
        /// 現在選択されている CharacterData を取得する
        /// </summary>
        public CharacterData CurrentCharacterData => characterData;

        private void Awake()
        {
            if (characterData != null)
            {
                normalSprite = characterData.normalSprite;
                discardSprite = characterData.discardSprite;
                
                if (characterRenderer != null && normalSprite != null)
                {
                    characterRenderer.sprite = normalSprite;
                }
            }
            else if (characterRenderer != null)
            {
                normalSprite = characterRenderer.sprite;
            }
        }

        private void Start()
        {
            if (characterRenderer != null)
            {
                originalPosition = characterRenderer.transform.localPosition;
            }
        }

        /// <summary>
        /// 次の敵キャラクターに切り替える。
        /// availableEnemies リストの中を順番にループする。
        /// </summary>
        public void CycleEnemy()
        {
            if (availableEnemies == null || availableEnemies.Length == 0) return;

            currentEnemyIndex = (currentEnemyIndex + 1) % availableEnemies.Length;
            ApplyCharacterData(availableEnemies[currentEnemyIndex]);
        }

        /// <summary>
        /// 指定した CharacterData を適用して画像を切り替える
        /// </summary>
        private void ApplyCharacterData(CharacterData data)
        {
            if (data == null) return;

            characterData = data;
            normalSprite = data.normalSprite;
            discardSprite = data.discardSprite;

            if (characterRenderer != null && normalSprite != null)
            {
                characterRenderer.sprite = normalSprite;
            }
        }

        /// <summary>
        /// クリックされた時のリアクションセリフを取得する
        /// </summary>
        public string GetClickDialogue()
        {
            if (characterData != null && !string.IsNullOrEmpty(characterData.clickDialogue))
            {
                return $"「{characterData.clickDialogue}」";
            }
            return null;
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
            if (characterRenderer != null && sprite != null)
            {
                characterRenderer.sprite = sprite;
            }
        }

        /// <summary>
        /// 打牌する時の画像（あるいは元の画像）に切り替える
        /// </summary>
        public void SetDiscardingState(bool isDiscarding)
        {
            if (characterRenderer != null)
            {
                if (isDiscarding && discardSprite != null)
                {
                    characterRenderer.sprite = discardSprite;
                }
                else if (!isDiscarding && normalSprite != null)
                {
                    characterRenderer.sprite = normalSprite;
                }
            }
        }

        public void PlayBounceAnimation(float duration)
        {
            if (characterRenderer == null) return;
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            bounceCoroutine = StartCoroutine(BounceRoutine(duration));
        }

        private System.Collections.IEnumerator BounceRoutine(float duration)
        {
            float elapsed = 0f;
            float bounceSpeed = 15f;
            float bounceHeight = 0.5f; // SpriteRenderer用に調整（必要に応じてインスペクターで調整可能にすることもできます）
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float yOffset = Mathf.Abs(Mathf.Sin(elapsed * bounceSpeed)) * bounceHeight;
                characterRenderer.transform.localPosition = originalPosition + new Vector3(0, yOffset, 0);
                yield return null;
            }
            
            characterRenderer.transform.localPosition = originalPosition;
            bounceCoroutine = null;
        }
    }
}
