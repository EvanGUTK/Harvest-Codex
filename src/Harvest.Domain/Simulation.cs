using System;
using System.Collections.Generic;
using System.Linq;

namespace Harvest.Domain
{
    /// <summary>Single-threaded deterministic simulation. A tick is committed only after all state validates.</summary>
    public sealed class Simulation : ISimulation
    {
        public WorldSnapshot Snapshot { get; private set; }
        public Simulation(WorldSnapshot stoppedSnapshot)
        {
            RequireStopped(stoppedSnapshot);
            Snapshot = stoppedSnapshot;
        }

        public SensorSnapshot Sense(long actorId)
        {
            var actor = Snapshot.Tractor(actorId);
            var tile = Snapshot.TileAt(actor.Position);
            return new SensorSnapshot(actor.Position, actor.Heading, tile.Crop == CropKind.Wheat && tile.Growth >= Snapshot.Rules.WheatMaturity, tile.Crop, actor.Fuel, actor.Cargo, actor.Implement);
        }

        public void RestoreStopped(WorldSnapshot snapshot)
        {
            // DTO constructor validation is exhaustive; the stopped-state check occurs before replacement.
            RequireStopped(snapshot);
            Snapshot = snapshot;
        }

        private static void RequireStopped(WorldSnapshot snapshot)
        {
            Contract.Require(snapshot != null, "Snapshot is required.");
            Contract.Require(snapshot!.IsStopped, "Cannot construct/restore active execution.", Reasons.ActiveState);
        }

        public TickReport AdvanceTick(IEnumerable<CommandRequest> commandBatch)
        {
            var requests = new List<CommandRequest>();
            if (commandBatch == null) return InvalidBatch(Reasons.InvalidBatch);
            var submittedActors = new HashSet<long>();
            foreach (var request in commandBatch)
            {
                if (request == null || requests.Count == 16) return InvalidBatch(Reasons.InvalidBatch);
                if (!submittedActors.Add(request.ActorId)) return InvalidBatch(Reasons.DuplicateActor);
                requests.Add(request);
            }
            if (Snapshot.Tick == long.MaxValue) return InvalidBatch(Reasons.TickOverflow);

            var before = Snapshot;
            var actors = before.Tractors.ToList();
            var depots = before.Depots.ToList();
            var tiles = before.Tiles.ToList();
            var pending = before.PendingActions.ToList();
            var submissions = new List<SubmissionOutcome>();
            var results = new List<ActionResult>();
            var events = new List<DomainEvent>();
            long nextTick = before.Tick + 1;
            int cursor = actors.FindIndex(v => v.ActorId == before.NextActorId);
            var order = new Dictionary<long, int>();
            for (int i = 0; i < actors.Count; i++) order[actors[(cursor + i) % actors.Count].ActorId] = i;
            requests.Sort((a, b) =>
            {
                int left = order.TryGetValue(a.ActorId, out int l) ? l : actors.Count;
                int right = order.TryGetValue(b.ActorId, out int r) ? r : actors.Count;
                int compare = left.CompareTo(right);
                return compare != 0 ? compare : a.ActorId.CompareTo(b.ActorId);
            });

            foreach (var request in requests)
            {
                int index = actors.FindIndex(v => v.ActorId == request.ActorId);
                string reason;
                PendingActionView? accepted = null;
                if (index < 0) reason = Reasons.UnknownActor;
                else
                {
                    var actor = actors[index];
                    if (request.RequestId <= actor.LastProcessedRequestId) reason = Reasons.DuplicateRequest;
                    else
                    {
                        // Legitimate rejections also consume sequence IDs. Active action IDs remain independent.
                        actors[index] = Copy(actor, lastRequest: request.RequestId);
                        reason = ValidateAction(before, actor, request, actors, depots, pending, out accepted);
                    }
                }
                if (reason != Reasons.None)
                {
                    submissions.Add(new SubmissionOutcome(request, SubmissionStatus.Rejected, reason));
                    results.Add(new ActionResult(request.ActorId, request.RequestId, false, reason, nextTick));
                    events.Add(new DomainEvent(nextTick, request.ActorId, request.RequestId, request.Kind, DomainEventKind.Rejected, reason));
                    continue;
                }
                var action = accepted!;
                var current = actors[index];
                actors[index] = Copy(current, fuel: current.Fuel - action.ReservedFuel);
                if (request.Kind == CommandKind.Refuel)
                {
                    int depotIndex = depots.FindIndex(v => v.DepotId == action.DepotId);
                    var depot = depots[depotIndex];
                    depots[depotIndex] = Copy(depot, fuel: depot.FuelStock - action.ReservedQuantity);
                }
                pending.Add(action);
                submissions.Add(new SubmissionOutcome(request, SubmissionStatus.Accepted, Reasons.None));
                events.Add(new DomainEvent(before.Tick, request.ActorId, request.RequestId, request.Kind, DomainEventKind.Accepted, Reasons.None));
            }

            foreach (var action in pending.Where(v => v.CompletionTick == nextTick).OrderBy(v => v.Request.ActorId).ToArray())
            {
                Complete(action, actors, depots, tiles, before.Width);
                results.Add(new ActionResult(action.Request.ActorId, action.Request.RequestId, true, Reasons.None, nextTick));
                events.Add(new DomainEvent(nextTick, action.Request.ActorId, action.Request.RequestId, action.Request.Kind, DomainEventKind.Completed, Reasons.None));
                pending.Remove(action);
            }
            for (int i = 0; i < tiles.Count; i++)
            {
                var tile = tiles[i];
                if (tile.Crop == CropKind.Wheat)
                {
                    int growth = (int)Math.Min((long)before.Rules.WheatMaturity, (long)tile.Growth + before.Rules.WheatGrowthPerTick);
                    tiles[i] = new TileState(tile.Position, tile.Blocked, tile.Tilled, tile.Moisture, tile.Crop, growth);
                }
            }
            // Advance independently of input batch size/order, including an empty tick.
            long nextActor = actors[(cursor + 1) % actors.Count].ActorId;
            var snapshot = new WorldSnapshot(before.Rules, nextTick, before.Width, before.Height, tiles, actors, depots, before.RngState, nextActor, pending, before.RngAlgorithm);
            // Events are chronological, then phase/actor ordered; arbitration order remains in Submissions.
            events = events.OrderBy(v => v.Tick).ThenBy(v => v.Kind).ThenBy(v => v.ActorId).ThenBy(v => v.RequestId).ToList();
            var report = new TickReport(snapshot, BatchStatus.Advanced, Reasons.None, submissions, results, events);
            Snapshot = snapshot;
            return report;
        }

