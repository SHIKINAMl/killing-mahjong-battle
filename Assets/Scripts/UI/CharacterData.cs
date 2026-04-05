using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// キャラクターごとの画像や基本情報を管理するScriptableObject
    /// Mahjong/CharacterData から作成可能
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Mahjong/CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Character Information")]
        public string characterName = "Unknown";

        [Header("Character Sprites")]
        public Sprite normalSprite;     // 通常時の画像
        public Sprite discardSprite;    // 打牌時の画像

        [Header("Reaction Sprites (Optional)")]
        public Sprite reactionSprite;   // 相手が打牌した時の反応画像など
        public Sprite winSprite;        // ロン・ツモ時の画像
        public Sprite damageSprite;     // ダメージを受けた時の画像
    }
}
