using System;
using System.Collections;
using UnityEngine;
using KillingMahjong.UI;

namespace KillingMahjong.Managers
{
    public partial class ReactionController
    {
        private int discardRevision;
        private bool capturingDiscard;
        private string discardLogContext;
        private float nextOrdinaryDiscardAt;
        private int lastOrdinaryEnemyLine;
        private string lastReactionText;
        private float lastReactionTextAt = -100;
        private readonly System.Collections.Generic.Dictionary<string, float> discardCsvTimes =
            new System.Collections.Generic.Dictionary<string, float>();

        private GameUIManager MatchUI
        {
            get
            {
                if (_uiManagerForTutorial == null) _uiManagerForTutorial = FindFirstObjectByType<GameUIManager>();
                return _uiManagerForTutorial;
            }
        }

        // 打牌の反応はその一打の間だけ有効。進行に必要な会話は捨てない。
        private void QueueContextual(Func<IEnumerator> play, Action dropped = null, bool transient = false)
        {
            bool discard = capturingDiscard;
            int revision = discardRevision, round = _currentRound;
            var ui = MatchUI;
            var phase = ui != null ? ui.CurrentPhaseStatus : default(KillingMahjong.EngineData.RoundStatus);
            float expires = Time.unscaledTime + 5f;
            string context = discard ? discardLogContext : null;
            Func<bool> valid = () => !transient && !discard ||
                (_currentRound == round && (!discard || revision == discardRevision) &&
                 Time.unscaledTime <= expires && (ui == null || ui.CurrentPhaseStatus == phase));
            reactionQueue.Enqueue(() => _currentReaction = StartCoroutine(
                PlayContextual(play, dropped, valid, context, transient || discard)));
        }

        private IEnumerator PlayContextual(Func<IEnumerator> play, Action dropped, Func<bool> valid, string context, bool waitForEffects)
        {
            yield return null;
            // 一打の着地を見せてから反応する。
            if (!string.IsNullOrEmpty(context)) yield return new WaitForSecondsRealtime(.18f);
            while (valid() && (OptionUI.AnyOpen || (waitForEffects && MatchUI != null && MatchUI.IsBusyForQueuedEffect)))
                yield return null;
            if (!valid())
            {
                dropped?.Invoke();
                _currentReaction = null;
                ProcessNextReaction();
                yield break;
            }
            if (dialogueUI != null) dialogueUI.SetReactionLogContext(context);
            yield return play();
        }

        private void ShowReactionText(string text)
        {
            if (dialogueUI == null || string.IsNullOrEmpty(text)) return;
            if (text == lastReactionText && Time.unscaledTime - lastReactionTextAt < 20f)
            { dialogueUI.SetReactionLogContext(null); return; }
            lastReactionText = text; lastReactionTextAt = Time.unscaledTime;
            dialogueUI.gameObject.SetActive(true);
            dialogueUI.ShowText(text.Contains("「") ? text : "「" + text + "」");
        }
    }
}
