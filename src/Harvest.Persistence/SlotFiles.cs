using System;
using System.IO;
using System.Collections.Generic;

namespace Harvest.Persistence
{
    // Fault-injection seam; implementations must preserve same-filesystem rename semantics.
    public interface ISlotFiles
    {
        bool Exists(string path);
        byte[] ReadBounded(string path, int maximumBytes);
        void WriteExclusiveAndFlush(string path, byte[] bytes);
        void InstallAtomic(string temporary, string destination);
        string[] FindTemps(string directory, string prefix);
    }
    public sealed class PhysicalSlotFiles : ISlotFiles
    {
        public bool Exists(string path) => File.Exists(path);
        public byte[] ReadBounded(string path, int maximumBytes)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > maximumBytes) throw new SaveException("LimitExceeded", "Save file exceeds bound.");
            using var output = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                if (output.Length + count > maximumBytes) throw new SaveException("LimitExceeded", "Save grew beyond bound.");
                output.Write(buffer, 0, count);
            }
            return output.ToArray();
        }
        public void WriteExclusiveAndFlush(string path, byte[] bytes)
        {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
        }
        public void InstallAtomic(string temporary, string destination)
        {
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
        public string[] FindTemps(string directory, string prefix)
        {
            var paths = new List<string>();
            foreach (var path in Directory.EnumerateFiles(directory, prefix + "*.tmp"))
            {
                paths.Add(path);
                if (paths.Count > 64) break;
            }
            return paths.ToArray();
        }
    }
}
