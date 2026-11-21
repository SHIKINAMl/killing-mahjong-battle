using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.Managers.Reactions
{
    /// <summary>
    /// プランナーが編集する反応ルールの束。
    ///
    /// **`Resources` に置く。** 対局シーンが2つある（`UIテストシーン` と `OpeningScene`）ので、
    /// シーンの参照で持たせると片方だけ設定し忘れる。`Resources.Load` なら両方から同じものを見る。
    ///
    /// **このクラスはファイル名と同じ名前のファイルに置く**（2026-10-23）。ScriptableObject のアセットは、
    /// クラス名とファイル名が同じでないとスクリプトに結び付かない。以前は ReactionRules.cs の中にあり、
    /// アセット（Resources/Reactions/ReactionRules.asset）がスクリプト無しの扱いになっていた。
    /// 読み込むたびに「The referenced script on this Behaviour is missing!」が出て、ルールは一度も効いていなかった
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
}
