using System;
using System.Collections.Generic;
using Harvest.Domain;
using Harvest.Application;
using Harvest.Language;
using System.Linq;
var world = new WorldSnapshot(SimulationRules.Default, 7, 1, 1, new[] {new TileState(new GridPosition(0,0))},
    new[] {new TractorState(1,new GridPosition(0,0),Heading.North,ImplementKind.Harvester,10,10,0,10)},Array.Empty<DepotState>(),1,1);
var scripts = new List<ScriptAssignment> {new ScriptAssignment(1,"invalid syntax remains recoverable")};
var save = new StoppedSessionSnapshot(SessionMode.Campaign,"spike-v1","starter-v1",world,0,scripts,1);
scripts.Clear();
if(save.Scripts.Count != 1 || save.World.Tick != 7) throw new Exception("Immutable stopped save failed");
void Reject(Action action) {try {action();} catch(ArgumentException) {return;} throw new Exception("Invalid save accepted");}
Reject(()=>new ScriptAssignment(1,new string('é',40000)));
Reject(()=>new StoppedSessionSnapshot(SessionMode.Sandbox,"spike-v1","starter-v1",world,1,save.Scripts,1));
Reject(()=>new StoppedSessionSnapshot(SessionMode.Campaign,"spike-v1","starter-v1",world,0,save.Scripts,2));
Reject(()=>new StoppedSessionSnapshot(SessionMode.Campaign,"spike-v1","starter-v1",world,0,new[]{save.Scripts[0],save.Scripts[0]},1));
Console.WriteLine("Application stopped DTO checks passed");

