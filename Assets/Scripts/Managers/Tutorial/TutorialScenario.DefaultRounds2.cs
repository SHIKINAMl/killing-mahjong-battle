using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    // 既定シナリオの第4〜5局。BuildDefault から切り出したもの（2026-08-30）。
    // **中身は切り出す前と1行も変えていない。**
    public partial class TutorialScenario
    {
        /// <summary>第4局。能力（手順(18)〜(20)）。対局あり。</summary>
        private static TutorialRoundData BuildRound4(int dora, List<int> abilityHand, List<int> abilityWaits, int abilityWinningTile, Func<List<int>> AbilityWall)
        {
            return new TutorialRoundData
            {
                label = "第4局 能力",

                // この局だけ専用の配牌。理由は AbilityWall の定義を参照。
                wallBaseIds = AbilityWall(),
                manganHandBaseIds = new List<int>(abilityHand),
                waitBaseIds = new List<int>(abilityWaits),
                doraBaseId = dora,

                allowManualHandSelection = false,
                rejectFirstConfirm = false,
                requireAutoManganToConfirm = true,

                // 相手は直前に能力コスト12700を払っていて血が薄い。
                // 賭け金の支払いで相手が死ぬと第5局が成立しないので小さめにする。
                betAmount = 1000,

                // 2打目で放銃させる。1打目は待ちでない牌、2打目にプレイヤーの待ち(9p)を打たせる。
                enemyDiscardBaseIds = new List<int> { TutorialTiles.Man(1), abilityWinningTile },
                enemyUsesAbility = true, // ⑱

                outcome = TutorialOutcome.PlayerRon,
                playerWinningTileBaseId = abilityWinningTile,

                // 対々和(2飜) + 混一色(門前3飜) = 5飜 満貫 = 1倍。
                // 敵は直前に能力コスト12700を払っているので、ここで大きく削ると
                // 第5局（決着）が成立しなくなる。満貫の等倍がちょうどいい。
                yakuList = new List<string> { "対々和", "混一色" },
                formulaText = "5飜",
                rankText = "満貫",

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次は能力の話よ。『自動』で手牌を作りなさい。"),
                },

                // 能力は手牌フェイズでしか使えないので、手牌を決めたあと・賭け金の前に実演する
                abilityIntroLines = new List<TutorialLine>
                {
                    new TutorialLine("手牌が決まったわね。ここからが本番。"),
                    new TutorialLine("能力が使えるのはこの手牌フェイズの間だけ。打牌が始まったら、もう使えないわ。"),
                    new TutorialLine("見せてあげる。よく見ていなさい。"),
                },

                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("さあ、打ちましょう。"),
                },

                // 手順⑱: 3つの能力を順に実演する
                abilityShowcases = new List<TutorialAbilityShowcase>
                {
                    new TutorialAbilityShowcase(
                        SkillNames.Perspective,
                        new TutorialLine("まずは『透視』。あなたの牌を3枚、勝手に覗くの。"),
                        new TutorialLine("ほら、印がついたでしょう。その3枚は私に丸見えよ。")),

                    new TutorialAbilityShowcase(
                        SkillNames.Mulligan,
                        new TutorialLine("次は『牌交換』。要らない牌を山と入れ替えるわ。"),
                        new TutorialLine("これで私の手はずいぶん良くなった。")),

                    new TutorialAbilityShowcase(
                        SkillNames.BoostHand,
                        new TutorialLine("最後は『役強化』。決めた役の翻数を+1するの。"),
                        new TutorialLine("『清一色』を選んだわ。同じ手でも打点が跳ね上がる。"))
                    {
                        boostYakuName = "清一色",
                        boostHan = 1,
                    },
                },

                // 手順⑲: 能力そのものの説明
                abilityExplainLines = new List<TutorialLine>
                {
                    new TutorialLine("これが能力よ。使えば対局を一気にひっくり返せる。"),
                    new TutorialLine("代償はあなたの血。体力そのものよ。"),
                    new TutorialLine("私のゲージ、見てごらんなさい。"),
                    new TutorialLine("……さっきの半分も残っていないでしょう？"),
                    new TutorialLine("なあに、その顔。"),
                    new TutorialLine("強くなるのに何も払わない方法があると思った？"),
                    new TutorialLine("使えるのは手牌フェイズの間だけ。使うなら今よ。"),
                    new TutorialLine("能力は画面の『能力』ボタンから確認できるわ。"),
                },

                // 手順⑳: 能力強化の説明 → 役一覧へ誘導
                enhanceExplainLines = new List<TutorialLine>
                {
                    new TutorialLine("それと、能力そのものを強化することもできるの。"),
                    new TutorialLine("『役強化』で積んだ翻数は、その役にずっと乗り続けるわ。"),
                    new TutorialLine("どの役がどれだけ育っているかは、役一覧で見なさい。"),
                },
                guideToYakuList = true,
                onYakuListOpenedLines = new List<TutorialLine>
                {
                    new TutorialLine("これが役一覧よ。役ごとの翻数と、強化された分が並んでいるわ。"),
                    new TutorialLine("さっき私が強化した『清一色』も乗っているでしょう？"),
                    new TutorialLine("狙う役を決めるときは、ここを見ること。"),
                },

                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("……ロン？"),
                    new TutorialLine("うそ。あれだけ払って、こっちが振り込むの？"),
                    new TutorialLine("能力を使っても、放銃すれば意味がない。血だけ捨てたのと同じね。"),
                    new TutorialLine("……ねえ、気づいてる？"),
                    new TutorialLine("私のゲージ、あなたより短いのよ。"),
                    new TutorialLine("……次で最後にしましょう。"),
                },
            };
        }

        /// <summary>第5局。決着（手順(21)〜(25)）。</summary>
        private static TutorialRoundData BuildRound5(int dora, int d1, int d2, int d3, int d4, List<int> finalHand, List<int> finalWaits, int finalWinningTile, Func<List<int>> FinalWall)
        {
            return new TutorialRoundData
            {
                label = "第5局 決着",
                wallBaseIds = FinalWall(),
                manganHandBaseIds = new List<int>(finalHand),
                waitBaseIds = new List<int>(finalWaits),
                doraBaseId = dora,

                allowManualHandSelection = true,   // ㉑ 自分で組ませる
                rejectFirstConfirm = false,

                // 自力で組んで『決定』でも、『自動』に任せてもよい。矢印の誘導はしない。
                // requireAutoManganToConfirm は残す＝制約『満貫手以下での開始は不可』の担保。
                // 自力で台本の手を組めていれば決定を押した時点で通る（自動を押す必要はない）。
                freeHandBuilding = true,
                requireAutoManganToConfirm = true,

                // 相手の残りは2900程度。賭け金の支払いで死なせないこと。
                // ダブル役満8倍なので 1000 賭けても 8000 動き、決着には十分。
                betAmount = 1000,
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, finalWinningTile },
                outcome = TutorialOutcome.PlayerRon,
                playerWinningTileBaseId = finalWinningTile,

                // 純正九蓮宝燈は 26飜 = ダブル役満 = 8倍。
                // 1000 × 8 = 8000 で、残り2900の相手を倒し切る
                yakuList = new List<string> { "純正九蓮宝燈" },
                formulaText = "26飜",
                rankText = "ダブル役満",

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("最後の局よ。"),
                    new TutorialLine("今度は自分で組みなさい。13枚、自分の手で選んで『決定』を押すの。"),
                    new TutorialLine("……どうしても組めないなら『自動』に頼ってもいいわ。好きになさい。"),
                },
                // 第4局は自分ロンで決着しているので流局の持ち越しは起きない。
                // inheritedBetLines は使われないため置いていない。
                onHandFilledLines = new List<TutorialLine>
                {
                    new TutorialLine("13枚そろったわね。その手で本当にいいの？"),
                    new TutorialLine("決めたなら『決定』を。迷うなら『自動』を。"),
                },
                // 決定して打牌フェイズに入った直後。命の賭け合いだと分からせる
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("手が決まった。もう引き返せないわ。"),
                    new TutorialLine("ここから先に賭かっているのは、お金じゃない。"),
                    new TutorialLine("一枚打つたびに、どちらかが減るのよ。"),
                    new TutorialLine("……ねえ。"),
                    new TutorialLine("ひとつだけ教えてあげる。"),
                    new TutorialLine("私も、あなたと同じだけ抜かれているのよ。"),
                    new TutorialLine("さあ、始めましょう。震える手で選びなさい。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("……九蓮宝燈。"),
                    new TutorialLine("しかも九面待ちですって……！？"),
                    new TutorialLine("きゃあああ……！"),
                    new TutorialLine("……ねえ。"),
                    new TutorialLine("この血は、\"払った\" のかしら。"),
                    new TutorialLine("それとも、\"抜かれた\" のかしら……"),
                    new TutorialLine("決めるのは……あなたなのね。"),
                },
            };
        }
    }
}
