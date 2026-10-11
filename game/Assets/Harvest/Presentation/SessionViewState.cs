#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Harvest.Presentation
{
    public enum ExecutionStatus { Stopped, Running, Paused }
    public enum FarmMode { Campaign, Sandbox }
    public sealed class CellView
    {
        public int X { get; }
        public int Y { get; }
        public string Label { get; }
        public CellView(int x, int y, string label) { X = x; Y = y; Label = label ?? ""; }
    }
    public sealed class TractorView
    {
        public long Id { get; }
        public int X { get; }
        public int Y { get; }
        public string Heading { get; }
        public string Details { get; }
        public TractorView(long id, int x, int y, string heading, string details)
        { Id = id; X = x; Y = y; Heading = heading ?? ""; Details = details ?? ""; }
    }
    public sealed class SessionViewState
    {
        public bool IsBound { get; }
        public ExecutionStatus Status { get; }
        public FarmMode Mode { get; }
        public long Tick { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<CellView> Cells { get; }
        public TractorView? Tractor { get; }
        public string Diagnostics { get; }
        public string Console { get; }
        public string Objective { get; }
        public string ActiveSource { get; }
        public bool CanRun => IsBound && Status != ExecutionStatus.Running;
        public bool CanPause => IsBound && Status == ExecutionStatus.Running;
        public bool CanStep => IsBound && Status == ExecutionStatus.Paused;
        public bool CanStop => IsBound && Status != ExecutionStatus.Stopped;
        public bool CanSelectMode => IsBound && Status == ExecutionStatus.Stopped;
        public SessionViewState(bool isBound, ExecutionStatus status, FarmMode mode, long tick,
            int width, int height, IEnumerable<CellView> cells, TractorView? tractor,
            string diagnostics, string console, string objective, string activeSource)
        {
            if (tick < 0 || width < 0 || width > 64 || height < 0 || height > 64)
                throw new ArgumentOutOfRangeException(nameof(width));
            var copy = cells.ToArray();
            if (copy.Length > 4096 || copy.Any(c => c == null || c.X < 0 || c.X >= width || c.Y < 0 || c.Y >= height))
                throw new ArgumentException("Invalid cell views", nameof(cells));
            IsBound = isBound; Status = status; Mode = mode; Tick = tick; Width = width; Height = height;
            Cells = Array.AsReadOnly(copy); Tractor = tractor;
            Diagnostics = diagnostics ?? ""; Console = console ?? ""; Objective = objective ?? ""; ActiveSource = activeSource ?? "";
        }
        public static SessionViewState Unbound => new SessionViewState(false, ExecutionStatus.Stopped,
            FarmMode.Campaign, 0, 0, 0, Array.Empty<CellView>(), null,
            "Session binding pending. Execution controls become available when Application is connected.", "", "", "");
    }
    // Presentation preferences are global; draft/actor selection belong to the session/save.
    public sealed class PresentationPreferences
    {
        public float TextScale { get; set; } = 1;
        public bool ShowGrid { get; set; } = true;
        public bool ReducedMotion { get; set; } = true;
    }
    public interface IHarvestSessionView
    {
        SessionViewState Read();
        string Draft { get; set; }
        void Run();
        void Pause();
        void Step();
        void Stop();
        void SelectMode(FarmMode mode);
    }
}
