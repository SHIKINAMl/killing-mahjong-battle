using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        private IEnumerator RunDrawVideoExplanation()
        {
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("ここにはこの局で打った牌の数が示されてる"),
                new TutorialLine("ここでお互いが17牌ずつ打ち終わったらゲームは終了\n流局ってのが起きるよ"),
                new TutorialLine("後輩ちゃんは今後攻だから…"),
                new TutorialLine("後輩ちゃんが17ターン目にロンされなかったら流局\nってことかな"),
            });
            yield return PlaySceneBreakAndWait();
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("んで…流局が起こるとどうなるかというと……"),
                new TutorialLine("ちょっと長いし、アタシの昔の対戦動画があるからこれで見よっか"),
            });
            // 差し出すポーズの専用素材は未受領のため、現在の立ち絵を保持する。
            ClearGuide();
            var replay=TutorialDrawReplayUI.Create(transform);
            try { yield return replay.Play(); }
            finally { if(replay!=null) replay.Close(); }
        }

        // ---- 動画のあと（2026-10-15 のフロー図で足された） ----
        //
        //  「だいたい流局についてはわかったかな？」→ 分岐（わかった／むずかしい）
        //  → 流局説明用UI → 流局で賭け金を払えなくなったら負け → フラッシュ
        //  →「あとは自由に打ってみて？」→ プレイヤーが打つ → 先輩が一言ずつ返して打つ
        //  → フラッシュ →「さっきの動画の女の子は誰なのか」→ BGM止まる
        //  →「動画の続きを見てみる？」→ 分岐「対局動画の続きを見ますか？」
        //
        // 分岐と復帰は2026-10-17版に従う。いいえはDEADEND、はいは図に記載された一言まで。

        private const string DrawUnderstoodQuestion = "だいたい流局についてはわかったかな？";
        private const string DrawUnderstoodLabel = "わかった";
        private const string DrawDifficultLabel = "むずかしい";

        private static readonly List<TutorialLine> DrawUnderstoodLines = new List<TutorialLine>
        {
            new TutorialLine("おっ後輩ちゃんさすが"),
        };

        private static readonly List<TutorialLine> DrawDifficultLines = new List<TutorialLine>
        {
            new TutorialLine("まぁだいたい流局したら賭け金をまた払わされるって覚えとけばいいよ"),
        };

        private static readonly List<TutorialLine> DrawSummaryLeadLines = new List<TutorialLine>
        {
            new TutorialLine("じゃまとめだよー"),
        };

        /// <summary>
        /// 「流局説明用UI」の**仮の板**。ボルテージのときと同じく、絵が届くまで「ここに入ります」とだけ出す。
        /// 文面は企画の領分なので書かない。**AIで絵を描かないこと。**
        /// </summary>
        private const string DrawPanelTitle = "流局の説明";
        private static readonly string[] DrawPanelPlaceholder =
        {
            "（ここに流局説明用UIが入ります）",
            "画像が届いたら差し替えます",
        };

        /// <summary>流局説明用UIの絵。Resources 直下からの相対パス（拡張子なし）。まだ無いので null。</summary>
        private const string DrawPanelSpritePath = null;

        private static readonly List<TutorialLine> DrawCannotPayLines = new List<TutorialLine>
        {
            new TutorialLine("あっそうそう流局で持ち点を越える賭け金を支払わなくなった場合だけど"),
            new TutorialLine("流局で賭け金を支払えなくなった時点で負けだからね"),
            new TutorialLine("チョーシ乗ってバカみたいな額賭けると痛い目にあうよー"),
        };

        private static readonly List<TutorialLine> DrawFreePlayLines = new List<TutorialLine>
        {
            new TutorialLine("さっ！長い説明も終わり\nあとは自由に打ってみて？"),
        };

        private static readonly List<TutorialLine> DrawSafeLines = new List<TutorialLine>
        {
            new TutorialLine("その顔はセーフだねー"),
        };

        private static readonly List<TutorialLine> DrawWhoIsGirlLines = new List<TutorialLine>
        {
            new TutorialLine("……そういえばさっきの動画の女の子は誰なのかって"),
            new TutorialLine("…………………………"),
        };

        private const string WatchRestQuestion = "……気になるなら動画の続きを見てみる？";

        /// <summary>
        /// 「対局動画の続きを見ますか？」の答え。0=はい / 1=いいえ / -1=まだ聞いていない。
        /// いいえはDEADEND No1へ進み、復帰後に再び選ぶ。
        /// </summary>
        private int _watchRestAnswer = -1;

        /// <summary>
        /// 動画のあとの会話と、プレイヤー→先輩の1巡。**この巡の打牌はここで済ませる。**
        /// 呼んだ側は、この巡の敵とプレイヤーの打牌を飛ばすこと。
        /// </summary>
        private IEnumerator RunAfterDrawVideo(TutorialRoundData data, int turn, HashSet<int> seen)
        {
            var board = BoardStateManager.Instance;

            int understood = -1;
            yield return AskYesNo(DrawUnderstoodQuestion, a => understood = a, DrawUnderstoodLabel, DrawDifficultLabel);
            yield return PlayLines(understood == 0 ? DrawUnderstoodLines : DrawDifficultLines);
            yield return PlayLines(DrawSummaryLeadLines);

            // 流局説明用UI表示 → プレイヤーが閉じる
            if (dialogueUI != null) dialogueUI.HideText();
            yield return ShowRulePanelRoutine(BorrowJapaneseFont(), DrawPanelTitle, DrawPanelPlaceholder, DrawPanelSpritePath);

            yield return PlayLines(DrawCannotPayLines);
            FlashScreen();
            yield return new WaitForSeconds(0.35f);
            yield return PlayLines(DrawFreePlayLines);

            // プレイヤーが打つ
            if (board != null) board.SetLocalTurn(true);
            _isWaitingForDiscard = true;
            _lastPlayerDiscardBaseId = -1;
            yield return new WaitUntil(() => !_isWaitingForDiscard || _aborted);
            if (_aborted) yield break;
            if (_lastPlayerDiscardBaseId >= 0) seen.Add(_lastPlayerDiscardBaseId);
            yield return new WaitForSeconds(discardInterval);

            // X はプレイヤーが出した牌、次の X は先輩がこれから出す牌（図の注記）
            int enemyBase = PickEnemyDiscardForSecondBattle(data, turn, seen);
            yield return PlayLines(new List<TutorialLine>
            {
                new TutorialLine($"おっ{GetTileName(_lastPlayerDiscardBaseId)}ね\nぜーんぜんロンできねー"),
                new TutorialLine($"じゃアタシの美少女的判断でコレ{GetTileName(enemyBase)}GO"),
            });

            // 先輩が牌を打つ
            if (board != null) board.SetLocalTurn(false);
            DiscardEnemyTileInSecondBattle(data, enemyBase, seen);
            yield return new WaitForSeconds(discardInterval);

            yield return PlayLines(DrawSafeLines);
            yield return RunWatchRestBranch(data);
        }
    }
}
