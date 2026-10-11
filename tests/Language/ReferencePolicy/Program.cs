using System;
using System.Linq;
using Harvest.Application;
using Harvest.Language;

internal static class ReferencePolicyChecks
{
    public static int Main()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        try
        {
            var first = LanguageReference.Available(ModePolicy.For(SessionMode.Campaign, 0));
            var rows = LanguageReference.Available(ModePolicy.For(SessionMode.Campaign, 1));
            var sandbox = LanguageReference.Available(ModePolicy.For(SessionMode.Sandbox, 0));
            Check(first.Count == 13, "Campaign entry chapter exposes literals and all twelve implemented APIs");
            Check(rows.Count == 17, "Next teaching chapter adds assignment/arithmetic/comparisons/counted loops");
            Check(sandbox.Count == 21, "Sandbox exposes every implemented reference entry");
            Check(!first.Any(e => e.Id == "for" || e.Id == "assignment" || e.Id == "conditions"), "Locked syntax absent in first chapter");
            Check(rows.Any(e => e.Id == "for") && !rows.Any(e => e.Id == "while" || e.Id == "conditions" || e.Id == "action-results"), "Every requirement, including conditions, remains enforced after row unlock");
            Check(first.All(e => rows.Any(r => r.Id == e.Id)), "Progression retains earlier API reference");
            Check(!ReferenceEquals(first, LanguageReference.Available(ModePolicy.For(SessionMode.Campaign, 0))), "Refresh returns isolated read-only result");
            foreach (var mode in new[] { SessionMode.Campaign, SessionMode.Sandbox })
            foreach (int chapter in new[] { 0, 1 })
            {
                Capabilities policy = ModePolicy.For(mode, chapter);
                foreach (ReferenceEntry entry in LanguageReference.Available(policy))
                    Check(LanguageCompiler.Compile(entry.Example, policy).Program != null, "Example compiles with real current policy: " + mode + "/" + chapter + "/" + entry.Id);
            }
            Check(first.Count == 13 && rows.Count == 17, "Mode/chapter refresh cannot mutate prior results");
            Console.WriteLine("PASS " + checks + " actual-policy reference checks"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
