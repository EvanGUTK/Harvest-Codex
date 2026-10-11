using System;
using System.Collections.Generic;
using System.Text;

namespace Harvest.Language
{
    /// <summary>Immutable reference content; examples are inert text, never automatically executed.</summary>
    public sealed class ReferenceEntry
    {
        public const int MaxIdBytes = 64;
        public const int MaxTitleBytes = 128;
        public const int MaxExampleBytes = 4096;
        public const int MaxDescriptionBytes = 2048;
        public const int MaxRequiredCapabilities = 18;

        public string Id { get; }
        public string Title { get; }
        public string Example { get; }
        public string Description { get; }
        public IReadOnlyList<string> RequiredCapabilities { get; }

        public ReferenceEntry(string id, string title, string example, string description,
            IEnumerable<string> requiredCapabilities)
        {
            Id = BoundedText(id, MaxIdBytes, nameof(id));
            foreach (char c in Id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                    throw new ArgumentException("Reference ID must use lower-case letters, digits, hyphens or underscores.", nameof(id));
            Title = BoundedText(title, MaxTitleBytes, nameof(title));
            Example = BoundedText(example, MaxExampleBytes, nameof(example));
            Description = BoundedText(description, MaxDescriptionBytes, nameof(description));
            if (requiredCapabilities == null) throw new ArgumentNullException(nameof(requiredCapabilities));
            var requirements = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            Capabilities implemented = Capabilities.All;
            foreach (string capability in requiredCapabilities)
            {
                if (requirements.Count >= MaxRequiredCapabilities)
                    throw new ArgumentException("Too many reference capability requirements.", nameof(requiredCapabilities));
                if (capability == null || capability.Length > 64 || !implemented.Allows(capability) || !unique.Add(capability))
                    throw new ArgumentException("Requirements must be distinct implemented capabilities.", nameof(requiredCapabilities));
                requirements.Add(capability);
            }
            RequiredCapabilities = requirements.AsReadOnly();
        }

        private static string BoundedText(string value, int maxBytes, string parameter)
        {
            if (value == null) throw new ArgumentNullException(parameter);
            if (value.Length > maxBytes || Encoding.UTF8.GetByteCount(value) > maxBytes || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Reference text must be nonempty and within its UTF-8 byte limit.", parameter);
            return value;
        }
    }
}
