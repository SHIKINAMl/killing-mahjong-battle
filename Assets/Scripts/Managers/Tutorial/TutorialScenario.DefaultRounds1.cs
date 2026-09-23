using System;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Common;

namespace KillingMahjong.Managers
{
    // 既定シナリオの第1〜3局。BuildDefault から切り出したもの（2026-08-30）。
    // **中身は切り出す前と1行も変えていない。** 局ごとに読めるようにしただけ。
    public partial class TutorialScenario
    {
        /// <summary>第1局。ロンの基本（手順(1)〜(7)）。</summary>
        private static TutorialRoundData BuildRound1(List<int> hand, List<int> waits, int dora, int d1, int d2, int d3, int d4, Func<List<int>> Wall)
        {
            return new TutorialRoundData
            {
                label = "第1局 ロンの基本",
                wallBaseIds = Wall(),
                manganHandBaseIds = new List<int>(hand),
                waitBaseIds = new List<int>(waits),
                manganHandYaku = new List<string> { "混一色", "一気通貫" },
                manganHandHan = 5,
                doraBaseId = dora,

                allowManualHandSelection = true,   // ① 適当に13枚選ばせる
                rejectFirstConfirm = true,         // ② 必ず弾く
                requireAutoManganToConfirm = true, // ④ オートへ誘導

                betAmount = 2000, // 各自2000払い、満貫1.0倍で自分+2000 / 相手-2000
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, TutorialTiles.Man(8) },
                outcome = TutorialOutcome.PlayerRon,
                playerWinningTileBaseId = TutorialTiles.Man(8),
                yakuList = new List<string> { "混一色", "一気通貫" },
                formulaText = "5飜",
                rankText = "満貫",
                // **満貫ちょうどにしてある（2026-09-17 のユーザー指示）。**
                // 倍率1.0なので 自分 +2000×1.0=2000 / 相手 -2000。
                // 2000払って2000戻るだけ＝増減なし。
                // 決着のセリフ「満貫で勝っても、あなたは1滴も増えていないの」が
                // **そのまま事実になる。** 以前は清一色6飜＝跳満で +3000 入っていて、
                // セリフと食い違っていた。

                betPromptText = "",

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("「おっ後輩ちゃんじゃーん」"),
                    new TutorialLine("「先輩を助けに来たんだな？えらいぞー」"),
                    new TutorialLine("「ふんふん……鬼気迫ったメールにいてもたってもいられなくなった」"),
                    new TutorialLine("「……ってアタシのこと好きすぎかよー！！」"),
                    new TutorialLine("「……まっ後輩ちゃんが今からやるのはそんな生やさしいもんじゃないんだけどね」"),
                    new TutorialLine("「ラチられた時にチラッと見たかと思うけど、ここは賭場…ギャンブルをするとこなんだ」"),
                    new TutorialLine("「しかもただのギャンブルじゃない……それは命がけのギャンブル！！」"),
                    new TutorialLine("「なになに……入口で契約書を書かされた時から覚悟はしてた」"),
                    new TutorialLine("「……って契約書！？アタシはそんなの書かなかったけど」"),
                    new TutorialLine("「ビビらそうと思ってホラ吹いたけどそっちのほうが怖いな」"),
                    new TutorialLine("「まっ長くなっちゃったから早速ルール説明するわ」"),
                    new TutorialLine("「大丈夫！殺し合いとかしないからさ！何回かギャンブルできればアタシたちは脱出できるってワケ」"),
                    new TutorialLine("「一人でここにカチコんだから相手いなかったんだよね…」"),
                    new TutorialLine("「それじゃはじめるよ」"),   // ここで牌が配られる（盤面を出す）
                    new TutorialLine("「ここでは特殊な麻雀をプレイしてギャンブルをしていくよ」"),
                },
                onHandFilledLines = new List<TutorialLine>
                {
                    new TutorialLine("あーそれは違うね"),
                    new TutorialLine("まぁ難しいし、しゃあないか"),
                    new TutorialLine("じゃあ、コレ使ってよ"),
                    new TutorialLine("これは おまかせボタン"),
                    new TutorialLine("自動で山牌の中から満貫以上の形を作ってくれるよ"),
                },
                onSelfManganLines = new List<TutorialLine>
                {
                    new TutorialLine("おっきっちりできたね"),
                    new TutorialLine("さすがは後輩ちゃん 飲み込み早いねー"),
                    new TutorialLine("じゃあまぁいらないと思うけど"),
                },
                beforeBetLines = new List<TutorialLine>
                {
                    new TutorialLine("あっ！その前に賭け金の設定をしなきゃね"),
                    new TutorialLine("あとで説明するから今回は2000賭けてくれるかな"),
                },
                // 第1局の対局導入は TutorialManager.Progression の専用フローで表示する。
                onBattleStartLines = new List<TutorialLine>(),
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("ロン。あなたの上がりね。"),
                    new TutorialLine("獲得は『自分が賭けた額 × 役の倍率』。跳満なら1.5倍。"),
                    new TutorialLine("負けたほうは『自分が賭けた額 × 相手の倍率』を失う。"),
                    new TutorialLine("……それでね。満貫は、1倍なの。"),
                    new TutorialLine("勝っても、払った分が戻ってくるだけ。"),
                    new TutorialLine("気づいた？ 満貫で勝っても、あなたは1滴も増えていないの。"),
                    new TutorialLine("大きく賭けて、大きく獲る。それしか増える道はないわ。"),
                },
            };
        }

        /// <summary>第2局。流局（手順(8)〜(11)）。</summary>
        private static TutorialRoundData BuildRound2(List<int> hand, List<int> waits, int dora, List<int> drawDiscards, Func<List<int>> Wall)
        {
            return new TutorialRoundData
            {
                label = "第2局 流局",
                wallBaseIds = Wall(),
                manganHandBaseIds = new List<int>(hand),
                waitBaseIds = new List<int>(waits),
                doraBaseId = dora,

                allowManualHandSelection = false,
                rejectFirstConfirm = false,
                requireAutoManganToConfirm = true,

                // 流局ぶんは次局へ積み増されるので少額にしておく。
                // 第3局は単騎の2倍が効くため、ここを大きくすると自分が死ぬ。
                betAmount = 600,
                // 17手ぶん。うち最初の15手は自動で流し、残り2手をプレイヤーに打たせる。
                enemyDiscardBaseIds = new List<int>(drawDiscards),
                autoDiscardTurns = 15,
                outcome = TutorialOutcome.Draw,
                // 流局では血が動かない。賭け金は決着していないので次の局へ積み増される
                // （持ち越しぶんだけ、次に決着したときの増減が大きくなる）。
                drawDamageToPlayer = 0,

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次は、誰も死なない局を見せてあげる。"),
                    new TutorialLine("『自動』を押しなさい。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("しばらく黙って見ていなさい。勝手に進めるわ。"),
                },
                beforeManualDiscardLines = new List<TutorialLine>
                {
                    new TutorialLine("お互い17牌捨てたら、その局は流れる。"),
                    new TutorialLine("あと2回よ。好きなのを捨てなさい。"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("流局。誰も上がらないまま牌が尽きたわ。"),
                    new TutorialLine("誰も死ななかったわね。"),
                    new TutorialLine("それでも、抜かれた血は卓の上にあるのよ。"),
                    new TutorialLine("決着していないんだから、賭け金は誰のものにもならない。"),
                    new TutorialLine("だから次の局に積まれるの。"),
                    new TutorialLine("……いい？ 延ばした分だけ、次に決まるときが重くなるのよ。"),
                    new TutorialLine("流局は、助かったんじゃないわ。"),
                    new TutorialLine("支払いを、先に延ばしただけ。"),
                },
            };
        }

        /// <summary>第3局。嘘の待ち牌と単騎（手順(12)〜(17)）。</summary>
        private static TutorialRoundData BuildRound3(List<int> hand, List<int> waits, int dora, int d1, int d2, int d3, int d4, int d5, List<int> ankoMelds, Func<List<int>> Wall)
        {
            return new TutorialRoundData
            {
                label = "第3局 フェイクと単騎",
                wallBaseIds = Wall(),
                manganHandBaseIds = new List<int>(hand),
                waitBaseIds = new List<int>(waits),
                doraBaseId = dora,

                allowManualHandSelection = false,
                rejectFirstConfirm = false,
                requireAutoManganToConfirm = true,

                betAmount = 1000,
                enemyDiscardBaseIds = new List<int> { d1, d2, d3, d4, d5 },
                lockedTileBaseId = TutorialTiles.Ton, // ⑭ 嘘の待ち牌。触れないようにする
                lockedTileMessage = "その牌は出しちゃダメって言ったでしょう！",

                outcome = TutorialOutcome.EnemyRon,
                enemyRonMeldBaseIds = ankoMelds,
                enemyRonOnPlayerDiscardTurn = 5,
                yakuList = new List<string> { "四暗刻" },
                formulaText = "役満",
                rankText = "役満",
                enemyWinningHan = 13, // 役満 = 4倍
                isTankiWin = true,    // 単騎で上がられるので自分の失う額は2倍
                // 第2局の流局で決着しなかった賭け金が積み増されているため、
                // 双方の賭け金は各1200。相手 +1200×4=4800 / 自分 -1200×4×2=9600

                introLines = new List<TutorialLine>
                {
                    new TutorialLine("次の局よ。『自動』を押して。"),
                },
                inheritedBetLines = new List<TutorialLine>
                {
                    new TutorialLine("前の局は流れたわね。賭けたぶんは、まだ卓の上にあるの。"),
                    new TutorialLine("今回は{0}円が自動で積まれて、場には合計{1}円。改めて賭ける必要はないわ。"),
                    new TutorialLine("誰も勝っていないのに、卓の上の血だけが増えていくのよ。"),
                    new TutorialLine("積まれた分だけ、次に上がったほうの取り分が大きくなる。"),
                },
                onBattleStartLines = new List<TutorialLine>
                {
                    new TutorialLine("……ふふ。正直に言うとね、ちょっと怖いの。"),
                    new TutorialLine("このまま流局を続けていたいくらい。"),
                    new TutorialLine("だから教えてあげる。私の待ちは東よ。"),
                    new TutorialLine("東だけは絶対に出さないでね。"),
                    new TutorialLine("……ほら。ちゃんと目を見て言ったでしょう？"),
                },
                outroLines = new List<TutorialLine>
                {
                    new TutorialLine("ロン。"),
                    new TutorialLine("……ふふっ、信じたの？"),
                    new TutorialLine("待ちが東だなんて、本当だとは一度も言っていないわ。"),
                    new TutorialLine("さっき言ったわよね。自分で決めて払った、って。"),
                    new TutorialLine("違うわ。決めさせられていたの。"),
                    new TutorialLine("単騎待ちは、たった1枚を待つ代わりに――相手の失う額が2倍になるの。"),
                    new TutorialLine("役満は4倍。その2倍だから、積まれた血の8倍ね。"),
                    new TutorialLine("全部いただくわ。"),
                    new TutorialLine("あら、契約書に垂れちゃった。"),
                    new TutorialLine("署名の隣に、点がひとつ増えただけね。"),
                },
            };
        }
    }
}
