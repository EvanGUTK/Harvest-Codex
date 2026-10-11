using System;
using System.Collections.Generic;
using Harvest.Domain;

internal static class Program
{
    private static int Main()
    {
        var tiles = new List<TileState> { new TileState(new GridPosition(0, 0), crop: CropKind.Wheat, growth: 10) };
        var world = new WorldSnapshot(SimulationRules.Default, 0, 1, 1, tiles,
            new[] { new TractorState(1, new GridPosition(0, 0), Heading.North, ImplementKind.Harvester, 5, 10, 0, 5, 7) }, Array.Empty<DepotState>(), 1, 1);
        tiles.Clear();
        Check(world.Tiles.Count == 1, "World must defensively copy tiles.");
        Check(world.RequestSequences[0].NextRequestId == 8, "Application resumes persisted request sequence.");
        Reject(() => new WorldSnapshot(SimulationRules.Default, 0, 1, 1, world.Tiles, new[] { world.Tractors[0], world.Tractors[0] }, world.Depots, 1, 1));
        Reject(() => new TractorState(1, new GridPosition(0, 0), Heading.North, ImplementKind.None, 11, 10, 0, 5));
        Reject(() => new ActionRule(0, 1));
        Reject(() => new WorldSnapshot(SimulationRules.Default, 0, 1, 1, world.Tiles, world.Tractors, world.Depots, 0, 1));
        // A newer processed rejection must not invalidate an older reserved action.
        var rules = new SimulationRules("sequence-test", new ActionRule(3, 1), new ActionRule(1, 1), new ActionRule(1, 1), new ActionRule(1, 0), new ActionRule(1, 0), 10, 1, 1);
        var actor = new TractorState(1, new GridPosition(0, 0), Heading.East, ImplementKind.Harvester, 4, 10, 0, 5, 2);
        var action = new PendingActionView(new CommandRequest(1, 1, CommandKind.MoveForward), new GridPosition(1, 0), 0, 3, 1);
        var active = new WorldSnapshot(rules, 2, 2, 1,
            new[] { new TileState(new GridPosition(0, 0)), new TileState(new GridPosition(1, 0)) },
            new[] { actor }, Array.Empty<DepotState>(), 1, 1, new[] { action });
        Check(active.PendingActions[0].Request.RequestId == 1 && active.RequestSequences[0].NextRequestId == 3, "Active request can precede highest processed request.");
        Reject(() => new WorldSnapshot(rules, 2, 2, 1, active.Tiles, active.Tractors, active.Depots, 1, 1,
            new[] { new PendingActionView(new CommandRequest(1, 3, CommandKind.MoveForward), new GridPosition(1, 0), 0, 3, 1) }));
        Console.WriteLine("PASS: defensive copies, persisted sequences, duplicate actors, resource capacities, durations, RNG validation and active/processed sequence invariants.");
        return 0;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    { try { action(); } catch (DomainValidationException) { return; } throw new Exception("Expected structured validation failure."); }
}
