using UnityEngine;
using TMPro;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        /// <summary>
        /// そのトリガーに喋れるセリフが1本でもあるか。
        ///
        /// **`ReactionController` が「トリガーを試して、無ければ CSV に落とす」ために要る。**
        /// 実際に `PlayReaction` を呼んでみるまで分からないままだと、
        /// 空振りしたことに気づけず、CSV に書いてあるセリフまで出なくなる。
        /// </summary>
        public bool HasReaction(ReactionTrigger trigger)
        {
            if (characterData == null || characterData.reactions == null) return false;
            return characterData.reactions.Exists(
                r => r != null && r.trigger == trigger && !string.IsNullOrEmpty(r.dialogueText));
        }

        public string PlayReaction(ReactionTrigger trigger, float duration = 3.0f, string formatArg = "")
        {
            if (characterData == null || characterData.reactions == null || characterData.reactions.Count == 0)
                return null;

            // 条件に合うリアクションを全て抽出
            var matches = characterData.reactions.FindAll(r => r.trigger == trigger);
            if (matches.Count == 0) return null;

            // ランダムに1つ選ぶ
            var reaction = matches[Random.Range(0, matches.Count)];

            // 画像が設定されていれば一時的に変更
            if (!string.IsNullOrEmpty(reaction.faceExpressionId))
            {
                var match = characterData.faceSprites?.Find(x => x.id == reaction.faceExpressionId);
                var bodyMatch = characterData.bodySprites?.Find(x => x.id == reaction.bodyExpressionId);

                if ((match != null && match.sprite != null) || (bodyMatch != null && bodyMatch.sprite != null))
                {
                    if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
                    reactionCoroutine = StartCoroutine(TemporaryVisualRoutine(bodyMatch?.sprite, match?.sprite, duration));
                }
            }

            string text = reaction.dialogueText;
            Debug.Log($"[EnemyInfoUI] PlayReaction: trigger={trigger}, matchFound=true, dialogueText='{text}'");
            
            if (!string.IsNullOrEmpty(formatArg) && !string.IsNullOrEmpty(text))
            {
                text = string.Format(text, formatArg);
            }

            return string.IsNullOrEmpty(text) ? null : $"「{text}」";
        }

        private System.Collections.IEnumerator TemporaryVisualRoutine(Sprite newBody, Sprite newFace, float duration)
        {
            Sprite originalFace = (faceRenderer != null) ? normalFaceSprite : null;
            Sprite originalBody = normalSprite;

            if (faceRenderer != null && newFace != null)
                faceRenderer.sprite = newFace;
            
            if (characterRenderer != null && newBody != null)
                characterRenderer.sprite = newBody;

            yield return new WaitForSeconds(duration);

            if (faceRenderer != null && originalFace != null)
                faceRenderer.sprite = originalFace;
            
            if (characterRenderer != null && originalBody != null)
                characterRenderer.sprite = originalBody;
            
            reactionCoroutine = null;
        }

        public void PlayReactionWithVisual(Sprite newBody, Sprite newFace, float duration)
        {
            if (reactionCoroutine != null) StopCoroutine(reactionCoroutine);
            reactionCoroutine = StartCoroutine(TemporaryVisualRoutine(newBody, newFace, duration));
            PlayBounceAnimation(0.3f); // 0.3秒で一瞬跳ねる
        }

        public void PlayReactionWithVisualId(string bodyId, string faceId, float duration)
        {
            if ((string.IsNullOrEmpty(bodyId) && string.IsNullOrEmpty(faceId)) || characterData == null)
            {
                PlayBounceAnimation(0.3f);
                return;
            }
            
            Sprite newBody = null;
            if (!string.IsNullOrEmpty(bodyId))
            {
                var bodyMatch = characterData.bodySprites?.Find(x => x.id == bodyId);
                if (bodyMatch != null) newBody = bodyMatch.sprite;
            }

            Sprite newFace = null;
            if (!string.IsNullOrEmpty(faceId))
            {
                var faceMatch = characterData.faceSprites?.Find(x => x.id == faceId);
                if (faceMatch != null) newFace = faceMatch.sprite;
            }

            if (newBody != null || newFace != null)
            {
                PlayReactionWithVisual(newBody, newFace, duration);
            }
            else
            {
                PlayBounceAnimation(0.3f);
                Debug.LogWarning($"[EnemyInfoUI] Neither body '{bodyId}' nor face '{faceId}' found in CharacterData!");
            }
        }
    }
}
