using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GameHub
{
    internal sealed partial class CleanupModule
    {
        private static readonly HashSet<string> SkippedRegistryVendors = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Microsoft", "Classes", "Policies", "Wow6432Node", "Clients", "RegisteredApplications", "Valve", "Epic Games", "Google", "Mozilla", "Intel", "NVIDIA Corporation", "AMD", "ATI", "Windows" };

        private static List<string> LeftoverRoots() => new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            CleanupSafety.LocalLow,
            CleanupSafety.MyGames,
            CleanupSafety.SavedGames,
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        private RemovedGame RecordFrom(Dictionary<string, object> message)
        {
            List<string> developers = Strings(message, "developers");
            GameEntry game = Hub.Find(message.GetString("id"));
            if (game != null) return Snapshot(game, developers);
            if (!(message.TryGetValue("game", out object raw) && raw is Dictionary<string, object> data)) return null;
            var record = new RemovedGame
            {
                Id = data.GetString("Id"),
                Name = data.GetString("Name"),
                Source = data.GetString("Source"),
                InstallPath = data.GetString("InstallPath"),
                Exe = data.GetString("Exe"),
                SteamAppId = data.GetString("SteamAppId"),
                Developers = developers,
            };
            if (data.TryGetValue("Developers", out object listed) && listed is object[] names) record.Developers.AddRange(names.OfType<string>());
            record.Developers = record.Developers.Distinct().ToList();
            return string.IsNullOrWhiteSpace(record.Name) ? null : record;
        }

        private static GameEntry EntryOf(RemovedGame record) => new GameEntry
        {
            Id = record.Id ?? "removed:" + record.Name,
            Name = record.Name,
            Source = record.Source,
            InstallPath = record.InstallPath,
            Exe = record.Exe,
            SteamAppId = record.SteamAppId,
            Status = "removed",
        };

        private async Task ScanLeftoversAsync(Dictionary<string, object> message)
        {
            RemovedGame record = RecordFrom(message);
            if (record == null) return;
            string requestId = message.GetString("requestId") ?? record.Id;
            try
            {
                List<GameEntry> library = Hub.Library?.Games ?? new List<GameEntry>();
                SavesModule saves = Hub.Module<SavesModule>();
                var (folders, registry) = await Task.Run(() => FindLeftovers(record, library, saves?.Service)).ConfigureAwait(false);
                string token = Guid.NewGuid().ToString("N");
                lock (leftoverScans)
                {
                    leftoverScans.Clear();
                    leftoverScans[token] = (folders, registry, record);
                }
                Hub.Post(new { type = "leftoversScan", requestId, token, game = record, folders, registry });
            }
            catch (Exception error)
            {
                Hub.Post(new { type = "leftoversScan", requestId, game = record, error = error.Message, folders = new object[0], registry = new object[0] });
            }
        }

        private static (List<CleanupItem> Folders, List<RegistryLeftover> Registry) FindLeftovers(RemovedGame record, List<GameEntry> library, SavesService saves)
        {
            GameEntry game = EntryOf(record);
            var others = library.Where(other => other.Id != record.Id && other.Status == "installed").ToList();
            var otherKeys = new HashSet<string>(others.SelectMany(NameKeys));
            var gameKeys = new HashSet<string>(NameKeys(game).Where(key => key.Length >= 4 && !otherKeys.Contains(key) && !CleanupSafety.IsDeniedName(key)));
            var developerKeys = new HashSet<string>(record.Developers.Select(SavesManifest.NormalizeName).Where(key => key.Length >= 3 && !CleanupSafety.IsDeniedName(key) && !otherKeys.Contains(key)));

            string backupRoot = saves?.BackupRoot ?? SavesService.DefaultBackupRoot;
            string syncRoot = saves?.SyncRoot;
            var folders = new List<CleanupItem>();

            void Add(string path, string label, bool selected, bool hasSaves)
            {
                string full = CleanupSafety.Full(path);
                if (full == null) return;
                if (CleanupSafety.IsSameOrInside(full, backupRoot) || (syncRoot != null && CleanupSafety.IsSameOrInside(full, syncRoot))) return;
                if (CleanupSafety.IsSameOrInside(full, HubPaths.SyncDir)) return;
                CleanupItem existing = folders.FirstOrDefault(item => CleanupSafety.IsSameOrInside(full, item.Path));
                if (existing != null)
                {
                    existing.Saves |= hasSaves;
                    if (hasSaves) existing.Selected = false;
                    return;
                }
                folders.RemoveAll(item => CleanupSafety.IsInside(item.Path, full));
                string reason = CleanupSafety.Check(full, library, record.Id, true, backupRoot);
                bool isFolder = Directory.Exists(full);
                FolderMeasure measure = CleanupSafety.Measure(full);
                folders.Add(new CleanupItem
                {
                    Path = full,
                    Kind = "leftover",
                    Label = label,
                    Game = record.Name,
                    GameId = record.Id,
                    IsFolder = isFolder,
                    Bytes = measure.Bytes,
                    Modified = (isFolder ? Directory.GetLastWriteTimeUtc(full) : File.GetLastWriteTimeUtc(full)).ToString("o"),
                    Selected = selected && !hasSaves && reason == null,
                    Reason = reason ?? (measure.HasLinks ? "This folder contains links to other locations." : null),
                    Saves = hasSaves,
                });
            }

            List<string> roots = LeftoverRoots();
            if (gameKeys.Count > 0)
            {
                foreach (string root in roots)
                {
                    foreach (string folder in SafeFolders(root))
                    {
                        string key = SavesManifest.NormalizeName(Path.GetFileName(folder));
                        if (gameKeys.Contains(key))
                        {
                            Add(folder, "Named after the game", true, false);
                            continue;
                        }
                        if (CleanupSafety.IsDeniedName(key) && !developerKeys.Contains(key) && key != "mygames" && key != "savedgames") continue;

                        bool developer = developerKeys.Contains(key);
                        string[] children = SafeFolders(folder).ToArray();
                        var matching = children.Where(child => gameKeys.Contains(SavesManifest.NormalizeName(Path.GetFileName(child)))).ToList();
                        if (matching.Count == 0) continue;
                        bool onlyThisGame = developer && children.Length == 1 && !SafeFiles(folder).Any();
                        if (onlyThisGame)
                        {
                            Add(folder, "Developer folder that only holds this game", true, false);
                        }
                        else
                        {
                            foreach (string child in matching) Add(child, developer ? "Game folder in the developer's folder" : "Named after the game", developer, false);
                        }
                    }
                }
            }

            var registry = new List<RegistryLeftover>();
            if (SavesManifest.IsAvailable && saves != null)
            {
                ManifestEntry entry = SavesManifest.Lookup(new[] { SavesService.QueryFor(game) }).Values.FirstOrDefault();
                if (entry != null)
                {
                    foreach (string path in saves.FindPaths(entry, SavesService.ContextFor(game), game.InstallPath))
                    {
                        string root = roots.FirstOrDefault(candidate => CleanupSafety.IsInside(path, candidate));
                        if (root == null) continue;
                        string target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
                        if (target == null || CleanupSafety.SamePath(target, root) || roots.Any(candidate => CleanupSafety.SamePath(candidate, target))) target = File.Exists(path) ? path : null;
                        if (target == null) continue;
                        Add(target, "Save data", false, true);
                    }
                    foreach (string key in SavesService.FindRegistry(entry, null))
                    {
                        if (key.Split('\\').Length >= 3) AddRegistry(registry, key, "Listed as this game's settings");
                    }
                }
            }

            if (gameKeys.Count > 0)
            {
                try
                {
                    using RegistryKey software = Registry.CurrentUser.OpenSubKey("Software");
                    foreach (string vendor in software?.GetSubKeyNames() ?? Array.Empty<string>())
                    {
                        if (SkippedRegistryVendors.Contains(vendor)) continue;
                        using RegistryKey vendorKey = software.OpenSubKey(vendor);
                        foreach (string name in vendorKey?.GetSubKeyNames() ?? Array.Empty<string>())
                        {
                            if (gameKeys.Contains(SavesManifest.NormalizeName(name))) AddRegistry(registry, $@"Software\{vendor}\{name}", "Named after the game");
                        }
                    }
                }
                catch
                {
                }
            }

            return (folders.OrderByDescending(item => item.Bytes).ToList(), registry);
        }

        private static void AddRegistry(List<RegistryLeftover> registry, string key, string reason)
        {
            if (!IsSafeRegistryKey(key) || registry.Any(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))) return;
            try
            {
                using RegistryKey opened = Registry.CurrentUser.OpenSubKey(key);
                if (opened == null) return;
                registry.Add(new RegistryLeftover { Key = key, Values = opened.ValueCount, SubKeys = opened.SubKeyCount, Selected = false, Reason = reason });
            }
            catch
            {
            }
        }

        private static bool IsSafeRegistryKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.IndexOfAny(new[] { '*', '?', '<', '>' }) >= 0) return false;
            string[] parts = key.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || !string.Equals(parts[0], "Software", StringComparison.OrdinalIgnoreCase)) return false;
            if (SkippedRegistryVendors.Contains(parts[1])) return false;
            return SavesService.HkcuSubKey(@"HKEY_CURRENT_USER\" + key) != null;
        }

        private async Task RecycleLeftoversAsync(string token, List<string> paths, List<string> keys, bool backup)
        {
            (List<CleanupItem> Folders, List<RegistryLeftover> Registry, RemovedGame Game) scan;
            lock (leftoverScans)
            {
                if (token == null || !leftoverScans.TryGetValue(token, out scan))
                {
                    Hub.Post(new { type = "leftoversResult", ok = false, message = "This list is out of date. Scan again." });
                    return;
                }
                leftoverScans.Remove(token);
            }

            List<GameEntry> library = Hub.Library?.Games ?? new List<GameEntry>();
            SavesModule saves = Hub.Module<SavesModule>();
            string backupRoot = saves?.Service.BackupRoot ?? SavesService.DefaultBackupRoot;
            var folders = scan.Folders.Where(item => item.Reason == null && paths.Any(path => CleanupSafety.SamePath(path, item.Path))).ToList();
            var registry = scan.Registry.Where(item => keys.Any(key => string.Equals(key, item.Key, StringComparison.OrdinalIgnoreCase))).ToList();
            var failed = new List<string>();
            int moved = 0;
            long bytes = 0;
            string note = "";

            try
            {
                var savesToKeep = folders.Where(item => item.Saves).ToList();
                if (backup && saves != null && (savesToKeep.Count > 0 || registry.Count > 0))
                {
                    var saveScan = new SaveScan { GameId = scan.Game.Id };
                    foreach (CleanupItem item in savesToKeep) saveScan.Locations.Add(new SaveLocation { Path = item.Path, IsFolder = item.IsFolder, Bytes = item.Bytes });
                    saveScan.Registry.AddRange(registry.Select(item => item.Key));
                    SaveBackup saved = await Task.Run(() => saves.Service.Backup(EntryOf(scan.Game), saveScan, "Before leftover cleanup")).ConfigureAwait(false);
                    note = $" Saves were backed up to {Path.GetFileName(saved.Path)}.";
                }
            }
            catch (Exception error)
            {
                Hub.Post(new { type = "leftoversResult", ok = false, message = "The save backup failed, so nothing was removed: " + error.Message });
                return;
            }

            foreach (CleanupItem item in folders)
            {
                string reason = CleanupSafety.Check(item.Path, library, scan.Game.Id, true, backupRoot);
                if (reason != null || !await CleanupSafety.RecycleAsync(item.Path, owner).ConfigureAwait(false))
                {
                    failed.Add(Path.GetFileName(item.Path));
                    continue;
                }
                moved++;
                bytes += item.Bytes;
            }

            string exportFolder = Path.Combine(backupRoot, "Registry backups");
            int removedKeys = 0;
            foreach (RegistryLeftover item in registry)
            {
                if (!IsSafeRegistryKey(item.Key))
                {
                    failed.Add(item.Key);
                    continue;
                }
                try
                {
                    Directory.CreateDirectory(exportFolder);
                    string file = Path.Combine(exportFolder, $"{CleanupSafety.SafeFileName(scan.Game.Name)} {DateTime.Now:yyyy-MM-dd HH-mm-ss} {CleanupSafety.SafeFileName(item.Key.Split('\\').Last())}.reg");
                    string exported = SavesService.ExportRegistry(item.Key);
                    if (exported == null) throw new InvalidOperationException("export failed");
                    File.Move(exported, file);
                    if (!File.Exists(file) || new FileInfo(file).Length == 0) throw new InvalidOperationException("export failed");
                    Registry.CurrentUser.DeleteSubKeyTree(item.Key, false);
                    removedKeys++;
                }
                catch
                {
                    failed.Add(item.Key.Split('\\').Last());
                }
            }

            string summary = moved > 0 ? $"Moved {moved} {(moved == 1 ? "folder" : "folders")} ({FormatBytes(bytes)}) to the Recycle Bin." : "";
            if (removedKeys > 0) summary += $" Removed {removedKeys} registry {(removedKeys == 1 ? "key" : "keys")} after saving a .reg backup.";
            if (failed.Count > 0) summary += $" Not removed: {string.Join(", ", failed.Take(4))}{(failed.Count > 4 ? "…" : "")}.";
            if (summary.Length == 0) summary = "Nothing was removed.";
            Hub.Post(new { type = "leftoversResult", ok = failed.Count == 0 && (moved > 0 || removedKeys > 0), message = summary.Trim() + note, registryBackupFolder = removedKeys > 0 ? exportFolder : null });
        }
    }
}
