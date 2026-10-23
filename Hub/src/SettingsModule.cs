using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameHub
{
    internal sealed class SettingsModule : HubModule
    {
        private static readonly string[] ConfigLists = { "LibraryFolders", "SearchFolders", "ExtraGames", "IgnoreNames" };
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        private SettingsBackup pendingImport;

        public override int Order => -100;

        public override void Start()
        {
            DiagLog.Attach();
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "settingsInfo":
                    _ = PostInfoAsync();
                    return true;
                case "saveConfigList":
                    _ = SaveConfigListAsync(message.GetString("key"), StringList(message, "items"));
                    return true;
                case "setSyncInterval":
                    _ = SetSyncIntervalAsync(message.GetString("value"));
                    return true;
                case "pickFolder":
                    PickFolder(message);
                    return true;
                case "pickFile":
                    PickFile(message);
                    return true;
                case "setTheme":
                    ApplyTheme(message);
                    return true;
                case "clientError":
                    DiagLog.Write("page", ClientError(message));
                    return true;
                case "diagLog":
                    DiagLog.Write(message.GetString("area") ?? "page", message.GetString("message"));
                    return true;
                case "clearCache":
                    _ = ClearCacheAsync(message.GetString("kind"));
                    return true;
                case "openDiagFile":
                    OpenDiagFile(message.GetString("file"));
                    return true;
                case "exportData":
                    ExportData(message);
                    return true;
                case "importData":
                    _ = ImportDataAsync();
                    return true;
                case "applyImport":
                    _ = ApplyImportAsync();
                    return true;
                case "cancelImport":
                    pendingImport = null;
                    return true;
                case "installHub":
                    InstallHub();
                    return true;
                case "uninstallHub":
                    _ = UninstallHubAsync();
                    return true;
                case "checkOnline":
                    _ = CheckOnlineAsync();
                    return true;
                case "pickThemeBackground":
                    PickThemeBackground();
                    return true;
            }
            return false;
        }

        private async Task PostInfoAsync()
        {
            string webView = Hub.Environment?.BrowserVersionString;
            string generatedAt = Hub.Library?.GeneratedAt;
            var info = await Task.Run(() =>
            {
                Dictionary<string, object> config = HubConfig.Load();
                string interval = null;
                try
                {
                    interval = SettingsSyncTask.ReadInterval();
                }
                catch (Exception error)
                {
                    DiagLog.Write("settings", "Reading the sync task failed: " + error.Message);
                }

                return new
                {
                    type = "settingsInfo",
                    config = ConfigLists.ToDictionary(key => key, key => HubConfig.GetList(config, key)),
                    configFile = HubPaths.ConfigFile,
                    sync = new { interval, available = interval != null, task = SettingsSyncTask.TaskName },
                    about = About(webView),
                    install = InstallService.Status(),
                    diagnostics = Diagnostics(generatedAt),
                };
            });
            Hub.Post(info);
        }

        private static object About(string webView)
        {
            string exe = Application.ExecutablePath;
            return new
            {
                version = File.GetLastWriteTime(exe).ToString("yyyy.M.d"),
                built = File.GetLastWriteTime(exe).ToString("o"),
                webView2 = webView ?? TryGetRuntimeVersion(),
                framework = Environment.Version.ToString(),
                os = WindowsVersion(),
                exe,
                hubDir = HubPaths.HubDir,
                cacheDir = HubPaths.CacheDir,
                syncDir = HubPaths.SyncDir,
                syncScript = HubPaths.SyncScript,
            };
        }

        private static string WindowsVersion()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                string product = key?.GetValue("ProductName") as string;
                string build = key?.GetValue("CurrentBuild") as string;
                if (product == null || build == null) return Environment.OSVersion.VersionString;
                if (int.TryParse(build, out int buildNumber) && buildNumber >= 22000) product = product.Replace("Windows 10", "Windows 11");
                string release = key.GetValue("DisplayVersion") as string;
                string revision = key.GetValue("UBR")?.ToString();
                string fullBuild = revision == null ? build : $"{build}.{revision}";
                return string.IsNullOrEmpty(release) ? $"{product} (build {fullBuild})" : $"{product} {release} (build {fullBuild})";
            }
            catch
            {
                return Environment.OSVersion.VersionString;
            }
        }

        private static string TryGetRuntimeVersion()
        {
            try
            {
                return Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch
            {
                return null;
            }
        }

        private static object Diagnostics(string generatedAt)
        {
            string logPath = Path.Combine(HubPaths.SyncDir, "last-run.log");
            string log = "";
            string logTime = null;
            try
            {
                log = LibraryService.ReadLastSyncLog();
                if (log.Length > 6000) log = "…" + log.Substring(log.Length - 6000);
                if (File.Exists(logPath)) logTime = File.GetLastWriteTime(logPath).ToString("o");
            }
            catch
            {
            }

            SizeCache sizes = Json.ReadFile<SizeCache>(HubPaths.SizeCacheFile);
            string lastMeasured = sizes?.Items?.Values.Select(record => record.ScannedAt).Where(value => value != null).DefaultIfEmpty(null).Max();

            return new
            {
                syncLog = log,
                syncLogTime = logTime,
                errors = DiagLog.Latest(60),
                lastFullScan = sizes?.LastFullScan,
                lastMeasured,
                libraryGeneratedAt = generatedAt,
                cache = CacheUsage(),
            };
        }

        private static List<object> CacheUsage()
        {
            string customArt = Path.Combine(HubPaths.ArtCacheDir, "custom");
            long artBytes = Directory.Exists(HubPaths.ArtCacheDir)
                ? new DirectoryInfo(HubPaths.ArtCacheDir).EnumerateDirectories().Where(folder => !IsCustomArt(folder.FullName)).Sum(folder => FolderBytes(folder.FullName))
                : 0;
            string metaFile = Path.Combine(HubPaths.CacheDir, "meta.json");
            long known = artBytes + FolderBytes(customArt) + FolderBytes(HubPaths.IconCacheDir) + FileBytes(metaFile) + FolderBytes(HubPaths.WebViewDataDir);
            long total = FolderBytes(HubPaths.CacheDir);

            return new List<object>
            {
                new { key = "art", label = "Downloaded artwork", bytes = artBytes, clearable = true },
                new { key = "meta", label = "Game details", bytes = FileBytes(metaFile), clearable = true },
                new { key = "icons", label = "Icons", bytes = FolderBytes(HubPaths.IconCacheDir), clearable = false },
                new { key = "custom", label = "Your custom artwork", bytes = FolderBytes(customArt), clearable = false },
                new { key = "webview", label = "Browser data", bytes = FolderBytes(HubPaths.WebViewDataDir), clearable = false },
                new { key = "other", label = "Other data", bytes = Math.Max(0, total - known), clearable = false },
            };
        }

        private static bool IsCustomArt(string folder) => string.Equals(Path.GetFileName(folder), "custom", StringComparison.OrdinalIgnoreCase);

        private static long FileBytes(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

        private static long FolderBytes(string folder)
        {
            try
            {
                return Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private async Task SaveConfigListAsync(string key, List<string> items)
        {
            if (!ConfigLists.Contains(key))
            {
                Hub.Toast("That setting can't be changed here.", "error");
                return;
            }

            List<string> cleaned = items
                .Select(item => item?.Trim())
                .Where(item => !string.IsNullOrEmpty(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            try
            {
                HubConfig.Update(config => config[key] = cleaned.ToArray());
            }
            catch (Exception error)
            {
                DiagLog.Write("settings", "Saving config.json failed: " + error);
                Hub.Toast("Couldn't save config.json: " + error.Message, "error");
                return;
            }

            await PostInfoAsync();
            await Hub.RefreshLibraryAsync();
        }

        private async Task SetSyncIntervalAsync(string value)
        {
            try
            {
                string applied = await SettingsSyncTask.ApplyAsync(value);
                Hub.Post(new { type = "syncInterval", interval = applied, ok = true });
            }
            catch (Exception error)
            {
                DiagLog.Write("settings", "Changing the sync schedule failed: " + error.Message);
                Hub.Post(new { type = "syncInterval", interval = (string)null, ok = false, error = error.Message });
            }
        }

        private void PickFolder(Dictionary<string, object> message)
        {
            string requestId = message.GetString("requestId");
            string path = null;
            try
            {
                path = SettingsNative.PickFolder(Hub.Form.Handle, message.GetString("title") ?? "Choose a folder", message.GetString("initial"));
            }
            catch (Exception error)
            {
                DiagLog.Write("settings", "Folder picker failed: " + error.Message);
                using var fallback = new FolderBrowserDialog { Description = message.GetString("title") ?? "Choose a folder", ShowNewFolderButton = false };
                if (fallback.ShowDialog(Hub.Form) == DialogResult.OK) path = fallback.SelectedPath;
            }
            Hub.Post(new { type = "pickedPath", requestId, path });
        }

        private void PickFile(Dictionary<string, object> message)
        {
            string initial = message.GetString("initial");
            using var dialog = new OpenFileDialog
            {
                Title = message.GetString("title") ?? "Choose a file",
                Filter = string.IsNullOrEmpty(message.GetString("filter")) ? "All files|*.*" : message.GetString("filter"),
                CheckFileExists = true,
                InitialDirectory = !string.IsNullOrEmpty(initial) && Directory.Exists(initial) ? initial : "",
            };
            string path = null;
            try
            {
                if (dialog.ShowDialog(Hub.Form) == DialogResult.OK) path = dialog.FileName;
            }
            catch (ArgumentException)
            {
                dialog.Filter = "All files|*.*";
                if (dialog.ShowDialog(Hub.Form) == DialogResult.OK) path = dialog.FileName;
            }
            Hub.Post(new { type = "pickedPath", requestId = message.GetString("requestId"), path });
        }

        private void ApplyTheme(Dictionary<string, object> message)
        {
            Color background = ThemeNative.ParseColor(message.GetString("background")) ?? MainForm.Background;
            bool light = message.TryGetValue("light", out object value) && value is bool flag && flag;
            Hub.RunOnUi(() => Hub.Form.ApplyTheme(background, light));
        }

        private void PickThemeBackground()
        {
            string source;
            using (var dialog = new OpenFileDialog { Title = "Choose a background image", Filter = "Images (*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp", CheckFileExists = true })
            {
                if (dialog.ShowDialog(Hub.Form) != DialogResult.OK) return;
                source = dialog.FileName;
            }

            try
            {
                if (new FileInfo(source).Length > 40L * 1024 * 1024) throw new InvalidDataException("The image is larger than 40 MB.");
                string folder = Path.Combine(HubPaths.PublicCacheDir, "theme");
                Directory.CreateDirectory(folder);
                string extension = Path.GetExtension(source).ToLowerInvariant();
                string name = "background" + extension;
                File.Copy(source, Path.Combine(folder, name), true);
                string url = $"https://cache.hub/theme/{name}?v={DateTime.UtcNow.Ticks}";
                Hub.Post(new { type = "themeBackground", url, name = Path.GetFileName(source) });
            }
            catch (Exception error)
            {
                DiagLog.Write("settings", "Copying the background image failed: " + error.Message);
                Hub.Toast("Couldn't use that image: " + error.Message, "error");
            }
        }

        private static string ClientError(Dictionary<string, object> message)
        {
            string text = message.GetString("message") ?? "Unknown error";
            string source = message.GetString("source");
            string stack = message.GetString("stack");
            if (!string.IsNullOrEmpty(source))
            {
                object line = message.TryGetValue("line", out object value) ? value : null;
                text += $" ({Path.GetFileName(source)}{(line != null ? ":" + line : "")})";
            }
            if (!string.IsNullOrEmpty(stack)) text += "\n" + (stack.Length > 2000 ? stack.Substring(0, 2000) : stack);
            return text;
        }

        private async Task ClearCacheAsync(string kind)
        {
            try
            {
                switch (kind)
                {
                    case "art":
                        await ClearArtAsync();
                        break;
                    case "meta":
                        await ClearMetaAsync();
                        break;
                    case "log":
                        DiagLog.Clear();
                        break;
                    default:
                        return;
                }
                Hub.Toast(kind == "log" ? "Error log cleared" : "Cache moved to the Recycle Bin. It refills as you browse.");
            }
            catch (Exception error)
            {
                DiagLog.Write("settings", $"Clearing the {kind} cache failed: {error}");
                Hub.Toast("Couldn't clear the cache: " + error.Message, "error");
            }
            await PostInfoAsync();
        }

        private async Task ClearArtAsync()
        {
            if (Directory.Exists(HubPaths.ArtCacheDir))
            {
                foreach (string folder in Directory.GetDirectories(HubPaths.ArtCacheDir).Where(folder => !IsCustomArt(folder)))
                {
                    await UninstallService.RecycleAsync(folder, Hub.Form.Handle);
                }
            }

            if (ArtIndexAccess.TryGet(out ArtIndex index, out object gate))
            {
                lock (gate)
                {
                    index.Items.Clear();
                    index.Lookups.Clear();
                    Json.WriteFile(HubPaths.ArtIndexFile, index);
                }
            }
            await Hub.ReloadLibraryAsync();
        }

        private async Task ClearMetaAsync()
        {
            HubModule meta = Hub.Modules.FirstOrDefault(module => module.GetType().Name == "MetaModule");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo cacheField = meta?.GetType().GetField("cache", flags);
            object gate = meta?.GetType().GetField("sync", flags)?.GetValue(meta) ?? new object();
            if (cacheField != null)
            {
                lock (gate) cacheField.SetValue(meta, Activator.CreateInstance(cacheField.FieldType));
            }

            string metaFile = Path.Combine(HubPaths.CacheDir, "meta.json");
            if (File.Exists(metaFile)) await UninstallService.RecycleAsync(metaFile, Hub.Form.Handle);
            await Hub.ReloadLibraryAsync();
        }

        private static void OpenDiagFile(string file)
        {
            string path = file switch
            {
                "syncLog" => Path.Combine(HubPaths.SyncDir, "last-run.log"),
                "errors" => DiagLog.FilePath,
                "config" => HubPaths.ConfigFile,
                _ => null,
            };
            if (path == null || !File.Exists(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            catch (Exception error)
            {
                DiagLog.Write("settings", "Opening a file failed: " + error.Message);
            }
        }

        private void ExportData(Dictionary<string, object> message)
        {
            var local = message.TryGetValue("local", out object value) ? value as Dictionary<string, object> : null;
            using var dialog = new SaveFileDialog
            {
                Title = "Export Game Hub data",
                Filter = "Game Hub backup with artwork (*.zip)|*.zip|Game Hub data (*.json)|*.json",
                FileName = $"Game Hub backup {DateTime.Now:yyyy-MM-dd}",
                AddExtension = true,
                OverwritePrompt = true,
            };
            if (dialog.ShowDialog(Hub.Form) != DialogResult.OK) return;

            string path = dialog.FileName;
            if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) path += dialog.FilterIndex == 2 ? ".json" : ".zip";
            Dictionary<string, object> data = SettingsTransfer.BuildData(Hub, local);
            _ = Task.Run(() =>
            {
                try
                {
                    SettingsTransfer.Write(path, data);
                    Hub.Toast("Exported to " + Path.GetFileName(path));
                }
                catch (Exception error)
                {
                    DiagLog.Write("settings", "Export failed: " + error);
                    Hub.Toast("Export failed: " + error.Message, "error");
                }
            });
        }

        private async Task ImportDataAsync()
        {
            string path;
            using (var dialog = new OpenFileDialog { Title = "Import Game Hub data", Filter = "Game Hub backup (*.zip;*.json)|*.zip;*.json", CheckFileExists = true })
            {
                if (dialog.ShowDialog(Hub.Form) != DialogResult.OK) return;
                path = dialog.FileName;
            }

            try
            {
                SettingsBackup backup = await Task.Run(() => SettingsTransfer.Read(path));
                pendingImport = backup;
                object local = backup.Data.TryGetValue("local", out object value) ? value : null;
                Hub.Post(new { type = "importLoaded", summary = SettingsTransfer.Summarize(backup), local });
            }
            catch (Exception error)
            {
                DiagLog.Write("settings", "Import failed: " + error);
                Hub.Toast("Couldn't read that backup: " + error.Message, "error");
            }
        }

        private async Task ApplyImportAsync()
        {
            SettingsBackup backup = pendingImport;
            pendingImport = null;
            if (backup == null) return;
            try
            {
                string summary = await Task.Run(() => SettingsTransfer.Apply(Hub, backup));
                await Hub.ReloadLibraryAsync();
                Hub.Post(new { type = "importApplied", ok = true, message = summary });
            }
            catch (Exception error)
            {
                DiagLog.Write("settings", "Import failed: " + error);
                Hub.Post(new { type = "importApplied", ok = false, message = error.Message });
            }
        }

        private void InstallHub()
        {
            try
            {
                InstallService.Install();
                Hub.Toast("Game Hub is in the Start Menu and Installed apps");
            }
            catch (Exception error)
            {
                DiagLog.Write("install", error.ToString());
                Hub.Toast("Install failed: " + error.Message, "error");
            }
            Hub.Post(new { type = "installState", install = InstallService.Status() });
        }

        private async Task UninstallHubAsync()
        {
            try
            {
                await InstallService.UninstallAsync(Hub.Form.Handle);
                Hub.Toast("Removed from the Start Menu and Installed apps");
            }
            catch (Exception error)
            {
                DiagLog.Write("install", error.ToString());
                Hub.Toast("Couldn't remove Game Hub: " + error.Message, "error");
            }
            Hub.Post(new { type = "installState", install = InstallService.Status() });
        }

        private async Task CheckOnlineAsync()
        {
            bool online;
            try
            {
                using HttpResponseMessage response = await Http.GetAsync("http://www.msftconnecttest.com/connecttest.txt", HttpCompletionOption.ResponseHeadersRead);
                online = response.IsSuccessStatusCode;
            }
            catch
            {
                online = false;
            }
            Hub.Post(new { type = "online", online });
        }

        private static List<string> StringList(Dictionary<string, object> message, string key)
        {
            return message.TryGetValue(key, out object value) && value is object[] items
                ? items.OfType<string>().ToList()
                : new List<string>();
        }
    }
}
