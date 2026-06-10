using UnityEngine;
using System.Collections;
using System;

namespace KillingMahjong.Visuals
{
    [RequireComponent(typeof(Camera))]
    public class CameraZoomController : MonoBehaviour
    {
        private Camera targetCamera;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float originalFOV;

        private Coroutine zoomCoroutine;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            SaveOriginalState();
        }

        public void SaveOriginalState()
        {
            originalPosition = transform.position;
            originalRotation = transform.rotation;
            originalFOV = targetCamera.fieldOfView;
        }

        public IEnumerator ZoomToTargetRoutine(Transform target, float zoomFactor = 0.5f, float duration = 0.5f)
        {
            if (targetCamera == null || target == null) yield break;

            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

            // ターゲットの少し手前・少し上にカメラを移動させる計算
            Vector3 directionToTarget = (target.position - originalPosition).normalized;
            // ターゲットに少し近づける（zoomFactorが0.5なら、距離の半分まで近づく）
            Vector3 targetPosition = Vector3.Lerp(originalPosition, target.position, zoomFactor);
            // 少しだけ上から見下ろす感じにするなど調整可能
            targetPosition.y += 0.5f;

            Quaternion targetRotation = Quaternion.LookRotation(target.position - targetPosition);
            float targetFOV = originalFOV * 0.7f; // ズームインするのでFOVも少し狭める

            yield return StartCoroutine(AnimateCamera(targetPosition, targetRotation, targetFOV, duration));
        }

        public IEnumerator ResetZoomRoutine(float duration = 0.4f)
        {
            if (targetCamera == null) yield break;
            if (zoomCoroutine != null) StopCoroutine(zoomCoroutine);

            yield return StartCoroutine(AnimateCamera(originalPosition, originalRotation, originalFOV, duration));
        }

        private IEnumerator AnimateCamera(Vector3 endPos, Quaternion endRot, float endFOV, float duration)
        {
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            float startFOV = targetCamera.fieldOfView;

            float t = 0;
            while (t < duration)
            {
                float progress = t / duration;
                // スムーズなイーズイン・アウト
                float eased = progress * progress * (3f - 2f * progress);

                transform.position = Vector3.Lerp(startPos, endPos, eased);
                transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
                targetCamera.fieldOfView = Mathf.Lerp(startFOV, endFOV, eased);

                t += Time.deltaTime;
                yield return null;
            }

            transform.position = endPos;
            transform.rotation = endRot;
            targetCamera.fieldOfView = endFOV;
        }
    }
}
