using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KillingMahjong.UI;
using KillingMahjong.Managers.Reactions;

namespace KillingMahjong.Managers
{
    public partial class ReactionController
    {

        private void ResetStateForNewGameCore()
        {
            _firstTerminalHonorPlayed = false;
            _firstMiddleTilePlayed = false;
            _firstMaxBetPlayed = false;
            _firstMinBetPlayed = false;
            _firstDrawPlayed = false;
            _round1DiscardCount = 0;
            _lastDiscardedTileId = -1;
            _firstPlayerAwasePlayed = false;
            _firstEnemyAwasePlayed = false;
            _currentRound = 1;

            _drawCount = 0;
            _playerConsecutiveHonorCount = 0;
            _handSelectionTimerActive = false;
            _betPhaseTimerActive = false;
            _playerHp = 10000;
            _enemyHp = 10000;
            _playerLostLastRound = false;
            _playerDiscardHistory.Clear();
            _enemyDiscardHistory.Clear();

            _betChangeCount = 0;
            _playerNearDeathPlayed = false;

            // ルールの「1対局に1回」と クールダウンもここで白紙に戻す
            ReactionRuleEngine.ResetMatch(ReactionRuleSet.Load());

            if (PlayerActivityWatcher.Instance != null)
                PlayerActivityWatcher.Instance.ResetForNewRound();
        }

        private bool PublishCore(ReactionEvent ev, ReactionContext ctx)
        {
            if (enemyInfoUI == null) return false;

            var set = ReactionRuleSet.Load();
            if (set == null) return false;

            var rule = ReactionRuleEngine.Match(set, ev, ctx);
            if (rule == null) return false;

            var line = ReactionRuleEngine.PickLine(rule);
            if (line == null) return false;

            float now = Time.unscaledTime;

            if (rule.priority == ReactionPriority.Ambient)
            {
                // 積むと待ち時間ぶん遅れて「もう終わった操作」に対して喋り出す
                if (isProcessingReactions || reactionQueue.Count > 0) return false;
                if (now - _lastAmbientAt < ambientGlobalCooldown) return false;
                _lastAmbientAt = now;
            }
            else if (rule.priority == ReactionPriority.Situation)
            {
                if (_queuedRules.Contains(rule)) return false;
            }

            ReactionRuleEngine.MarkFired(rule);
            _queuedRules.Add(rule);

            var captured = rule;
            string text = line.text;
            string face = line.faceId;
            reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessRuleLine(captured, text, face)));
            if (!isProcessingReactions) ProcessNextReaction();
            return true;
        }

        private IEnumerator ProcessRuleLine(ReactionRule rule, string text, string faceId)
        {
            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText(text.Contains("「") ? text : $"「{text}」");
            }
            if (enemyInfoUI != null && !string.IsNullOrEmpty(faceId))
            {
                enemyInfoUI.PlayReactionWithVisualId("", faceId, reactionDisplayDuration);
            }

            yield return WaitWhileLogIsOpen(reactionDisplayDuration);

            _queuedRules.Remove(rule);
            _pendingOnComplete = null;
            _currentReaction = null;
            ProcessNextReaction();
        }

        private ReactionContext NewContext()
        {
            return new ReactionContext().WithCommon();
        }

        private bool PlayOrFallback(ReactionTrigger trigger, ReactionPriority priority, string csvCondition)
        {
            if (Trigger(trigger, priority)) return true;
            if (string.IsNullOrEmpty(csvCondition)) return false;
            EnqueueCSVDialogue(csvCondition);
            return true;
        }

        private static bool IsLocalTenpai()
        {
            var b = BoardStateManager.Instance;
            return b != null && b.LocalWaitDataList != null && b.LocalWaitDataList.Count > 0;
        }
    }
}
