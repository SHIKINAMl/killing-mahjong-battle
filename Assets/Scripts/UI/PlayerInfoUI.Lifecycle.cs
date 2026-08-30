using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class PlayerInfoUI
    {
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

        private void LateUpdate()
        {
            // アニメーター等で強制的に戻されてしまうのを防ぐため、ズーム中は毎フレーム最後に上書きする
            if (isZoomedIn)
            {
                var myCanvas = GetComponent<Canvas>();
                if (myCanvas != null && myCanvas.sortingOrder != UISortingOrders.InfoPanelHighlight)
                {
                    myCanvas.sortingOrder = UISortingOrders.InfoPanelHighlight;
                }
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
            CharacterVisualUtil.ResolveDefaultSprites(data, ref normalSprite, ref discardSprite, ref normalFaceSprite);

            CharacterVisualUtil.ApplyIfPresent(characterRenderer, normalSprite);
            CharacterVisualUtil.ApplyIfPresent(faceRenderer, normalFaceSprite);
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

            // **ここで先に作っておく。** ダメージを受けてから作ったのでは、
            // 混ぜるための「過去の画面」が1枚も溜まっていない。
            // 作った時点から保存が始まるので、最初の一撃から効く
            HpDamageGlitch.Ensure();
        }

        public void StartTurnTimer(float duration)
        {
            if (timerUI != null)
            {
                timerUI.StartTimer(duration);
            }
        }

        public void StopTurnTimer()
        {
            if (timerUI != null)
            {
                timerUI.StopTimer();
            }
        }
    }
}
