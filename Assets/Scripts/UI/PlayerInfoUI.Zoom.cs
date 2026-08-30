using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class PlayerInfoUI
    {
        private void BringToFront(Transform target)
        {
            if (target == null) return;

            // ルートのCanvasのみを手前に出す（子Canvasを一律上書きすると表示順が壊れるため）
            _sortingScope.BringToFront(target.gameObject, UISortingOrders.InfoPanelHighlight);

            // 手やスマホ本体などのSpriteRendererを手前に持ってくる
            var sprites = target.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var s in sprites)
            {
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
                s.sortingOrder = 0;
            }
        }

        // --- ズーム演出（指定したオブジェクトを巨大化し、少し手前・上に浮かせる） ---
        public System.Collections.IEnumerator ZoomInRoutine(float duration = 0.4f, float targetScaleMulti = 2.5f)
        {
            Debug.Log($"[PlayerInfoUI] ZoomInRoutine called! Target scale: {targetScaleMulti}");
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);
            
            isZoomedIn = true; // ズーム開始

            Transform targetObj = zoomTarget != null ? zoomTarget : transform;
            
            // ズーム対象が何であれ、PlayerInfoUI全体を最前面に出す
            BringToFront(transform);

            // 強制的にCanvasのSortOrderを引き上げる（インスペクターの値のままになる現象の回避）
            var myCanvas = GetComponent<Canvas>();
            if (myCanvas != null)
            {
                myCanvas.overrideSorting = true;
                myCanvas.sortingOrder = UISortingOrders.InfoPanelHighlight;
            }

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
                // 矩形の中心ではなく「絵の中心」を画面の中心に合わせる（BettingZoomLift 参照）
                targetPos = new Vector3(0f, BettingZoomLift, 0f);
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
            isZoomedIn = false; // ズーム終了
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

            // ズーム解除時に確実に通常時の値へ戻す
            var myCanvas = GetComponent<Canvas>();
            if (myCanvas != null)
            {
                myCanvas.sortingOrder = UISortingOrders.InfoPanelNormal;
            }
        }

        public System.Collections.IEnumerator ResetZoomRoutine(float duration = 0.3f)
        {
            isZoomedIn = false; // ズーム終了
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

            // ズーム解除時に確実に通常時の値へ戻す
            var myCanvas = GetComponent<Canvas>();
            if (myCanvas != null)
            {
                myCanvas.sortingOrder = UISortingOrders.InfoPanelNormal;
            }
        }
    }
}
