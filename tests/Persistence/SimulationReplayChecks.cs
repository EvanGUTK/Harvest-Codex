using System;
using System.IO;
using System.Linq;
using Harvest.Application;
using Harvest.Domain;
using Harvest.Persistence;

internal static class SimulationReplayChecks
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var rules = new SimulationRules("save-replay-v1", new ActionRule(2, 2), new ActionRule(1, 1), new ActionRule(3, 2), new ActionRule(2, 0), new ActionRule(3, 1), 10, 1, 3);
        var initial = new WorldSnapshot(rules, 10, 3, 1,
            Enumerable.Range(0, 3).Select(x => new TileState(new GridPosition(x, 0), crop: CropKind.Wheat, growth: 9)),
            new[] { new TractorState(9, new GridPosition(0, 0), Heading.East, ImplementKind.Harvester, 10, 20, 0, 20), new TractorState(17, new GridPosition(2, 0), Heading.West, ImplementKind.Harvester, 10, 20, 0, 20) },
            new[] { new DepotState(1, new GridPosition(0, 0), 100, 0, 100) }, 987654321u, 17);
        var codec = new StoppedSessionCodec(rules, "sim-replay-v1", "content-replay-v1");
        StoppedSessionSnapshot Session(WorldSnapshot world) => new StoppedSessionSnapshot(SessionMode.Sandbox, "sim-replay-v1", "content-replay-v1", world, 0, new[] { new ScriptAssignment(9, "move_forward()"), new ScriptAssignment(17, "move_forward()") }, 17);
        var directory = Path.Combine(Path.GetTempPath(), "harvest-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = codec.CreateStore(directory, SessionMode.Sandbox);
            store.Save("farm", codec.Encode(Session(initial)));
            var restoredWorld = codec.Decode(store.Load("farm").Primary!).World;
            var baseline = new Simulation(initial); var restored = new Simulation(restoredWorld);
            check(WorldStateHash.Compute(baseline.Snapshot) == WorldStateHash.Compute(restored.Snapshot), "Disk roundtrip canonical state hash");
            var contention = new[] { new CommandRequest(9, 1, CommandKind.MoveForward), new CommandRequest(17, 1, CommandKind.MoveForward) };
            var steps = new[] {
                contention, Array.Empty<CommandRequest>(),
                new[] { new CommandRequest(9, 2, CommandKind.TurnRight), new CommandRequest(17, 2, CommandKind.Harvest) },
                Array.Empty<CommandRequest>(), Array.Empty<CommandRequest>(),
                new[] { new CommandRequest(9, 3, CommandKind.Refuel), new CommandRequest(17, 3, CommandKind.Unload) },
                Array.Empty<CommandRequest>(), Array.Empty<CommandRequest>() };
            for (int i = 0; i < steps.Length; i++)
            {
                var left = baseline.AdvanceTick(steps[i]); var right = restored.AdvanceTick(steps[i]);
                check(WorldStateHash.Compute(left.Snapshot) == WorldStateHash.Compute(right.Snapshot), "Replay state hash after tick " + i);
                check(string.Join("|", left.Submissions.Select(s => s.Request.ActorId + ":" + s.Status + ":" + s.Reason)) == string.Join("|", right.Submissions.Select(s => s.Request.ActorId + ":" + s.Status + ":" + s.Reason)), "Replay arbitration/outcomes after tick " + i);
                if (i == 0) check(right.Submissions[0].Request.ActorId == 17 && right.Submissions[0].Status == SubmissionStatus.Accepted && right.Submissions[1].Reason == Reasons.Occupied, "Restored cursor preserves contention winner");
            }
            check(restored.Snapshot.RngState == 987654321u, "Current RNG state retained without reseeding or extra draws");
            check(restored.Snapshot.Tractor(17).Cargo == 3 && restored.Snapshot.Depots[0].FuelStock < 100, "Replay exercises harvest and depot resource accounting");
            var cancellation = new Simulation(initial);
            cancellation.AdvanceTick(contention);
            check(cancellation.Snapshot.PendingActions.Count == 1 && cancellation.Snapshot.Tractor(17).Fuel == 8, "Action fuel reserved before Stop");
            reject(() => DomainWorldCodec.EncodeStopped(cancellation.Snapshot), "InvalidSnapshot");
            var activeBeforeStop = cancellation.Snapshot;
            var stopped = cancellation.StopAtBoundary().Snapshot;
            check(stopped.Tick == activeBeforeStop.Tick && stopped.NextActorId == activeBeforeStop.NextActorId, "Stop preserves tick/cursor without growth tick");
            check(stopped.Tractor(17).Fuel == 10 && stopped.PendingActions.Count == 0, "Stop refunds unused fuel once and clears reservations");
            store.Save("cancelled", codec.Encode(Session(stopped)));
            var afterLoad = new Simulation(codec.Decode(store.Load("cancelled").Primary!).World);
            var hash = WorldStateHash.Compute(afterLoad.Snapshot);
            afterLoad.StopAtBoundary(); afterLoad.StopAtBoundary();
            check(WorldStateHash.Compute(afterLoad.Snapshot) == hash && afterLoad.Snapshot.Tractor(17).Fuel == 10, "Reloaded Stop cannot double-refund cancelled action");
            var retry = afterLoad.AdvanceTick(new[] { new CommandRequest(17, afterLoad.Snapshot.Tractor(17).NextRequestId, CommandKind.MoveForward) });
            check(retry.Submissions[0].Status == SubmissionStatus.Accepted, "Stopped save has no stale target reservations");
            check(afterLoad.Snapshot.Tractor(17).LastProcessedRequestId == 2, "Reloaded sequence continues after cancelled request");
        }
        finally { Directory.Delete(directory, true); }
    }
}
