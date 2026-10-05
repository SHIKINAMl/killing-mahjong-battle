using System;
using System.Collections.Generic;
using System.Linq;
using KillingMahjong.UI;
using KillingMahjong.Network;
using KillingMahjong.Network.Handlers;
using KillingMahjong.EngineData;
using Kind = KillingMahjong.UI.RoundStartCoordinator.BoardUpdateKind;

// The coordinator, Unity glue, message guards and keyed queue come from production.
// Drawing, coroutines and network transport are replaced by observable callbacks.
static class Probe
{
    public static GameUIManager UI;
    public static List<string> Events = new List<string>();
    static readonly NetworkMessageHandler Network = new NetworkMessageHandler();
    static readonly PhaseMessageHandler Phase = new PhaseMessageHandler();
    static readonly DealingMessageHandler Deal = new DealingMessageHandler();
    static readonly StatusMessageHandler Status = new StatusMessageHandler();
    public static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
    static void Fresh(bool hasTransition = true)
    {
        UI = new GameUIManager(hasTransition);
        Events.Clear();
    }
    public static void Apply(string value, Kind kind)
    {
        Check(!UI.PhaseController.ShouldDeferRoundStartBoardUpdate(kind, false), "Board update before round reset/transition completed");
        Check(UI.PhaseTransitionUI == null || UI.PhaseTransitionUI.Covered, "Board update before full blackout");
        Check(UI.WaitUIReady, "Board update before waiting UI completed");
        Events.Add(value);
    }
    static void Receive(string value) { Deal.Handle("dealing_completed", value, Network); Status.Handle("status", value, Network); }
    static void Dealing() { Phase.Handle("phase_change", "dealing", Network); }
    static void Pass(string name) { Console.WriteLine("PASS " + name); }
    static void Main()
    {
        // Status can arrive before the phase visibility action in the initial round.
        Fresh();
        UI.PhaseController.StartInitialMatch();
        Status.Handle("status", "early", Network);
        Dealing();
        Deal.Handle("dealing_completed", "initial", Network);
        UI.Flush();
        Check(Events.Count == 0, "Early initial payload leaked");
        UI.PhaseTransitionUI.FinishDarken();
        // Deliberately apply the deferred Dealing visibility LAST: it must not own reset.
        UI.MovePhaseVisibilityLast();
        UI.Flush();
        Check(Events.SequenceEqual(new[] { "reset", "wait", "status:early", "deal:initial", "reveal" }), "Initial reset still depends on queue position");
        Check(UI.ResetCount == 1 && UI.PhaseTransitionUI.DarkenStarts == 1, "Initial phase restarted blackout/reset");
        Pass("initial reset independent of phase visibility queue order");

        // Previous animation postpones the next round; even a forced flush must not bypass cover/reset.
        Events.Clear(); UI.CurrentPhaseStatus = RoundStatus.HandSelection; UI.SetIsTransitioning(true);
        Dealing(); Receive("next");
        UI.ForceFlush = true; UI.Flush();
        Check(Events.Count == 0 && UI.PhaseTransitionUI.IsDarkenTransitioning, "Forced flush applied a board before cover");
        UI.Flush(); Check(Events.Count == 0, "Forced retry bypassed coordinator");
        UI.SetIsTransitioning(false); UI.ForceFlush = false;
        UI.PhaseTransitionUI.FinishDarken(); UI.Flush();
        Check(Events.SequenceEqual(new[] { "reset", "wait", "deal:next", "reveal", "status:next" }), "Next round order changed");
        Check(UI.ResetCount == 2, "Round reset ran more than once");
        Pass("next round after other animation / forced flush / reset once");

        // Draw has separate cover and completion callbacks.
        Fresh(); UI.CurrentPhaseStatus = RoundStatus.Draw; UI.AfterDraw = true;
        Dealing(); Receive("draw");
        UI.PhaseTransitionUI.FinishDrawMidpoint();
        UI.ForceFlush = true; UI.Flush();
        Check(Events.SequenceEqual(new[] { "reset" }), "Draw data applied before complete callback");
        UI.ForceFlush = false; UI.PhaseTransitionUI.FinishDraw(); UI.Flush();
        Check(Events.SequenceEqual(new[] { "reset", "wait", "deal:draw", "reveal", "status:draw" }), "Draw completion never released board");
        Check(!UI.IsTransitioning && UI.ResetCount == 1, "Draw lock/reset incorrect");
        Pass("draw midpoint reset / completion gate shared by both payloads");

        Fresh(); Dealing(); UI.PhaseTransitionUI.FinishDarken(); Receive("late");
        Check(UI.QueueCount == 0 && Events.SequenceEqual(new[] { "reset", "wait", "deal:late", "reveal", "status:late" }), "Late notifications blocked");
        Dealing(); Network.RaiseDealingCompleted();
        Check(UI.QueueCount == 0 && UI.ResetCount == 1 && UI.RevealCount == 1, "Duplicate phase/completion restarted the round");
        Pass("late payload / duplicate phase and completion");

        Fresh(); Dealing();
        Status.Handle("status", "old", Network); Deal.Handle("dealing_completed", "old", Network);
        Status.Handle("status", "new", Network); Deal.Handle("dealing_completed", "new", Network);
        Check(UI.QueueCount == 2, "Latest payloads piled up");
        UI.PhaseTransitionUI.FinishDarken(); UI.Flush();
        Check(Events.SequenceEqual(new[] { "reset", "wait", "status:new", "deal:new", "reveal" }), "Key replacement changed arrival position or lost latest payload");
        Pass("latest payload replacement preserves keyed queue position");

        Fresh(false); Dealing(); Receive("no-ui");
        Check(UI.QueueCount == 0 && Events.SequenceEqual(new[] { "reset", "wait", "deal:no-ui", "status:no-ui" }), "Missing transition UI caused a deadlock");
        Pass("fallback without transition UI");

        var flow = new RoundStartCoordinator(); Action covered = null, ready = null;
        int resets = 0, waits = 0, reveals = 0;
        flow.BeginRound();
        flow.TryStartTransition((a,b) => { covered=a; ready=b; }, () => resets++, () => waits++);
        ready(); Check(flow.IsWaitingForReady && waits == 0, "Early ready callback released the round");
        Check(!flow.CompleteDealing(() => reveals++), "Early completion released blackout");
        covered(); covered(); ready(); ready();
        flow.CompleteDealing(() => reveals++); flow.CompleteDealing(() => reveals++);
        Check(resets == 1 && waits == 1 && reveals == 1, "Duplicate callback repeated a side effect");
        Pass("out-of-order and duplicate callbacks cannot release early");

        flow.BeginRound();
        flow.TryStartTransition((a,b) => { covered=a; ready=b; }, () => resets++, () => waits++);
        flow.Reset(); flow.BeginRound(); covered(); ready();
        Check(flow.CurrentStage == RoundStartCoordinator.Stage.WaitingForTransition && resets == 1 && waits == 1, "Stale callbacks mutated a new match");
        Pass("callbacks from a previous match are ignored");

        var outsideRound = new RoundStartCoordinator();
        Check(!outsideRound.ShouldDeferBoardUpdate(Kind.Status, true), "Ordinary status now waits on unrelated animations");
        Check(outsideRound.ShouldDeferBoardUpdate(Kind.DealingCompleted, true), "Dealing stopped waiting on other animations");
        outsideRound.BeginRound();
        Check(outsideRound.ShouldDeferBoardUpdate(Kind.Status, false) && outsideRound.ShouldDeferBoardUpdate(Kind.DealingCompleted, false), "Idle gap leaked a round payload");
        Pass("ordinary status policy preserved / idle gap blocks both round payloads");

        var reentrant = new RoundStartCoordinator(); bool blockedDuringWaitUI = false;
        reentrant.BeginRound();
        reentrant.TryStartTransition((a,b) => { a(); b(); }, () => {}, () => blockedDuringWaitUI=reentrant.ShouldDeferBoardUpdate(Kind.Status,false));
        Check(blockedDuringWaitUI && !reentrant.IsWaitingForReady, "Wait UI was prepared after payload release");
        Pass("waiting UI completes before board updates are released");
        // Run the actual completion block of the presentation coroutine as well.
        foreach (bool withText in new[] { true, false })
        {
            var completion = new CompletionRegression.CompletionProbe();
            if (withText) completion.centerText = new CompletionRegression.Text();
            var order = new List<string>();
            completion.Finish(
                () => { Check(completion.checkerMaterial.Progress == 1f && completion.IsDarkenTransitioning, "Reset callback ran before cover"); order.Add("covered"); },
                () => { Check(completion.checkerMaterial.Progress == 1f && !completion.IsDarkenTransitioning, "Ready callback ran before presentation ready"); order.Add("ready"); });
            Check(order.SequenceEqual(new[] { "covered", "ready" }), "Presentation callbacks reversed or lost");
            Pass("production darken completion callbacks " + (withText ? "with text" : "without text"));
        }
        Console.WriteLine("All 12 control-flow regressions passed (graphics/transport simulated).");
    }
}
namespace UnityEngine
{
    public static class Debug { public static void Log(string value) {} }
    public static class Object { public static T FindFirstObjectByType<T>() { return (T)(object)Probe.UI; } }
    public static class JsonUtility { public static T FromJson<T>(string json) { return (T)(object)new PhaseChangeMessage { new_status=json }; } }
}
namespace KillingMahjong.EngineData
{
    public enum RoundStatus { None, Dealing, HandSelection, Betting, Discard, Draw }
    public class PhaseChangeMessage { public string new_status; }
    public class DiscardPhaseStartedMessage { public DiscardData data; }
    public class DiscardData { public string first_player; }
}
namespace KillingMahjong.Managers
{
    public class BoardStateManager
    {
        public static BoardStateManager Instance = new BoardStateManager();
        public void SetLocalPlayerFirstRound(bool value) {}
        public void SetLocalTurn(bool value) {}
    }
    public class ReactionController
    {
        public static ReactionController Instance { get { return null; } }
        public void Setup(object dialogue, object enemy, object player) {}
    }
}
namespace KillingMahjong.Network.Handlers { public interface IServerMessageHandler {} }
namespace KillingMahjong.Network
{
    public class NetworkMessageHandler
    {
        public bool AgariProcessed;
        public string LocalPlayerId = "self";
        public void RaiseDealingStarted() { Probe.UI.PhaseController.HandleDealingStarted(); }
        public void RaiseDealingCompleted() { Probe.UI.PhaseController.HandleDealingCompleted(); }
        public void RaisePhaseStatusChanged(RoundStatus status)
        {
            if (Probe.UI.CurrentPhaseStatus == status) return;
            Probe.UI.CurrentPhaseStatus = status;
            Probe.UI.ApplyPhaseVisibility(status);
        }
    }
}
namespace KillingMahjong.UI
{
    public partial class GameUIPhaseController
    {
        private GameUIManager uiManager;
        public GameUIPhaseController(GameUIManager manager) { uiManager=manager; }
        public void StartInitialMatch() { roundStart.Reset(); BeginRoundStart(); StartRoundStartTransition("initial",false); }
        public void StartDealingVisibility() { StartRoundStartTransition("round",uiManager.AfterDraw); }
        private void SetMatchUIVisibility(bool value) {}
        private void ShowDealingWaitUI() { Probe.Events.Add("wait"); uiManager.WaitUIReady=true; }
    }
    public partial class GameUIManager
    {
        public GameUIPhaseController PhaseController { get; private set; }
        public PhaseTransitionUI PhaseTransitionUI;
        public RoundStatus CurrentPhaseStatus;
        public bool IsTransitioning, ForceFlush, WaitUIReady, AfterDraw;
        public int ResetCount, RevealCount;
        public object DialogueUI, EnemyInfoUI, PlayerInfoUI;
        private readonly List<KeyValuePair<string,Action>> deferredActions = new List<KeyValuePair<string,Action>>();
        public GameUIManager(bool hasTransition) { PhaseController=new GameUIPhaseController(this); if(hasTransition) PhaseTransitionUI=new PhaseTransitionUI(); }
        public bool IsBusyWithTransition => !ForceFlush && (IsTransitioning || (PhaseTransitionUI != null && PhaseTransitionUI.IsDarkenTransitioning));
        public int QueueCount => deferredActions.Count;
        private void EnsureFlushWatcher() {}
        public void SetIsTransitioning(bool value) { IsTransitioning=value; }
        public void ClearAllTiles() { ResetCount++; WaitUIReady=false; Probe.Events.Add("reset"); }
        public void ApplyPhaseVisibility(RoundStatus status)
        {
            if(IsBusyWithTransition) { DeferUntilIdle("phaseVisibility:"+status,()=>ApplyPhaseVisibility(status)); return; }
            if(status == RoundStatus.Dealing) PhaseController.StartDealingVisibility();
        }
        public void MovePhaseVisibilityLast() { var phase=deferredActions.Where(x=>x.Key.StartsWith("phaseVisibility:")).ToArray(); deferredActions.RemoveAll(x=>x.Key.StartsWith("phaseVisibility:")); deferredActions.AddRange(phase); }
        public void Flush() { var copy=deferredActions.ToArray(); deferredActions.Clear(); foreach(var entry in copy) entry.Value(); }
    }
    public class PhaseTransitionUI
    {
        public bool IsDarkenTransitioning, Covered;
        public int DarkenStarts;
        private Action coveredCallback, readyCallback;
        public void PrepareRoundStartWait() {}
        public void PlayRoundStartDarken(string text, Action covered, Action ready) { DarkenStarts++; Covered=false; IsDarkenTransitioning=true; coveredCallback=covered; readyCallback=ready; }
        public void FinishDarken() { Covered=true; coveredCallback(); IsDarkenTransitioning=false; readyCallback(); }
        public void PlayDrawTransition(Action covered, Action ready) { Covered=false; coveredCallback=covered; readyCallback=ready; }
        public void FinishDrawMidpoint() { Covered=true; coveredCallback(); }
        public void FinishDraw() { readyCallback(); }
        public void PlayRoundStartFadeOut() { Probe.UI.RevealCount++; Probe.Events.Add("reveal"); }
    }
}
