using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;

namespace KillingMahjong.UI
{
    // 清算パネルの中身を、サーバーの精算結果から組み立てる。
    // **ここでは計算をしない。** 詳しくは BuildSettlementInfo の説明を読むこと。
    public partial class GameUIPhaseController
    {

        /// <summary>
        /// ロン演出に出す計算式を、サーバーの清算結果から組み立てる。
        ///
        /// **数値はサーバーが出したものだけを並べる。掛け算をやり直さないこと。**
        /// 以前は `winner_bet × multiplier` を自分で組んでいたが、強襲を撃った局は
        /// 獲得が 0 に潰されるため「5000 × 1 = 0」という嘘の式が出ていた（2026-08-07 に実機で確認）。
        ///
        /// 強襲の局は式の意味自体が変わる。獲得ではなく、相手への追加ダメージになるので
        /// そのまま伝える。`assault_applied` を先に見ること。
        ///
        /// 内訳が無いときは null を返す。演出側は式を伏せて額だけ見せる。
        /// </summary>
        /// <summary>
        /// 清算パネルの中身を、サーバーの精算結果から組み立てる。
        ///
        /// **計算はしない。サーバーが出した数字を並べ替えるだけ。**
        /// 唯一の例外が「役ごとの翻数」で、これはサーバーが内訳を送っていないので
        /// クライアントの表（`GameRules.GetBaseHan`）から引く。
        /// **引いた行の合計がサーバーの `han` と一致したときだけ表示する**（§ShowPerRowHan）。
        /// 足して合わない数字を並べるのは、出さないより悪い。
        ///
        /// サーバー側の対応する計算は `mahjong_engine/engine/game_engine.py:757-790`。
        ///   勝者 … `自分の賭け金 × 持ち越し局数 × 倍率`
        ///   敗者 … `相手の賭け金 × 持ち越し局数 × 倍率 ×（単騎なら2）＋ 強襲の上乗せ`
        /// **母数が別なので、両者の額は一致しない。** それを2列で見せるのがこのパネルの主目的。
        /// </summary>
        private static RonSettlementInfo BuildSettlementInfo(LiquidationData liq, bool isLocalWin,
            List<Common.YakuNameUtil.Entry> yakuSummary, string rankName,
            int prevLocalHp, int newLocalHp, int prevEnemyHp, int newEnemyHp)
        {
            if (liq == null) return null;

            var info = new RonSettlementInfo
            {
                RankName = rankName,
                TotalHan = liq.han,
                Multiplier = liq.multiplier,
                CarryRounds = liq.carry_over_draw_count + 1,
                IsTankiWait = liq.is_tanki_wait,
                AssaultApplied = liq.assault_applied,
                AssaultBonusDamage = liq.assault_bonus_damage,
                LocalWon = isLocalWin,

                // **「自分」「相手」はローカル基準。** 勝った側基準ではない
                MyBet = isLocalWin ? liq.winner_bet : liq.loser_bet,
                TheirBet = isLocalWin ? liq.loser_bet : liq.winner_bet,
                MyDelta = isLocalWin ? liq.winner_gain : -liq.loser_loss,
                TheirDelta = isLocalWin ? -liq.loser_loss : liq.winner_gain,

                MyHpBefore = prevLocalHp,
                MyHpAfter = newLocalHp,
                TheirHpBefore = prevEnemyHp,
                TheirHpAfter = newEnemyHp,
            };

            FillYakuRows(info, yakuSummary, liq.han);

            return info;
        }

