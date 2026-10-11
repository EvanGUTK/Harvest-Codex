using System;
using System.Collections.Generic;
using System.Linq;

namespace Harvest.Domain
{
    public sealed class TileState
    {
        public GridPosition Position { get; }
        public bool Blocked { get; }
        public bool Tilled { get; }
        public int Moisture { get; }
        public CropKind Crop { get; }
        public int Growth { get; }
        public TileState(GridPosition position, bool blocked = false, bool tilled = false, int moisture = 0, CropKind crop = CropKind.None, int growth = 0)
        {
            Contract.Resource(moisture, "Moisture"); Contract.Resource(growth, "Growth"); Contract.Defined(crop, "Crop");
            Contract.Require((crop != CropKind.None || growth == 0) && (!blocked || crop == CropKind.None), "Empty/blocked tile has crop growth.");
            Position = position; Blocked = blocked; Tilled = tilled; Moisture = moisture; Crop = crop; Growth = growth;
        }
    }

    public sealed class TractorState
    {
        public long ActorId { get; }
        public GridPosition Position { get; }
        public Heading Heading { get; }
        public ImplementKind Implement { get; }
        // Available fuel excludes any pending action's reservation.
        public int Fuel { get; }
        public int FuelCapacity { get; }
        public int Cargo { get; }
        public int CargoCapacity { get; }
        public long LastProcessedRequestId { get; }
        public long NextRequestId => checked(LastProcessedRequestId + 1);
        public TractorState(long actorId, GridPosition position, Heading heading, ImplementKind implement, int fuel, int fuelCapacity, int cargo, int cargoCapacity, long lastProcessedRequestId = 0)
        {
            Contract.Require(actorId > 0 && lastProcessedRequestId >= 0, "Invalid tractor/sequence ID.");
            Contract.Defined(heading, "Heading"); Contract.Defined(implement, "Implement");
            Contract.Resource(fuel, "Fuel"); Contract.Resource(fuelCapacity, "Fuel capacity"); Contract.Resource(cargo, "Cargo"); Contract.Resource(cargoCapacity, "Cargo capacity");
            Contract.Require(fuel <= fuelCapacity && cargo <= cargoCapacity, "Tractor resources exceed capacity.");
            ActorId = actorId; Position = position; Heading = heading; Implement = implement; Fuel = fuel; FuelCapacity = fuelCapacity; Cargo = cargo; CargoCapacity = cargoCapacity; LastProcessedRequestId = lastProcessedRequestId;
        }
    }

    public sealed class DepotState
    {
        public long DepotId { get; }
        public GridPosition Position { get; }
        public int FuelStock { get; }
        public int WheatStock { get; }
        public int WheatCapacity { get; }
        public DepotState(long depotId, GridPosition position, int fuelStock, int wheatStock, int wheatCapacity)
        {
            Contract.Require(depotId > 0, "Depot ID must be positive.");
            Contract.Resource(fuelStock, "Depot fuel"); Contract.Resource(wheatStock, "Depot wheat"); Contract.Resource(wheatCapacity, "Depot capacity");
            Contract.Require(wheatStock <= wheatCapacity, "Depot capacity exceeded.");
            DepotId = depotId; Position = position; FuelStock = fuelStock; WheatStock = wheatStock; WheatCapacity = wheatCapacity;
        }
    }

    public sealed class PendingActionView
    {
        public CommandRequest Request { get; }
        public GridPosition Target { get; }
        public long StartTick { get; }
        public long CompletionTick { get; }
        public int ReservedFuel { get; }
        // Harvest yield, unload wheat, or refuel fuel quantity; zero for move/turn.
        public int ReservedQuantity { get; }
        public long DepotId { get; }
        public PendingActionView(CommandRequest request, GridPosition target, long startTick, long completionTick, int reservedFuel, int reservedQuantity = 0, long depotId = 0)
        {
            Contract.Require(request != null && startTick >= 0 && completionTick > startTick && completionTick - startTick <= 10000 && depotId >= 0, "Invalid pending action.");
            Contract.Resource(reservedFuel, "Reserved fuel"); Contract.Resource(reservedQuantity, "Reserved quantity");
            Request = request!; Target = target; StartTick = startTick; CompletionTick = completionTick; ReservedFuel = reservedFuel; ReservedQuantity = reservedQuantity; DepotId = depotId;
        }
    }

