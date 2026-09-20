using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        // 相手の番を静かに示すための値。位置・明滅・浮遊状態には触れない。
        private const float OpponentTurnScaleMultiplier = 1.03f;
        private const float OpponentTurnTransitionSeconds = 0.25f;

        private Coroutine opponentTurnEmphasisCoroutine;
        private Transform opponentTurnEmphasisTarget;
        private Vector3 opponentTurnEmphasisBaseScale;
        private bool hasOpponentTurnEmphasisBaseScale;
        private bool isOpponentTurnEmphasisActive;

        /// <summary>
        /// 相手の番だけ、既存のズーム対象を位置を変えずに少しだけ大きくする。
        /// </summary>
        public void SetOpponentTurnEmphasis(bool active)
        {
            Transform target = zoomTarget != null ? zoomTarget : transform;
            if (target == null) return;

            if (active)
            {
                if (!hasOpponentTurnEmphasisBaseScale || opponentTurnEmphasisTarget != target)
                {
                    StopOpponentTurnEmphasisRoutine();
                    if (hasOpponentTurnEmphasisBaseScale && opponentTurnEmphasisTarget != null)
                    {
                        opponentTurnEmphasisTarget.localScale = opponentTurnEmphasisBaseScale;
                    }

                    opponentTurnEmphasisTarget = target;
                    opponentTurnEmphasisBaseScale = target.localScale;
                    hasOpponentTurnEmphasisBaseScale = true;
                }

                if (isOpponentTurnEmphasisActive) return;

                isOpponentTurnEmphasisActive = true;
                StartOpponentTurnEmphasisRoutine(target, opponentTurnEmphasisBaseScale * OpponentTurnScaleMultiplier);
                return;
            }

            if (!hasOpponentTurnEmphasisBaseScale || opponentTurnEmphasisTarget == null) return;

            isOpponentTurnEmphasisActive = false;
            StartOpponentTurnEmphasisRoutine(opponentTurnEmphasisTarget, opponentTurnEmphasisBaseScale);
        }

        private void StartOpponentTurnEmphasisRoutine(Transform target, Vector3 targetScale)
        {
            StopOpponentTurnEmphasisRoutine();

            if (!gameObject.activeInHierarchy)
            {
                target.localScale = targetScale;
                ClearOpponentTurnEmphasisBaseScaleIfRestored(targetScale);
                return;
            }

            opponentTurnEmphasisCoroutine = StartCoroutine(OpponentTurnEmphasisRoutine(target, targetScale));
        }

        private void StopOpponentTurnEmphasisRoutine()
        {
            if (opponentTurnEmphasisCoroutine == null) return;

            StopCoroutine(opponentTurnEmphasisCoroutine);
            opponentTurnEmphasisCoroutine = null;
        }

        private System.Collections.IEnumerator OpponentTurnEmphasisRoutine(Transform target, Vector3 targetScale)
        {
            Vector3 startScale = target.localScale;
            for (float elapsed = 0f; elapsed < OpponentTurnTransitionSeconds; elapsed += Time.unscaledDeltaTime)
            {
                if (target == null) yield break;

                float progress = Mathf.Clamp01(elapsed / OpponentTurnTransitionSeconds);
                float eased = progress * progress * (3f - 2f * progress);
                target.localScale = Vector3.Lerp(startScale, targetScale, eased);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = targetScale;
                ClearOpponentTurnEmphasisBaseScaleIfRestored(targetScale);
            }

            opponentTurnEmphasisCoroutine = null;
        }

        private void ClearOpponentTurnEmphasisBaseScaleIfRestored(Vector3 targetScale)
        {
            if (isOpponentTurnEmphasisActive || !hasOpponentTurnEmphasisBaseScale) return;
            if ((targetScale - opponentTurnEmphasisBaseScale).sqrMagnitude > 0.000001f) return;

            opponentTurnEmphasisTarget = null;
            hasOpponentTurnEmphasisBaseScale = false;
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