        /// <summary>
        /// 役の行を積んで、行ごとの翻数を出してよいかを決める。
        ///
        /// **本編（サーバーの liquidation）とチュートリアル（台本）の両方から呼ぶ。**
        /// 2026-08-29 にチュートリアルへ内訳を出すとき、丸ごと写すと
        /// 「片方だけ直す事故」がまた起きるのでここへ寄せた。
        ///
        /// <paramref name="totalHan"/> は**正とみなす翻数**（本編ならサーバーの `han`、
        /// チュートリアルなら台本の翻数）。行の合計がこれと一致したときだけ内訳の翻数を出す。
        /// </summary>
        public static void FillYakuRows(RonSettlementInfo info, List<Common.YakuNameUtil.Entry> yakuSummary, int totalHan)
        {
            if (info == null || yakuSummary == null) return;

            // 行ごとの翻数。サーバーは重複も1要素ずつ足しているので、こちらも
            // 「(素の翻 + 強化) × 枚数」で数える（`game_engine.py:757-758` と同じ数え方）
            int rowSum = 0;
            bool allKnown = true;

            // 画面は 800x600 で、パネルは手牌と役帯の上に収まらないといけない。
            // 行が増えすぎると上へはみ出すので、あふれた分は1行にまとめる。
            // **役名そのものは直前の宣言で1つずつ読ませてあるので、ここで落ちても情報は失われない。**
            const int MaxRows = 5;
            int overflowHan = 0;
            int overflowCount = 0;

            foreach (var e in yakuSummary)
            {
                int baseHan = KillingMahjong.GameRules.GetBaseHan(e.BaseName);
                if (baseHan < 0) allKnown = false;

                int rowHan = (Mathf.Max(baseHan, 0) + e.Boost) * e.Count;
                rowSum += rowHan;

                // 最後の枠は「他N役」に使うので、あふれると分かった時点で畳む
                bool isLast = info.Rows.Count == MaxRows - 1;
                if (info.Rows.Count >= MaxRows || (isLast && yakuSummary.Count > MaxRows))
                {
                    overflowHan += rowHan;
                    overflowCount += e.Count;
                    continue;
                }

                info.Rows.Add(new RonSettlementInfo.YakuRow
                {
                    Name = Common.YakuNameUtil.ToDisplayText(new Common.YakuNameUtil.Entry
                    {
                        BaseName = e.BaseName,
                        Boost = 0,      // 強化は別の列に出すので、名前には混ぜない
                        Count = e.Count
                    }),
                    Boost = e.Boost,
                    Han = rowHan,
                });
            }

            if (overflowCount > 0)
            {
                info.Rows.Add(new RonSettlementInfo.YakuRow
                {
                    Name = $"他{overflowCount}役",
                    Boost = 0,
                    Han = overflowHan,
                });
            }

            info.ShowPerRowHan = allKnown && rowSum == totalHan;
            if (!info.ShowPerRowHan)
            {
                Debug.LogWarning($"[Ron] 役ごとの翻数を伏せた（合計 {rowSum} / 正 {totalHan}, 全役既知={allKnown}）");
            }
        }

        /// <summary>
        /// 精算倍率から役ランクの呼称を出す。
        ///
        /// **サーバーの `_get_liquidation_multiplier` / `_get_multiplier_label` と対で読むこと**
        /// （`mahjong_engine/engine/game_engine.py:646-675`）。あちらは翻数から倍率と呼称の
        /// 両方を出しているが、**呼称は精算のペイロードに入っていない**ので、こちらで倍率から戻す。
        ///
        /// **26飜以上（倍率 8.0）の枝が無かった。** 2026-08-27 の調査で見つかった不具合で、
        /// `>= 4.0` に吸われて「役満」と出る一方、式には「× 8」と出て**画面の中で食い違っていた**。
        /// `純正九蓮宝燈` で実際に到達する。ボイス `rank_double_yakuman.wav` も収録済みだったが、
        /// この文字列が生成されないため**一度も鳴っていなかった**。
        ///
        /// **この判定は表示のためだけのもの。** 勝敗も点数もサーバーが決めている。
        /// 以前は `ExecuteRonAction` と `HandleAgari` に同じ if 連鎖が丸ごと2つあり、
        /// 片方だけ直す事故が起きる形だったので1本にまとめてある。
        /// </summary>
        private static string ResolveRankName(float multiplier)
        {
            if (multiplier >= 8.0f) return "ダブル役満";
            if (multiplier >= 4.0f) return "役満";
            if (multiplier >= 3.0f) return "三倍満";
            if (multiplier >= 2.0f) return "倍満";
            if (multiplier >= 1.5f) return "跳満";
            return "満貫";
        }

        private static string BuildScoreFormula(LiquidationData liq)
        {
            if (liq == null) return null;

            if (liq.assault_applied)
            {
                // 得るはずだった額がそのまま相手への追加ダメージへ回る。
                // 獲得は 0 なので「= 0」を出さず、何が起きたかを文で見せる
                if (liq.assault_bonus_damage <= 0) return null;
                return $"強襲 → 相手へ {liq.assault_bonus_damage} の追加ダメージ";
            }

            if (liq.winner_bet <= 0 || liq.multiplier <= 0f) return null;

            // 1.0 は「1」、1.5 は「1.5」と出す。末尾の 0 を引きずらない
            string mult = liq.multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            // 答えもサーバーの winner_gain をそのまま置く。掛け算し直さない
            return $"{liq.winner_bet} × {mult} = {liq.winner_gain}";
        }
    }
}
