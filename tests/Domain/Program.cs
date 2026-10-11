using System;
using System.Collections.Generic;
using System.Linq;
using Harvest.Domain;

internal static class Program
{
    private static int passed;
    private static int Main()
    {
        var tests = new (string, Action)[] {
            ("immutable validated contracts", Contracts), ("move/turn duration and directions", Movement),
            ("boundaries and obstacles", Boundaries), ("fuel and rejection sequences", FuelAndSequences),
            ("harvest timing/capacity/implement", Harvest), ("growth tick-start sensors", Growth),
            ("all-or-nothing depot transfers", Depot), ("stop refunds all action kinds", Stop),
            ("malformed batch is atomic", Batch), ("contention and cursor fairness", Contention),
            ("stopped restore and replay", Restore), ("canonical deterministic replay", Replay),
            ("bounded world and arithmetic", Limits), ("pending reservation validation", ActiveValidation), ("rejected IDs survive stop/restore", RejectedSequenceRestore), ("world cross-reference validation", WorldValidation)
        };
        foreach (var (name, test) in tests)
        {
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception error) { Console.Error.WriteLine("FAIL " + name + ": " + error); return 1; }
        }
        Console.WriteLine("Passed " + passed + " Domain behavior groups.");
        return 0;
    }
    private static void Check(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
    private static void Equal<T>(T expected, T actual, string message = "Unexpected value")
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception(message + ": expected " + expected + ", actual " + actual); }
    private static void Reject(Action action, string? reason = null)
    {
        try { action(); } catch (DomainValidationException error) { if (reason != null) Equal(reason, error.Reason); return; }
        throw new Exception("Expected structured validation error");
    }
    private static SimulationRules Rules(int duration = 1, int cost = 1, int growth = 1, int yield = 1, int depotCost = 0)
        => new SimulationRules("tests-v1", new ActionRule(duration, cost), new ActionRule(duration, cost), new ActionRule(duration, cost), new ActionRule(duration, depotCost), new ActionRule(duration, depotCost), 10, growth, yield);
    private static TractorState Actor(long id = 1, int x = 0, int y = 0, Heading heading = Heading.East, int fuel = 20, int cargo = 0, int cargoCapacity = 10, ImplementKind implement = ImplementKind.Harvester, long last = 0)
        => new TractorState(id, new GridPosition(x, y), heading, implement, fuel, 20, cargo, cargoCapacity, last);
    private static WorldSnapshot World(SimulationRules? rules = null, int width = 3, int height = 3, IEnumerable<TractorState>? actors = null, IEnumerable<DepotState>? depots = null, int cropGrowth = 10, int blockedX = -1, long tick = 0, long cursor = 1)
    {
        var tiles = new List<TileState>();
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            bool blocked = x == blockedX && y == 0;
            tiles.Add(new TileState(new GridPosition(x, y), blocked, crop: blocked ? CropKind.None : CropKind.Wheat, growth: blocked ? 0 : cropGrowth));
        }
        return new WorldSnapshot(rules ?? Rules(), tick, width, height, tiles, actors ?? new[] { Actor() }, depots ?? Array.Empty<DepotState>(), WorldSnapshot.StateFromSeed(0), cursor);
    }
    private static TickReport Send(Simulation simulation, CommandKind kind, long id = 1, long? request = null)
        => simulation.AdvanceTick(new[] { new CommandRequest(id, request ?? simulation.Snapshot.Tractor(id).NextRequestId, kind) });
    private static TickReport Empty(Simulation simulation) => simulation.AdvanceTick(Array.Empty<CommandRequest>());
    private static void Failure(TickReport report, string reason)
    {
        Equal(BatchStatus.Advanced, report.Status); Equal(SubmissionStatus.Rejected, report.Submissions[0].Status);
        Equal(reason, report.Results.Single().Reason); Check(!report.Results.Single().Ok);
        Equal(report.Snapshot.Tick, report.Results.Single().TerminalTick);
    }
    private static void Contracts()
    {
        var tiles = World().Tiles.ToList(); var actors = new List<TractorState> { Actor(last: 7) };
        var w = new WorldSnapshot(Rules(), 0, 3, 3, tiles, actors, Array.Empty<DepotState>(), 1, 1);
        tiles.Clear(); actors.Clear(); Equal(9, w.Tiles.Count); Equal(8L, w.RequestSequences[0].NextRequestId);
        Reject(() => new WorldSnapshot(Rules(), 0, 3, 3, w.Tiles, new[] { Actor(), Actor() }, w.Depots, 1, 1));
        Reject(() => new TractorState(1, new GridPosition(0, 0), Heading.North, ImplementKind.None, 21, 20, 0, 5));
        Reject(() => new ActionRule(0, 1)); Reject(() => new ActionRule(10001, 0));
        Reject(() => new ActionRule(1, -1)); Reject(() => new CommandRequest(1, 0, CommandKind.Harvest), Reasons.InvalidArgument);
        Reject(() => new TileState(new GridPosition(0, 0), crop: CropKind.None, growth: 1));
        Reject(() => new SensorSnapshot(new GridPosition(0, 0), Heading.North, true, CropKind.None, 0, 0, ImplementKind.None));
        Reject(() => new ActionResult(1, 1, true, Reasons.Blocked, 0));
    }
    private static void Movement()
    {
        var simulation = new Simulation(World(Rules(duration: 3, cost: 2)));
        var first = Send(simulation, CommandKind.MoveForward);
        Equal(SubmissionStatus.Accepted, first.Submissions[0].Status); Equal(0, first.Results.Count);
        Equal(new GridPosition(0, 0), simulation.Snapshot.Tractor(1).Position); Equal(18, simulation.Snapshot.Tractor(1).Fuel);
        Equal(3L, simulation.Snapshot.PendingActions[0].CompletionTick);
        Failure(Send(simulation, CommandKind.TurnLeft), Reasons.Busy);
        Equal(2L, simulation.Snapshot.Tractor(1).LastProcessedRequestId);
        var final = Empty(simulation); Equal(1, final.Results.Count); Equal(1L, final.Results[0].RequestId); Equal(3L, final.Results[0].TerminalTick);
        Equal(new GridPosition(1, 0), final.Snapshot.Tractor(1).Position); Equal(18, final.Snapshot.Tractor(1).Fuel);
        Equal(0, Empty(simulation).Results.Count);
        var expected = new[] { new GridPosition(1, 2), new GridPosition(2, 1), new GridPosition(1, 0), new GridPosition(0, 1) };
        for (int h = 0; h < 4; h++)
        {
            var s = new Simulation(World(actors: new[] { Actor(x: 1, y: 1, heading: (Heading)h) }));
            Send(s, CommandKind.MoveForward); Equal(expected[h], s.Snapshot.Tractor(1).Position);
            Send(s, CommandKind.TurnLeft); Equal((Heading)((h + 3) % 4), s.Snapshot.Tractor(1).Heading);
            Send(s, CommandKind.TurnRight); Equal((Heading)h, s.Snapshot.Tractor(1).Heading);
        }
    }
    private static void Boundaries()
    {
        var boundary = new[] { Actor(heading: Heading.West), Actor(heading: Heading.South), Actor(x: 2, heading: Heading.East), Actor(y: 2, heading: Heading.North) };
        foreach (var actor in boundary)
        {
            var s = new Simulation(World(actors: new[] { actor })); Failure(Send(s, CommandKind.MoveForward), Reasons.OutOfBounds);
            Equal(actor.Position, s.Snapshot.Tractor(1).Position); Equal(actor.Fuel, s.Snapshot.Tractor(1).Fuel);
        }
        var blocked = new Simulation(World(blockedX: 1)); Failure(Send(blocked, CommandKind.MoveForward), Reasons.Blocked);
    }
    private static void FuelAndSequences()
    {
        var exact = new Simulation(World(Rules(cost: 2), actors: new[] { Actor(fuel: 2) }));
        Check(Send(exact, CommandKind.TurnRight).Results.Single().Ok); Equal(0, exact.Snapshot.Tractor(1).Fuel);
        var low = new Simulation(World(Rules(cost: 2), actors: new[] { Actor(fuel: 1) }));
        Failure(Send(low, CommandKind.Harvest), Reasons.InsufficientFuel); Equal(1, low.Snapshot.Tractor(1).Fuel);
        Equal(1L, low.Snapshot.Tractor(1).LastProcessedRequestId);
        Failure(Send(low, CommandKind.Harvest, request: 1), Reasons.DuplicateRequest); Equal(1L, low.Snapshot.Tractor(1).LastProcessedRequestId);
        Failure(Send(low, (CommandKind)99, request: 8), Reasons.UnsupportedCommand); Equal(9L, low.Snapshot.Tractor(1).NextRequestId);
        Failure(Send(low, CommandKind.TurnLeft, id: 99, request: 1), Reasons.UnknownActor);
    }
    private static void Harvest()
    {
        var s = new Simulation(World(Rules(duration: 2, yield: 3), actors: new[] { Actor(cargo: 2, cargoCapacity: 5) }));
        Send(s, CommandKind.Harvest); Equal(2, s.Snapshot.Tractor(1).Cargo); Equal(CropKind.Wheat, s.Snapshot.TileAt(new GridPosition(0, 0)).Crop);
        Empty(s); Equal(5, s.Snapshot.Tractor(1).Cargo); Equal(CropKind.None, s.Snapshot.TileAt(new GridPosition(0, 0)).Crop);
        Failure(Send(s, CommandKind.Harvest), Reasons.NoCrop);
        var full = new Simulation(World(Rules(yield: 3), actors: new[] { Actor(cargo: 3, cargoCapacity: 5) }));
        Failure(Send(full, CommandKind.Harvest), Reasons.CargoFull); Equal(3, full.Snapshot.Tractor(1).Cargo); Equal(20, full.Snapshot.Tractor(1).Fuel);
        var wrong = new Simulation(World(actors: new[] { Actor(implement: ImplementKind.None) })); Failure(Send(wrong, CommandKind.Harvest), Reasons.IncorrectImplement);
        var immature = new Simulation(World(cropGrowth: 9)); Failure(Send(immature, CommandKind.Harvest), Reasons.ImmatureCrop);
        Check(Send(immature, CommandKind.Harvest).Results.Single().Ok);
    }
    private static void Growth()
    {
        var s = new Simulation(World(cropGrowth: 8)); Check(!s.Sense(1).Ripe); Check(!s.Sense(1).Ripe); Equal(0L, s.Snapshot.Tick);
        Empty(s); Equal(9, s.Snapshot.Tiles[0].Growth); Check(!s.Sense(1).Ripe);
        s.StopAtBoundary(); Equal(9, s.Snapshot.Tiles[0].Growth); Equal(1L, s.Snapshot.Tick);
        Failure(Send(s, CommandKind.Harvest), Reasons.ImmatureCrop); Check(s.Sense(1).Ripe);
        for (int i = 0; i < 20; i++) Empty(s); Equal(10, s.Snapshot.Tiles[0].Growth);
        Equal(1u, s.Snapshot.RngState);
        var max = new Simulation(World(Rules(growth: 1000000), cropGrowth: 0)); Empty(max); Equal(10, max.Snapshot.Tiles[0].Growth);
    }
    private static DepotState D(int fuel = 100, int wheat = 0, int capacity = 100) => new DepotState(1, new GridPosition(0, 0), fuel, wheat, capacity);
    private static void Depot()
    {
        var s = new Simulation(World(Rules(duration: 2), actors: new[] { Actor(fuel: 4, cargo: 5) }, depots: new[] { D(fuel: 16, capacity: 5) }));
        Send(s, CommandKind.Unload); Equal(5, s.Snapshot.Tractor(1).Cargo); Equal(0, s.Snapshot.Depots[0].WheatStock);
        Empty(s); Equal(0, s.Snapshot.Tractor(1).Cargo); Equal(5, s.Snapshot.Depots[0].WheatStock);
        Send(s, CommandKind.Refuel); Equal(0, s.Snapshot.Depots[0].FuelStock); Equal(4, s.Snapshot.Tractor(1).Fuel);
        Empty(s); Equal(20, s.Snapshot.Tractor(1).Fuel);
        var full = new Simulation(World(actors: new[] { Actor(cargo: 5) }, depots: new[] { D(wheat: 1, capacity: 5) }));
        Failure(Send(full, CommandKind.Unload), Reasons.DepotFull); Equal(5, full.Snapshot.Tractor(1).Cargo); Equal(1, full.Snapshot.Depots[0].WheatStock);
        var stock = new Simulation(World(actors: new[] { Actor(fuel: 4) }, depots: new[] { D(fuel: 15) }));
        Failure(Send(stock, CommandKind.Refuel), Reasons.InsufficientStock); Equal(15, stock.Snapshot.Depots[0].FuelStock); Equal(4, stock.Snapshot.Tractor(1).Fuel);
        var remote = new Simulation(World()); Failure(Send(remote, CommandKind.Unload), Reasons.NotAtDepot); Failure(Send(remote, CommandKind.Refuel), Reasons.NotAtDepot);
        var costly = new Simulation(World(Rules(depotCost: 2), actors: new[] { Actor(fuel: 4) }, depots: new[] { D(fuel: 18) }));
        Check(Send(costly, CommandKind.Refuel).Results.Single().Ok); Equal(20, costly.Snapshot.Tractor(1).Fuel); Equal(0, costly.Snapshot.Depots[0].FuelStock);
        var empty = new Simulation(World(depots: new[] { D(fuel: 0) }));
        Check(Send(empty, CommandKind.Refuel).Results.Single().Ok); Check(Send(empty, CommandKind.Unload).Results.Single().Ok);
    }
    private static void Stop()
    {
        foreach (CommandKind kind in Enum.GetValues<CommandKind>())
        {
            var s = new Simulation(World(Rules(duration: 4, cost: 2, depotCost: 2), actors: new[] { Actor(fuel: 10, cargo: 2) }, depots: new[] { D() }));
            Send(s, kind); Empty(s); long tick = s.Snapshot.Tick; uint rng = s.Snapshot.RngState; long cursor = s.Snapshot.NextActorId;
            var stopped = s.StopAtBoundary(); Equal(tick, stopped.Snapshot.Tick); Equal(rng, stopped.Snapshot.RngState); Equal(cursor, stopped.Snapshot.NextActorId);
            Equal(1, stopped.Results.Count); Equal(Reasons.Cancelled, stopped.Results[0].Reason); Equal(DomainEventKind.Cancelled, stopped.Events[0].Kind);
            Equal(10, s.Snapshot.Tractor(1).Fuel); Equal(2, s.Snapshot.Tractor(1).Cargo); Equal(100, s.Snapshot.Depots[0].FuelStock); Equal(0, s.Snapshot.Depots[0].WheatStock);
            Equal(new GridPosition(0, 0), s.Snapshot.Tractor(1).Position); Equal(Heading.East, s.Snapshot.Tractor(1).Heading); Equal(CropKind.Wheat, s.Snapshot.Tiles[0].Crop);
            string hash = WorldStateHash.Compute(s.Snapshot); Equal(0, s.StopAtBoundary().Results.Count); Equal(hash, WorldStateHash.Compute(s.Snapshot));
            Equal(1L, s.Snapshot.Tractor(1).LastProcessedRequestId);
            Send(s, kind); Check(s.Snapshot.PendingActions.Count == 1, "Reservations must be released for subsequent action");
        }
        var fresh = new Simulation(World(cropGrowth: 2)); var snapshot = fresh.Snapshot;
        Equal(0, fresh.StopAtBoundary().Events.Count); Check(ReferenceEquals(snapshot, fresh.Snapshot));
        var complete = new Simulation(World()); Send(complete, CommandKind.Harvest); complete.StopAtBoundary(); Equal(19, complete.Snapshot.Tractor(1).Fuel); Equal(1, complete.Snapshot.Tractor(1).Cargo);
        var immediate = new Simulation(World(Rules(duration: 2))); Send(immediate, CommandKind.MoveForward); immediate.StopAtBoundary(); Equal(20, immediate.Snapshot.Tractor(1).Fuel);
    }
    private static void Batch()
    {
        var s = new Simulation(World(cropGrowth: 1)); string hash = WorldStateHash.Compute(s.Snapshot);
        var duplicate = s.AdvanceTick(new[] { new CommandRequest(1, 1, CommandKind.Harvest), new CommandRequest(1, 2, CommandKind.TurnLeft) });
        Equal(BatchStatus.InvalidBatch, duplicate.Status); Equal(Reasons.DuplicateActor, duplicate.Reason); Equal(0, duplicate.Results.Count); Equal(hash, WorldStateHash.Compute(s.Snapshot));
        Equal(BatchStatus.InvalidBatch, s.AdvanceTick(null!).Status); Equal(BatchStatus.InvalidBatch, s.AdvanceTick(new CommandRequest[] { null! }).Status);
        var tooMany = Enumerable.Range(1, 17).Select(id => new CommandRequest(id, 1, CommandKind.Harvest));
        Equal(BatchStatus.InvalidBatch, s.AdvanceTick(tooMany).Status); Equal(hash, WorldStateHash.Compute(s.Snapshot));
        Equal(1L, Empty(s).Snapshot.Tick);
        Failure(Send(s, CommandKind.Harvest), Reasons.ImmatureCrop); Equal(2L, s.Snapshot.Tick);
    }
    private static void Contention()
    {
        var actors = new[] { Actor(1, x: 0, y: 1), Actor(2, x: 2, y: 1, heading: Heading.West) };
        var s = new Simulation(World(Rules(duration: 3), actors: actors));
        var report = s.AdvanceTick(new[] { new CommandRequest(2, 1, CommandKind.MoveForward), new CommandRequest(1, 1, CommandKind.MoveForward) });
        Equal(1L, report.Submissions[0].Request.ActorId); Equal(SubmissionStatus.Accepted, report.Submissions[0].Status); Equal(Reasons.Occupied, report.Submissions[1].Reason);
        Equal(2L, s.Snapshot.NextActorId); Equal(1, s.Snapshot.PendingActions.Count); Equal(19, s.Snapshot.Tractor(1).Fuel); Equal(20, s.Snapshot.Tractor(2).Fuel);
        Failure(Send(s, CommandKind.MoveForward, id: 2), Reasons.Occupied);
        s.StopAtBoundary();
        // Cursor cycles on empty ticks too, and survives stop.
        var second = new Simulation(World(Rules(duration: 3), actors: actors, cursor: 2));
        var wins = second.AdvanceTick(new[] { new CommandRequest(1, 1, CommandKind.MoveForward), new CommandRequest(2, 1, CommandKind.MoveForward) });
        Equal(2L, wins.Submissions[0].Request.ActorId); Equal(SubmissionStatus.Accepted, wins.Submissions[0].Status);
        var swap = new Simulation(World(actors: new[] { Actor(1, x: 0), Actor(2, x: 1, heading: Heading.West) }));
        var swaps = swap.AdvanceTick(new[] { new CommandRequest(1, 1, CommandKind.MoveForward), new CommandRequest(2, 1, CommandKind.MoveForward) });
        Check(swaps.Results.All(v => v.Reason == Reasons.Occupied)); Equal(2, swaps.Results.Count);
        var follow = new Simulation(World(actors: new[] { Actor(1, x: 0), Actor(2, x: 1) }));
        var following = follow.AdvanceTick(new[] { new CommandRequest(2, 1, CommandKind.MoveForward), new CommandRequest(1, 1, CommandKind.MoveForward) });
        Equal(Reasons.Occupied, following.Submissions[0].Reason); Equal(new GridPosition(2, 0), follow.Snapshot.Tractor(2).Position);
        Check(follow.Snapshot.Tractors.Select(v => v.Position).Distinct().Count() == 2);
    }
    private static void Restore()
    {
        var source = new Simulation(World(Rules(duration: 3), actors: new[] { Actor(last: 6, fuel: 8) }, cropGrowth: 4));
        Send(source, CommandKind.MoveForward); var active = source.Snapshot;
        var other = new Simulation(World()); string original = WorldStateHash.Compute(other.Snapshot);
        Reject(() => other.RestoreStopped(active), Reasons.ActiveState); Equal(original, WorldStateHash.Compute(other.Snapshot));
        Reject(() => new Simulation(active), Reasons.ActiveState); Reject(() => other.RestoreStopped(null!));
        source.StopAtBoundary(); other.RestoreStopped(source.Snapshot);
        Equal(8L, other.Snapshot.Tractor(1).NextRequestId); Equal(WorldStateHash.Compute(source.Snapshot), WorldStateHash.Compute(other.Snapshot));
        for (int i = 0; i < 8; i++)
        {
            if (i == 0) { Send(source, CommandKind.TurnRight); Send(other, CommandKind.TurnRight); }
            else { Empty(source); Empty(other); }
            Equal(WorldStateHash.Compute(source.Snapshot), WorldStateHash.Compute(other.Snapshot));
        }
        Failure(Send(other, CommandKind.TurnLeft, request: 7), Reasons.DuplicateRequest);
    }
    private static void Replay()
    {
        var actors = new[] { Actor(1, x: 0, y: 1), Actor(2, x: 2, y: 1, heading: Heading.West) };
        var a = new Simulation(World(actors: actors, cropGrowth: 8)); var baseWorld = World(actors: actors.Reverse(), cropGrowth: 8);
        var b = new Simulation(new WorldSnapshot(baseWorld.Rules, 0, 3, 3, baseWorld.Tiles.Reverse(), baseWorld.Tractors.Reverse(), baseWorld.Depots, 1, 1));
        Equal(WorldStateHash.Compute(a.Snapshot), WorldStateHash.Compute(b.Snapshot));
        for (int i = 0; i < 50; i++)
        {
            var kind = i % 3 == 0 ? CommandKind.MoveForward : i % 3 == 1 ? CommandKind.TurnRight : CommandKind.Harvest;
            var batch = a.Snapshot.Tractors.Select(v => new CommandRequest(v.ActorId, v.NextRequestId, kind)).ToArray();
            var first = a.AdvanceTick(batch); var second = b.AdvanceTick(batch.Reverse());
            Equal(WorldStateHash.Compute(first.Snapshot), WorldStateHash.Compute(second.Snapshot));
            Equal(string.Join("|", first.Events.Select(EventText)), string.Join("|", second.Events.Select(EventText)));
            Equal(string.Join("|", first.Results.Select(ResultText)), string.Join("|", second.Results.Select(ResultText)));
            Check(first.Events.Zip(first.Events.Skip(1), (left, right) => left.Tick <= right.Tick).All(v => v), "Events must be chronological");
        }
        Check(WorldStateHash.Compute(a.Snapshot).Length == 64);
        var different = new WorldSnapshot(a.Snapshot.Rules, a.Snapshot.Tick, 3, 3, a.Snapshot.Tiles, a.Snapshot.Tractors, a.Snapshot.Depots, 2, a.Snapshot.NextActorId);
        Check(WorldStateHash.Compute(different) != WorldStateHash.Compute(a.Snapshot), "Hash must include RNG");
        var seq = new WorldSnapshot(a.Snapshot.Rules, a.Snapshot.Tick, 3, 3, a.Snapshot.Tiles, a.Snapshot.Tractors.Select(v => Actor(v.ActorId, v.Position.X, v.Position.Y, v.Heading, v.Fuel, v.Cargo, v.CargoCapacity, v.Implement, v.LastProcessedRequestId + 1)), a.Snapshot.Depots, 1, a.Snapshot.NextActorId);
        Check(WorldStateHash.Compute(seq) != WorldStateHash.Compute(a.Snapshot), "Hash must include request sequence");
    }
    private static string EventText(DomainEvent v) => v.Tick + ":" + v.ActorId + ":" + v.RequestId + ":" + v.Command + ":" + v.Kind + ":" + v.Reason;
    private static string ResultText(ActionResult v) => v.ActorId + ":" + v.RequestId + ":" + v.Ok + ":" + v.Reason + ":" + v.TerminalTick;
    private static void Limits()
    {
        Reject(() => new GridPosition(-1, 0)); Reject(() => new GridPosition(64, 0));
        Reject(() => World(width: 0)); Reject(() => new DepotState(1, new GridPosition(0, 0), 1000001, 0, 1));
        Reject(() => new WorldSnapshot(Rules(), 0, 3, 3, World().Tiles, new[] { Actor() }, Array.Empty<DepotState>(), 0, 1));
        Reject(() => new WorldSnapshot(Rules(), 0, 3, 3, World().Tiles, new[] { Actor() }, Array.Empty<DepotState>(), 1, 1, rngAlgorithm: "unknown"));
        Reject(() => World(cursor: 2)); Reject(() => World(cropGrowth: 11));
        var end = new Simulation(World(tick: long.MaxValue)); string hash = WorldStateHash.Compute(end.Snapshot);
        Equal(Reasons.TickOverflow, Empty(end).Reason); Equal(hash, WorldStateHash.Compute(end.Snapshot));
        var near = new Simulation(World(Rules(duration: 2), tick: long.MaxValue - 1));
        Failure(Send(near, CommandKind.MoveForward), Reasons.TickOverflow); Equal(long.MaxValue, near.Snapshot.Tick);
        var final = new Simulation(World(tick: long.MaxValue - 1)); Check(Send(final, CommandKind.Harvest).Results.Single().Ok);
        var exhausted = Actor(last: long.MaxValue);
        try { _ = exhausted.NextRequestId; throw new Exception("Sequence wrapped"); } catch (OverflowException) { }
        var maximum = new Simulation(World(width: 64, height: 64, actors: Enumerable.Range(1, 16).Select(i => Actor(i, x: i - 1)), cursor: 1));
        var results = maximum.AdvanceTick(maximum.Snapshot.Tractors.Select(v => new CommandRequest(v.ActorId, 1, CommandKind.Harvest)));
        Equal(16, results.Results.Count); Equal(4096, results.Snapshot.Tiles.Count);
    }

    private static void RejectedSequenceRestore()
    {
        var s = new Simulation(World(Rules(duration: 5)));
        Send(s, CommandKind.MoveForward, request: 1);
        Failure(Send(s, CommandKind.Harvest, request: 2), Reasons.Busy);
        Failure(Send(s, (CommandKind)99, request: 3), Reasons.Busy);
        // A stale request does not reset processed ID or invalidate the pending action.
        Failure(Send(s, CommandKind.Harvest, request: 1), Reasons.DuplicateRequest);
        Equal(3L, s.Snapshot.Tractor(1).LastProcessedRequestId); Equal(1L, s.Snapshot.PendingActions.Single().Request.RequestId);
        var stopped = s.StopAtBoundary(); Equal(4L, stopped.Snapshot.Tick); Equal(20, stopped.Snapshot.Tractor(1).Fuel);
        Equal(3L, stopped.Snapshot.RequestSequences.Single().LastProcessedRequestId); Equal(4L, stopped.Snapshot.RequestSequences.Single().NextRequestId);
        var restored = new Simulation(World()); restored.RestoreStopped(stopped.Snapshot);
        Equal(WorldStateHash.Compute(s.Snapshot), WorldStateHash.Compute(restored.Snapshot));
        Failure(Send(restored, CommandKind.TurnRight, request: 3), Reasons.DuplicateRequest);
        Failure(Send(restored, (CommandKind)99, request: 4), Reasons.UnsupportedCommand);
        Equal(5L, restored.Snapshot.RequestSequences.Single().NextRequestId);
        Send(restored, CommandKind.MoveForward); Equal(5L, restored.Snapshot.PendingActions.Single().Request.RequestId);
        for (int i = 0; i < 3; i++) Equal(0, Empty(restored).Results.Count);
        var terminal = Empty(restored); Equal(5L, terminal.Results.Single().RequestId); Check(terminal.Results.Single().Ok);
        Equal(19, restored.Snapshot.Tractor(1).Fuel);
        var last = new Simulation(World(actors: new[] { Actor(last: long.MaxValue - 1) }));
        Send(last, CommandKind.Harvest, request: long.MaxValue); Equal(long.MaxValue, last.Snapshot.Tractor(1).LastProcessedRequestId);
        Failure(Send(last, CommandKind.Harvest, request: long.MaxValue), Reasons.DuplicateRequest);
        var exhaustedRestore = new Simulation(World()); exhaustedRestore.RestoreStopped(last.StopAtBoundary().Snapshot);
        Equal(long.MaxValue, exhaustedRestore.Snapshot.RequestSequences.Single().LastProcessedRequestId);
        try { _ = exhaustedRestore.Snapshot.RequestSequences.Single().NextRequestId; throw new Exception("Restored sequence wrapped"); } catch (OverflowException) { }
        // Cursor is persisted independently from request sequence state.
        var actors = new[] { Actor(1, x: 0, y: 1), Actor(2, x: 2, y: 1, heading: Heading.West) };
        var fleet = new Simulation(World(actors: actors)); Empty(fleet); var fleetStopped = fleet.StopAtBoundary();
        var fleetRestored = new Simulation(World()); fleetRestored.RestoreStopped(fleetStopped.Snapshot);
        Equal(2L, fleetRestored.Snapshot.NextActorId);
        var contention = fleetRestored.AdvanceTick(new[] { new CommandRequest(1, 1, CommandKind.MoveForward), new CommandRequest(2, 1, CommandKind.MoveForward) });
        Equal(2L, contention.Submissions[0].Request.ActorId); Equal(SubmissionStatus.Accepted, contention.Submissions[0].Status);
    }
    private static void WorldValidation()
    {
        var world = World();
        Reject(() => new WorldSnapshot(world.Rules, -1, 3, 3, world.Tiles, world.Tractors, world.Depots, 1, 1));
        Reject(() => new WorldSnapshot(world.Rules, 0, 65, 3, world.Tiles, world.Tractors, world.Depots, 1, 1));
        Reject(() => new WorldSnapshot(world.Rules, 0, 3, 3, world.Tiles.Skip(1), world.Tractors, world.Depots, 1, 1));
        Reject(() => new WorldSnapshot(world.Rules, 0, 3, 3, world.Tiles.Skip(1).Append(world.Tiles[1]), world.Tractors, world.Depots, 1, 1));
        Reject(() => new WorldSnapshot(world.Rules, 0, 3, 3, null!, world.Tractors, world.Depots, 1, 1));
        Reject(() => new WorldSnapshot(world.Rules, 0, 3, 3, world.Tiles, Array.Empty<TractorState>(), world.Depots, 1, 1));
        Reject(() => World(actors: new[] { Actor(1), Actor(2) }));
        Reject(() => World(actors: new[] { Actor(x: 3) }));
        Reject(() => World(blockedX: 0));
        Reject(() => World(depots: new[] { D(), D() }));
        Reject(() => World(depots: new[] { D(), new DepotState(2, D().Position, 0, 0, 0) }));
        Reject(() => World(depots: new[] { new DepotState(1, new GridPosition(3, 0), 0, 0, 0) }));
        Reject(() => World(width: 64, actors: Enumerable.Range(1, 17).Select(i => Actor(i, x: i - 1))));
        Reject(() => new TractorState(1, new GridPosition(0, 0), (Heading)99, ImplementKind.None, 0, 0, 0, 0));
        Reject(() => new TractorState(1, new GridPosition(0, 0), Heading.North, ImplementKind.None, 0, 0, 2, 1));
        Reject(() => new DepotState(1, new GridPosition(0, 0), 0, 2, 1));
        Reject(() => new SimulationRules("bad", new ActionRule(1, 0), new ActionRule(1, 0), new ActionRule(1, 0), new ActionRule(1, 0), new ActionRule(1, 0), 0, 0, 1));
        Reject(() => new SimulationRules("bad", new ActionRule(1, 0), new ActionRule(1, 0), new ActionRule(1, 0), new ActionRule(1, 0), new ActionRule(1, 0), 10, 0, 0));
        var stopped = new Simulation(world); string hash = WorldStateHash.Compute(stopped.Snapshot);
        // Invalid restore DTO construction must leave the current live state unchanged.
        Reject(() => stopped.RestoreStopped(new WorldSnapshot(world.Rules, 0, 3, 3, world.Tiles, world.Tractors, world.Depots, 1, 99)));
        Equal(hash, WorldStateHash.Compute(stopped.Snapshot));
    }

    private static void ActiveValidation()
    {
        var s = new Simulation(World(Rules(duration: 3))); Send(s, CommandKind.MoveForward); var w = s.Snapshot; var p = w.PendingActions[0];
        Reject(() => new WorldSnapshot(w.Rules, w.Tick, w.Width, w.Height, w.Tiles, w.Tractors, w.Depots, w.RngState, w.NextActorId,
            new[] { new PendingActionView(p.Request, new GridPosition(2, 0), p.StartTick, p.CompletionTick, p.ReservedFuel) }));
        Reject(() => new WorldSnapshot(w.Rules, w.Tick, w.Width, w.Height, w.Tiles, w.Tractors, w.Depots, w.RngState, w.NextActorId,
            new[] { new PendingActionView(p.Request, p.Target, p.StartTick, p.CompletionTick, 2) }));
        Reject(() => new WorldSnapshot(w.Rules, w.Tick, w.Width, w.Height, w.Tiles, w.Tractors, w.Depots, w.RngState, w.NextActorId,
            new[] { p, p }));
        Reject(() => new WorldSnapshot(w.Rules, w.Tick, w.Width, w.Height, w.Tiles, w.Tractors, w.Depots, w.RngState, w.NextActorId,
            new[] { new PendingActionView(new CommandRequest(1, w.Tractor(1).LastProcessedRequestId + 1, CommandKind.MoveForward), p.Target, p.StartTick, p.CompletionTick, p.ReservedFuel) }));
        Reject(() => new WorldSnapshot(w.Rules, p.CompletionTick, w.Width, w.Height, w.Tiles, w.Tractors, w.Depots, w.RngState, w.NextActorId, new[] { p }));
    }
}
