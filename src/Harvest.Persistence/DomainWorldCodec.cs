using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Harvest.Domain;

namespace Harvest.Persistence
{
    // v1 explicit disk schema. Constructor validation remains authoritative in Domain.
    public static class DomainWorldCodec
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static string EncodeStopped(WorldSnapshot world)
        {
            if (world == null || !world.IsStopped) throw new SaveException("InvalidSnapshot", "World must be stopped.");
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject();
                writer.WriteNumber("worldSchemaVersion", 1); writer.WriteString("executionState", "stopped");
                writer.WritePropertyName("rules"); WriteRules(writer, world.Rules);
                writer.WriteNumber("tick", world.Tick); writer.WriteNumber("width", world.Width); writer.WriteNumber("height", world.Height);
                writer.WriteString("rngAlgorithm", world.RngAlgorithm); writer.WriteNumber("rngState", world.RngState); writer.WriteNumber("nextActorId", world.NextActorId);
                writer.WriteStartArray("tiles");
                foreach (var tile in world.Tiles)
                {
                    writer.WriteStartObject(); Position(writer, tile.Position);
                    writer.WriteBoolean("blocked", tile.Blocked); writer.WriteBoolean("tilled", tile.Tilled);
                    writer.WriteNumber("moisture", tile.Moisture); writer.WriteString("crop", tile.Crop.ToString()); writer.WriteNumber("growth", tile.Growth);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteStartArray("tractors");
                foreach (var tractor in world.Tractors)
                {
                    writer.WriteStartObject(); writer.WriteNumber("actorId", tractor.ActorId); Position(writer, tractor.Position);
                    writer.WriteString("heading", tractor.Heading.ToString()); writer.WriteString("implement", tractor.Implement.ToString());
                    writer.WriteNumber("fuel", tractor.Fuel); writer.WriteNumber("fuelCapacity", tractor.FuelCapacity);
                    writer.WriteNumber("cargo", tractor.Cargo); writer.WriteNumber("cargoCapacity", tractor.CargoCapacity);
                    writer.WriteNumber("lastProcessedRequestId", tractor.LastProcessedRequestId); writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteStartArray("depots");
                foreach (var depot in world.Depots)
                {
                    writer.WriteStartObject(); writer.WriteNumber("depotId", depot.DepotId); Position(writer, depot.Position);
                    writer.WriteNumber("fuelStock", depot.FuelStock); writer.WriteNumber("wheatStock", depot.WheatStock); writer.WriteNumber("wheatCapacity", depot.WheatCapacity); writer.WriteEndObject();
                }
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            if (output.Length > SaveEnvelope.MaxFileBytes) throw new SaveException("LimitExceeded", "World exceeds save bound.");
            return Utf8.GetString(output.ToArray());
        }
        public static WorldSnapshot DecodeStopped(string json, SimulationRules expectedRules)
        {
            if (expectedRules == null) throw new ArgumentNullException(nameof(expectedRules));
            try
            {
                if (json == null || Utf8.GetByteCount(json) > SaveEnvelope.MaxFileBytes) throw new SaveException("LimitExceeded", "World exceeds save bound.");
                using var document = SaveEnvelope.ValidateJson(Utf8.GetBytes(json), true);
                var root = document.RootElement;
                StrictJson.Keys(root, "worldSchemaVersion", "executionState", "rules", "tick", "width", "height", "rngAlgorithm", "rngState", "nextActorId", "tiles", "tractors", "depots");
                if (root.GetProperty("worldSchemaVersion").GetInt32() != 1) throw new SaveException("UnsupportedSchema", "Unsupported world schema.");
                if (StrictJson.Text(root, "executionState", 16) != "stopped") throw new SaveException("InvalidSnapshot", "World is not stopped.");
                ReadAndMatchRules(root.GetProperty("rules"), expectedRules);
                var tiles = new List<TileState>();
                foreach (var tile in StrictJson.Array(root, "tiles", 4096).EnumerateArray())
                {
                    StrictJson.Keys(tile, "x", "y", "blocked", "tilled", "moisture", "crop", "growth");
                    tiles.Add(new TileState(Position(tile), tile.GetProperty("blocked").GetBoolean(), tile.GetProperty("tilled").GetBoolean(), tile.GetProperty("moisture").GetInt32(), StrictJson.EnumValue<CropKind>(tile, "crop"), tile.GetProperty("growth").GetInt32()));
                }
                var tractors = new List<TractorState>();
                foreach (var tractor in StrictJson.Array(root, "tractors", 16).EnumerateArray())
                {
                    StrictJson.Keys(tractor, "actorId", "x", "y", "heading", "implement", "fuel", "fuelCapacity", "cargo", "cargoCapacity", "lastProcessedRequestId");
                    tractors.Add(new TractorState(tractor.GetProperty("actorId").GetInt64(), Position(tractor), StrictJson.EnumValue<Heading>(tractor, "heading"), StrictJson.EnumValue<ImplementKind>(tractor, "implement"), tractor.GetProperty("fuel").GetInt32(), tractor.GetProperty("fuelCapacity").GetInt32(), tractor.GetProperty("cargo").GetInt32(), tractor.GetProperty("cargoCapacity").GetInt32(), tractor.GetProperty("lastProcessedRequestId").GetInt64()));
                }
                var depots = new List<DepotState>();
                foreach (var depot in StrictJson.Array(root, "depots", 4096).EnumerateArray())
                {
                    StrictJson.Keys(depot, "depotId", "x", "y", "fuelStock", "wheatStock", "wheatCapacity");
                    depots.Add(new DepotState(depot.GetProperty("depotId").GetInt64(), Position(depot), depot.GetProperty("fuelStock").GetInt32(), depot.GetProperty("wheatStock").GetInt32(), depot.GetProperty("wheatCapacity").GetInt32()));
                }
                return new WorldSnapshot(expectedRules, root.GetProperty("tick").GetInt64(), root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32(), tiles, tractors, depots, root.GetProperty("rngState").GetUInt32(), root.GetProperty("nextActorId").GetInt64(), rngAlgorithm: StrictJson.Text(root, "rngAlgorithm", 64));
            }
            catch (SaveException) { throw; }
            catch (DomainValidationException ex) { throw new SaveException("InvalidSnapshot", ex.Message, ex); }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException || ex is InvalidOperationException || ex is FormatException || ex is OverflowException || ex is KeyNotFoundException)
            { throw new SaveException("Corrupt", "Malformed world JSON.", ex); }
        }
        private static GridPosition Position(JsonElement value) => new GridPosition(value.GetProperty("x").GetInt32(), value.GetProperty("y").GetInt32());
        private static void Position(Utf8JsonWriter writer, GridPosition value) { writer.WriteNumber("x", value.X); writer.WriteNumber("y", value.Y); }
        private static void WriteRules(Utf8JsonWriter writer, SimulationRules rules)
        {
            writer.WriteStartObject(); writer.WriteString("version", rules.Version);
            WriteRule(writer, "move", rules.Move); WriteRule(writer, "turn", rules.Turn); WriteRule(writer, "harvest", rules.Harvest); WriteRule(writer, "unload", rules.Unload); WriteRule(writer, "refuel", rules.Refuel);
            writer.WriteNumber("wheatMaturity", rules.WheatMaturity); writer.WriteNumber("wheatGrowthPerTick", rules.WheatGrowthPerTick); writer.WriteNumber("wheatYield", rules.WheatYield); writer.WriteEndObject();
        }
        private static void WriteRule(Utf8JsonWriter writer, string key, ActionRule rule)
        { writer.WriteStartObject(key); writer.WriteNumber("durationTicks", rule.DurationTicks); writer.WriteNumber("fuelCost", rule.FuelCost); writer.WriteEndObject(); }
        private static void ReadAndMatchRules(JsonElement value, SimulationRules rules)
        {
            StrictJson.Keys(value, "version", "move", "turn", "harvest", "unload", "refuel", "wheatMaturity", "wheatGrowthPerTick", "wheatYield");
            if (StrictJson.Text(value, "version", 64) != rules.Version || value.GetProperty("wheatMaturity").GetInt32() != rules.WheatMaturity || value.GetProperty("wheatGrowthPerTick").GetInt32() != rules.WheatGrowthPerTick || value.GetProperty("wheatYield").GetInt32() != rules.WheatYield)
                throw new SaveException("IncompatibleSimulation", "Rules/content differ from current validated configuration.");
            MatchRule(value, "move", rules.Move); MatchRule(value, "turn", rules.Turn); MatchRule(value, "harvest", rules.Harvest); MatchRule(value, "unload", rules.Unload); MatchRule(value, "refuel", rules.Refuel);
        }
        private static void MatchRule(JsonElement parent, string key, ActionRule rule)
        {
            var value = parent.GetProperty(key); StrictJson.Keys(value, "durationTicks", "fuelCost");
            if (value.GetProperty("durationTicks").GetInt32() != rule.DurationTicks || value.GetProperty("fuelCost").GetInt32() != rule.FuelCost)
                throw new SaveException("IncompatibleSimulation", "Action rules differ: " + key);
        }
    }
}
