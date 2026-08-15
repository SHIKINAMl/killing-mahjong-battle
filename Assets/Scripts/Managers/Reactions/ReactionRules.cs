using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.UI;

namespace KillingMahjong.Managers.Reactions
{
    /// <summary>
    /// 反応を出すきっかけ。**ここだけはコードで決める。**
    ///
    /// プランナーが自由に足せるのは「条件」と「セリフ」で、
    /// 「いつゲームがこちらに声をかけるか」はゲーム側の都合なので data では決められない。
    /// そのかわり数を絞ってあり、たいていの新しい状況は
    /// **既にあるイベント＋条件の組み合わせ**で表現できるようにしてある。
    ///
    /// **必ず末尾に追加すること。** `ReactionTrigger` と同じく序数で直列化される。
    /// </summary>
    public enum ReactionEvent
    {
        RoundStart,        // 局が始まった
        BetConfirmed,      // 賭け金が確定した
        HandConfirmed,     // 手牌が確定した
        Discard,           // 牌が切られた
        Agari,             // 和了した
        Draw,              // 流局した
        MatchEnd,          // 対局が終わった
        SkillCast,         // スキルが発動した
        CharacterClick,    // 女の子がクリックされた
        PlayerActivity,    // 盤面と関係ないプレイヤーの操作（放置・連打・ミュートなど）
    }

    /// <summary>条件の比べ方</summary>
    public enum CompareOp
    {
        Equal,
        NotEqual,
        GreaterOrEqual,
        LessOrEqual,
        Greater,
        Less,
    }

    /// <summary>そのルールを何回まで出すか</summary>
    public enum FireLimit
    {
        /// <summary>制限なし（クールダウンだけ効く）</summary>
        None,
        /// <summary>1局に1回</summary>
        OncePerRound,
        /// <summary>1対局に1回</summary>
        OncePerMatch,
    }

    /// <summary>変数の型。エディタが入力欄の見た目を変えるのに使う</summary>
    public enum VarKind
    {
        Number,
        Bool,
        Text,
    }

    /// <summary>
    /// 条件ひとつ。「賭け金 が 5000 以上」のような1行。
    /// `key` は <see cref="ReactionVariableCatalog"/> に載っている変数名。
    /// </summary>
    [Serializable]
    public class ReactionCondition
    {
        public string key = "";
        public CompareOp op = CompareOp.Equal;

        /// <summary>Number と Bool（0/1）で使う</summary>
        public float number;

        /// <summary>Text で使う</summary>
        public string text = "";
    }

    /// <summary>
    /// セリフ1本。`CharacterReaction` と同じ形にしてあるので、
    /// 既存のリアクションからそのまま書き写せる。
    /// </summary>
    [Serializable]
    public class ReactionRuleLine
    {
        [TextArea(2, 4)]
        public string text = "";
        public string faceId = "";
        public ManfuType manfu = ManfuType.None;
    }

    /// <summary>
    /// プランナーが1つ作る「状況」。
    ///
    /// **同じイベントのルールは上から順に見て、最初に条件を満たした1件だけが出る。**
    /// これは既存の `CheckDiscardConditions` などと同じ考え方で、
    /// 「優先したいものを上に置く」だけで済むようにしている。
    /// 点数付けで自動的に選ぶ方式は、なぜそれが選ばれたのか分からなくなるので採らない。
    /// </summary>
    [Serializable]
    public class ReactionRule
    {
        [Tooltip("一覧に出る名前。動作には影響しないので分かりやすく")]
        public string label = "新しい状況";

        [Tooltip("外すと、消さずに止められる")]
        public bool enabled = true;

        public ReactionEvent trigger = ReactionEvent.Discard;

        [Tooltip("すべて満たしたときだけ出る。空なら「そのイベントなら必ず」")]
        public List<ReactionCondition> conditions = new List<ReactionCondition>();

        [Tooltip("Progress=必ず出す / Situation=同じものが並んでいたら捨てる / Ambient=演出中は捨てる")]
        public ReactionPriority priority = ReactionPriority.Situation;

        [Tooltip("このルールを再び出せるまでの秒数。0 なら制限なし")]
        public float cooldownSeconds = 0f;

        public FireLimit limit = FireLimit.None;

        [Tooltip("複数書くとランダムに1つ選ばれる")]
        public List<ReactionRuleLine> lines = new List<ReactionRuleLine>();

        /// <summary>実行時の発火記録。アセットには保存しない</summary>
        [NonSerialized] public float lastFiredAt = -9999f;
        [NonSerialized] public int firedInRound;
        [NonSerialized] public int firedInMatch;
    }

    /// <summary>
    /// プランナーが編集する反応ルールの束。
    ///
    /// **`Resources` に置く。** 対局シーンが2つある（`UIテストシーン` と `OpeningScene`）ので、
    /// シーンの参照で持たせると片方だけ設定し忘れる。`Resources.Load` なら両方から同じものを見る。
    /// </summary>
    [CreateAssetMenu(fileName = "ReactionRules", menuName = "Mahjong/反応ルール")]
    public class ReactionRuleSet : ScriptableObject
    {
        /// <summary>`Resources.Load` で引くときの名前（拡張子なし）</summary>
        public const string ResourcePath = "Reactions/ReactionRules";

