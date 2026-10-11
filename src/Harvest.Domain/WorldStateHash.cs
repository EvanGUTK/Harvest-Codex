using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Harvest.Domain
{
    /// <summary>Versioned canonical replay hash, independent of host dictionary/string hash behavior.</summary>
    public static class WorldStateHash
    {
        public static string Compute(WorldSnapshot snapshot)
        {
            Contract.Require(snapshot != null, "Snapshot required.");
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write("harvest-state-v1");
                    var rules = snapshot!.Rules;
                    writer.Write(rules.Version);
                    foreach (var rule in new[] { rules.Move, rules.Turn, rules.Harvest, rules.Unload, rules.Refuel })
                    { writer.Write(rule.DurationTicks); writer.Write(rule.FuelCost); }
                    writer.Write(rules.WheatMaturity); writer.Write(rules.WheatGrowthPerTick); writer.Write(rules.WheatYield);
                    writer.Write(snapshot.Tick); writer.Write(snapshot.Width); writer.Write(snapshot.Height);
                    writer.Write(snapshot.RngAlgorithm); writer.Write(snapshot.RngState); writer.Write(snapshot.NextActorId);
                    writer.Write(snapshot.Tiles.Count);
                    foreach (var tile in snapshot.Tiles)
                    {
                        Position(writer, tile.Position); writer.Write(tile.Blocked); writer.Write(tile.Tilled); writer.Write(tile.Moisture); writer.Write((int)tile.Crop); writer.Write(tile.Growth);
                    }
                    writer.Write(snapshot.Tractors.Count);
                    foreach (var actor in snapshot.Tractors)
                    {
                        writer.Write(actor.ActorId); Position(writer, actor.Position); writer.Write((int)actor.Heading); writer.Write((int)actor.Implement);
                        writer.Write(actor.Fuel); writer.Write(actor.FuelCapacity); writer.Write(actor.Cargo); writer.Write(actor.CargoCapacity); writer.Write(actor.LastProcessedRequestId);
                    }
                    writer.Write(snapshot.Depots.Count);
                    foreach (var depot in snapshot.Depots)
                    { writer.Write(depot.DepotId); Position(writer, depot.Position); writer.Write(depot.FuelStock); writer.Write(depot.WheatStock); writer.Write(depot.WheatCapacity); }
                    writer.Write(snapshot.PendingActions.Count);
                    foreach (var action in snapshot.PendingActions)
                    {
                        writer.Write(action.Request.ActorId); writer.Write(action.Request.RequestId); writer.Write((int)action.Request.Kind); Position(writer, action.Target);
                        writer.Write(action.StartTick); writer.Write(action.CompletionTick); writer.Write(action.ReservedFuel); writer.Write(action.ReservedQuantity); writer.Write(action.DepotId);
                    }
                }
                using (var hash = SHA256.Create())
                {
                    byte[] digest = hash.ComputeHash(stream.ToArray());
                    var text = new StringBuilder(64);
                    foreach (byte value in digest) text.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                    return text.ToString();
                }
            }
        }
        private static void Position(BinaryWriter writer, GridPosition position) { writer.Write(position.X); writer.Write(position.Y); }
    }
}
