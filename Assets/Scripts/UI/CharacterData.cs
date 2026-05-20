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
        public Sprite surprisedSprite;  // クリックでびっくりした時の画像
        public Sprite winSprite;        // ロン・ツモ時の画像
        public Sprite damageSprite;     // ダメージを受けた時の画像

        [Header("Dialogues")]
        [TextArea(2, 4)]
        public string introductionDialogue = "よろしくお願いします！"; // 登場時（初期表示時）のセリフ
        
        [TextArea(2, 4)]
        public string clickDialogue = "びっくりしたー"; // クリックされた時のリアクションセリフ
        
        [TextArea(2, 4)]
        public string winDialogue = "私の勝ちですね！"; // 勝利時のセリフ
        
        [TextArea(2, 4)]
        public string loseDialogue = "負けちゃった…"; // 敗北・ダメージ時のセリフ
    }
}
