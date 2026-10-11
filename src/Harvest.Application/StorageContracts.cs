using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Harvest.Application
{
    public enum StorageStatus { Succeeded, Failed, RecoveryAvailable }

    public sealed class RecoveryChoice
    {
        public string Id { get; }
        public string Label { get; }
        public RecoveryChoice(string id, string label)
        {
            if (id == null || id.Length != 32) throw new ArgumentException("Recovery ID must be opaque 32-character hex.", nameof(id));
            foreach (char c in id)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) throw new ArgumentException("Invalid recovery ID.", nameof(id));
            if (string.IsNullOrWhiteSpace(label) || label.Length > 160) throw new ArgumentException("Recovery label must be bounded.", nameof(label));
            Id = id; Label = label;
        }
    }

    public sealed class StorageOutcome
    {
        public StorageStatus Status { get; }
        public string Code { get; }
        public string Message { get; }
        public IReadOnlyList<RecoveryChoice> RecoveryChoices { get; }
        public StorageOutcome(StorageStatus status, string code, string message, IEnumerable<RecoveryChoice>? choices = null)
        {
            if (!Enum.IsDefined(typeof(StorageStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (string.IsNullOrWhiteSpace(code) || code.Length > 64) throw new ArgumentException("Storage code must be bounded.", nameof(code));
            if (string.IsNullOrWhiteSpace(message) || message.Length > 1024) throw new ArgumentException("Storage message must be bounded.", nameof(message));
            var copy = new List<RecoveryChoice>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (choices != null)
                foreach (var choice in choices)
                {
                    if (choice == null || copy.Count >= 65 || !ids.Add(choice.Id)) throw new ArgumentException("Invalid recovery choices.", nameof(choices));
                    copy.Add(choice);
                }
            if ((status == StorageStatus.Succeeded) != (code == "none") ||
                (status == StorageStatus.RecoveryAvailable) != (copy.Count > 0))
                throw new ArgumentException("Storage status/code/choices disagree.");
            Status = status; Code = code; Message = message;
            RecoveryChoices = new ReadOnlyCollection<RecoveryChoice>(copy);
        }
    }

    public interface ISessionStorage
    {
        StorageOutcome Save();
        StorageOutcome Load();
        StorageOutcome Recover(string recoveryId);
        void InvalidateRecovery();
    }
}
