using System;
using System.Collections.Generic;
using Harvest.Presentation;
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
var unbound = SessionViewState.Unbound;
Check(!unbound.CanRun && !unbound.CanPause && !unbound.CanStep && !unbound.CanStop && !unbound.CanSelectMode, "Unbound controls must be disabled");
foreach (var status in Enum.GetValues<ExecutionStatus>())
{
    var cells = new List<CellView> { new CellView(0, 0, "Ripe") };
    var state = new SessionViewState(true, status, FarmMode.Sandbox, 42, 1, 1, cells, null, "line 2 column 3", "", "", "");
    cells.Clear();
    Check(state.Cells.Count == 1, "Snapshot must isolate caller mutations");
    Check(state.CanRun == (status != ExecutionStatus.Running), "Run state");
    Check(state.CanPause == (status == ExecutionStatus.Running), "Pause state");
    Check(state.CanStep == (status == ExecutionStatus.Paused), "Step state");
    Check(state.CanStop == (status != ExecutionStatus.Stopped), "Stop state");
    Check(state.CanSelectMode == (status == ExecutionStatus.Stopped), "Mode switch state");
}
try { _ = new SessionViewState(true, ExecutionStatus.Stopped, FarmMode.Campaign, 0, 1, 1, new[] { new CellView(1, 0, "") }, null, "", "", "", ""); throw new Exception("Invalid coordinate accepted"); }
catch (ArgumentException) { }
Console.WriteLine("Presentation state checks passed");

LayoutChecks.Run();
