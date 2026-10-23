using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameHub
{
    internal sealed class DiscoveryModule : HubModule
    {
        private readonly object gate = new object();
        private List<object> hiddenGames = new List<object>();
        private List<object> mergedGames = new List<object>();
        private Dictionary<string, object> originals = new Dictionary<string, object>();
        private List<object> detectedEmulators = new List<object>();
        private string lastFollowUpStamp;
        private bool previewRunning;

        public override int Order => -90;

        public override void Start()
        {
            Task.Run(() =>
            {
                List<object> found = DiscoveryEmulators.Detect();
                lock (gate) detectedEmulators = found;
            });
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "discovery.pickExe":
                    PickExe(message);
                    return true;
                case "discovery.saveManual":
                    SaveManual(message);
                    return true;
                case "discovery.removeManual":
                    RemoveManual(message.GetString("id"));
                    return true;
                case "discovery.setOverride":
                    SetOverride(message);
                    return true;
                case "discovery.hide":
                    Hide(message.GetString("id"));
                    return true;
                case "discovery.unhide":
                    Unhide(message.GetString("id"));
                    return true;
                case "discovery.unhideName":
                    UnhideName(message.GetString("name"));
                    return true;
                case "discovery.merge":
                    Merge(message.GetString("keep"), message.GetString("absorb"));
                    return true;
                case "discovery.unmerge":
                    Unmerge(message.GetString("keep"), message.GetString("absorb"));
                    return true;
                case "discovery.saveEmulators":
                    SaveEmulators(message);
                    return true;
                case "discovery.shortcutsPreview":
                    _ = PreviewShortcutsAsync();
                    return true;
                case "discovery.shortcutsApply":
                    _ = ApplyShortcutsAsync();
                    return true;
            }
            return false;
        }

        public override void Decorate(LibraryDocument library)
        {
            Dictionary<string, object> config = HubConfig.Load();
            var ignoredIds = new HashSet<string>(HubConfig.GetList(config, "IgnoreIds"), StringComparer.OrdinalIgnoreCase);
            var ignoredNames = HubConfig.GetList(config, "IgnoreNames");
            var ignoredKeys = new HashSet<string>(ignoredNames.Select(NameKey).Where(key => key.Length > 0));
            Dictionary<string, object> overrides = config.TryGetValue("Overrides", out object value) ? value as Dictionary<string, object> : null;

            var originalValues = new Dictionary<string, object>();
            var originalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hidden = new List<object>();
            var merged = new List<object>();
            var byId = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (GameEntry game in library.Games)
            {
                if (game.Id != null && !byId.ContainsKey(game.Id)) byId[game.Id] = game;
            }

            foreach (GameEntry game in library.Games)
            {
                if (game.Id == null || overrides == null || !(overrides.TryGetValue(game.Id, out object entry) && entry is Dictionary<string, object> change)) continue;
                originalValues[game.Id] = new { game.Name, game.Exe, game.Arguments, game.Url };
                originalNames[game.Id] = game.Name;
                ApplyOverride(game, change);
            }

            var absorbed = new HashSet<GameEntry>();
            foreach (Dictionary<string, object> merge in ReadMerges(config))
            {
                string keepId = merge.GetString("Keep");
                string absorbId = merge.GetString("Absorb");
                byId.TryGetValue(keepId ?? "", out GameEntry keep);
                byId.TryGetValue(absorbId ?? "", out GameEntry other);
                merged.Add(new
                {
                    keep = keepId,
                    absorb = absorbId,
                    keepName = keep?.Name,
                    absorbName = other?.Name,
                    absorbSource = other?.Source,
                    absorbPath = other?.InstallPath ?? other?.Exe,
                    missing = keep == null || other == null,
                });
                if (keep == null || other == null || keep == other || absorbed.Contains(keep)) continue;
                MergeInto(keep, other);
                absorbed.Add(other);
            }

            var visible = new List<GameEntry>();
            var hiddenKeys = new HashSet<string>();
            foreach (GameEntry game in library.Games)
            {
                if (absorbed.Contains(game)) continue;
                bool byIdHidden = game.Id != null && ignoredIds.Contains(game.Id);
                bool byName = ignoredKeys.Contains(NameKey(game.Name)) || (originalNames.TryGetValue(game.Id ?? "", out string originalName) && ignoredKeys.Contains(NameKey(originalName)));
                if (byIdHidden || byName)
                {
                    hidden.Add(new { id = game.Id, name = game.Name, source = game.Source, status = game.Status, byName = !byIdHidden, iconUrl = game.IconUrl });
                    hiddenKeys.Add(NameKey(game.Name));
                    if (originalNames.TryGetValue(game.Id ?? "", out string hiddenOriginal)) hiddenKeys.Add(NameKey(hiddenOriginal));
                    continue;
                }
                visible.Add(game);
            }

            foreach (string name in ignoredNames)
            {
                if (!hiddenKeys.Contains(NameKey(name))) hidden.Add(new { id = (string)null, name, source = (string)null, status = (string)null, byName = true, iconUrl = (string)null });
            }

            library.Games = visible;
            lock (gate)
            {
                hiddenGames = hidden;
                mergedGames = merged;
                originals = originalValues;
            }
        }

        public override void OnLibraryLoaded(LibraryDocument library)
        {
            string stamp = ConfigStamp();
            if (stamp == null || string.IsNullOrEmpty(library.ConfigStamp) || library.ConfigStamp == stamp || lastFollowUpStamp == stamp) return;
            lastFollowUpStamp = stamp;
            Hub.RunOnUi(() => _ = Hub.RefreshLibraryAsync());
        }

        public override void AddLibraryData(Dictionary<string, object> extras)
        {
            Dictionary<string, object> config = HubConfig.Load();
            lock (gate)
            {
                extras["discovery"] = new
                {
                    manual = config.TryGetValue("ManualGames", out object manual) ? manual : new object[0],
                    overrides = config.TryGetValue("Overrides", out object overrides) ? overrides : new Dictionary<string, object>(),
                    originals,
                    hidden = hiddenGames,
                    merges = mergedGames,
                    emulators = config.TryGetValue("Emulators", out object emulators) ? emulators : new object[0],
                    detectedEmulators,
                    watchFolders = Hub.Settings.GetBool("discovery.watchFolders", true),
                    coverIcons = Hub.Settings.GetBool("discovery.coverIcons", false),
                    shortcutFolder = config.GetString("ShortcutFolderName") ?? "Games",
                };
            }
        }

        private static void ApplyOverride(GameEntry game, Dictionary<string, object> change)
        {
            string name = (change.GetString("Name") ?? "").Trim();
            if (name.Length > 0) game.Name = name;

            string exe = ExpandPath(change.GetString("Exe"));
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                bool iconFromExe = string.IsNullOrEmpty(game.Icon) || string.Equals(game.Icon, game.Exe, StringComparison.OrdinalIgnoreCase);
                if (!string.Equals(exe, game.Exe, StringComparison.OrdinalIgnoreCase) && iconFromExe)
                {
                    game.Icon = exe;
                    game.IconUrl = IconService.GetUrl(exe, 128) ?? game.IconUrl;
                }
                game.Exe = exe;
                game.Url = null;
            }

            if (change.TryGetValue("Arguments", out object raw) && raw != null)
            {
                string arguments = (raw as string ?? "").Trim();
                game.Arguments = arguments.Length > 0 ? arguments : null;
                if (arguments.Length > 0 && !string.IsNullOrEmpty(game.Exe)) game.Url = null;
            }
        }

        private static void MergeInto(GameEntry keep, GameEntry other)
        {
            keep.Parts ??= new List<GamePart>();
            var known = new HashSet<string>(keep.MeasuredPaths.Select(path => path.TrimEnd('\\')), StringComparer.OrdinalIgnoreCase);

            void AddPart(string label, string path)
            {
                if (string.IsNullOrEmpty(path)) return;
                string trimmed = path.TrimEnd('\\');
                if (known.Any(existing => IsInside(trimmed, existing))) return;
                known.Add(trimmed);
                keep.Parts.Add(new GamePart { Label = label, Path = path });
            }

            AddPart($"{SourceLabel(other.Source)} copy", other.InstallPath);
            foreach (GamePart part in other.Parts ?? new List<GamePart>()) AddPart(part.Label, part.Path);

            keep.LastPlayed = Latest(keep.LastPlayed, other.LastPlayed);
            if ((other.PlaytimeMinutes ?? 0) > (keep.PlaytimeMinutes ?? 0)) keep.PlaytimeMinutes = other.PlaytimeMinutes;
            keep.SteamAppId ??= other.SteamAppId;
            keep.CoverUrl ??= other.CoverUrl;
            keep.HeroUrl ??= other.HeroUrl;
            keep.LogoUrl ??= other.LogoUrl;
            keep.HeaderUrl ??= other.HeaderUrl;
            keep.IconUrl ??= other.IconUrl;
            if (keep.Status != "installed" && other.Status == "installed")
            {
                keep.Status = "installed";
                keep.Exe ??= other.Exe;
                keep.Arguments ??= other.Arguments;
                keep.Url ??= other.Url;
            }
            keep.Download ??= other.Download;
            if (other.NeedsUpdate == true) keep.NeedsUpdate = true;
        }

        private static string SourceLabel(string source)
        {
            if (string.IsNullOrEmpty(source)) return "Other";
            return source.StartsWith("Emulator:") ? source.Substring(9) : source;
        }

        private static bool IsInside(string path, string folder)
        {
            return string.Equals(path, folder, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase)
                || folder.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private static string Latest(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second;
            if (string.IsNullOrEmpty(second)) return first;
            try
            {
                return DateTime.Parse(first).ToUniversalTime() >= DateTime.Parse(second).ToUniversalTime() ? first : second;
            }
            catch
            {
                return first;
            }
        }

        internal static string NameKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var builder = new StringBuilder(name.Length);
            foreach (char character in name.ToLowerInvariant())
            {
                if (char.IsLetter(character) || char.IsDigit(character)) builder.Append(character);
            }
            return builder.ToString();
        }

        internal static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            return Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')).TrimEnd('\\');
        }

        private static IEnumerable<Dictionary<string, object>> ReadMerges(Dictionary<string, object> config)
        {
            return config.TryGetValue("Merges", out object value) && value is object[] items
                ? items.OfType<Dictionary<string, object>>()
                : Enumerable.Empty<Dictionary<string, object>>();
        }

        private static string ConfigStamp()
        {
            try
            {
                return File.Exists(HubPaths.ConfigFile) ? File.GetLastWriteTimeUtc(HubPaths.ConfigFile).Ticks.ToString() : null;
            }
            catch
            {
                return null;
            }
        }

        private void PickExe(Dictionary<string, object> message)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Choose the game's program",
                Filter = "Programs|*.exe;*.bat;*.cmd;*.lnk;*.url|All files|*.*",
                CheckFileExists = true,
                DereferenceLinks = true,
            };
            string initial = ExpandPath(message.GetString("initial"));
            if (!string.IsNullOrEmpty(initial))
            {
                string folder = File.Exists(initial) ? Path.GetDirectoryName(initial) : initial;
                if (Directory.Exists(folder)) dialog.InitialDirectory = folder;
            }

            string path = dialog.ShowDialog(Hub.Form) == DialogResult.OK ? dialog.FileName : null;
            Hub.Post(new
            {
                type = "discovery.pickedExe",
                requestId = message.GetString("requestId"),
                path,
                name = path == null ? null : SuggestName(path),
                known = path == null ? null : FindByExe(path),
            });
        }

        private string FindByExe(string path)
        {
            return Hub.Library?.Games.FirstOrDefault(game => string.Equals(game.Exe, path, StringComparison.OrdinalIgnoreCase))?.Name;
        }

        private static string SuggestName(string path)
        {
            try
            {
                if (string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    string product = FileVersionInfo.GetVersionInfo(path).ProductName?.Trim();
                    if (!string.IsNullOrEmpty(product) && !IsGenericProduct(product)) return product;
                }
            }
            catch
            {
            }

            string file = Path.GetFileNameWithoutExtension(path);
            if (file.Contains(' ') && !IsGenericProduct(file)) return file;
            string folder = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
            return string.IsNullOrEmpty(folder) ? file : folder.Replace('_', ' ').Trim();
        }

        private static bool IsGenericProduct(string name)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(name, @"(?i)^(unity.*|unreal.*|ue[45].*|.*bootstrap.*|game|launcher|nw\.?js|electron|godot.*|microsoft.*|windows.*|java.*|python.*|mono.*)$");
        }

        private void SaveManual(Dictionary<string, object> message)
        {
            string exe = ExpandPath(message.GetString("exe"));
            string name = (message.GetString("name") ?? "").Trim();
            string arguments = (message.GetString("arguments") ?? "").Trim();
            string previousId = message.GetString("id");
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                Hub.Toast("That program could not be found.", "error");
                return;
            }
            if (name.Length == 0) name = SuggestName(exe);
            string id = "manual:" + exe.ToLowerInvariant();

            UpdateConfig(config =>
            {
                var games = Items(config, "ManualGames");
                games.RemoveAll(entry => string.Equals(ManualId(entry), previousId, StringComparison.OrdinalIgnoreCase) || string.Equals(ManualId(entry), id, StringComparison.OrdinalIgnoreCase));
                games.Add(new Dictionary<string, object> { ["Id"] = id, ["Name"] = name, ["Exe"] = exe, ["Arguments"] = arguments });
                config["ManualGames"] = games.Cast<object>().ToArray();
            }, previousId == null ? $"Added {name}" : $"Saved {name}");
            Hub.Post(new { type = "discovery.manualSaved", id, requestId = message.GetString("requestId") });
        }

        private static string ManualId(Dictionary<string, object> entry)
        {
            string id = entry.GetString("Id");
            if (!string.IsNullOrEmpty(id)) return id;
            string exe = ExpandPath(entry.GetString("Exe"));
            return exe == null ? null : "manual:" + exe.ToLowerInvariant();
        }

        private void RemoveManual(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            UpdateConfig(config =>
            {
                var games = Items(config, "ManualGames");
                games.RemoveAll(entry => string.Equals(ManualId(entry), id, StringComparison.OrdinalIgnoreCase));
                config["ManualGames"] = games.Cast<object>().ToArray();
                RemoveIdReferences(config, id);
            }, "Removed from the library");
        }

        private void SetOverride(Dictionary<string, object> message)
        {
            string id = message.GetString("id");
            if (string.IsNullOrEmpty(id)) return;
            string name = (message.GetString("name") ?? "").Trim();
            string exe = (message.GetString("exe") ?? "").Trim().Trim('"');
            bool hasArguments = message.TryGetValue("arguments", out object rawArguments) && rawArguments != null;
            string arguments = (rawArguments as string ?? "").Trim();

            if (exe.Length > 0 && !File.Exists(ExpandPath(exe)))
            {
                Hub.Toast("That program could not be found.", "error");
                return;
            }

            if (id.StartsWith("manual:", StringComparison.OrdinalIgnoreCase))
            {
                SaveManual(new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["name"] = name,
                    ["exe"] = exe.Length > 0 ? exe : Hub.Find(id)?.Exe,
                    ["arguments"] = hasArguments ? arguments : Hub.Find(id)?.Arguments,
                });
                return;
            }

            UpdateConfig(config =>
            {
                var overrides = config.TryGetValue("Overrides", out object value) && value is Dictionary<string, object> existing
                    ? existing
                    : new Dictionary<string, object>();
                var change = new Dictionary<string, object>();
                if (name.Length > 0) change["Name"] = name;
                if (exe.Length > 0) change["Exe"] = exe;
                if (hasArguments) change["Arguments"] = arguments;

                if (change.Count == 0) overrides.Remove(id);
                else overrides[id] = change;
                if (overrides.Count == 0) config.Remove("Overrides");
                else config["Overrides"] = overrides;
            }, "Saved");
        }

        private void Hide(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            string name = Hub.Find(id)?.Name ?? "Game";
            UpdateConfig(config =>
            {
                var ids = HubConfig.GetList(config, "IgnoreIds");
                if (!ids.Contains(id, StringComparer.OrdinalIgnoreCase)) ids.Add(id);
                config["IgnoreIds"] = ids.Cast<object>().ToArray();
            }, $"{name} is hidden");
        }

        private void Unhide(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            UpdateConfig(config =>
            {
                var ids = HubConfig.GetList(config, "IgnoreIds");
                ids.RemoveAll(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase));
                config["IgnoreIds"] = ids.Cast<object>().ToArray();
            }, "Back in the library");
        }

        private void UnhideName(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            string key = NameKey(name);
            UpdateConfig(config =>
            {
                var names = HubConfig.GetList(config, "IgnoreNames");
                names.RemoveAll(existing => NameKey(existing) == key);
                config["IgnoreNames"] = names.Cast<object>().ToArray();
            }, "Back in the library");
        }

        private void Merge(string keep, string absorb)
        {
            if (string.IsNullOrEmpty(keep) || string.IsNullOrEmpty(absorb) || string.Equals(keep, absorb, StringComparison.OrdinalIgnoreCase)) return;
            string keepName = Hub.Find(keep)?.Name ?? "the game";
            UpdateConfig(config =>
            {
                var merges = Items(config, "Merges");
                merges.RemoveAll(entry => string.Equals(entry.GetString("Absorb"), absorb, StringComparison.OrdinalIgnoreCase));
                foreach (var entry in merges.Where(entry => string.Equals(entry.GetString("Keep"), absorb, StringComparison.OrdinalIgnoreCase))) entry["Keep"] = keep;
                merges.Add(new Dictionary<string, object> { ["Keep"] = keep, ["Absorb"] = absorb });
                config["Merges"] = merges.Cast<object>().ToArray();
            }, $"Merged into {keepName}");
        }

        private void Unmerge(string keep, string absorb)
        {
            UpdateConfig(config =>
            {
                var merges = Items(config, "Merges");
                merges.RemoveAll(entry => string.Equals(entry.GetString("Absorb"), absorb, StringComparison.OrdinalIgnoreCase)
                    && (keep == null || string.Equals(entry.GetString("Keep"), keep, StringComparison.OrdinalIgnoreCase)));
                if (merges.Count == 0) config.Remove("Merges");
                else config["Merges"] = merges.Cast<object>().ToArray();
            }, "Split back into separate games");
        }

        private static void RemoveIdReferences(Dictionary<string, object> config, string id)
        {
            var ids = HubConfig.GetList(config, "IgnoreIds");
            if (ids.RemoveAll(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase)) > 0) config["IgnoreIds"] = ids.Cast<object>().ToArray();
            if (config.TryGetValue("Overrides", out object value) && value is Dictionary<string, object> overrides) overrides.Remove(id);
            var merges = Items(config, "Merges");
            if (merges.RemoveAll(entry => string.Equals(entry.GetString("Keep"), id, StringComparison.OrdinalIgnoreCase) || string.Equals(entry.GetString("Absorb"), id, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                config["Merges"] = merges.Cast<object>().ToArray();
            }
        }

        private void SaveEmulators(Dictionary<string, object> message)
        {
            var emulators = new List<object>();
            var names = new HashSet<string>();
            foreach (var entry in (message.TryGetValue("emulators", out object value) && value is object[] items ? items : new object[0]).OfType<Dictionary<string, object>>())
            {
                string name = (entry.GetString("Name") ?? "").Trim();
                string exe = (entry.GetString("Exe") ?? "").Trim().Trim('"');
                if (name.Length == 0 || exe.Length == 0 || !names.Add(NameKey(name))) continue;
                emulators.Add(new Dictionary<string, object>
                {
                    ["Name"] = name,
                    ["Exe"] = exe,
                    ["Arguments"] = (entry.GetString("Arguments") ?? "").Trim(),
                    ["RomFolders"] = StringItems(entry, "RomFolders"),
                    ["Extensions"] = StringItems(entry, "Extensions").Select(extension => extension.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant()).Where(extension => extension.Length > 0).Distinct().ToArray(),
                });
            }

            UpdateConfig(config =>
            {
                if (emulators.Count == 0) config.Remove("Emulators");
                else config["Emulators"] = emulators.ToArray();
            }, "Emulators saved");
        }

        private static string[] StringItems(Dictionary<string, object> entry, string key)
        {
            return entry.TryGetValue(key, out object value) && value is object[] items
                ? items.OfType<string>().Select(item => item.Trim()).Where(item => item.Length > 0).ToArray()
                : new string[0];
        }

        private static List<Dictionary<string, object>> Items(Dictionary<string, object> config, string key)
        {
            return config.TryGetValue(key, out object value) && value is object[] items
                ? items.OfType<Dictionary<string, object>>().ToList()
                : new List<Dictionary<string, object>>();
        }

        private void UpdateConfig(Action<Dictionary<string, object>> change, string toast)
        {
            Task.Run(() => HubConfig.Update(change)).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Hub.Toast("Could not save the game settings: " + task.Exception?.GetBaseException().Message, "error");
                    return;
                }
                if (!string.IsNullOrEmpty(toast)) Hub.Toast(toast, "success");
                Hub.RunOnUi(async () =>
                {
                    try
                    {
                        await Hub.ReloadLibraryAsync();
                        await Hub.RefreshLibraryAsync();
                    }
                    catch (Exception error)
                    {
                        Hub.Toast("Could not refresh the library: " + error.Message, "error");
                    }
                });
            });
        }

        private async Task PreviewShortcutsAsync()
        {
            if (previewRunning) return;
            previewRunning = true;
            Hub.Post(new { type = "discovery.shortcutsPreview", running = true });
            string output = Path.Combine(Path.GetTempPath(), "game-hub-shortcuts-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                string error = await RunScriptAsync($"-Quiet -PreviewJson \"{output}\"");
                Dictionary<string, object> plan = File.Exists(output) ? Json.ParseObject(File.ReadAllText(output, Encoding.UTF8)) : null;
                Hub.Post(new
                {
                    type = "discovery.shortcutsPreview",
                    running = false,
                    error = plan == null ? error ?? "The preview could not be created." : null,
                    folder = plan?.GetString("Folder"),
                    items = plan != null && plan.TryGetValue("Items", out object items) ? items : new object[0],
                    skipped = plan != null && plan.TryGetValue("Skipped", out object skipped) ? skipped : new object[0],
                });
            }
            catch (Exception error)
            {
                Hub.Post(new { type = "discovery.shortcutsPreview", running = false, error = error.Message });
            }
            finally
            {
                previewRunning = false;
                try
                {
                    if (File.Exists(output)) File.Delete(output);
                }
                catch
                {
                }
            }
        }

        private async Task ApplyShortcutsAsync()
        {
            await Hub.RefreshLibraryAsync();
            Hub.Post(new { type = "discovery.shortcutsApplied" });
        }

        private static Task<string> RunScriptAsync(string arguments)
        {
            var completion = new TaskCompletionSource<string>();
            var errors = new StringBuilder();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{HubPaths.SyncScript}\" {arguments}",
                    WorkingDirectory = HubPaths.SyncDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                },
                EnableRaisingEvents = true,
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) lock (errors) errors.AppendLine(args.Data);
            };
            process.Exited += (sender, args) =>
            {
                process.WaitForExit();
                string text;
                lock (errors) text = errors.ToString().Trim();
                completion.TrySetResult(text.Length > 0 ? text : null);
                process.Dispose();
            };

            try
            {
                process.Start();
                process.BeginErrorReadLine();
            }
            catch (Exception error)
            {
                completion.TrySetResult(error.Message);
            }
            return completion.Task;
        }
    }
}
