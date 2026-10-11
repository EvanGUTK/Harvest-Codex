using System;
using System.Linq;
using Harvest.Application;
using Harvest.Domain;
using Harvest.Language;
using Harvest.Presentation;
internal static class StorageViewChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Run()
    {
        var world = new WorldSnapshot(SimulationRules.Default,0,1,1,
            new[]{new TileState(new GridPosition(0,0),crop:CropKind.Wheat,growth:10)},
            new[]{new TractorState(1,new GridPosition(0,0),Heading.North,ImplementKind.Harvester,10,10,0,10)},
            new[]{new DepotState(1,new GridPosition(0,0),100,0,100)},1,1);
        var controller = new SessionController(new Simulation(world),new LanguageProgramFactory(),"spike-v1","starter-v1",1);
        StoppedSessionSnapshot Create(SessionMode mode) => new StoppedSessionSnapshot(mode,"spike-v1","starter-v1",world,0,new[]{new ScriptAssignment(1,"")},1);
        var withoutStorage = new ApplicationSessionView(controller,Create);
        Check(!withoutStorage.ReadStorage().Available && !withoutStorage.ReadStorage().CanSave && !withoutStorage.ReadStorage().CanLoad,"Optional storage compatibility");
        var storage = new RecordingStorage(controller);
        var view = new ApplicationSessionView(controller,Create,storage);
        view.Draft="harvest()\nunload()\n";
        var referenceDraft = view.Draft;
        var chapterZero = view.ReadReference();
        Check(!chapterZero.Any(e=>e.Id=="for" || e.Id=="while"),"Locked reference leaked");
        foreach(var entry in chapterZero) Check(LanguageCompiler.Compile(entry.Example,ModePolicy.For(controller.Mode,controller.CampaignChapter)).Program != null,"Available example cannot compile");
        Check(view.Draft==referenceDraft && controller.World.Tick==0,"Reference changed source/world");
        view.Run(); view.Pause(); view.Step(); view.Step();
        Check(view.ReadReference().Any(e=>e.Id=="for") && !view.ReadReference().Any(e=>e.Id=="while"),"Progression reference failed to refresh");
        view.Run(); long liveTick=controller.World.Tick;
        view.RequestLoad(); view.ConfirmReplacement();
        Check(storage.Loads==0 && controller.Status==SessionStatus.Running && controller.World.Tick==liveTick,"Active load changed session");
        view.Save();
        Check(storage.Saves==1 && controller.Status==SessionStatus.Stopped && controller.World.Tick==liveTick,"Save did not use stop boundary");
        view.Draft="keep this draft";
        view.RequestLoad();
        Check(storage.Loads==0 && view.ReadStorage().CanConfirm && view.ReadStorage().Confirmation.Contains("farm and draft"),"Load executed before explicit confirmation");
        view.CancelReplacement(); view.ConfirmReplacement();
        Check(storage.Loads==0 && view.Draft=="keep this draft","Cancellation replaced draft");
        view.RequestLoad(); view.ConfirmReplacement();
        Check(storage.Loads==1 && view.ReadStorage().RecoveryChoices.Count==1,"Recovery choices not exposed");
        var oldChoice=view.ReadStorage().RecoveryChoices[0];
        view.RequestRecovery(oldChoice.Id);
        Check(storage.Recovers==0 && view.ReadStorage().CanConfirm,"Recovery did not await confirmation");
        view.CancelReplacement();
        Check(view.ReadStorage().RecoveryChoices.Count==1,"Cancel lost explicit recovery choices");
        view.RequestRecovery(oldChoice.Id); view.ConfirmReplacement();
        Check(storage.Recovers==1 && view.ReadStorage().RecoveryChoices.Count==0 && view.Draft=="keep this draft","Recovery altered source without service restoration");
        view.RequestRecovery(oldChoice.Id); view.ConfirmReplacement();
        Check(storage.Recovers==1,"Replayed recovery ID reached storage");
        view.RequestLoad(); view.ConfirmReplacement();
        oldChoice=view.ReadStorage().RecoveryChoices[0]; view.RequestRecovery(oldChoice.Id);
        view.SelectMode(FarmMode.Sandbox);
        Check(!view.ReadStorage().CanConfirm && view.ReadStorage().RecoveryChoices.Count==0 && storage.Offer==null,"Mode switch did not invalidate confirmations/offers");
        view.ConfirmReplacement(); view.RequestRecovery(oldChoice.Id);
        Check(storage.Recovers==1 && controller.Mode==SessionMode.Sandbox,"Cross-mode recovery touched session");
        Check(view.ReadReference().Any(e=>e.Id=="while") && view.ReadReference().Any(e=>e.Id=="action-results"),"Sandbox reference unavailable");
        foreach(var entry in view.ReadReference()) Check(LanguageCompiler.Compile(entry.Example,Capabilities.All).Program != null,"Sandbox reference example invalid");
        view.RequestLoad();
        controller.RestoreStopped(Create(SessionMode.Campaign)); // External replacement also invalidates on read/confirm.
        view.ConfirmReplacement();
        Check(storage.Loads==2 && !view.ReadStorage().CanConfirm,"Confirmation failed current-mode recheck");
        view.Draft="wait(5)\n"; view.RequestLoad();
        Check(view.ReadStorage().CanConfirm,"Stopped load confirmation missing");
        controller.Run(); long unchanged=controller.World.Tick;
        view.ConfirmReplacement();
        Check(storage.Loads==2 && controller.Status==SessionStatus.Running && controller.World.Tick==unchanged && view.Draft=="wait(5)\n", "Pending confirmation ignored new running state");
        view.Stop(); storage.FailSave=true; view.Save();
        Check(view.ReadStorage().Code=="save_failed" && view.ReadStorage().Message.Contains("remains stopped") && controller.Status==SessionStatus.Stopped,"Save failure was not visible/stopped");
        // Returned read-only choices survive later UI invalidation; consumers cannot mutate the offer.
        var choices = new[]{new RecoveryChoice(new string('a',32),"Previous save")};
        var frozen = new StorageViewState(true,true,"recovery_available","Choose explicitly","",choices);
        choices[0]=new RecoveryChoice(new string('b',32),"Interrupted save");
        Check(frozen.RecoveryChoices[0].Id==new string('a',32),"Storage view state mutable");
        var delayedRules = new SimulationRules("delayed-v1",new ActionRule(3,2),world.Rules.Turn,world.Rules.Harvest,world.Rules.Unload,world.Rules.Refuel,10,1,1);
        var delayedWorld = new WorldSnapshot(delayedRules,0,2,1,new[]{new TileState(new GridPosition(0,0)),new TileState(new GridPosition(1,0))},
            new[]{new TractorState(1,new GridPosition(0,0),Heading.East,ImplementKind.Harvester,10,10,0,10)},Array.Empty<DepotState>(),1,1);
        var delayed = new SessionController(new Simulation(delayedWorld),new LanguageProgramFactory(),"delayed-v1","starter-v1",1);
        var delayedStore=new RecordingStorage(delayed);
        var delayedView=new ApplicationSessionView(delayed,_=>throw new Exception("Mode factory not needed"),delayedStore);
        delayedView.Draft="move_forward()\n"; delayedView.Run(); delayedView.Pump(.1);
        Check(delayed.World.PendingActions.Count==1 && delayed.World.Tractor(1).Fuel==8,"Real pending reservation missing");
        delayedView.Save();
        Check(delayed.Status==SessionStatus.Stopped && delayed.World.Tick==1 && delayed.World.PendingActions.Count==0 && delayed.World.Tractor(1).Fuel==10 && delayed.World.Tractor(1).Position.X==0,"Save control did not stop/cancel/refund without an extra tick");
        Console.WriteLine("Storage confirmation/recovery/mode and current-policy reference checks passed");
    }
    private sealed class RecordingStorage : ISessionStorage
    {
        private readonly SessionController controller;
        private int sequence;
        public int Saves,Loads,Recovers;
        public string? Offer;
        public bool FailSave;
        public RecordingStorage(SessionController controller) {this.controller=controller;}
        public StorageOutcome Save() {Saves++;InvalidateRecovery();controller.CaptureStopped();return FailSave ? new StorageOutcome(StorageStatus.Failed,"save_failed","Save failed. Farm remains stopped; previous save retained.") : Success("Saved. Execution stopped.");}
        public StorageOutcome Load()
        {
            Loads++;InvalidateRecovery();Offer=(++sequence).ToString("x32");
            return new StorageOutcome(StorageStatus.RecoveryAvailable,"recovery_available","Saved farm needs explicit recovery.",new[]{new RecoveryChoice(Offer,"Previous save")});
        }
        public StorageOutcome Recover(string id)
        {
            Check(id==Offer,"UI invalidated service offer before confirmation");
            Recovers++;InvalidateRecovery();return Success("Recovered validated save. Execution stopped.");
        }
        public void InvalidateRecovery() {Offer=null;}
        private static StorageOutcome Success(string message) => new StorageOutcome(StorageStatus.Succeeded,"none",message);
    }
}
