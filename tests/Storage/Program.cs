using System;
using System.IO;
using System.Linq;
using System.Text;
using Harvest.Application;
using Harvest.Composition;
using Harvest.Domain;
using Harvest.Persistence;

internal static class Program
{
    private static int checks;
    private static void Check(bool valid, string message) { checks++; if (!valid) throw new Exception(message); }
    private static StoppedSessionCodec Codec(SimulationRules? rules = null) => new StoppedSessionCodec(rules ?? SimulationRules.Default, FirstFarm.SimulationId, FirstFarm.ContentId);
    private static SessionController Controller(SessionMode mode = SessionMode.Campaign, WorldSnapshot? world = null, IProgramFactory? factory = null)
    {
        var controller = new SessionController(new Simulation(world ?? FirstFarm.Create(mode).World), factory ?? new LanguageProgramFactory(), FirstFarm.SimulationId, FirstFarm.ContentId, 1, mode);
        controller.Draft = FirstFarm.StartingDraft; return controller;
    }
    private static string Identity(SessionController controller) => controller.Status + "|" + controller.Mode + "|" + controller.CampaignChapter + "|" + controller.SelectedActorId + "|" + controller.Draft + "|" + WorldStateHash.Compute(controller.World);
    private static void Outcome(StorageOutcome outcome, StorageStatus status, string? root = null)
    {
        Check(outcome.Status == status, "Outcome " + outcome.Code + ": " + outcome.Message);
        Check(outcome.Message.Length <= 1024 && outcome.Code.Length <= 64 && !outcome.Message.Contains("Exception") && !outcome.Message.Contains(" at "), "Bounded friendly message");
        if (root != null) Check(!outcome.Message.Contains(root) && outcome.RecoveryChoices.All(c => !c.Label.Contains(root)), "No raw storage paths");
    }
    private static void WithRoot(Action<string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "harvest-storage-" + Guid.NewGuid().ToString("N"));
        try { action(root); } finally { if (Directory.Exists(root)) Directory.Delete(root, true); else if (File.Exists(root)) File.Delete(root); }
    }
    private static void Main()
    {
        RoundtripAndNamespaces(); StopAndFailures(); Recovery(); InvalidInputs(); OfferEdgeChecks();
        Console.WriteLine("PASS: " + checks + " storage facade assertions with actual controller, language, simulation and disk.");
    }
    private static void RoundtripAndNamespaces()
    {
        WithRoot(root =>
        {
            var controller = Controller(); var storage = new SessionStorage(controller, root, Codec());
            Check(!Directory.Exists(root), "Constructor has no filesystem side effects");
            Check(controller.Run(), "Run real first harvest"); controller.Pump(.8);
            Check(controller.CampaignChapter == 1 && controller.World.Depots[0].WheatStock > 0, "Real unload unlocks chapter");
            controller.Draft = "broken syntax 👩🏽‍🌾\nif ???";
            Outcome(storage.Save(), StorageStatus.Succeeded, root);
            var expected = Identity(controller);
            var fresh = Controller(); var freshStore = new SessionStorage(fresh, root, Codec());
            Outcome(freshStore.Load(), StorageStatus.Succeeded, root);
            Check(Identity(fresh) == expected, "Cold controller restores farm/draft/chapter/selection/sequences stopped");
            Check(fresh.World.Tractor(1).LastProcessedRequestId == controller.World.Tractor(1).LastProcessedRequestId, "Request sequence preserved");
            var sandbox = Controller(SessionMode.Sandbox); sandbox.Draft = "sandbox only";
            var sandboxStore = new SessionStorage(sandbox, root, Codec()); Outcome(sandboxStore.Save(), StorageStatus.Succeeded, root);
            Check(File.Exists(Path.Combine(root, "campaign", "farm.save")) && File.Exists(Path.Combine(root, "sandbox", "farm.save")), "Mode-separated fixed farm slots");
            fresh.Draft = "replace me"; Outcome(freshStore.Load(), StorageStatus.Succeeded, root);
            Check(fresh.Draft == controller.Draft && fresh.Mode == SessionMode.Campaign, "Sandbox save does not replace campaign progress");
            var before = Identity(fresh);
            File.Copy(Path.Combine(root, "sandbox", "farm.save"), Path.Combine(root, "campaign", "farm.save"), true);
            Outcome(freshStore.Load(), StorageStatus.Failed, root); Check(Identity(fresh) == before, "Wrong-mode primary leaves live session unchanged");
        });
        WithRoot(root =>
        {
            var initial = FirstFarm.Create(SessionMode.Campaign);
            var world = new WorldSnapshot(initial.World.Rules, 33, initial.World.Width, initial.World.Height, initial.World.Tiles,
                new[] { initial.World.Tractors[0], new TractorState(2, new GridPosition(0, 1), Heading.South, ImplementKind.None, 4, 10, 0, 5, 18) }, initial.World.Depots, 23456, 2);
            var controller = Controller();
            controller.RestoreStopped(new StoppedSessionSnapshot(SessionMode.Campaign, FirstFarm.SimulationId, FirstFarm.ContentId, world, 1, new[] { new ScriptAssignment(1, "draft one"), new ScriptAssignment(2, "draft two") }, 2));
            Outcome(new SessionStorage(controller, root, Codec()).Save(), StorageStatus.Succeeded);
            var fresh = Controller(); Outcome(new SessionStorage(fresh, root, Codec()).Load(), StorageStatus.Succeeded);
            Check(fresh.SelectedActorId == 2 && fresh.Draft == "draft two" && fresh.CaptureStopped().Scripts.Count == 2, "Selection and all actor assignments persist");
        });
    }
    private static void StopAndFailures()
    {
        var defaults = SimulationRules.Default;
        var rules = new SimulationRules(defaults.Version, new ActionRule(3, 2), defaults.Turn, defaults.Harvest, defaults.Unload, defaults.Refuel, defaults.WheatMaturity, defaults.WheatGrowthPerTick, defaults.WheatYield);
        var first = FirstFarm.Create(SessionMode.Campaign).World;
        var world = new WorldSnapshot(rules, 0, first.Width, first.Height, first.Tiles, first.Tractors, first.Depots, first.RngState, first.NextActorId);
        WithRoot(root =>
        {
            var controller = Controller(world: world); controller.Draft = "move_forward()";
            var storage = new SessionStorage(controller, root, Codec(rules));
            Check(controller.Run(), "Run pending action"); controller.Pump(.1);
            Check(controller.World.PendingActions.Count == 1 && controller.World.Tractor(1).Fuel == 48, "Real action reserves fuel");
            var active = Identity(controller);
            Outcome(storage.Load(), StorageStatus.Failed, root); Check(Identity(controller) == active && !Directory.Exists(root), "Active load changes nothing and does no I/O");
            controller.Pause(); var paused = Identity(controller);
            Outcome(storage.Load(), StorageStatus.Failed, root); Check(Identity(controller) == paused, "Paused load unchanged");
            var tick = controller.World.Tick;
            Outcome(storage.Save(), StorageStatus.Succeeded, root);
            Check(controller.Status == SessionStatus.Stopped && controller.World.Tick == tick && controller.World.Tractor(1).Fuel == 50 && controller.World.PendingActions.Count == 0, "Save uses Stop and refunds once without tick advance");
            Outcome(storage.Save(), StorageStatus.Succeeded); Check(controller.World.Tractor(1).Fuel == 50, "Repeated save cannot double refund");
            var fresh = Controller(world: world); Outcome(new SessionStorage(fresh, root, Codec(rules)).Load(), StorageStatus.Succeeded);
            Check(fresh.World.Tractor(1).Fuel == 50 && fresh.World.Tractor(1).LastProcessedRequestId == 1 && fresh.Status == SessionStatus.Stopped, "Cold load stopped cancellation/accounting state");
        });
        WithRoot(root =>
        {
            File.WriteAllText(root, "directory blocked");
            var controller = Controller(world: world); controller.Draft = "move_forward()";
            var storage = new SessionStorage(controller, root, Codec(rules)); controller.Run(); controller.Pump(.1);
            Outcome(storage.Save(), StorageStatus.Failed, root);
            Check(controller.Status == SessionStatus.Stopped && controller.World.Tractor(1).Fuel == 50 && controller.Draft == "move_forward()", "Directory I/O failure leaves Stop/refund/draft intact");
        });
        WithRoot(root =>
        {
            var controller = Controller(factory: new ExplodingFactory()); var storage = new SessionStorage(controller, root, Codec()); controller.Run();
            try { storage.Save(); throw new Exception("Programming error swallowed"); }
            catch (InvalidOperationException ex) { Check(ex.Message == "Programming failure", "Unexpected programming error propagates"); }
        });
    }
    private static void Recovery()
    {
        WithRoot(root =>
        {
            var controller = Controller(); var storage = new SessionStorage(controller, root, Codec());
            controller.Draft = "old farm draft"; storage.Save(); controller.Draft = "new farm draft"; storage.Save();
            var primary = Path.Combine(root, "campaign", "farm.save"); var backup = primary + ".bak";
            File.WriteAllText(primary, "broken"); var before = Identity(controller);
            var offered = storage.Load(); Outcome(offered, StorageStatus.RecoveryAvailable, root);
            Check(Identity(controller) == before && offered.RecoveryChoices.Count == 1 && offered.RecoveryChoices[0].Label == "Previous save", "Recovery is explicit, no live promotion");
            var id = offered.RecoveryChoices[0].Id;
            Check(id.Length == 32 && id.All(c => "0123456789abcdef".Contains(c)), "Opaque recovery ID");
            Outcome(storage.Recover(id), StorageStatus.Succeeded, root); Check(controller.Draft == "old farm draft" && controller.Status == SessionStatus.Stopped, "Explicit selected backup restored");
            before = Identity(controller); Outcome(storage.Recover(id), StorageStatus.Failed); Check(Identity(controller) == before, "Recovery IDs single-use");
            var offeredAgain = storage.Load().RecoveryChoices[0].Id;
            // Different valid bytes at the same pathname must never restore substituted content.
            var changed = new StoppedSessionSnapshot(SessionMode.Campaign, FirstFarm.SimulationId, FirstFarm.ContentId, controller.World, 0, new[] { new ScriptAssignment(1, "substituted valid draft") }, 1);
            File.WriteAllBytes(backup, Codec().Encode(changed).Encode()); before = Identity(controller);
            var rejected = storage.Recover(offeredAgain); Outcome(rejected, StorageStatus.Failed, root);
            Check(rejected.Code == "ChangedRecovery" && Identity(controller) == before, "Valid replacement rejected by pinned byte identity");
            offeredAgain = storage.Load().RecoveryChoices[0].Id; File.Delete(backup);
            Outcome(storage.Recover(offeredAgain), StorageStatus.Failed); Check(Identity(controller) == before, "Removed recovery candidate cannot replace state");
            File.WriteAllBytes(backup, Codec().Encode(changed).Encode()); offeredAgain = storage.Load().RecoveryChoices[0].Id; File.WriteAllText(backup, "corrupt");
            Outcome(storage.Recover(offeredAgain), StorageStatus.Failed); Check(Identity(controller) == before, "Corrupt candidate rejected without mutation");
            File.WriteAllBytes(backup, Codec().Encode(changed).Encode());
            offeredAgain = storage.Load().RecoveryChoices[0].Id; storage.InvalidateRecovery();
            Outcome(storage.Recover(offeredAgain), StorageStatus.Failed); Check(Identity(controller) == before, "Explicit mode/view invalidation clears offers");
            offeredAgain = storage.Load().RecoveryChoices[0].Id;
            controller.RestoreStopped(FirstFarm.Create(SessionMode.Sandbox)); before = Identity(controller);
            Outcome(storage.Recover(offeredAgain), StorageStatus.Failed); Check(Identity(controller) == before, "Cross-mode offered ID rejected");
            controller.RestoreStopped(FirstFarm.Create(SessionMode.Campaign));
            offeredAgain = storage.Load().RecoveryChoices[0].Id; storage.Load(); Outcome(storage.Recover(offeredAgain), StorageStatus.Failed);
            offeredAgain = storage.Load().RecoveryChoices[0].Id; storage.Save(); Outcome(storage.Recover(offeredAgain), StorageStatus.Failed);
            File.WriteAllText(primary, "broken"); offeredAgain = storage.Load().RecoveryChoices[0].Id;
            Outcome(storage.Recover("unknown"), StorageStatus.Failed); Outcome(storage.Recover(offeredAgain), StorageStatus.Failed);
            offeredAgain = storage.Load().RecoveryChoices[0].Id;
            controller.Draft = "move_forward()"; Check(controller.Run(), "Run before offered recovery"); before = Identity(controller);
            Outcome(storage.Recover(offeredAgain), StorageStatus.Failed); Check(Identity(controller) == before, "Active recovery unchanged and consumes offers");
            controller.Stop(); Outcome(storage.Recover(offeredAgain), StorageStatus.Failed);
        });
        WithRoot(root =>
        {
            Directory.CreateDirectory(Path.Combine(root, "campaign"));
            var controller = Controller(); var storage = new SessionStorage(controller, root, Codec());
            var bytes = Codec().Encode(controller.CaptureStopped()).Encode();
            File.WriteAllBytes(Path.Combine(root, "campaign", "farm.interrupted.tmp"), bytes);
            var before = Identity(controller); var offer = storage.Load(); Outcome(offer, StorageStatus.RecoveryAvailable);
            Check(offer.RecoveryChoices.Single().Label.StartsWith("Interrupted save") && Identity(controller) == before && !File.Exists(Path.Combine(root, "campaign", "farm.save")), "Orphan temp only explicit recovery");
            Outcome(storage.Recover(offer.RecoveryChoices[0].Id), StorageStatus.Succeeded);
            Check(!File.Exists(Path.Combine(root, "campaign", "farm.save")), "Recovery does not silently promote to primary");
        });
    }
    private static void InvalidInputs()
    {
        WithRoot(root =>
        {
            var controller = Controller(); var storage = new SessionStorage(controller, root, Codec());
            var before = Identity(controller); Outcome(storage.Load(), StorageStatus.Failed, root); Check(Identity(controller) == before, "Missing save does not replace live session");
            var primary = Path.Combine(root, "campaign", "farm.save");
            var bytes = Codec().Encode(controller.CaptureStopped()).Encode();
            File.WriteAllText(primary, Encoding.UTF8.GetString(bytes).Replace("\"schemaVersion\":1", "\"schemaVersion\":2"));
            var future = storage.Load(); Outcome(future, StorageStatus.Failed, root); Check(future.Code == "UnsupportedSchema" && Identity(controller) == before, "Future save friendly and unchanged");
            File.WriteAllText(primary, "bad"); Outcome(storage.Load(), StorageStatus.Failed, root); Check(Identity(controller) == before, "Corrupt save unchanged");
            var controllerMismatch = new SessionController(new Simulation(controller.World), new LanguageProgramFactory(), "other-sim", FirstFarm.ContentId, 1);
            var mismatched = new SessionStorage(controllerMismatch, root, Codec());
            File.WriteAllBytes(primary, bytes); Outcome(mismatched.Load(), StorageStatus.Failed, root);
            Check(controllerMismatch.Status == SessionStatus.Stopped && controllerMismatch.Draft == "", "Controller compatibility rejection friendly and unchanged");
            File.WriteAllBytes(primary + ".bak", bytes); File.WriteAllText(primary, "bad");
            for (int i = 0; i < 70; i++) File.WriteAllBytes(Path.Combine(root, "campaign", "farm." + i + ".tmp"), bytes);
            var many = storage.Load(); Outcome(many, StorageStatus.RecoveryAvailable, root);
            Check(many.RecoveryChoices.Count == 65 && many.Message.Contains("could not be validated"), "Bounded offers preserve backup and report skipped artifacts");
            foreach (var invalid in new[] { "", "relative/path", new string('x', 4097) })
            {
                try { new SessionStorage(controller, invalid, Codec()); throw new Exception("Expected invalid root"); }
                catch (ArgumentException) { checks++; }
            }
        });
    }
    private static void OfferEdgeChecks()
    {
        WithRoot(root =>
        {
            var controller = Controller(); var storage = new SessionStorage(controller, root, Codec());
            controller.Draft = "previous"; storage.Save(); controller.Draft = "current"; storage.Save();
            var primary = Path.Combine(root, "campaign", "farm.save"); var backup = primary + ".bak";
            File.WriteAllText(primary, "broken");
            var id = storage.Load().RecoveryChoices.Single().Id;
            var originalPayload = SaveEnvelope.Decode(File.ReadAllBytes(backup)).Payload;
            File.AppendAllText(backup, " ");
            Check(SaveEnvelope.Decode(File.ReadAllBytes(backup)).Payload == originalPayload, "Replacement byte test remains valid with identical payload");
            var before = Identity(controller); var changed = storage.Recover(id);
            Outcome(changed, StorageStatus.Failed, root);
            Check(changed.Code == "ChangedRecovery" && Identity(controller) == before, "Identity pins exact bytes, not only payload semantics");
            id = storage.Load().RecoveryChoices.Single().Id;
            var priorPrimary = File.ReadAllBytes(primary); var priorBackup = File.ReadAllBytes(backup);
            controller.Draft = "\uD800"; // Application permits this code unit; persistence rejects invalid Unicode.
            var failed = storage.Save(); Outcome(failed, StorageStatus.Failed, root);
            Check(failed.Code == "InvalidSnapshot" && controller.Status == SessionStatus.Stopped, "Expected encoding failure leaves session stopped");
            Check(File.ReadAllBytes(primary).SequenceEqual(priorPrimary) && File.ReadAllBytes(backup).SequenceEqual(priorBackup), "Failed save preserves prior primary and backup bytes");
            var expired = storage.Recover(id); Outcome(expired, StorageStatus.Failed);
            Check(expired.Code == "StaleRecovery", "Failed Save invalidates old offer even with intact candidate");
            controller.Draft = "move_forward()";
            id = storage.Load().RecoveryChoices.Single().Id; Check(controller.Run(), "Run for failed Load invalidation"); before = Identity(controller);
            Outcome(storage.Load(), StorageStatus.Failed);
            Check(Identity(controller) == before, "Rejected active Load leaves execution/state unchanged");
            controller.Stop(); expired = storage.Recover(id); Outcome(expired, StorageStatus.Failed);
            Check(expired.Code == "StaleRecovery", "Rejected active Load invalidates old offers");
            id = storage.Load().RecoveryChoices.Single().Id; controller.Run(); controller.Pause(); before = Identity(controller);
            Outcome(storage.Recover(id), StorageStatus.Failed); Check(Identity(controller) == before, "Paused Recover does not stop or replace state");
            controller.Stop(); Outcome(storage.Recover(id), StorageStatus.Failed);
            id = storage.Load().RecoveryChoices.Single().Id;
            Outcome(storage.Recover(new string('x', 100000)), StorageStatus.Failed);
            Outcome(storage.Recover(id), StorageStatus.Failed);
            Outcome(storage.Recover(null!), StorageStatus.Failed);
            // Mode-switch integration invalidates even an A->B->A switch between calls.
            id = storage.Load().RecoveryChoices.Single().Id;
            controller.RestoreStopped(FirstFarm.Create(SessionMode.Sandbox)); storage.InvalidateRecovery();
            controller.RestoreStopped(FirstFarm.Create(SessionMode.Campaign)); storage.InvalidateRecovery();
            expired = storage.Recover(id); Outcome(expired, StorageStatus.Failed);
            Check(expired.Code == "StaleRecovery", "Explicit mode-switch invalidation handles A-B-A");
        });
    }
    private sealed class ExplodingFactory : IProgramFactory
    {
        public ProgramStart CompileAndStart(string source, long actorId, Harvest.Language.Capabilities capabilities) => new ProgramStart(new ExplodingDriver(), Array.Empty<Harvest.Language.Diagnostic>());
    }
    private sealed class ExplodingDriver : IProgramDriver
    {
        public ProgramFrame Resume(Harvest.Language.TickInput input) => throw new InvalidOperationException("Programming failure");
        public void Stop() => throw new InvalidOperationException("Programming failure");
    }
}
