using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace GameHub
{
    internal sealed class SaveLocation
    {
        public string Path { get; set; }
        public bool IsFolder { get; set; }
        public long Bytes { get; set; }
        public int Files { get; set; }
        public bool TooLarge { get; set; }
    }

    internal sealed class SaveScan
    {
        public string GameId { get; set; }
        public string ManifestName { get; set; }
        public List<SaveLocation> Locations { get; set; } = new List<SaveLocation>();
        public List<string> Registry { get; set; } = new List<string>();
        public long TotalBytes => Locations.Where(location => !location.TooLarge).Sum(location => location.Bytes);
        public bool HasSaves => Locations.Any(location => !location.TooLarge) || Registry.Count > 0;
    }

    internal sealed class SaveBackup
    {
        public string Path { get; set; }
        public string FileName { get; set; }
        public string CreatedAt { get; set; }
        public long Bytes { get; set; }
        public string Reason { get; set; }
        public int Skipped { get; set; }
        public bool Synced { get; set; }
    }

    internal sealed class SaveBackupIndex
    {
        public string Game { get; set; }
        public string GameId { get; set; }
        public string CreatedAt { get; set; }
        public string Reason { get; set; }
        public string Manifest { get; set; }
        public List<SaveBackupItem> Items { get; set; } = new List<SaveBackupItem>();
        public List<string> Registry { get; set; } = new List<string>();
    }

    internal sealed class SaveBackupItem
    {
        public int Index { get; set; }
        public string Path { get; set; }
        public bool IsFolder { get; set; }
    }

    internal sealed class SavesService
    {
        public const string BackupFolderKey = "saves.backupFolder";
        public const string KeepKey = "saves.keep";
        public const string SyncFolderKey = "saves.syncFolder";
        public const string BeforeUninstallKey = "saves.backupBeforeUninstall";
        private const long MaxLocationBytes = 4L * 1024 * 1024 * 1024;
        private const int MaxLocationFiles = 20000;
        private static readonly Regex StampPattern = new Regex(@" \d{4}-\d{2}-\d{2} \d{2}-\d{2}-\d{2}( \(\d+\))?\.zip$", RegexOptions.IgnoreCase);

        private readonly SettingsStore settings;
        private readonly object gate = new object();
        private readonly Dictionary<string, ManifestEntry> entries = new Dictionary<string, ManifestEntry>();
        private readonly HashSet<string> lookedUp = new HashSet<string>();
        private DateTime manifestStamp;

        public SavesService(SettingsStore settings)
        {
            this.settings = settings;
        }

        public static string DefaultBackupRoot => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Game Hub Saves");

        public string BackupRoot
        {
            get
            {
                string configured = CleanupSafety.Full(settings.GetString(BackupFolderKey, ""));
                return string.IsNullOrEmpty(configured) || CleanupSafety.IsDriveRoot(configured) ? DefaultBackupRoot : configured;
            }
        }

        public string SyncRoot
        {
            get
            {
                string configured = CleanupSafety.Full(settings.GetString(SyncFolderKey, ""));
                return string.IsNullOrEmpty(configured) || CleanupSafety.IsDriveRoot(configured) ? null : configured;
            }
        }

        public int Keep => Math.Max(1, Math.Min(200, settings.GetInt(KeepKey, 10)));

        public static string OneDriveFolder
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("OneDrive") ?? Environment.GetEnvironmentVariable("OneDriveConsumer");
                return !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : null;
            }
        }

        public ManifestEntry EntryFor(GameEntry game, IEnumerable<GameEntry> library)
        {
            if (!SavesManifest.IsAvailable || game == null) return null;
            lock (gate)
            {
                DateTime stamp = SavesManifest.UpdatedAt ?? DateTime.MinValue;
                if (stamp != manifestStamp)
                {
                    entries.Clear();
                    lookedUp.Clear();
                    manifestStamp = stamp;
                }

                if (!lookedUp.Contains(game.Id))
                {
                    var pending = (library ?? Enumerable.Empty<GameEntry>()).Where(other => other != null && !lookedUp.Contains(other.Id)).ToList();
                    if (!pending.Contains(game)) pending.Add(game);
                    var queries = pending.Select(QueryFor).ToList();
                    foreach (var match in SavesManifest.Lookup(queries)) entries[match.Key] = match.Value;
                    foreach (GameEntry looked in pending) lookedUp.Add(looked.Id);
                }
                return entries.TryGetValue(game.Id, out ManifestEntry entry) ? entry : null;
            }
        }

        public static ManifestQuery QueryFor(GameEntry game) => new ManifestQuery
        {
            Key = game.Id,
            Name = game.Name,
            SteamAppId = game.Source == "Steam" || game.Source == "Hydra" ? game.SteamAppId : null,
        };

        public static string StoreOf(GameEntry game) => game.Source switch
        {
            "Steam" => "steam",
            "Epic" => "epic",
            _ => null,
        };

        public static SaveContext ContextFor(GameEntry game)
        {
            string install = CleanupSafety.Full(game.InstallPath);
            var context = new SaveContext
            {
                Base = install,
                GameFolder = install == null ? null : System.IO.Path.GetFileName(install),
                Store = StoreOf(game),
                StoreGameId = game.Source == "Steam" ? game.SteamAppId : null,
            };

            if (game.Source == "Steam")
            {
                if (!string.IsNullOrEmpty(HubPaths.SteamRoot)) context.Roots.Add(HubPaths.SteamRoot);
                int steamapps = install?.IndexOf(@"\steamapps\", StringComparison.OrdinalIgnoreCase) ?? -1;
                if (steamapps > 0) context.Roots.Add(install.Substring(0, steamapps));
            }
            else if (install != null)
            {
                string parent = System.IO.Path.GetDirectoryName(install);
                if (!string.IsNullOrEmpty(parent)) context.Roots.Add(parent);
            }
            return context;
        }

        public SaveScan Scan(GameEntry game, IReadOnlyList<GameEntry> library)
        {
            var scan = new SaveScan { GameId = game.Id };
            ManifestEntry entry = EntryFor(game, library);
            if (entry == null) return scan;
            scan.ManifestName = entry.Name;
            List<string> found = FindPaths(entry, ContextFor(game), game.InstallPath);
            scan.Locations = Measure(found);
            scan.Registry = FindRegistry(entry, StoreOf(game));
            return scan;
        }

        public List<string> FindPaths(ManifestEntry entry, SaveContext context, string installPath)
        {
            string install = CleanupSafety.Full(installPath);
            string backupRoot = BackupRoot;
            List<string> protectedRoots = CleanupSafety.ProtectedRoots();
            var matches = new List<string>();

            foreach (ManifestPath file in entry.Files.Where(file => file.AppliesTo(context.Store)))
            {
                foreach (string pattern in SavesPaths.Expand(file.Template, context))
                {
                    foreach (string path in SavesPaths.Find(pattern))
                    {
                        string full = CleanupSafety.Full(path);
                        if (full == null || CleanupSafety.IsProtected(full, protectedRoots)) continue;
                        if (install != null && (CleanupSafety.SamePath(full, install) || CleanupSafety.IsInside(install, full))) continue;
                        if (CleanupSafety.IsSameOrInside(full, backupRoot)) continue;
                        if (SyncRoot != null && CleanupSafety.IsSameOrInside(full, SyncRoot)) continue;
                        if (!matches.Contains(full, StringComparer.OrdinalIgnoreCase)) matches.Add(full);
                    }
                }
            }

            var accepted = new List<string>();
            foreach (string path in matches.OrderBy(path => path.Length))
            {
                if (accepted.Any(parent => CleanupSafety.IsInside(path, parent))) continue;
                accepted.Add(path);
            }
            return accepted;
        }

        private static List<SaveLocation> Measure(IEnumerable<string> paths)
        {
            var locations = new List<SaveLocation>();
            foreach (string path in paths)
            {
                bool isFolder = Directory.Exists(path);
                FolderMeasure measure = CleanupSafety.Measure(path, MaxLocationFiles);
                locations.Add(new SaveLocation
                {
                    Path = path,
                    IsFolder = isFolder,
                    Bytes = measure.Bytes,
                    Files = measure.Files,
                    TooLarge = measure.Truncated || measure.Bytes > MaxLocationBytes,
                });
            }
            return locations;
        }

        public static List<string> FindRegistry(ManifestEntry entry, string store)
        {
            var keys = new List<string>();
            foreach (ManifestPath registry in entry.Registry.Where(registry => registry.AppliesTo(store)))
            {
                string key = HkcuSubKey(registry.Template);
                if (key == null || keys.Contains(key, StringComparer.OrdinalIgnoreCase)) continue;
                try
                {
                    using RegistryKey opened = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key);
                    if (opened != null) keys.Add(key);
                }
                catch
                {
                }
            }
            return keys;
        }

        public static string HkcuSubKey(string template)
        {
            if (string.IsNullOrEmpty(template) || template.IndexOfAny(new[] { '<', '*', '?', '[' }) >= 0) return null;
            string path = template.Replace('/', '\\').Trim('\\');
            foreach (string prefix in new[] { @"HKEY_CURRENT_USER\", @"HKCU\" })
            {
                if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string sub = path.Substring(prefix.Length).Trim('\\');
                string[] parts = sub.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !string.Equals(parts[0], "Software", StringComparison.OrdinalIgnoreCase)) return null;
                if (CleanupSafety.IsDeniedName(SavesManifest.NormalizeName(parts[1])) && parts.Length < 3) return null;
                if (string.Equals(parts[1], "Microsoft", StringComparison.OrdinalIgnoreCase) || string.Equals(parts[1], "Classes", StringComparison.OrdinalIgnoreCase) || string.Equals(parts[1], "Policies", StringComparison.OrdinalIgnoreCase) || string.Equals(parts[1], "Wow6432Node", StringComparison.OrdinalIgnoreCase)) return null;
                return string.Join("\\", parts);
            }
            return null;
        }

        public string GameFolder(GameEntry game) => System.IO.Path.Combine(BackupRoot, CleanupSafety.SafeFileName(game.Name));

        public SaveBackup Backup(GameEntry game, SaveScan scan, string reason, CancellationToken token = default)
        {
            var included = scan.Locations.Where(location => !location.TooLarge).ToList();
            if (included.Count == 0 && scan.Registry.Count == 0) throw new InvalidOperationException("No saves were found for this game.");

            string folder = GameFolder(game);
            Directory.CreateDirectory(folder);
            string safeName = CleanupSafety.SafeFileName(game.Name);
            DateTime now = DateTime.Now;
            string file = System.IO.Path.Combine(folder, $"{safeName} {now:yyyy-MM-dd HH-mm-ss}.zip");
            for (int copy = 2; File.Exists(file); copy++) file = System.IO.Path.Combine(folder, $"{safeName} {now:yyyy-MM-dd HH-mm-ss} ({copy}).zip");
            string temporary = file + ".partial";

            var index = new SaveBackupIndex
            {
                Game = game.Name,
                GameId = game.Id,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                Reason = reason ?? "Manual backup",
                Manifest = scan.ManifestName,
            };
            int skipped = 0;

            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
                {
                    for (int position = 0; position < included.Count; position++)
                    {
                        token.ThrowIfCancellationRequested();
                        SaveLocation location = included[position];
                        index.Items.Add(new SaveBackupItem { Index = position, Path = location.Path, IsFolder = location.IsFolder });
                        if (location.IsFolder)
                        {
                            foreach (string source in EnumerateFiles(location.Path))
                            {
                                token.ThrowIfCancellationRequested();
                                string relative = source.Substring(location.Path.TrimEnd('\\').Length + 1).Replace('\\', '/');
                                if (!AddFile(zip, source, $"files/{position}/{relative}")) skipped++;
                            }
                        }
                        else if (!AddFile(zip, location.Path, $"files/{position}/{System.IO.Path.GetFileName(location.Path)}"))
                        {
                            skipped++;
                        }
                    }

                    for (int position = 0; position < scan.Registry.Count; position++)
                    {
                        string exported = ExportRegistry(scan.Registry[position]);
                        if (exported == null)
                        {
                            skipped++;
                            continue;
                        }
                        try
                        {
                            zip.CreateEntryFromFile(exported, $"registry/{position}.reg");
                            index.Registry.Add(scan.Registry[position]);
                        }
                        finally
                        {
                            File.Delete(exported);
                        }
                    }

                    ZipArchiveEntry indexEntry = zip.CreateEntry("backup.json");
                    using var writer = new StreamWriter(indexEntry.Open(), new UTF8Encoding(false));
                    writer.Write(Json.Serialize(index));
                }
                File.Move(temporary, file);
            }
            catch
            {
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch
                {
                }
                throw;
            }

            var backup = new SaveBackup
            {
                Path = file,
                FileName = System.IO.Path.GetFileName(file),
                CreatedAt = index.CreatedAt,
                Bytes = new FileInfo(file).Length,
                Reason = index.Reason,
                Skipped = skipped,
            };
            backup.Synced = CopyToSync(game, file);
            return backup;
        }

        private static IEnumerable<string> EnumerateFiles(string folder)
        {
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string[] files;
                string[] folders;
                try
                {
                    files = Directory.GetFiles(current);
                    folders = Directory.GetDirectories(current);
                }
                catch
                {
                    continue;
                }
                foreach (string file in files) yield return file;
                foreach (string child in folders)
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    }
                    catch
                    {
                        continue;
                    }
                    pending.Push(child);
                }
            }
        }

        private static bool AddFile(ZipArchive zip, string source, string entryName)
        {
            try
            {
                using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                ZipArchiveEntry entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
                entry.LastWriteTime = File.GetLastWriteTime(source);
                using Stream output = entry.Open();
                input.CopyTo(output);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string ExportRegistry(string subKey)
        {
            string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hubreg_" + Guid.NewGuid().ToString("N") + ".reg");
            return RunReg($"export \"HKCU\\{subKey}\" \"{file}\" /y") && File.Exists(file) && new FileInfo(file).Length > 0 ? file : null;
        }

        public static bool RunReg(string arguments)
        {
            try
            {
                var start = new ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory, "reg.exe"), arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                using Process process = Process.Start(start);
                if (!process.WaitForExit(30000)) return false;
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private bool CopyToSync(GameEntry game, string file)
        {
            string sync = SyncRoot;
            if (sync == null) return false;
            try
            {
                string target = System.IO.Path.Combine(sync, CleanupSafety.SafeFileName(game.Name));
                Directory.CreateDirectory(target);
                string destination = System.IO.Path.Combine(target, System.IO.Path.GetFileName(file));
                if (!File.Exists(destination)) File.Copy(file, destination);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<string> PruneCandidates(GameEntry game)
        {
            var candidates = new List<string>();
            string safeName = CleanupSafety.SafeFileName(game.Name);
            var roots = new List<string> { BackupRoot };
            if (SyncRoot != null) roots.Add(SyncRoot);
            foreach (string root in roots)
            {
                string folder = System.IO.Path.Combine(root, safeName);
                if (!Directory.Exists(folder)) continue;
                var zips = Directory.GetFiles(folder, "*.zip")
                    .Where(file => IsBackupName(System.IO.Path.GetFileName(file), safeName))
                    .OrderByDescending(file => System.IO.Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                candidates.AddRange(zips.Skip(Keep));
            }
            return candidates;
        }

        private static bool IsBackupName(string fileName, string safeName) =>
            fileName.StartsWith(safeName + " ", StringComparison.OrdinalIgnoreCase) && StampPattern.IsMatch(fileName.Substring(safeName.Length));

        public List<SaveBackup> ListBackups(GameEntry game)
        {
            var backups = new List<SaveBackup>();
            string folder = GameFolder(game);
            if (!Directory.Exists(folder)) return backups;
            string safeName = CleanupSafety.SafeFileName(game.Name);
            foreach (string file in Directory.GetFiles(folder, "*.zip").Where(file => IsBackupName(System.IO.Path.GetFileName(file), safeName)))
            {
                var info = new FileInfo(file);
                SaveBackupIndex index = ReadIndex(file);
                backups.Add(new SaveBackup
                {
                    Path = file,
                    FileName = info.Name,
                    CreatedAt = index?.CreatedAt ?? info.LastWriteTimeUtc.ToString("o"),
                    Bytes = info.Length,
                    Reason = index?.Reason ?? "Backup",
                });
            }
            return backups.OrderByDescending(backup => backup.FileName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static SaveBackupIndex ReadIndex(string file)
        {
            try
            {
                using ZipArchive zip = ZipFile.OpenRead(file);
                ZipArchiveEntry entry = zip.GetEntry("backup.json");
                if (entry == null) return null;
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                return Json.Deserialize<SaveBackupIndex>(reader.ReadToEnd());
            }
            catch
            {
                return null;
            }
        }

        public bool IsKnownBackup(string path)
        {
            string full = CleanupSafety.Full(path);
            if (full == null || !full.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return false;
            return CleanupSafety.IsInside(full, BackupRoot) || (SyncRoot != null && CleanupSafety.IsInside(full, SyncRoot));
        }

        public string Restore(string zipPath)
        {
            SaveBackupIndex index = ReadIndex(zipPath) ?? throw new InvalidOperationException("This backup has no index, so Game Hub can't tell where its files belong.");
            List<string> protectedRoots = CleanupSafety.ProtectedRoots();
            var targets = new Dictionary<int, SaveBackupItem>();
            foreach (SaveBackupItem item in index.Items)
            {
                string full = CleanupSafety.Full(item.Path);
                if (full == null || CleanupSafety.IsProtected(full, protectedRoots)) throw new InvalidOperationException("The backup points to a protected folder: " + item.Path);
                targets[item.Index] = new SaveBackupItem { Index = item.Index, Path = full, IsFolder = item.IsFolder };
            }

            int restored = 0;
            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (!entry.FullName.StartsWith("files/", StringComparison.Ordinal) || entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
                    string[] parts = entry.FullName.Split(new[] { '/' }, 3);
                    if (parts.Length < 3 || !int.TryParse(parts[1], out int number) || !targets.TryGetValue(number, out SaveBackupItem target)) continue;

                    string destination = target.IsFolder
                        ? System.IO.Path.GetFullPath(System.IO.Path.Combine(target.Path, parts[2].Replace('/', '\\')))
                        : target.Path;
                    if (target.IsFolder && !CleanupSafety.IsInside(destination, target.Path)) continue;

                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                    if (File.Exists(destination))
                    {
                        FileAttributes attributes = File.GetAttributes(destination);
                        if ((attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(destination, attributes & ~FileAttributes.ReadOnly);
                    }
                    entry.ExtractToFile(destination, true);
                    restored++;
                }

                foreach (ZipArchiveEntry entry in zip.Entries.Where(entry => entry.FullName.StartsWith("registry/", StringComparison.Ordinal) && entry.FullName.EndsWith(".reg", StringComparison.OrdinalIgnoreCase)))
                {
                    string temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hubreg_" + Guid.NewGuid().ToString("N") + ".reg");
                    try
                    {
                        entry.ExtractToFile(temporary, true);
                        if (IsCurrentUserOnly(temporary) && RunReg($"import \"{temporary}\"")) restored++;
                    }
                    finally
                    {
                        if (File.Exists(temporary)) File.Delete(temporary);
                    }
                }
            }
            return $"Restored {restored} {(restored == 1 ? "item" : "items")}.";
        }

        private static bool IsCurrentUserOnly(string regFile)
        {
            string text = File.ReadAllText(regFile);
            var headers = text.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("[")).ToList();
            return headers.Count > 0 && headers.All(line => line.StartsWith(@"[HKEY_CURRENT_USER\Software\", StringComparison.OrdinalIgnoreCase));
        }
    }
}
