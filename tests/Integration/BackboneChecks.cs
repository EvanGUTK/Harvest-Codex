using System;
using System.IO;
using Harvest.Application;
using Harvest.Composition;
using Harvest.Domain;
using Harvest.Language;
using Harvest.Persistence;

static class BackboneChecks
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

    static SessionStorage Storage(SessionController session, string root)
        => new SessionStorage(session, root, new StoppedSessionCodec(SimulationRules.Default, FirstFarm.SimulationId, FirstFarm.ContentId));

    public static void Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "harvest-backbone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var campaign = Session(SessionMode.Campaign);
            var storage = Storage(campaign, root);
            Require(campaign.Run(), "Campaign starter compiles");
            for (int i = 0; i < 20; i++) campaign.Pump(.1);
            Require(storage.Save().Status == StorageStatus.Succeeded && campaign.Status == SessionStatus.Stopped,
                "Save stops the actual running campaign");
            string saved = WorldStateHash.Compute(campaign.World);
            string draft = campaign.Draft;

            var fresh = Session(SessionMode.Campaign);
            var freshStorage = Storage(fresh, root);
            Require(freshStorage.Load().Status == StorageStatus.Succeeded, "New controller/service loads disk save");
            Require(WorldStateHash.Compute(fresh.World) == saved && fresh.Draft == draft && fresh.CampaignChapter == 1,
                "Cold load preserves world, draft and progression");
            Require(fresh.Status == SessionStatus.Stopped, "Loading cannot execute saved source");
            var reference = LanguageReference.Available(ModePolicy.For(fresh.Mode, fresh.CampaignChapter));
            Require(Contains(reference, "for") && !Contains(reference, "while"), "Loaded campaign reference uses unlocked policy");

            fresh.Draft = "turn_right()\n";
            Require(fresh.Run(), "Freshly loaded session runs");
            string beforeActiveLoad = WorldStateHash.Compute(fresh.World);
            Require(freshStorage.Load().Status == StorageStatus.Failed && fresh.Status == SessionStatus.Running &&
                WorldStateHash.Compute(fresh.World) == beforeActiveLoad && fresh.Draft == "turn_right()\n",
                "Active load cannot stop or replace world/draft");
            fresh.Pump(.1); fresh.Stop();
            Require(fresh.World.Tractor(1).LastProcessedRequestId > campaign.World.Tractor(1).LastProcessedRequestId,
                "Cold-loaded request sequence remains monotonic");

            var sandbox = Session(SessionMode.Sandbox);
            sandbox.Draft = "while True:\n    x = 1\n";
            var sandboxStorage = Storage(sandbox, root);
            Require(sandboxStorage.Save().Status == StorageStatus.Succeeded, "Sandbox has a separate disk save");
            Require(storage.Load().Status == StorageStatus.Succeeded && campaign.Draft == draft &&
                WorldStateHash.Compute(campaign.World) == saved, "Sandbox save cannot replace campaign");
            Require(Contains(LanguageReference.Available(ModePolicy.For(sandbox.Mode, sandbox.CampaignChapter)), "while"),
                "Sandbox reference unlocks supported loop");

            // Create a real previous-good backup, then force explicit recovery of the corrupt primary.
            campaign.Draft = "log(\"new draft\")\n";
            Require(storage.Save().Status == StorageStatus.Succeeded, "Second save writes recoverable previous version");
            string primary = Path.Combine(root, "campaign", "farm.save");
            File.WriteAllText(primary, "corrupt");
            string beforeRecovery = WorldStateHash.Compute(campaign.World);
            var offer = storage.Load();
            Require(offer.Status == StorageStatus.RecoveryAvailable && offer.RecoveryChoices.Count > 0,
                "Corrupt primary exposes explicit backup choices");
            Require(campaign.Draft == "log(\"new draft\")\n" && WorldStateHash.Compute(campaign.World) == beforeRecovery,
                "Offering recovery never mutates live session");
            string id = offer.RecoveryChoices[0].Id;
            Require(storage.Recover(id).Status == StorageStatus.Succeeded && campaign.Draft == draft,
                "Explicit revalidated backup recovers original draft");
            Require(storage.Recover(id).Status == StorageStatus.Failed, "Consumed recovery ID cannot replay");

            offer = storage.Load();
            Require(offer.Status == StorageStatus.RecoveryAvailable, "Recovery does not silently promote corrupt primary");
            id = offer.RecoveryChoices[0].Id;
            storage.InvalidateRecovery();
            Require(storage.Recover(id).Status == StorageStatus.Failed, "Invalidated offer cannot replace farm");

            // A valid but different replacement at the offered path must not be accepted as the chosen backup.
            offer = storage.Load(); id = offer.RecoveryChoices[0].Id;
            var codec = new StoppedSessionCodec(SimulationRules.Default, FirstFarm.SimulationId, FirstFarm.ContentId);
            File.WriteAllBytes(primary + ".bak", codec.Encode(FirstFarm.Create(SessionMode.Campaign)).Encode());
            string beforeSubstitution = WorldStateHash.Compute(campaign.World);
            Require(storage.Recover(id).Status == StorageStatus.Failed &&
                WorldStateHash.Compute(campaign.World) == beforeSubstitution && campaign.Draft == draft,
                "Recovery pins offered content identity, including valid replacements");
            Console.WriteLine("PASS fresh-session disk storage, stopped/active semantics, mode separation, policy reference and explicit pinned recovery");
        }
        finally { Directory.Delete(root, true); }
    }

    static bool Contains(System.Collections.Generic.IReadOnlyList<ReferenceEntry> entries, string id)
    { foreach (var entry in entries) if (entry.Id == id) return true; return false; }
}