    public sealed class RequestSequenceState
    {
        public long ActorId { get; }
        public long LastProcessedRequestId { get; }
        public long NextRequestId => checked(LastProcessedRequestId + 1);
        public RequestSequenceState(long actorId, long lastProcessedRequestId)
        {
            Contract.Require(actorId > 0 && lastProcessedRequestId >= 0, "Invalid sequence state.");
            ActorId = actorId; LastProcessedRequestId = lastProcessedRequestId;
        }
    }

    public sealed class SensorSnapshot
    {
        public GridPosition Position { get; }
        public Heading Heading { get; }
        public bool Ripe { get; }
        public CropKind Crop { get; }
        public int Fuel { get; }
        public int Cargo { get; }
        public ImplementKind Implement { get; }
        public SensorSnapshot(GridPosition position, Heading heading, bool ripe, CropKind crop, int fuel, int cargo, ImplementKind implement)
        {
            Contract.Defined(heading, "Heading"); Contract.Defined(crop, "Crop"); Contract.Defined(implement, "Implement"); Contract.Resource(fuel, "Fuel"); Contract.Resource(cargo, "Cargo");
            Contract.Require(!ripe || crop != CropKind.None, "Empty tile cannot be ripe.");
            Position = position; Heading = heading; Ripe = ripe; Crop = crop; Fuel = fuel; Cargo = cargo; Implement = implement;
        }
    }