var sim = new FakeSimulation(world);
var factory = new FakeFactory();
var session = new SessionController(sim,factory,"spike-v1","starter-v1",1);
session.Draft = "original";
if(!session.Run()) throw new Exception("Run failed");
session.Pause(); var tick = session.World.Tick;
session.Pump(50); if(session.World.Tick != tick) throw new Exception("Paused pump advanced");
session.Draft = "changed"; session.Step();
if(session.World.Tick != tick+1 || session.Status != SessionStatus.Paused || session.ActiveSource != "original") throw new Exception("Step/draft isolation failed");
session.Run(); session.Pump(.1); if(session.World.Tick != tick+2) throw new Exception("Resume retained pause backlog");
session.Stop(); if(session.World.Tick != tick+2 || session.Draft != "changed") throw new Exception("Stop changed world/draft");
var captured = session.CaptureStopped();
factory.FailCompile = true; if(session.Run() || session.Diagnostics.Count != 1 || session.Status != SessionStatus.Stopped) throw new Exception("Compile failure state");
factory.FailCompile = false; session.Run();
var liveTick = session.World.Tick; sim.RejectRestore = true;
Reject(()=>session.RestoreStopped(captured));
if(session.Status != SessionStatus.Running || session.World.Tick != liveTick || factory.Driver.Stopped) throw new Exception("Rejected restore changed live session");
sim.RejectRestore = false; session.RestoreStopped(captured);
if(session.Status != SessionStatus.Stopped || !factory.Driver.Stopped || session.Draft != captured.Scripts[0].Draft) throw new Exception("Restore did not stop");
factory.Driver.Malformed = true; session.Run(); session.Pause(); session.Step();
if(session.Status != SessionStatus.Stopped || session.World.Tick != captured.World.Tick || session.Diagnostics[0].Code != "invalid_proposal") throw new Exception("Malformed proposal not faulted before advance");
if(ModePolicy.For(SessionMode.Campaign,0).Allows("while") || !ModePolicy.For(SessionMode.Sandbox,0).Allows("while") || !ModePolicy.For(SessionMode.Campaign,1).Allows("for")) throw new Exception("Mode gating");
Console.WriteLine("Application control/restore/gateway seam checks passed");
// Real interpreter compute-only loop: the fake world is only a scheduling seam.
var bounded = new SessionController(new FakeSimulation(world),new LanguageProgramFactory(),"spike-v1","starter-v1",1,SessionMode.Sandbox);
bounded.Draft="while True:\n    x = 1\n";
if(!bounded.Run()) throw new Exception("Real interpreter did not start");
bounded.Pause(); bounded.Step();
if(bounded.World.Tick != 8 || bounded.Status != SessionStatus.Paused) throw new Exception("Infinite loop did not yield one tick");
bounded.Stop(); if(bounded.World.Tick != 8 || bounded.Status != SessionStatus.Stopped) throw new Exception("Infinite loop stop");
bounded.Draft="wait(2)\nlog(sense().heading)\n"; bounded.Run(); bounded.Pause();
bounded.Step(); bounded.Step(); bounded.Step();
if(!bounded.Console.Contains("north")) throw new Exception("Scalar runtime sensor/wait mapping failed");
var modeController = new SessionController(new FakeSimulation(world),new LanguageProgramFactory(),"spike-v1","starter-v1",1);
var view = new Harvest.Presentation.ApplicationSessionView(modeController,mode => new StoppedSessionSnapshot(mode,"spike-v1","starter-v1",world,0,new[]{new ScriptAssignment(1,"")},1));
view.Draft="campaign draft"; view.SelectMode(Harvest.Presentation.FarmMode.Sandbox); view.Draft="sandbox draft";
view.SelectMode(Harvest.Presentation.FarmMode.Campaign);
if(view.Draft != "campaign draft" || !modeController.AchievementEligible) throw new Exception("Mode save isolation");
view.SelectMode(Harvest.Presentation.FarmMode.Sandbox);
if(view.Draft != "sandbox draft" || modeController.AchievementEligible) throw new Exception("Sandbox eligibility/isolation");
Console.WriteLine("Real Language scheduling and presentation mode isolation checks passed");
var rejectedWorld = new FakeSimulation(world) { RejectCommandFixture = true };
var rejectedSession = new SessionController(rejectedWorld,new LanguageProgramFactory(),"spike-v1","starter-v1",1,SessionMode.Sandbox);
rejectedSession.Draft="r = harvest()\nlog(r.reason)\n"; rejectedSession.Run(); rejectedSession.Pause(); rejectedSession.Step();
if(rejectedSession.Console.Contains("no_crop")) throw new Exception("Result visible during submission tick");
rejectedSession.Step(); if(!rejectedSession.Console.Contains("no_crop")) throw new Exception("Terminal result not delivered next tick");
rejectedSession.Stop(); rejectedSession.Run(); rejectedSession.Pause(); rejectedSession.Step();
if(rejectedWorld.Snapshot.Tractor(1).LastProcessedRequestId != 2) throw new Exception("Request IDs restarted on rerun");
Console.WriteLine("Real Language rejection routing/request sequence checks passed");
var realWorld = new WorldSnapshot(SimulationRules.Default,0,2,1,
    new[]{new TileState(new GridPosition(0,0),crop:CropKind.Wheat,growth:10),new TileState(new GridPosition(1,0),crop:CropKind.Wheat,growth:0)},
    new[]{new TractorState(1,new GridPosition(0,0),Heading.North,ImplementKind.Harvester,10,10,0,10)},
    new[]{new DepotState(1,new GridPosition(0,0),100,0,100)},1,1);
var real = new SessionController(new Simulation(realWorld),new LanguageProgramFactory(),"spike-v1","starter-v1",1);
real.Draft="harvest()\nunload()\nlog('done')\n";
if(!real.Run()) throw new Exception("Real harvest compile");
real.Pause(); real.Pump(10);
if(real.World.Tick != 0 || real.World.TileAt(new GridPosition(1,0)).Growth != 0) throw new Exception("Real pause crop freeze");
real.Step(); if(real.World.Tick != 1 || real.World.Tractor(1).Cargo != 1 || real.World.TileAt(new GridPosition(0,0)).Crop != CropKind.None || real.Status != SessionStatus.Paused) throw new Exception("Real harvest step");
real.Step(); if(real.World.Depots[0].WheatStock != 1 || real.CampaignChapter != 1) throw new Exception("Real unload/progression");
real.Step(); if(!real.Console.Contains("done") || real.World.Tick != 3 || real.World.TileAt(new GridPosition(1,0)).Growth != 3) throw new Exception("Real completion/growth");
var realSave=real.CaptureStopped(); real.RestoreStopped(realSave);
if(real.World.Tick != 3 || real.Status != SessionStatus.Stopped || real.Draft != realSave.Scripts[0].Draft) throw new Exception("Real stopped capture/restore");
Console.WriteLine("Concrete Domain + Language + Application harvest/pause/step/stop/progression passed");
// EXP-02: test through the actual view/session setter, including multibyte boundary.
var retained = view.Draft;
if(Harvest.Presentation.DraftEdit.TryApply(new string('é',32769),text => view.Draft=text,out var editError) ||
    view.Draft != retained || !editError.Contains("Previous draft retained")) throw new Exception("Oversized multibyte draft was not visibly rejected/retained");
