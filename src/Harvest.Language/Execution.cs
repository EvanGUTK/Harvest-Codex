using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Harvest.Language
{
    internal sealed class RecordValue
    {
        internal readonly Dictionary<string, object> Fields;
        internal RecordValue(Dictionary<string, object> fields) { Fields = fields; }
    }
    internal sealed class LoopState
    {
        internal long Index, Count;
        internal LoopState(long count) { Count = count; }
    }
    public sealed class Execution
    {
        private readonly CompiledProgram program; private readonly long actorId;
        private readonly Dictionary<string, object> variables = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, LoopState> loops = new Dictionary<string, LoopState>();
        private readonly List<object> stack = new List<object>();
        private int pc; private long pendingId, wakeTick = -1, lastTick = -1, lastRequestId;
        private ExecutionStatus status = ExecutionStatus.Ready; private Diagnostic? fault; private SourceSpan span = new SourceSpan(0, 0, 1, 1);
        internal Execution(CompiledProgram program, long actorId)
        { if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId)); this.program = program; this.actorId = actorId; }
        public ExecutionStatus Status => status;
        public void Stop() { status = ExecutionStatus.Stopped; stack.Clear(); variables.Clear(); loops.Clear(); pendingId = 0; }
        public RunSlice Resume(TickInput input)
        {
            var logs = new List<string>(); int used = 0; ActionProposal? proposal = null;
            RunSlice Slice() => new RunSlice(status, used, span, proposal, logs, fault);
            if (status == ExecutionStatus.Completed || status == ExecutionStatus.Faulted || status == ExecutionStatus.Stopped) return Slice();
            try
            {
                if (input.Tick <= lastTick) throw Error("invalid_tick", "Resume requires a strictly increasing simulation tick.");
                lastTick = input.Tick;
                if (pendingId != 0)
                {
                    if (input.Result == null) return Slice();
                    if (input.Result.ActorId != actorId || input.Result.RequestId != pendingId) throw Error("gateway_failure", "Terminal result does not match the pending action.");
                    Push(new RecordValue(new Dictionary<string, object> { ["ok"] = input.Result.Ok, ["reason"] = ValidateScalar(input.Result.Reason) })); pendingId = 0;
                }
                else if (input.Result != null) throw Error("gateway_failure", "Unexpected terminal result.");
                if (wakeTick >= 0)
                { if (input.Tick < wakeTick) return Slice(); wakeTick = -1; Push(true); }
                status = ExecutionStatus.Ready;
                while (pc < program.Code.Length && used < program.Limits.InstructionsPerTick)
                {
                    Instruction instruction = program.Code[pc++]; span = instruction.Span; used++;
                    switch (instruction.Op)
                    {
                        case Op.Push: Push(instruction.Arg!); break;
                        case Op.PushArithmetic: Gate(input, "arithmetic"); Push(instruction.Arg!); break;
                        case Op.Load:
                            if (!variables.TryGetValue((string)instruction.Arg!, out object value)) throw Error("unknown_name", "Variable has not been assigned: " + instruction.Arg);
                            Push(value); break;
                        case Op.Store: Gate(input, "assignment"); Store((string)instruction.Arg!, Pop()); break;
                        case Op.Pop: Pop(); break;
                        case Op.Unary:
                            string unary = (string)instruction.Arg!; Gate(input, unary == "not" || unary == "bool" ? "conditions" : "arithmetic");
                            object operand = Pop();
                            if (unary == "not") Push(!Bool(operand)); else if (unary == "bool") Push(Bool(operand)); else Push(unary == "-" ? checked(-Integer(operand)) : Integer(operand)); break;
                        case Op.Binary:
                            Gate(input, "arithmetic"); object right = Pop(), left = Pop(); Push(Binary((string)instruction.Arg!, left, right)); break;
                        case Op.Field:
                            if (!(Pop() is RecordValue record) || !record.Fields.TryGetValue((string)instruction.Arg!, out object field)) throw Error("type_error", "Value does not expose that immutable field.");
                            Push(field); break;
                        case Op.Jump: if (instruction.Arg is string feature) Gate(input, feature); pc = instruction.Target; break;
                        case Op.JumpFalse: Gate(input, (string)instruction.Arg!); if (!Bool(Pop())) pc = instruction.Target; break;
                        case Op.ShortAnd: Gate(input, "conditions"); if (!Bool(Peek())) pc = instruction.Target; else Pop(); break;
                        case Op.ShortOr: Gate(input, "conditions"); if (Bool(Peek())) pc = instruction.Target; else Pop(); break;
                        case Op.RangeStart:
                            Gate(input, "for"); Gate(input, "range"); long count = Integer(Pop());
                            if (count < 0 || count > RuntimeLimits.RangeCount) throw Error("range_limit", "range count must be 0..1,000,000.");
                            string[] names = (string[])instruction.Arg!;
                            if (count == 0) pc = instruction.Target; else { loops[names[0]] = new LoopState(count); Store(names[1], 0L); }
                            break;
                        case Op.RangeNext:
                            Gate(input, "for"); string[] next = (string[])instruction.Arg!; LoopState loop = loops[next[0]];
                            loop.Index++;
                            if (loop.Index < loop.Count) { Store(next[1], loop.Index); pc = instruction.Target; } else loops.Remove(next[0]); break;
                        case Op.Call:
                            string api = (string)instruction.Arg!; Gate(input, api);
                            switch (api)
                            {
                                case "sense": Push(Sensor(input, false)); break;
                                case "position": Push(Sensor(input, true)); break;
                                case "fuel": case "cargo": Push(SensorField(input, api)); break;
                                case "log":
                                    string formatted = Format(Pop()); if (logs.Count < RuntimeLimits.LogsPerTick) logs.Add(formatted);
                                    else if (logs.Count == RuntimeLimits.LogsPerTick) logs[logs.Count - 1] = "[log limit reached; output truncated]";
                                    Push(true); break;
                                case "wait":
                                    long ticks = Integer(Pop()); if (ticks < 1 || ticks > 1000000) throw Error("wait_limit", "wait expects 1..1,000,000 ticks.");
                                    wakeTick = checked(input.Tick + ticks); status = ExecutionStatus.WaitingTick; return Slice();
                                default:
                                    long id = input.NextRequestId(); if (id <= 0 || id <= lastRequestId) throw Error("gateway_failure", "Application request IDs must be positive and increasing.");
                                    pendingId = lastRequestId = id; proposal = new ActionProposal(actorId, id, api); status = ExecutionStatus.WaitingAction; return Slice();
                            }
                            break;
                    }
                    if (stack.Count + variables.Count * 12 + loops.Count * 2 > RuntimeLimits.ValueCells) throw Error("memory_limit", "Live value cell limit exceeded.");
                }
                if (pc >= program.Code.Length) status = ExecutionStatus.Completed;
            }
            catch (LanguageError error) { fault = error.Diagnostic; status = ExecutionStatus.Faulted; }
            catch (OverflowException) { fault = new Diagnostic("numeric_overflow", "Checked int64 arithmetic overflow.", span); status = ExecutionStatus.Faulted; }
            catch (Exception) { fault = new Diagnostic("gateway_failure", "Runtime boundary failed; check the supplied gateway data.", span); status = ExecutionStatus.Faulted; }
            return Slice();
        }
        private LanguageError Error(string code, string message) => new LanguageError(code, message, span);
        private void Gate(TickInput input, string name) { if (!input.Capabilities.Allows(name)) throw Error("capability_locked", "Capability is locked: " + name); }
        private void Push(object value) { if (stack.Count >= RuntimeLimits.ValueCells) throw Error("memory_limit", "Operand stack limit exceeded."); stack.Add(value); }
        private object Peek() => stack[stack.Count - 1];
        private object Pop() { object value = Peek(); stack.RemoveAt(stack.Count - 1); return value; }
        private void Store(string name, object value)
        { if (!variables.ContainsKey(name) && variables.Count >= RuntimeLimits.Variables) throw Error("variable_limit", "Too many variables."); variables[name] = value; }
        private bool Bool(object value) { if (value is bool b) return b; throw Error("type_error", "Expected a boolean."); }
        private long Integer(object value) { if (value is long n) return n; throw Error("type_error", "Expected an int64 integer."); }
        private object Binary(string op, object left, object right)
        {
            if (op == "==" || op == "!=")
            { if (left.GetType() != right.GetType() || left is RecordValue) throw Error("type_error", "Equality requires matching scalar types."); bool equal = left.Equals(right); return op == "==" ? equal : !equal; }
            if (op == "+" && left is string a && right is string b)
            {
                if (Encoding.UTF8.GetByteCount(a) + Encoding.UTF8.GetByteCount(b) > RuntimeLimits.StringBytes) throw Error("string_limit", "Concatenation exceeds string limit.");
                return a + b;
            }
            long x = Integer(left), y = Integer(right);
            switch (op)
            {
                case "+": return checked(x + y); case "-": return checked(x - y); case "*": return checked(x * y);
                case "<": return x < y; case "<=": return x <= y; case ">": return x > y; case ">=": return x >= y;
                case "//": case "%":
                    if (y == 0) throw Error("divide_by_zero", "Division by zero.");
                    if (x == long.MinValue && y == -1) { if (op == "%") return 0L; throw new OverflowException(); }
                    long quotient = x / y, remainder = x % y;
                    if (remainder != 0 && (remainder < 0) != (y < 0)) { quotient = checked(quotient - 1); remainder = checked(remainder + y); }
                    return op == "//" ? quotient : remainder;
                default: throw Error("unsupported_syntax", "Unsupported operator.");
            }
        }
        private object SensorField(TickInput input, string name)
        { if (!input.Sensor.TryGetValue(name, out object value)) throw Error("gateway_failure", "Missing sensor field: " + name); return ValidateScalar(value); }
        private object ValidateScalar(object value)
        {
            if (value is long || value is bool) return value;
            if (value is string text && text.Length <= RuntimeLimits.StringBytes && Encoding.UTF8.GetByteCount(text) <= RuntimeLimits.StringBytes) return text;
            throw Error("gateway_failure", "Sensors must supply bounded immutable language scalars.");
        }
        private RecordValue Sensor(TickInput input, bool position)
        {
            var result = new Dictionary<string, object>();
            string[] keys = position ? new[] { "x", "y" } : new[] { "x", "y", "heading", "ripe", "crop", "fuel", "cargo", "implement" };
            foreach (string key in keys) result[key] = SensorField(input, key);
            return new RecordValue(result);
        }
        private string Format(object value)
        {
            string text = value is RecordValue ? "<immutable record>" : value is bool boolean ? (boolean ? "True" : "False") : value is long number ? number.ToString(CultureInfo.InvariantCulture) : (string)value;
            if (Encoding.UTF8.GetByteCount(text) <= RuntimeLimits.LogBytes) return text;
            const string marker = " [truncated]"; int budget = RuntimeLimits.LogBytes - marker.Length, count = 0, bytes = 0;
            while (count < text.Length)
            {
                int chars = char.IsHighSurrogate(text[count]) && count + 1 < text.Length && char.IsLowSurrogate(text[count + 1]) ? 2 : 1;
                int next = Encoding.UTF8.GetByteCount(text.Substring(count, chars)); if (bytes + next > budget) break; bytes += next; count += chars;
            }
            return text.Substring(0, count) + marker;
        }
    }
}
