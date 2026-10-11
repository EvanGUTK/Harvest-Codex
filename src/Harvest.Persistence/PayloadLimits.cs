using System;
using System.Collections.Generic;
using System.Text;

namespace Harvest.Persistence
{
    // Reusable bounds for the forthcoming published stopped-session adapter.
    public static class PayloadLimits
    {
        public const int ScriptBytes = 64 * 1024;
        public const int AggregateScriptBytes = 1024 * 1024;
        public const int SaveLocalEditorBytes = 64 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static void ValidateScripts(IEnumerable<string> sources)
        {
            int total = 0;
            foreach (var source in sources)
            {
                var length = ByteCount(source);
                if (length > ScriptBytes || length > AggregateScriptBytes - total)
                    throw new SaveException("LimitExceeded", "Script source exceeds save bounds.");
                total += length;
            }
        }
        public static void ValidateSaveLocalEditorJson(string editorJson)
        {
            if (ByteCount(editorJson) > SaveLocalEditorBytes) throw new SaveException("LimitExceeded", "Save-local editor state exceeds 64 KiB.");
            // Adapter owns the allowlist; global audio/accessibility settings are excluded.
        }
        private static int ByteCount(string text)
        {
            if (text == null) throw new SaveException("InvalidSnapshot", "Null source/editor text.");
            try { return Utf8.GetByteCount(text); }
            catch (ArgumentException ex) { throw new SaveException("InvalidSnapshot", "Invalid Unicode text.", ex); }
        }
    }
}