        private TickReport InvalidBatch(string reason) => new TickReport(Snapshot, BatchStatus.InvalidBatch, reason, Array.Empty<SubmissionOutcome>(), Array.Empty<ActionResult>(), Array.Empty<DomainEvent>());

        private static string ValidateAction(WorldSnapshot world, TractorState actor, CommandRequest request, List<TractorState> actors, List<DepotState> depots, List<PendingActionView> pending, out PendingActionView? action)
        {
            action = null;
            if (pending.Any(v => v.Request.ActorId == actor.ActorId)) return Reasons.Busy;
            if (!Enum.IsDefined(typeof(CommandKind), request.Kind)) return Reasons.UnsupportedCommand;
            var rule = world.Rules.For(request.Kind);
            if (world.Tick > long.MaxValue - rule.DurationTicks) return Reasons.TickOverflow;
            if (actor.Fuel < rule.FuelCost) return Reasons.InsufficientFuel;
            var target = actor.Position;
            int quantity = 0;
            long depotId = 0;
            switch (request.Kind)
            {
                case CommandKind.MoveForward:
                    int x = actor.Position.X + (actor.Heading == Heading.East ? 1 : actor.Heading == Heading.West ? -1 : 0);
                    int y = actor.Position.Y + (actor.Heading == Heading.North ? 1 : actor.Heading == Heading.South ? -1 : 0);
                    if (x < 0 || x >= world.Width || y < 0 || y >= world.Height) return Reasons.OutOfBounds;
                    target = new GridPosition(x, y);
                    if (world.TileAt(target).Blocked) return Reasons.Blocked;
                    if (actors.Any(v => v.Position == target) || pending.Any(v => v.Request.Kind == CommandKind.MoveForward && v.Target == target)) return Reasons.Occupied;
                    break;
                case CommandKind.Harvest:
                    if (actor.Implement != ImplementKind.Harvester) return Reasons.IncorrectImplement;
                    var tile = world.TileAt(target);
                    if (tile.Crop == CropKind.None) return Reasons.NoCrop;
                    if (tile.Growth < world.Rules.WheatMaturity) return Reasons.ImmatureCrop;
                    quantity = world.Rules.WheatYield;
                    if ((long)actor.Cargo + quantity > actor.CargoCapacity) return Reasons.CargoFull;
                    break;
                case CommandKind.Unload:
                case CommandKind.Refuel:
                    var depot = depots.FirstOrDefault(v => v.Position == actor.Position);
                    if (depot == null) return Reasons.NotAtDepot;
                    depotId = depot.DepotId;
                    if (request.Kind == CommandKind.Unload)
                    {
                        quantity = actor.Cargo;
                        long reserved = pending.Where(v => v.Request.Kind == CommandKind.Unload && v.DepotId == depotId).Sum(v => (long)v.ReservedQuantity);
                        if ((long)depot.WheatStock + reserved + quantity > depot.WheatCapacity) return Reasons.DepotFull;
                    }
                    else
                    {
                        // Fill capacity after the refuel action itself consumes its configured fuel cost.
                        quantity = actor.FuelCapacity - (actor.Fuel - rule.FuelCost);
                        if (depot.FuelStock < quantity) return Reasons.InsufficientStock;
                    }
                    break;
            }
            action = new PendingActionView(request, target, world.Tick, world.Tick + rule.DurationTicks, rule.FuelCost, quantity, depotId);
            return Reasons.None;
        }

