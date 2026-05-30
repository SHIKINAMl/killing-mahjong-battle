using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class EnemyInfoUI : MonoBehaviour
    {
        [Header("Enemy HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private UnityEngine.UI.Image hpFillImage; // 追加: 人型のHPメーター用画像
        private int maxHp = 20000; // 最大HP（割合計算用）
        
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

        public string PlayReaction(ReactionTrigger trigger, float duration = 3.0f, string formatArg = "")
        {
            if (characterData == null || characterData.reactions == null || characterData.reactions.Count == 0)
                return null;

            // 条件に合うリアクションを全て抽出
            var matches = characterData.reactions.FindAll(r => r.trigger == trigger);
            if (matches.Count == 0) return null;

            // ランダムに1つ選ぶ
            var reaction = matches[Random.Range(0, matches.Count)];

            // 画像が設定されていれば一時的に変更
            if (reaction.faceSprite != null)
            {
                if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
                reactionCoroutine = StartCoroutine(TemporaryFaceRoutine(reaction.faceSprite, duration));
            }

            string text = reaction.dialogueText;
            Debug.Log($"[EnemyInfoUI] PlayReaction: trigger={trigger}, matchFound=true, dialogueText='{text}'");
            
            if (!string.IsNullOrEmpty(formatArg) && !string.IsNullOrEmpty(text))
            {
                text = string.Format(text, formatArg);
            }

            return string.IsNullOrEmpty(text) ? null : $"「{text}」";
        }

        private System.Collections.IEnumerator TemporaryFaceRoutine(Sprite newSprite, float duration)
        {
            if (characterRenderer != null && newSprite != null)
            {
                characterRenderer.sprite = newSprite;
            }

            yield return new WaitForSeconds(duration);

            if (characterRenderer != null && normalSprite != null)
            {
                characterRenderer.sprite = normalSprite;
            }
            
            reactionCoroutine = null;
        }

        public void PlayReactionWithFace(Sprite faceSprite, float duration)
        {
            if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
            reactionCoroutine = StartCoroutine(TemporaryFaceRoutine(faceSprite, duration));
            PlayBounceAnimation(0.3f); // 0.3秒で一瞬跳ねる
        }

        public void SetHP(int hp)
        {
            if (hpText != null)
            {
                // 数字のみ表示する
                hpText.text = hp.ToString();
            }
            
            // 人型メーターの割合を更新する
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = (float)hp / maxHp;
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
