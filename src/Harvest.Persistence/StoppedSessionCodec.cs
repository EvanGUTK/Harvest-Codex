using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Harvest.Application;
using Harvest.Domain;

namespace Harvest.Persistence
{
    public sealed class StoppedSessionCodec
    {
        private readonly SimulationRules rules;
        private readonly string simulationId;
        private readonly string contentId;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public StoppedSessionCodec(SimulationRules rules, string simulationId, string contentId)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (string.IsNullOrWhiteSpace(simulationId) || simulationId.Length > 64 || string.IsNullOrWhiteSpace(contentId) || contentId.Length > 64)
                throw new ArgumentException("Compatibility IDs must be nonempty and bounded.");
            this.simulationId = simulationId; this.contentId = contentId;
        }
        public SaveEnvelope Encode(StoppedSessionSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            Compatibility(snapshot.SimulationId, snapshot.ContentId);
            PayloadLimits.ValidateScripts(snapshot.Scripts.Select(s => s.Draft));
            var worldJson = DomainWorldCodec.EncodeStopped(snapshot.World);
            // Compare complete authoritative rule configuration before persisting.
            DomainWorldCodec.DecodeStopped(worldJson, rules);
            using var world = JsonDocument.Parse(worldJson);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output))
            {
                writer.WriteStartObject(); writer.WriteNumber("sessionSchemaVersion", 1); writer.WriteString("executionState", "stopped");
                writer.WriteString("mode", snapshot.Mode.ToString()); writer.WriteNumber("campaignChapter", snapshot.CampaignChapter);
                writer.WriteStartObject("editor"); writer.WriteNumber("selectedActorId", snapshot.SelectedActorId); writer.WriteEndObject();
                writer.WritePropertyName("world"); world.RootElement.WriteTo(writer);
                writer.WriteStartArray("scripts");
                foreach (var script in snapshot.Scripts)
                { writer.WriteStartObject(); writer.WriteNumber("actorId", script.ActorId); writer.WriteString("draft", script.Draft); writer.WriteEndObject(); }
                writer.WriteEndArray(); writer.WriteEndObject();
            }
            return SaveEnvelope.Create(simulationId, contentId, Utf8.GetString(output.ToArray()));
        }
        public StoppedSessionSnapshot Decode(SaveEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            Compatibility(envelope.SimulationVersion, envelope.ContentVersion);
            try
            {
                using var document = SaveEnvelope.ValidateJson(Utf8.GetBytes(envelope.Payload), true);
                var root = document.RootElement;
                StrictJson.Keys(root, "sessionSchemaVersion", "executionState", "mode", "campaignChapter", "editor", "world", "scripts");
                if (root.GetProperty("sessionSchemaVersion").GetInt32() != 1) throw new SaveException("UnsupportedSchema", "Unsupported stopped-session schema.");
                if (StrictJson.Text(root, "executionState", 16) != "stopped") throw new SaveException("InvalidSnapshot", "Session must be stopped.");
                var mode = StrictJson.EnumValue<SessionMode>(root, "mode");
                var editor = root.GetProperty("editor"); StrictJson.Keys(editor, "selectedActorId");
                PayloadLimits.ValidateSaveLocalEditorJson(editor.GetRawText());
                var world = DomainWorldCodec.DecodeStopped(root.GetProperty("world").GetRawText(), rules);
                var scripts = new List<ScriptAssignment>();
                foreach (var script in StrictJson.Array(root, "scripts", 16).EnumerateArray())
                {
                    StrictJson.Keys(script, "actorId", "draft");
                    var draft = StrictJson.Text(script, "draft", PayloadLimits.ScriptBytes);
                    PayloadLimits.ValidateScripts(new[] { draft });
                    scripts.Add(new ScriptAssignment(script.GetProperty("actorId").GetInt64(), draft));
                }
                PayloadLimits.ValidateScripts(scripts.Select(s => s.Draft));
                return new StoppedSessionSnapshot(mode, simulationId, contentId, world, root.GetProperty("campaignChapter").GetInt32(), scripts, editor.GetProperty("selectedActorId").GetInt64());
            }
            catch (SaveException) { throw; }
            catch (ArgumentException ex) { throw new SaveException("InvalidSnapshot", "Invalid stopped session: " + ex.Message, ex); }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is FormatException || ex is OverflowException || ex is KeyNotFoundException)
            { throw new SaveException("Corrupt", "Malformed stopped-session payload.", ex); }
        }
        public AtomicSlotStore CreateStore(string existingModeDirectory, SessionMode expectedMode, ISlotFiles? files = null)
        {
            if (!Enum.IsDefined(typeof(SessionMode), expectedMode)) throw new ArgumentException("Unknown mode.");
            return new AtomicSlotStore(existingModeDirectory, envelope =>
            {
                if (Decode(envelope).Mode != expectedMode) throw new SaveException("InvalidSnapshot", "Save belongs to another mode namespace.");
            }, files);
        }
        private void Compatibility(string simulation, string content)
        {
            if (simulation != simulationId || content != contentId) throw new SaveException("IncompatibleSimulation", "Unsupported simulation/content configuration.");
        }
    }
}
