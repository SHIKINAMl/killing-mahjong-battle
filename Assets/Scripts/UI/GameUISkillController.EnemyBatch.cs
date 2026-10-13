using System.Collections;
using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Network;
using KillingMahjong.UI.Effects;

namespace KillingMahjong.UI
{
    public partial class GameUISkillController
    {
        private sealed class EnemyBatch
        {
            public readonly List<SkillCastedData> Casts = new List<SkillCastedData>();
            public EffectQueue.Ticket Ticket;
            public bool Started;
            public int Generation;
        }

        private EnemyBatch _pendingEnemyBatch;

        private void EnqueueEnemySkill(EffectQueue queue, SkillCastedData data)
        {
            var batch = _pendingEnemyBatch;
            if (batch == null || batch.Started || !queue.IsLastPending(batch.Ticket)
                || batch.Casts[0].player_id != data.player_id)
            {
                batch = new EnemyBatch { Generation = _skillRunGeneration };
                batch.Casts.Add(data);
                _pendingEnemyBatch = batch;
                var captured = batch;
                batch.Ticket = queue.Enqueue("skill:enemy-batch", () => RunQueuedEnemyBatch(captured));
            }
            else batch.Casts.Add(data);

            // 多数の発動をまとめても、各効果の待機を途中で打ち切らない。
            batch.Ticket.Timeout = UnityEngine.Mathf.Max(batch.Ticket.Timeout, batch.Casts.Count * 4f + 4f);
        }

        private IEnumerator RunQueuedEnemyBatch(EnemyBatch batch)
        {
            batch.Started = true;
            if (_pendingEnemyBatch == batch) _pendingEnemyBatch = null;
            if (this == null || !isActiveAndEnabled || batch.Generation != _skillRunGeneration) yield break;
            bool done = false;
            StartCoroutine(RunThenFlag(PlayEnemyBatch(batch), () => done = true));
            while (!done && this != null && isActiveAndEnabled && batch.Generation == _skillRunGeneration)
                yield return null;
        }

        private IEnumerator PlayEnemyBatch(EnemyBatch batch)
        {
            var lease = uiManager.BeginTransition("skill:enemy-batch");
            skillTransitions.Add(lease);
            bool finished = false;
            try
            {
                bool combined = batch.Casts.Count > 1;
                if (combined)
                {
                    var character = uiManager.EnemyInfoUI != null ? uiManager.EnemyInfoUI.CurrentCharacterData : null;
                    if (uiManager.PhaseTransitionUI != null)
                        yield return EnemySkillBatchPresentation.Play(uiManager.PhaseTransitionUI, character, batch.Casts);
                    else if (uiManager.DialogueUI != null)
                    {
                        uiManager.DialogueUI.ShowText("【相手】がアビリティを発動！\n" + EnemySkillBatchPresentation.BuildLabel(batch.Casts));
                        yield return new UnityEngine.WaitForSeconds(2f);
                    }
                }
                foreach (var cast in batch.Casts)
                {
                    if (!lease.IsActive || batch.Generation != _skillRunGeneration) yield break;
                    // 表示は集約しても、通知と効果の順序・サーバー確定値は元のまま。
                    yield return HandleSkillEffectsRoutine(cast, lease, skipCutin: combined);
                }
                finished = lease.IsActive;
            }
            finally { skillTransitions.Remove(lease); lease.Dispose(); }
            if (finished) uiManager.RefreshBoardAfterTransition();
        }
    }
}
