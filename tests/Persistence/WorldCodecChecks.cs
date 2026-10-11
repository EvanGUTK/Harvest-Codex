using System;
using Harvest.Domain;
using Harvest.Persistence;

internal static class WorldCodecChecks
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var rules = SimulationRules.Default;
        var world = new WorldSnapshot(rules, 123456789012L, 2, 1,
            new[] { new TileState(new GridPosition(0, 0), tilled: true, moisture: 7, crop: CropKind.Wheat, growth: 9), new TileState(new GridPosition(1, 0)) },
            new[] { new TractorState(9, new GridPosition(0, 0), Heading.West, ImplementKind.Harvester, 7, 20, 4, 10, 9223372036854775806L), new TractorState(17, new GridPosition(1, 0), Heading.North, ImplementKind.None, 9, 20, 0, 10, 12) },
            new[] { new DepotState(3, new GridPosition(0, 0), 87, 23, 100) }, 3456789012u, 17);
        var json = DomainWorldCodec.EncodeStopped(world);
        var restored = DomainWorldCodec.DecodeStopped(json, rules);
        check(DomainWorldCodec.EncodeStopped(restored) == json, "All authoritative world fields roundtrip");
        check(restored.RngState == 3456789012u && restored.NextActorId == 17 && restored.Tick == world.Tick, "RNG/cursor/tick restored exactly");
        check(restored.RequestSequences[0].NextRequestId == long.MaxValue && restored.RequestSequences[1].LastProcessedRequestId == 12, "Request sequences derived exactly from persisted tractors");
        check(restored.IsStopped && restored.PendingActions.Count == 0, "No active execution restored");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"rngState\":3456789012", "\"rngState\":0"), rules), "InvalidSnapshot");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"nextActorId\":17", "\"nextActorId\":99"), rules), "InvalidSnapshot");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"width\":2", "\"width\":1"), rules), "InvalidSnapshot");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"heading\":\"West\"", "\"heading\":\"4\""), rules), "Corrupt");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"actorId\":17", "\"actorId\":9"), rules), "InvalidSnapshot");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"worldSchemaVersion\":1", "\"worldSchemaVersion\":2"), rules), "UnsupportedSchema");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"fuel\":7", "\"fuel\":1000001"), rules), "InvalidSnapshot");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"tick\":123456789012", "\"tick\":9223372036854775808"), rules), "Corrupt");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"executionState\":\"stopped\"", "\"executionState\":\"running\""), rules), "InvalidSnapshot");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"worldSchemaVersion\":1", "\"worldSchemaVersion\":1,\"pendingActions\":[]"), rules), "Corrupt");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"durationTicks\":1", "\"durationTicks\":2"), rules), "IncompatibleSimulation");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"version\":\"spike-v1\"", "\"version\":\"future\""), rules), "IncompatibleSimulation");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"growth\":9", "\"growth\":99"), rules), "InvalidSnapshot");
        reject(() => DomainWorldCodec.DecodeStopped(json.Replace("\"rngAlgorithm\":\"xorshift32-v1\"", "\"rngAlgorithm\":\"unknown\""), rules), "InvalidSnapshot");
        var active = new WorldSnapshot(rules, 0, 2, 1, world.Tiles,
            new[] { new TractorState(9, new GridPosition(0, 0), Heading.East, ImplementKind.Harvester, 7, 20, 0, 10, 1) }, Array.Empty<DepotState>(), 1, 9,
            new[] { new PendingActionView(new CommandRequest(9, 1, CommandKind.MoveForward), new GridPosition(1, 0), 0, 1, 1) });
        reject(() => DomainWorldCodec.EncodeStopped(active), "InvalidSnapshot");
    }
}