if(!Harvest.Presentation.DraftEdit.TryApply(new string('é',32768),text => view.Draft=text,out editError) ||
    view.Draft != new string('é',32768) || editError != "") throw new Exception("Exact UTF-8 boundary was rejected or truncated");
retained = view.Draft;
if(Harvest.Presentation.DraftEdit.TryApply(new string('a',65537),text => view.Draft=text,out editError) || view.Draft != retained)
    throw new Exception("Oversized ASCII edit was truncated or committed");
if(!Harvest.Presentation.DraftEdit.TryApply("recovered draft",text => view.Draft=text,out editError) || editError != "" || view.Draft != "recovered draft")
    throw new Exception("Subsequent valid edit did not recover/clear warning");
Console.WriteLine("UTF-8 edit rejection, retained draft, exact limit and recovery checks passed");
StorageViewChecks.Run();
sealed class FakeFactory : IProgramFactory
{
    public bool FailCompile;
    public FakeDriver Driver = new FakeDriver();
    public ProgramStart CompileAndStart(string source,long actorId,Capabilities capabilities)
    {
        if(FailCompile) return new ProgramStart(null,new[]{new Diagnostic("syntax","Invalid source",new SourceSpan(0,1,1,1))});
        Driver.Stopped = false; return new ProgramStart(Driver,Array.Empty<Diagnostic>());
    }
}
sealed class FakeDriver : IProgramDriver
{
    public bool Stopped, Malformed;
    public ProgramFrame Resume(TickInput input)
    {
        foreach(var value in input.Sensor.Values) if(value is not long && value is not string && value is not bool) throw new Exception("Non-scalar sensor");
        return new ProgramFrame(ExecutionStatus.Ready,Malformed ? new ActionProposal(2,input.NextRequestId(),"harvest") : null,default,null,Array.Empty<string>());
    }
    public void Stop() {Stopped=true;}
}
sealed class FakeSimulation : ISimulation
{
    public WorldSnapshot Snapshot {get;private set;}
    public bool RejectRestore, RejectCommandFixture;
    public FakeSimulation(WorldSnapshot snapshot) {Snapshot=snapshot;}
    public SensorSnapshot Sense(long actorId) {var t=Snapshot.Tractor(actorId);return new SensorSnapshot(t.Position,t.Heading,false,CropKind.None,t.Fuel,t.Cargo,t.Implement);}
    public TickReport AdvanceTick(IEnumerable<CommandRequest> commands)
    {
        var batch = commands.ToArray();
        if(batch.Length > 0 && !RejectCommandFixture) throw new Exception("Test seam does not simulate gameplay");
        var request = batch.FirstOrDefault();
        var tractors = Snapshot.Tractors.Select(t => request == null ? t : new TractorState(t.ActorId,t.Position,t.Heading,t.Implement,t.Fuel,t.FuelCapacity,t.Cargo,t.CargoCapacity,request.RequestId));
        Snapshot=new WorldSnapshot(Snapshot.Rules,Snapshot.Tick+1,Snapshot.Width,Snapshot.Height,Snapshot.Tiles,tractors,Snapshot.Depots,Snapshot.RngState,Snapshot.NextActorId);
        return new TickReport(Snapshot,BatchStatus.Advanced,Reasons.None,
            request == null ? Array.Empty<SubmissionOutcome>() : new[]{new SubmissionOutcome(request,SubmissionStatus.Rejected,Reasons.NoCrop)},
            request == null ? Array.Empty<ActionResult>() : new[]{new ActionResult(request.ActorId,request.RequestId,false,Reasons.NoCrop,Snapshot.Tick)},
            Array.Empty<DomainEvent>());
    }
    public StopReport StopAtBoundary() => new StopReport(Snapshot,Array.Empty<ActionResult>(),Array.Empty<DomainEvent>());
    public void RestoreStopped(WorldSnapshot snapshot) {if(RejectRestore) throw new ArgumentException("Rejected");Snapshot=snapshot;}
}
