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
        /// <summary>
        /// まぶたを画面の外まではみ出させる量。
        ///
        /// **ぴったり半分ずつだと、四辺と中央の継ぎ目に線が出る。**
        /// 丸め誤差で 1px 足りない行ができるため。上下左右へ少し余らせて隠す。
        /// </summary>
        private const float LidOverscan = 4f;

        private void SetupLid(RectTransform lid, bool isTop)
        {
            if (lid == null) return;

            lid.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
            lid.anchorMin = new Vector2(0f, isTop ? 0.5f : 0f);
            lid.anchorMax = new Vector2(1f, isTop ? 1f : 0.5f);

            // アンカー一杯に広げたうえで、四方へ少しはみ出させる。
            // 中央側（上まぶたなら下辺）も余らせて、2枚の境目を重ねる
            lid.offsetMin = new Vector2(-LidOverscan, isTop ? -LidOverscan : -LidOverscan);
            lid.offsetMax = new Vector2(LidOverscan, isTop ? LidOverscan : LidOverscan);

            // **スプライトを外して単色で塗る。**
            // 既定の `Background` は9スライスで、外周1pxが不透明にならない。
            // そのせいで画面の四辺と中央に、後ろの卓が透けた線が出ていた
            // （2026-09-27 に実機で確認）。sprite を null にすると
            // Image はただの塗りつぶしになり、端まできっちり黒くなる。
            var image = lid.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.type = Image.Type.Simple;
                var c = image.color;
                image.color = new Color(0f, 0f, 0f, 1f);
                // 元が真っ黒でなかった場合に備えて、明るさだけは元を尊重する
                if (c.r > 0.05f || c.g > 0.05f || c.b > 0.05f)
                    image.color = new Color(c.r, c.g, c.b, 1f);
            }
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

            // **一度で開ききる（2026-09-13 の指示）。**
            //
            // 元は「少し開いて閉じる」を2回はさんでから開いていた。
            // まぶたが開くたびに**赤い部屋が一瞬映っては消える**ので、
            // 画面が赤く点滅しているように見える、と指摘を受けた。
            // 実測でも まぶたの縮尺が 1.00→0.74→1.00→0.47→1.00→0.19→0.00 と往復していた。
            //
            // 目を覚ます感じは、開く速さ（finalOpenDuration）で出す。
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
