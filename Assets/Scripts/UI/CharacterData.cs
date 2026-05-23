using UnityEngine;

namespace KillingMahjong.UI
{
    public enum ReactionTrigger
    {
        GameStart,
        Click,
        HandSelection,
        EnemyDiscard,
        PlayerDiscard,
        Win,
        Lose,
        Damage
    }

    [System.Serializable]
    public class CharacterReaction
    {
        public ReactionTrigger trigger;
        public Sprite faceSprite;
        
        [TextArea(2, 4)]
        public string dialogueText;
    }

    /// <summary>
    /// キャラクターごとの画像や基本情報を管理するScriptableObject
    /// Mahjong/CharacterData から作成可能
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Mahjong/CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Character Information")]
        public string characterName = "Unknown";

        [Header("Default Sprites")]
        public Sprite normalSprite;     // 通常時の画像
        public Sprite discardSprite;    // 通常の打牌時の画像（リアクションが無い場合のフォールバック）

        [Header("Reactions")]
        public System.Collections.Generic.List<CharacterReaction> reactions = new System.Collections.Generic.List<CharacterReaction>();
    }
}
