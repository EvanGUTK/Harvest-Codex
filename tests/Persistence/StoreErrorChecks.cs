using System;
using System.IO;
using Harvest.Persistence;

internal static class StoreErrorChecks
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var directory = Path.Combine(Path.GetTempPath(), "harvest-review04-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "slot.save"), "corrupt");
            var store = new AtomicSlotStore(directory, _ => { }, new DeniedScanFiles());
            reject(() => store.Load("slot"), "IoFailure");
        }
        finally { Directory.Delete(directory, true); }
        var root = Path.GetPathRoot(Path.GetFullPath("."))!;
        var probe = new RootProbeFiles();
        new AtomicSlotStore(root, _ => { }, probe).Load("slot");
        check(probe.ScannedDirectory == root, "Filesystem root preserved during normalization");
        check(probe.ReadPath == Path.Combine(root, "slot.save"), "Root slot remains absolute");
    }
    private sealed class DeniedScanFiles : ISlotFiles
    {
        private readonly PhysicalSlotFiles inner = new PhysicalSlotFiles();
        public bool Exists(string path) => inner.Exists(path);
        public byte[] ReadBounded(string path, int maximumBytes) => inner.ReadBounded(path, maximumBytes);
        public void WriteExclusiveAndFlush(string path, byte[] bytes) => inner.WriteExclusiveAndFlush(path, bytes);
        public void InstallAtomic(string temporary, string destination) => inner.InstallAtomic(temporary, destination);
        public string[] FindTemps(string directory, string prefix) => throw new UnauthorizedAccessException("Injected denied recovery scan");
    }
    private sealed class RootProbeFiles : ISlotFiles
    {
        public string? ScannedDirectory { get; private set; }
        public string? ReadPath { get; private set; }
        public bool Exists(string path) => false;
        public byte[] ReadBounded(string path, int maximumBytes) { ReadPath = path; throw new FileNotFoundException(); }
        public string[] FindTemps(string directory, string prefix) { ScannedDirectory = directory; return Array.Empty<string>(); }
        public void WriteExclusiveAndFlush(string path, byte[] bytes) => throw new Exception("Unexpected write");
        public void InstallAtomic(string temporary, string destination) => throw new Exception("Unexpected install");
    }
}
