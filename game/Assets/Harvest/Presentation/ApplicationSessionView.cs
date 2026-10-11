#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Harvest.Application;
using Harvest.Domain;
namespace Harvest.Presentation
{
    public sealed class ApplicationSessionView : IHarvestSessionView
    {
        private readonly SessionController controller;
        private readonly Func<SessionMode, StoppedSessionSnapshot> createSession;
        private readonly Dictionary<SessionMode, StoppedSessionSnapshot> modes = new Dictionary<SessionMode, StoppedSessionSnapshot>();
        public ApplicationSessionView(SessionController controller, Func<SessionMode, StoppedSessionSnapshot> createSession)
        { this.controller = controller; this.createSession = createSession; }
        public string Draft { get => controller.Draft; set => controller.Draft = value; }
        public void Run() => controller.Run();
        public void Pause() => controller.Pause();
        public void Step() => controller.Step();
        public void Stop() => controller.Stop();
        public void Pump(double seconds) => controller.Pump(seconds);
        public void SelectMode(FarmMode mode)
        {
            if (controller.Status != SessionStatus.Stopped) return;
            var requested = mode == FarmMode.Campaign ? SessionMode.Campaign : SessionMode.Sandbox;
            if (requested == controller.Mode) return;
            // Prepare/validate replacement before changing current session. Each mode retains its own world/drafts.
            var destination = modes.TryGetValue(requested, out var saved) ? saved : createSession(requested);
            if (destination.Mode != requested) throw new ArgumentException("Mode factory returned wrong namespace");
            var previous = controller.CaptureStopped();
            controller.RestoreStopped(destination);
            modes[previous.Mode] = previous;
        }
        public SessionViewState Read()
        {
            var world = controller.World;
            var tractor = world.Tractor(controller.SelectedActorId);
            var pending = world.PendingActions.FirstOrDefault(p => p.Request.ActorId == tractor.ActorId);
            var details = "Fuel " + tractor.Fuel + "/" + tractor.FuelCapacity + "\nCargo " + tractor.Cargo + "/" + tractor.CargoCapacity +
                "\nImplement " + tractor.Implement + "\nAction " + (pending == null ? "Idle" : pending.Request.Kind + " until tick " + pending.CompletionTick);
            var cells = world.Tiles.Select(t => new CellView(t.Position.X, t.Position.Y,
                t.Blocked ? "Blocked" : t.Crop == CropKind.None ? "Soil" : t.Growth >= world.Rules.WheatMaturity ? "Ripe" : "Growing" )).ToArray();
            foreach (var depot in world.Depots)
            {
                int index = depot.Position.Y * world.Width + depot.Position.X;
                cells[index] = new CellView(depot.Position.X, depot.Position.Y, cells[index].Label + "\nDepot");
            }
            var diagnostics = string.Join("\n", controller.Diagnostics.Select(d => "⚠ " + d.Code + " line " + d.Span.Line + ", column " + d.Span.Column + ": " + d.Message));
            var status = controller.Status == SessionStatus.Running ? ExecutionStatus.Running : controller.Status == SessionStatus.Paused ? ExecutionStatus.Paused : ExecutionStatus.Stopped;
            string objective = controller.Mode == SessionMode.Sandbox ? "Sandbox: all implemented capabilities; standard bounded execution. No compulsory objectives." :
                controller.CampaignChapter == 0 ? "First harvest: harvest and unload wheat at a depot." : "Counted loops available. Working rows: practice traversing the field.";
            return new SessionViewState(true, status, controller.Mode == SessionMode.Campaign ? FarmMode.Campaign : FarmMode.Sandbox,
                world.Tick, world.Width, world.Height, cells, new TractorView(tractor.ActorId,tractor.Position.X,tractor.Position.Y,tractor.Heading.ToString(),details),
                diagnostics,string.Join("\n",controller.Console),objective,
                controller.ActiveSource.Length == 0 ? "None" : "line " + controller.ActiveSpan.Line + ", column " + controller.ActiveSpan.Column);
        }
    }
}
