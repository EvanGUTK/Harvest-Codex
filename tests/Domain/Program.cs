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
        Console.WriteLine("PASS: defensive copies, persisted sequences, duplicate actors, resource capacities, durations and RNG validation.");
        return 0;
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    { try { action(); } catch (DomainValidationException) { return; } throw new Exception("Expected structured validation failure."); }
}
