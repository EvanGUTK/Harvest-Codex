using System.Collections.Generic;
using Harvest.Application;
using Harvest.Domain;

namespace Harvest.Composition
{
    /// <summary>Versioned starting content; all subsequent changes pass through the simulation.</summary>
    public static class FirstFarm
    {
        public const string SimulationId = "harvest-spike-v1";
        public const string ContentId = "first-farm-v1";
        public const long TractorId = 1;
        public const string StartingDraft = "move_forward()\nharvest()\nturn_right()\nturn_right()\nmove_forward()\nunload()\n";

        public static StoppedSessionSnapshot Create(SessionMode mode)
        {
            var rules = SimulationRules.Default;
            var tiles = new List<TileState>();
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                {
                    bool wheat = x > 0 && y < 3;
                    tiles.Add(new TileState(new GridPosition(x, y), tilled: wheat,
                        crop: wheat ? CropKind.Wheat : CropKind.None,
                        growth: wheat ? rules.WheatMaturity : 0));
                }
            var home = new GridPosition(0, 0);
            var world = new WorldSnapshot(rules, 0, 6, 6, tiles,
                new[] { new TractorState(TractorId, home, Heading.East, ImplementKind.Harvester, 50, 50, 0, 8) },
                new[] { new DepotState(1, home, 500, 0, 500) }, 1, TractorId);
            return new StoppedSessionSnapshot(mode, SimulationId, ContentId, world, 0,
                new[] { new ScriptAssignment(TractorId, StartingDraft) }, TractorId);
        }
    }
}
