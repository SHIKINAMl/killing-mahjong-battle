using UnityEngine;

namespace KillingMahjong.UI
{
    public partial class EnemyInfoUI
    {
        // --- 仮の四角（2026-10-11 のユーザー指示）---
        //
        // CharacterData.showAsPlaceholder が立っているキャラクターは、立ち絵を出さず1色の四角にする。
        //
        // **立ち絵を差し替える所は何か所もある**（表情・ポーズ・打牌・まばたき・リアクション）。
        // その全部に分岐を足す代わりに、描く直前（LateUpdate）で四角へ置き換えている。
        // 元の絵の指定は覚えておくので、印を外したキャラクターへ替われば元に戻る。

        private bool _placeholderShown;
        private Sprite _placeholderSource;
        private Sprite _placeholderSquare;

        private void LateUpdate()
        {
            bool want = characterData != null && characterData.showAsPlaceholder && characterRenderer != null;
            if (!want)
            {
                if (_placeholderShown) RestoreFromPlaceholder();
                return;
            }

            // ほかの処理が立ち絵を入れ直していたら、その絵に合わせた四角へ置き換える
            Sprite current = characterRenderer.sprite;
            if (current != null && current != _placeholderSquare)
            {
                _placeholderSource = current;
                _placeholderSquare = CharacterPlaceholder.SquareFor(current);
            }
            if (_placeholderSquare != null) characterRenderer.sprite = _placeholderSquare;

            // 色だけ替える。透明度は、消える演出などがほかで動かしているので触らない
            Color tint = characterData.placeholderColor;
            tint.a = characterRenderer.color.a;
            characterRenderer.color = tint;

            if (faceRenderer != null) faceRenderer.enabled = false;
            _placeholderShown = true;
        }

        private void RestoreFromPlaceholder()
        {
            _placeholderShown = false;
            if (characterRenderer != null)
            {
                if (characterRenderer.sprite == _placeholderSquare && _placeholderSource != null)
                    characterRenderer.sprite = _placeholderSource;
                Color c = characterRenderer.color;
                characterRenderer.color = new Color(1f, 1f, 1f, c.a);
            }
            if (faceRenderer != null) faceRenderer.enabled = true;
            _placeholderSource = null;
            _placeholderSquare = null;
        }
    }
}
