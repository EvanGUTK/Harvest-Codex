namespace Harvest.Domain
{
    public sealed class ActionRule
    {
        public int DurationTicks { get; }
        public int FuelCost { get; }
        public ActionRule(int durationTicks, int fuelCost)
        {
            Contract.Require(durationTicks > 0 && durationTicks <= 10000, "Duration must be 1..10000.");
            Contract.Resource(fuelCost, "Fuel cost"); DurationTicks = durationTicks; FuelCost = fuelCost;
        }
    }

    public sealed class SimulationRules
    {
        public string Version { get; }
        public ActionRule Move { get; }
        public ActionRule Turn { get; }
        public ActionRule Harvest { get; }
        public ActionRule Unload { get; }
        public ActionRule Refuel { get; }
        public int WheatMaturity { get; }
        public int WheatGrowthPerTick { get; }
        public int WheatYield { get; }
        public SimulationRules(string version, ActionRule move, ActionRule turn, ActionRule harvest, ActionRule unload, ActionRule refuel, int wheatMaturity, int wheatGrowthPerTick, int wheatYield)
        {
            Contract.Require(!string.IsNullOrWhiteSpace(version) && version.Length <= 64, "Rules version required (<=64 chars).");
            Contract.Require(move != null && turn != null && harvest != null && unload != null && refuel != null, "Action rules required.");
            Contract.Resource(wheatMaturity, "Maturity"); Contract.Resource(wheatGrowthPerTick, "Growth"); Contract.Resource(wheatYield, "Yield");
            Contract.Require(wheatMaturity > 0 && wheatYield > 0, "Maturity/yield must be positive.");
            Version = version; Move = move!; Turn = turn!; Harvest = harvest!; Unload = unload!; Refuel = refuel!;
            WheatMaturity = wheatMaturity; WheatGrowthPerTick = wheatGrowthPerTick; WheatYield = wheatYield;
        }
        public ActionRule For(CommandKind kind)
        {
            switch (kind)
            {
                case CommandKind.MoveForward: return Move;
                case CommandKind.TurnLeft: case CommandKind.TurnRight: return Turn;
                case CommandKind.Harvest: return Harvest;
                case CommandKind.Unload: return Unload;
                case CommandKind.Refuel: return Refuel;
                default: throw new DomainValidationException(Reasons.UnsupportedCommand, "Unsupported command.");
            }
        }
        public static SimulationRules Default => new SimulationRules("spike-v1", new ActionRule(1, 1), new ActionRule(1, 1), new ActionRule(1, 1), new ActionRule(1, 0), new ActionRule(1, 0), 10, 1, 1);
    }
}
