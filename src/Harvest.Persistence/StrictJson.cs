using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Harvest.Persistence
{
    internal static class StrictJson
    {
        internal static void Keys(JsonElement value, params string[] names)
        {
            if (value.ValueKind != JsonValueKind.Object) throw new SaveException("Corrupt", "Expected object.");
            var expected = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
                if (!expected.Remove(property.Name)) throw new SaveException("Corrupt", "Unexpected/duplicate key: " + property.Name);
            if (expected.Count != 0) throw new SaveException("Corrupt", "Missing keys: " + string.Join(",", expected));
        }
        internal static JsonElement Array(JsonElement parent, string key, int maximum)
        {
            var value = parent.GetProperty(key);
            if (value.ValueKind != JsonValueKind.Array) throw new SaveException("Corrupt", "Expected array: " + key);
            if (value.GetArrayLength() > maximum) throw new SaveException("LimitExceeded", "Array exceeds bound: " + key);
            return value;
        }
        internal static string Text(JsonElement parent, string key, int maximum)
        {
            var value = parent.GetProperty(key).GetString();
            if (value == null || value.Length > maximum) throw new SaveException("Corrupt", "Invalid string: " + key);
            return value;
        }
        internal static T EnumValue<T>(JsonElement parent, string key) where T : struct, Enum
        {
            var text = Text(parent, key, 64);
            if (!Enum.TryParse<T>(text, false, out var result) || Enum.GetName(typeof(T), result) != text)
                throw new SaveException("Corrupt", "Unknown enum: " + key);
            return result;
        }
    }
}
