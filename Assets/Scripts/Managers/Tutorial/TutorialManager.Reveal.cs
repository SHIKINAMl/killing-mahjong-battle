using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.EngineData;
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        private static readonly string[] RevealQuestions = {
            "おまえは誰だ", "先輩をどうした", "このゲームはなんだ",
        };

        private static readonly List<TutorialLine>[] RevealAnswers = {
            new List<TutorialLine> {
                new TutorialLine("ワタシ？\nワタシはゲーム好きの吸血鬼"),
                new TutorialLine("ゲームを通してこの地の人たちの魂を頂いているわ"),
                new TutorialLine("気軽にヴァンちゃん\nとでも呼んで？"),
            },
            new List<TutorialLine> {
                new TutorialLine("先輩？困ったことに試合の途中で死んじゃったみたいね"),
                new TutorialLine("だから代打として一番仲良さそうな君をここに呼び出してみたの"),
                new TutorialLine("ワタシの演技もなかなかだったでしょ？"),
            },
            new List<TutorialLine> {
                new TutorialLine("このゲーム？\nまぁ一種の儀式みたいなものね"),
                new TutorialLine("ゲームに負けた相手の魂を勝者が自由にできる……"),
                new TutorialLine("アナタの先輩はこれを止めようとしてたみたいだけど"),
                new TutorialLine("点の使いすぎで死んじゃった"),
            },
        };

        /// <summary>
        /// 正体を明かしたあとの怖い曲（Resources/Bgm）。codex がコードで合成したオリジナル曲。
        /// 聞き比べ A / C の実装例を見せて、2026-10-30 にプランナーが A に決めた
        /// </summary>
        private const string TutorialScaryBgm = "tut_scary_A";

        /// <summary>
        /// 負けて画面が真っ黒になってからの曲（Resources/Bgm）。同じく codex の合成曲の候補 C。
        /// 2026-10-30 プランナー「画面が暗くなったらCの曲に切り替えて」
        /// </summary>
        private const string TutorialRevivalBgm = "tut_scary_C";

        /// <summary>2026-10-18の図。「はい」から動画後半、3つの質問、指定された決着まで。</summary>
        private IEnumerator RunWatchRestContinuation(TutorialRoundData data)
        {
            if (dialogueUI != null) { dialogueUI.HideText(); dialogueUI.HideAdvanceOnAnyClick(); }
            // 動画を見せるあいだは、スマホを差し出す絵にする
            SetSeniorPose(SeniorPhonePose);
            var replay = TutorialDrawReplayUI.Create(transform, gameUIManager != null ? gameUIManager.EnemyInfoUI : null);
            try { yield return replay.PlayContinuation(); }
            finally { if (replay != null) replay.Close(); SetSeniorPose(SeniorNormalPose); }
            if (_aborted) yield break;

            yield return PlaySceneBreakAndWait();
            if (gameUIManager != null && gameUIManager.EnemyInfoUI != null)
                yield return gameUIManager.EnemyInfoUI.RevealTutorialCharacter();
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("ふふふ……\n先輩死んじゃったね？"),
            });
            // 「先輩死んじゃったね？」のあとから怖い曲を流す（2026-10-29 要望「できればここ怖げなBGMほしいかも」）
            if (AudioManager.Instance != null) AudioManager.Instance.PlayTutorialBgm(TutorialScaryBgm);
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("困ってる？困惑しているわよね？"),
                new TutorialLine("お前はだれ？とか先輩はどこ？とかこのゲームはなんだ？とか"),
                new TutorialLine("牌を打ったらワタシが教えてあげるわ"),
            });

            var asked = new HashSet<int>();
            var seen = new HashSet<int>();
            var board = BoardStateManager.Instance;
            if (board != null)
            {
                foreach (int id in board.DiscardedWallIndexes)
                    if (id >= 0 && id < board.OriginalWallTiles.Count)
                        seen.Add(TutorialTiles.BaseOf(board.OriginalWallTiles[id]));
            }
            try
            {
                while (asked.Count < RevealQuestions.Length && !_aborted)
                {
                    int answer = -1;
                    var parent = BuildIntroCanvas(dim: false);
                    int row = 0;
                    for (int i = 0; i < RevealQuestions.Length; i++)
                    {
                        if (asked.Contains(i)) continue;
                        int choice = i;
                        var button = CreateButton(parent, BorrowJapaneseFont(), RevealQuestions[i],
                            new Vector2(0f, -65f - row++ * 58f), () => answer = choice);
                        ((RectTransform)button.transform).sizeDelta = new Vector2(300f, 50f);
                    }
                    yield return new WaitUntil(() => answer >= 0 || _aborted);
                    CloseIntro();
                    if (_aborted) yield break;
                    asked.Add(answer);

                    // 選択肢のたびにプレイヤーが先に自動打牌する。手動入力の待機は開けない。
                    if (board != null) board.SetLocalTurn(true);
                    _lastAutoDiscardBaseId = -1;
                    yield return AutoDiscardForPlayer(seen);   // 新規牌を打ってボルテージを切らさない
                    if (_lastAutoDiscardBaseId < 0)
                        throw new System.InvalidOperationException("質問に答える前の自動打牌に使える牌がありません。");
                    if (board != null && gameUIManager != null && gameUIManager.RiverUI != null)
                    {
                        // 表示だけの自動打牌を、今回の分岐では台帳にも反映する。
                        int tile = TutorialTiles.Encode(_lastAutoDiscardBaseId,
                            _lastAutoDiscardBaseId == data.doraBaseId);
                        int index = board.FindAvailableWallIndex(tile);
                        if (index >= 0) board.MarkWallIndexAsDiscarded(index);
                        board.RemoveTileFromWall(tile);
                        board.LastDiscardedTileId = tile;
                    }
                    if (_lastAutoDiscardBaseId >= 0) seen.Add(_lastAutoDiscardBaseId);
                    if (board != null) board.SetLocalTurn(false);
                    yield return new WaitForSeconds(discardInterval);
                    yield return PlayLines(RevealAnswers[answer]);
                    if (answer == 2)
                    {
                        yield return PlaySceneBreakAndWait();
                        yield return PlayLines(new List<TutorialLine> {
                            new TutorialLine("まぁ点と呼んでるけど…賭けの代金は血液だものね"),
                        });
                    }
                    // **最後の選択肢のあとは女の子は打たない**（2026-10-22 プランナー指摘）。
                    // 主人公が最後に出した牌でそのままロンするため
                    if (asked.Count < RevealQuestions.Length)
                    {
                        DiscardForEnemyInSecondBattle(data, TimeLimitTalkEnemyTurn + 1 + asked.Count, seen);
                        yield return new WaitForSeconds(discardInterval);
                    }
                }
                if (_aborted) yield break;
                yield return PlayLines(new List<TutorialLine> {
                    new TutorialLine("さて……\nもう心残りはないかしらね"),
                });
                yield return PlayScriptedTutorialDefeat(data, _lastAutoDiscardBaseId);
                yield return RunRevivalFinale();
            }
            finally
            {
                CloseIntro();
                _isWaitingForDiscard = false;
                if (board != null) board.SetLocalTurn(false);
            }
        }

        /// <summary>図でHP0が指定されたチュートリアル専用の決着。本編の判定・通信には混ぜない。</summary>
        private IEnumerator PlayScriptedTutorialDefeat(TutorialRoundData data, int discardedBaseId, bool deadEnd = false)
        {
            if (discardedBaseId < 0)
                throw new System.InvalidOperationException("チュートリアルの決着前に打牌されていません。");
            SetPhase(RoundStatus.Agari);
            var ronData = _scenario.defeatRon;
            int tile = TutorialTiles.Encode(discardedBaseId, discardedBaseId == data.doraBaseId);
            var hand = TutorialTiles.EncodeAll(ronData.meldBaseIds, data.doraBaseId);
            hand.Add(tile);
            int previousPlayerHp = PlayerShownHp;
            int previousEnemyHp = EnemyShownHp;
            var settlement = BuildScriptedDefeatSettlement(ronData, previousPlayerHp, previousEnemyHp,
                _playerStake, _enemyStake, VoltageSystem.GetMultiplier(isEnemy: true));
            // 役の斜め表示までを先に見せる。説明を終えるまではHPも精算表も進めない。
            yield return PlayRonAnimation(hand, tile, ronData.ToPresentation(), false, previousPlayerHp, settlement.MyHpAfter,
                previousEnemyHp, settlement.TheirHpAfter, -settlement.MyDelta, settlement: settlement,
                suppressSettlementPanel: true, deferHpUpdate: true);
            yield return ExplainTutorialAssault(ronData, deadEnd, settlement.AssaultBonusDamage);
            if (_aborted) yield break;
            var ronUI = gameUIManager != null ? gameUIManager.RonAnimationUI : null;
            if (ronUI != null)
            {
                ronUI.ShowTutorialSettlement(settlement);
                yield return new WaitForSeconds(2f);
                if (_aborted) { ronUI.HideTutorialSettlement(); yield break; }
            }
            ClearStakes();
            _playerHp = settlement.MyHpAfter;
            _enemyHp = settlement.TheirHpAfter;
            if (ronUI != null)
                yield return ronUI.PlayTutorialSettlementTransfer(settlement,
                    gameUIManager.PlayerInfoUI, gameUIManager.EnemyInfoUI,
                    previousPlayerHp, settlement.MyHpAfter, previousEnemyHp, settlement.TheirHpAfter);
            ApplyHpToUI();
            UI.Effects.AssaultMarkUI.Clear();
            if (gameUIManager != null) gameUIManager.ScoreGauge.SetStakes(0, 0);
        }

        private static RonSettlementInfo BuildScriptedDefeatSettlement(TutorialDefeatRonData data,
            int previousPlayerHp, int previousEnemyHp, int playerBet, int enemyBet, float voltage)
        {
            int han = 0;
            foreach (string yaku in data.yakuList) han += GameRules.GetBaseHan(yaku);
            int bonus = Mathf.RoundToInt(GameRules.CalculateWinnerGain(enemyBet, han) * voltage);
            int loss = GameRules.CalculateLoserLoss(playerBet, han, winnerWonWithTanki: true) + bonus;
            var settlement = new RonSettlementInfo
            {
                RankName = data.rankText, TotalHan = han, Multiplier = GameRules.GetMultiplier(han),
                LocalWon = false, VoltageMultiplier = voltage, IsTankiWait = true,
                // チュートリアルだけの計算。本編の確定値・サーバー処理には持ち込まない。
                AssaultApplied = true, AssaultBonusDamage = bonus,
                MyBet = playerBet, TheirBet = enemyBet,
                MyDelta = -loss, TheirDelta = 0,
                MyHpBefore = previousPlayerHp, MyHpAfter = Mathf.Max(0, previousPlayerHp - loss),
                // 強襲で放棄するのは儲けだけ。場に預けた相手自身の掛け金は返る。
                TheirHpBefore = previousEnemyHp, TheirHpAfter = previousEnemyHp + enemyBet,
            };
            GameUIPhaseController.FillYakuRows(settlement, Common.YakuNameUtil.Summarize(data.yakuList), han);
            return settlement;
        }
    }
}
