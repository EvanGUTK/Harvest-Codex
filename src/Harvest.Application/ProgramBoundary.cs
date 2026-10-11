using System;
using System.Collections.Generic;
using Harvest.Language;
namespace Harvest.Application
{
    // External runtime composition seam. Implemented by Language adapter once its compiler is published.
    public interface IProgramDriver
    {
        ProgramFrame Resume(TickInput input);
        void Stop();
    }
    public interface IProgramFactory
    {
        ProgramStart CompileAndStart(string source, long actorId, Capabilities capabilities);
    }
    public sealed class ProgramStart
    {
        public IProgramDriver? Driver { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public ProgramStart(IProgramDriver? driver, IEnumerable<Diagnostic> diagnostics)
        { Driver = driver; Diagnostics = new List<Diagnostic>(diagnostics).AsReadOnly(); }
    }
    public sealed class ProgramFrame
    {
        public ExecutionStatus Status { get; }
        public ActionProposal? Proposal { get; }
        public SourceSpan Span { get; }
        public Diagnostic? Diagnostic { get; }
        public IReadOnlyList<string> Logs { get; }
        public ProgramFrame(ExecutionStatus status, ActionProposal? proposal, SourceSpan span, Diagnostic? diagnostic, IEnumerable<string> logs)
        { Status = status; Proposal = proposal; Span = span; Diagnostic = diagnostic; Logs = new List<string>(logs).AsReadOnly(); }
    }
}
