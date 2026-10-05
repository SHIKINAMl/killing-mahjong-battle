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
    static void Fresh() { BoardStateManager.Instance=new BoardStateManager(); ReactionController.Instance=new ReactionController(); KillingMahjong.Effects.ScreenFlash.Count=0; }
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
            u.PhaseController.ResetRound(); mid(); done(); Check(u.IsTransitioning && u.VisualController.Rebuilds==0,"old callback touched new round");
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
            var u=UI(); u.CurrentPhaseStatus=RoundStatus.HandSelection; var c=new GameUIHandSelectionController(u); c.Confirm(); var done=u.PhaseTransitionUI.CenterComplete; u.PhaseController.ResetMatch(); done(); Check(u.Sends.Count==0 && u.IsTransitioning,"previous match submit/unlock");
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
        Console.WriteLine($"All {passed} phase regressions passed (graphics/transport simulated).");
    }
}
namespace UnityEngine
{
    public class GameObject { public bool activeSelf=true; public void SetActive(bool v){activeSelf=v;} }
    public class MonoBehaviour { protected object StartCoroutine(IEnumerator e)=>e; }
    public static class Debug { public static void Log(string s){} public static void LogWarning(string s){} }
    public enum FindObjectsInactive { Include } public enum FindObjectsSortMode { None }
    public static class Object { public static T[] FindObjectsByType<T>(FindObjectsInactive a, FindObjectsSortMode b)=>Array.Empty<T>(); }
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
        public int LocalPlayerHp=1000, EnemyPlayerHp=2000, LocalPlayerSpecialVictoryCount, CurrentDoraId=-1, HpSaves, BeforeLocal, BeforeEnemy;
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
        public string LocalPlayerId="self"; public bool AgariProcessed; public int Agaris; public List<string> Events=new List<string>();
        public void RaisePhaseStatusChanged(RoundStatus s){Events.Add("phase:"+s);} public void RaiseAgari(bool local){Agaris++;Events.Add("agari");}
        public void RaiseTileDiscarded(int tile,bool local){Events.Add("tile:"+tile);} public void RaiseDraw(DrawPlayerData[] v){}
        public void RaiseAgariPendingReceived(AgariPendingData v){} public void RaiseNextRoundWaitingReceived(NextRoundWaitingData v){} public void RaiseGameEnded(GameEndInfo v){}
    }
    public static class ServerJsonParser
    {
        public static LiquidationData ParseLiquidationFromJson(string json) { using var doc=JsonDocument.Parse(json); if(!doc.RootElement.TryGetProperty("data",out var d)||!d.TryGetProperty("liquidation",out var l))return null; return JsonSerializer.Deserialize<LiquidationData>(l.GetRawText(),new JsonSerializerOptions{IncludeFields=true}); }
        public static bool TryParseGameEnd(string json,string local,out GameEndInfo info){info=null;return false;}
    }
}
namespace KillingMahjong.Network.Handlers { public interface IServerMessageHandler{} }
namespace KillingMahjong.UI
{
    public class Widget
    {
        public UnityEngine.GameObject gameObject=new UnityEngine.GameObject(); public int Starts,Stops,Stakes,BetShows,TextShows,Confirmations; public float LastDuration;
        public bool FirstRoundChromeHidden, IsHandSelectionConfirmed;
        public void StartTurnTimer(float seconds){Starts++;LastDuration=seconds;} public void StopTurnTimer(){Stops++;}
        public void ShowBettingPhase(int max,int hp,int sv,Action<int> done){BetShows++;} public void HideBettingPhase(bool instant=false){}
        public void SetPanelVisible(bool v){} public void SetSubmittedState(bool v){} public void SetSuppressedForTransition(bool v){}
        public void SetBackgroundRaycast(bool v){} public void ShowReadyBox(bool v){} public void DisplayWaits(List<int> v){}
        public void UpdateLayout(RoundStatus s){} public void UpdateContainerPosition(bool v){} public void UpdateWallHighlights(List<int> waits,bool v){} public void UpdateDiscardTurnIndicator(bool a,bool b){}
        public void Clear(){} public void SetHP(int v){} public void AddStakes(int a,int b){Stakes++;} public void SetVisible(bool v){} public void SetVitalsVisible(bool v){}
        public void Hide(){} public void ShowDora(int v){} public void CloseYakuList(){} public void UpdateTurnText(){} public void ShowText(string s){TextShows++;}
        public void ResetForNewRound(){} public void ConfirmHandSelectionComplete(){Confirmations++;} public T GetComponentInChildren<T>() where T:new()=>new T();
    }
    public class PhaseTransitionUI
    {
        public bool IsDarkenTransitioning; public int Prompts; public Action Midpoint,Complete,CenterComplete; public BettingCompletedInfo Info;
        public void PlayTransition(string t,Widget p,BettingCompletedInfo b,Action mid,Action done){Info=b;Midpoint=mid;Complete=done;}
        public void PlayCenterTextAnim(string t,float seconds,Action done){CenterComplete=done;} public void PlayPromptText(string t,float seconds){Prompts++;}
    }
    public class VisualProbe
    {
        GameUIManager manager; public int Rebuilds; public VisualProbe(GameUIManager m){manager=m;}
        public void RebuildAllTilesFromState(){Probe.Check(manager.CanRebuildBoard,"board rebuild outside allowed scope");Rebuilds++;}
    }
    public static class VoltageUI { public static void SetCanvasVisible(bool v){} }
    public partial class GameUIManager
    {
        public RoundStatus CurrentPhaseStatus; private bool isTransitioning; public bool IsTransitioning=>isTransitioning;
        public bool IsBusyWithTransition=>isTransitioning || (PhaseTransitionUI?.IsDarkenTransitioning??false);
        public bool IsTutorialMode,IsWaitDeductionUIEnabled; public int Rons;
        public Widget HandUI=new Widget(),WallUI=new Widget(),EnemyHandUI=new Widget(),EnemyWallUI=new Widget(),RiverUI=new Widget(),EnemyRiverUI=new Widget(),WaitUI=new Widget(),DialogueUI=new Widget(),BettingUI=new Widget(),PlayerInfoUI=new Widget(),EnemyInfoUI=new Widget(),AbilityUI=new Widget(),DoraDisplayUI=new Widget(),YakuListUI=new Widget(),ScoreGauge=new Widget(),BetPotUI=new Widget(),WaitDeduction=new Widget(),TutorialManager=new Widget(),RonAnimationUI=new Widget();
        private Widget abilityUI=>AbilityUI; public PhaseTransitionUI PhaseTransitionUI=new PhaseTransitionUI();
        public GameUIPhaseController PhaseController; public VisualProbe VisualController; public List<ActionPayload> Sends=new List<ActionPayload>();
        private readonly List<KeyValuePair<string,Action>> deferredActions=new List<KeyValuePair<string,Action>>();
        public int QueueCount=>deferredActions.Count;
        public GameUIManager(){PhaseController=new GameUIPhaseController(this); VisualController=new VisualProbe(this);}
        private void UpdateTurnIndicatorVisibility(){} private void EnsureFlushWatcher(){}
        public void Flush(){var snapshot=deferredActions.ToArray();deferredActions.Clear();foreach(var p in snapshot)p.Value();}
        public void SetCurrentPhaseStatus(RoundStatus v){CurrentPhaseStatus=v;} public void RecordHpHistory(int a,int b){}
        public void SendActionToServer(string action,ActionPayload data){Sends.Add(data);} public void ExecuteRonAction(){Rons++;}
    }
    public partial class GameUIPhaseController : UnityEngine.MonoBehaviour
    {
        private GameUIManager uiManager; private bool _hasSentNextRoundForCurrentPhase,_hasShownHandSelectionPrompt,_hasExecutedRonAnimation,_isCarryOverNextRound,_pendingDrawTransition; private int _currentRoundIndex=1;
        public int RoundStarts; public GameUIPhaseController(GameUIManager m){uiManager=m;}
        public void ResetMatch(){ResetPhasePresentation();ResetBettingTransition();} public void ResetRound(){ResetPhasePresentation();ResetBettingTransition();}
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
}
