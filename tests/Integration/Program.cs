using System;
using System.Linq;
using Harvest.Application;
using Harvest.Composition;
using Harvest.Domain;
using Harvest.Persistence;

static class Program
{
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    static SessionController Session(SessionMode mode)
    {
        var initial = FirstFarm.Create(mode);
        var session = new SessionController(new Simulation(initial.World), new LanguageProgramFactory(),
            FirstFarm.SimulationId, FirstFarm.ContentId, FirstFarm.TractorId, mode);
        session.RestoreStopped(initial);
        return session;
    }

    static void Main()
    {
        var campaign = Session(SessionMode.Campaign);
        Require(campaign.Run(), "Starter program must compile under chapter zero");
        campaign.Pump(.1);
        campaign.Pause();
        long paused = campaign.World.Tick;
        string pausedHash = WorldStateHash.Compute(campaign.World);
        campaign.Pump(10);
        Require(campaign.World.Tick == paused && WorldStateHash.Compute(campaign.World) == pausedHash, "Pause freezes world");
        campaign.Step();
        Require(campaign.World.Tick == paused + 1, "Step advances exactly one tick");
        Require(campaign.Run(), "Paused program resumes");
        for (int i = 0; i < 20; i++) campaign.Pump(.1);
        campaign.Stop();
        Require(campaign.Diagnostics.Count == 0, "Starter program must have no faults");
        Require(campaign.World.Tractor(1).Position == new GridPosition(0, 0), "Interpreter returns tractor to depot");
        Require(campaign.World.Depots.Single().WheatStock == 1 && campaign.World.Tractor(1).Cargo == 0, "Real harvest transfers to depot");
        Require(campaign.World.Tractor(1).Fuel == 45, "Fuel consumed exactly once by six real actions");
        Require(campaign.CampaignChapter == 1, "Real delivery unlocks counted loops");

        var captured = campaign.CaptureStopped();
        var codec = new StoppedSessionCodec(SimulationRules.Default, FirstFarm.SimulationId, FirstFarm.ContentId);
        var restored = codec.Decode(SaveEnvelope.Decode(codec.Encode(captured).Encode()));
        Require(WorldStateHash.Compute(restored.World) == WorldStateHash.Compute(captured.World), "Save roundtrip preserves canonical world");
        var clone = Session(SessionMode.Campaign);
        clone.RestoreStopped(restored);
        long previousId = clone.World.Tractor(1).LastProcessedRequestId;
        clone.Draft = "turn_right()\n";
        Require(clone.Run(), "Saved session reruns");
        clone.Pump(.1);
        clone.Stop();
        Require(clone.World.Tractor(1).LastProcessedRequestId > previousId, "Request sequence survives save/rerun");
        Require(campaign.World.Tractor(1).LastProcessedRequestId == previousId, "Restored clone cannot mutate original");

        var sandbox = Session(SessionMode.Sandbox);
        sandbox.Draft = "while True:\n    x = 1\n";
        Require(sandbox.Run(), "Sandbox unlocks implemented loop/assignment syntax");
        sandbox.Pump(.1);
        sandbox.Pause();
        long boundedTick = sandbox.World.Tick;
        sandbox.Step();
        Require(sandbox.World.Tick == boundedTick + 1, "Infinite program yields to Step");
        sandbox.Stop();
        Require(sandbox.Status == SessionStatus.Stopped && sandbox.World.IsStopped, "Infinite program stops at boundary");
        Require(!sandbox.AchievementEligible, "Sandbox cannot grant campaign achievements");
        var locked = Session(SessionMode.Campaign);
        locked.Draft = sandbox.Draft;
        Require(!locked.Run() && locked.Diagnostics.Count > 0 && locked.World.Tick == 0, "Campaign rejects locked syntax before world mutation");
        Console.WriteLine("PASS real scripting/tractor delivery, pause/step/stop, campaign gates, save replay and bounded sandbox loop");
        BackboneChecks.Run();
    }
}