    public sealed class WorldSnapshot
    {
        public const string SupportedRngAlgorithm = "xorshift32-v1";
        public string RulesVersion => Rules.Version;
        public SimulationRules Rules { get; }
        public long Tick { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<TileState> Tiles { get; }
        public IReadOnlyList<TractorState> Tractors { get; }
        public IReadOnlyList<DepotState> Depots { get; }
        public string RngAlgorithm { get; }
        public uint RngState { get; }
        public long NextActorId { get; }
        public IReadOnlyList<RequestSequenceState> RequestSequences { get; }
        public IReadOnlyList<PendingActionView> PendingActions { get; }
        public bool IsStopped => PendingActions.Count == 0;
        public WorldSnapshot(SimulationRules rules, long tick, int width, int height, IEnumerable<TileState> tiles, IEnumerable<TractorState> tractors, IEnumerable<DepotState> depots, uint rngState, long nextActorId, IEnumerable<PendingActionView>? pendingActions = null, string rngAlgorithm = SupportedRngAlgorithm)
        {
            Contract.Require(rules != null && tick >= 0 && width > 0 && width <= 64 && height > 0 && height <= 64, "Invalid world rules/tick/grid.");
            Contract.Require(rngState != 0 && rngAlgorithm == SupportedRngAlgorithm, "Unsupported/zero RNG state.");
            Rules = rules!; Tick = tick; Width = width; Height = height; RngState = rngState; RngAlgorithm = rngAlgorithm; NextActorId = nextActorId;
            Tiles = Array.AsReadOnly(Contract.Copy(tiles, 4096, "Tiles").OrderBy(v => v.Position.Y).ThenBy(v => v.Position.X).ToArray());
            Tractors = Array.AsReadOnly(Contract.Copy(tractors, 16, "Tractors").OrderBy(v => v.ActorId).ToArray());
            Depots = Array.AsReadOnly(Contract.Copy(depots, 4096, "Depots").OrderBy(v => v.DepotId).ToArray());
            PendingActions = Array.AsReadOnly(Contract.Copy(pendingActions ?? Array.Empty<PendingActionView>(), 16, "Pending actions").OrderBy(v => v.Request.ActorId).ToArray());
            RequestSequences = Array.AsReadOnly(Tractors.Select(v => new RequestSequenceState(v.ActorId, v.LastProcessedRequestId)).ToArray());
            Validate();
        }
        public TileState TileAt(GridPosition p)
        { Contract.Require(InBounds(p), "Tile is outside grid."); return Tiles[p.Y * Width + p.X]; }
        public TractorState Tractor(long actorId)
        { return Tractors.FirstOrDefault(v => v.ActorId == actorId) ?? throw new DomainValidationException(Reasons.UnknownActor, "Unknown tractor."); }
        public bool InBounds(GridPosition p) => p.X < Width && p.Y < Height;
        public static uint StateFromSeed(uint seed) => seed == 0 ? 1u : seed;

        private void Validate()
        {
            Contract.Require(Tiles.Count == Width * Height, "Grid must contain every tile exactly once.");
            for (int i = 0; i < Tiles.Count; i++)
            {
                var tile = Tiles[i];
                Contract.Require(tile.Position.X == i % Width && tile.Position.Y == i / Width && tile.Growth <= Rules.WheatMaturity, "Invalid tile coordinates/growth.");
            }
            Contract.Require(Tractors.Count > 0 && Tractors.Select(v => v.ActorId).Distinct().Count() == Tractors.Count && Tractors.Select(v => v.Position).Distinct().Count() == Tractors.Count, "Tractor IDs/positions must be unique.");
            Contract.Require(Tractors.Any(v => v.ActorId == NextActorId), "Scheduler cursor must name a tractor.");
            foreach (var tractor in Tractors) Contract.Require(InBounds(tractor.Position) && !TileAt(tractor.Position).Blocked, "Tractor on invalid tile.");
            Contract.Require(Depots.Select(v => v.DepotId).Distinct().Count() == Depots.Count && Depots.Select(v => v.Position).Distinct().Count() == Depots.Count, "Depot IDs/positions must be unique.");
            foreach (var depot in Depots) Contract.Require(InBounds(depot.Position) && !TileAt(depot.Position).Blocked, "Depot on invalid tile.");
            Contract.Require(PendingActions.Select(v => v.Request.ActorId).Distinct().Count() == PendingActions.Count, "Multiple actions per tractor.");
            var moves = new HashSet<GridPosition>();
            foreach (var pending in PendingActions)
            {
                var tractor = Tractor(pending.Request.ActorId);
                Contract.Require(Enum.IsDefined(typeof(CommandKind), pending.Request.Kind), "Unknown pending command.");
                var rule = Rules.For(pending.Request.Kind);
                Contract.Require(pending.Request.RequestId <= tractor.LastProcessedRequestId && pending.StartTick <= Tick && pending.CompletionTick > Tick && pending.CompletionTick - pending.StartTick == rule.DurationTicks && pending.ReservedFuel == rule.FuelCost && InBounds(pending.Target), "Invalid pending sequence/timing/reservation.");
                Contract.Require((long)tractor.Fuel + pending.ReservedFuel <= tractor.FuelCapacity, "Reserved fuel exceeds capacity.");
                if (pending.Request.Kind == CommandKind.MoveForward)
                {
                    int dx = tractor.Heading == Heading.East ? 1 : tractor.Heading == Heading.West ? -1 : 0;
                    int dy = tractor.Heading == Heading.North ? 1 : tractor.Heading == Heading.South ? -1 : 0;
                    Contract.Require(pending.Target.X == tractor.Position.X + dx && pending.Target.Y == tractor.Position.Y + dy && !TileAt(pending.Target).Blocked && Tractors.All(v => v.Position != pending.Target) && moves.Add(pending.Target) && pending.ReservedQuantity == 0 && pending.DepotId == 0, "Invalid movement reservation.");
                }
                else
                {
                    Contract.Require(pending.Target == tractor.Position, "Stationary action has wrong target.");
                    if (pending.Request.Kind == CommandKind.Harvest)
                        Contract.Require(tractor.Implement == ImplementKind.Harvester && TileAt(pending.Target).Crop == CropKind.Wheat && TileAt(pending.Target).Growth == Rules.WheatMaturity && pending.ReservedQuantity == Rules.WheatYield && (long)tractor.Cargo + pending.ReservedQuantity <= tractor.CargoCapacity && pending.DepotId == 0, "Invalid harvest reservation.");
                    else if (pending.Request.Kind == CommandKind.Unload || pending.Request.Kind == CommandKind.Refuel)
                    {
                        var depot = Depots.FirstOrDefault(v => v.DepotId == pending.DepotId);
                        Contract.Require(depot != null && depot.Position == tractor.Position, "Invalid depot action.");
                        if (pending.Request.Kind == CommandKind.Unload) Contract.Require(pending.ReservedQuantity == tractor.Cargo && (long)depot!.WheatStock + pending.ReservedQuantity <= depot.WheatCapacity, "Invalid unload reservation.");
                        else Contract.Require((long)tractor.Fuel + pending.ReservedFuel + pending.ReservedQuantity == tractor.FuelCapacity && (long)depot!.FuelStock + pending.ReservedQuantity <= Contract.MaxResource, "Invalid refuel reservation.");
                    }
                    else Contract.Require(pending.ReservedQuantity == 0 && pending.DepotId == 0, "Turn has unexpected reservations.");
                }
            }
        }
    }
}
