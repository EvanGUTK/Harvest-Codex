using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harvest.Language;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static readonly Dictionary<string, object> Sensor = new Dictionary<string, object> { ["x"] = 1L, ["y"] = 2L, ["heading"] = "North", ["ripe"] = true, ["crop"] = "wheat", ["fuel"] = 100L, ["cargo"] = 0L, ["implement"] = "harvest" };
    private static Execution Start(string source, int budget = 256)
    {
        CompileResult compiled = LanguageCompiler.Compile(source, Capabilities.All, new RuntimeLimits(budget));
        Check(compiled.Program != null, "Compile failed: " + string.Join(",", compiled.Diagnostics.Select(d => d.Code + " " + d.Message)));
        return compiled.Program!.Start(1);
    }
    private static TickInput Input(long tick, TerminalResult? result = null, Capabilities? caps = null, Func<long>? ids = null)
        => new TickInput(tick, Sensor, result, caps ?? Capabilities.All, ids ?? (() => tick + 100));
    private static List<string> Run(string source, int budget = 256)
    {
        Execution execution = Start(source, budget); var logs = new List<string>();
        for (long tick = 0; tick < 10000; tick++)
        {
            RunSlice slice = execution.Resume(Input(tick)); logs.AddRange(slice.Logs);
            Check(slice.InstructionsUsed <= budget, "Budget exceeded");
            Check(slice.Status != ExecutionStatus.Faulted, "Runtime fault: " + slice.Diagnostic?.Code);
            if (slice.Status == ExecutionStatus.Completed) return logs;
        }
        throw new Exception("Expected program completion");
    }
    private static void CompileError(string source, string code)
    { var result = LanguageCompiler.Compile(source, Capabilities.All); Check(result.Program == null && result.Diagnostics.Count == 1 && result.Diagnostics[0].Code == code, "Expected compile " + code); }
    private static void Fault(string source, string code)
    { RunSlice slice = Start(source).Resume(Input(0)); Check(slice.Status == ExecutionStatus.Faulted && slice.Diagnostic?.Code == code, "Expected runtime " + code + ", got " + slice.Diagnostic?.Code); }
    public static int Main()
    {
        try
        {
            Check(Run("log(2 + 3 * 4)\nlog(-7 // 3)\nlog(-7 % 3)\nlog(7 // -3)\nlog(7 % -3)\n").SequenceEqual(new[] { "14", "-3", "2", "-3", "-2" }), "Arithmetic precedence/floor semantics");
            Check(Run("log(-9223372036854775808)\nlog(-9223372036854775808 % -1)\n").SequenceEqual(new[] { long.MinValue.ToString(), "0" }), "Minimum int64");
            Check(Run("log(not 1 == 2)\nlog(False and missing)\nlog(True or missing)\n").SequenceEqual(new[] { "True", "False", "True" }), "Short circuit and not precedence");
            string loops = "total = 0\nfor row in range(3):\n    for col in range(2):\n        total = total + row + col\nif total == 9:\n    log(\"yes\")\nelse:\n    log(\"no\")\nwhile total < 12:\n    total = total + 1\nlog(total)\n";
            Check(Run(loops).SequenceEqual(new[] { "yes", "12" }), "Nested loops and if/else");
            Check(Run(loops, 1).SequenceEqual(Run(loops, 256)), "Determinism across instruction slices");
            Check(Run("for i in range(0):\n    log(\"wrong\")\nlog(\"done\")\n").SequenceEqual(new[] { "done" }), "Empty lazy range");
            Check(Run("# comment\r\nx = \"a\\n\" # trailing\r\n\r\nlog(x)\r\n").Single() == "a\n", "CRLF/comments/escapes");
            Check(Run("log(position().x)\nlog(sense().ripe)\nlog(fuel())\nlog(cargo())\n").SequenceEqual(new[] { "1", "True", "100", "0" }), "Tick snapshots");
            CompileError("import os\n", "unsupported_syntax"); CompileError("def f():\n    log(1)\n", "unsupported_syntax"); CompileError("x = [1]\n", "unsupported_syntax");
            CompileError("log(1 < 2 < 3)\n", "unsupported_syntax"); CompileError("log(sense().__class__)\n", "unsupported_member"); CompileError("foo()\n", "unsupported_api");
            CompileError("if True:\n\tlog(1)\n", "invalid_indent"); CompileError("if True:\n  log(1)\n", "invalid_indent");
            CompileError("log(\"unfinished)\n", "unterminated_string"); CompileError("log(\"\\q\")\n", "invalid_escape"); CompileError("log(1, 2)\n", "argument_count");
            CompileError("log(9223372036854775808)\n", "numeric_overflow");
            CompileError("log(" + new string('(', 70) + "1" + new string(')', 70) + ")\n", "nesting_limit");
            CompileError(new string('#', 65537), "source_limit"); CompileError("#" + new string('界', 22000), "source_limit");
            CompileError("log(\"" + new string('界', 1400) + "\")\n", "string_limit");
            CompileError(string.Concat(Enumerable.Repeat("log(1)\n", 2000)), "token_limit");
            CompileError(string.Concat(Enumerable.Repeat("x = 1\n", 2100)), "token_limit");
            CompileError(string.Concat(Enumerable.Repeat("x = 1\n", 2050)), "token_limit");
            Fault("if 1:\n    log(1)\n", "type_error"); Fault("log(True + 1)\n", "type_error"); Fault("log(1 // 0)\n", "divide_by_zero");
            Fault("log(9223372036854775807 + 1)\n", "numeric_overflow"); Fault("log(-(-9223372036854775808))\n", "numeric_overflow");
            Fault("log(-9223372036854775808 // -1)\n", "numeric_overflow"); Fault("log(missing)\n", "unknown_name"); Fault("log(1.ok)\n", "type_error");
            Fault("for n in range(1000001):\n    log(n)\n", "range_limit"); Fault("wait(0)\n", "wait_limit");
            string longString = "\"" + new string('x', 3000) + "\"";
            Fault("x = " + longString + "\nlog(x + x)\n", "string_limit");
            Check(Encoding.UTF8.GetByteCount(Run("log(" + longString + ")\n").Single()) <= 256, "Bounded/truncated log");
            Check(Run("log(" + longString + ")\n").Single().EndsWith("[truncated]"), "Visible truncation marker");
            RunSlice logSlice = Start(string.Concat(Enumerable.Repeat("log(1)\n", 30))).Resume(Input(0));
            Check(logSlice.Logs.Count == 16 && logSlice.Logs.Last().Contains("truncated"), "Per-tick log cap");
            var forever = Start("while True:\n    x = 1\n");
            for (long tick = 0; tick < 1000; tick++) { RunSlice slice = forever.Resume(Input(tick)); Check(slice.Status == ExecutionStatus.Ready && slice.InstructionsUsed == 256, "Infinite loop must yield indefinitely"); }
            forever.Stop(); Check(forever.Resume(Input(1000)).Status == ExecutionStatus.Stopped, "Stop responsiveness");
            var waiting = Start("wait(3)\nlog(\"awake\")\n");
            Check(waiting.Resume(Input(10)).Status == ExecutionStatus.WaitingTick, "Wait starts"); Check(waiting.Resume(Input(11)).InstructionsUsed == 0, "Wait yields"); Check(waiting.Resume(Input(12)).Status == ExecutionStatus.WaitingTick, "Wait remains pending"); Check(waiting.Resume(Input(13)).Logs.Single() == "awake", "Wait wakes exactly");
            Check(Start("wait(1)\n").Resume(Input(long.MaxValue)).Diagnostic?.Code == "numeric_overflow", "Wake tick overflow diagnostic");
            var action = Start("r = harvest()\nif not r.ok:\n    log(r.reason)\nmove_forward()\n"); int calls = 0;
            RunSlice first = action.Resume(Input(0, ids: () => { calls++; return 900; }));
            Check(first.Status == ExecutionStatus.WaitingAction && first.Proposal?.RequestId == 900 && calls == 1, "Application ID used");
            RunSlice pending = action.Resume(Input(1, ids: () => { calls++; return 901; })); Check(pending.Proposal == null && pending.InstructionsUsed == 0 && calls == 1, "No duplicate pending submission");
            RunSlice rejected = action.Resume(Input(2, new TerminalResult(1, 900, false, "immature"), ids: () => { calls++; return 901; }));
            Check(rejected.Logs.Single() == "immature" && rejected.Proposal?.RequestId == 901 && calls == 2, "Rejection is script value and next action yields");
            Check(action.Resume(Input(3, new TerminalResult(1, 901, true, "none"))).Status == ExecutionStatus.Completed, "Completion resumes");
            var mismatch = Start("harvest()\n"); mismatch.Resume(Input(0)); Check(mismatch.Resume(Input(1, new TerminalResult(2, 100, true, "none"))).Diagnostic?.Code == "gateway_failure", "Wrong actor result rejected");
            var duplicate = Start("harvest()\nmove_forward()\n"); duplicate.Resume(Input(0, ids: () => 42)); Check(duplicate.Resume(Input(1, new TerminalResult(1, 42, true, "none"), ids: () => 42)).Diagnostic?.Code == "gateway_failure", "Duplicate ID faults");
            var locked = LanguageCompiler.Compile("harvest()\n", new Capabilities(Array.Empty<string>())); Check(locked.Diagnostics.Single().Code == "capability_locked", "Compile gate");
            Check(Start("harvest()\n").Resume(Input(0, caps: new Capabilities(Array.Empty<string>()))).Diagnostic?.Code == "capability_locked", "Runtime API gate");
            Check(Start("while True:\n    log(1)\n").Resume(Input(0, caps: new Capabilities(new[] { "log" }))).Diagnostic?.Code == "capability_locked", "Runtime while gate");
            var span = LanguageCompiler.Compile("# 😀\r\nlog(\"x\")\r\nfoo()\n", Capabilities.All).Diagnostics.Single().Span;
            Check(span.Line == 3 && span.Column == 1 && span.Offset == 16, "UTF-16 offsets and CRLF line numbers: " + span.Offset);
            checks += ContractChecks.Run();
            Console.WriteLine("PASS " + checks + " checks"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
