using System.Collections.Generic;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;

namespace KillingMahjong.UI
{
    public partial class GameUIHandSelectionController
    {
        // is_tenpaiにはrequest idが無い。WebSocketの応答順を、送った順のスナップショットに対応させる。
        // 取消済みの問い合わせも列に残し、遅い応答を次の選択の返事に取り違えない。
        private sealed class TenpaiProbe
        {
            public List<int> Indexes;
            public int Version, Generation;
            public bool Confirmation;
            public string Signature;
        }
        private readonly Queue<TenpaiProbe> tenpaiProbes = new Queue<TenpaiProbe>();

        private void SendTenpaiProbe(List<int> indexes, bool confirmation)
        {
            tenpaiProbes.Enqueue(new TenpaiProbe { Indexes = new List<int>(indexes), Version = submissionVersion,
                Generation = uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0,
                Confirmation = confirmation, Signature = CurrentSelectionSignature() });
            uiManager.SendActionToServer("is_tenpai", new Network.ActionPayload { wall_indexes = new List<int>(indexes) });
        }

        private bool HandleTenpaiProbe(IsTenpaiData data)
        {
            if (tenpaiProbes.Count == 0) return false;
            var probe = tenpaiProbes.Dequeue();
            if (!probe.Confirmation && SameHandIndexes(probe.Indexes, _rankRequestIndexes)) _rankRequestInFlight = false;
            int generation = uiManager.PhaseController != null ? uiManager.PhaseController.PresentationGeneration : 0;
            if (probe.Generation != generation || uiManager.CurrentPhaseStatus != RoundStatus.HandSelection) return true;
            if (probe.Signature != null && probe.Signature != CurrentSelectionSignature())
            {
                if (!_confirming) UpdateInstantRankCallForCurrentHand();
                return true;
            }
            data = data ?? new IsTenpaiData { waits = System.Array.Empty<WaitData>() };
            if (_confirming && SameHandIndexes(probe.Indexes, _pendingHandIndexes)
                && (!probe.Confirmation || probe.Version == submissionVersion))
            {
                StoreWaitDetails(data.waits);
                ResolveImmediateConfirmation(data);
                return true;
            }
            if (probe.Confirmation || probe.Version != submissionVersion || !CanShowInstantRankCall()
                || !TryCaptureCurrentHandSelection(out List<int> now, out _)) return true;
            if (!SameHandIndexes(now, probe.Indexes)) { UpdateInstantRankCallForCurrentHand(); return true; }
            _rankResultIndexes = new List<int>(now);
            _rankSignature = CurrentSelectionSignature();
            _rankWaits = data.waits ?? System.Array.Empty<WaitData>();
            _hasRankResult = true;
            StoreWaitDetails(_rankWaits);
            ShowRankCall(_rankWaits);
            return true;
        }

        private void StoreWaitDetails(WaitData[] waits)
        {
            var board = BoardStateManager.Instance;
            if (board == null) return;
            board.LocalWaitDataList.Clear();
            var nonMangan = new List<int>();
            if (waits != null) foreach (var w in waits)
            {
                if (w == null) continue;
                board.LocalWaitDataList.Add(w);
                if (!w.mangan_or_more) nonMangan.Add(w.tile);
            }
            board.SetNonManganWaits(nonMangan);
        }

        // 同じ山牌indexでも、牌交換や役強化の後は古い表示データを使わない。
        private string CurrentSelectionSignature()
        {
            var board = BoardStateManager.Instance;
            if (board == null) return "";
            var values = new List<int>(board.CurrentHandTiles); values.Sort();
            var keys = new List<string>(board.LocalBoostHandBonus.Keys); keys.Sort(System.StringComparer.Ordinal);
            var boosts = new List<string>();
            foreach (var key in keys) boosts.Add(key + "=" + board.LocalBoostHandBonus[key]);
            return string.Join(",", values) + ";" + string.Join(",", boosts);
        }

        private IsTenpaiData BuildTutorialWaits()
        {
            var tm = uiManager.TutorialManager;
            var result = new List<WaitData>();
            if (tm != null) foreach (int id in tm.GetCurrentWaitTileIds())
                result.Add(new WaitData { tile = id, yaku = tm.CurrentHandYaku.ToArray(), han = tm.CurrentHandHan,
                    mangan_or_more = true, multiplier = GameRules.GetMultiplier(tm.CurrentHandHan),
                    multiplier_label = tm.CurrentHandHan >= 26 ? "ダブル役満" : tm.CurrentHandHan >= 13 ? "役満"
                        : tm.CurrentHandHan >= 11 ? "三倍満" : tm.CurrentHandHan >= 8 ? "倍満" : tm.CurrentHandHan >= 6 ? "跳満" : "満貫" });
            return new IsTenpaiData { waits = result.ToArray() };
        }
    }
}
