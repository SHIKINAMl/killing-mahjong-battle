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
        private Coroutine reactionCoroutine;
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

        public string GetClickDialogue()
        {
            if (characterData != null && !string.IsNullOrEmpty(characterData.clickDialogue))
            {
                return $"「{characterData.clickDialogue}」";
            }
            return null;
        }

        public string GetIntroductionDialogue()
        {
            if (characterData != null && !string.IsNullOrEmpty(characterData.introductionDialogue))
            {
                return $"「{characterData.introductionDialogue}」";
            }
            return null;
        }

        public string GetWinDialogue()
        {
            if (characterData != null && !string.IsNullOrEmpty(characterData.winDialogue))
            {
                return $"「{characterData.winDialogue}」";
            }
            return null;
        }

        public string GetLoseDialogue()
        {
            if (characterData != null && !string.IsNullOrEmpty(characterData.loseDialogue))
            {
                return $"「{characterData.loseDialogue}」";
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

        public void SetDiscardingState(bool isDiscarding)
        {
            if (characterRenderer != null)
            {
                // インスペクターで実行中に変更された場合も反映されるように、characterDataから直接読み取る
                Sprite targetDiscardSprite = (characterData != null && characterData.discardSprite != null) ? characterData.discardSprite : discardSprite;
                Sprite targetNormalSprite = (characterData != null && characterData.normalSprite != null) ? characterData.normalSprite : normalSprite;

                if (isDiscarding && targetDiscardSprite != null)
                {
                    characterRenderer.sprite = targetDiscardSprite;
                }
                else if (!isDiscarding && targetNormalSprite != null)
                {
                    characterRenderer.sprite = targetNormalSprite;
                }
            }
        }

        /// <summary>
        /// びっくりした顔に変更し、指定時間後に元に戻す
        /// </summary>
        public void PlaySurprisedReaction(float duration)
        {
            if (characterData == null) return;
            
            if (reactionCoroutine != null)
            {
                StopCoroutine(reactionCoroutine);
            }
            
            reactionCoroutine = StartCoroutine(SurprisedRoutine(duration));
        }

        private System.Collections.IEnumerator SurprisedRoutine(float duration)
        {
            if (characterRenderer != null && characterData.surprisedSprite != null)
            {
                characterRenderer.sprite = characterData.surprisedSprite;
            }

            yield return new WaitForSeconds(duration);

            if (characterRenderer != null && normalSprite != null)
            {
                characterRenderer.sprite = normalSprite;
            }
            
            reactionCoroutine = null;
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
            // duration の時間内で Sin(0) から Sin(PI) まで推移するように速度を計算 (1回跳ねる)
            float bounceSpeed = Mathf.PI / duration;
            float bounceHeight = 0.5f; // SpriteRenderer用に調整（必要に応じてインスペクターで調整可能にすることもできます）
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float yOffset = Mathf.Sin(elapsed * bounceSpeed) * bounceHeight;
                characterRenderer.transform.localPosition = originalPosition + new Vector3(0, yOffset, 0);
                yield return null;
            }
            
            characterRenderer.transform.localPosition = originalPosition;
            bounceCoroutine = null;
        }
    }
}
