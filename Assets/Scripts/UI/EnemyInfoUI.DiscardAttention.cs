using System.Collections;
using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        private Coroutine discardAttention;
        private Sprite attentionOriginal, attentionApplied;

        public void PlayDiscardAttention(bool late)
        {
            CancelDiscardAttention();
            if (!isActiveAndEnabled || reactionCoroutine != null || DialogueUI.IsTalking
                || characterData == null || faceRenderer == null || faceRenderer.sprite != normalFaceSprite) return;
            string id = late ? "キメ顔" : characterData.blinkFaceId;
            if (!CharacterVisualUtil.TryFindFaceSprite(characterData, id, out var sprite) || sprite == normalFaceSprite) return;
            attentionOriginal = faceRenderer.sprite;
            attentionApplied = sprite;
            faceRenderer.sprite = sprite;
            discardAttention = StartCoroutine(DiscardAttentionRoutine(late ? .32f : .14f));
        }

        private IEnumerator DiscardAttentionRoutine(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
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
            if (faceRenderer != null && faceRenderer.sprite == attentionApplied && reactionCoroutine == null)
                faceRenderer.sprite = attentionOriginal;
            attentionApplied = attentionOriginal = null;
        }
    }
}
