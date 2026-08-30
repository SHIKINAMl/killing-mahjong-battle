using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    // 台本アセットが未設定のときに使われる既定シナリオ。
    // 局ごとの中身は TutorialScenario.DefaultRounds1.cs / DefaultRounds2.cs。
    public partial class TutorialScenario
    {
        /// <summary>
        /// 台本アセットが未設定のときに使われる既定シナリオ。
        ///
        /// 血の流れ。
        ///
        /// 賭け金は**確定した時点で両者の血から引かれ**、場に積まれる（賭けた分は先に払う）。
        /// 決着したときの増減はこれとは別に `GameRules` の式で決まる:
        ///
        ///   勝者が得る額 = 勝者自身の賭け金 × 勝者の役の倍率
        ///   敗者が失う額 = 敗者自身の賭け金 × 勝者の役の倍率（単騎で上がられたら2倍）
        ///   倍率: 満貫1 / 跳満1.5 / 倍満2 / 三倍満3 / 役満4 / ダブル役満8
        ///
        /// 満貫（1倍）は払った賭け金と同額が戻るだけなので、勝っても差し引き0になる。
        /// 流局では決着せず、賭け金は次の局へ積み増される（＝次の決着の元手が増える）。
        /// 敵は第4局で能力を3つ使い、そのコストぶん自分の血を失う。
        ///
        ///   開始                                              P20000 / E20000
        ///   第1局 賭け金2000ずつ引かれる                      P18000 / E18000
        ///         自分ロン 清一色6飜=跳満1.5倍
        ///           自分 +3000 / 相手 -3000                   P21000 / E15000
        ///   第2局 賭け金600ずつ                               P20400 / E14400
        ///         流局（場はそのまま持ち越し）                P20400 / E14400
        ///   第3局 同額600が自動で引かれ、賭け金は各1200        P19800 / E13800
        ///         相手ロン 四暗刻単騎13飜=役満4倍・単騎
        ///           相手 +4800 / 自分 -9600                   P10200 / E18600
        ///   第4局 能力コスト -12700（手牌フェイズ）           P10200 / E 5900
        ///         賭け金1000ずつ                              P 9200 / E 4900
        ///         自分ロン 対々和+混一色5飜=満貫1倍
        ///           自分 +1000 / 相手 -1000                   P10200 / E 3900
        ///   第5局 賭け金1000ずつ                              P 9200 / E 2900
        ///         自分ロン 純正九蓮宝燈26飜=ダブル役満8倍
        ///           自分 +8000 / 相手 -8000（残2900で死亡）   P17200 / E    0（決着）
        ///
        /// 数値を触るときの制約:
        ///   - 第3局で自分が死なないこと（単騎の2倍が効くので損失が跳ね上がる）
        ///   - 第4局の前に相手が能力コスト12700を払えること
        ///   - 賭け金の支払いで誰も死なないこと（第4局・第5局の相手の残り血が薄い）
        ///   - 第4局のあとも相手が生き残り、第5局で死ぬこと
        ///   - 全局とも満貫以上（制約『満貫手以下での開始は不可』）
        /// 数値を触ったら必ず最後まで通して確認すること。
        /// </summary>
        public static TutorialScenario BuildDefault()
        {
            var s = CreateInstance<TutorialScenario>();
            s.playerStartHp = 20000;
            s.enemyStartHp = 20000;

            // --- 共通の配牌 ---
            // 手牌13枚: 一萬×3 二三四萬 五六七萬 八萬×2 九萬×2
            //   → 111m 234m 567m 88m 99m の清一色シャンポン待ち（8m / 9m）
            var hand = new List<int>
            {
                TutorialTiles.Man(1), TutorialTiles.Man(1), TutorialTiles.Man(1),
                TutorialTiles.Man(2), TutorialTiles.Man(3), TutorialTiles.Man(4),
                TutorialTiles.Man(5), TutorialTiles.Man(6), TutorialTiles.Man(7),
                TutorialTiles.Man(8), TutorialTiles.Man(8),
                TutorialTiles.Man(9), TutorialTiles.Man(9),
            };
            // 待ちは 8m / 9m のシャンポンに加えて 7m の三面待ち。
            //   7m: 11m雀頭 + 123m 456m 789m 789m
            //   8m: 111m 234m 567m 888m 99m
            //   9m: 111m 234m 567m 999m 88m
            // いずれの和了形も萬子のみなので清一色が成立する。
            var waits = new List<int> { TutorialTiles.Man(7), TutorialTiles.Man(8), TutorialTiles.Man(9) };

            // 残り21枚: 一筒〜九筒 / 一索〜九索 / 東×2 / 西
            var rest = new List<int>();
            for (int n = 1; n <= 9; n++) rest.Add(TutorialTiles.Pin(n));
            for (int n = 1; n <= 9; n++) rest.Add(TutorialTiles.Sou(n));
            rest.Add(TutorialTiles.Ton);
            rest.Add(TutorialTiles.Ton);
            rest.Add(TutorialTiles.Sha);

            List<int> Wall()
            {
                var w = new List<int>(hand);
                w.AddRange(rest);
                return w;
            }

            // --- 第4局（能力）専用の配牌 ---
            // 第4局は敵が能力に12700もの血を払った直後なので、跳満12000で上がると
            // 敵の血が尽きて第5局（決着）が成立しない。ここは満貫ちょうどに抑えたい。
            //
            // ただし制約『満貫手以下での開始は不可』があるため、安くしすぎてもいけない。
            // 清一色（門前6飜＝跳満）ではなく、ちょうど5飜＝満貫になる構成にする。
            //   111p 444p 777p 99p 東東 → 9p で和了すると
            //   111p 444p 777p 999p 東東 = 対々和(2飜) + 混一色(門前3飜) = 5飜 満貫
            // 待ちは 9p / 東 のシャンポン。
            var abilityHand = new List<int>
            {
                TutorialTiles.Pin(1), TutorialTiles.Pin(1), TutorialTiles.Pin(1),
                TutorialTiles.Pin(4), TutorialTiles.Pin(4), TutorialTiles.Pin(4),
                TutorialTiles.Pin(7), TutorialTiles.Pin(7), TutorialTiles.Pin(7),
                TutorialTiles.Pin(9), TutorialTiles.Pin(9),
                TutorialTiles.Ton, TutorialTiles.Ton,
            };

            var abilityWaits = new List<int> { TutorialTiles.Pin(9), TutorialTiles.Ton };
            int abilityWinningTile = TutorialTiles.Pin(9);

            // 残り21枚。手牌の待ち（9p / 東）は1枚も含めないこと。
            // 含めるとプレイヤーが自分の待ちを打ててしまい、フリテンの説明が必要になる。
            List<int> AbilityWall()
            {
                var w = new List<int>(abilityHand);
                for (int n = 1; n <= 9; n++) w.Add(TutorialTiles.Man(n));
                for (int n = 1; n <= 9; n++) w.Add(TutorialTiles.Sou(n));
                w.Add(TutorialTiles.Sha);
                w.Add(TutorialTiles.Pin(2));
                w.Add(TutorialTiles.Pin(5)); // ドラ表示と同じ牌。手牌には入らないので打点は動かない
                return w;
            }

            // --- 第5局（決着）専用の配牌 ---
            // 決着局が第1局とまったく同じ「清一色 6飜 跳満 12000」だと、
            // 大逆転のはずの最後の一撃が開幕と同じ勝ち方にしか見えない。
            // そこで決着局だけ役を跳ね上げる。
            //
            // 1112345678999m は九面待ち（1〜9萬のどれでも和了）なので、
            // どの萬子で和了しても純正九蓮宝燈が成立する。
            var finalHand = new List<int>
            {
                TutorialTiles.Man(1), TutorialTiles.Man(1), TutorialTiles.Man(1),
                TutorialTiles.Man(2), TutorialTiles.Man(3), TutorialTiles.Man(4),
                TutorialTiles.Man(5), TutorialTiles.Man(6), TutorialTiles.Man(7),
                TutorialTiles.Man(8),
                TutorialTiles.Man(9), TutorialTiles.Man(9), TutorialTiles.Man(9),
            };

            var finalWaits = new List<int>();
            for (int n = 1; n <= 9; n++) finalWaits.Add(TutorialTiles.Man(n));

            List<int> FinalWall()
            {
                var w = new List<int>(finalHand);
                w.AddRange(rest);
                return w;
            }

            // アタリ牌は真ん中の五萬。プレイヤーが打てるのは山の残り21枚（筒子・索子・字牌）
            // なので、萬子を敵が捨てても牌が5枚目になることはない。
            int finalWinningTile = TutorialTiles.Man(5);

            int dora = TutorialTiles.Pin(5);

            // 敵の捨て牌に使う無難な牌（プレイヤーの待ち 8m/9m を含まない）
            int d1 = TutorialTiles.Ton;
            int d2 = TutorialTiles.Pin(1);
            int d3 = TutorialTiles.Sou(1);
            int d4 = TutorialTiles.Pin(9);
            int d5 = TutorialTiles.Sou(9);

            // 第2局（流局の説明）用: 17手ぶんの敵の捨て牌。
            // プレイヤーの待ち 7m/8m/9m を含まないよう筒子・索子だけで組む。
            var drawDiscards = new List<int>();
            for (int n = 1; n <= 9; n++) drawDiscards.Add(TutorialTiles.Pin(n));
            for (int n = 1; n <= 8; n++) drawDiscards.Add(TutorialTiles.Sou(n));

            // 敵の役満手（単騎待ち・面子部分12枚）: 222m 333m 444m 555m
            //
            // 単騎のアタリ牌は「プレイヤーが実際に打った牌」になるため、どの牌で放銃しても
            // 破綻しない構成にする必要がある。プレイヤーが打てるのは山牌の残り21枚
            // （筒子・索子・字牌）なので、面子側を萬子だけで固めれば牌が5枚目にならない。
            //   例: 3p で放銃 → 222m 333m 444m 555m + 3p3p = 四暗刻単騎
            var ankoMelds = new List<int>
            {
                TutorialTiles.Man(2), TutorialTiles.Man(2), TutorialTiles.Man(2),
                TutorialTiles.Man(3), TutorialTiles.Man(3), TutorialTiles.Man(3),
                TutorialTiles.Man(4), TutorialTiles.Man(4), TutorialTiles.Man(4),
                TutorialTiles.Man(5), TutorialTiles.Man(5), TutorialTiles.Man(5),
            };

            // ================= 第1局: ロンの基本（手順①〜⑦） =================
            s.rounds.Add(BuildRound1(hand, waits, dora, d1, d2, d3, d4, Wall));

            // ================= 第2局: 流局（手順⑧〜⑪） =================
            s.rounds.Add(BuildRound2(hand, waits, dora, drawDiscards, Wall));

            // ================= 第3局: 嘘の待ち牌と単騎（手順⑫〜⑰） =================
            s.rounds.Add(BuildRound3(hand, waits, dora, d1, d2, d3, d4, d5, ankoMelds, Wall));

            // ================= 第4局: 能力（手順⑱〜⑳）※対局あり =================
            s.rounds.Add(BuildRound4(dora, abilityHand, abilityWaits, abilityWinningTile, AbilityWall));

            // ================= 第5局: 決着（手順㉑〜㉕） =================
            s.rounds.Add(BuildRound5(dora, d1, d2, d3, d4, finalHand, finalWaits, finalWinningTile, FinalWall));


            // 第1〜3局は同じ清一色の手牌を使うので、聴牌チェックの応答も共通にしておく
            foreach (var r in s.rounds)
            {
                r.manganHandYaku = new List<string> { "清一色" };
                r.manganHandHan = 6;

                // 開幕は女の子とセリフだけ。1行目を送ったら盤面を出す。
                r.revealBoardAfterLineIndex = 0;
            }

            // プレイヤーが自分で牌を選ぶ局（第1局・第5局）は、イントロを全て送り終えてから盤面を出す。
            // 途中で出すと「13枚選んで」のセリフを送る前に牌が触れてしまい、
            // 説明を読む前に盤面が進んでしまう。-1 = イントロを全て流し終えたあと。
            foreach (var r in s.rounds)
            {
                if (r.allowManualHandSelection) r.revealBoardAfterLineIndex = -1;
            }

            // 第4局と第5局は手牌も役も違うので、共通設定のあとで上書きする
            if (s.rounds.Count > 3)
            {
                s.rounds[3].manganHandYaku = new List<string> { "対々和", "混一色" };
                s.rounds[3].manganHandHan = 5;
            }
            if (s.rounds.Count > 4)
            {
                s.rounds[4].manganHandYaku = new List<string> { "純正九蓮宝燈" };
                s.rounds[4].manganHandHan = 26;
            }

            // 相手が倒れたあとの沈黙。これを送るとタイトルへ戻る。
            // 話者を既定にしているので吹き出しに 「…………」 と出る。
            // （以前あった先輩の締めセリフは、倒れた直後に出すと空気が壊れるので外した）
            s.endingLines = new List<TutorialLine>
            {
                new TutorialLine("…………"),
            };
            s.titleSceneName = "タイトルシーン";

            // **セリフだけ、外の表で差し替えられるようにしてある。**
            // `Assets/Resources/TutorialLines.asset` があれば、ID が一致した行の文字列で
            // 上書きする。表が無い／IDが無い行は、ここに書いた文字列がそのまま使われる。
            // 表の作り方は Tools/チュートリアル/台本TSVの書き出し・取り込み。
            //
            // 構造（局数・実演・ボタンの開放順）はここが正で、文字だけ外。
            // 台本ごとアセットへ移すと BuildDefault と二重管理になる（A-7 と同じ罠）。
            ApplyLineTable(s);

            return s;
        }
    }
}
