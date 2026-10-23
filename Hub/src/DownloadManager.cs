using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;

namespace GameHub
{
    internal sealed class DownloadItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Uri { get; set; }
        public string SourceHost { get; set; }
        public string State { get; set; }
        public string Error { get; set; }
        public long Received { get; set; }
        public long Total { get; set; } = -1;
        public double Speed { get; set; }
        public long StartedAt { get; set; }
        public long FinishedAt { get; set; }
        public bool Risky { get; set; }
        public bool CanResume { get; set; }
        public string Scan { get; set; }
        public string ScanDetail { get; set; }

        [ScriptIgnore] public CoreWebView2DownloadOperation Operation { get; set; }
        [ScriptIgnore] public long SampleBytes { get; set; }
        [ScriptIgnore] public DateTime SampleTime { get; set; }

        [ScriptIgnore] public bool IsActive => State == "downloading" || State == "paused" || State == "interrupted" || State == "pending";
    }

    internal sealed class DownloadManager
    {
        private const int KeptItems = 80;
        private static readonly string ListFile = Path.Combine(HubPaths.CacheDir, "store-downloads.json");
        private static readonly HashSet<string> RiskyExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".msix", ".msixbundle", ".appx", ".zip", ".rar", ".7z", ".bat", ".cmd", ".scr", ".ps1", ".vbs", ".jar", ".iso",
        };
        private static readonly HashSet<string> LibraryExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".zip", ".rar", ".7z", ".iso",
        };

        private sealed class PendingDownload
        {
            public CoreWebView2DownloadStartingEventArgs Args;
            public CoreWebView2Deferral Deferral;
            public DownloadItem Item;
        }

        private readonly HubContext hub;
        private readonly List<DownloadItem> items;
        private readonly Dictionary<string, PendingDownload> pending = new Dictionary<string, PendingDownload>();
        private readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer { Interval = 500 };
        private readonly System.Windows.Forms.Timer refreshDelay = new System.Windows.Forms.Timer { Interval = 6000 };

        public event Action<string> RetryRequested;

        public DownloadManager(HubContext hub)
        {
            this.hub = hub;
            items = Json.ReadFile<List<DownloadItem>>(ListFile) ?? new List<DownloadItem>();
            foreach (DownloadItem item in items.Where(entry => entry.IsActive))
            {
                item.State = "failed";
                item.Error = "Stopped when Game Hub closed";
                item.CanResume = false;
                item.Speed = 0;
            }
            foreach (DownloadItem item in items.Where(entry => entry.Scan == "scanning")) item.Scan = null;

            ticker.Tick += (sender, args) => Tick();
            refreshDelay.Tick += (sender, args) =>
            {
                refreshDelay.Stop();
                _ = hub.RefreshLibraryAsync();
            };
        }

        public int ActiveCount => items.Count(item => item.State == "downloading");

        public string DefaultFolder
        {
            get
            {
                string chosen = hub.Settings.GetString("store.downloadFolder");
                if (!string.IsNullOrWhiteSpace(chosen) && Directory.Exists(chosen)) return chosen;
                return KnownDownloadsFolder();
            }
        }

        public void Attach(CoreWebView2 core, Func<string> pageHost)
        {
            core.DownloadStarting += (sender, args) => OnDownloadStarting(args, pageHost());
        }

        public object Snapshot() => new
        {
            type = "downloads",
            items = items.ToList(),
            folder = DefaultFolder,
            scanner = DownloadScanner.Available,
        };

        public void PostList() => hub.Post(Snapshot());

        public List<object> FolderChoices() => FolderPaths().Select(choice => (object)new { label = choice.Label, path = choice.Path }).ToList();

        private List<(string Label, string Path)> FolderPaths()
        {
            var choices = new List<(string Label, string Path)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string downloads = DefaultFolder;
            seen.Add(downloads.TrimEnd('\\'));
            choices.Add(("Download folder", downloads));
            try
            {
                foreach (string raw in HubConfig.GetList(HubConfig.Load(), "LibraryFolders"))
                {
                    string folder = Environment.ExpandEnvironmentVariables(raw ?? "").Trim();
                    if (folder.Length == 0 || !Directory.Exists(folder) || !seen.Add(folder.TrimEnd('\\'))) continue;
                    choices.Add(("Library folder", folder));
                }
            }
            catch
            {
            }
            return choices;
        }

        private void OnDownloadStarting(CoreWebView2DownloadStartingEventArgs args, string pageHost)
        {
            args.Handled = true;
            CoreWebView2DownloadOperation operation = args.DownloadOperation;
            string fileName = SafeFileName(System.IO.Path.GetFileName(args.ResultFilePath ?? ""));
            string sourceHost = !string.IsNullOrEmpty(pageHost) ? pageHost : StoreDomains.HostOf(operation.Uri);
            string extension = System.IO.Path.GetExtension(fileName);
            bool risky = RiskyExtensions.Contains(extension) && !StoreDomains.IsOfficial(sourceHost);

            var item = new DownloadItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = fileName,
                Uri = operation.Uri,
                SourceHost = sourceHost,
                State = "pending",
                Total = operation.TotalBytesToReceive.HasValue ? (long)operation.TotalBytesToReceive.Value : -1,
                StartedAt = Now(),
                Risky = risky,
            };

            bool ask = risky || hub.Settings.GetString("store.downloadPrompt") == "always";
            if (!ask)
            {
                Begin(args, item, DefaultFolder);
                return;
            }

            pending[item.Id] = new PendingDownload { Args = args, Deferral = args.GetDeferral(), Item = item };
            hub.Post(new
            {
                type = "downloadConfirm",
                id = item.Id,
                name = item.Name,
                host = sourceHost,
                size = item.Total,
                risky,
                folders = FolderChoices(),
                folder = DefaultFolder,
            });
        }

        public void Decide(string id, string folder)
        {
            if (id == null || !pending.TryGetValue(id, out PendingDownload request)) return;
            pending.Remove(id);
            try
            {
                bool allowed = !string.IsNullOrEmpty(folder) && FolderPaths().Any(choice => string.Equals(choice.Path, folder, StringComparison.OrdinalIgnoreCase));
                if (allowed) Begin(request.Args, request.Item, folder);
                else request.Args.Cancel = true;
            }
            catch (Exception error)
            {
                request.Args.Cancel = true;
                hub.Toast("Couldn't start the download: " + error.Message, "error");
            }
            finally
            {
                request.Deferral.Complete();
            }
        }

        private void Begin(CoreWebView2DownloadStartingEventArgs args, DownloadItem item, string folder)
        {
            Directory.CreateDirectory(folder);
            string path = UniquePath(folder, item.Name);
            args.ResultFilePath = path;
            item.Path = path;
            item.Name = System.IO.Path.GetFileName(path);
            item.State = "downloading";
            item.SampleTime = DateTime.UtcNow;

            CoreWebView2DownloadOperation operation = args.DownloadOperation;
            item.Operation = operation;
            operation.StateChanged += (sender, unused) => OnStateChanged(item);
            operation.BytesReceivedChanged += (sender, unused) =>
            {
                item.Received = operation.BytesReceived;
                if (operation.TotalBytesToReceive.HasValue) item.Total = (long)operation.TotalBytesToReceive.Value;
            };

            items.Insert(0, item);
            Trim();
            Save();
            PostList();
            ticker.Start();
        }

        private void OnStateChanged(DownloadItem item)
        {
            CoreWebView2DownloadOperation operation = item.Operation;
            if (operation == null) return;

            switch (operation.State)
            {
                case CoreWebView2DownloadState.InProgress:
                    item.State = "downloading";
                    item.Error = null;
                    item.SampleBytes = operation.BytesReceived;
                    item.SampleTime = DateTime.UtcNow;
                    ticker.Start();
                    break;
                case CoreWebView2DownloadState.Interrupted:
                {
                    CoreWebView2DownloadInterruptReason reason = operation.InterruptReason;
                    item.Speed = 0;
                    item.CanResume = operation.CanResume;
                    if (reason == CoreWebView2DownloadInterruptReason.UserPaused) item.State = "paused";
                    else if (reason == CoreWebView2DownloadInterruptReason.UserCanceled) item.State = "cancelled";
                    else
                    {
                        item.State = operation.CanResume ? "interrupted" : "failed";
                        item.Error = DescribeInterrupt(reason);
                    }
                    break;
                }
                case CoreWebView2DownloadState.Completed:
                    item.State = "completed";
                    item.Speed = 0;
                    item.Received = operation.BytesReceived;
                    item.Total = item.Received;
                    item.Path = operation.ResultFilePath;
                    item.Name = System.IO.Path.GetFileName(item.Path);
                    item.FinishedAt = Now();
                    item.Operation = null;
                    OnCompleted(item);
                    break;
            }
            Save();
            PostList();
        }

        private void OnCompleted(DownloadItem item)
        {
            hub.Toast($"Downloaded {item.Name}", "info");
            if (System.Windows.Forms.Form.ActiveForm != hub.Form) hub.Notify("Download finished", item.Name, hub.ShowWindow);

            if (LibraryExtensions.Contains(System.IO.Path.GetExtension(item.Name)))
            {
                refreshDelay.Stop();
                refreshDelay.Start();
            }

            if (hub.Settings.GetBool("store.scanDownloads", true)) _ = ScanAsync(item);
        }

        public async Task ScanAsync(DownloadItem item)
        {
            if (item.Path == null || !File.Exists(item.Path)) return;
            item.Scan = "scanning";
            item.ScanDetail = null;
            PostList();

            DownloadScanResult result = await Task.Run(() => DownloadScanner.Scan(item.Path));
            item.Scan = result.Status;
            item.ScanDetail = result.Detail;
            Save();
            PostList();

            if (result.Status == "threat")
            {
                hub.Toast($"Windows Defender found a threat in {item.Name}", "error");
                hub.Notify("Threat found in a download", $"{item.Name}: {result.Detail}", hub.ShowWindow);
            }
        }

        private void Tick()
        {
            DateTime now = DateTime.UtcNow;
            bool anyActive = false;
            foreach (DownloadItem item in items.Where(entry => entry.State == "downloading"))
            {
                anyActive = true;
                double seconds = (now - item.SampleTime).TotalSeconds;
                if (seconds < 0.2) continue;
                double instant = Math.Max(0, item.Received - item.SampleBytes) / seconds;
                item.Speed = item.Speed <= 0 ? instant : item.Speed * 0.6 + instant * 0.4;
                item.SampleBytes = item.Received;
                item.SampleTime = now;
            }
            if (!anyActive) ticker.Stop();
            PostList();
        }

        public void Handle(string action, string id)
        {
            DownloadItem item = items.FirstOrDefault(entry => entry.Id == id);
            switch (action)
            {
                case "pause":
                    if (item?.Operation != null && item.State == "downloading") item.Operation.Pause();
                    break;
                case "resume":
                    if (item?.Operation != null && item.Operation.CanResume) item.Operation.Resume();
                    else if (item != null && item.Uri != null) Retry(item);
                    break;
                case "cancel":
                    item?.Operation?.Cancel();
                    break;
                case "retry":
                    if (item != null) Retry(item);
                    break;
                case "open":
                    Open(item);
                    break;
                case "folder":
                    ShowInFolder(item);
                    break;
                case "scan":
                    if (item != null && item.State == "completed") _ = ScanAsync(item);
                    break;
                case "remove":
                    if (item == null) break;
                    if (item.Operation != null && item.IsActive) item.Operation.Cancel();
                    items.Remove(item);
                    Save();
                    PostList();
                    break;
                case "clear":
                    items.RemoveAll(entry => !entry.IsActive);
                    Save();
                    PostList();
                    break;
                case "recycle":
                    _ = RecycleAsync(item);
                    break;
                case "list":
                    PostList();
                    break;
            }
        }

        private void Retry(DownloadItem item)
        {
            if (item.Operation != null && item.IsActive) item.Operation.Cancel();
            items.Remove(item);
            Save();
            PostList();
            RetryRequested?.Invoke(item.Uri);
        }

        private void Open(DownloadItem item)
        {
            if (item?.Path == null || !File.Exists(item.Path))
            {
                hub.Toast("That file isn't there anymore.", "error");
                return;
            }
            if (item.Scan == "threat")
            {
                hub.Toast("Windows Defender flagged this file, so Game Hub won't open it.", "error");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true, WorkingDirectory = System.IO.Path.GetDirectoryName(item.Path) });
            }
            catch (Exception error)
            {
                hub.Toast(error.Message, "error");
            }
        }

        private void ShowInFolder(DownloadItem item)
        {
            if (item?.Path == null) return;
            try
            {
                if (File.Exists(item.Path)) Process.Start("explorer.exe", $"/select,\"{item.Path}\"");
                else if (Directory.Exists(System.IO.Path.GetDirectoryName(item.Path))) Process.Start("explorer.exe", $"\"{System.IO.Path.GetDirectoryName(item.Path)}\"");
                else hub.Toast("That folder isn't there anymore.", "error");
            }
            catch (Exception error)
            {
                hub.Toast(error.Message, "error");
            }
        }

        private async Task RecycleAsync(DownloadItem item)
        {
            if (item?.Path == null || item.IsActive || !File.Exists(item.Path)) return;
            bool recycled = await UninstallService.RecycleAsync(item.Path, hub.Form.Handle);
            if (recycled || !File.Exists(item.Path))
            {
                item.State = "recycled";
                hub.Toast($"Moved {item.Name} to the Recycle Bin", "info");
            }
            else
            {
                hub.Toast($"Couldn't move {item.Name} to the Recycle Bin", "error");
            }
            Save();
            PostList();
        }

        private void Trim()
        {
            while (items.Count > KeptItems)
            {
                int index = items.FindLastIndex(entry => !entry.IsActive);
                if (index < 0) break;
                items.RemoveAt(index);
            }
        }

        private void Save()
        {
            try
            {
                Json.WriteFile(ListFile, items);
            }
            catch
            {
            }
        }

        private static string DescribeInterrupt(CoreWebView2DownloadInterruptReason reason)
        {
            string name = reason.ToString();
            if (name.StartsWith("Network", StringComparison.Ordinal)) return "Network problem";
            if (name.StartsWith("Server", StringComparison.Ordinal)) return "The server refused the download";
            if (name == "FileNoSpace") return "The disk is full";
            if (name == "FileAccessDenied") return "Access to the folder was denied";
            if (name == "FileBlocked" || name == "FileSecurityCheckFailed" || name == "FileFailedVirusScan") return "Windows blocked the file";
            if (name == "FileTooLarge") return "The file is too large for this drive";
            if (name == "FileNameTooLong") return "The file name is too long";
            return "The download stopped";
        }

        private static string SafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "download";
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
            return name.Trim();
        }

        private static string UniquePath(string folder, string name)
        {
            string path = System.IO.Path.Combine(folder, name);
            string stem = System.IO.Path.GetFileNameWithoutExtension(name);
            string extension = System.IO.Path.GetExtension(name);
            for (int index = 1; File.Exists(path) || File.Exists(path + ".crdownload"); index++)
            {
                path = System.IO.Path.Combine(folder, $"{stem} ({index}){extension}");
            }
            return path;
        }

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid folderId, uint flags, IntPtr token, out IntPtr path);

        public static string KnownDownloadsFolder()
        {
            try
            {
                if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out IntPtr pointer) == 0)
                {
                    string path = Marshal.PtrToStringUni(pointer);
                    Marshal.FreeCoTaskMem(pointer);
                    if (!string.IsNullOrEmpty(path)) return path;
                }
            }
            catch
            {
            }
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }

    internal sealed class DownloadScanResult
    {
        public string Status { get; set; }
        public string Detail { get; set; }
    }

    internal static class DownloadScanner
    {
        public static bool Available => FindScanner() != null;

        private static string FindScanner()
        {
            string classic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windows Defender", "MpCmdRun.exe");
            if (File.Exists(classic)) return classic;
            try
            {
                string platform = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows Defender\Platform");
                if (Directory.Exists(platform))
                {
                    return Directory.GetDirectories(platform)
                        .OrderByDescending(folder => folder, StringComparer.OrdinalIgnoreCase)
                        .Select(folder => Path.Combine(folder, "MpCmdRun.exe"))
                        .FirstOrDefault(File.Exists);
                }
            }
            catch
            {
            }
            return null;
        }

        public static DownloadScanResult Scan(string file)
        {
            string scanner = FindScanner();
            if (scanner == null) return new DownloadScanResult { Status = "unavailable", Detail = "Windows Defender isn't available on this PC" };

            try
            {
                var start = new ProcessStartInfo(scanner, $"-Scan -ScanType 3 -File \"{file}\" -DisableRemediation")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using Process process = Process.Start(start);
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(15 * 60 * 1000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }
                    return new DownloadScanResult { Status = "error", Detail = "The scan took too long" };
                }
                string text = output.Result + "\n" + errors.Result;

                switch (process.ExitCode)
                {
                    case 0:
                        return new DownloadScanResult { Status = "clean", Detail = "No threats found" };
                    case 2:
                        return new DownloadScanResult { Status = "threat", Detail = ThreatName(text) ?? "Threat detected" };
                    default:
                        bool unavailable = text.IndexOf("0x800106ba", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("not running", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) >= 0;
                        return unavailable
                            ? new DownloadScanResult { Status = "unavailable", Detail = "Windows Defender is turned off or replaced by another antivirus" }
                            : new DownloadScanResult { Status = "error", Detail = $"The scan failed (code 0x{process.ExitCode:X})" };
                }
            }
            catch (Exception error)
            {
                return new DownloadScanResult { Status = "error", Detail = error.Message };
            }
        }

        private static string ThreatName(string output)
        {
            foreach (string line in output.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("Threat ", StringComparison.OrdinalIgnoreCase) && trimmed.Contains(":"))
                {
                    string value = trimmed.Substring(trimmed.IndexOf(':') + 1).Trim();
                    if (value.Length > 0) return value;
                }
            }
            return null;
        }
    }
}
