using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using KillingMahjong.UI;
using KillingMahjong.EngineData;
using KillingMahjong.Managers;
using KillingMahjong.Network;
using KillingMahjong.Network.Handlers;

// Production phase/confirmation/liquidation control flow is linked or extracted.
// Native Unity drawing, animation duration, transport and JSON fallback are simulated.
static class Probe
{
    static int passed;
    public static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Test(string name, Action run) { Fresh(); run(); passed++; Console.WriteLine("PASS " + name); }
    static void Fresh() { BoardStateManager.Instance=new BoardStateManager(); ReactionController.Instance=new ReactionController(); KillingMahjong.Effects.ScreenFlash.Count=0; NetworkMessageHandler.Instance=new NetworkMessageHandler(); UnityEngine.Time.frameCount=0; UnityEngine.Debug.Warnings=UnityEngine.Debug.Errors=0; }
    static GameUIManager UI(bool transition=true) { var ui=new GameUIManager(); if(!transition) ui.PhaseTransitionUI=null; return ui; }
    static void Repaint(GameUIManager ui) { for(int i=0;i<5;i++) ui.PhaseController.HandlePhaseVisibility(ui.CurrentPhaseStatus); }
    static BettingCompletedInfo Bet() => new BettingCompletedInfo { LocalBet=100, EnemyBet=200, LocalHpBefore=1000, EnemyHpBefore=2000, LocalHpAfter=900, EnemyHpAfter=1800, HasServerHealth=true };
    static string Settlement(bool discard, string winner="self") => JsonSerializer.Serialize(new { data = new { is_draw=false, is_win=true, tile=0, liquidation=new { winner_id=winner, loser_id=winner=="self"?"enemy":"self", winner_health=4567, loser_health=1234 } } });
    static void Main()
    {
        Test("hand entry starts once; repaint and duplicate phase cannot restart timer/prompt", () => {
            var u=UI(); u.PhaseController.UpdatePhaseStatus(RoundStatus.HandSelection); Repaint(u); u.PhaseController.UpdatePhaseStatus(RoundStatus.HandSelection);
            Check(u.PlayerInfoUI.Starts==1 && u.PlayerInfoUI.LastDuration==15 && ReactionController.Instance.HandStarts==1 && u.PhaseTransitionUI.Prompts==1 && KillingMahjong.Effects.ScreenFlash.Count==1,"hand entry repeated");
        });
        Test("betting repaint does not reopen input or restart measurement", () => {
            var u=UI(); u.PhaseController.UpdatePhaseStatus(RoundStatus.Betting); Repaint(u);
            Check(u.BettingUI.BetShows==1 && u.PlayerInfoUI.Starts==1 && ReactionController.Instance.BetStarts==1,"bet entry repeated");
        });
        Test("discard repaint preserves countdown; turn events start/stop timer", () => {
            var u=UI(); BoardStateManager.Instance.IsLocalTurn=true; u.PhaseController.UpdatePhaseStatus(RoundStatus.Discard); Repaint(u);
            Check(u.PlayerInfoUI.Starts==1 && u.PlayerInfoUI.LastDuration==10,"discard repaint reset countdown");
            BoardStateManager.Instance.IsLocalTurn=false; u.PhaseController.HandleDiscardTurnChanged(); Check(u.PlayerInfoUI.Stops==1,"enemy turn still timing");
            BoardStateManager.Instance.IsLocalTurn=true; u.PhaseController.HandleDiscardTurnChanged(); Check(u.PlayerInfoUI.Starts==2,"local turn not restarted");
        });
        Test("turn received during animation resumes only after unlock", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Discard; BoardStateManager.Instance.IsLocalTurn=true;
            u.SetIsTransitioning(true); u.PhaseController.HandleDiscardTurnChanged(); u.PhaseController.HandleDiscardTurnChanged(); Check(u.PlayerInfoUI.Starts==0,"timer leaked during lock");
            u.SetIsTransitioning(false); u.SetIsTransitioning(false); Check(u.PlayerInfoUI.Starts==1,"pending turn lost or repeated");
        });
        Test("deferred entry cannot be replaced by repaint", () => {
            var u=UI(); u.SetIsTransitioning(true); u.PhaseController.UpdatePhaseStatus(RoundStatus.HandSelection); Repaint(u);
            Check(u.QueueCount==2,"entry/repaint keys not independent"); u.SetIsTransitioning(false); u.Flush(); Check(u.PlayerInfoUI.Starts==1,"entry lost");
        });
        Test("obsolete entries skipped; dealing preparation retained", () => {
            var u=UI(); u.SetIsTransitioning(true); u.PhaseController.UpdatePhaseStatus(RoundStatus.Dealing); u.PhaseController.UpdatePhaseStatus(RoundStatus.HandSelection); u.PhaseController.UpdatePhaseStatus(RoundStatus.Betting);
            u.SetIsTransitioning(false); u.Flush(); Check(u.PhaseController.RoundStarts==1 && ReactionController.Instance.HandStarts==0 && u.BettingUI.BetShows==1,"stale entry affected new phase");
        });
        Test("previous round or match queued entries ignored", () => {
            var u=UI(); u.SetIsTransitioning(true); u.PhaseController.UpdatePhaseStatus(RoundStatus.HandSelection); u.PhaseController.ResetMatch(); u.SetIsTransitioning(false); u.Flush(); Check(u.PlayerInfoUI.Starts==0,"old match entry restarted timer");
        });
        Test("settlement and draw repaint cannot replay entry effects", () => {
            var u=UI(); BoardStateManager.Instance.LastIsLocalWin=true; u.PhaseController.UpdatePhaseStatus(RoundStatus.Agari); Repaint(u); Check(u.Rons==1,"ron replayed");
            u.PhaseController.UpdatePhaseStatus(RoundStatus.Draw); Repaint(u); Check(u.DialogueUI.TextShows==1,"draw dialogue repeated");
        });
        Test("tutorial phase repaint starts no normal countdown", () => {
            var u=UI(); u.IsTutorialMode=true; u.PhaseController.UpdatePhaseStatus(RoundStatus.HandSelection); Repaint(u); u.PhaseController.UpdatePhaseStatus(RoundStatus.Betting); u.PhaseController.UpdatePhaseStatus(RoundStatus.Discard);
            Check(u.PlayerInfoUI.Starts==0 && u.BettingUI.BetShows==0,"tutorial gained normal countdown");
        });
        Test("betting covered midpoint preserves input lock; callback effects run once", () => {
            var u=UI(); BoardStateManager.Instance.IsLocalTurn=true; u.CurrentPhaseStatus=RoundStatus.Betting; u.PhaseController.OnBettingCompleteFromServer(Bet());
            var p=u.PhaseTransitionUI; p.Midpoint(); p.Midpoint(); Check(u.IsTransitioning && !u.IsUpdatingCoveredBoard && !u.CanRebuildBoard && u.VisualController.Rebuilds==1 && u.PlayerInfoUI.Starts==0,"midpoint unlocked input or repeated");
            p.Complete(); p.Complete(); Check(!u.IsTransitioning && u.VisualController.Rebuilds==2 && u.PlayerInfoUI.Starts==1,"completion repeated or timer absent");
        });
        Test("early betting completion does not release blackout gate", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Betting; u.PhaseController.TriggerBettingAnimationPhase("round",Bet()); u.PhaseTransitionUI.Complete(); Check(u.IsTransitioning && u.VisualController.Rebuilds==0,"completion bypassed midpoint");
            u.PhaseTransitionUI.Midpoint(); u.PhaseTransitionUI.Complete(); Check(!u.IsTransitioning,"correct sequence did not finish");
        });
        Test("repeated public betting start cannot replace active callbacks and strand lock", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Betting; u.PhaseController.TriggerBettingAnimationPhase("round",Bet()); var mid=u.PhaseTransitionUI.Midpoint;
            u.PhaseController.TriggerBettingAnimationPhase("duplicate",Bet()); Check(u.QueueCount==0 && mid==u.PhaseTransitionUI.Midpoint,"active context replaced");
            mid(); u.PhaseTransitionUI.Complete(); Check(!u.IsTransitioning,"duplicate start stranded lock");
        });
        Test("bet result duplicate adds stakes only once; snapshot cannot drift", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Betting; var b=Bet(); u.SetIsTransitioning(true); u.PhaseController.OnBettingCompleteFromServer(b); b.LocalHpAfter=-1; u.PhaseController.OnBettingCompleteFromServer(b);
            u.SetIsTransitioning(false); u.Flush(); Check(u.BetPotUI.Stakes==1 && u.ScoreGauge.Stakes==1 && u.PhaseTransitionUI.Info.LocalHpAfter==900,"duplicate stakes or mutated snapshot");
        });
        Test("betting without transition UI reaches discard and unlocks", () => {
            var u=UI(false); u.CurrentPhaseStatus=RoundStatus.Betting; u.PhaseController.OnBettingCompleteFromServer(Bet()); Check(u.CurrentPhaseStatus==RoundStatus.Discard && !u.IsTransitioning && u.VisualController.Rebuilds==2,"missing UI deadlock");
        });
        Test("old betting callbacks cannot mutate new round or unlock its animation", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Betting; u.PhaseController.OnBettingCompleteFromServer(Bet()); var mid=u.PhaseTransitionUI.Midpoint; var done=u.PhaseTransitionUI.Complete;
            var other=u.BeginTransition("next-round-animation"); u.PhaseController.ResetRound(); mid(); done(); Check(u.IsTransitioning && u.VisualController.Rebuilds==0,"old callback touched new round"); other.Dispose();
            u.SetIsTransitioning(false); u.PhaseController.OnBettingCompleteFromServer(Bet()); Check(u.BetPotUI.Stakes==2,"new round result blocked");
        });
        Test("covered scope is nested and exception-safe without unlocking input", () => {
            var u=UI(); u.SetIsTransitioning(true); try { u.RunCoveredBoardUpdate(()=>{Check(u.CanRebuildBoard,"not enabled under cover"); u.RunCoveredBoardUpdate(()=>{}); Check(u.IsUpdatingCoveredBoard,"nested scope broke outer"); throw new InvalidOperationException();}); } catch(InvalidOperationException) {}
            Check(!u.IsUpdatingCoveredBoard && !u.CanRebuildBoard && u.IsTransitioning,"scope leaked/unlocked");
        });
        foreach(bool discardFirst in new[]{true,false}) Test("liquidation arrival order " + (discardFirst?"discard first":"round first"), () => {
            var n=new NetworkMessageHandler(); var d=new DiscardMessageHandler(); var r=new RoundLifecycleMessageHandler(); var json=Settlement(discardFirst);
            if(discardFirst) { d.Handle("discard_accepted",json,n); r.Handle("round_end",json,n); } else { r.Handle("round_end",json,n); d.Handle("discard_accepted",json,n); }
            var b=BoardStateManager.Instance; Check(n.Agaris==1 && b.HpSaves==1 && b.BeforeLocal==1000 && b.LocalPlayerHp==4567 && b.EnemyPlayerHp==1234,"double/missing/wrong settlement");
            if(discardFirst) Check(n.Events.SequenceEqual(new[]{"tile:0","phase:Agari","agari"}),"tile 0 omitted or ordered after phase");
        });
        Test("enemy liquidation maps server HP without local arithmetic", () => {
            var n=new NetworkMessageHandler(); new RoundLifecycleMessageHandler().Handle("round_end",Settlement(false,"enemy"),n); var b=BoardStateManager.Instance; Check(!b.LastIsLocalWin && b.LocalPlayerHp==1234 && b.EnemyPlayerHp==4567,"server HP incorrectly mapped");
        });
        Test("invalid liquidation does not consume later valid notification", () => {
            var n=new NetworkMessageHandler(); var d=new DiscardMessageHandler(); d.Handle("discard_accepted","{\"data\":{\"is_win\":true,\"tile\":0}}",n);
            Check(!n.AgariProcessed && n.Events.Count==0,"invalid data consumed settlement"); new RoundLifecycleMessageHandler().Handle("round_end",Settlement(false),n); Check(n.Agaris==1 && BoardStateManager.Instance.HpSaves==1,"later recovery lost");
        });
        Test("liquidation parser fallback preserves before HP", () => {
            var n=new NetworkMessageHandler(); Check(LiquidationMessageApplier.TryApply(null,Settlement(false),n),"fallback failed"); Check(BoardStateManager.Instance.HpSaves==1,"fallback missing pre-settlement HP");
        });
        Test("confirmation snapshots selection/waits and sends once", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; var c=new GameUIHandSelectionController(u); var waits=new[]{7,8}; c.Confirm(waits); var done=u.PhaseTransitionUI.CenterComplete;
            c.Indexes[0]=99; c.Tiles.Clear(); waits[0]=99; done(); done(); Check(u.Sends.Count==1 && u.Sends[0].hand_indexes[0]==1 && u.Sends[0].hand.Count==2 && BoardStateManager.Instance.CurrentWaitTiles.SequenceEqual(new[]{7,8}),"confirmation data drift/repeat");
        });
        Test("confirmation stale generation or phase cannot send/unlock", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; var c=new GameUIHandSelectionController(u); c.Confirm(); var done=u.PhaseTransitionUI.CenterComplete; var other=u.BeginTransition("other"); u.PhaseController.ResetMatch(); done(); Check(u.Sends.Count==0 && u.IsTransitioning,"previous match submit/unlock");
            c.Confirm(); done=u.PhaseTransitionUI.CenterComplete; u.CurrentPhaseStatus=RoundStatus.Betting; done(); Check(u.Sends.Count==0 && u.IsTransitioning,"stale phase submit/unlock");
        });
        Test("cancel/reselection invalidates pending confirmation", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; var c=new GameUIHandSelectionController(u); c.Confirm(); var old=u.PhaseTransitionUI.CenterComplete; c.Cancel(); c.Confirm(); old(); Check(u.Sends.Count==0 && u.IsTransitioning,"cancelled submission applied"); u.PhaseTransitionUI.CenterComplete(); Check(u.Sends.Count==1,"new submission lost");
        });
        Test("old dialog confirmation cannot submit selection from a new round", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; var c=new GameUIHandSelectionController(u); var click=c.DialogConfirmation();
            u.PhaseController.ResetRound(); click(); Check(u.Sends.Count==0 && !u.IsTransitioning,"old round dialog accepted");
            click=c.DialogConfirmation(); c.Cancel(); click(); Check(u.Sends.Count==0 && !u.IsTransitioning,"old selection dialog accepted");
            c.DialogConfirmation()(); u.PhaseTransitionUI.CenterComplete(); Check(u.Sends.Count==1,"current dialog blocked");
        });
        Test("confirmation fallback and tutorial branch retained", () => {
            var u=UI(false); u.CurrentPhaseStatus=RoundStatus.HandSelection; new GameUIHandSelectionController(u).Confirm(); Check(u.Sends.Count==1 && !u.IsTransitioning,"confirmation fallback failed");
            u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; u.IsTutorialMode=true; new GameUIHandSelectionController(u).Confirm(new[]{7},true); u.PhaseTransitionUI.CenterComplete(); Check(u.Sends.Count==0 && u.TutorialManager.Confirmations==1,"tutorial routing changed");
        });
        Test("overlapping owners release only their own ticket, including duplicate release", () => {
            int changed=0; var locks=new TransitionLockSet(()=>changed++); var a=locks.Acquire("a"); var b=locks.Acquire("b");
            a.Dispose(); a.Dispose(); Check(locks.IsLocked && b.IsActive && changed==1,"first owner released second"); b.Dispose(); Check(!locks.IsLocked && changed==2,"aggregate lock change wrong");
        });
        Test("same owner name still has independent tickets", () => {
            var locks=new TransitionLockSet(); var a=locks.Acquire("skill"); var b=locks.Acquire("skill"); b.Dispose(); Check(a.IsActive && locks.IsLocked,"same owner tokens conflated"); a.Dispose(); Check(!locks.IsLocked,"same owner stranded");
        });
        Test("reset invalidates old tickets without letting them release new ones", () => {
            var locks=new TransitionLockSet(); var old=locks.Acquire("old"); locks.Reset(); var current=locks.Acquire("new"); old.Dispose(); Check(!old.IsActive && current.IsActive && locks.IsLocked,"old generation released current"); current.Dispose();
        });
        Test("lock owner required; exception releases using scope", () => {
            var locks=new TransitionLockSet(); bool rejected=false; try{locks.Acquire("");}catch(ArgumentException){rejected=true;} Check(rejected && !locks.IsLocked,"unnamed owner accepted");
            try{using(locks.Acquire("throw")){throw new InvalidOperationException();}}catch(InvalidOperationException){} Check(!locks.IsLocked,"exception stranded lock");
        });
        Test("compatibility false cannot release a migrated owner", () => {
            var u=UI(); var owned=u.BeginTransition("owned"); u.SetIsTransitioning(true); u.SetIsTransitioning(true); u.SetIsTransitioning(false); Check(u.IsTransitioning && owned.IsActive,"legacy false unlocked owner"); owned.Dispose(); Check(!u.IsTransitioning,"legacy true acquired twice");
        });
        Test("network error cancels waiting request but preserves betting animation", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Betting; u.SkillController.WaitForMulligan(); var skill=u.SkillController; var n=new GameUINetworkHandler(u); var other=u.BeginTransition("betting-in-progress");
            n.Error(); Check(!skill.RequestPending && u.IsTransitioning && other.IsActive,"error released animation"); other.Dispose(); Check(!u.IsTransitioning,"request error stranded lock");
        });
        Test("skill request transfers continuously to its animation", () => {
            var u=UI(); u.SkillController.WaitForMulligan(); var r=u.SkillController.StartSkill(); Check(r.MoveNext(),"skill did not start"); Check(!u.SkillController.RequestPending && u.IsTransitioning,"transfer temporarily unlocked");
            Check(!r.MoveNext() && !u.IsTransitioning && u.VisualController.Rebuilds==1,"skill did not release/refresh");
        });
        Test("enemy skill response cannot release local pending request", () => {
            var u=UI(); u.SkillController.WaitForMulligan(); var r=u.SkillController.StartSkill("mulligan","enemy"); r.MoveNext(); Check(u.SkillController.RequestPending,"enemy response cleared local request");
            ((IDisposable)r).Dispose(); Check(u.IsTransitioning,"enemy completion cleared local request"); u.SkillController.CancelPendingSkillRequest(); Check(!u.IsTransitioning,"pending request stuck");
        });
        Test("skill coroutine disposal releases its lease and preserves another owner", () => {
            var u=UI(); var other=u.BeginTransition("other"); var r=u.SkillController.StartSkill("boost_hand"); r.MoveNext(); ((IDisposable)r).Dispose(); Check(u.IsTransitioning && other.IsActive,"skill cancellation unlocked another owner"); other.Dispose(); Check(!u.IsTransitioning,"disposed coroutine retained lock");
        });
        Test("skill cancellation invalidates active work without late refresh", () => {
            var u=UI(); var other=u.BeginTransition("other"); var r=u.SkillController.StartSkill("perspective"); r.MoveNext(); u.SkillController.CancelActiveTransitions(); Check(u.IsTransitioning && other.IsActive,"skill disable unlocked other owner"); r.MoveNext(); Check(u.VisualController.Rebuilds==0 && u.QueueCount==0,"cancelled skill refreshed new state"); other.Dispose();
        });
        Test("skill completion queues board refresh until other owner finishes", () => {
            var u=UI(); var other=u.BeginTransition("other"); var r=u.SkillController.StartSkill("assault"); r.MoveNext(); r.MoveNext(); Check(u.IsTransitioning && u.QueueCount==1 && u.VisualController.Rebuilds==0,"board rebuilt under another animation"); other.Dispose(); u.Flush(); Check(u.VisualController.Rebuilds==1,"final board refresh lost");
        });
        Test("betting completion cannot unlock or restore UI through another owner", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Betting; u.PhaseController.TriggerBettingAnimationPhase("round",Bet()); u.PhaseTransitionUI.Midpoint(); var other=u.BeginTransition("skill"); u.PhaseTransitionUI.Complete();
            Check(u.IsTransitioning && u.VisualController.Rebuilds==1 && u.QueueCount==1,"bet restored board through other lock"); other.Dispose(); u.Flush(); Check(u.VisualController.Rebuilds==2,"bet restore lost");
        });
        Test("manager disable cancels presentation and invalidates every old ticket", () => {
            var u=UI(); var old=u.BeginTransition("tile-move"); u.CurrentPhaseStatus=RoundStatus.HandSelection; u.HandSelectionController.Confirm(); u.SkillController.WaitForMulligan();
            u.DisableForTest();
            Check(!u.IsTransitioning && !old.IsActive && !u.SkillController.RequestPending,"disable left lock active"); var current=u.BeginTransition("new-match"); old.Dispose(); u.PhaseTransitionUI.CenterComplete(); Check(current.IsActive && u.Sends.Count==0,"old callbacks released new match"); current.Dispose();
        });
        Test("duplicate draw prepares/publicizes once and sends next round once", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Draw; BoardStateManager.Instance.CurrentWaitTiles.Add(7); BoardStateManager.Instance.CurrentEnemyWaitTiles.Add(8);
            u.PhaseController.HandleDraw(); var click=u.DialogueUI.NextRoundButton; u.PhaseController.HandleDraw(); Check(u.PhaseController.RoundIndex==2 && u.EnemyHandUI.Reveals==1 && u.PlayerInfoUI.ReadyShows==1,"duplicate draw side effects");
            click(); click(); Check(NetworkMessageHandler.Instance.NextRounds==1 && u.PhaseController.DrawTransitionPending && !u.WaitUI.gameObject.activeSelf && !u.EnemyWaitUI.gameObject.activeSelf,"draw OK repeat/lost carryover");
        });
        Test("early opponent readiness survives ready-box creation after draw animation", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Draw; var other=u.BeginTransition("last-discard"); u.PhaseController.HandleDraw();
            u.PhaseController.HandleNextRoundWaitingReceived(new NextRoundWaitingData{ready_players=new List<string>{"enemy"}}); other.Dispose(); u.Flush(); Check(u.EnemyInfoUI.Ready && !u.PlayerInfoUI.Ready,"early ready mark reset by ShowReadyBox");
        });
        Test("old next-round OK cannot confirm the following round", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Draw; u.PhaseController.HandleDraw(); var old=u.DialogueUI.NextRoundButton; u.PhaseController.ResetRound(); u.PhaseController.HandleDraw();
            old(); Check(NetworkMessageHandler.Instance.NextRounds==0 && !u.PlayerInfoUI.Ready,"old OK confirmed new round"); u.DialogueUI.NextRoundButton(); Check(NetworkMessageHandler.Instance.NextRounds==1,"new OK blocked");
        });
        Test("late ready notification ignored during live play", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Dealing; u.PhaseController.HandleNextRoundWaitingReceived(new NextRoundWaitingData{ready_players=new List<string>{"enemy"}});
            u.CurrentPhaseStatus=RoundStatus.Draw; u.PhaseController.HandleDraw(); Check(!u.EnemyInfoUI.Ready,"previous-round readiness carried into next ending");
        });
        Test("OK waits for overlapping animation; repeated clicks queue one confirmation", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Draw; u.PhaseController.HandleDraw(); var other=u.BeginTransition("other"); u.DialogueUI.NextRoundButton(); u.DialogueUI.NextRoundButton();
            Check(NetworkMessageHandler.Instance.NextRounds==0 && u.QueueCount==1,"OK bypassed input lock or queued twice"); other.Dispose(); u.Flush(); Check(NetworkMessageHandler.Instance.NextRounds==1,"delayed OK lost");
        });
        foreach(bool draw in new[]{true,false}) Test("server game-over result replaces next-round send: "+(draw?"draw":"agari"), () => {
            var u=UI(); u.CurrentPhaseStatus=draw?RoundStatus.Draw:RoundStatus.Agari; if(draw)u.PhaseController.HandleDraw();else u.PhaseController.ShowAgariWait();
            u.IsGameOver=true; u.DialogueUI.NextRoundButton(); u.DialogueUI.NextRoundButton(); u.TickResults(); Check(u.Results==1 && NetworkMessageHandler.Instance.NextRounds==0,"game-end OK sent next round/replayed result");
        });
        Test("missing dialogue UI automatically confirms draw and agari wait", () => {
            var u=UI(); u.DialogueUI=null; u.CurrentPhaseStatus=RoundStatus.Draw; u.PhaseController.HandleDraw(); Check(NetworkMessageHandler.Instance.NextRounds==1,"draw fallback stalled");
            u.PhaseController.ResetRound(); u.CurrentPhaseStatus=RoundStatus.Agari; u.PhaseController.ShowAgariWait(); Check(NetworkMessageHandler.Instance.NextRounds==2,"agari fallback stalled");
        });
        Test("ron completion is once and waits until other owner finishes", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Agari; var done=u.PhaseController.PrepareRonCompletion(); var other=u.BeginTransition("other"); done(); done(); Check(u.PhaseController.RoundIndex==1 && u.QueueCount==1 && u.IsTransitioning,"ron completion bypassed other owner");
            other.Dispose(); u.Flush(); done(); Check(u.PhaseController.RoundIndex==2 && u.PlayerInfoUI.ReadyShows==1,"ron completion repeated");
        });
        Test("old ron completion cannot clear a new round lock or increment its index", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Agari; var old=u.PhaseController.PrepareRonCompletion(); u.PhaseController.ResetRound(); u.ResetTransitionLocks(); var current=u.BeginTransition("new-round"); old(); Check(current.IsActive && u.PhaseController.RoundIndex==1,"stale ron completion affected new round"); current.Dispose();
        });
        Test("round ending rejects premature confirm and preserves local confirmation against older ready data", () => {
            var flow=new RoundEndCoordinator(); var ticket=flow.Begin(RoundEndCoordinator.Outcome.Draw); Check(!flow.Confirm(ticket) && flow.Begin(RoundEndCoordinator.Outcome.Agari)==null,"invalid ending stage accepted");
            flow.FinishPresentation(ticket); Check(flow.Confirm(ticket) && !flow.Confirm(ticket),"confirm not once"); flow.SetReady(false,true); Check(flow.LocalReady && flow.EnemyReady,"server readiness cleared local OK");
            flow.Reset(); var current=flow.Begin(RoundEndCoordinator.Outcome.Agari); Check(!flow.FinishPresentation(ticket) && !flow.Confirm(ticket) && flow.IsCurrent(current),"old ending ticket accepted");
        });
        Test("presentation cleanup is reverse order and once", () => {
            var calls=new List<int>(); var scope=new PresentationScope(); scope.AddCleanup(()=>calls.Add(1)); scope.AddCleanup(()=>calls.Add(2));
            scope.Dispose(); scope.Dispose(); Check(!scope.IsActive && calls.SequenceEqual(new[]{2,1}),"cleanup order or duplicate disposal");
        });
        Test("one cleanup failure cannot strand other resources", () => {
            int errors=0,cleaned=0; var scope=new PresentationScope(e=>errors++); scope.AddCleanup(()=>cleaned++); scope.AddCleanup(()=>throw new Exception("cleanup"));
            scope.Dispose(); Check(errors==1 && cleaned==1,"cleanup stopped after exception");
        });
        Test("late resource registration on cancelled presentation cleans immediately", () => {
            var scope=new PresentationScope(); scope.Dispose(); var owned=new UnityEngine.GameObject(); scope.Own(owned);
            Check(!owned.activeSelf && owned.DestroyCalls==1,"late resource leaked");
        });
        Test("presentation destroys only its registered objects", () => {
            var own=new UnityEngine.GameObject(); var other=new UnityEngine.GameObject(); var scope=new PresentationScope(); scope.Own(own); scope.Dispose(); scope.Dispose();
            Check(!own.activeSelf && own.DestroyCalls==1 && other.activeSelf && other.DestroyCalls==0,"foreign presentation destroyed");
        });
        Test("cancelled nested presentation cannot resume mutation or completion", () => {
            int mutations=0,finished=0,disposed=0; var scope=new PresentationScope(); var run=scope.Run(NestedProbe(()=>mutations++,()=>finished++,()=>disposed++));
            Check(run.MoveNext() && run.Current is IEnumerator,"nested coroutine not yielded"); var child=(IEnumerator)run.Current; Check(child.MoveNext() && mutations==1,"child did not enter");
            scope.Dispose(); Check(!child.MoveNext() && !run.MoveNext() && mutations==1 && finished==0 && disposed==1,"cancelled nested coroutine resumed");
        });
        Test("normal nested presentation finishes once with its finally", () => {
            int mutations=0,finished=0,disposed=0; using(var scope=new PresentationScope()) Drain(scope.Run(NestedProbe(()=>mutations++,()=>finished++,()=>disposed++)));
            Check(mutations==2 && finished==1 && disposed==1,"normal presentation lost completion");
        });
        Test("presentation set cancellation does not invalidate subsequent playback", () => {
            var set=new PresentationScopeSet(); var old=set.Begin(); var owned=new UnityEngine.GameObject(); old.Own(owned); set.CancelAll(); var current=set.Begin();
            old.Dispose(); Check(!old.IsActive && !owned.activeSelf && current.IsActive,"old cancellation affected replay"); current.Dispose();
        });
        Test("exception in presentation body still restores registered state", () => {
            float alpha=0.4f; var owned=new UnityEngine.GameObject(); bool caught=false;var scope=new PresentationScope();
            try { scope.AddCleanup(()=>alpha=0.4f);scope.Own(owned);alpha=0;Drain(scope.Run(FailingPresentation())); }
            catch(InvalidOperationException){caught=true;}
            Check(caught && !scope.IsActive && alpha==0.4f && !owned.activeSelf && owned.DestroyCalls==1,"exception stranded presentation without parent disposal");
        });
        Test("nested presentation cancellation restores shared state in acquisition reverse order", () => {
            bool suppressed=false;var set=new PresentationScopeSet();var first=set.Begin();bool firstBefore=suppressed;first.AddCleanup(()=>suppressed=firstBefore);suppressed=true;
            var second=set.Begin();bool secondBefore=suppressed;second.AddCleanup(()=>suppressed=secondBefore);set.CancelAll();
            Check(!suppressed,"overlapping restoration left shared state suppressed");
        });
        foreach(bool draw in new[]{true,false}) Test("late game_end after confirmed OK starts result once: "+draw, () => {
            var u=UI(); u.CurrentPhaseStatus=draw?RoundStatus.Draw:RoundStatus.Agari;
            if(draw) u.PhaseController.HandleDraw(); else u.PhaseController.ShowAgariWait();
            u.DialogueUI.NextRoundButton(); Check(NetworkMessageHandler.Instance.NextRounds==1,"next round not requested");
            u.ReceiveGameEnd(End()); u.TickResults(); u.ReceiveGameEnd(End()); u.TickResults();
            Check(u.Results==1 && u.LocalFinalScore==1500 && u.EnemyFinalScore==500,"late result lost or repeated");
        });
        Test("game_end during hand selection starts result without a next-round OK", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; u.ReceiveGameEnd(End());
            Check(u.Results==0,"result started inside network callback"); u.TickResults();
            Check(u.Results==1 && u.PlayerInfoUI.Stops==1 && NetworkMessageHandler.Instance.NextRounds==0,"hand-selection result stalled");
        });
        Test("game_end preserves ron presentation and confirmation order", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Agari; var done=u.PhaseController.PrepareRonCompletion();
            u.ReceiveGameEnd(End()); u.TickResults(); Check(u.Results==0,"result interrupted ron");
            done(); u.TickResults(); Check(u.Results==0,"result skipped OK");
            u.DialogueUI.NextRoundButton(); u.TickResults(); Check(u.Results==1 && NetworkMessageHandler.Instance.NextRounds==0,"confirmed end did not finish");
        });
        Test("game_end waits when settlement phase precedes presentation ticket", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.Draw; u.ReceiveGameEnd(End()); u.TickResults();
            Check(u.Results==0,"result overtook delayed draw presentation");
            u.PhaseController.HandleDraw(); u.TickResults(); Check(u.Results==0,"draw confirmation skipped");
            u.DialogueUI.NextRoundButton(); u.TickResults(); Check(u.Results==1,"draw result missing");
        });
        Test("result waits for actual lock and darkening", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; var lease=u.BeginTransition("skill");
            u.ReceiveGameEnd(End()); u.TickResults(); Check(u.Results==0,"live owner bypassed");
            lease.Dispose(); u.PhaseTransitionUI.IsDarkenTransitioning=true; u.TickResults(); Check(u.Results==0,"darkening bypassed");
            u.PhaseTransitionUI.IsDarkenTransitioning=false; u.TickResults(); Check(u.Results==1,"result failed after animation");
        });
        Test("missing controller and dialogue still allow a server-confirmed result", () => {
            var u=UI(); u.PhaseController=null; u.DialogueUI=null; u.ReceiveGameEnd(End()); u.TickResults(); Check(u.Results==1,"fallback result stalled");
        });
        Test("tutorial and absent end payload do not start automatic result", () => {
            var u=UI(); u.ReceiveGameEnd(null); u.ShowGameResult(); u.TickResults(); Check(!u.IsGameOver && u.Results==0,"invalid end accepted");
            u.IsTutorialMode=true; u.ReceiveGameEnd(End()); u.TickResults(); Check(u.Results==0,"tutorial scenario interrupted");
        });
        Test("new match clears pending result and final values", () => {
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; u.ReceiveGameEnd(End()); u.ResetGameResultState(); u.TickResults();
            Check(u.Results==0 && !u.IsGameOver && u.LocalFinalScore==0 && u.HistoryCount==0,"pending old result survived reset");
            u.ReceiveGameEnd(End()); u.TickResults(); Check(u.Results==1,"new match result suppressed");
        });
        Test("new match stops old momentum continuation before victory display", () => {
            var u=UI(); u.UseMomentum(); u.RecordHpHistory(2000,2000); u.ReceiveGameEnd(End()); u.TickResults();
            Check(u.Results==0 && u.ResultRoutineCount==1,"momentum did not defer victory");
            u.ResetGameResultState(); u.ResumeResultRoutines(); Check(u.Results==0,"old match displayed victory");
            u.ReceiveGameEnd(End()); u.TickResults(); Check(u.Results==1,"subsequent match could not display result");
        });
        Test("deferred actions wait one frame and preserve latest payload order", () => {
            var u=UI(); var log=new List<int>();
            u.DeferUntilIdle("a",()=>log.Add(1)); u.DeferUntilIdle("b",()=>log.Add(2)); u.DeferUntilIdle("a",()=>log.Add(3));
            u.PumpThisFrame(); Check(log.Count==0,"executed inside enqueue frame");
            u.Flush(); Check(log.SequenceEqual(new[]{3,2}) && u.QueueCount==0,"coalescing reordered payloads");
        });
        Test("timeout warns once without bypassing lock; release restores board execution", () => {
            var u=UI(); var lease=u.BeginTransition("long-animation");
            u.DeferUntilIdle("board",()=>u.VisualController.RebuildAllTilesFromState());
            for(int i=0;i<20;i++)u.AdvanceDeferredFrame(1f);
            Check(UnityEngine.Debug.Warnings==1 && u.QueueCount==1 && u.VisualController.Rebuilds==0 && lease.IsActive,"timeout bypassed lock or spammed warnings");
            lease.Dispose(); u.Flush(); Check(u.VisualController.Rebuilds==1 && u.QueueCount==0,"unlocked queue did not resume");
        });
        Test("queue stops between actions when an action acquires a new lock", () => {
            var u=UI(); TransitionLockSet.Lease lease=null; int applied=0;
            u.DeferUntilIdle("start",()=>lease=u.BeginTransition("new-animation")); u.DeferUntilIdle("after",()=>applied++);
            u.Flush(); Check(applied==0 && u.QueueCount==1,"snapshot crossed newly acquired lock");
            lease.Dispose();u.Flush();Check(applied==1,"remaining action lost");
        });
        Test("queued retry is bounded to one attempt per frame", () => {
            var u=UI();int attempts=0;Action retry=null;retry=()=>{attempts++;u.DeferUntilIdle("retry",retry);};
            u.DeferUntilIdle("retry",retry);u.Flush();Check(attempts==1 && u.QueueCount==1,"retry looped in one frame");
            u.Flush();Check(attempts==2 && u.QueueCount==1,"retry failed next frame");
        });
        Test("failure in one queued callback does not lose the next callback", () => {
            var u=UI();int applied=0;u.DeferUntilIdle("bad",()=>throw new InvalidOperationException("probe"));u.DeferUntilIdle("good",()=>applied++);
            u.Flush();Check(applied==1 && u.QueueCount==0 && UnityEngine.Debug.Errors==1,"exception stranded queue");
        });
        Test("disabled manager retains notifications and resumes latest payload through Update", () => {
            var u=UI();int value=0;u.BeginTransition("cancelled-on-disable");u.DeferUntilIdle("status",()=>value=1);
            u.DisableForTest();u.DeferUntilIdle("status",()=>value=2);u.AdvanceDeferredFrame(30f);
            Check(value==0 && u.QueueCount==1,"disabled manager ran or dropped notification");
            u.EnableForTest();u.TickResults();Check(value==2 && u.QueueCount==0,"reenable left stopped watcher");
        });
        Test("callback disabling UI leaves remaining queue for reenable", () => {
            var u=UI();int applied=0;u.DeferUntilIdle("disable",()=>u.DisableForTest());u.DeferUntilIdle("after",()=>applied++);
            u.Flush();Check(applied==0 && u.QueueCount==1,"ran after disable");u.EnableForTest();u.Flush();Check(applied==1,"remaining queue lost");
        });
        Test("queue reset during callback drops old work and delays new work", () => {
            var u=UI();int old=0,fresh=0;u.DeferUntilIdle("reset",()=>{u.ResetDeferredActions();u.DeferUntilIdle("new",()=>fresh++);});u.DeferUntilIdle("old",()=>old++);
            u.Flush();Check(old==0 && fresh==0 && u.QueueCount==1,"new-match reset leaked old work or ran new work immediately");
            u.Flush();Check(old==0 && fresh==1,"new queue did not execute");
        });
        Test("callback reentry cannot pump remaining actions recursively", () => {
            var u=UI();var log=new List<int>();u.DeferUntilIdle("outer",()=>{log.Add(1);u.PumpThisFrame();log.Add(2);});u.DeferUntilIdle("next",()=>log.Add(3));
            u.Flush();Check(log.SequenceEqual(new[]{1,2,3}),"recursive queue changed order");
        });
        Test("darkening blocks queue without an owner lock", () => {
            var u=UI();int applied=0;u.PhaseTransitionUI.IsDarkenTransitioning=true;u.DeferUntilIdle("wait",()=>applied++);u.AdvanceDeferredFrame(10f);
            Check(applied==0 && u.QueueCount==1,"darkening guard bypassed");u.PhaseTransitionUI.IsDarkenTransitioning=false;u.Flush();Check(applied==1,"darkening completion failed");
        });
        foreach (bool localWin in new[]{true,false})
        {
            foreach (string method in new[]{"hp_zero","cumulative_earned_points","unknown"})
            {
                Test($"actual result parser and winner selection: {method}, localWin={localWin}", () => {
                    var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection;
                    var b=BoardStateManager.Instance;
                    b.LocalCumulativeEarnedPoints=localWin?31000:25000;
                    b.EnemyCumulativeEarnedPoints=localWin?25000:31000;
                    // Cumulative winner deliberately has less HP, unlike normal outcomes.
                    int local=method=="cumulative_earned_points"?(localWin?13000:49200):(localWin?1000:0);
                    int enemy=method=="cumulative_earned_points"?(localWin?49200:13000):(localWin?0:1000);
                    string json=JsonSerializer.Serialize(new {type="game_end",victory_method=method,final_scores=new Dictionary<string,int>{{"other",enemy},{"self",local}}});
                    Check(ServerJsonParser.TryParseGameEnd(json,"self",out var info),"parse failed");
                    u.ReceiveGameEnd(info);u.TickResults();u.ReceiveGameEnd(info);u.TickResults();
                    Check(u.Results==1 && u.ResultType==(localWin?VictoryType.NormalVictory:VictoryType.NormalDefeat),"wrong or duplicate result");
                    Check(u.LocalFinalScore==local && u.EnemyFinalScore==enemy,"final HP reversed");
                });
            }
        }
        Console.WriteLine($"All {passed} phase regressions passed (graphics/transport simulated).");
        AuditServerOutcomes();
    }
    // Optional diagnostic: real server payloads plus the last client-side status.
    // Keep failures visible; do not encode today's incorrect result as expected behavior.
    static void AuditServerOutcomes()
    {
        string path=Environment.GetEnvironmentVariable("KM_OUTCOME_FIXTURES");
        if(string.IsNullOrEmpty(path))return;
        using var doc=JsonDocument.Parse(System.IO.File.ReadAllText(path));
        int failures=0;
        foreach(var item in doc.RootElement.EnumerateArray())
        {
            string name=item.GetProperty("name").GetString();
            var end=item.GetProperty("gameEnd");
            if(end.ValueKind==JsonValueKind.Null)
            {
                Console.WriteLine($"FAIL outcome {name}: HP reached zero but no game_end; phase={item.GetProperty("phase").GetString()}");
                failures++;
                if(!item.TryGetProperty("delayedGameEnd",out end) || end.ValueKind==JsonValueKind.Null)continue;
                name+=" (after next hand selection)";
            }
            Fresh();var u=UI();u.CurrentPhaseStatus=RoundStatus.HandSelection;
            var oldPoints=item.GetProperty("staleCumulative");
            BoardStateManager.Instance.LocalCumulativeEarnedPoints=oldPoints[0].GetInt32();
            BoardStateManager.Instance.EnemyCumulativeEarnedPoints=oldPoints[1].GetInt32();
            bool parsed=ServerJsonParser.TryParseGameEnd(end.GetRawText(),"self",out var info);
            if(parsed){u.ReceiveGameEnd(info);u.TickResults();}
            bool expected=item.GetProperty("expectedWin").GetBoolean();
            bool actual=u.ResultType==VictoryType.NormalVictory;
            bool ok=parsed && u.Results==1 && actual==expected;
            if(!ok)failures++;
            Console.WriteLine($"{(ok?"PASS":"FAIL")} outcome {name}: expectedWin={expected}, actualWin={actual}, resultCalls={u.Results}");
        }
        Console.WriteLine($"Outcome audit: {failures} failures (server payload replay; no live transport).");
        if(failures>0)Environment.ExitCode=1;
    }
    static GameEndInfo End()=>new GameEndInfo{LocalScoreFound=true,EnemyScoreFound=true,LocalScore=1500,EnemyScore=500};
    static IEnumerator NestedProbe(Action mutation,Action complete,Action disposed) {yield return ChildProbe(mutation,disposed);complete();}
    static IEnumerator ChildProbe(Action mutation,Action disposed) {try {mutation();yield return null;mutation();}finally{disposed();}}
    static IEnumerator FailingPresentation() {yield return null;throw new InvalidOperationException("presentation");}
    static void Drain(IEnumerator routine) {while(routine.MoveNext()) if(routine.Current is IEnumerator child) Drain(child);}
}
namespace UnityEngine
{
    public static class Time { public static int frameCount; public static float unscaledDeltaTime=1f/60f; }
    public static class Mathf { public static float Max(float a,float b)=>Math.Max(a,b); }
    public class Coroutine { public IEnumerator Routine; public bool Stopped; }
    public class WaitForSeconds { public WaitForSeconds(float seconds){} }
    public class GameObject { public bool activeSelf=true; public int DestroyCalls; public void SetActive(bool v){activeSelf=v;} }
    public class MonoBehaviour { protected object StartCoroutine(IEnumerator e)=>e; protected void StopAllCoroutines(){} }
    public static class Debug { public static int Warnings,Errors; public static void Log(string s){} public static void LogWarning(string s){Warnings++;} public static void LogError(string s){Errors++;} }
    public enum FindObjectsInactive { Include } public enum FindObjectsSortMode { None }
    public static class Object { public static void Destroy(GameObject g){g.DestroyCalls++;} public static T[] FindObjectsByType<T>(FindObjectsInactive a, FindObjectsSortMode b)=>Array.Empty<T>(); }
    public static class JsonUtility { public static T FromJson<T>(string json)=>JsonSerializer.Deserialize<T>(json,new JsonSerializerOptions{IncludeFields=true}); }
}
namespace UnityEngine.UI { public class LayoutGroup { public bool enabled; } }
namespace KillingMahjong.Effects
{
    public static class ScreenFlash { public static int Count; public static void Play(bool playSound){Count++;} }
    public class TileClatterEffect { public UnityEngine.GameObject gameObject=new UnityEngine.GameObject(); }
}
namespace KillingMahjong.Managers
{
    public class BoardStateManager
    {
        public static BoardStateManager Instance;
        public const int CumulativeVictoryPoints=30000; public int LocalCumulativeEarnedPoints,EnemyCumulativeEarnedPoints;
        public int LocalPlayerHp=1000, EnemyPlayerHp=2000, LocalPlayerSpecialVictoryCount, CurrentDoraId=-1, HpSaves, BeforeLocal, BeforeEnemy;
        public List<int> CurrentHandTiles=new List<int>(), CurrentEnemyHandTiles=new List<int>(), CurrentEnemyWaitTiles=new List<int>(); public void SortTileIds(List<int> tiles){tiles.Sort();}
        public bool IsLocalTurn, LastIsLocalWin; public LiquidationData LastLiquidationData; public List<int> CurrentWaitTiles=new List<int>();
        public void RememberHpBeforeLiquidation(){HpSaves++; BeforeLocal=LocalPlayerHp; BeforeEnemy=EnemyPlayerHp;}
        public void UpdateHp(int a,int b){LocalPlayerHp=a;EnemyPlayerHp=b;} public void SetLocalTurn(bool b){IsLocalTurn=b;}
        public void SetLocalState(object a,object b,List<int> waits){CurrentWaitTiles=waits;} public void FireRebuildEvent(){} public void ClearWaitTiles(){CurrentWaitTiles.Clear();}
    }
    public class ReactionController
    {
        public static ReactionController Instance; public int HandStarts,BetStarts;
        public void StartHandSelectionTimer(){HandStarts++;} public void StartBetPhaseTimer(){BetStarts++;}
        public void StopHandSelectionTimer(bool v){} public void CheckAndPlayBetReaction(int a,int b,bool c){} public void SetPlayerHp(int v){} public void SetEnemyHp(int v){}
        public void SetCurrentRound(int v){} public void CheckAndPlayDrawReaction(){} public void HandleRoundStart(int v){}
        public void HandleGameEnd(bool local){}
    }
    public class AudioManager { public static AudioManager Instance; public void UpdateBgmIntensityFromHp(int a,int b){} }
    public class PhaseManager { public static PhaseManager Instance; public void ChangeRoundStatus(RoundStatus v){} }
}
namespace KillingMahjong.Network
{
    public class ActionPayload { public int bet_amount,amount; public List<int> hand_indexes,hand; }
    public class NextRoundWaitingMessage { public NextRoundWaitingData data; }
    public class NextRoundWaitingData { public int ready_count; public List<string> ready_players; }
    public class NetworkMessageHandler
    {
        public static NetworkMessageHandler Instance; public int NextRounds; public void SendActionToServer(string action,ActionPayload payload){if(action=="next_round")NextRounds++;}
        public event Action<StatusData> OnStatusReceived;
        public string LocalPlayerId="self"; public bool AgariProcessed; public int Agaris; public List<string> Events=new List<string>();
        public void RaisePhaseStatusChanged(RoundStatus s){Events.Add("phase:"+s);} public void RaiseAgari(bool local){Agaris++;Events.Add("agari");}
        public void RaiseTileDiscarded(int tile,bool local){Events.Add("tile:"+tile);} public void RaiseDraw(DrawPlayerData[] v){}
        public void RaiseAgariPendingReceived(AgariPendingData v){} public void RaiseNextRoundWaitingReceived(NextRoundWaitingData v){} public void RaiseGameEnded(GameEndInfo v){}
    }
    public static partial class ServerJsonParser
    {
        public static LiquidationData ParseLiquidationFromJson(string json) { using var doc=JsonDocument.Parse(json); if(!doc.RootElement.TryGetProperty("data",out var d)||!d.TryGetProperty("liquidation",out var l))return null; return JsonSerializer.Deserialize<LiquidationData>(l.GetRawText(),new JsonSerializerOptions{IncludeFields=true}); }
    }
}
namespace KillingMahjong.Network.Handlers { public interface IServerMessageHandler{} }
namespace KillingMahjong.UI
{
    public class Widget
    {
        public UnityEngine.GameObject gameObject=new UnityEngine.GameObject(); public int Starts,Stops,Stakes,BetShows,TextShows,Confirmations; public float LastDuration;
        public bool FirstRoundChromeHidden, IsHandSelectionConfirmed;
        public void StopAllCoroutines(){} public void CancelPresentation(){}
        public bool Ready; public int ReadyShows,Reveals,AnimationCalls; public Action NextRoundButton;
        public void HideNextRoundButton(){} public void StopHeartbeatEffect(){} public void ShowMomentum(List<int> a,List<int> b){}
        public VictoryType ResultType; public void PlayAnimation(VictoryType type,int local,int enemy){AnimationCalls++;ResultType=type;}
        public void StartTurnTimer(float seconds){Starts++;LastDuration=seconds;} public void StopTurnTimer(){Stops++;}
        public void ShowBettingPhase(int max,int hp,int sv,Action<int> done){BetShows++;} public void HideBettingPhase(bool instant=false){}
        public void SetPanelVisible(bool v){} public void SetSubmittedState(bool v){} public void SetSuppressedForTransition(bool v){}
        public void SetBackgroundRaycast(bool v){} public void ShowReadyBox(bool v){if(v){ReadyShows++;Ready=false;}} public void DisplayWaits(List<int> v){}
        public void SetReadyCheck(bool v){Ready=v;} public void ShowNextRoundButton(Action a){NextRoundButton=a;} public void SortHandSlots(){} public void RevealAllHands(object r){Reveals++;}
        public void UpdateLayout(RoundStatus s){} public void UpdateContainerPosition(bool v){} public void UpdateWallHighlights(List<int> waits,bool v){} public void UpdateDiscardTurnIndicator(bool a,bool b){}
        public void Clear(){} public void SetHP(int v){} public void AddStakes(int a,int b){Stakes++;} public void SetVisible(bool v){} public void SetVitalsVisible(bool v){}
        public void Hide(){} public void ShowDora(int v){} public void CloseYakuList(){} public void UpdateTurnText(){} public void ShowText(string s){TextShows++;}
        public void ResetForNewRound(){} public void ConfirmHandSelectionComplete(){Confirmations++;} public T GetComponentInChildren<T>() where T:new()=>new T();
    }
    public class PhaseTransitionUI
    {
        public bool IsDarkenTransitioning; public int Prompts; public Action Midpoint,Complete,CenterComplete; public BettingCompletedInfo Info;
        public void CancelTransitions(){IsDarkenTransitioning=false;}
        public void CancelSkillPresentations(){}
        public void PlayTransition(string t,Widget p,BettingCompletedInfo b,Action mid,Action done){Info=b;Midpoint=mid;Complete=done;}
        public void PlayCenterTextAnim(string t,float seconds,Action done){CenterComplete=done;} public void PlayPromptText(string t,float seconds){Prompts++;}
    }
    public class VisualProbe
    {
        GameUIManager manager; public int Rebuilds; public VisualProbe(GameUIManager m){manager=m;}
        public void CancelSkillPresentations(){}
        public void RebuildAllTilesFromState(){Probe.Check(manager.CanRebuildBoard,"board rebuild outside allowed scope");Rebuilds++;}
    }
    public static class VoltageUI { public static void SetCanvasVisible(bool v){} }
    public enum VictoryType { NormalVictory, NormalDefeat }
    public partial class GameUIManager
    {
        public RoundStatus CurrentPhaseStatus; private bool isTransitioning; public bool IsTransitioning=>isTransitioning;
        public bool isActiveAndEnabled=true; public bool IsBusyWithTransition=>isTransitioning || (PhaseTransitionUI?.IsDarkenTransitioning??false);
        public bool IsTutorialMode,IsWaitDeductionUIEnabled; public int Rons;
        public bool IsGameOver; public int Results=>victoryUI.AnimationCalls; public Widget EnemyWaitUI=new Widget(); public object TileResourceManager;
        public int LocalFinalScore,EnemyFinalScore; private GameEndInfo lastGameEndInfo;
        private List<int> playerHpHistory=new List<int>(),enemyHpHistory=new List<int>(); public int HistoryCount=>playerHpHistory.Count;
        private Widget playerInfoUI=>PlayerInfoUI; private Widget matchMomentumUI; private Widget victoryUI=new Widget();
        public VictoryType ResultType=>victoryUI.ResultType; public void UseMomentum(){matchMomentumUI=new Widget();}
        private List<UnityEngine.Coroutine> resultRoutines=new List<UnityEngine.Coroutine>(); public int ResultRoutineCount=>resultRoutines.Count;
        private UnityEngine.Coroutine StartCoroutine(IEnumerator routine){var c=new UnityEngine.Coroutine{Routine=routine};resultRoutines.Add(c);routine.MoveNext();return c;}
        private void StopCoroutine(UnityEngine.Coroutine c){c.Stopped=true;}
        public void ResumeResultRoutines(){foreach(var c in resultRoutines) if(!c.Stopped)c.Routine.MoveNext();}
        public void ReceiveGameEnd(GameEndInfo info){HandleGameEnded(info);} public void TickResults(){Update();}
        public Widget HandUI=new Widget(),WallUI=new Widget(),EnemyHandUI=new Widget(),EnemyWallUI=new Widget(),RiverUI=new Widget(),EnemyRiverUI=new Widget(),WaitUI=new Widget(),DialogueUI=new Widget(),BettingUI=new Widget(),PlayerInfoUI=new Widget(),EnemyInfoUI=new Widget(),AbilityUI=new Widget(),DoraDisplayUI=new Widget(),YakuListUI=new Widget(),ScoreGauge=new Widget(),BetPotUI=new Widget(),WaitDeduction=new Widget(),TutorialManager=new Widget(),RonAnimationUI=new Widget();
        private Widget abilityUI=>AbilityUI; public PhaseTransitionUI PhaseTransitionUI=new PhaseTransitionUI();
        public GameUIPhaseController PhaseController; public VisualProbe VisualController; public List<ActionPayload> Sends=new List<ActionPayload>(); public GameUIHandSelectionController HandSelectionController; public GameUISkillController SkillController;
        public int QueueCount=>deferredActions.Count;
        public GameUIManager(){PhaseController=new GameUIPhaseController(this); VisualController=new VisualProbe(this); HandSelectionController=new GameUIHandSelectionController(this); SkillController=new GameUISkillController(this);}
        private void UpdateTurnIndicatorVisibility(){}
        public void Flush(){AdvanceDeferredFrame(0f);}
        public void AdvanceDeferredFrame(float delta){UnityEngine.Time.frameCount++;ProcessDeferredActions(delta);}
        public void PumpThisFrame(float delta=0f){ProcessDeferredActions(delta);}
        public void SetCurrentPhaseStatus(RoundStatus v){CurrentPhaseStatus=v;}
        public void SendActionToServer(string action,ActionPayload data){Sends.Add(data);} public void ExecuteRonAction(){Rons++;}
        public void DisableForTest(){isActiveAndEnabled=false;OnDisable();}
        public void EnableForTest(){isActiveAndEnabled=true;}
    }
    public partial class GameUIPhaseController : UnityEngine.MonoBehaviour
    {
        private GameUIManager uiManager; private bool _hasSentNextRoundForCurrentPhase,_hasShownHandSelectionPrompt,_hasExecutedRonAnimation,_isCarryOverNextRound; private int _currentRoundIndex=1;
        public int RoundStarts; public GameUIPhaseController(GameUIManager m){uiManager=m;}
        private readonly RoundStartCoordinator roundStart=new RoundStartCoordinator(); private TransitionLockSet.Lease ronTransition; private TransitionLockSet.Lease roundStartTransition;
        public void ResetMatch(){ResetRound();} public void ResetRound(){ResetPhasePresentation();ResetBettingTransition();roundEnd.Reset();_hasSentNextRoundForCurrentPhase=false;}
        public int RoundIndex=>_currentRoundIndex; public bool DrawTransitionPending=>_pendingDrawTransition;
        private void OnScoreSettlementComplete(RoundEndCoordinator.Ticket ticket,bool local){ShowNextRoundWait(ticket);}
        public Action PrepareRonCompletion(){var ticket=roundEnd.Begin(RoundEndCoordinator.Outcome.Agari);var lease=uiManager.BeginTransition("ron");return ()=>CompleteRonPresentation(ticket,lease,true);}
        public void ShowAgariWait(){var ticket=roundEnd.Begin(RoundEndCoordinator.Outcome.Agari);if(ticket!=null){RevealRoundEndHands();ShowNextRoundWait(ticket);}}
        private void SetReadyBadgesSuppressed(bool v){} private void ApplyPhaseReadyMarks(RoundStatus v){} private void ResetPhaseReadyMarks(){} private void HideReadyBoxes(){}
        private void StartRoundStartTransition(string title,bool draw){RoundStarts++;} private IEnumerator ShowReadyBadgesAfterBettingPanelSlideOut(){yield break;}
    }
    public partial class GameUIHandSelectionController
    {
        private GameUIManager uiManager; private List<int> _pendingHandIndexes=new List<int>{1,2},_pendingHandTiles=new List<int>{3,4}; private bool _autoConfirmNextHandSelection;
        public List<int> Indexes=>_pendingHandIndexes; public List<int> Tiles=>_pendingHandTiles;
        public GameUIHandSelectionController(GameUIManager m){uiManager=m;} public void Confirm(int[] waits=null,bool tutorial=false){ConfirmSelection(waits,tutorial);} public void Cancel(){CancelSelectionConfirmation(true);}
        public Action DialogConfirmation()=>GuardSelectionConfirmation(()=>ConfirmSelection());
    }
    public partial class GameUISkillController : UnityEngine.MonoBehaviour
    {
        private GameUIManager uiManager; private TransitionLockSet.Lease pendingMulligan;
        private readonly HashSet<TransitionLockSet.Lease> skillTransitions=new HashSet<TransitionLockSet.Lease>();
        private readonly HashSet<Action<StatusData>> skillStatusHandlers=new HashSet<Action<StatusData>>();
        private object _lastMulliganOutSlotRt; private int _lastMulliganOutTileId,_lastMulliganTargetIndex;
        private Widget _mulliganSwapAnimator;
        public GameUISkillController(GameUIManager u){uiManager=u;} private void CancelSkillSelection(){}
        public void WaitForMulligan(){pendingMulligan=uiManager.BeginTransition("mulligan-request");}
        public bool RequestPending=>pendingMulligan?.IsActive??false;
        public IEnumerator StartSkill(string type="mulligan",string player="self")=>HandleSkillCastedRoutine(new SkillCastedData{skillType=type,player_id=player});
        private IEnumerator HandleSkillEffectsRoutine(SkillCastedData data,TransitionLockSet.Lease lease){yield return null;}
    }
    public partial class GameUINetworkHandler
    {
        private GameUIManager uiManager; public GameUINetworkHandler(GameUIManager u){uiManager=u;} public void Error(){HandleError("error");}
    }
    public class LoadingManager { public static LoadingManager Instance; public void ForceHide(){} }
}
namespace KillingMahjong.EngineData { public class StatusData{} public class SkillCastedData { public string skillType,player_id; } }
