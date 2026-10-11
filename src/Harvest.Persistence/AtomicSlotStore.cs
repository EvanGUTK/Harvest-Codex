using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;

namespace Harvest.Persistence
{
    public sealed class RecoveryCandidate
    {
        public string Path { get; }
        public SaveEnvelope Envelope { get; }
        internal RecoveryCandidate(string path, SaveEnvelope envelope) { Path = path; Envelope = envelope; }
    }
    public sealed class RecoveryDiagnostic
    {
        public string Path { get; }
        public string Code { get; }
        internal RecoveryDiagnostic(string path, string code) { Path = path; Code = code; }
    }
    public sealed class SlotLoadResult
    {
        public SaveEnvelope? Primary { get; }
        public string? PrimaryError { get; }
        public IReadOnlyList<RecoveryCandidate> RecoveryCandidates { get; }
        public IReadOnlyList<RecoveryDiagnostic> RecoveryDiagnostics { get; }
        internal SlotLoadResult(SaveEnvelope? primary, string? error, List<RecoveryCandidate> candidates, List<RecoveryDiagnostic> diagnostics)
        { Primary = primary; PrimaryError = error; RecoveryCandidates = candidates.AsReadOnly(); RecoveryDiagnostics = diagnostics.AsReadOnly(); }
    }
    public sealed class AtomicSlotStore
    {
        private readonly string directory;
        private readonly ISlotFiles files;
        // All instances in this process serialize a directory. Cross-process writers are unsupported.
        private static readonly ConcurrentDictionary<string, object> Gates = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        private readonly object gate;
        private readonly Action<SaveEnvelope> validatePayload;
        public AtomicSlotStore(string existingDirectory, Action<SaveEnvelope> validatePayload, ISlotFiles? files = null)
        {
            directory = System.IO.Path.GetFullPath(existingDirectory).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            gate = Gates.GetOrAdd(directory, _ => new object());
            this.validatePayload = validatePayload ?? throw new ArgumentNullException(nameof(validatePayload));
            this.files = files ?? new PhysicalSlotFiles();
        }
        public void Save(string slot, SaveEnvelope envelope)
        {
            lock (gate)
            {
                try
                {
                    validatePayload(envelope);
                    var encoded = envelope.Encode(); var primary = SlotPath(slot);
                    var temporary = TempPath(slot);
                    files.WriteExclusiveAndFlush(temporary, encoded);
                    ReadValidated(temporary);
                    if (files.Exists(primary))
                    {
                        byte[]? previous = null;
                        try { previous = files.ReadBounded(primary, SaveEnvelope.MaxFileBytes); Validate(previous); }
                        catch (SaveException ex) when (ex.Code == "Corrupt" || ex.Code == "InvalidSnapshot" || ex.Code == "LimitExceeded") { previous = null; }
                        if (previous != null)
                        {
                            var backupTemp = TempPath(slot);
                            files.WriteExclusiveAndFlush(backupTemp, previous);
                            ReadValidated(backupTemp);
                            files.InstallAtomic(backupTemp, primary + ".bak");
                        }
                    }
                    files.InstallAtomic(temporary, primary);
                }
                catch (IOException ex) { throw new SaveException("IoFailure", "Save failed; inspect primary/backup/temporary recovery candidates.", ex); }
                catch (UnauthorizedAccessException ex) { throw new SaveException("IoFailure", "Save access denied.", ex); }
            }
        }
        public SlotLoadResult Load(string slot)
        {
            lock (gate)
            {
                var primary = SlotPath(slot); string? error = null;
                try { return new SlotLoadResult(ReadValidated(primary), null, new List<RecoveryCandidate>(), new List<RecoveryDiagnostic>()); }
                catch (SaveException ex) { error = ex.Code; }
                var candidates = new List<RecoveryCandidate>();
                var diagnostics = new List<RecoveryDiagnostic>();
                TryCandidate(primary + ".bak", candidates, diagnostics);
                try
                {
                    var temps = files.FindTemps(directory, slot + "."); Array.Sort(temps, StringComparer.Ordinal);
                    if (temps.Length > 64)
                        diagnostics.Add(new RecoveryDiagnostic(directory, "LimitExceeded"));
                    for (int i = 0; i < Math.Min(temps.Length, 64); i++) TryCandidate(temps[i], candidates, diagnostics);
                }
                catch (IOException ex) { throw new SaveException("IoFailure", "Recovery scan failed.", ex); }
                return new SlotLoadResult(null, error, candidates, diagnostics);
            }
        }
        // Selection is explicit and revalidated from disk. Store never touches a live session.
        public SaveEnvelope ReadRecovery(string slot, string candidatePath)
        {
            lock (gate)
            {
                var primary = SlotPath(slot);
                var full = System.IO.Path.GetFullPath(candidatePath);
                if (full != primary + ".bak" && !(System.IO.Path.GetDirectoryName(full) == directory && System.IO.Path.GetFileName(full).StartsWith(slot + ".", StringComparison.Ordinal) && full.EndsWith(".tmp", StringComparison.Ordinal)))
                    throw new SaveException("InvalidSlot", "Candidate does not belong to slot.");
                return ReadValidated(full);
            }
        }
        private void TryCandidate(string path, List<RecoveryCandidate> candidates, List<RecoveryDiagnostic> diagnostics)
        {
            if (!files.Exists(path)) return;
            try { candidates.Add(new RecoveryCandidate(path, ReadValidated(path))); }
            catch (SaveException ex) { diagnostics.Add(new RecoveryDiagnostic(path, ex.Code)); }
        }
        private SaveEnvelope ReadValidated(string path)
        {
            try { return Validate(files.ReadBounded(path, SaveEnvelope.MaxFileBytes)); }
            catch (IOException ex) { throw new SaveException("IoFailure", "Cannot read save candidate.", ex); }
            catch (UnauthorizedAccessException ex) { throw new SaveException("IoFailure", "Cannot access save candidate.", ex); }
        }
        private SaveEnvelope Validate(byte[] bytes) { var envelope = SaveEnvelope.Decode(bytes); validatePayload(envelope); return envelope; }
        private string TempPath(string slot) => System.IO.Path.Combine(directory, slot + "." + Guid.NewGuid().ToString("N") + ".tmp");
        private string SlotPath(string slot)
        {
            if (string.IsNullOrEmpty(slot) || slot.Length > 64) throw new SaveException("InvalidSlot", "Invalid slot name.");
            foreach (var c in slot) if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_')) throw new SaveException("InvalidSlot", "Slot must be a simple identifier.");
            return System.IO.Path.Combine(directory, slot + ".save");
        }
    }
}
