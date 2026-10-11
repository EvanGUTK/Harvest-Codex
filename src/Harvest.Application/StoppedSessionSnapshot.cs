using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harvest.Domain;
namespace Harvest.Application
{
    public enum SessionMode { Campaign, Sandbox }
    public enum SessionStatus { Stopped, Running, Paused }
    public sealed class ScriptAssignment
    {
        public long ActorId { get; }
        public string Draft { get; }
        public ScriptAssignment(long actorId, string draft)
        {
            if (actorId <= 0 || draft == null || draft.Length > 65536 || Encoding.UTF8.GetByteCount(draft) > 65536)
                throw new ArgumentException("Invalid script assignment");
            ActorId = actorId; Draft = draft;
        }
    }
    // Immutable stopped save contract; no global audio/accessibility preferences.
    public sealed class StoppedSessionSnapshot
    {
        public SessionMode Mode { get; }
        public string SimulationId { get; }
        public string ContentId { get; }
        public WorldSnapshot World { get; }
        public int CampaignChapter { get; }
        public IReadOnlyList<ScriptAssignment> Scripts { get; }
        public long SelectedActorId { get; }
        // Spike has no sandbox modifiers. Unsupported modifiers must not be silently restored.
        public StoppedSessionSnapshot(SessionMode mode, string simulationId, string contentId, WorldSnapshot world,
            int campaignChapter, IEnumerable<ScriptAssignment> scripts, long selectedActorId)
        {
            if (!Enum.IsDefined(typeof(SessionMode), mode) || string.IsNullOrWhiteSpace(simulationId) || simulationId.Length > 64 ||
                string.IsNullOrWhiteSpace(contentId) || contentId.Length > 64 || world == null || !world.IsStopped ||
                campaignChapter < 0 || campaignChapter > 1 || (mode == SessionMode.Sandbox && campaignChapter != 0))
                throw new ArgumentException("Invalid stopped session");
            var copy = new List<ScriptAssignment>(); int bytes = 0;
            foreach (var script in scripts ?? throw new ArgumentNullException(nameof(scripts)))
            {
                if (copy.Count >= 16 || script == null || copy.Any(s => s.ActorId == script.ActorId)) throw new ArgumentException("Invalid assignments");
                world.Tractor(script.ActorId); bytes = checked(bytes + Encoding.UTF8.GetByteCount(script.Draft)); copy.Add(script);
            }
            if (bytes > 1048576 || copy.Count == 0 || copy.All(s => s.ActorId != selectedActorId)) throw new ArgumentException("Invalid editor selection/scripts");
            Mode = mode; SimulationId = simulationId; ContentId = contentId; World = world; CampaignChapter = campaignChapter;
            Scripts = copy.AsReadOnly(); SelectedActorId = selectedActorId;
        }
    }
}
