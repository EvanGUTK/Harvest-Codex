using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Harvest.Application;
using Harvest.Persistence;

namespace Harvest.Composition
{
    /// <summary>Synchronous UI-thread storage boundary. Paths and offers stay private.</summary>
    public sealed class SessionStorage : ISessionStorage
    {
        private readonly SessionController controller;
        private readonly StoppedSessionCodec codec;
        private readonly string campaignDirectory, sandboxDirectory;
        private readonly AtomicSlotStore campaignStore, sandboxStore;
        private readonly Dictionary<string, Offer> offers = new Dictionary<string, Offer>(StringComparer.Ordinal);
        private readonly PhysicalSlotFiles files = new PhysicalSlotFiles();
        private const string Slot = "farm";

        public SessionStorage(SessionController controller, string rootDirectory, StoppedSessionCodec codec)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
            if (string.IsNullOrWhiteSpace(rootDirectory) || rootDirectory.Length > 4096 || !Path.IsPathFullyQualified(rootDirectory))
                throw new ArgumentException("Storage root must be an absolute bounded trusted path.", nameof(rootDirectory));
            var root = Path.GetFullPath(rootDirectory);
            if (root.Length > 4096 - 80) throw new ArgumentException("Storage root leaves insufficient room for save artifacts.", nameof(rootDirectory));
            campaignDirectory = Path.Combine(root, "campaign"); sandboxDirectory = Path.Combine(root, "sandbox");
            campaignStore = codec.CreateStore(campaignDirectory, SessionMode.Campaign);
            sandboxStore = codec.CreateStore(sandboxDirectory, SessionMode.Sandbox);
        }
        public void InvalidateRecovery() => offers.Clear();

