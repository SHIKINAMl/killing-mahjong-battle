using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// リアクションの識別子。
    ///
    /// **必ず末尾に追加すること。** この値は `CharacterData` アセットに
    /// 整数（序数）で直列化されているため、途中に挿入すると既存アセットの
    /// 割り当てが黙って別のトリガーへ化ける。並べ替えも同じ理由で禁止。
    /// </summary>
    public enum ReactionTrigger
    {
        GameStart,
        Click,
        HandSelection,
        EnemyDiscard,
        PlayerDiscard,
        Win,
        Lose,
        Damage,

        // ---- ここから 2026-08-03 追加 ----
        //
        // 敵の手牌・待ちを知らないと判定できないものは意図的に入れていない。
        //
        // データ自体は届いている（`hand_selection_completed` と `status` が相手の手牌を配っている。
        // これは 2026-08-04 に「現状維持」と決めたので、今後も届き続ける）。
        // それでも入れていないのは、**反応そのものが答えを教えてしまう**ため。
        // 「危険牌に触れたら女の子が動揺する」を実装すると、
        // それは「その牌は当たり牌だ」と口で言っているのと変わらない。
        //
        // 必要になったら足せるが、対人戦の駆け引きが壊れることを承知のうえで判断すること。

        // ② ベッティング・心理戦
        Bet_BluffMax,
        Bet_TenpaiMax,
        Bet_HesitateMax,
        Bet_RevengeMax,
        Bet_TenpaiMin,
        Bet_NoTenMin,
        Bet_FidgetSpam,
        Bet_ZeroGiveUp,

        // ③ スキル発動・コスト消費
        Skill_PlayerClairvoyance,
        Skill_PlayerEnhance,
        Skill_PlayerSpecialWin,
        Skill_EnemyClairvoyance,
        Skill_HighCostPaid,
        Skill_NearDeathByCost,

        // ④ 打牌・河の状況
        Discard_RedDora,
        Discard_RawYakuhai,
        Discard_SafeTile,
        Discard_CenterTile,
        Discard_SameTileStreak,
        Discard_HonorStreak,

        // ⑥ 勝敗・被弾・瀕死
        Result_EnemyHitYakuman,
        Result_EnemyDoraBomb,
        Result_PlayerHitYakuman,
        Result_PlayerNearDeath,
        Result_EnemyKO,

        // ⑦ メタ操作・UIつつき・放置
        Meta_ClickHead,
        Meta_ClickChest,
        Meta_ClickSpam,
        Meta_ScreenClickSpam,
        Meta_Idle20s,
        Meta_Idle60s,
        Meta_MuteAudio,
        Meta_WindowRefocus,

        // ① 牌・手牌・山牌操作（自分の情報だけで判定できるものに限る）
        Tile_HoverDora,
        Tile_HoverHesitation,
        Tile_WallPoke,
        Tile_InstantDiscard,
        Tile_SpamClick,
        Tile_PeekHold,
        Tile_ThinkTimeout,
    }

    /// <summary>頭上に出す漫符の種類。表情が7枚しかないぶん、ここで感情を分ける。</summary>
    public enum ManfuType
    {
        None,
        Sweat,        // 冷や汗
        Anger,        // 青筋
        Exclamation,  // ！
        Question,     // ？
        Heart,        // ハート
        Blush,        // 赤面
    }

    [System.Serializable]
    public class CharacterReaction
    {
        public ReactionTrigger trigger;
        public string bodyExpressionId; // 追加: 表情に合わせた体のポーズ
        public string faceExpressionId; // faceSprites に登録したIDを指定

        [TextArea(2, 4)]
        public string dialogueText;

        [Header("追加要素（2026-08-03）")]
        [Tooltip("頭上に出す漫符。表情が7枚しかないので、感情の差はここで付ける")]
        public ManfuType manfuType = ManfuType.None;

        [Tooltip("表示時間(秒)。0 なら ReactionController の既定値を使う。" +
                 "既存データを 0 のままにしておけば従来どおりの長さで出る")]
        public float duration = 0f;

        [Tooltip("専用ボイス。未設定なら鳴らさない")]
        public AudioClip voiceClip;
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
