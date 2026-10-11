using Harvest.Language;
namespace Harvest.Application
{
    public static class ModePolicy
    {
        public static Capabilities For(SessionMode mode, int chapter)
        {
            if (mode == SessionMode.Sandbox) return Capabilities.All;
            var sequential = new[] { "move_forward", "turn_left", "turn_right", "harvest", "unload", "refuel", "sense", "position", "fuel", "cargo", "wait", "log" };
            if (chapter == 0) return new Capabilities(sequential);
            return new Capabilities(new[] { "move_forward", "turn_left", "turn_right", "harvest", "unload", "refuel", "sense", "position", "fuel", "cargo", "wait", "log", "assignment", "arithmetic", "for", "range" });
        }
    }
}
