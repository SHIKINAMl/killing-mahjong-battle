using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

namespace KillingMahjong.UI
{
    public class BlinkEffectUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private RectTransform topLidPanel;
        [SerializeField] private RectTransform bottomLidPanel;

        /// <summary>
        /// 目を覚ます（まばたきを数回して完全に開く）演出
        /// </summary>
        public void PlayWakeUpEffect(Action onComplete = null)
        {
            if (topLidPanel == null || bottomLidPanel == null)
            {
                Debug.LogWarning("[BlinkEffectUI] まぶたのパネルが設定されていません。演出をスキップします。");
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(WakeUpRoutine(onComplete));
        }

        private IEnumerator WakeUpRoutine(Action onComplete)
        {
            // 初期状態：完全に閉じている (スケールY = 1, アンカーによる全画面の半分を覆う想定)
            SetLidScale(1f);
            
            // 少し待つ
            yield return new WaitForSeconds(1.0f);

            // 1回目のまばたき（少し開いてすぐ閉じる）
            yield return StartCoroutine(MoveLidScale(1f, 0.7f, 0.2f));
            yield return StartCoroutine(MoveLidScale(0.7f, 1f, 0.15f));
            
            yield return new WaitForSeconds(0.3f);

            // 2回目のまばたき（もう少し開いて閉じる）
            yield return StartCoroutine(MoveLidScale(1f, 0.4f, 0.2f));
            yield return StartCoroutine(MoveLidScale(0.4f, 1f, 0.15f));

            yield return new WaitForSeconds(0.3f);

            // 3回目（完全に開く）
            yield return StartCoroutine(MoveLidScale(1f, 0f, 0.8f));

            // 開ききったら無効化する（Raycastブロックなどを避けるため）
            topLidPanel.gameObject.SetActive(false);
            bottomLidPanel.gameObject.SetActive(false);

            onComplete?.Invoke();
        }

        private IEnumerator MoveLidScale(float startScale, float endScale, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                // イーズアウト（ゆっくり止まる）
                float ease = 1f - Mathf.Pow(1f - t, 3f); 
                
                SetLidScale(Mathf.Lerp(startScale, endScale, ease));
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetLidScale(endScale);
        }

        private void SetLidScale(float scaleY)
        {
            if (topLidPanel != null)
                topLidPanel.localScale = new Vector3(1f, scaleY, 1f);
            if (bottomLidPanel != null)
                bottomLidPanel.localScale = new Vector3(1f, scaleY, 1f);
        }
    }
}
