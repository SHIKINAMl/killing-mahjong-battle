using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class EnemyInfoUI : MonoBehaviour
    {
        [Header("Enemy HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private UnityEngine.UI.Image hpFillImage; 
        private int maxHp = 20000; 
        private int currentHp = 20000;
        
        [Header("Boost Bonus")]
        [SerializeField] private TextMeshProUGUI boostBonusText; // 動的生成も可
        
        [Header("Zoom Target")]
        [SerializeField] private Transform zoomTarget; // 追加：拡大させたい子オブジェクトを指定
        [SerializeField] private Vector2 zoomOffsetUI = new Vector2(-1200f, 100f); // UI時の移動量
        [SerializeField] private Vector3 zoomOffsetWorld = new Vector3(-4.0f, 1.0f, -2.0f); // 3D時の移動量

        [Header("Character Portrait")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer faceRenderer; // 追加：表情レイヤー用
        [SerializeField] private CharacterData characterData; // キャラクター管理データ
        [SerializeField] private float bounceDuration = 0.5f; // 上下する時間（インスペクターで設定可能）
        [SerializeField] private float bounceHeight = 0.5f;   // 上下する高さ（インスペクターで設定可能）

        [Header("Available Enemies")]
        [SerializeField] private CharacterData[] availableEnemies; // インスペクターで登録する敵キャラクターリスト
        private int currentEnemyIndex = -1; // -1 = デフォルトの characterData を使用中

        [Header("Enemy Panel Settings")]
        [SerializeField] private GameObject enemyPanel; // 敵パネルの参照

        private Sprite normalSprite;
        private Sprite discardSprite;
        private Sprite normalFaceSprite; // 通常時の顔画像
        
        private Coroutine bounceCoroutine;
        private Coroutine reactionCoroutine;
        private Coroutine zoomCoroutine;
        private Coroutine blinkCoroutine;
        private Vector3 originalPosition;

        // ズーム用
        private Vector3 originalLocalPos;
        private Vector3 originalScale;

        /// <summary>
        /// 現在選択されている CharacterData を取得する
        /// </summary>
        public CharacterData CurrentCharacterData => characterData;

        private void OnEnable()
        {
            if (blinkCoroutine == null)
            {
                blinkCoroutine = StartCoroutine(BlinkRoutine());
            }
        }

        private void OnDisable()
        {
            if (blinkCoroutine != null)
            {
                StopCoroutine(blinkCoroutine);
                blinkCoroutine = null;
            }
        }

        private System.Collections.IEnumerator BlinkRoutine()
        {
            while (true)
            {
                if (characterData == null || !characterData.enableBlink || faceRenderer == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                float waitTime = Random.Range(characterData.blinkIntervalMin, characterData.blinkIntervalMax);
                yield return new WaitForSeconds(waitTime);

                if (reactionCoroutine != null || faceRenderer.sprite != normalFaceSprite)
                    continue;

                Sprite fullBlink = characterData.faceSprites?.Find(x => x.id == characterData.blinkFaceId)?.sprite;

                if (fullBlink != null)
                {
                    if (faceRenderer.sprite == normalFaceSprite && reactionCoroutine == null)
                    {
                        faceRenderer.sprite = fullBlink;
                        yield return new WaitForSeconds(0.12f);
                    }

                    if (faceRenderer.sprite == fullBlink && reactionCoroutine == null)
                    {
                        faceRenderer.sprite = normalFaceSprite;
                    }
                }
            }
        }

        private void Awake()
        {
            if (characterData != null)
            {
                ApplyCharacterData(characterData);
            }
            else if (characterRenderer != null)
            {
                normalSprite = characterRenderer.sprite;
                if (faceRenderer != null) normalFaceSprite = faceRenderer.sprite;
            }
        }

        private void Start()
        {
            if (characterRenderer != null)
            {
                originalPosition = characterRenderer.transform.localPosition;
            }

            // UI要素が前面に出て牌のクリック判定を吸い取るのを防ぐため、当たり判定を無効化する
            var graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            foreach (var g in graphics)
            {
                g.raycastTarget = false;
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
            
            Sprite defaultBody = null;
            Sprite defaultFace = null;
            
            if (data.bodySprites != null && data.bodySprites.Count > 0)
            {
                var match = data.bodySprites.Find(x => x.id == data.defaultBodyId);
                if (match != null) defaultBody = match.sprite;
                else defaultBody = data.bodySprites[0].sprite;
                normalSprite = defaultBody; // 上書き
            }
            
            if (data.faceSprites != null && data.faceSprites.Count > 0)
            {
                var match = data.faceSprites.Find(x => x.id == data.defaultFaceId);
                if (match != null) defaultFace = match.sprite;
                else defaultFace = data.faceSprites[0].sprite;
                normalFaceSprite = defaultFace;
            }

            if (characterRenderer != null && normalSprite != null)
            {
                characterRenderer.sprite = normalSprite;
            }
            
            if (faceRenderer != null && normalFaceSprite != null)
            {
                faceRenderer.sprite = normalFaceSprite;
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
            if (!string.IsNullOrEmpty(reaction.faceExpressionId))
            {
                var match = characterData.faceSprites?.Find(x => x.id == reaction.faceExpressionId);
                var bodyMatch = characterData.bodySprites?.Find(x => x.id == reaction.bodyExpressionId);

                if ((match != null && match.sprite != null) || (bodyMatch != null && bodyMatch.sprite != null))
                {
                    if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
                    reactionCoroutine = StartCoroutine(TemporaryVisualRoutine(bodyMatch?.sprite, match?.sprite, duration));
                }
            }

            string text = reaction.dialogueText;
            Debug.Log($"[EnemyInfoUI] PlayReaction: trigger={trigger}, matchFound=true, dialogueText='{text}'");
            
            if (!string.IsNullOrEmpty(formatArg) && !string.IsNullOrEmpty(text))
            {
                text = string.Format(text, formatArg);
            }

            return string.IsNullOrEmpty(text) ? null : $"「{text}」";
        }

        private System.Collections.IEnumerator TemporaryVisualRoutine(Sprite newBody, Sprite newFace, float duration)
        {
            Sprite originalFace = (faceRenderer != null) ? normalFaceSprite : null;
            Sprite originalBody = normalSprite;

            if (faceRenderer != null && newFace != null)
                faceRenderer.sprite = newFace;
            
            if (characterRenderer != null && newBody != null)
                characterRenderer.sprite = newBody;

            yield return new WaitForSeconds(duration);

            if (faceRenderer != null && originalFace != null)
                faceRenderer.sprite = originalFace;
            
            if (characterRenderer != null && originalBody != null)
                characterRenderer.sprite = originalBody;
            
            reactionCoroutine = null;
        }

        public void PlayReactionWithVisual(Sprite newBody, Sprite newFace, float duration)
        {
            if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
            reactionCoroutine = StartCoroutine(TemporaryVisualRoutine(newBody, newFace, duration));
            PlayBounceAnimation(0.3f); // 0.3秒で一瞬跳ねる
        }

        public void PlayReactionWithVisualId(string bodyId, string faceId, float duration)
        {
            if ((string.IsNullOrEmpty(bodyId) && string.IsNullOrEmpty(faceId)) || characterData == null)
            {
                PlayBounceAnimation(0.3f);
                return;
            }
            
            Sprite newBody = null;
            if (!string.IsNullOrEmpty(bodyId))
            {
                var bodyMatch = characterData.bodySprites?.Find(x => x.id == bodyId);
                if (bodyMatch != null) newBody = bodyMatch.sprite;
            }

            Sprite newFace = null;
            if (!string.IsNullOrEmpty(faceId))
            {
                var faceMatch = characterData.faceSprites?.Find(x => x.id == faceId);
                if (faceMatch != null) newFace = faceMatch.sprite;
            }

            if (newBody != null || newFace != null)
            {
                PlayReactionWithVisual(newBody, newFace, duration);
            }
            else
            {
                PlayBounceAnimation(0.3f);
                Debug.LogWarning($"[EnemyInfoUI] Neither body '{bodyId}' nor face '{faceId}' found in CharacterData!");
            }
        }

        public void SetMaxHP(int max)
        {
            maxHp = max;
        }

        public void SetHP(int hp)
        {
            currentHp = hp;
            if (hpText != null) hpText.text = currentHp.ToString();
            
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
            // SetBodyPoseかSetFaceExpressionを本来は推奨するが、旧互換として残す
            if (characterRenderer != null && sprite != null)
            {
                characterRenderer.sprite = sprite;
            }
        }

        public void SetBodyPose(string poseId)
        {
            if (characterData == null || characterRenderer == null) return;
            var match = characterData.bodySprites?.Find(x => x.id == poseId);
            if (match != null && match.sprite != null)
            {
                characterRenderer.sprite = match.sprite;
            }
        }

        public void SetFaceExpression(string expressionId)
        {
            if (characterData == null || faceRenderer == null) return;
            var match = characterData.faceSprites?.Find(x => x.id == expressionId);
            if (match != null && match.sprite != null)
            {
                faceRenderer.sprite = match.sprite;
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

        public void PlayBounceAnimation(float duration = 0.5f)
        {
            if (characterRenderer == null) return;
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            // 引数で渡されたduration（例: 5秒）に関わらず、インスペクターで設定した時間で一瞬跳ねる
            bounceCoroutine = StartCoroutine(BounceRoutine(bounceDuration));
        }

        private System.Collections.IEnumerator BounceRoutine(float durationToBounce)
        {
            float elapsed = 0f;
            // durationToBounce の時間内で Sin(0) から Sin(PI) まで推移するように速度を計算 (1回跳ねる)
            float bounceSpeed = Mathf.PI / durationToBounce;
            
            while (elapsed < durationToBounce)
            {
                elapsed += Time.deltaTime;
                float yOffset = Mathf.Sin(elapsed * bounceSpeed) * bounceHeight;
                characterRenderer.transform.localPosition = originalPosition + new Vector3(0, yOffset, 0);
                yield return null;
            }
            
            characterRenderer.transform.localPosition = originalPosition;
            bounceCoroutine = null;
        }

        // --- ズーム演出（指定したオブジェクトを巨大化し、少し手前・上に浮かせる） ---
        public System.Collections.IEnumerator ZoomInRoutine(float duration = 0.4f, float targetScaleMulti = 2.5f)
        {
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;

            // ズーム中は揺れを止める（右に戻ってしまうバグ対策）
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = false;
            }

            // ズーム開始直前の位置とサイズを記憶する
            originalLocalPos = targetObj.localPosition;
            originalScale = targetObj.localScale;
            if (originalScale == Vector3.zero) originalScale = Vector3.one;

            // UI用（ピクセル単位）か、3D用かで移動量を変える必要がある
            RectTransform rt = targetObj.GetComponent<RectTransform>();
            float moveX = (rt != null) ? zoomOffsetUI.x : zoomOffsetWorld.x;
            float moveY = (rt != null) ? zoomOffsetUI.y : zoomOffsetWorld.y;
            float moveZ = (rt != null) ? 0f : zoomOffsetWorld.z;

            Vector3 targetPos = originalLocalPos + new Vector3(moveX, moveY, moveZ);
            Vector3 targetScale = originalScale * targetScaleMulti;

            float t = 0;
            while (t < duration)
            {
                float progress = t / duration;
                float eased = progress * progress * (3f - 2f * progress);

                targetObj.localPosition = Vector3.Lerp(originalLocalPos, targetPos, eased);
                targetObj.localScale = Vector3.Lerp(originalScale, targetScale, eased);

                t += Time.deltaTime;
                yield return null;
            }

            targetObj.localPosition = targetPos;
            targetObj.localScale = targetScale;
        }

        public System.Collections.IEnumerator ResetZoomRoutine(float duration = 0.3f)
        {
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;

            Vector3 startPos = targetObj.localPosition;
            Vector3 startScale = targetObj.localScale;

            float t = 0;
            while (t < duration)
            {
                float progress = t / duration;
                float eased = progress * progress * (3f - 2f * progress);

                targetObj.localPosition = Vector3.Lerp(startPos, originalLocalPos, eased);
                targetObj.localScale = Vector3.Lerp(startScale, originalScale, eased);

                t += Time.deltaTime;
                yield return null;
            }

            targetObj.localPosition = originalLocalPos;
            targetObj.localScale = originalScale;

            // ズーム終了後に揺れを再開する
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = true;
                anim.UpdateInitialPosition();
            }
        }
    }
}
