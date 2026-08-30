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

        private IEnumerator WaitWhileLogIsOpen(float duration)
        {
            float elapsed = 0f;
            yield return null; // 最初のフレームでのクリック誤爆を防ぐ

            while (elapsed < duration)
            {
                if (dialogueUI != null && dialogueUI.IsLogOpen)
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.deltaTime;

                // 指定時間経過後、かつスキップが許可されている場合のみクリック判定を行う
                if (allowClickSkip && elapsed >= minWaitBeforeSkip)
                {
                    bool isClicked = false;
                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) isClicked = true;
                    if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame) isClicked = true;

                    if (isClicked)
                    {
                        break;
                    }
                }

                yield return null;
            }
        }

        private bool TriggerCore(ReactionTrigger trigger, ReactionPriority priority, string formatArg = "")
        {
            if (enemyInfoUI == null) return false;

            // **セリフが1本も無いトリガーはここで断る。**
            // 積んでしまうと ProcessTriggerReaction が空振りして即 Dequeue するだけなのに、
            // 呼び出し側には true が返る。すると「トリガーで喋ったから CSV は要らない」と
            // 誤解して、CSV に書いてあるセリフまで出なくなる
            if (!enemyInfoUI.HasReaction(trigger)) return false;

            float now = Time.unscaledTime;

            // 同じセリフの連発を防ぐ。進行に関わるものは止めない
            if (priority != ReactionPriority.Progress)
            {
                float last;
                if (_lastFiredAt.TryGetValue(trigger, out last) && now - last < perTriggerCooldown)
                {
                    return false;
                }
            }

            if (priority == ReactionPriority.Ambient)
            {
                // ここでキューに積んではいけない。積むと待ち時間ぶん遅れて
                // 「もう終わった操作」に対して喋り出す
                if (isProcessingReactions || reactionQueue.Count > 0) return false;
                if (now - _lastAmbientAt < ambientGlobalCooldown) return false;
            }
            else if (priority == ReactionPriority.Situation)
            {
                if (_queuedTriggers.Contains(trigger)) return false;
            }

            _lastFiredAt[trigger] = now;
            if (priority == ReactionPriority.Ambient) _lastAmbientAt = now;

            _queuedTriggers.Add(trigger);
            reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessTriggerReaction(trigger, formatArg)));
            if (!isProcessingReactions) ProcessNextReaction();
            return true;
        }

        private IEnumerator ProcessTriggerReaction(ReactionTrigger trigger, string formatArg)
        {
            float duration = reactionDisplayDuration;
            string text = enemyInfoUI.PlayReaction(trigger, duration, formatArg ?? "");

            // データが無いトリガーで待ち時間を潰さない。
            // 5秒間なにも出ないまま後続を止めてしまうため
            if (string.IsNullOrEmpty(text))
            {
                _queuedTriggers.Remove(trigger);
                _currentReaction = null;
                ProcessNextReaction();
                yield break;
            }

            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText(text.Contains("「") ? text : $"「{text}」");
            }

            yield return WaitWhileLogIsOpen(duration);

            _queuedTriggers.Remove(trigger);
            _pendingOnComplete = null;
            _currentReaction = null;
            ProcessNextReaction();
        }

        private void EnqueueDiscardReactionCore(int tileId, bool isLocalPlayer, string tileName)
        {
            string conditionBase = isLocalPlayer ? "プレイヤーが打牌した時" : "相手が打牌した時";
            int randomIdx = UnityEngine.Random.Range(1, 6);
            string condition = $"{conditionBase}{randomIdx}";
            
            var entry = Managers.DialogueManager.Instance.GetDialogueEntry(condition);
            if (entry == null)
            {
                entry = Managers.DialogueManager.Instance.GetDialogueEntry(conditionBase);
                condition = conditionBase;
            }

            if (entry != null && (!string.IsNullOrEmpty(entry.Dialogue1) || !string.IsNullOrEmpty(entry.Dialogue2)))
            {
                if (isLocalPlayer)
                {
                    EnqueueFormattedCSVDialogue(condition, tileName, false);
                }
                else
                {
                    if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(true);
                    EnqueueFormattedCSVDialogue(condition, tileName, false, () => {
                        if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);
                    });
                }
            }
            else
            {
                reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessLegacyDiscardEvent(tileId, isLocalPlayer, tileName)));
                if (!isProcessingReactions)
                {
                    ProcessNextReaction();
                }
            }
        }

        private void EnqueueFormattedCSVDialogueCore(string condition, string formatArg, bool clearPrevious = true, Action onComplete = null)
        {
            var entry = Managers.DialogueManager.Instance.GetDialogueEntry(condition);
            if (entry != null && (!string.IsNullOrEmpty(entry.Dialogue1) || !string.IsNullOrEmpty(entry.Dialogue2)))
            {
                if (clearPrevious) ClearReactions();

                if (!string.IsNullOrEmpty(entry.Dialogue1))
                {
                    string safeText1 = string.Format(entry.Dialogue1, formatArg);
                    string safeExpr = entry.Expression;
                    string safePose = entry.Pose;
                    bool isLast = string.IsNullOrEmpty(entry.Dialogue2);
                    reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessCSVDialogue(safeText1, safePose, safeExpr, isLast ? onComplete : null)));
                }

                if (!string.IsNullOrEmpty(entry.Dialogue2))
                {
                    string safeText2 = string.Format(entry.Dialogue2, formatArg);
                    string safeExpr = entry.Expression;
                    string safePose = entry.Pose;
                    reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessCSVDialogue(safeText2, safePose, safeExpr, onComplete)));
                }
                
                if (!isProcessingReactions) ProcessNextReaction();
            }
        }

        private void EnqueueCustomDialogueCore(string text, string poseName = "", string expressionName = "", bool clearPrevious = true)
        {
            if (clearPrevious) ClearReactions();
            reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(ProcessCSVDialogue(text, poseName, expressionName, null)));
            if (!isProcessingReactions) ProcessNextReaction();
        }

        private void EnqueueCSVDialogueCore(string condition, bool clearPrevious = true)
        {
            EnqueueFormattedCSVDialogue(condition, "", clearPrevious);
        }

        private IEnumerator ProcessCSVDialogue(string text, string poseName, string expressionName, Action onComplete = null)
        {
            _pendingOnComplete = onComplete;

            if (dialogueUI != null)
            {
                dialogueUI.gameObject.SetActive(true);
                dialogueUI.ShowText(text.Contains("「") ? text : $"「{text}」");
            }
            if (enemyInfoUI != null)
            {
                if (!string.IsNullOrEmpty(expressionName) || !string.IsNullOrEmpty(poseName))
                {
                    enemyInfoUI.PlayReactionWithVisualId(poseName, expressionName, reactionDisplayDuration);
                }
                else
                {
                    enemyInfoUI.PlayBounceAnimation(reactionDisplayDuration);
                }
            }

            // StartCoroutine で包まないこと。包むと親を StopCoroutine しても
            // 子コルーチンが生き残り、演出が二重に進む。
            yield return WaitWhileLogIsOpen(reactionDisplayDuration);

            _pendingOnComplete = null;
            _currentReaction = null;
            onComplete?.Invoke();
            ProcessNextReaction();
        }

        private IEnumerator ProcessLegacyDiscardEvent(int tileId, bool isLocalPlayer, string tileName)
        {
            // 中断されても打牌モーションが解除されるようにしておく
            _pendingOnComplete = () => { if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false); };

            if (isLocalPlayer)
            {
                if (dialogueUI != null)
                {
                    string text = enemyInfoUI != null ? enemyInfoUI.PlayReaction(ReactionTrigger.PlayerDiscard, reactionDisplayDuration) : null;
                    if (string.IsNullOrEmpty(text)) text = "「プレイヤーが何かを捨てたな…」";
                    dialogueUI.ShowText(text);
                }
                if (enemyInfoUI != null)
                {
                    enemyInfoUI.PlayBounceAnimation(reactionDisplayDuration);
                }
            }
            else
            {
                if (dialogueUI != null)
                {
                    string text = enemyInfoUI != null ? enemyInfoUI.PlayReaction(ReactionTrigger.EnemyDiscard, reactionDisplayDuration, tileName) : null;
                    if (string.IsNullOrEmpty(text)) text = $"「{tileName}を切るわ！」";
                    dialogueUI.ShowText(text);
                }
                
                if (enemyInfoUI != null) 
                {
                    enemyInfoUI.SetDiscardingState(true);
                    enemyInfoUI.PlayBounceAnimation(reactionDisplayDuration);
                }
            }

            yield return WaitWhileLogIsOpen(reactionDisplayDuration);

            _pendingOnComplete = null;
            _currentReaction = null;
            if (enemyInfoUI != null) enemyInfoUI.SetDiscardingState(false);

            ProcessNextReaction();
        }
    }
}
