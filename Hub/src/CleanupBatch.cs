using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed partial class CleanupModule
    {
        private volatile bool batchSkip;
        private volatile bool batchCancel;
        private bool batchRunning;

        private void SendBatchPlan(List<string> ids)
        {
            List<GameEntry> library = Hub.Library?.Games ?? new List<GameEntry>();
            var items = new List<object>();
            foreach (string id in ids.Distinct())
            {
                GameEntry game = Hub.Find(id);
                if (game == null) continue;
                UninstallPlan plan;
                try
                {
                    plan = UninstallService.BuildPlan(game, library);
                }
                catch (Exception error)
                {
                    plan = new UninstallPlan { GameId = id, Method = "none", Title = "Can't uninstall from here", Blocked = error.Message };
                }
                items.Add(new
                {
                    id,
                    name = game.Name,
                    source = game.Source,
                    method = plan.Method,
                    title = plan.Title,
                    blocked = plan.Blocked,
                    warning = plan.Warning,
                    launcher = plan.LauncherName,
                    folders = plan.Folders.Where(folder => folder.Allowed && folder.Selected).Select(folder => new { path = folder.Path, label = folder.Label }).ToList()
                });
            }
            Hub.Post(new { type = "batchUninstallPlan", items, running = batchRunning });
        }

        private async Task RunBatchAsync(List<string> ids, bool backup)
        {
            if (batchRunning)
            {
                Hub.Post(new { type = "batchUninstallDone", ok = false, message = "A batch uninstall is already running.", results = new object[0] });
                return;
            }
            batchRunning = true;
            batchCancel = false;
            var results = new List<object>();
            var openedLaunchers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool removedAny = false;
            SavesModule saves = Hub.Module<SavesModule>();

            try
            {
                var games = ids.Distinct().Select(Hub.Find).Where(game => game != null).ToList();
                for (int index = 0; index < games.Count; index++)
                {
                    GameEntry game = games[index];
                    batchSkip = false;
                    if (batchCancel)
                    {
                        results.Add(new { id = game.Id, name = game.Name, status = "cancelled", message = "Not started." });
                        continue;
                    }

                    void Progress(string status, string message) => Hub.Post(new { type = "batchUninstallProgress", index, total = games.Count, id = game.Id, name = game.Name, status, message });

                    List<GameEntry> library = Hub.Library?.Games ?? new List<GameEntry>();
                    UninstallPlan plan = UninstallService.BuildPlan(game, library);
                    if (plan.Method == "none")
                    {
                        string reason = plan.Blocked ?? "Game Hub can't remove this game.";
                        Progress("blocked", reason);
                        results.Add(new { id = game.Id, name = game.Name, status = "blocked", message = reason });
                        continue;
                    }

                    if (!string.IsNullOrEmpty(game.InstallPath) && Directory.Exists(game.InstallPath))
                    {
                        List<string> running = await Task.Run(() => CleanupSafety.RunningFrom(game.InstallPath)).ConfigureAwait(false);
                        if (running.Count > 0)
                        {
                            string reason = $"{string.Join(", ", running)} is running. Close it first.";
                            Progress("blocked", reason);
                            results.Add(new { id = game.Id, name = game.Name, status = "blocked", message = reason });
                            continue;
                        }
                    }

                    string backupNote = "";
                    if (backup && saves != null && plan.Method != "launcher")
                    {
                        Progress("backup", "Backing up saves…");
                        try
                        {
                            SaveBackup saved = await saves.BackupGameAsync(game, "Before uninstall").ConfigureAwait(false);
                            if (saved != null) backupNote = " Saves backed up.";
                        }
                        catch (Exception error)
                        {
                            string reason = "Save backup failed, so it was left installed: " + error.Message;
                            Progress("failed", reason);
                            results.Add(new { id = game.Id, name = game.Name, status = "failed", message = reason });
                            continue;
                        }
                    }

                    try
                    {
                        (string status, string message) outcome = plan.Method switch
                        {
                            "recycle" => await RecycleGameAsync(game, plan, Progress).ConfigureAwait(false),
                            "launcher" => OpenLauncher(game, plan, openedLaunchers),
                            _ => await RunExternalAsync(game, plan, Progress).ConfigureAwait(false),
                        };
                        if (outcome.status == "removed") removedAny = true;
                        if (plan.Method != "recycle" && outcome.status != "failed") TrackRemoval(game, Array.Empty<string>());
                        Progress(outcome.status, outcome.message + backupNote);
                        results.Add(new { id = game.Id, name = game.Name, status = outcome.status, message = outcome.message + backupNote });
                    }
                    catch (Exception error)
                    {
                        Progress("failed", error.Message);
                        results.Add(new { id = game.Id, name = game.Name, status = "failed", message = error.Message });
                    }
                }
            }
            finally
            {
                batchRunning = false;
                Hub.Post(new { type = "batchUninstallDone", ok = true, cancelled = batchCancel, results });
                if (removedAny) _ = Hub.RefreshLibraryAsync();
            }
        }

        private async Task<(string, string)> RecycleGameAsync(GameEntry game, UninstallPlan plan, Action<string, string> progress)
        {
            var folders = plan.Folders.Where(folder => folder.Allowed && folder.Selected).ToList();
            if (folders.Count == 0) return ("blocked", "No folder could be removed safely.");
            progress("working", "Moving to the Recycle Bin…");
            var failed = new List<string>();
            foreach (UninstallFolder folder in folders)
            {
                if (!await UninstallService.RecycleAsync(folder.Path, owner).ConfigureAwait(false)) failed.Add(folder.Label);
            }
            return failed.Count == 0
                ? ("removed", "Moved to the Recycle Bin.")
                : (failed.Count == folders.Count ? "failed" : "partial", $"Not moved: {string.Join(", ", failed)}.");
        }

        private static (string, string) OpenLauncher(GameEntry game, UninstallPlan plan, HashSet<string> opened)
        {
            string key = plan.LauncherName ?? plan.FileName ?? "";
            if (game.Source == "Xbox" || opened.Add(key)) UninstallService.StartExternal(plan);
            return ("opened", $"Uninstall it in {plan.LauncherName ?? "its launcher"}.");
        }

        private async Task<(string, string)> RunExternalAsync(GameEntry game, UninstallPlan plan, Action<string, string> progress)
        {
            UninstallService.StartExternal(plan);
            progress("waiting", plan.Method == "steam" ? "Confirm in Steam. Waiting for it to finish…" : "Finish the uninstaller. Waiting for it to finish…");
            DateTime deadline = DateTime.UtcNow.AddMinutes(plan.Method == "steam" ? 20 : 30);
            while (DateTime.UtcNow < deadline)
            {
                if (batchSkip) return ("skipped", "Skipped waiting. Game Hub updates when it's gone.");
                await Task.Delay(1500).ConfigureAwait(false);
                bool installGone = !string.IsNullOrEmpty(game.InstallPath) && !Directory.Exists(game.InstallPath);
                bool exeGone = !string.IsNullOrEmpty(game.Exe) && !File.Exists(game.Exe);
                if (installGone || exeGone) return ("removed", "Uninstalled.");
            }
            return ("skipped", "Timed out waiting. Game Hub updates when it's gone.");
        }
    }
}
