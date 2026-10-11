using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Harvest.Domain
{
    public enum Heading { North, East, South, West }
    public enum ImplementKind { None, Harvester }
    public enum CropKind { None, Wheat }
    public enum CommandKind { MoveForward, TurnLeft, TurnRight, Harvest, Unload, Refuel }
    public enum SubmissionStatus { Accepted, Rejected }
    public enum BatchStatus { Advanced, InvalidBatch }
    public enum DomainEventKind { Accepted, Rejected, Completed, Cancelled }

    public static class Reasons
    {
        public const string None = "none", InvalidWorld = "invalid_world", InvalidArgument = "invalid_argument",
            InvalidBatch = "invalid_batch", DuplicateActor = "duplicate_actor", DuplicateRequest = "duplicate_request",
            UnknownActor = "unknown_actor", Busy = "busy", UnsupportedCommand = "unsupported_command",
            OutOfBounds = "out_of_bounds", Blocked = "blocked", Occupied = "occupied",
            InsufficientFuel = "insufficient_fuel", IncorrectImplement = "incorrect_implement",
            NoCrop = "no_crop", ImmatureCrop = "immature_crop", CargoFull = "cargo_full",
            NotAtDepot = "not_at_depot", InsufficientStock = "insufficient_stock", DepotFull = "depot_full",
            Cancelled = "cancelled", TickOverflow = "tick_overflow", ActiveState = "active_state";
    }

    public sealed class DomainValidationException : ArgumentException
    {
        public string Reason { get; }
        public DomainValidationException(string reason, string message) : base(message) { Reason = reason; }
    }

    internal static class Contract
    {
        internal const int MaxResource = 1000000;
        internal static void Require(bool condition, string message, string reason = Reasons.InvalidWorld)
        { if (!condition) throw new DomainValidationException(reason, message); }
        internal static void Resource(int value, string name) => Require(value >= 0 && value <= MaxResource, name + " must be 0..1000000.");
        internal static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values, int max, string name)
        {
            Require(values != null, name + " is required.");
            var copy = new List<T>();
            foreach (var value in values!)
            {
                Require(copy.Count < max, name + " exceeds its bound.");
                Require(value is not null, name + " contains null.");
                copy.Add(value);
            }
            return copy.AsReadOnly();
        }
        internal static void Defined<T>(T value, string name) where T : struct => Require(Enum.IsDefined(typeof(T), value), name + " is unknown.");
    }

    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public int X { get; }
        public int Y { get; }
        public GridPosition(int x, int y)
        { Contract.Require(x >= 0 && x < 64 && y >= 0 && y < 64, "Coordinates must be 0..63."); X = x; Y = y; }
        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is GridPosition p && Equals(p);
        public override int GetHashCode() => X * 64 + Y;
        public static bool operator ==(GridPosition a, GridPosition b) => a.Equals(b);
        public static bool operator !=(GridPosition a, GridPosition b) => !a.Equals(b);
        public override string ToString() => "(" + X + "," + Y + ")";
    }

    public sealed class CommandRequest
    {
        public long ActorId { get; }
        public long RequestId { get; }
        public CommandKind Kind { get; }
        public CommandRequest(long actorId, long requestId, CommandKind kind)
        {
            Contract.Require(actorId > 0 && requestId > 0, "Command IDs must be positive.", Reasons.InvalidArgument);
            // Unknown kinds are representable so execution can report unsupported_command.
            ActorId = actorId; RequestId = requestId; Kind = kind;
        }
    }

    public sealed class ActionResult
    {
        public long ActorId { get; }
        public long RequestId { get; }
        public bool Ok { get; }
        public string Reason { get; }
        public long TerminalTick { get; }
        public ActionResult(long actorId, long requestId, bool ok, string reason, long terminalTick)
        {
            Contract.Require(actorId > 0 && requestId > 0 && terminalTick >= 0, "Invalid result IDs/tick.");
            Contract.Require(!string.IsNullOrEmpty(reason) && reason.Length <= 64 && (ok == (reason == Reasons.None)), "Invalid result reason.");
            ActorId = actorId; RequestId = requestId; Ok = ok; Reason = reason; TerminalTick = terminalTick;
        }
    }

    public sealed class SubmissionOutcome
    {
        public CommandRequest Request { get; }
        public SubmissionStatus Status { get; }
        public string Reason { get; }
        public SubmissionOutcome(CommandRequest request, SubmissionStatus status, string reason)
        {
            Contract.Require(request != null, "Request is required."); Contract.Defined(status, "Submission status");
            Contract.Require(!string.IsNullOrEmpty(reason) && reason.Length <= 64 && ((status == SubmissionStatus.Accepted) == (reason == Reasons.None)), "Invalid submission reason.");
            Request = request!; Status = status; Reason = reason;
        }
    }

    public sealed class DomainEvent
    {
        public long Tick { get; }
        public long ActorId { get; }
        public long RequestId { get; }
        public CommandKind Command { get; }
        public DomainEventKind Kind { get; }
        public string Reason { get; }
        public DomainEvent(long tick, long actorId, long requestId, CommandKind command, DomainEventKind kind, string reason)
        {
            Contract.Require(tick >= 0 && actorId > 0 && requestId > 0, "Invalid event IDs/tick."); Contract.Defined(kind, "Event kind");
            Contract.Require(!string.IsNullOrEmpty(reason) && reason.Length <= 64, "Invalid event reason.");
            Tick = tick; ActorId = actorId; RequestId = requestId; Command = command; Kind = kind; Reason = reason;
        }
    }

    public sealed class TickReport
    {
        public WorldSnapshot Snapshot { get; }
        public BatchStatus Status { get; }
        public string Reason { get; }
        public IReadOnlyList<SubmissionOutcome> Submissions { get; }
        public IReadOnlyList<ActionResult> Results { get; }
        public IReadOnlyList<DomainEvent> Events { get; }
        public TickReport(WorldSnapshot snapshot, BatchStatus status, string reason, IEnumerable<SubmissionOutcome> submissions, IEnumerable<ActionResult> results, IEnumerable<DomainEvent> events)
        {
            Contract.Require(snapshot != null, "Snapshot required."); Contract.Defined(status, "Batch status");
            Contract.Require(!string.IsNullOrEmpty(reason) && reason.Length <= 64 && ((status == BatchStatus.Advanced) == (reason == Reasons.None)), "Invalid batch reason.");
            Snapshot = snapshot!; Status = status; Reason = reason;
            Submissions = Contract.Copy(submissions, 16, "Submissions"); Results = Contract.Copy(results, 32, "Results"); Events = Contract.Copy(events, 48, "Events");
        }
    }

    public sealed class StopReport
    {
        public WorldSnapshot Snapshot { get; }
        public IReadOnlyList<ActionResult> Results { get; }
        public IReadOnlyList<DomainEvent> Events { get; }
        public StopReport(WorldSnapshot snapshot, IEnumerable<ActionResult> results, IEnumerable<DomainEvent> events)
        {
            Contract.Require(snapshot != null && snapshot.PendingActions.Count == 0, "Stop snapshot must be stopped.");
            Snapshot = snapshot!; Results = Contract.Copy(results, 16, "Results"); Events = Contract.Copy(events, 16, "Events");
        }
    }

    public interface ISimulation
    {
        WorldSnapshot Snapshot { get; }
        SensorSnapshot Sense(long actorId);
        TickReport AdvanceTick(IEnumerable<CommandRequest> commandBatch);
        StopReport StopAtBoundary();
        void RestoreStopped(WorldSnapshot snapshot);
    }
}
