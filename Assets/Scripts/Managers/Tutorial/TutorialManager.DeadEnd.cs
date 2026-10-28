using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KillingMahjong.Common;
using KillingMahjong.EngineData;
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
    public partial class TutorialManager
    {
        private bool _reachedFlowEnd;

        private IEnumerator RunWatchRestBranch(TutorialRoundData data)
        {
            // DEADENDのリスポーン地点は、問いかけ直前のフラッシュ。
            while (!_aborted)
            {
                yield return PlaySceneBreakAndWait();
                yield return PlayLines(DrawWhoIsGirlLines);
                if (AudioManager.Instance != null) AudioManager.Instance.CutBgmImmediately();
                yield return PlayLines(new List<TutorialLine> { new TutorialLine(WatchRestQuestion) });
                // 先輩のセリフではなく、選択肢の地の文として聞く（2026-10-23 プランナー指摘）
                yield return AskYesNo("対局動画の続きを見ますか？", a => _watchRestAnswer = a, asDialogue: false);
                if (_aborted) yield break;
                if (_watchRestAnswer == 0)
                {
                    yield return PlayLines(new List<TutorialLine> { new TutorialLine("じゃあ") });
                    yield return RunWatchRestContinuation(data);
                    _reachedFlowEnd = true;
                    yield break;
                }
                yield return RunTutorialDeadEnd(data);
            }
        }

        private IEnumerator RunTutorialDeadEnd(TutorialRoundData data)
        {
            var board = BoardStateManager.Instance;
            int hpBefore = _playerHp;
            int enemyHpBefore = _enemyHp;
            int playerStakeBefore = _playerStake, enemyStakeBefore = _enemyStake, stakeRoundsBefore = _stakeRounds;
            var wall = board != null ? new List<int>(board.CurrentWallTiles) : null;
            var discarded = board != null ? new HashSet<int>(board.DiscardedWallIndexes) : null;
            var hidden = board != null ? new HashSet<int>(board.HiddenTiles) : null;
            int lastDiscard = board != null ? board.LastDiscardedTileId : -1;
            var restoreVoltage = VoltageSystem.CaptureTutorialCheckpoint();
            var restoreRiver = gameUIManager != null && gameUIManager.RiverUI != null
                ? gameUIManager.RiverUI.CaptureTutorialCheckpoint() : null;
            var ending = (TutorialDeadEndUI)null;
            try
            {
                yield return PlayLines(new List<TutorialLine> {
                    new TutorialLine("……ふーんそうなんだねぇ"),
                    new TutorialLine("じゃあ君のを打っちゃってよ"),
                });
                if (board != null) board.SetLocalTurn(true);
                _lastPlayerDiscardBaseId = -1;
                _isWaitingForDiscard = true;
                yield return new WaitUntil(() => !_isWaitingForDiscard || _aborted);
                if (_aborted) yield break;
                yield return new WaitForSeconds(discardInterval);
                if (board != null) board.SetLocalTurn(false);

                yield return PlayScriptedTutorialDefeat(data, _lastPlayerDiscardBaseId, deadEnd: true);
                ClearGuide();
                SetBoardVisible(false);
                yield return PlayLines(new List<TutorialLine> {
                    new TutorialLine("あーあ……"),
                    new TutorialLine("アタシにロンされちゃって敗北？"),
                    new TutorialLine("初心者とはいえあの女が言うから期待してたんだけど……"),
                    new TutorialLine("まっ負けたやつはいらんわ"),
                    new TutorialLine("<b><size=23>消えなよ</size></b>"),
                });
                if (dialogueUI != null) { dialogueUI.HideText(); dialogueUI.HideAdvanceOnAnyClick(); }
                ending = TutorialDeadEndUI.Create(transform);
                yield return ending.Play(BorrowJapaneseFont());
            }
            finally
            {
                if (ending != null) ending.Close();
                Tutorial.TutorialAudioDirector.ResetVisuals();
                _isWaitingForDiscard = false;
                _isWaitingForLine = false;
                if (!_aborted)
                {
                    _playerHp = hpBefore;
                    _enemyHp = enemyHpBefore;
                    _playerStake = playerStakeBefore;
                    _enemyStake = enemyStakeBefore;
                    _stakeRounds = stakeRoundsBefore;
                    restoreRiver?.Invoke();
                    restoreVoltage();
                    if (board != null)
                    {
                        board.CurrentWallTiles.Clear(); board.CurrentWallTiles.AddRange(wall);
                        board.DiscardedWallIndexes.Clear(); board.DiscardedWallIndexes.UnionWith(discarded);
                        board.HiddenTiles.Clear(); board.HiddenTiles.UnionWith(hidden);
                        board.LastDiscardedTileId = lastDiscard;
                        board.ClearSelection();
                        board.SetLocalTurn(false);
                    }
                    SetBoardVisible(true);
                    SetPhase(RoundStatus.Discard);
                    // 対局へ戻るので対局の曲を鳴らし直す（このあと台本の「BGM止まる」でまた断ち切る）
                    PlayTutorialBattleBgm();
                    if (gameUIManager != null && gameUIManager.VisualController != null)
                        gameUIManager.VisualController.InvalidateRebuildCache();
                    if (board != null) board.FireRebuildEvent();
                    ApplyPotToUI();
                    if (_tutorialEnemyAssaultActive && gameUIManager != null && gameUIManager.PlayerInfoUI != null)
                        UI.Effects.AssaultMarkUI.Show(gameUIManager.PlayerInfoUI.HpGaugeAnchor, attachToLeft: true);
                }
            }
        }
    }
}
