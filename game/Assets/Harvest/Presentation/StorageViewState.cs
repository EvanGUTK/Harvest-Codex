#nullable enable
using System;
using System.Collections.Generic;
using Harvest.Application;
using Harvest.Language;
namespace Harvest.Presentation
{
    public interface ISessionStorageView
    {
        StorageViewState ReadStorage();
        void Save();
        void RequestLoad();
        void RequestRecovery(string id);
        void ConfirmReplacement();
        void CancelReplacement();
    }
    public interface IProgrammingReferenceView
    {
        IReadOnlyList<ReferenceEntry> ReadReference();
    }
    public sealed class StorageViewState
    {
        public bool Available { get; }
        public bool CanSave { get; }
        public bool CanLoad { get; }
        public bool CanConfirm { get; }
        public string Code { get; }
        public string Message { get; }
        public string Confirmation { get; }
        public IReadOnlyList<RecoveryChoice> RecoveryChoices { get; }
        internal StorageViewState(bool available, bool stopped, string code, string message,
            string confirmation, IEnumerable<RecoveryChoice> choices)
        {
            if (code.Length > 64 || message.Length > 1024 || confirmation.Length > 512) throw new ArgumentException("Storage display text exceeds its bound");
            var copy = new List<RecoveryChoice>();
            foreach (var choice in choices)
            { if (copy.Count >= 65 || choice == null) throw new ArgumentException("Invalid displayed recovery choices"); copy.Add(choice); }
            Available = available; CanSave = available; CanLoad = available && stopped;
            CanConfirm = available && stopped && confirmation.Length > 0;
            Code = code; Message = message; Confirmation = confirmation; RecoveryChoices = copy.AsReadOnly();
        }
    }
}
