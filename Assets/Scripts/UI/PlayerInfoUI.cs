using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public class PlayerInfoUI : MonoBehaviour
    {
        [Header("HP Display")]
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private UnityEngine.UI.Image hpFillImage; // 追加: 人型のHPメーター用画像
        private int maxHp = 20000; // 最大HP（割合計算用）

        [Header("Boost Bonus")]
        [SerializeField] private TextMeshProUGUI boostBonusText; // 動的生成も可

        [Header("Zoom Target")]
        [SerializeField] private Transform zoomTarget; // 追加：拡大させたい子オブジェクトを指定
        [SerializeField] private Vector3 zoomOffsetUI = new Vector3(-1200f, 100f, -500f); // ズーム時に手前に出すためVector3に変更
        [SerializeField] private Vector3 zoomOffsetWorld = new Vector3(-4.0f, 1.0f, -2.0f); // 3D時の移動量

        [Header("Character Portrait")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer faceRenderer; // 追加：表情レイヤー用
        [SerializeField] private CharacterData characterData; // キャラクター管理データ

        public CharacterData CurrentCharacterData => characterData;

        private int currentHp = 20000; // 暫定の初期HP
        private Sprite normalSprite;
        private Sprite discardSprite;
        private Sprite normalFaceSprite; // 通常時の顔画像
        
        private Coroutine bounceCoroutine;
        private Coroutine zoomCoroutine;
        private Coroutine blinkCoroutine;
        private Vector3 originalPosition;
        
        // ズーム用
        private Vector3 originalLocalPos;
        private Vector3 originalScale;

        private bool isInitialized = false;

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

                if (faceRenderer.sprite != normalFaceSprite)
                    continue;

                Sprite fullBlink = characterData.faceSprites?.Find(x => x.id == characterData.blinkFaceId)?.sprite;

                if (fullBlink != null)
                {
                    if (faceRenderer.sprite == normalFaceSprite)
                    {
                        faceRenderer.sprite = fullBlink;
                        yield return new WaitForSeconds(0.12f);
                    }

                    if (faceRenderer.sprite == fullBlink)
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

            InitializeOriginalTransform();
        }

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

        private void InitializeOriginalTransform()
        {
            if (isInitialized) return;

            if (characterRenderer != null)
            {
                originalPosition = characterRenderer.transform.localPosition;
            }

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;
            originalLocalPos = targetObj.localPosition;
            originalScale = targetObj.localScale;

            // UI要素が前面に出て牌のクリック判定を吸い取るのを防ぐため、当たり判定を無効化する
            // ただし、ボタンとして機能すべき要素（自身や親にButtonコンポーネントを持つ）は除外する
            var graphics = GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
            foreach (var g in graphics)
            {
                if (g.GetComponentInParent<UnityEngine.UI.Button>(true) != null)
                {
                    continue;
                }
                g.raycastTarget = false;
            }

            isInitialized = true;
        }

        private void Start()
        {
            InitializeOriginalTransform();
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
            // 旧互換として残す
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

        /// <summary>強調表示時の前面化と復元。プロジェクトルールに従いルートCanvasの overrideSorting のみを操作する。</summary>
        private readonly CanvasSortingScope _sortingScope = new CanvasSortingScope();

        private void BringToFront(Transform target)
        {
            if (target == null) return;

            // ルートのCanvasのみを手前に出す（子Canvasを一律上書きすると表示順が壊れるため）
            _sortingScope.BringToFront(target.gameObject, UISortingOrders.InfoPanelHighlight, "UI");

            // 手やスマホ本体などのSpriteRendererを手前に持ってくる
            var sprites = target.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprites)
            {
                s.sortingLayerName = "UI";
                s.sortingOrder = UISortingOrders.InfoPanelHighlight;
            }
        }

        private void ResetSorting(Transform target)
        {
            if (target == null) return;

            _sortingScope.Restore(target.gameObject);

            var sprites = target.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprites)
            {
                s.sortingLayerName = "Default";
                s.sortingOrder = 0;
            }
        }


        // --- ズーム演出（指定したオブジェクトを巨大化し、少し手前・上に浮かせる） ---
        public System.Collections.IEnumerator ZoomInRoutine(float duration = 0.4f, float targetScaleMulti = 2.5f)
        {
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;
            
            // ズーム対象が何であれ、PlayerInfoUI全体を最前面に出す
            BringToFront(transform);

            // 0の場合の安全対策
            if (originalScale == Vector3.zero) originalScale = Vector3.one;

            // ズーム中は揺れを止める
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = false;
            }

            // UI用（ピクセル単位）か、3D用かで移動量を変える必要がある
            RectTransform rt = targetObj.GetComponent<RectTransform>();
            Vector3 targetPos;
            if (rt != null)
            {
                targetPos = Vector3.zero;
            }
            else
            {
                targetPos = Vector3.zero;
            }

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

        public void ResetZoomImmediate()
        {
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
            Transform targetObj = zoomTarget != null ? zoomTarget : transform;
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = true;
            }

            targetObj.localPosition = originalLocalPos;
            targetObj.localScale = originalScale;
            ResetSorting(transform);
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
            var floatAnims = GetComponentsInChildren<FloatingAnimator>(true);
            foreach (var anim in floatAnims)
            {
                anim.enabled = true;
            }
            ResetSorting(transform);
        }
    }
}
