using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class MoveModule : HubModule
    {
        private static readonly HashSet<string> CopySources = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Folder", "Extra", "Downloads", "Hydra" };
        private const long SpaceMargin = 256L * 1024 * 1024;

        private IntPtr owner;
        private CancellationTokenSource moving;

        public override void Start()
        {
            owner = Hub.Form.Handle;
        }

        public override void Stop()
        {
            moving?.Cancel();
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "movePlan":
                    _ = SendPlanAsync(message.GetString("id"));
                    return true;
                case "movePickFolder":
                    PickFolder(message.GetString("id"), message.GetString("start"));
                    return true;
                case "moveOpenSteam":
                    OpenSteam(message.GetString("id"));
                    return true;
                case "moveStart":
                    _ = MoveAsync(message.GetString("id"), message.GetString("destination"));
                    return true;
                case "moveCancel":
                    moving?.Cancel();
                    return true;
            }
            return false;
        }

        private sealed class MoveSource
        {
            public GameEntry Game;
            public string Kind;
            public string Folder;
            public string Reason;
            public string Warning;
        }

        private MoveSource Describe(GameEntry game)
        {
            var source = new MoveSource { Game = game, Folder = CleanupSafety.Full(game.InstallPath) };
            if (game.Status != "installed")
            {
                source.Kind = "blocked";
                source.Reason = "Only installed games can be moved.";
                return source;
            }
            if (game.Source == "Steam")
            {
                source.Kind = string.IsNullOrEmpty(game.SteamAppId) ? "blocked" : "steam";
                source.Reason = source.Kind == "blocked" ? "Move this game from Steam's Storage settings." : null;
                return source;
            }
            if (game.Source == "Epic")
            {
                source.Kind = "epic";
                return source;
            }
            if (!CopySources.Contains(game.Source ?? ""))
            {
                source.Kind = "blocked";
                source.Reason = $"{game.Source} manages this game, so move it from its own launcher.";
                return source;
            }

            source.Kind = "copy";
            if (source.Folder == null || !Directory.Exists(source.Folder))
            {
                source.Kind = "blocked";
                source.Reason = "Game Hub can't find this game's folder.";
                return source;
            }
            if (!string.IsNullOrEmpty(game.Exe) && !CleanupSafety.IsInside(game.Exe, source.Folder))
            {
                source.Kind = "blocked";
                source.Reason = "The game's program isn't inside its folder, so moving it could break it.";
                return source;
            }
            string reason = CleanupSafety.Check(source.Folder, Hub.Library?.Games ?? new List<GameEntry>(), game.Id, false);
            if (reason != null)
            {
                source.Kind = "blocked";
                source.Reason = reason;
                return source;
            }
            if (game.Source == "Hydra") source.Warning = "Hydra will still point at the old folder. Game Hub keeps tracking the new one, but Hydra may show the game as missing.";
            return source;
        }

        private async Task SendPlanAsync(string id)
        {
            GameEntry game = Hub.Find(id);
            if (game == null) return;
            MoveSource source = Describe(game);
            long bytes = 0;
            int files = 0;
            bool links = false;
            List<string> running = new List<string>();
            if (source.Kind == "copy")
            {
                Hub.Post(new { type = "movePlan", id, kind = "measuring", source = source.Folder });
                FolderMeasure measure = await Task.Run(() => CleanupSafety.Measure(source.Folder)).ConfigureAwait(false);
                bytes = measure.Bytes;
                files = measure.Files;
                links = measure.HasLinks;
                running = await Task.Run(() => CleanupSafety.RunningFrom(source.Folder)).ConfigureAwait(false);
            }
            Hub.Post(new
            {
                type = "movePlan",
                id,
                name = game.Name,
                kind = links ? "blocked" : source.Kind,
                source = source.Folder,
                bytes,
                files,
                reason = links ? "This folder contains links to other locations, so it can't be copied safely." : source.Reason,
                warning = source.Warning,
                running = running.Count > 0 ? string.Join(", ", running) : null,
                roots = Drives(source.Folder),
            });
        }

        private static List<object> Drives(string sourceFolder)
        {
            string sourceRoot = sourceFolder == null ? null : Path.GetPathRoot(sourceFolder);
            var drives = new List<object>();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives().Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed))
                {
                    drives.Add(new
                    {
                        path = drive.RootDirectory.FullName,
                        free = drive.AvailableFreeSpace,
                        total = drive.TotalSize,
                        label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "Local disk" : drive.VolumeLabel,
                        current = string.Equals(drive.RootDirectory.FullName, sourceRoot, StringComparison.OrdinalIgnoreCase),
                    });
                }
            }
            catch
            {
            }
            return drives;
        }

        private void OpenSteam(string id)
        {
            GameEntry game = Hub.Find(id);
            if (game == null || string.IsNullOrEmpty(game.SteamAppId)) return;
            try
            {
                Process.Start(new ProcessStartInfo("steam://gameproperties/" + game.SteamAppId) { UseShellExecute = true });
            }
            catch (Exception error)
            {
                Hub.Toast("Couldn't open Steam: " + error.Message, "error");
            }
        }

        private void PickFolder(string id, string start)
        {
            GameEntry game = Hub.Find(id);
            if (game == null) return;
            Hub.RunOnUi(() =>
            {
                string chosen = CleanupNative.PickFolder(Hub.Form.Handle, $"Choose where to move {game.Name}", Directory.Exists(start ?? "") ? start : null);
                if (string.IsNullOrEmpty(chosen)) return;
                string folder = CleanupSafety.Full(game.InstallPath);
                Hub.Post(new { type = "movePicked", id, path = chosen, target = TargetFor(folder, chosen), reason = DestinationProblem(folder, chosen), free = FreeSpace(chosen) });
            });
        }

        private static string TargetFor(string source, string destination) =>
            source == null || destination == null ? null : Path.Combine(CleanupSafety.Full(destination), Path.GetFileName(source));

        private static long FreeSpace(string path)
        {
            try
            {
                return new DriveInfo(Path.GetPathRoot(CleanupSafety.Full(path))).AvailableFreeSpace;
            }
            catch
            {
                return -1;
            }
        }

        private static string DestinationProblem(string source, string destination)
        {
            string full = CleanupSafety.Full(destination);
            if (full == null || !Directory.Exists(full)) return "That folder doesn't exist.";
            if (source == null) return "Game Hub can't find this game's folder.";
            if (CleanupSafety.IsSameOrInside(full, source)) return "Choose a folder outside the game's own folder.";
            if (CleanupSafety.SamePath(full, Path.GetDirectoryName(source))) return "The game is already in this folder.";
            string lower = full.ToLowerInvariant() + "\\";
            if (lower.Contains("\\steamapps\\") || lower.Contains("\\epic games\\")) return "That folder belongs to a launcher.";
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (CleanupSafety.IsSameOrInside(full, windows)) return "Choose a folder outside Windows.";
            string target = TargetFor(source, full);
            if (Directory.Exists(target) || File.Exists(target)) return $"{Path.GetFileName(target)} already exists there.";
            return null;
        }

        private async Task MoveAsync(string id, string destination)
        {
            if (moving != null)
            {
                Hub.Post(new { type = "moveDone", id, ok = false, message = "Another game is already being moved." });
                return;
            }
            GameEntry game = Hub.Find(id);
            if (game == null) return;

            moving = new CancellationTokenSource();
            CancellationToken token = moving.Token;
            string target = null;
            bool created = false;
            try
            {
                MoveSource source = Describe(game);
                if (source.Kind != "copy") throw new InvalidOperationException(source.Reason ?? "This game can't be moved from here.");
                string problem = DestinationProblem(source.Folder, destination);
                if (problem != null) throw new InvalidOperationException(problem);
                target = TargetFor(source.Folder, destination);

                Progress("checking", 0, 0, 0, 0);
                List<string> running = await Task.Run(() => CleanupSafety.RunningFrom(source.Folder)).ConfigureAwait(false);
                if (running.Count > 0) throw new InvalidOperationException($"Close {game.Name} first ({string.Join(", ", running)} is running).");

                FolderMeasure measure = await Task.Run(() => CleanupSafety.Measure(source.Folder)).ConfigureAwait(false);
                if (measure.HasLinks) throw new InvalidOperationException("This folder contains links to other locations, so it can't be copied safely.");
                if (measure.Truncated) throw new InvalidOperationException("This folder has too many files to move safely.");

                bool sameVolume = string.Equals(Path.GetPathRoot(source.Folder), Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase);
                string note;
                if (sameVolume)
                {
                    Progress("moving", 0, measure.Bytes, 0, measure.Files);
                    await Task.Run(() => Directory.Move(source.Folder, target)).ConfigureAwait(false);
                    note = $"Moved {game.Name} to {target}.";
                }
                else
                {
                    long free = FreeSpace(target);
                    if (free >= 0 && free < measure.Bytes + SpaceMargin) throw new InvalidOperationException($"Not enough space. {game.Name} needs {CleanupModule.FormatBytes(measure.Bytes)} and the drive has {CleanupModule.FormatBytes(free)} free.");

                    Directory.CreateDirectory(target);
                    created = true;
                    await Task.Run(() => Copy(source.Folder, target, measure, token), token).ConfigureAwait(false);

                    Progress("verifying", measure.Bytes, measure.Bytes, measure.Files, measure.Files);
                    FolderMeasure before = await Task.Run(() => CleanupSafety.Measure(source.Folder)).ConfigureAwait(false);
                    FolderMeasure after = await Task.Run(() => CleanupSafety.Measure(target)).ConfigureAwait(false);
                    if (before.Files != after.Files || before.Bytes != after.Bytes) throw new InvalidOperationException($"The copy didn't match the original ({after.Files} of {before.Files} files, {CleanupModule.FormatBytes(after.Bytes)} of {CleanupModule.FormatBytes(before.Bytes)}). The original was kept.");

                    created = false;
                    Progress("recycling", measure.Bytes, measure.Bytes, measure.Files, measure.Files);
                    bool recycled = CleanupSafety.Check(source.Folder, Hub.Library?.Games ?? new List<GameEntry>(), game.Id, false) == null
                        && await CleanupSafety.RecycleAsync(source.Folder, owner).ConfigureAwait(false);
                    note = recycled
                        ? $"Moved {game.Name} to {target}. The old copy is in the Recycle Bin."
                        : $"Copied {game.Name} to {target}, but the old folder couldn't be moved to the Recycle Bin. Remove it yourself when you're ready.";
                }

                string newId = UpdateReferences(game, source.Folder, target);
                Hub.Post(new { type = "gameMoved", from = game.Id, to = newId });
                Hub.Post(new { type = "moveDone", id, ok = true, message = note, newId });
                _ = Hub.RefreshLibraryAsync();
            }
            catch (Exception error)
            {
                bool cancelled = error is OperationCanceledException;
                string message = cancelled ? "Move cancelled. The game wasn't changed." : error.Message;
                if (created && target != null && Directory.Exists(target))
                {
                    bool cleaned = await CleanupSafety.RecycleAsync(target, owner).ConfigureAwait(false);
                    if (!cleaned) message += $" The partial copy at {target} couldn't be removed.";
                    else if (!cancelled) message += " The partial copy was moved to the Recycle Bin.";
                }
                Hub.Post(new { type = "moveDone", id, ok = false, cancelled, message });
            }
            finally
            {
                moving.Dispose();
                moving = null;
            }

            void Progress(string phase, long done, long total, int files, int totalFiles) =>
                Hub.Post(new { type = "moveProgress", id, phase, done, total, files, totalFiles });
        }

        private void Copy(string source, string target, FolderMeasure measure, CancellationToken token)
        {
            long done = 0;
            int files = 0;
            var buffer = new byte[1024 * 1024];
            var timer = Stopwatch.StartNew();
            var folders = new Stack<(string From, string To)>();
            folders.Push((source, target));
            var stamps = new List<(string Path, DirectoryInfo Info)>();

            while (folders.Count > 0)
            {
                var (from, to) = folders.Pop();
                Directory.CreateDirectory(to);
                stamps.Add((to, new DirectoryInfo(from)));
                foreach (string folder in Directory.GetDirectories(from)) folders.Push((folder, Path.Combine(to, Path.GetFileName(folder))));
                foreach (string file in Directory.GetFiles(from))
                {
                    token.ThrowIfCancellationRequested();
                    string destination = Path.Combine(to, Path.GetFileName(file));
                    using (var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan))
                    using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length))
                    {
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            output.Write(buffer, 0, read);
                            done += read;
                            if (timer.ElapsedMilliseconds > 250)
                            {
                                timer.Restart();
                                Hub.Post(new { type = "moveProgress", phase = "copying", done, total = measure.Bytes, files, totalFiles = measure.Files });
                            }
                        }
                    }
                    var info = new FileInfo(file);
                    File.SetCreationTimeUtc(destination, info.CreationTimeUtc);
                    File.SetLastWriteTimeUtc(destination, info.LastWriteTimeUtc);
                    File.SetAttributes(destination, info.Attributes & ~FileAttributes.ReadOnly);
                    if ((info.Attributes & FileAttributes.ReadOnly) != 0) File.SetAttributes(destination, info.Attributes);
                    files++;
                }
            }
            foreach (var (path, info) in stamps)
            {
                try
                {
                    Directory.SetCreationTimeUtc(path, info.CreationTimeUtc);
                    Directory.SetLastWriteTimeUtc(path, info.LastWriteTimeUtc);
                }
                catch
                {
                }
            }
            Hub.Post(new { type = "moveProgress", phase = "copying", done, total = measure.Bytes, files, totalFiles = measure.Files });
        }

        private string UpdateReferences(GameEntry game, string from, string to)
        {
            string newExe = !string.IsNullOrEmpty(game.Exe) && CleanupSafety.IsInside(game.Exe, from) ? to + game.Exe.Substring(from.Length) : game.Exe;
            string newId = game.Id != null && game.Id.StartsWith("local:", StringComparison.OrdinalIgnoreCase) && newExe != null ? "local:" + newExe.ToLowerInvariant() : game.Id;

            HubConfig.Update(config =>
            {
                foreach (string key in config.Keys.ToList()) config[key] = Rewrite(config[key], from, to, game.Id, newId);
                List<string> libraries = HubConfig.GetList(config, "LibraryFolders").Select(entry => CleanupSafety.Full(entry)).Where(entry => entry != null).ToList();
                List<string> extras = HubConfig.GetList(config, "ExtraGames");
                bool coveredByLibrary = libraries.Any(library => CleanupSafety.SamePath(Path.GetDirectoryName(to), library));
                bool coveredByExtra = extras.Any(entry => CleanupSafety.IsSameOrInside(to, CleanupSafety.Full(entry)) || CleanupSafety.IsInside(CleanupSafety.Full(entry), to));
                if (!coveredByLibrary && !coveredByExtra)
                {
                    extras.Add(to);
                    config["ExtraGames"] = extras.Cast<object>().ToArray();
                }
            });

            if (newId != game.Id)
            {
                Dictionary<string, object> data = Hub.UserData.Get(game.Id);
                if (data.Count > 0)
                {
                    Hub.UserData.Merge(newId, data);
                    Hub.UserData.Merge(game.Id, data.Keys.ToDictionary(key => key, key => (object)null));
                }
                if (Hub.State.Launches.TryGetValue(game.Id, out string launched))
                {
                    Hub.State.Launches[newId] = launched;
                    Hub.State.Launches.Remove(game.Id);
                    Hub.State.Save();
                }
            }
            return newId;
        }

        private static object Rewrite(object value, string from, string to, string oldId, string newId)
        {
            switch (value)
            {
                case string text:
                    if (oldId != null && string.Equals(text, oldId, StringComparison.OrdinalIgnoreCase)) return newId;
                    string expanded = Environment.ExpandEnvironmentVariables(text);
                    if (expanded.Length < 3 || expanded[1] != ':' || !Path.IsPathRooted(expanded)) return text;
                    string full = CleanupSafety.Full(expanded);
                    if (full != null && CleanupSafety.IsSameOrInside(full, from)) return to + full.Substring(from.Length);
                    return text;
                case Dictionary<string, object> map:
                    var rewritten = new Dictionary<string, object>();
                    foreach (var entry in map)
                    {
                        string key = oldId != null && string.Equals(entry.Key, oldId, StringComparison.OrdinalIgnoreCase) ? newId : entry.Key;
                        rewritten[key] = Rewrite(entry.Value, from, to, oldId, newId);
                    }
                    return rewritten;
                case object[] items:
                    return items.Select(item => Rewrite(item, from, to, oldId, newId)).ToArray();
                default:
                    return value;
            }
        }
    }
}
