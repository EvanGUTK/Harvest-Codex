using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Harvest.Language
{
    public readonly struct SourceSpan
    {
        public int Offset { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public SourceSpan(int offset, int length, int line, int column)
        { Offset = offset; Length = length; Line = line; Column = column; }
    }
    public sealed class Diagnostic
    {
        public string Code { get; }
        public string Message { get; }
        public SourceSpan Span { get; }
        public Diagnostic(string code, string message, SourceSpan span) { Code = code; Message = message; Span = span; }
    }
    public sealed class Capabilities
    {
        private readonly HashSet<string> allowed;
        public Capabilities(IEnumerable<string> names) { allowed = new HashSet<string>(names, StringComparer.Ordinal); }
        public bool Allows(string name) => allowed.Contains(name);
        public static Capabilities All => new Capabilities(new[] { "assignment", "arithmetic", "conditions", "for", "while", "move_forward", "turn_left", "turn_right", "harvest", "unload", "refuel", "sense", "position", "fuel", "cargo", "wait", "log", "range" });
    }
    public sealed class RuntimeLimits
    {
        public const int SourceBytes = 65536, Tokens = 8192, Nodes = 4096, Depth = 64;
        public const int Variables = 256, ValueCells = 4096, StringBytes = 4096;
        public const int RangeCount = 1000000, LogsPerTick = 16, LogBytes = 256;
        public int InstructionsPerTick { get; }
        public RuntimeLimits(int instructionsPerTick = 256)
        { if (instructionsPerTick < 1 || instructionsPerTick > 256) throw new ArgumentOutOfRangeException(nameof(instructionsPerTick)); InstructionsPerTick = instructionsPerTick; }
    }
    // Language DTOs only. Application maps these to authoritative Domain DTOs.
    public sealed class ActionProposal
    {
        public long ActorId { get; }
        public long RequestId { get; }
        public string ApiName { get; }
        public ActionProposal(long actorId, long requestId, string apiName) { ActorId = actorId; RequestId = requestId; ApiName = apiName; }
    }
    public sealed class TerminalResult
    {
        public long ActorId { get; }
        public long RequestId { get; }
        public bool Ok { get; }
        public string Reason { get; }
        public TerminalResult(long actorId, long requestId, bool ok, string reason)
        { ActorId = actorId; RequestId = requestId; Ok = ok; Reason = reason; }
    }
    public sealed class TickInput
    {
        public long Tick { get; }
        public IReadOnlyDictionary<string, object> Sensor { get; }
        public TerminalResult? Result { get; }
        public Capabilities Capabilities { get; }
        public Func<long> NextRequestId { get; }
        public TickInput(long tick, IDictionary<string, object> sensor, TerminalResult? result, Capabilities capabilities, Func<long> nextRequestId)
        { if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick)); Tick = tick; Sensor = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(sensor)); Result = result; Capabilities = capabilities; NextRequestId = nextRequestId; }
    }
    public enum ExecutionStatus { Ready, WaitingAction, WaitingTick, Completed, Faulted, Stopped }
    public sealed class RunSlice
    {
        public ExecutionStatus Status { get; }
        public int InstructionsUsed { get; }
        public SourceSpan ActiveSpan { get; }
        public ActionProposal? Proposal { get; }
        public IReadOnlyList<string> Logs { get; }
        public Diagnostic? Diagnostic { get; }
        internal RunSlice(ExecutionStatus status, int used, SourceSpan span, ActionProposal? proposal, List<string> logs, Diagnostic? diagnostic)
        { Status = status; InstructionsUsed = used; ActiveSpan = span; Proposal = proposal; Logs = logs.AsReadOnly(); Diagnostic = diagnostic; }
    }
}
