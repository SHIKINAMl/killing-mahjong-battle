using System.Collections.Generic;

namespace KillingMahjong.UI
{
    /// <summary>
    /// ロンの清算パネルに出す内容。**表示のためだけの入れ物で、計算はしない。**
    ///
    /// 数字はすべてサーバーの `liquidation` から来たものをそのまま持つ。
    /// ここで掛け算や引き算をやり直すと、強襲のように獲得と損失が非対称になる仕様が入ったときに
    /// 嘘の式が出る（2026-08-07 に「5000 × 1 = 0」が実機で出た前例がある）。
    ///
    /// **「自分」「相手」はローカルプレイヤー基準。** 勝った側基準ではない。
    /// 組み立てるのは <c>GameUIPhaseController.BuildSettlementInfo</c>。
    /// </summary>
    public sealed class RonSettlementInfo
    {
        /// <summary>役1行ぶん。</summary>
        public sealed class YakuRow
        {
            /// <summary>画面に出す役名（`ドラ×3` のように枚数をまとめたもの。`+N` は含まない）。</summary>
            public string Name;

            /// <summary>強化で乗った翻。0 なら強化なし。</summary>
            public int Boost;

            /// <summary>この行の翻数（強化ぶんと枚数ぶんを含む）。</summary>
            public int Han;
        }

        public List<YakuRow> Rows = new List<YakuRow>();

        /// <summary>
        /// 役ごとの翻数を出してよいか。
        ///
        /// **翻数の正はサーバーの `han` だけ。** 行ごとの内訳はクライアントの表
        /// （`GameRules.GetBaseHan`）から引いており、サーバーの役表とずれる可能性がある。
        /// 行の合計がサーバーの `han` と一致したときだけ true にすること。
        /// **足して合わない数字を並べるくらいなら、合計だけ出す方がよい。**
        /// </summary>
        public bool ShowPerRowHan;

        public int TotalHan;
        public float Multiplier;
        public string RankName;

        /// <summary>流局の持ち越しを含めた局数。1 なら持ち越し無し。</summary>
        public int CarryRounds = 1;

        /// <summary>素点（持ち越し込み）。自分と相手で額が違う。</summary>
        public int MyBet;
        public int TheirBet;

        /// <summary>単騎待ちで支払いが倍になったか。**倍になるのは負けた側だけ。**</summary>
        public bool IsTankiWait;

        /// <summary>この局に強襲が乗ったか。乗ると勝者の獲得は 0 に潰れ、その分が敗者への追加ダメージへ回る。</summary>
        public bool AssaultApplied;
        public int AssaultBonusDamage;

        /// <summary>血の増減。増える側が正、減る側が負。</summary>
        public int MyDelta;
        public int TheirDelta;

        public int MyHpBefore;
        public int MyHpAfter;
        public int TheirHpBefore;
        public int TheirHpAfter;

        /// <summary>ローカルプレイヤーが勝った側か。</summary>
        public bool LocalWon;
    }
}