        public List<ReactionRule> rules = new List<ReactionRule>();

        private static ReactionRuleSet _cached;
        private static bool _lookedUp;

        /// <summary>
        /// 実行時に読む。**無くても動く**（従来のトリガーと CSV がそのまま使われる）ので、
        /// 見つからなくてもエラーにはしない。
        /// </summary>
        public static ReactionRuleSet Load()
        {
            if (_lookedUp) return _cached;
            _lookedUp = true;
            _cached = Resources.Load<ReactionRuleSet>(ResourcePath);
            return _cached;
        }

        /// <summary>エディタで作り直したときに読み直させる</summary>
        public static void ClearCache()
        {
            _cached = null;
            _lookedUp = false;
        }
    }

    /// <summary>
    /// イベントと一緒に渡される「いまの状況」。
    ///
    /// 条件はこの中の値と比べる。**呼び出し側は分かる値を全部入れてよい**。
    /// 使われなかった値は無視されるだけで、害はない。
    /// </summary>
    public class ReactionContext
    {
        private readonly Dictionary<string, float> _numbers = new Dictionary<string, float>();
        private readonly Dictionary<string, string> _texts = new Dictionary<string, string>();

        public ReactionContext Set(string key, float value)
        {
            _numbers[key] = value;
            return this;
        }

        public ReactionContext Set(string key, bool value)
        {
            _numbers[key] = value ? 1f : 0f;
            return this;
        }

        public ReactionContext Set(string key, string value)
        {
            _texts[key] = value ?? "";
            return this;
        }

        public bool TryGetNumber(string key, out float value)
        {
            return _numbers.TryGetValue(key, out value);
        }

        public bool TryGetText(string key, out string value)
        {
            return _texts.TryGetValue(key, out value);
        }

        public void Clear()
        {
            _numbers.Clear();
            _texts.Clear();
        }

        /// <summary>
        /// どのイベントでも渡す共通の値。
        /// **「盤面と関係ないことを知っている」演出の材料がここに入っている**
        /// （通算成績・前回からの日数・時刻）。
        /// </summary>
        public ReactionContext WithCommon()
        {
            var board = BoardStateManager.Instance;
            if (board != null)
            {
                Set(ReactionVars.MyHp, board.LocalPlayerHp);
                Set(ReactionVars.EnemyHp, board.EnemyPlayerHp);
                Set(ReactionVars.HpDiff, board.LocalPlayerHp - board.EnemyPlayerHp);
            }

            // 局番号は `ReactionController` が持っている。共通の値として全イベントに配る
            // （ここで配らないと、カタログに載っていても値が届かない条件になる）
            if (ReactionController.Instance != null)
                Set(ReactionVars.RoundNumber, ReactionController.Instance.CurrentRound);

            Set(ReactionVars.TotalWins, KillingMahjong.Core.PlayerStatsManager.Wins);
            Set(ReactionVars.TotalLosses, KillingMahjong.Core.PlayerStatsManager.Losses);
            Set(ReactionVars.DaysSinceLastPlay, PlaySessionLog.DaysSinceLastPlay);
            Set(ReactionVars.HourOfDay, DateTime.Now.Hour);
            return this;
        }
    }

    /// <summary>
    /// 「前にいつ遊んだか」を覚えておくだけの記録。
    ///
    /// **これが無いと「久しぶりね」が言えない。** 対局の中の情報だけでは、
    /// プレイヤーが前回いつ来たかは絶対に分からない。
    /// `PlayerStatsManager` と同じく PlayerPrefs だけで完結し、サーバーには依存しない。
    /// </summary>
    public static class PlaySessionLog
    {
        private const string KeyLastPlay = "Reaction_LastPlayTicks";

        /// <summary>前回このゲームを起動した日からの日数。初回は 0</summary>
        public static int DaysSinceLastPlay { get; private set; }

        /// <summary>
        /// シーンの配置に頼らず、起動時に必ず1回だけ走らせる。
        /// タイトルから始めても対局シーンを直接再生しても、同じように記録が残る。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoStart()
        {
            MarkSessionStart();
        }

        /// <summary>起動時に1回だけ呼ぶ。読み取ってから今日の日付で上書きする</summary>
        public static void MarkSessionStart()
        {
            string saved = PlayerPrefs.GetString(KeyLastPlay, "");
            long ticks;
            if (!string.IsNullOrEmpty(saved) && long.TryParse(saved, out ticks))
            {
                var last = new DateTime(ticks);
                DaysSinceLastPlay = Mathf.Max(0, (int)(DateTime.Now.Date - last.Date).TotalDays);
            }
            else
            {
                DaysSinceLastPlay = 0;
            }

            PlayerPrefs.SetString(KeyLastPlay, DateTime.Now.Ticks.ToString());
            PlayerPrefs.Save();
        }
    }
}