        public StorageOutcome Save()
        {
            InvalidateRecovery();
            try
            {
                var snapshot = controller.CaptureStopped();
                var envelope = codec.Encode(snapshot);
                Directory.CreateDirectory(DirectoryFor(snapshot.Mode));
                StoreFor(snapshot.Mode).Save(Slot, envelope);
                return Success("Farm and draft saved. Execution is stopped.");
            }
            catch (SaveException ex) { return Failure(ex.Code); }
            catch (IOException) { return Failure("IoFailure"); }
            catch (UnauthorizedAccessException) { return Failure("IoFailure"); }
        }
        public StorageOutcome Load()
        {
            InvalidateRecovery();
            if (controller.Status != SessionStatus.Stopped) return Failure("StoppedRequired");
            try
            {
                var mode = controller.Mode;
                Directory.CreateDirectory(DirectoryFor(mode));
                var loaded = StoreFor(mode).Load(Slot);
                if (loaded.Primary != null) return Restore(loaded.Primary, mode, "Farm and draft loaded. Execution is stopped.");
                var choices = new List<RecoveryChoice>(); int interrupted = 0, unavailable = loaded.RecoveryDiagnostics.Count;
                foreach (var candidate in loaded.RecoveryCandidates)
                {
                    // Pin the exact bytes being offered, then validate those same bytes.
                    // A changed artifact during discovery is skipped with a friendly aggregate message.
                    try
                    {
                        var bytes = files.ReadBounded(candidate.Path, SaveEnvelope.MaxFileBytes);
                        ValidateMode(SaveEnvelope.Decode(bytes), mode);
                        var id = Guid.NewGuid().ToString("N");
                        offers.Add(id, new Offer(mode, candidate.Path, Hash(bytes)));
                        var label = candidate.Path.EndsWith(".bak", StringComparison.Ordinal) ? "Previous save" : "Interrupted save " + (++interrupted);
                        choices.Add(new RecoveryChoice(id, label));
                    }
                    catch (SaveException) { unavailable++; }
                    catch (IOException) { unavailable++; }
                    catch (UnauthorizedAccessException) { unavailable++; }
                }
                if (choices.Count > 0)
                    return new StorageOutcome(StorageStatus.RecoveryAvailable, "RecoveryAvailable", "The current save could not be loaded. Choose a validated previous or interrupted save to recover explicitly." + (unavailable > 0 ? " Some other artifacts could not be validated." : ""), choices);
                return Failure(loaded.PrimaryError ?? "Corrupt");
            }
            catch (SaveException ex) { InvalidateRecovery(); return Failure(ex.Code); }
            catch (IOException) { InvalidateRecovery(); return Failure("IoFailure"); }
            catch (UnauthorizedAccessException) { InvalidateRecovery(); return Failure("IoFailure"); }
        }
        public StorageOutcome Recover(string recoveryId)
        {
            Offer? offer = null;
            if (recoveryId != null && recoveryId.Length == 32) offers.TryGetValue(recoveryId, out offer);
            InvalidateRecovery(); // Every attempt consumes all offers, even a failed/active attempt.
            if (controller.Status != SessionStatus.Stopped) return Failure("StoppedRequired");
            if (offer == null || offer.Mode != controller.Mode) return Failure("StaleRecovery");
            try
            {
                var bytes = files.ReadBounded(offer.Path, SaveEnvelope.MaxFileBytes);
                if (!SameHash(offer.Identity, Hash(bytes))) return Failure("ChangedRecovery");
                // Restore only the exact reread bytes whose identity was compared, avoiding a second read race.
                return Restore(SaveEnvelope.Decode(bytes), offer.Mode, "Selected farm and draft recovered. Execution is stopped.");
            }
            catch (SaveException ex) { return Failure(ex.Code); }
            catch (IOException) { return Failure("IoFailure"); }
            catch (UnauthorizedAccessException) { return Failure("IoFailure"); }
        }
        private StorageOutcome Restore(SaveEnvelope envelope, SessionMode mode, string message)
        {
            var snapshot = ValidateMode(envelope, mode);
            try { controller.RestoreStopped(snapshot); }
            catch (ArgumentException ex) when (ex.ParamName == "snapshot") { return Failure("IncompatibleSimulation"); }
            return Success(message);
        }
        private StoppedSessionSnapshot ValidateMode(SaveEnvelope envelope, SessionMode mode)
        {
            var snapshot = codec.Decode(envelope);
            if (snapshot.Mode != mode) throw new SaveException("InvalidSnapshot", "Wrong mode.");
            return snapshot;
        }
        private string DirectoryFor(SessionMode mode) => mode == SessionMode.Campaign ? campaignDirectory : sandboxDirectory;
        private AtomicSlotStore StoreFor(SessionMode mode) => mode == SessionMode.Campaign ? campaignStore : sandboxStore;
        private static StorageOutcome Success(string message) => new StorageOutcome(StorageStatus.Succeeded, "none", message);
        private static StorageOutcome Failure(string code)
        {
            switch (code)
            {
                case "StoppedRequired": return new StorageOutcome(StorageStatus.Failed, code, "Stop execution before loading or recovering a farm.");
                case "StaleRecovery": return new StorageOutcome(StorageStatus.Failed, code, "That recovery choice has expired. Load again to see current choices.");
                case "ChangedRecovery": return new StorageOutcome(StorageStatus.Failed, code, "That recovery save changed. Load again to validate current choices.");
                case "UnsupportedSchema": return new StorageOutcome(StorageStatus.Failed, code, "This save uses an unsupported version. Use a compatible game version.");
                case "IncompatibleSimulation": return new StorageOutcome(StorageStatus.Failed, code, "This save requires a different game or content version.");
                case "LimitExceeded": return new StorageOutcome(StorageStatus.Failed, code, "Save data or recovery artifacts exceed the supported limits.");
                case "Corrupt": case "InvalidSnapshot": return new StorageOutcome(StorageStatus.Failed, code, "The save could not be validated. Your current farm and draft have not been replaced.");
                default: return new StorageOutcome(StorageStatus.Failed, "IoFailure", "Storage could not be read or written. Check available space and access, then try again.");
            }
        }
        private static byte[] Hash(byte[] bytes) { using var sha = SHA256.Create(); return sha.ComputeHash(bytes); }
        private static bool SameHash(byte[] left, byte[] right)
        {
            int difference = left.Length ^ right.Length;
            for (int i = 0; i < Math.Min(left.Length, right.Length); i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }
        private sealed class Offer
        {
            internal SessionMode Mode { get; }
            internal string Path { get; }
            internal byte[] Identity { get; }
            internal Offer(SessionMode mode, string path, byte[] identity) { Mode = mode; Path = path; Identity = identity; }
        }
    }
}
