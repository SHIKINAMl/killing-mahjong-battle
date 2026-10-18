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

        /// <summary>2026-10-18の図。「はい」から動画後半、3つの質問、指定された決着まで。</summary>
        private IEnumerator RunWatchRestContinuation(TutorialRoundData data)
        {
            if (dialogueUI != null) { dialogueUI.HideText(); dialogueUI.HideAdvanceOnAnyClick(); }
            var replay = TutorialDrawReplayUI.Create(transform);
            try { yield return replay.PlayContinuation(); }
            finally { if (replay != null) replay.Close(); }
            if (_aborted) yield break;

            yield return PlaySceneBreakAndWait();
            if (gameUIManager != null && gameUIManager.EnemyInfoUI != null)
                yield return gameUIManager.EnemyInfoUI.RevealTutorialCharacter();
            yield return PlayLines(new List<TutorialLine> {
                new TutorialLine("ふふふ……\n先輩死んじゃったね？"),
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
                    yield return AutoDiscardForPlayer();
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
                    DiscardForEnemyInSecondBattle(data, TimeLimitTalkEnemyTurn + 1 + asked.Count, seen);
                    yield return new WaitForSeconds(discardInterval);
                }
                if (_aborted) yield break;
                yield return PlayLines(new List<TutorialLine> {
                    new TutorialLine("さて……\nもう心残りはないかしらね"),
                });
                yield return PlayScriptedTutorialDefeat(data, _lastAutoDiscardBaseId);
                yield return PlayLines(new List<TutorialLine> {
                    new TutorialLine("たかだか他人の死に熱くなりすぎなのよね"),
                });
                yield return RunRevivalFinale();
            }
            finally
            {
                CloseIntro();
                _isWaitingForDiscard = false;
                if (board != null) board.SetLocalTurn(false);
            }
        }

        /// <summary>図でHP0が指定されたチュートリアル専用の決着。勝敗式を追加しない。</summary>
        private IEnumerator PlayScriptedTutorialDefeat(TutorialRoundData data, int discardedBaseId)
        {
            if (discardedBaseId < 0)
                throw new System.InvalidOperationException("チュートリアルの決着前に打牌されていません。");
            SetPhase(RoundStatus.Agari);
            var ronData = _scenario.rounds.Count > 2 ? _scenario.rounds[2] : data;
            int tile = TutorialTiles.Encode(discardedBaseId, discardedBaseId == data.doraBaseId);
            var hand = TutorialTiles.EncodeAll(ronData.enemyRonMeldBaseIds, data.doraBaseId);
            hand.Add(tile);
            yield return PlayRonAnimation(hand, tile, ronData, false, _playerHp, 0,
                _enemyHp, _enemyHp, _playerHp, suppressSettlementPanel: true);
            _playerHp = 0;
            ApplyHpToUI();
        }
    }
}
