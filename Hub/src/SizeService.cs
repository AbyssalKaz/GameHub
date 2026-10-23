using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class SizeRecord
    {
        public string Path { get; set; }
        public long Bytes { get; set; }
        public long Files { get; set; }
        public bool Missing { get; set; }
        public string ScannedAt { get; set; }
        public string Signature { get; set; }
    }

    internal sealed class SizeCache
    {
        public string LastFullScan { get; set; }
        public Dictionary<string, SizeRecord> Items { get; set; } = new Dictionary<string, SizeRecord>();
    }

    internal sealed class SizeService
    {
        private static readonly TimeSpan FullScanInterval = TimeSpan.FromHours(6);

        private readonly object sync = new object();
        private readonly SizeCache cache;

        public bool IsScanning { get; private set; }

        public bool ForceNextScan { get; set; }

        public string LastFullScan
        {
            get
            {
                lock (sync) return cache.LastFullScan;
            }
        }

        public SizeService()
        {
            cache = Json.ReadFile<SizeCache>(HubPaths.SizeCacheFile) ?? new SizeCache();
            cache.Items ??= new Dictionary<string, SizeRecord>();
        }

        public static string Key(string path) => path.TrimEnd('\\').ToLowerInvariant();

        public Dictionary<string, SizeRecord> Snapshot()
        {
            lock (sync) return new Dictionary<string, SizeRecord>(cache.Items);
        }

        public bool IsFullScanDue
        {
            get
            {
                lock (sync)
                {
                    return cache.LastFullScan == null || DateTime.UtcNow - DateTime.Parse(cache.LastFullScan).ToUniversalTime() > FullScanInterval;
                }
            }
        }

        public async Task ScanAsync(IEnumerable<string> paths, bool everything, Action<SizeRecord> measured, Action<int, int> progress)
        {
            List<string> candidates;
            Dictionary<string, SizeRecord> known;
            bool force;
            lock (sync)
            {
                if (IsScanning) return;
                IsScanning = true;
                force = ForceNextScan;
                ForceNextScan = false;
                known = new Dictionary<string, SizeRecord>(cache.Items);
                candidates = paths
                    .Where(path => !string.IsNullOrEmpty(path))
                    .GroupBy(Key)
                    .Select(group => group.First())
                    .Where(path => everything || !known.ContainsKey(Key(path)))
                    .ToList();
            }

            try
            {
                List<string> targets = force || !everything
                    ? candidates
                    : await Task.Run(() => candidates.Where(path => HasChanged(path, known)).ToList());

                int done = 0;
                progress(done, targets.Count);

                var perDrive = targets.GroupBy(path => System.IO.Path.GetPathRoot(path).ToUpperInvariant());
                await Task.WhenAll(perDrive.Select(drive => Task.Run(() =>
                {
                    foreach (string path in drive)
                    {
                        SizeRecord record = Measure(path);
                        lock (sync) cache.Items[Key(path)] = record;
                        measured(record);
                        progress(System.Threading.Interlocked.Increment(ref done), targets.Count);
                    }
                })));

                lock (sync)
                {
                    if (everything) cache.LastFullScan = DateTime.UtcNow.ToString("o");
                    Json.WriteFile(HubPaths.SizeCacheFile, cache);
                }
            }
            finally
            {
                lock (sync) IsScanning = false;
            }
        }

        private static bool HasChanged(string path, Dictionary<string, SizeRecord> known)
        {
            if (!known.TryGetValue(Key(path), out SizeRecord record) || string.IsNullOrEmpty(record.Signature)) return true;
            return Signature(path) != record.Signature;
        }

        public static string Signature(string root)
        {
            try
            {
                var directory = new DirectoryInfo(root.TrimEnd('\\'));
                if (!directory.Exists) return "missing";
                ulong hash = 14695981039346656037UL;
                void Add(string text)
                {
                    foreach (char character in text)
                    {
                        hash ^= character;
                        hash *= 1099511628211UL;
                    }
                }
                Add(directory.LastWriteTimeUtc.Ticks.ToString());
                foreach (DirectoryInfo child in directory.EnumerateDirectories().OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if ((child.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    Add("|" + child.Name.ToLowerInvariant() + ":" + child.LastWriteTimeUtc.Ticks);
                }
                return hash.ToString("x16");
            }
            catch
            {
                return null;
            }
        }

        private static SizeRecord Measure(string root)
        {
            var record = new SizeRecord { Path = root, ScannedAt = DateTime.UtcNow.ToString("o"), Signature = Signature(root) };
            if (!Directory.Exists(root))
            {
                record.Missing = true;
                return record;
            }

            var pending = new Stack<string>();
            pending.Push(root.TrimEnd('\\'));

            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                IntPtr handle = NativeMethods.FindFirstFileExW(@"\\?\" + directory + @"\*", NativeMethods.FindExInfoBasic, out NativeMethods.Win32FindData data, 0, IntPtr.Zero, NativeMethods.FindFirstExLargeFetch);
                if (handle == NativeMethods.InvalidHandle) continue;

                try
                {
                    do
                    {
                        string name = data.FileName;
                        if (name == "." || name == "..") continue;

                        if ((data.FileAttributes & NativeMethods.FileAttributeDirectory) != 0)
                        {
                            if ((data.FileAttributes & NativeMethods.FileAttributeReparsePoint) == 0) pending.Push(directory + "\\" + name);
                        }
                        else
                        {
                            record.Bytes += ((long)data.FileSizeHigh << 32) | data.FileSizeLow;
                            record.Files++;
                        }
                    }
                    while (NativeMethods.FindNextFileW(handle, out data));
                }
                finally
                {
                    NativeMethods.FindClose(handle);
                }
            }
            return record;
        }
    }
}
