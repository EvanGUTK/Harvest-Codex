using Harvest.Language;
namespace Harvest.Application
{
    public sealed class LanguageProgramFactory : IProgramFactory
    {
        public ProgramStart CompileAndStart(string source, long actorId, Capabilities capabilities)
        {
            var compiled = LanguageCompiler.Compile(source, capabilities);
            return new ProgramStart(compiled.Program == null ? null : new Driver(compiled.Program.Start(actorId)), compiled.Diagnostics);
        }
        private sealed class Driver : IProgramDriver
        {
            private readonly Execution execution;
            public Driver(Execution execution) { this.execution = execution; }
            public ProgramFrame Resume(TickInput input)
            {
                var slice = execution.Resume(input);
                return new ProgramFrame(slice.Status, slice.Proposal, slice.ActiveSpan, slice.Diagnostic, slice.Logs);
            }
            public void Stop() => execution.Stop();
        }
    }
}
