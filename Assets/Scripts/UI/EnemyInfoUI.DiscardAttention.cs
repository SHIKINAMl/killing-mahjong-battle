using System.Collections;
using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        private Coroutine discardAttention;
        private Sprite attentionOriginal, attentionApplied;
        private Vector3 attentionBodyStart, attentionFaceStart;
        private bool attentionMovesFace, attentionMoved;

        public void PlayDiscardAttention(bool late)
        {
            CancelDiscardAttention();
            if (!isActiveAndEnabled || reactionCoroutine != null || bounceCoroutine != null || suppressBlink || DialogueUI.IsTalking
                || characterRenderer == null || !characterRenderer.enabled
                || characterData == null || faceRenderer == null || faceRenderer.sprite != normalFaceSprite) return;
            var slime = characterRenderer.GetComponent<SlimeBounceAnimator>();
            if (slime != null && slime.IsPlaying) return;
            string id = late ? "キメ顔" : characterData.blinkFaceId;
            if (CharacterVisualUtil.TryFindFaceSprite(characterData, id, out var sprite) && sprite != normalFaceSprite)
            {
                attentionOriginal = faceRenderer.sprite;
                attentionApplied = sprite;
                faceRenderer.sprite = sprite;
            }
            attentionBodyStart = characterRenderer.transform.localPosition;
            attentionFaceStart = faceRenderer.transform.localPosition;
            attentionMovesFace = faceRenderer.transform != characterRenderer.transform
                && !faceRenderer.transform.IsChildOf(characterRenderer.transform);
            attentionMoved = true;
            discardAttention = StartCoroutine(DiscardAttentionRoutine(late));
        }

        private IEnumerator DiscardAttentionRoutine(bool late)
        {
            float duration = late ? .42f : .34f;
            float height = characterRenderer.bounds.size.y;
            // 立ち絵の高さに比例した小さな反応。顔が別階層でも同じ距離だけ動かす。
            Vector3 displacement = new Vector3(-.009f, -.006f, 0f) * height * (late ? 1.2f : 1f);
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                if (DialogueUI.IsTalking || reactionCoroutine != null) break;
                float p = Mathf.Clamp01(PresentationTiming.Sample(t) / duration);
                float weight = Mathf.Sin(p * Mathf.PI);
                weight *= weight;
                var body = characterRenderer.transform;
                body.localPosition = attentionBodyStart + (body.parent != null
                    ? body.parent.InverseTransformVector(displacement * weight) : displacement * weight);
                if (attentionMovesFace)
                {
                    var face = faceRenderer.transform;
                    face.localPosition = attentionFaceStart + (face.parent != null
                        ? face.parent.InverseTransformVector(displacement * weight) : displacement * weight);
                }
                yield return null;
            }
            RestoreDiscardAttention();
            discardAttention = null;
        }

        public void CancelDiscardAttention()
        {
            if (discardAttention != null) StopCoroutine(discardAttention);
            discardAttention = null;
            RestoreDiscardAttention();
        }

        private void RestoreDiscardAttention()
        {
            if (attentionMoved)
            {
                if (characterRenderer != null) characterRenderer.transform.localPosition = attentionBodyStart;
                if (attentionMovesFace && faceRenderer != null) faceRenderer.transform.localPosition = attentionFaceStart;
                attentionMoved = false;
            }
            if (faceRenderer != null && faceRenderer.sprite == attentionApplied && reactionCoroutine == null)
                faceRenderer.sprite = attentionOriginal;
            attentionApplied = attentionOriginal = null;
        }
    }
}
