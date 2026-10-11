using System;
using System.Text;
using Harvest.Language;
namespace Harvest.Presentation
{
    public static class DraftEdit
    {
        public static bool TryApply(string candidate, Action<string> apply, out string error)
        {
            // Character count is a cheap early bound; UTF-8 bytes match Application/Language limits.
            // Reject the whole edit before calling the session setter; never truncate source.
            if (candidate.Length > RuntimeLimits.SourceBytes || Encoding.UTF8.GetByteCount(candidate) > RuntimeLimits.SourceBytes)
            {
                error = "Draft edit rejected: source exceeds 64 KiB of UTF-8 text. Previous draft retained. Reduce the pasted text and try again.";
                return false;
            }
            apply(candidate);
            error = "";
            return true;
        }
    }
}
