using System;
using System.Collections.Generic;
using Harvest.Language;

internal static class ContractChecks
{
    private static int checks;
    private static void Reject(Action action)
    { checks++; try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid DTO accepted"); }
    public static int Main() { Console.WriteLine("PASS " + Run() + " constructor checks"); return 0; }
    internal static int Run()
    {
        Reject(() => new ActionProposal(0, 1, "harvest")); Reject(() => new ActionProposal(1, 0, "harvest"));
        Reject(() => new ActionProposal(1, 1, "eval")); Reject(() => new ActionProposal(1, 1, null!));
        Reject(() => new TerminalResult(-1, 1, true, "none")); Reject(() => new TerminalResult(1, -1, true, "none"));
        Reject(() => new TerminalResult(1, 1, false, null!)); Reject(() => new TerminalResult(1, 1, false, "none"));
        Reject(() => new TerminalResult(1, 1, true, "blocked")); Reject(() => new TerminalResult(1, 1, false, new string('a', 65)));
        Reject(() => new TerminalResult(1, 1, false, "Bad Code"));
        var sensor = new Dictionary<string, object> { ["fuel"] = 1L };
        TickInput input = new TickInput(0, sensor, null, Capabilities.All, () => 1);
        sensor["fuel"] = 9L; checks++; if ((long)input.Sensor["fuel"] != 1L) throw new Exception("Snapshot mutation visible");
        Reject(() => new TickInput(0, new Dictionary<string, object> { ["fuel"] = new List<int> { 1 } }, null, Capabilities.All, () => 1));
        Reject(() => new TickInput(0, new Dictionary<string, object> { ["extra"] = 1L }, null, Capabilities.All, () => 1));
        Reject(() => new TickInput(0, new Dictionary<string, object> { ["fuel"] = 1 }, null, Capabilities.All, () => 1));
        Reject(() => new TickInput(0, new Dictionary<string, object> { ["ripe"] = 1L }, null, Capabilities.All, () => 1));
        Reject(() => new TickInput(0, new Dictionary<string, object> { ["crop"] = new string('界', 1400) }, null, Capabilities.All, () => 1));
        Reject(() => new TickInput(0, sensor, null, null!, () => 1)); Reject(() => new TickInput(0, sensor, null, Capabilities.All, null!));
        Reject(() => new TickInput(0, null!, null, Capabilities.All, () => 1));
        Reject(() => new SourceSpan(-1, 1, 1, 1)); Reject(() => new SourceSpan(0, 1, 0, 1));
        Reject(() => new Capabilities(RepeatedNames()));
        return checks;
    }
    private static IEnumerable<string> RepeatedNames() { while (true) yield return "harvest"; }
}
