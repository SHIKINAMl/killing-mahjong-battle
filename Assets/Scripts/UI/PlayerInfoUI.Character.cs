using UnityEngine;
using TMPro;
using KillingMahjong.Common;

namespace KillingMahjong.UI
{
    public partial class PlayerInfoUI
    {
        public void ReduceHp(int amount)
        {
            currentHp -= amount;
            SetHP(currentHp);
        }

        public void SetCharacterSprite(Sprite sprite)
        {
            // 旧互換として残す
            CharacterVisualUtil.ApplyIfPresent(characterRenderer, sprite);
        }

        public void SetBodyPose(string poseId)
        {
            if (characterData == null || characterRenderer == null) return;
            if (CharacterVisualUtil.TryFindBodySprite(characterData, poseId, out var sprite))
            {
                characterRenderer.sprite = sprite;
            }
        }

        public void SetFaceExpression(string expressionId)
        {
            if (characterData == null || faceRenderer == null) return;
            if (CharacterVisualUtil.TryFindFaceSprite(characterData, expressionId, out var sprite))
            {
                faceRenderer.sprite = sprite;
            }
        }

        /// <summary>
        /// 打牌する時の画像（あるいは元の画像）に切り替える
        /// </summary>
        public void SetDiscardingState(bool isDiscarding)
        {
            if (characterRenderer != null)
            {
                // 実行中に変更された場合も反映されるように、characterDataから直接読み取る
                Sprite target = CharacterVisualUtil.ResolveDiscardingSprite(characterData, discardSprite, normalSprite, isDiscarding);
                if (target != null)
                {
                    characterRenderer.sprite = target;
                }
            }
        }

        public void PlayBounceAnimation(float duration)
        {
            if (characterRenderer == null) return;
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            bounceCoroutine = StartCoroutine(BounceRoutine(duration));
        }

        private System.Collections.IEnumerator BounceRoutine(float duration)
        {
            float elapsed = 0f;
            float bounceSpeed = 15f;
            float bounceHeight = 0.5f; // SpriteRenderer用に調整 
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float yOffset = Mathf.Abs(Mathf.Sin(elapsed * bounceSpeed)) * bounceHeight;
                characterRenderer.transform.localPosition = originalPosition + new Vector3(0, yOffset, 0);
                yield return null;
            }
            
            characterRenderer.transform.localPosition = originalPosition;
            bounceCoroutine = null;
        }
    }
}
