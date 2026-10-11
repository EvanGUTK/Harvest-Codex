using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Harvest.Persistence;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    private static void Reject(Action action, string code)
    {
        try { action(); throw new Exception("Expected " + code); }
        catch (SaveException ex) { Check(ex.Code == code, "Wrong error: " + ex.Code); }
    }
    private static SaveEnvelope Envelope(int tick) => SaveEnvelope.Create("spike-1", "wheat-1", "{\"executionState\":\"stopped\",\"tick\":" + tick + "}");
    private static void Validate(SaveEnvelope envelope)
    {
        using var document = JsonDocument.Parse(envelope.Payload);
        if (document.RootElement.GetProperty("executionState").GetString() != "stopped") throw new SaveException("InvalidSnapshot", "Not stopped.");
    }
    private static void Main()
    {
        WorldCodecChecks.Run(Check, Reject);
        var encoded = Envelope(0).Encode();
        Check(SaveEnvelope.Decode(encoded).Payload == Envelope(0).Payload, "Envelope roundtrip");
        var text = Encoding.UTF8.GetString(encoded);
        Reject(() => SaveEnvelope.Decode(Encoding.UTF8.GetBytes(text.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"))), "UnsupportedSchema");
        Reject(() => SaveEnvelope.Decode(Encoding.UTF8.GetBytes(text.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1"))), "Corrupt");
        Reject(() => SaveEnvelope.Decode(Encoding.UTF8.GetBytes(text.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"unexpected\":0"))), "Corrupt");
        Reject(() => SaveEnvelope.Decode(Encoding.UTF8.GetBytes(text.Replace("spike-1", "spike-1\""))), "Corrupt");
        Reject(() => SaveEnvelope.Decode(new byte[] { 0xff }), "Corrupt");
        Reject(() => SaveEnvelope.Decode(new byte[SaveEnvelope.MaxFileBytes + 1]), "LimitExceeded");
        Reject(() => SaveEnvelope.Create("v", "c", "{\"a\":{\"b\":1,\"b\":2}}"), "Corrupt");
        Reject(() => SaveEnvelope.Create("v", "c", "{\"a\":" + new string('[', 65) + "0" + new string(']', 65) + "}"), "Corrupt");
        var tamper = encoded.ToArray();
        var index = text.IndexOf("payloadSha256", StringComparison.Ordinal) + "payloadSha256\":\"".Length;
        tamper[index] = tamper[index] == (byte)'a' ? (byte)'b' : (byte)'a';
        Reject(() => SaveEnvelope.Decode(tamper), "Corrupt");
        PayloadLimits.ValidateScripts(new[] { new string('x', 65536) });
        Reject(() => PayloadLimits.ValidateScripts(new[] { new string('é', 32769) }), "LimitExceeded");
        Reject(() => PayloadLimits.ValidateScripts(Enumerable.Repeat(new string('x', 65536), 17)), "LimitExceeded");
        Reject(() => PayloadLimits.ValidateSaveLocalEditorJson(new string('x', 65537)), "LimitExceeded");
        WithDirectory(directory =>
        {
            var store = new AtomicSlotStore(directory, Validate);
            store.Save("campaign", Envelope(0)); store.Save("campaign", Envelope(1));
            Check(store.Load("campaign").Primary!.Payload == Envelope(1).Payload, "Primary replaced");
            Check(SaveEnvelope.Decode(File.ReadAllBytes(Path.Combine(directory, "campaign.save.bak"))).Payload == Envelope(0).Payload, "Previous backup");
            File.WriteAllText(Path.Combine(directory, "campaign.save"), "corrupt");
            var recovery = store.Load("campaign");
            Check(recovery.Primary == null && recovery.PrimaryError == "Corrupt" && recovery.RecoveryCandidates.Count == 1, "Explicit backup recovery");
            Check(store.ReadRecovery("campaign", recovery.RecoveryCandidates[0].Path).Payload == Envelope(0).Payload, "Read selected candidate");
            store.Save("campaign", Envelope(2));
            Check(SaveEnvelope.Decode(File.ReadAllBytes(Path.Combine(directory, "campaign.save.bak"))).Payload == Envelope(0).Payload, "Corrupt primary did not overwrite good backup");
            Reject(() => store.Save("../escape", Envelope(0)), "InvalidSlot");
            Reject(() => store.ReadRecovery("campaign", Path.Combine(directory, "other.save.bak")), "InvalidSlot");
            Reject(() => store.Save("campaign", SaveEnvelope.Create("v", "c", "{\"executionState\":\"running\"}")), "InvalidSnapshot");
            Check(store.Load("campaign").Primary!.Payload == Envelope(2).Payload, "Invalid snapshot leaves primary untouched");
            var future = Encoding.UTF8.GetString(Envelope(3).Encode()).Replace("\"schemaVersion\":1", "\"schemaVersion\":2");
            File.WriteAllText(Path.Combine(directory, "campaign.save"), future);
            Reject(() => store.Save("campaign", Envelope(4)), "UnsupportedSchema");
            Check(File.ReadAllText(Path.Combine(directory, "campaign.save")) == future, "Future primary not overwritten");
        });
        // Fail immediately before or after each mutating filesystem operation.
        for (int operation = 1; operation <= 4; operation++)
        foreach (bool after in new[] { false, true })
        {
            int selected = operation;
            WithDirectory(directory =>
            {
                var physical = new AtomicSlotStore(directory, Validate);
                physical.Save("slot", Envelope(0)); physical.Save("slot", Envelope(1));
                var fault = new FaultFiles(selected, after);
                var failing = new AtomicSlotStore(directory, Validate, fault);
                Reject(() => failing.Save("slot", Envelope(2)), "IoFailure");
                var result = physical.Load("slot");
                Check(result.Primary != null, "Interruption retained primary");
                Check(result.Primary!.Payload == Envelope(1).Payload || result.Primary.Payload == Envelope(2).Payload, "Primary is complete old/new");
                var backup = SaveEnvelope.Decode(File.ReadAllBytes(Path.Combine(directory, "slot.save.bak")));
                Check(backup.Payload == Envelope(0).Payload || backup.Payload == Envelope(1).Payload, "Interruption retained valid backup");
                File.WriteAllText(Path.Combine(directory, "slot.save"), "bad");
                var recovery = physical.Load("slot");
                Check(recovery.Primary == null && recovery.RecoveryCandidates.Count >= 1, "Recovery never silently promotes temp");
                foreach (var candidate in recovery.RecoveryCandidates) Validate(physical.ReadRecovery("slot", candidate.Path));
            });
        }
        WithDirectory(directory =>
        {
            // A temp flushed before first primary installation is only a recovery candidate.
            var failing = new AtomicSlotStore(directory, Validate, new FaultFiles(2, false));
            Reject(() => failing.Save("slot", Envelope(0)), "IoFailure");
            var result = new AtomicSlotStore(directory, Validate).Load("slot");
            Check(result.Primary == null && result.RecoveryCandidates.Count == 1, "First-write orphan temp explicit");
            File.WriteAllText(Path.Combine(directory, "slot.broken.tmp"), "partial");
            var withPartial = new AtomicSlotStore(directory, Validate).Load("slot");
            Check(withPartial.RecoveryCandidates.Count == 1, "Partial temp excluded");
            Check(withPartial.RecoveryDiagnostics.Count == 1 && withPartial.RecoveryDiagnostics[0].Code == "Corrupt", "Partial temp diagnosis surfaced");
        });
        foreach (bool corruptInsteadOfThrow in new[] { false, true })
        WithDirectory(directory =>
        {
            var normal = new AtomicSlotStore(directory, Validate);
            normal.Save("slot", Envelope(0)); normal.Save("slot", Envelope(1));
            var failing = new AtomicSlotStore(directory, Validate, new PartialFiles(corruptInsteadOfThrow));
            Reject(() => failing.Save("slot", Envelope(2)), corruptInsteadOfThrow ? "Corrupt" : "IoFailure");
            Check(normal.Load("slot").Primary!.Payload == Envelope(1).Payload, "Partial/corrupt temp did not install");
            Check(SaveEnvelope.Decode(File.ReadAllBytes(Path.Combine(directory, "slot.save.bak"))).Payload == Envelope(0).Payload, "Partial/corrupt temp did not replace backup");
        });
        WithDirectory(directory =>
        {
            var store = new AtomicSlotStore(directory, Validate);
            store.Save("slot", Envelope(0)); store.Save("slot", Envelope(1));
            File.WriteAllText(Path.Combine(directory, "slot.save"), "bad");
            for (int i = 0; i < 70; i++) File.WriteAllText(Path.Combine(directory, "slot." + i + ".tmp"), "bad");
            var result = store.Load("slot");
            Check(result.RecoveryCandidates.Count == 1, "Excess orphan temps preserve backup option");
            Check(result.RecoveryDiagnostics.Any(d => d.Code == "LimitExceeded"), "Recovery scan bound diagnosed");
            Check(result.RecoveryDiagnostics.Count <= 65, "Recovery scan allocation bounded");
        });
        WithDirectory(directory =>
        {
            Parallel.For(0, 24, tick => new AtomicSlotStore(directory, Validate).Save("slot", Envelope(tick)));
            var store = new AtomicSlotStore(directory, Validate);
            Check(store.Load("slot").Primary != null, "Concurrent instances retain primary");
            Validate(SaveEnvelope.Decode(File.ReadAllBytes(Path.Combine(directory, "slot.save.bak"))));
        });
        Console.WriteLine("PASS: " + checks + " persistence assertions, including macOS atomic replacement and 8 fault boundaries.");
    }
    private static void WithDirectory(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "harvest-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(directory); } finally { Directory.Delete(directory, true); }
    }
    private sealed class PartialFiles : ISlotFiles
    {
        private readonly PhysicalSlotFiles inner = new PhysicalSlotFiles();
        private readonly bool corruptInsteadOfThrow;
        public PartialFiles(bool corruptInsteadOfThrow) { this.corruptInsteadOfThrow = corruptInsteadOfThrow; }
        public bool Exists(string path) => inner.Exists(path);
        public byte[] ReadBounded(string path, int maximumBytes) => inner.ReadBounded(path, maximumBytes);
        public string[] FindTemps(string directory, string prefix) => inner.FindTemps(directory, prefix);
        public void InstallAtomic(string temporary, string destination) => inner.InstallAtomic(temporary, destination);
        public void WriteExclusiveAndFlush(string path, byte[] bytes)
        {
            inner.WriteExclusiveAndFlush(path, bytes.Take(bytes.Length / 2).ToArray());
            if (!corruptInsteadOfThrow) throw new IOException("Injected partial write");
        }
    }
    private sealed class FaultFiles : ISlotFiles
    {
        private readonly PhysicalSlotFiles inner = new PhysicalSlotFiles();
        private readonly int operation; private readonly bool after; private int current;
        public FaultFiles(int operation, bool after) { this.operation = operation; this.after = after; }
        private void Mutate(Action action)
        {
            current++;
            if (current == operation && !after) throw new IOException("Injected before operation");
            action();
            if (current == operation && after) throw new IOException("Injected after operation");
        }
        public bool Exists(string path) => inner.Exists(path);
        public byte[] ReadBounded(string path, int maximumBytes) => inner.ReadBounded(path, maximumBytes);
        public string[] FindTemps(string directory, string prefix) => inner.FindTemps(directory, prefix);
        public void WriteExclusiveAndFlush(string path, byte[] bytes) => Mutate(() => inner.WriteExclusiveAndFlush(path, bytes));
        public void InstallAtomic(string temporary, string destination) => Mutate(() => inner.InstallAtomic(temporary, destination));
    }
}
