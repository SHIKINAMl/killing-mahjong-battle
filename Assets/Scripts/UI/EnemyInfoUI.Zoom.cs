using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
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
