using System;
using System.Collections.Generic;
using System.Linq;
using Harvest.Domain;
using Harvest.Language;
namespace Harvest.Application
{
    public sealed class SessionController
    {
        private readonly ISimulation simulation;
        private readonly IProgramFactory programs;
        private readonly string simulationId, contentId;
        private Dictionary<long, string> drafts = new Dictionary<long, string>();
        private IProgramDriver? driver;
        private TerminalResult? result;
        private double backlog;
        private readonly Queue<string> console = new Queue<string>();
        public SessionStatus Status { get; private set; } = SessionStatus.Stopped;
        public SessionMode Mode { get; private set; }
        public int CampaignChapter { get; private set; }
        public long SelectedActorId { get; private set; }
        public string ActiveSource { get; private set; } = "";
        public SourceSpan ActiveSpan { get; private set; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; private set; } = Array.Empty<Diagnostic>();
        public WorldSnapshot World => simulation.Snapshot;
        public IReadOnlyList<string> Console => Array.AsReadOnly(console.ToArray());
        public bool AchievementEligible => Mode == SessionMode.Campaign;
        public string Draft { get => drafts[SelectedActorId]; set { var valid = new ScriptAssignment(SelectedActorId, value); drafts[SelectedActorId] = valid.Draft; } }
        public SessionController(ISimulation simulation, IProgramFactory programs, string simulationId, string contentId, long selectedActorId, SessionMode mode = SessionMode.Campaign)
        {
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.programs = programs ?? throw new ArgumentNullException(nameof(programs));
            this.simulationId = simulationId; this.contentId = contentId;
            var validated = new StoppedSessionSnapshot(mode, simulationId, contentId, simulation.Snapshot, 0, new[] { new ScriptAssignment(selectedActorId, "") }, selectedActorId);
            Mode = validated.Mode; SelectedActorId = selectedActorId; drafts[selectedActorId] = "";
        }
        public bool Run()
        {
            if (Status == SessionStatus.Running) return false;
            if (Status == SessionStatus.Paused) { Status = SessionStatus.Running; backlog = 0; return true; }
            var start = programs.CompileAndStart(Draft, SelectedActorId, ModePolicy.For(Mode, CampaignChapter));
            Diagnostics = start.Diagnostics;
            if (start.Driver == null) return false;
            driver = start.Driver; ActiveSource = Draft; result = null; backlog = 0; Status = SessionStatus.Running; return true;
        }
        public void Pause() { if (Status == SessionStatus.Running) { Status = SessionStatus.Paused; backlog = 0; } }
        public void Step() { if (Status == SessionStatus.Paused) AdvanceTick(); }
        public void Pump(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            if (Status != SessionStatus.Running) { backlog = 0; return; }
            backlog = Math.Min(backlog + elapsedSeconds, .8); // 10 Hz, at most 8 ticks per frame.
            int count = 0;
            while (backlog + 1e-9 >= .1 && count++ < 8 && Status == SessionStatus.Running) { backlog -= .1; AdvanceTick(); }
        }
        public void Stop()
        {
            var stopped = simulation.StopAtBoundary();
            driver?.Stop(); driver = null; result = null; backlog = 0; Status = SessionStatus.Stopped;
            foreach (var e in stopped.Events) Log(e.Tick + " " + e.Command + " " + e.Kind + " " + e.Reason);
        }
        public StoppedSessionSnapshot CaptureStopped()
        {
            Stop(); return new StoppedSessionSnapshot(Mode, simulationId, contentId, World, CampaignChapter,
                drafts.Select(p => new ScriptAssignment(p.Key, p.Value)), SelectedActorId);
        }
        public void RestoreStopped(StoppedSessionSnapshot snapshot)
        {
            if (snapshot == null || snapshot.SimulationId != simulationId || snapshot.ContentId != contentId || snapshot.World.RulesVersion != World.RulesVersion)
                throw new ArgumentException("Incompatible stopped session", nameof(snapshot));
            // Prepare all Application state before the Domain transaction. A rejection preserves live execution.
            var replacement = snapshot.Scripts.ToDictionary(p => p.ActorId, p => p.Draft);
            simulation.RestoreStopped(snapshot.World);
            driver?.Stop(); driver = null; result = null; backlog = 0;
            drafts = replacement; SelectedActorId = snapshot.SelectedActorId; Mode = snapshot.Mode; CampaignChapter = snapshot.CampaignChapter;
            Status = SessionStatus.Stopped; ActiveSource = ""; ActiveSpan = default; Diagnostics = Array.Empty<Diagnostic>(); console.Clear();
        }
        // Mode switching requires explicit session replacement to keep campaign/sandbox worlds isolated.
        private void AdvanceTick()
        {
            if (World.Tick == long.MaxValue) { Fault("tick_overflow", "Simulation tick limit reached."); return; }
            long wheatBefore = World.Depots.Sum(d => (long)d.WheatStock);
            var commands = new List<CommandRequest>();
            if (driver != null)
            {
                var sensor = simulation.Sense(SelectedActorId);
                var scalar = new Dictionary<string, object> { ["x"] = (long)sensor.Position.X, ["y"] = (long)sensor.Position.Y,
                    ["heading"] = sensor.Heading.ToString().ToLowerInvariant(), ["ripe"] = sensor.Ripe,
                    ["crop"] = sensor.Crop.ToString().ToLowerInvariant(), ["fuel"] = (long)sensor.Fuel,
                    ["cargo"] = (long)sensor.Cargo, ["implement"] = sensor.Implement.ToString().ToLowerInvariant() };
                long next = World.Tractor(SelectedActorId).LastProcessedRequestId;
                long issued = 0; int allocations = 0;
                ProgramFrame frame;
                try { frame = driver.Resume(new TickInput(World.Tick, scalar, result, ModePolicy.For(Mode, CampaignChapter), () => { allocations++; issued = checked(++next); return issued; })); }
                catch (OverflowException) { Fault("request_overflow", "Request sequence exhausted."); return; }
                result = null; ActiveSpan = frame.Span;
                foreach (var text in frame.Logs.Take(16)) Log(text);
                if (frame.Diagnostic != null) Diagnostics = Array.AsReadOnly(new[] { frame.Diagnostic });
                if (frame.Status == ExecutionStatus.Faulted) { Stop(); return; }
                if (frame.Proposal != null)
                {
                    var p = frame.Proposal;
                    if (allocations != 1 || p.ActorId != SelectedActorId || p.RequestId <= 0 || p.RequestId != issued || !ModePolicy.For(Mode, CampaignChapter).Allows(p.ApiName) || !TryCommand(p.ApiName, out var kind))
                    { Fault("invalid_proposal", "Runtime proposed an invalid or locked command."); return; }
                    commands.Add(new CommandRequest(p.ActorId, p.RequestId, kind));
                }
                if (frame.Status == ExecutionStatus.Completed || frame.Status == ExecutionStatus.Stopped) { driver.Stop(); driver = null; }
            }
            var report = simulation.AdvanceTick(commands);
            if (report.Status != BatchStatus.Advanced) { Fault(report.Reason, "Simulation rejected the command batch."); return; }
            foreach (var terminal in report.Results)
                if (terminal.ActorId == SelectedActorId) result = new TerminalResult(terminal.ActorId, terminal.RequestId, terminal.Ok, terminal.Reason);
            foreach (var e in report.Events) Log(e.Tick + " " + e.Command + " " + e.Kind + " " + e.Reason);
            if (Mode == SessionMode.Campaign && CampaignChapter == 0 && report.Events.Any(e => e.Kind == DomainEventKind.Completed && e.Command == CommandKind.Unload && e.Reason == Reasons.None) && report.Snapshot.Depots.Sum(d => (long)d.WheatStock) > wheatBefore) CampaignChapter = 1;
        }
        private void Fault(string code, string message)
        { Diagnostics = Array.AsReadOnly(new[] { new Diagnostic(code, message, ActiveSpan) }); Stop(); }
        private void Log(string text)
        {
            // Bounded display history; runtime independently bounds player formatting.
            if (text.Length > 256) text = text.Substring(0, 256) + " [truncated]";
            console.Enqueue(text); while (console.Count > 128) console.Dequeue();
        }
        private static bool TryCommand(string name, out CommandKind kind)
        {
            switch (name) { case "move_forward": kind = CommandKind.MoveForward; return true; case "turn_left": kind = CommandKind.TurnLeft; return true;
                case "turn_right": kind = CommandKind.TurnRight; return true; case "harvest": kind = CommandKind.Harvest; return true;
                case "unload": kind = CommandKind.Unload; return true; case "refuel": kind = CommandKind.Refuel; return true; default: kind = default; return false; }
        }
    }
}
