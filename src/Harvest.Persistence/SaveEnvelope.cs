using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Harvest.Persistence
{
    public sealed class SaveException : Exception
    {
        public string Code { get; }
        public SaveException(string code, string message, Exception? inner = null) : base(message, inner) { Code = code; }
    }

    // Disk layer intentionally treats stopped session payloads as inert JSON.
    // The published Application/Domain adapter must validate their complete schema.
    public sealed class SaveEnvelope
    {
        public const int MaxFileBytes = 16 * 1024 * 1024;
        public const int MaxDepth = 64;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public string SimulationVersion { get; }
        public string ContentVersion { get; }
        public string Payload { get; }
        private SaveEnvelope(string simulationVersion, string contentVersion, string payload)
        { SimulationVersion = simulationVersion; ContentVersion = contentVersion; Payload = payload; }

        public static SaveEnvelope Create(string simulationVersion, string contentVersion, string payload)
        {
            ValidateId(simulationVersion); ValidateId(contentVersion);
            try
            {
                if (payload == null || Utf8.GetByteCount(payload) > MaxFileBytes) throw new SaveException("LimitExceeded", "Payload exceeds file bound.");
                using var document = ValidateJson(Utf8.GetBytes(payload), true);
            }
            catch (SaveException) { throw; }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException)
            { throw new SaveException("Corrupt", "Malformed payload JSON.", ex); }
            return new SaveEnvelope(simulationVersion, contentVersion, payload);
        }
        public byte[] Encode()
        {
            var bytes = Utf8.GetBytes(Payload);
            var result = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object> {
                ["format"] = "harvest-codex-save", ["schemaVersion"] = 1,
                ["simulationVersion"] = SimulationVersion, ["contentVersion"] = ContentVersion,
                ["payloadByteLength"] = bytes.Length, ["payloadSha256"] = Hash(bytes), ["payload"] = Payload });
            if (result.Length > MaxFileBytes) throw new SaveException("LimitExceeded", "Save exceeds 16 MiB.");
            return result;
        }
        public static SaveEnvelope Decode(byte[] bytes)
        {
            try
            {
                using var document = ValidateJson(bytes, true);
                var root = document.RootElement;
                var expected = new HashSet<string>(new[] { "format", "schemaVersion", "simulationVersion", "contentVersion", "payloadByteLength", "payloadSha256", "payload" }, StringComparer.Ordinal);
                foreach (var property in root.EnumerateObject())
                    if (!expected.Remove(property.Name)) throw new SaveException("Corrupt", "Unknown envelope key: " + property.Name);
                if (expected.Count != 0) throw new SaveException("Corrupt", "Missing envelope keys.");
                if (root.GetProperty("format").GetString() != "harvest-codex-save") throw new SaveException("Corrupt", "Invalid format.");
                if (root.GetProperty("schemaVersion").GetInt32() != 1) throw new SaveException("UnsupportedSchema", "Unsupported save schema.");
                var payload = root.GetProperty("payload").GetString()!;
                var payloadBytes = Utf8.GetBytes(payload);
                if (root.GetProperty("payloadByteLength").GetInt32() != payloadBytes.Length || root.GetProperty("payloadSha256").GetString() != Hash(payloadBytes))
                    throw new SaveException("Corrupt", "Payload length/checksum mismatch.");
                return Create(root.GetProperty("simulationVersion").GetString()!, root.GetProperty("contentVersion").GetString()!, payload);
            }
            catch (SaveException) { throw; }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is FormatException || ex is ArgumentException || ex is OverflowException)
            { throw new SaveException("Corrupt", "Malformed save envelope.", ex); }
        }
        private static void ValidateId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) throw new SaveException("Corrupt", "Invalid compatibility identifier.");
        }
        private static string Hash(byte[] bytes)
        { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        internal static JsonDocument ValidateJson(byte[] bytes, bool objectRoot)
        {
            if (bytes.Length > MaxFileBytes) throw new SaveException("LimitExceeded", "JSON exceeds file bound.");
            Utf8.GetCharCount(bytes); // Reject invalid UTF-8 before parsing.
            var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = MaxDepth });
            try
            {
                if (objectRoot && document.RootElement.ValueKind != JsonValueKind.Object) throw new SaveException("Corrupt", "Expected JSON object.");
                ValidateKeys(document.RootElement);
                return document;
            }
            catch { document.Dispose(); throw; }
        }
        private static void ValidateKeys(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    if (!keys.Add(property.Name)) throw new SaveException("Corrupt", "Duplicate JSON key: " + property.Name);
                    ValidateKeys(property.Value);
                }
            }
            else if (value.ValueKind == JsonValueKind.Array)
                foreach (var item in value.EnumerateArray()) ValidateKeys(item);
        }
    }
}
