using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public class PlayerInfoUI : MonoBehaviour
    {
        [Header("HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private UnityEngine.UI.Image hpFillImage; // 追加: 人型のHPメーター用画像
        private int maxHp = 20000; // 最大HP（割合計算用）

        [Header("Zoom Target")]
        [SerializeField] private Transform zoomTarget; // 追加：拡大させたい子オブジェクトを指定
        [SerializeField] private Vector2 zoomOffsetUI = new Vector2(-1200f, 100f); // UI時の移動量
        [SerializeField] private Vector3 zoomOffsetWorld = new Vector3(-4.0f, 1.0f, -2.0f); // 3D時の移動量

        [Header("Character Portrait")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private CharacterData characterData; // キャラクター管理データ

        private int currentHp = 20000; // 暫定の初期HP
        private Sprite normalSprite;
        private Sprite discardSprite;
        
        private Coroutine bounceCoroutine;
        private Coroutine zoomCoroutine;
        private Vector3 originalPosition;
        
        // ズーム用
        private Vector3 originalLocalPos;
        private Vector3 originalScale;

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

            // UI要素が前面に出て牌のクリック判定を吸い取るのを防ぐため、当たり判定を無効化する
            var graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            foreach (var g in graphics)
            {
                g.raycastTarget = false;
            }
        }

        public void SetMaxHP(int max)
        {
            maxHp = max;
        }

        public void SetHP(int hp)
        {
            currentHp = hp;
            if (hpText != null)
            {
                // 数字のみ表示する
                hpText.text = currentHp.ToString();
            }
            
            // 人型メーターの割合を更新する
            if (hpFillImage != null)
            {
                hpFillImage.fillAmount = (float)hp / maxHp;
            }
        }
        
        public void ReduceHp(int amount)
        {
            currentHp -= amount;
            SetHP(currentHp);
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
                // 実行中に変更された場合も反映されるように、characterDataから直接読み取る
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
            float bounceSpeed = 15f;
            float bounceHeight = 0.5f; // SpriteRenderer用に調整 
            
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

        // --- ズーム演出（指定したオブジェクトを巨大化し、少し手前・上に浮かせる） ---
        public System.Collections.IEnumerator ZoomInRoutine(float duration = 0.4f, float targetScaleMulti = 2.5f)
        {
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;

            // ズーム開始直前の位置とサイズを記憶する
            originalLocalPos = targetObj.localPosition;
            originalScale = targetObj.localScale;

            // 0の場合の安全対策
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
                float eased = progress * progress * (3f - 2f * progress); // 滑らかに

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
        }
    }
}