        private static void Complete(PendingActionView action, List<TractorState> actors, List<DepotState> depots, List<TileState> tiles, int width)
        {
            int index = actors.FindIndex(v => v.ActorId == action.Request.ActorId);
            var actor = actors[index];
            switch (action.Request.Kind)
            {
                case CommandKind.MoveForward: actors[index] = Copy(actor, position: action.Target); break;
                case CommandKind.TurnLeft: actors[index] = Copy(actor, heading: (Heading)(((int)actor.Heading + 3) % 4)); break;
                case CommandKind.TurnRight: actors[index] = Copy(actor, heading: (Heading)(((int)actor.Heading + 1) % 4)); break;
                case CommandKind.Harvest:
                    int tileIndex = action.Target.Y * width + action.Target.X;
                    var tile = tiles[tileIndex];
                    tiles[tileIndex] = new TileState(tile.Position, tile.Blocked, tile.Tilled, tile.Moisture);
                    actors[index] = Copy(actor, cargo: checked(actor.Cargo + action.ReservedQuantity));
                    break;
                case CommandKind.Unload:
                    int depotIndex = depots.FindIndex(v => v.DepotId == action.DepotId);
                    var depot = depots[depotIndex];
                    depots[depotIndex] = Copy(depot, wheat: checked(depot.WheatStock + action.ReservedQuantity));
                    actors[index] = Copy(actor, cargo: actor.Cargo - action.ReservedQuantity);
                    break;
                case CommandKind.Refuel: actors[index] = Copy(actor, fuel: checked(actor.Fuel + action.ReservedQuantity)); break;
                default: throw new DomainValidationException(Reasons.UnsupportedCommand, "Unknown active action.");
            }
        }

        public StopReport StopAtBoundary()
        {
            var before = Snapshot;
            if (before.IsStopped) return new StopReport(before, Array.Empty<ActionResult>(), Array.Empty<DomainEvent>());
            var actors = before.Tractors.ToList();
            var depots = before.Depots.ToList();
            var results = new List<ActionResult>();
            var events = new List<DomainEvent>();
            foreach (var action in before.PendingActions)
            {
                int index = actors.FindIndex(v => v.ActorId == action.Request.ActorId);
                var actor = actors[index];
                actors[index] = Copy(actor, fuel: checked(actor.Fuel + action.ReservedFuel));
                if (action.Request.Kind == CommandKind.Refuel)
                {
                    int depotIndex = depots.FindIndex(v => v.DepotId == action.DepotId);
                    var depot = depots[depotIndex];
                    depots[depotIndex] = Copy(depot, fuel: checked(depot.FuelStock + action.ReservedQuantity));
                }
                results.Add(new ActionResult(action.Request.ActorId, action.Request.RequestId, false, Reasons.Cancelled, before.Tick));
                events.Add(new DomainEvent(before.Tick, action.Request.ActorId, action.Request.RequestId, action.Request.Kind, DomainEventKind.Cancelled, Reasons.Cancelled));
            }
            var snapshot = new WorldSnapshot(before.Rules, before.Tick, before.Width, before.Height, before.Tiles, actors, depots, before.RngState, before.NextActorId, rngAlgorithm: before.RngAlgorithm);
            var report = new StopReport(snapshot, results, events);
            Snapshot = snapshot;
            return report;
        }

        private static TractorState Copy(TractorState actor, GridPosition? position = null, Heading? heading = null, int? fuel = null, int? cargo = null, long? lastRequest = null)
            => new TractorState(actor.ActorId, position ?? actor.Position, heading ?? actor.Heading, actor.Implement, fuel ?? actor.Fuel, actor.FuelCapacity, cargo ?? actor.Cargo, actor.CargoCapacity, lastRequest ?? actor.LastProcessedRequestId);
        private static DepotState Copy(DepotState depot, int? fuel = null, int? wheat = null)
            => new DepotState(depot.DepotId, depot.Position, fuel ?? depot.FuelStock, wheat ?? depot.WheatStock, depot.WheatCapacity);
    }
}
