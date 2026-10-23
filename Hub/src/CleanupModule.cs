using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GameHub
{
    internal sealed class RemovedGame
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public string InstallPath { get; set; }
        public string Exe { get; set; }
        public string SteamAppId { get; set; }
        public List<string> Developers { get; set; } = new List<string>();
        public string At { get; set; }
    }

    internal sealed class CleanupHistory
    {
        public List<RemovedGame> Pending { get; set; } = new List<RemovedGame>();
    }

    internal sealed class CleanupItem
    {
        public string Path { get; set; }
        public string Kind { get; set; }
        public string Label { get; set; }
        public string Game { get; set; }
        public string GameId { get; set; }
        public bool IsFolder { get; set; }
        public long Bytes { get; set; }
        public string Modified { get; set; }
        public bool Selected { get; set; }
        public string Reason { get; set; }
        public bool Saves { get; set; }
    }

    internal sealed class RegistryLeftover
    {
        public string Key { get; set; }
        public int Values { get; set; }
        public int SubKeys { get; set; }
        public bool Selected { get; set; }
        public string Reason { get; set; }
    }

    internal sealed partial class CleanupModule : HubModule
    {
        private static readonly string HistoryFile = Path.Combine(HubPaths.CacheDir, "cleanup.json");
        private static readonly HashSet<string> InstallerExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".exe", ".msi", ".zip", ".rar", ".7z", ".iso", ".tar", ".gz", ".bin" };
        private static readonly string[] InstallerPrefixes = { "setup", "installer", "install", "gog", "fitgirl", "dodi", "repack" };
        private static readonly Regex HydraDownloadsPath = new Regex("\"downloadsPath\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)+)\"", RegexOptions.Compiled);

        private IntPtr owner;
        private readonly object historyGate = new object();
        private List<CleanupItem> lastInstallerScan = new List<CleanupItem>();
        private readonly Dictionary<string, (List<CleanupItem> Folders, List<RegistryLeftover> Registry, RemovedGame Game)> leftoverScans = new Dictionary<string, (List<CleanupItem>, List<RegistryLeftover>, RemovedGame)>();
        private bool scanning;

        public override void Start()
        {
            owner = Hub.Form.Handle;
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "cleanupScan":
                    _ = ScanInstallersAsync();
                    return true;
                case "cleanupRecycle":
                    _ = RecycleInstallersAsync(Strings(message, "paths"));
                    return true;
                case "cleanupReveal":
                    try
                    {
                        CleanupSafety.Reveal(message.GetString("path"));
                    }
                    catch
                    {
                    }
                    return true;
                case "cleanupTrackRemoval":
                    TrackRemoval(Hub.Find(message.GetString("id")), Strings(message, "developers"));
                    return true;
                case "leftoversScan":
                    _ = ScanLeftoversAsync(message);
                    return true;
                case "leftoversRecycle":
                    _ = RecycleLeftoversAsync(message.GetString("token"), Strings(message, "paths"), Strings(message, "registry"), message.TryGetValue("backup", out object backupSaves) && backupSaves is bool backupFlag && backupFlag);
                    return true;
                case "leftoversDismiss":
                    Dismiss(message.GetString("id"));
                    return true;
                case "verifyFiles":
                    Verify(message.GetString("id"));
                    return true;
                case "batchUninstallPlan":
                    SendBatchPlan(Strings(message, "ids"));
                    return true;
                case "batchUninstall":
                    _ = RunBatchAsync(Strings(message, "ids"), message.TryGetValue("backup", out object backup) && backup is bool flag && flag);
                    return true;
                case "batchSkip":
                    batchSkip = true;
                    return true;
                case "batchCancel":
                    batchCancel = true;
                    batchSkip = true;
                    return true;
            }
            return false;
        }

        public override void OnLibraryLoaded(LibraryDocument library)
        {
            List<RemovedGame> finished;
            lock (historyGate)
            {
                CleanupHistory history = LoadHistory();
                DateTime cutoff = DateTime.UtcNow.AddDays(-3);
                history.Pending.RemoveAll(record => !DateTime.TryParse(record.At, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime at) || at < cutoff);
                finished = history.Pending.Where(record => IsGone(record, library)).ToList();
                if (finished.Count == 0) return;
                history.Pending.RemoveAll(finished.Contains);
                SaveHistory(history);
            }
            foreach (RemovedGame record in finished) Hub.Post(new { type = "leftoversPrompt", game = record });
        }

        private static bool IsGone(RemovedGame record, LibraryDocument library)
        {
            bool stillInstalled = library.Games.Any(game => game.Id == record.Id && game.Status == "installed");
            if (stillInstalled) return false;
            bool folderGone = string.IsNullOrEmpty(record.InstallPath) || !Directory.Exists(record.InstallPath);
            bool exeGone = string.IsNullOrEmpty(record.Exe) || !File.Exists(record.Exe);
            return folderGone || exeGone;
        }

        private static List<string> Strings(Dictionary<string, object> message, string key) =>
            (message.TryGetValue(key, out object value) ? value as object[] : null)?.OfType<string>().ToList() ?? new List<string>();

        private static CleanupHistory LoadHistory() => Json.ReadFile<CleanupHistory>(HistoryFile) ?? new CleanupHistory();

        private static void SaveHistory(CleanupHistory history)
        {
            try
            {
                Json.WriteFile(HistoryFile, history);
            }
            catch
            {
            }
        }

        public static RemovedGame Snapshot(GameEntry game, IEnumerable<string> developers) => new RemovedGame
        {
            Id = game.Id,
            Name = game.Name,
            Source = game.Source,
            InstallPath = game.InstallPath,
            Exe = game.Exe,
            SteamAppId = game.SteamAppId,
            Developers = (developers ?? Enumerable.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToList(),
            At = DateTime.UtcNow.ToString("o"),
        };

        internal void TrackRemoval(GameEntry game, IEnumerable<string> developers)
        {
            if (game == null) return;
            lock (historyGate)
            {
                CleanupHistory history = LoadHistory();
                history.Pending.RemoveAll(record => record.Id == game.Id);
                history.Pending.Add(Snapshot(game, developers));
                SaveHistory(history);
            }
        }

        private void Dismiss(string id)
        {
            lock (historyGate)
            {
                CleanupHistory history = LoadHistory();
                if (history.Pending.RemoveAll(record => record.Id == id) > 0) SaveHistory(history);
            }
        }

        private void Verify(string id)
        {
            GameEntry game = Hub.Find(id);
            string url = VerifyUrl(game);
            if (url == null)
            {
                Hub.Toast("Only Steam and Epic games can be verified from here.", "error");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                Hub.Toast(game.Source == "Steam" ? $"Steam is checking {game.Name}'s files." : $"Epic Games is checking {game.Name}'s files.");
            }
            catch (Exception error)
            {
                Hub.Toast("Couldn't start the check: " + error.Message, "error");
            }
        }

        public static string VerifyUrl(GameEntry game)
        {
            if (game == null || game.Status != "installed") return null;
            if (game.Source == "Steam" && !string.IsNullOrEmpty(game.SteamAppId)) return "steam://validate/" + game.SteamAppId;
            if (game.Source == "Epic" && !string.IsNullOrEmpty(game.Url) && game.Url.StartsWith("com.epicgames.launcher://apps/", StringComparison.OrdinalIgnoreCase))
            {
                int query = game.Url.IndexOf('?');
                return (query < 0 ? game.Url : game.Url.Substring(0, query)) + "?action=verify";
            }
            return null;
        }

        private async Task ScanInstallersAsync()
        {
            if (scanning) return;
            scanning = true;
            Hub.Post(new { type = "cleanupScanning" });
            try
            {
                List<GameEntry> library = Hub.Library?.Games ?? new List<GameEntry>();
                var (items, roots) = await Task.Run(() => FindInstallers(library)).ConfigureAwait(false);
                lastInstallerScan = items;
                Hub.Post(new { type = "cleanupScan", items, hydraRoots = roots, downloads = CleanupSafety.Downloads, scannedAt = DateTime.UtcNow.ToString("o") });
            }
            catch (Exception error)
            {
                Hub.Post(new { type = "cleanupScan", items = new object[0], error = error.Message });
            }
            finally
            {
                scanning = false;
            }
        }

        private static (List<CleanupItem> Items, List<string> HydraRoots) FindInstallers(List<GameEntry> library)
        {
            var items = new List<CleanupItem>();
            var installed = library.Where(game => game.Status == "installed").ToList();

            foreach (GameEntry game in installed.Where(game => game.Source == "Hydra"))
            {
                foreach (GamePart part in game.Parts ?? new List<GamePart>())
                {
                    if (string.IsNullOrEmpty(part.Path) || !string.Equals(part.Label, "Downloaded installer", StringComparison.OrdinalIgnoreCase)) continue;
                    AddItem(items, library, game.Id, part.Path, "hydra", "Hydra download", game, true);
                }
            }

            var roots = HydraRoots(library);
            var nameIndex = installed
                .SelectMany(game => NameKeys(game).Select(key => (Key: key, Game: game)))
                .Where(entry => entry.Key.Length >= 4)
                .GroupBy(entry => entry.Key)
                .ToDictionary(group => group.Key, group => group.First().Game);

            foreach (string root in roots)
            {
                foreach (string file in SafeFiles(root))
                {
                    if (!InstallerExtensions.Contains(Path.GetExtension(file))) continue;
                    GameEntry owner = MatchInstaller(Path.GetFileNameWithoutExtension(file), nameIndex);
                    AddItem(items, library, null, file, "hydra", owner != null ? "Hydra download" : "Hydra download, no matching game", owner, owner != null);
                }
                foreach (string folder in SafeFolders(root))
                {
                    GameEntry owner = MatchInstaller(Path.GetFileName(folder), nameIndex, exactOnly: true);
                    if (owner == null) continue;
                    AddItem(items, library, null, folder, "hydra", "Hydra download", owner, false);
                }
            }

            foreach (string file in SafeFiles(CleanupSafety.Downloads))
            {
                if (!InstallerExtensions.Contains(Path.GetExtension(file))) continue;
                GameEntry owner = MatchInstaller(Path.GetFileNameWithoutExtension(file), nameIndex);
                if (owner == null) continue;
                AddItem(items, library, null, file, "downloads", Path.GetExtension(file).Equals(".exe", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).Equals(".msi", StringComparison.OrdinalIgnoreCase) ? "Installer" : "Archive", owner, true);
            }

            return (items.GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).OrderBy(item => item.Kind).ThenByDescending(item => item.Bytes).ToList(), roots);
        }

        private static void AddItem(List<CleanupItem> items, List<GameEntry> library, string ownerId, string path, string kind, string label, GameEntry game, bool selected)
        {
            string full = CleanupSafety.Full(path);
            if (full == null || items.Any(item => CleanupSafety.SamePath(item.Path, full))) return;
            string reason = CleanupSafety.Check(full, library, ownerId, true);
            if (reason != null && ownerId == null) return;
            bool isFolder = Directory.Exists(full);
            FolderMeasure measure = CleanupSafety.Measure(full);
            DateTime modified = isFolder ? Directory.GetLastWriteTimeUtc(full) : File.GetLastWriteTimeUtc(full);
            items.Add(new CleanupItem
            {
                Path = full,
                Kind = kind,
                Label = label,
                Game = game?.Name,
                GameId = game?.Id,
                IsFolder = isFolder,
                Bytes = measure.Bytes,
                Modified = modified.ToString("o"),
                Selected = selected && reason == null,
                Reason = reason,
            });
        }

        private static List<string> HydraRoots(List<GameEntry> library)
        {
            var roots = new List<string>();
            string database = Path.Combine(HubPaths.HydraDataDir, "hydra-db");
            if (Directory.Exists(database))
            {
                foreach (string file in SafeFiles(database).Where(file => file.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".ldb", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        string text = Encoding.UTF8.GetString(ReadShared(file));
                        foreach (Match match in HydraDownloadsPath.Matches(text))
                        {
                            string path = Regex.Unescape(match.Groups[1].Value);
                            if (Directory.Exists(path)) roots.Add(CleanupSafety.Full(path));
                        }
                    }
                    catch
                    {
                    }
                }
            }

            foreach (GameEntry game in library.Where(game => game.Source == "Hydra"))
            {
                foreach (GamePart part in game.Parts ?? new List<GamePart>())
                {
                    if (string.IsNullOrEmpty(part.Path) || part.Label?.IndexOf("Download", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string parent = Path.GetDirectoryName(CleanupSafety.Full(part.Path) ?? "");
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent)) roots.Add(CleanupSafety.Full(parent));
                }
            }

            return roots
                .Where(root => root != null && !CleanupSafety.IsDriveRoot(root))
                .Where(root => !CleanupSafety.SamePath(root, CleanupSafety.Downloads))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static byte[] ReadShared(string file)
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 64 * 1024 * 1024) return Array.Empty<byte>();
            var buffer = new byte[stream.Length];
            int read = 0;
            while (read < buffer.Length)
            {
                int chunk = stream.Read(buffer, read, buffer.Length - read);
                if (chunk <= 0) break;
                read += chunk;
            }
            return buffer;
        }

        private static IEnumerable<string> NameKeys(GameEntry game)
        {
            string name = SavesManifest.NormalizeName(game.Name);
            if (name.Length > 0) yield return name;
            foreach (string separator in new[] { ":", " - ", " – " })
            {
                int index = (game.Name ?? "").IndexOf(separator, StringComparison.Ordinal);
                if (index > 0)
                {
                    string head = SavesManifest.NormalizeName(game.Name.Substring(0, index));
                    if (head.Length >= 5) yield return head;
                }
            }
            string folder = CleanupSafety.Full(game.InstallPath);
            if (folder != null)
            {
                string leaf = SavesManifest.NormalizeName(Path.GetFileName(folder));
                if (leaf.Length >= 5 && !CleanupSafety.IsDeniedName(leaf)) yield return leaf;
            }
        }

        private static GameEntry MatchInstaller(string fileName, Dictionary<string, GameEntry> nameIndex, bool exactOnly = false)
        {
            string key = SavesManifest.NormalizeName(fileName);
            if (key.Length < 4) return null;
            var candidates = new List<string> { key };
            foreach (string prefix in InstallerPrefixes)
            {
                if (key.StartsWith(prefix) && key.Length > prefix.Length + 3) candidates.Add(key.Substring(prefix.Length));
            }

            foreach (string candidate in candidates)
            {
                if (nameIndex.TryGetValue(candidate, out GameEntry exact)) return exact;
            }
            if (exactOnly) return null;

            GameEntry best = null;
            int bestLength = 0;
            foreach (var entry in nameIndex)
            {
                if (entry.Key.Length < 5 || entry.Key.Length <= bestLength) continue;
                foreach (string candidate in candidates)
                {
                    if (!candidate.StartsWith(entry.Key)) continue;
                    string rest = candidate.Substring(entry.Key.Length);
                    if (rest.Length == 0 || char.IsDigit(rest[0]) || Regex.IsMatch(rest, "^(v|build|setup|win|x64|x86|repack|update|patch|gog|steam|rip|portable|installer|full|goty|deluxe|complete|edition|multi)"))
                    {
                        best = entry.Value;
                        bestLength = entry.Key.Length;
                        break;
                    }
                }
            }
            return best;
        }

        private static IEnumerable<string> SafeFiles(string folder)
        {
            try
            {
                return Directory.Exists(folder) ? Directory.GetFiles(folder) : Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> SafeFolders(string folder)
        {
            try
            {
                return Directory.Exists(folder) ? Directory.GetDirectories(folder) : Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private async Task RecycleInstallersAsync(List<string> paths)
        {
            List<GameEntry> library = Hub.Library?.Games ?? new List<GameEntry>();
            var allowed = lastInstallerScan.Where(item => item.Reason == null && paths.Any(path => CleanupSafety.SamePath(path, item.Path))).ToList();
            int moved = 0;
            long bytes = 0;
            var failed = new List<string>();
            foreach (CleanupItem item in allowed)
            {
                string reason = CleanupSafety.Check(item.Path, library, item.Kind == "hydra" && item.IsFolder ? item.GameId : null, true);
                if (reason != null || !await CleanupSafety.RecycleAsync(item.Path, owner).ConfigureAwait(false))
                {
                    failed.Add(Path.GetFileName(item.Path));
                    continue;
                }
                moved++;
                bytes += item.Bytes;
            }

            string message = failed.Count == 0
                ? $"Moved {moved} {(moved == 1 ? "item" : "items")} to the Recycle Bin, freeing about {FormatBytes(bytes)}."
                : $"Moved {moved} to the Recycle Bin. Not moved: {string.Join(", ", failed.Take(4))}{(failed.Count > 4 ? "…" : "")}";
            Hub.Post(new { type = "cleanupResult", ok = failed.Count == 0 && moved > 0, message, moved, bytes });
            if (allowed.Any(item => item.Kind == "hydra")) _ = Hub.RefreshLibraryAsync();
            await ScanInstallersAsync().ConfigureAwait(false);
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
        }
    }
}
