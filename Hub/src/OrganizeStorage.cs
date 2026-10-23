using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class FolderBreakdownItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Bytes { get; set; }
        public long Files { get; set; }
        public bool IsFiles { get; set; }
        public bool Pending { get; set; }
    }

    internal sealed class FolderBreakdown
    {
        public string Path { get; set; }
        public string ScannedAt { get; set; }
        public List<FolderBreakdownItem> Items { get; set; } = new List<FolderBreakdownItem>();
    }

    internal sealed class FolderBreakdownCache
    {
        public Dictionary<string, FolderBreakdown> Items { get; set; } = new Dictionary<string, FolderBreakdown>();
    }

    internal sealed class OrganizeStorageModule : HubModule
    {
        private const int MaxCachedFolders = 80;
        private const int MaxItemsSent = 300;
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(12);
        private static readonly string CacheFile = Path.Combine(HubPaths.CacheDir, "breakdowns.json");

        private readonly object gate = new object();
        private readonly ConcurrentDictionary<string, bool> running = new ConcurrentDictionary<string, bool>();
        private FolderBreakdownCache cache;

        public override void Start()
        {
            cache = Json.ReadFile<FolderBreakdownCache>(CacheFile) ?? new FolderBreakdownCache();
            cache.Items ??= new Dictionary<string, FolderBreakdown>();
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "organizeBreakdown":
                    RequestBreakdown(message.GetString("path"), message.TryGetValue("force", out object force) && force is bool forced && forced);
                    return true;
                case "organizeOpenFolder":
                    OpenFolder(message.GetString("path"));
                    return true;
                default:
                    return false;
            }
        }

        private void RequestBreakdown(string requested, bool force)
        {
            string path = Normalize(requested);
            if (path == null || !IsInsideGameFolder(path))
            {
                Hub.Post(new { type = "organizeBreakdown", path = requested, error = "This folder isn't part of a game in your library." });
                return;
            }
            if (!Directory.Exists(path))
            {
                Hub.Post(new { type = "organizeBreakdown", path = requested, error = "This folder no longer exists." });
                return;
            }

            string key = SizeService.Key(path);
            FolderBreakdown cached;
            lock (gate) cache.Items.TryGetValue(key, out cached);

            bool fresh = cached != null && DateTime.TryParse(cached.ScannedAt, out DateTime scannedAt) && DateTime.UtcNow - scannedAt.ToUniversalTime() < CacheLifetime;
            if (cached != null && fresh && !force)
            {
                PostBreakdown(requested, cached.Items, cached.Items.Count, cached.Items.Count, true, cached.ScannedAt);
                return;
            }

            if (!running.TryAdd(key, true)) return;
            _ = Task.Run(() =>
            {
                try
                {
                    Scan(requested, path, key);
                }
                catch (Exception error)
                {
                    Trace.WriteLine($"OrganizeStorageModule: {error}");
                    Hub.Post(new { type = "organizeBreakdown", path = requested, error = "Couldn't read this folder." });
                }
                finally
                {
                    running.TryRemove(key, out _);
                }
            });
        }

        private void Scan(string requested, string path, string key)
        {
            var items = new List<FolderBreakdownItem>();
            var looseFiles = new FolderBreakdownItem { Name = "Files in this folder", Path = path, IsFiles = true };

            foreach (var entry in ListEntries(path))
            {
                if (entry.IsDirectory)
                {
                    if (!entry.IsLink) items.Add(new FolderBreakdownItem { Name = entry.Name, Path = System.IO.Path.Combine(path, entry.Name), Pending = true });
                }
                else
                {
                    looseFiles.Bytes += entry.Bytes;
                    looseFiles.Files++;
                }
            }
            if (looseFiles.Files > 0) items.Add(looseFiles);

            int total = items.Count(item => item.Pending);
            int done = 0;
            var throttle = Stopwatch.StartNew();
            PostBreakdown(requested, items, done, total, false, null);

            foreach (FolderBreakdownItem item in items.Where(item => item.Pending).ToList())
            {
                Measure(item);
                item.Pending = false;
                done++;
                if (throttle.ElapsedMilliseconds >= 200)
                {
                    throttle.Restart();
                    PostBreakdown(requested, items, done, total, false, null);
                }
            }

            var result = new FolderBreakdown
            {
                Path = path,
                ScannedAt = DateTime.UtcNow.ToString("o"),
                Items = items.OrderByDescending(item => item.Bytes).ToList(),
            };

            lock (gate)
            {
                cache.Items[key] = result;
                if (cache.Items.Count > MaxCachedFolders)
                {
                    foreach (string stale in cache.Items.OrderBy(entry => entry.Value.ScannedAt).Take(cache.Items.Count - MaxCachedFolders).Select(entry => entry.Key).ToList())
                    {
                        cache.Items.Remove(stale);
                    }
                }
                try
                {
                    Json.WriteFile(CacheFile, cache);
                }
                catch (Exception error)
                {
                    Trace.WriteLine($"OrganizeStorageModule: {error.Message}");
                }
            }

            PostBreakdown(requested, result.Items, total, total, true, result.ScannedAt);
        }

        private void PostBreakdown(string path, List<FolderBreakdownItem> items, int done, int total, bool complete, string scannedAt)
        {
            List<FolderBreakdownItem> ordered;
            lock (items) ordered = items.OrderByDescending(item => item.Pending ? -1 : item.Bytes).ToList();

            var sent = ordered.Take(MaxItemsSent).ToList();
            var rest = ordered.Skip(MaxItemsSent).ToList();
            if (rest.Count > 0)
            {
                sent.Add(new FolderBreakdownItem
                {
                    Name = $"{rest.Count} smaller folders",
                    Path = path,
                    IsFiles = true,
                    Bytes = rest.Sum(item => item.Bytes),
                    Files = rest.Sum(item => item.Files),
                    Pending = rest.Any(item => item.Pending),
                });
            }

            Hub.Post(new
            {
                type = "organizeBreakdown",
                path,
                items = sent.Select(item => new { name = item.Name, path = item.Path, bytes = item.Bytes, files = item.Files, isFiles = item.IsFiles, pending = item.Pending }),
                done,
                total,
                complete,
                scannedAt,
            });
        }

        private void OpenFolder(string requested)
        {
            string path = Normalize(requested);
            if (path == null || !IsInsideGameFolder(path) || !Directory.Exists(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception error)
            {
                Hub.Toast("Couldn't open the folder: " + error.Message, "error");
            }
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                string full = System.IO.Path.GetFullPath(path);
                string root = System.IO.Path.GetPathRoot(full);
                return full.Length > root.Length ? full.TrimEnd('\\') : full;
            }
            catch
            {
                return null;
            }
        }

        private bool IsInsideGameFolder(string path)
        {
            LibraryDocument library = Hub.Library;
            if (library?.Games == null) return false;
            return library.Games
                .SelectMany(game => game.MeasuredPaths)
                .Select(Normalize)
                .Where(root => root != null && root.Length > 3)
                .Any(root => path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase));
        }

        private readonly struct DirectoryEntry
        {
            public DirectoryEntry(string name, bool isDirectory, bool isLink, long bytes)
            {
                Name = name;
                IsDirectory = isDirectory;
                IsLink = isLink;
                Bytes = bytes;
            }

            public string Name { get; }
            public bool IsDirectory { get; }
            public bool IsLink { get; }
            public long Bytes { get; }
        }

        private static IEnumerable<DirectoryEntry> ListEntries(string directory)
        {
            var entries = new List<DirectoryEntry>();
            IntPtr handle = NativeMethods.FindFirstFileExW(@"\\?\" + directory.TrimEnd('\\') + @"\*", NativeMethods.FindExInfoBasic, out NativeMethods.Win32FindData data, 0, IntPtr.Zero, NativeMethods.FindFirstExLargeFetch);
            if (handle == NativeMethods.InvalidHandle) return entries;

            try
            {
                do
                {
                    string name = data.FileName;
                    if (name == "." || name == "..") continue;
                    bool isDirectory = (data.FileAttributes & NativeMethods.FileAttributeDirectory) != 0;
                    bool isLink = (data.FileAttributes & NativeMethods.FileAttributeReparsePoint) != 0;
                    entries.Add(new DirectoryEntry(name, isDirectory, isLink, ((long)data.FileSizeHigh << 32) | data.FileSizeLow));
                }
                while (NativeMethods.FindNextFileW(handle, out data));
            }
            finally
            {
                NativeMethods.FindClose(handle);
            }
            return entries;
        }

        private static void Measure(FolderBreakdownItem item)
        {
            var pending = new Stack<string>();
            pending.Push(item.Path.TrimEnd('\\'));
            long bytes = 0;
            long files = 0;

            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                foreach (DirectoryEntry entry in ListEntries(directory))
                {
                    if (entry.IsDirectory)
                    {
                        if (!entry.IsLink) pending.Push(directory + "\\" + entry.Name);
                    }
                    else
                    {
                        bytes += entry.Bytes;
                        files++;
                    }
                }
            }

            item.Bytes = bytes;
            item.Files = files;
        }
    }

    internal sealed class OrganizeModule : HubModule
    {
        private const string FirstSeenKey = "organize.firstSeen";

        public override void Decorate(LibraryDocument library)
        {
            if (library?.Games == null || library.Games.Count == 0) return;

            var existing = Hub.Settings.Get(FirstSeenKey) as Dictionary<string, object>;
            bool baseline = existing == null;
            var firstSeen = existing != null ? new Dictionary<string, object>(existing) : new Dictionary<string, object>();
            string now = DateTime.UtcNow.ToString("o");
            bool changed = baseline;

            foreach (GameEntry game in library.Games)
            {
                if (string.IsNullOrEmpty(game.Id) || firstSeen.ContainsKey(game.Id)) continue;
                firstSeen[game.Id] = baseline ? game.InstalledAt ?? "" : now;
                changed = true;
            }

            if (changed) Hub.Settings.Set(FirstSeenKey, firstSeen);
        }
    }
}
