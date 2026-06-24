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
        public string bodyExpressionId; // 追加: 表情に合わせた体のポーズ
        public string faceExpressionId; // faceSprites に登録したIDを指定
        
        [TextArea(2, 4)]
        public string dialogueText;
    }

    [System.Serializable]
    public class NamedSprite
    {
        public string id;
        public Sprite sprite;
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

        [Header("Body System (のっぺらぼう)")]
        public System.Collections.Generic.List<NamedSprite> bodySprites = new System.Collections.Generic.List<NamedSprite>();
        public string defaultBodyId = "normal";

        [Header("Face System (表情)")]
        public System.Collections.Generic.List<NamedSprite> faceSprites = new System.Collections.Generic.List<NamedSprite>();
        public string defaultFaceId = "normal";

        [Header("Blink Animation (瞬き)")]
        public bool enableBlink = true;
        public float blinkIntervalMin = 2.0f;
        public float blinkIntervalMax = 5.0f;
        public string blinkFaceId = "blink";       // 完全に閉じた目

        [Header("Default Sprites (Old System - Fallback)")]
        public Sprite normalSprite;     // 通常時の画像
        public Sprite discardSprite;    // 通常の打牌時の画像（リアクションが無い場合のフォールバック）

        [Header("Reactions")]
        public System.Collections.Generic.List<CharacterReaction> reactions = new System.Collections.Generic.List<CharacterReaction>();
    }
}
