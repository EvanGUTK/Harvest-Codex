using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Harvest.Language
{
    public readonly struct SourceSpan
    {
        public int Offset { get; }
        public int Length { get; }
        public int Line { get; }
        public int Column { get; }
        public SourceSpan(int offset, int length, int line, int column)
        { if (offset < 0 || length < 0 || line < 1 || column < 1 || (long)offset + length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(offset)); Offset = offset; Length = length; Line = line; Column = column; }
    }
    public sealed class Diagnostic
    {
        public string Code { get; }
        public string Message { get; }
        public SourceSpan Span { get; }
        public Diagnostic(string code, string message, SourceSpan span) { Code = code ?? throw new ArgumentNullException(nameof(code)); Message = message ?? throw new ArgumentNullException(nameof(message)); Span = span; }
    }
    public sealed class Capabilities
    {
        private readonly HashSet<string> allowed;
        public Capabilities(IEnumerable<string> names) { if (names == null) throw new ArgumentNullException(nameof(names)); allowed = new HashSet<string>(StringComparer.Ordinal); int count = 0; foreach (string name in names) { if (++count > 64 || string.IsNullOrEmpty(name) || name.Length > 64) throw new ArgumentException("Capabilities must be bounded nonempty names.", nameof(names)); allowed.Add(name); } }
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
        public ActionProposal(long actorId, long requestId, string apiName)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            switch (apiName) { case "move_forward": case "turn_left": case "turn_right": case "harvest": case "unload": case "refuel": break; default: throw new ArgumentException("Unknown action API.", nameof(apiName)); }
            ActorId = actorId; RequestId = requestId; ApiName = apiName;
        }
    }
    public sealed class TerminalResult
    {
        public long ActorId { get; }
        public long RequestId { get; }
        public bool Ok { get; }
        public string Reason { get; }
        public TerminalResult(long actorId, long requestId, bool ok, string reason)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (requestId <= 0) throw new ArgumentOutOfRangeException(nameof(requestId));
            if (string.IsNullOrEmpty(reason) || reason.Length > 64) throw new ArgumentException("Reason must be a bounded code.", nameof(reason));
            foreach (char c in reason) if (!(c >= 'a' && c <= 'z') && c != '_') throw new ArgumentException("Reason must be a lower-case code.", nameof(reason));
            if (ok != (reason == "none")) throw new ArgumentException("Success uses none; failure requires a reason.", nameof(reason));
            ActorId = actorId; RequestId = requestId; Ok = ok; Reason = reason;
        }
    }
    public sealed class TickInput
    {
        public long Tick { get; }
        public IReadOnlyDictionary<string, object> Sensor { get; }
        public TerminalResult? Result { get; }
        public Capabilities Capabilities { get; }
        public Func<long> NextRequestId { get; }
        public TickInput(long tick, IDictionary<string, object> sensor, TerminalResult? result, Capabilities capabilities, Func<long> nextRequestId)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (sensor == null) throw new ArgumentNullException(nameof(sensor));
            if (capabilities == null) throw new ArgumentNullException(nameof(capabilities));
            if (nextRequestId == null) throw new ArgumentNullException(nameof(nextRequestId));
            if (sensor.Count > 8) throw new ArgumentException("Too many sensor fields.", nameof(sensor));
            var copy = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in sensor)
            {
                bool valid;
                switch (pair.Key)
                {
                    case "x": case "y": case "fuel": case "cargo": valid = pair.Value is long n && n >= 0; break;
                    case "ripe": valid = pair.Value is bool; break;
                    case "heading": case "crop": case "implement": valid = pair.Value is string text && text.Length <= RuntimeLimits.StringBytes && Encoding.UTF8.GetByteCount(text) <= RuntimeLimits.StringBytes; break;
                    default: valid = false; break;
                }
                if (!valid) throw new ArgumentException("Sensor contains an unknown field or invalid scalar.", nameof(sensor));
                copy.Add(pair.Key, pair.Value);
            }
            Tick = tick; Sensor = new ReadOnlyDictionary<string, object>(copy); Result = result; Capabilities = capabilities; NextRequestId = nextRequestId;
        }
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
        { Status = status; InstructionsUsed = used; ActiveSpan = span; Proposal = proposal; Logs = new List<string>(logs).AsReadOnly(); Diagnostic = diagnostic; }
    }
}
