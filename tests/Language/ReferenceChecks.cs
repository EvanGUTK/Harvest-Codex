using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harvest.Language;

internal static class ReferenceChecks
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    { checks++; try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid reference accepted"); }
    internal static int Run()
    {
        IReadOnlyList<ReferenceEntry> all = LanguageReference.Available(Capabilities.All);
        Check(all.Count == 21 && all.Count <= LanguageReference.MaxEntries, "Implemented reference catalogue size");
        Check(all.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count() == all.Count, "Stable unique entry IDs");
        Check(all.Select(e => e.Id).SequenceEqual(LanguageReference.Available(Capabilities.All).Select(e => e.Id)), "Stable teaching order");
        Check(LanguageReference.Available(new Capabilities(Array.Empty<string>())).Single().Id == "literals", "Unlocked literals require no capability");
        foreach (ReferenceEntry entry in all)
        {
            var exact = new Capabilities(entry.RequiredCapabilities);
            CompileResult sample = LanguageCompiler.Compile(entry.Example, exact);
            Check(sample.Program != null && sample.Diagnostics.Count == 0, "Example compiles under advertised requirements: " + entry.Id);
            Check(LanguageReference.Available(exact).Any(e => e.Id == entry.Id), "All requirements grant entry: " + entry.Id);
            foreach (string requirement in entry.RequiredCapabilities)
            {
                var missing = new Capabilities(entry.RequiredCapabilities.Where(c => c != requirement));
                Check(!LanguageReference.Available(missing).Any(e => e.Id == entry.Id), "Missing requirement hides entry: " + entry.Id + "/" + requirement);
                Check(LanguageCompiler.Compile(entry.Example, missing).Program == null, "Sample needs advertised requirement: " + entry.Id + "/" + requirement);
            }
            Check(Encoding.UTF8.GetByteCount(entry.Example) <= ReferenceEntry.MaxExampleBytes && ReferenceEntry.MaxExampleBytes <= RuntimeLimits.SourceBytes, "Example source bound");
        }
        var requirements = new List<string> { "log" };
        var external = new ReferenceEntry("example", "Title", "log(1)\n", "Description", requirements);
        requirements[0] = "while";
        Check(external.RequiredCapabilities.Single() == "log", "Requirement defensive copy");
        bool immutableRequirements = false;
        try { ((IList<string>)external.RequiredCapabilities)[0] = "while"; } catch (NotSupportedException) { immutableRequirements = true; }
        Check(immutableRequirements, "Published requirement list is read-only");
        bool immutableEntries = false;
        try { ((IList<ReferenceEntry>)all).Clear(); } catch (NotSupportedException) { immutableEntries = true; }
        Check(immutableEntries && LanguageReference.Available(Capabilities.All).Count == 21, "Filtered entry list cannot mutate catalogue");
        Reject(() => LanguageReference.Available(null!));
        Reject(() => new ReferenceEntry(null!, "T", "1", "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", null!, "1", "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", null!, "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", "1", null!, Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", "1", "D", null!));
        Reject(() => new ReferenceEntry("Bad ID", "T", "1", "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "  ", "1", "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", "\n", "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", "1", "  ", Array.Empty<string>()));
        Reject(() => new ReferenceEntry(new string('a', 65), "T", "1", "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", new string('a', 129), "1", "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", new string('a', 4097), "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", "1", new string('a', 2049), Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", new string('界', 43), "1", "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", new string('界', 1366), "D", Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", "1", new string('界', 683), Array.Empty<string>()));
        Reject(() => new ReferenceEntry("id", "T", "1", "D", new[] { "log", "log" }));
        Reject(() => new ReferenceEntry("id", "T", "1", "D", new[] { "imports" }));
        Reject(() => new ReferenceEntry("id", "T", "1", "D", new[] { "Log" }));
        Reject(() => new ReferenceEntry("id", "T", "1", "D", new string[] { null! }));
        Reject(() => new ReferenceEntry("id", "T", "1", "D", Forever()));
        Check(new ReferenceEntry(new string('a', 64), new string('a', 128), new string('a', 4096), new string('a', 2048), Array.Empty<string>()).Example.Length == 4096, "Exact text limits accepted");
        return checks;
    }
    private static IEnumerable<string> Forever() { while (true) yield return "log"; }
}
