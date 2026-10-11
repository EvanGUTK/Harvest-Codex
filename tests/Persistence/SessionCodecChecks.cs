using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Harvest.Application;
using Harvest.Domain;
using Harvest.Persistence;

internal static class SessionCodecChecks
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var rules = SimulationRules.Default;
        var world = new WorldSnapshot(rules, 77, 1, 1,
            new[] { new TileState(new GridPosition(0, 0), crop: CropKind.Wheat, growth: 10) },
            new[] { new TractorState(1, new GridPosition(0, 0), Heading.North, ImplementKind.Harvester, 9, 10, 2, 8, 56) },
            new[] { new DepotState(1, new GridPosition(0, 0), 100, 40, 100) }, 12345u, 1);
        var codec = new StoppedSessionCodec(rules, "sim-spike-1", "content-spike-1");
        var campaign = new StoppedSessionSnapshot(SessionMode.Campaign, "sim-spike-1", "content-spike-1", world, 1,
            new[] { new ScriptAssignment(1, "intentionally bad player syntax 👩🏽‍🌾\nwhile ???") }, 1);
        var sandbox = new StoppedSessionSnapshot(SessionMode.Sandbox, "sim-spike-1", "content-spike-1", world, 0,
            new[] { new ScriptAssignment(1, "harvest()") }, 1);
        foreach (var session in new[] { campaign, sandbox })
        {
            var envelope = codec.Encode(session);
            var restored = codec.Decode(SaveEnvelope.Decode(envelope.Encode()));
            check(codec.Encode(restored).Payload == envelope.Payload, "Campaign/sandbox session roundtrip");
            check(restored.Scripts[0].Draft == session.Scripts[0].Draft && restored.World.Tick == 77 && restored.World.RngState == 12345u, "Inert scripts and deterministic world preserved");
            check(restored.World.Tractors[0].NextRequestId == 57, "Sequence resumes without reuse");
        }
        var encoded = codec.Encode(campaign);
        void Mutate(Action<JsonObject> mutation, string code)
        {
            var root = JsonNode.Parse(encoded.Payload)!.AsObject(); mutation(root);
            reject(() => codec.Decode(SaveEnvelope.Create("sim-spike-1", "content-spike-1", root.ToJsonString())), code);
        }
        Mutate(root => root["sandboxModifiers"] = new JsonObject(), "Corrupt");
        Mutate(root => root["editor"]!["audioVolume"] = 1, "Corrupt");
        Mutate(root => root["editor"]!["selectedActorId"] = 99, "InvalidSnapshot");
        Mutate(root => root["campaignChapter"] = 2, "InvalidSnapshot");
        Mutate(root => root["mode"] = "Sandbox", "InvalidSnapshot");
        Mutate(root => root["mode"] = "Unknown", "Corrupt");
        Mutate(root => root["scripts"]![0]!["actorId"] = 99, "InvalidSnapshot");
        Mutate(root => root["scripts"]!.AsArray().Add(root["scripts"]![0]!.DeepClone()), "InvalidSnapshot");
        Mutate(root => root["scripts"] = new JsonArray(), "InvalidSnapshot");
        Mutate(root => root["scripts"]![0]!["draft"] = new string('é', 32769), "LimitExceeded");
        Mutate(root => root["scripts"]![0]!["draft"] = new string('x', 65537), "LimitExceeded");
        Mutate(root => root["scripts"]![0]!["activeContinuation"] = new JsonObject(), "Corrupt");
        Mutate(root => root["editor"]!["accessibility"] = new JsonObject(), "Corrupt");
        Mutate(root => { var script = root["scripts"]![0]!.DeepClone(); var many = new JsonArray(); for (int i = 0; i < 17; i++) many.Add(script.DeepClone()); root["scripts"] = many; }, "LimitExceeded");
        Mutate(root => root["executionState"] = "paused", "InvalidSnapshot");
        Mutate(root => root["sessionSchemaVersion"] = 2, "UnsupportedSchema");
        Mutate(root => { root["sessionSchemaVersion"] = 2; root["futureSessionField"] = true; }, "UnsupportedSchema");
        Mutate(root => root.Remove("world"), "Corrupt");
        reject(() => codec.Decode(SaveEnvelope.Create("sim-future", "content-spike-1", encoded.Payload)), "IncompatibleSimulation");
        reject(() => codec.Decode(SaveEnvelope.Create("sim-spike-1", "content-future", encoded.Payload)), "IncompatibleSimulation");
        var directory = Path.Combine(Path.GetTempPath(), "harvest-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = codec.CreateStore(directory, SessionMode.Campaign);
            store.Save("farm", encoded); store.Save("farm", encoded);
            reject(() => store.Save("farm", codec.Encode(sandbox)), "InvalidSnapshot");
            var invalid = JsonNode.Parse(encoded.Payload)!.AsObject(); invalid["world"]!["rngState"] = 0;
            File.WriteAllBytes(Path.Combine(directory, "farm.save"), SaveEnvelope.Create("sim-spike-1", "content-spike-1", invalid.ToJsonString()).Encode());
            var recovery = store.Load("farm");
            check(recovery.Primary == null && recovery.PrimaryError == "InvalidSnapshot" && recovery.RecoveryCandidates.Count == 1, "Valid checksum cannot bypass full world validation");
            var selected = codec.Decode(store.ReadRecovery("farm", recovery.RecoveryCandidates[0].Path));
            check(selected.World.RngState == 12345u, "Explicit semantic recovery returns fresh valid stopped DTO");
            store.Save("farm", encoded);
            check(codec.Decode(SaveEnvelope.Decode(File.ReadAllBytes(Path.Combine(directory, "farm.save.bak")))).World.RngState == 12345u, "Semantically corrupt primary preserves valid backup");
            var futureSession = JsonNode.Parse(encoded.Payload)!.AsObject();
            futureSession["sessionSchemaVersion"] = 2; futureSession["futureSessionField"] = true;
            var futureBytes = SaveEnvelope.Create("sim-spike-1", "content-spike-1", futureSession.ToJsonString()).Encode();
            File.WriteAllBytes(Path.Combine(directory, "farm.save"), futureBytes);
            reject(() => store.Save("farm", encoded), "UnsupportedSchema");
            check(File.ReadAllBytes(Path.Combine(directory, "farm.save")).SequenceEqual(futureBytes), "Future expanded session preserved");
            // A future configuration is preserved rather than overwritten by a current save.
            var incompatible = SaveEnvelope.Create("future-sim", "content-spike-1", encoded.Payload).Encode();
            File.WriteAllBytes(Path.Combine(directory, "farm.save"), incompatible);
            reject(() => store.Save("farm", encoded), "IncompatibleSimulation");
            check(File.ReadAllBytes(Path.Combine(directory, "farm.save")).SequenceEqual(incompatible), "Incompatible primary bytes preserved");
        }
        finally { Directory.Delete(directory, true); }
    }
}
