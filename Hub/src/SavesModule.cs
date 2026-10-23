using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class SavesModule : HubModule
    {
        private SavesService service;
        private IntPtr owner;
        private CancellationTokenSource backupAll;
        private readonly SemaphoreSlim workGate = new SemaphoreSlim(1, 1);

        public SavesService Service => service;

        public override void Start()
        {
            service = new SavesService(Hub.Settings);
            owner = Hub.Form.Handle;
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(45)).ConfigureAwait(false);
                if (await SavesManifest.EnsureFreshAsync().ConfigureAwait(false)) PostInfo();
            });
        }

        public override void Stop()
        {
            backupAll?.Cancel();
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "savesScan":
                    _ = ScanAsync(message.GetString("id"));
                    return true;
                case "savesBackup":
                    _ = BackupAsync(message.GetString("id"), message.GetString("reason"));
                    return true;
                case "savesRestore":
                    _ = RestoreAsync(message.GetString("id"), message.GetString("path"));
                    return true;
                case "savesOpenFolder":
                    OpenFolder(message.GetString("id"));
                    return true;
                case "savesReveal":
                    Reveal(message.GetString("path"));
                    return true;
                case "savesInfo":
                    PostInfo();
                    return true;
                case "savesPickFolder":
                    PickFolder(message.GetString("key"));
                    return true;
                case "savesRefreshManifest":
                    _ = RefreshManifestAsync();
                    return true;
                case "savesBackupAll":
                    _ = BackupAllAsync();
                    return true;
                case "savesCancelAll":
                    backupAll?.Cancel();
                    return true;
            }
            return false;
        }

        public async Task<SaveScan> ScanGameAsync(GameEntry game)
        {
            if (!SavesManifest.IsAvailable) await SavesManifest.EnsureFreshAsync().ConfigureAwait(false);
            List<GameEntry> library = Hub.Library?.Games ?? new List<GameEntry>();
            return await Task.Run(() => service.Scan(game, library)).ConfigureAwait(false);
        }

        public async Task<SaveBackup> BackupGameAsync(GameEntry game, string reason, SaveScan scan = null, CancellationToken token = default)
        {
            scan ??= await ScanGameAsync(game).ConfigureAwait(false);
            if (!scan.HasSaves) return null;
            await workGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                SaveBackup backup = await Task.Run(() => service.Backup(game, scan, reason, token), token).ConfigureAwait(false);
                foreach (string old in service.PruneCandidates(game)) await CleanupSafety.RecycleAsync(old, owner).ConfigureAwait(false);
                return backup;
            }
            finally
            {
                workGate.Release();
            }
        }

        private async Task ScanAsync(string id)
        {
            GameEntry game = Hub.Find(id);
            if (game == null) return;
            try
            {
                if (!SavesManifest.IsAvailable)
                {
                    Hub.Post(new { type = "savesScan", id, loading = true });
                    await SavesManifest.EnsureFreshAsync().ConfigureAwait(false);
                    PostInfo();
                }
                SaveScan scan = await ScanGameAsync(game).ConfigureAwait(false);
                PostScan(game, scan);
            }
            catch (Exception error)
            {
                Hub.Post(new { type = "savesScan", id, available = SavesManifest.IsAvailable, error = error.Message, locations = new object[0], registry = new object[0], backups = BackupsOf(game) });
            }
        }

        private void PostScan(GameEntry game, SaveScan scan)
        {
            Hub.Post(new
            {
                type = "savesScan",
                id = game.Id,
                available = SavesManifest.IsAvailable,
                manifestName = scan.ManifestName,
                locations = scan.Locations.Select(location => new { path = location.Path, isFolder = location.IsFolder, bytes = location.Bytes, files = location.Files, tooLarge = location.TooLarge }).ToList(),
                registry = scan.Registry.Select(key => @"HKEY_CURRENT_USER\" + key).ToList(),
                totalBytes = scan.TotalBytes,
                backups = BackupsOf(game),
                backupFolder = service.GameFolder(game),
                syncFolder = service.SyncRoot,
            });
        }

        private List<object> BackupsOf(GameEntry game)
        {
            try
            {
                return service.ListBackups(game).Select(backup => (object)new { path = backup.Path, name = backup.FileName, createdAt = backup.CreatedAt, bytes = backup.Bytes, reason = backup.Reason }).ToList();
            }
            catch
            {
                return new List<object>();
            }
        }

        private async Task BackupAsync(string id, string reason)
        {
            GameEntry game = Hub.Find(id);
            if (game == null) return;
            try
            {
                SaveScan scan = await ScanGameAsync(game).ConfigureAwait(false);
                SaveBackup backup = await BackupGameAsync(game, string.IsNullOrWhiteSpace(reason) ? "Manual backup" : reason, scan).ConfigureAwait(false);
                if (backup == null)
                {
                    Hub.Post(new { type = "savesBackupResult", id, ok = false, empty = true, message = $"No saves were found for {game.Name}." });
                }
                else
                {
                    string extra = backup.Skipped > 0 ? $" {backup.Skipped} locked {(backup.Skipped == 1 ? "file was" : "files were")} skipped." : "";
                    Hub.Post(new { type = "savesBackupResult", id, ok = true, path = backup.Path, synced = backup.Synced, message = $"Backed up {game.Name}'s saves.{extra}" });
                }
                PostScan(game, scan);
            }
            catch (Exception error)
            {
                Hub.Post(new { type = "savesBackupResult", id, ok = false, message = "Backup failed: " + error.Message });
            }
        }

        private async Task RestoreAsync(string id, string path)
        {
            GameEntry game = Hub.Find(id);
            if (game == null) return;
            try
            {
                if (!service.IsKnownBackup(path)) throw new InvalidOperationException("That file isn't one of Game Hub's save backups.");
                List<string> running = string.IsNullOrEmpty(game.InstallPath) ? new List<string>() : await Task.Run(() => CleanupSafety.RunningFrom(game.InstallPath)).ConfigureAwait(false);
                if (running.Count > 0) throw new InvalidOperationException($"Close {game.Name} first ({string.Join(", ", running)} is running).");

                SaveScan current = await ScanGameAsync(game).ConfigureAwait(false);
                SaveBackup safety = current.HasSaves ? await BackupGameAsync(game, "Before restore", current).ConfigureAwait(false) : null;
                string result;
                await workGate.WaitAsync().ConfigureAwait(false);
                try
                {
                    result = await Task.Run(() => service.Restore(path)).ConfigureAwait(false);
                }
                finally
                {
                    workGate.Release();
                }
                string note = safety != null ? " Your previous saves were backed up first." : "";
                Hub.Post(new { type = "savesRestoreResult", id, ok = true, message = result + note });
                PostScan(game, await ScanGameAsync(game).ConfigureAwait(false));
            }
            catch (Exception error)
            {
                Hub.Post(new { type = "savesRestoreResult", id, ok = false, message = "Restore failed: " + error.Message });
            }
        }

        private void OpenFolder(string id)
        {
            GameEntry game = string.IsNullOrEmpty(id) ? null : Hub.Find(id);
            string folder = game != null && Directory.Exists(service.GameFolder(game)) ? service.GameFolder(game) : service.BackupRoot;
            try
            {
                Directory.CreateDirectory(folder);
                CleanupSafety.Reveal(folder);
            }
            catch (Exception error)
            {
                Hub.Toast("Couldn't open the backup folder: " + error.Message, "error");
            }
        }

        private void Reveal(string path)
        {
            try
            {
                CleanupSafety.Reveal(path);
            }
            catch
            {
            }
        }

        private void PickFolder(string key)
        {
            if (key != SavesService.BackupFolderKey && key != SavesService.SyncFolderKey) return;
            Hub.RunOnUi(() =>
            {
                string start = key == SavesService.BackupFolderKey ? service.BackupRoot : service.SyncRoot ?? SavesService.OneDriveFolder;
                string chosen = CleanupNative.PickFolder(Hub.Form.Handle, key == SavesService.BackupFolderKey ? "Choose where save backups go" : "Choose a folder to sync backups to", start);
                if (string.IsNullOrEmpty(chosen)) return;
                if (CleanupSafety.IsDriveRoot(chosen))
                {
                    Hub.Toast("Choose a folder, not the root of a drive.", "error");
                    return;
                }
                if (key == SavesService.SyncFolderKey && CleanupSafety.SamePath(chosen, service.BackupRoot))
                {
                    Hub.Toast("The sync folder must be different from the backup folder.", "error");
                    return;
                }
                if (key == SavesService.SyncFolderKey && CleanupSafety.SamePath(chosen, SavesService.OneDriveFolder)) chosen = Path.Combine(chosen, "Game Hub Saves");
                Hub.Settings.Set(key, chosen);
                PostInfo();
            });
        }

        private async Task RefreshManifestAsync()
        {
            Hub.Post(new { type = "savesManifestLoading" });
            bool updated = await SavesManifest.EnsureFreshAsync(true).ConfigureAwait(false);
            PostInfo();
            Hub.Toast(updated ? "Save locations list updated." : SavesManifest.IsAvailable ? "Save locations list is already up to date." : "Couldn't download the save locations list.", SavesManifest.IsAvailable ? "info" : "error");
        }

        private void PostInfo()
        {
            if (service == null) return;
            Hub.Post(new
            {
                type = "savesInfo",
                backupFolder = service.BackupRoot,
                defaultBackupFolder = SavesService.DefaultBackupRoot,
                syncFolder = service.SyncRoot,
                oneDrive = SavesService.OneDriveFolder,
                keep = service.Keep,
                manifestAvailable = SavesManifest.IsAvailable,
                manifestUpdatedAt = SavesManifest.UpdatedAt?.ToString("o"),
                running = backupAll != null,
            });
        }

        private async Task BackupAllAsync()
        {
            if (backupAll != null) return;
            backupAll = new CancellationTokenSource();
            CancellationToken token = backupAll.Token;
            int done = 0, backedUp = 0, failed = 0;
            long bytes = 0;
            try
            {
                if (!SavesManifest.IsAvailable) await SavesManifest.EnsureFreshAsync().ConfigureAwait(false);
                List<GameEntry> games = (Hub.Library?.Games ?? new List<GameEntry>()).Where(game => game.Status == "installed").OrderBy(game => game.Name).ToList();
                foreach (GameEntry game in games)
                {
                    if (token.IsCancellationRequested) break;
                    Hub.Post(new { type = "savesBackupAllProgress", running = true, done, total = games.Count, name = game.Name, backedUp, failed });
                    try
                    {
                        SaveScan scan = await ScanGameAsync(game).ConfigureAwait(false);
                        if (scan.HasSaves)
                        {
                            SaveBackup backup = await BackupGameAsync(game, "Back up all", scan, token).ConfigureAwait(false);
                            if (backup != null)
                            {
                                backedUp++;
                                bytes += backup.Bytes;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        failed++;
                    }
                    done++;
                }
                Hub.Post(new { type = "savesBackupAllProgress", running = false, done, total = games.Count, backedUp, failed, bytes, cancelled = token.IsCancellationRequested });
            }
            finally
            {
                backupAll.Dispose();
                backupAll = null;
            }
        }
    }
}
