using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    /// <summary>
    /// オープニングの「目を覚ます」演出。
    ///
    /// 上下のまぶたパネルを Y スケールで開閉する。まぶたとして成立させるには
    /// 上まぶたが画面上端を、下まぶたが画面下端を軸に縮む必要があるため、
    /// pivot とアンカーは Awake で強制的に揃える（インスペクタの設定漏れ対策）。
    /// </summary>
    public class BlinkEffectUI : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private RectTransform topLidPanel;
        [SerializeField] private RectTransform bottomLidPanel;

        [Header("Timing")]
        [Tooltip("目を開け始めるまでの間（秒）")]
        [SerializeField] private float initialWait = 1.0f;
        [Tooltip("完全に開ききるのにかける時間（秒）")]
        [SerializeField] private float finalOpenDuration = 0.8f;

        private void Awake()
        {
            // まぶたは全UIより手前に出さないと、卓や紙の裏に隠れて何も見えない
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = UISortingOrders.OpeningEyelid;
            }

            SetupLid(topLidPanel, isTop: true);
            SetupLid(bottomLidPanel, isTop: false);

            // シーン開始時点で「目を閉じている」状態にしておく。
            // ここで閉じないと、演出が始まるまでの数秒間だけ卓が丸見えになる。
            SetLidsActive(true);
            SetLidScale(1f);
        }

        /// <summary>
        /// 上まぶたは画面上半分・pivot上端、下まぶたは画面下半分・pivot下端に揃える。
        /// こうしないと localScale.y を下げたときにパネル自身の中心へ縮んでしまい、
        /// 「画面中央に黒帯が2本残る」見た目になってまぶたに見えない。
        /// </summary>
        private void SetupLid(RectTransform lid, bool isTop)
        {
            if (lid == null) return;

            lid.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
            lid.anchorMin = new Vector2(0f, isTop ? 0.5f : 0f);
            lid.anchorMax = new Vector2(1f, isTop ? 1f : 0.5f);

            // アンカー一杯に広げる（画面比が変わっても上下ぴったり半分を覆う）
            lid.offsetMin = Vector2.zero;
            lid.offsetMax = Vector2.zero;
        }

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
            // 演出のたびに閉じた状態から始める（前回の実行で無効化されたままでも復帰する）
            SetLidsActive(true);
            SetLidScale(1f);

            yield return new WaitForSeconds(initialWait);

            // 1回目のまばたき（少し開いてすぐ閉じる）
            yield return StartCoroutine(MoveLidScale(1f, 0.7f, 0.2f));
            yield return StartCoroutine(MoveLidScale(0.7f, 1f, 0.15f));

            yield return new WaitForSeconds(0.3f);

            // 2回目のまばたき（もう少し開いて閉じる）
            yield return StartCoroutine(MoveLidScale(1f, 0.4f, 0.2f));
            yield return StartCoroutine(MoveLidScale(0.4f, 1f, 0.15f));

            yield return new WaitForSeconds(0.3f);

            // 3回目（完全に開く）
            yield return StartCoroutine(MoveLidScale(1f, 0f, finalOpenDuration));

            // 開ききったら無効化する（Raycastブロックなどを避けるため）
            SetLidsActive(false);

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

        private void SetLidsActive(bool active)
        {
            if (topLidPanel != null) topLidPanel.gameObject.SetActive(active);
            if (bottomLidPanel != null) bottomLidPanel.gameObject.SetActive(active);
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
