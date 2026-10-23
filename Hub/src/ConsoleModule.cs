using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace GameHub
{
    internal sealed class ConsoleModule : HubModule
    {
        private const string MusicHost = "music.hub";
        private const int MaxTracks = 500;

        private static readonly HashSet<string> MusicExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".ogg", ".wav", ".m4a", ".flac", ".opus", ".aac", ".webm",
        };

        private static readonly string[] PictureExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        private readonly int ownProcessId = Process.GetCurrentProcess().Id;
        private string mappedMusicFolder;
        private string profileCache;
        private DateTime profileCheckedUtc;

        public override int Order => 70;

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "consoleStatus":
                    Task.Run(SendStatus);
                    return true;
                case "consoleVolume":
                    ChangeVolume(message);
                    return true;
                case "consoleMusicPick":
                    PickMusic(message.GetString("mode"));
                    return true;
                case "consoleMusicList":
                    ListMusic(false);
                    return true;
                case "consolePower":
                    Power(message.GetString("action"));
                    return true;
                case "consoleResume":
                    Resume(message.GetString("id"), ReadNumber(message, "pid"));
                    return true;
                default:
                    return false;
            }
        }

        private static double? ReadNumber(Dictionary<string, object> message, string key)
        {
            if (!message.TryGetValue(key, out object value) || value == null) return null;
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void SendStatus()
        {
            try
            {
                var pads = Hub.Module<XInputModule>()?.ConnectedPads() ?? new List<Dictionary<string, object>>();
                ConsoleNative.VolumeState volume = ConsoleNative.ChangeVolume(null, null, null);
                Hub.Post(new
                {
                    type = "consoleStatus",
                    pads,
                    network = NetworkStatus(),
                    volume = volume == null ? null : new { level = volume.Level, muted = volume.Muted },
                    profile = new { name = ConsoleNative.DisplayName(), picture = ProfilePicture() },
                });
            }
            catch (Exception error)
            {
                DiagLog.Write("console", "Status failed: " + error.Message);
            }
        }

        private static object NetworkStatus()
        {
            ConsoleNative.WifiConnection wifi = ConsoleNative.CurrentWifi();
            if (wifi != null) return new { kind = "wifi", signal = (int?)wifi.Signal, ssid = wifi.Ssid };

            try
            {
                var adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                                      adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                      adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                                      adapter.GetIPProperties().GatewayAddresses.Any())
                    .ToList();
                if (adapters.Any(adapter => adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    return new { kind = "wifi", signal = (int?)null, ssid = (string)null };
                if (adapters.Count > 0) return new { kind = "ethernet", signal = (int?)null, ssid = (string)null };
                return new { kind = "none", signal = (int?)null, ssid = (string)null };
            }
            catch (Exception)
            {
                return null;
            }
        }

        private string ProfilePicture()
        {
            if (profileCache != null && DateTime.UtcNow - profileCheckedUtc < TimeSpan.FromMinutes(10)) return profileCache;
            profileCheckedUtc = DateTime.UtcNow;
            try
            {
                string source = WindowsAccountPicture() ?? SteamAvatar();
                if (source == null) return profileCache = "";

                string folder = Path.Combine(HubPaths.PublicCacheDir, "console");
                Directory.CreateDirectory(folder);
                string extension = Path.GetExtension(source).ToLowerInvariant();
                string target = Path.Combine(folder, "avatar" + extension);
                foreach (string old in Directory.GetFiles(folder, "avatar.*"))
                {
                    if (!string.Equals(old, target, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
                }

                var info = new FileInfo(source);
                if (!File.Exists(target) || new FileInfo(target).Length != info.Length || File.GetLastWriteTimeUtc(target) < info.LastWriteTimeUtc)
                {
                    File.Copy(source, target, true);
                    File.SetLastWriteTimeUtc(target, info.LastWriteTimeUtc);
                }
                return profileCache = $"https://cache.hub/console/avatar{extension}?v={info.LastWriteTimeUtc.Ticks}";
            }
            catch (Exception error)
            {
                DiagLog.Write("console", "Profile picture failed: " + error.Message);
                return profileCache = "";
            }
        }

        private static string WindowsAccountPicture()
        {
            string sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid == null) return null;

            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\" + sid))
            {
                if (key != null)
                {
                    foreach (string name in new[] { "Image448", "Image424", "Image240", "Image208", "Image192", "Image96" })
                    {
                        if (key.GetValue(name) is string path && File.Exists(path)) return path;
                    }
                }
            }

            string publicFolder = Path.Combine(Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public", "AccountPictures", sid);
            if (Directory.Exists(publicFolder))
            {
                string best = Directory.GetFiles(publicFolder)
                    .Where(file => PictureExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .OrderByDescending(file => new FileInfo(file).Length)
                    .FirstOrDefault();
                if (best != null) return best;
            }
            return null;
        }

        private static string SteamAvatar()
        {
            string root = HubPaths.SteamRoot;
            if (root == null) return null;
            string folder = Path.Combine(root, "config", "avatarcache");
            if (!Directory.Exists(folder)) return null;

            string users = Path.Combine(root, "config", "loginusers.vdf");
            if (File.Exists(users))
            {
                string text = File.ReadAllText(users);
                foreach (Match match in Regex.Matches(text, "\"(\\d{17})\"\\s*\\{([^}]*)\\}"))
                {
                    if (!Regex.IsMatch(match.Groups[2].Value, "\"MostRecent\"\\s*\"1\"")) continue;
                    string avatar = Path.Combine(folder, match.Groups[1].Value + ".png");
                    if (File.Exists(avatar)) return avatar;
                }
            }

            return Directory.GetFiles(folder, "*.png")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private void ChangeVolume(Dictionary<string, object> message)
        {
            double? delta = ReadNumber(message, "delta");
            double? level = ReadNumber(message, "level");
            bool? mute = message.TryGetValue("mute", out object value) && value is bool flag ? flag : (bool?)null;

            Task.Run(() =>
            {
                ConsoleNative.VolumeState state = ConsoleNative.ChangeVolume(delta, level, mute);
                if (state == null)
                {
                    Hub.Toast("The volume could not be changed", "error");
                    return;
                }
                Hub.Post(new { type = "consoleVolume", level = state.Level, muted = state.Muted, show = level == null });
            });
        }

        private void PickMusic(string mode)
        {
            string current = Hub.Settings.GetString("console.musicPath", "");
            string initial = File.Exists(current) ? Path.GetDirectoryName(current) : Directory.Exists(current) ? current : null;
            string picked = null;

            try
            {
                if (mode == "file")
                {
                    using var dialog = new OpenFileDialog
                    {
                        Title = "Choose a music file",
                        Filter = "Audio files|" + string.Join(";", MusicExtensions.Select(extension => "*" + extension)) + "|All files|*.*",
                        CheckFileExists = true,
                    };
                    if (initial != null) dialog.InitialDirectory = initial;
                    if (dialog.ShowDialog(Hub.Form) == DialogResult.OK) picked = dialog.FileName;
                }
                else
                {
                    picked = SettingsNative.PickFolder(Hub.Form.Handle, "Choose a music folder", initial);
                }
            }
            catch (Exception error)
            {
                DiagLog.Write("console", "Music picker failed: " + error.Message);
                using var fallback = new FolderBrowserDialog { Description = "Choose a music folder", ShowNewFolderButton = false };
                if (fallback.ShowDialog(Hub.Form) == DialogResult.OK) picked = fallback.SelectedPath;
            }

            if (string.IsNullOrEmpty(picked)) return;
            Hub.Settings.Set("console.musicPath", picked);
            Hub.Settings.Set("console.music", true);
            Hub.Post(new { type = "settings", settings = Hub.Settings.Snapshot() });
            ListMusic(true);
        }

        private void ListMusic(bool picked)
        {
            string path = Hub.Settings.GetString("console.musicPath", "");
            Task.Run(() =>
            {
                string folder = null;
                List<string> files;
                try
                {
                    if (File.Exists(path))
                    {
                        folder = Path.GetDirectoryName(path);
                        files = MusicExtensions.Contains(Path.GetExtension(path)) ? new List<string> { path } : new List<string>();
                    }
                    else if (Directory.Exists(path))
                    {
                        folder = path;
                        files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(file => MusicExtensions.Contains(Path.GetExtension(file)))
                            .Take(MaxTracks)
                            .ToList();
                    }
                    else
                    {
                        files = new List<string>();
                    }
                }
                catch (Exception error)
                {
                    DiagLog.Write("console", "Listing music failed: " + error.Message);
                    files = new List<string>();
                }

                Hub.RunOnUi(() =>
                {
                    if (folder != null) MapMusicFolder(folder);
                    var tracks = folder == null ? new List<object>() : files.Select(file => (object)new
                    {
                        url = TrackUrl(folder, file),
                        name = Path.GetFileNameWithoutExtension(file),
                    }).ToList();
                    Hub.Post(new { type = "consoleMusic", tracks, path, picked });
                });
            });
        }

        private void MapMusicFolder(string folder)
        {
            CoreWebView2 web = Hub.Web;
            if (web == null || string.Equals(mappedMusicFolder, folder, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                if (mappedMusicFolder != null) web.ClearVirtualHostNameToFolderMapping(MusicHost);
                web.SetVirtualHostNameToFolderMapping(MusicHost, folder, CoreWebView2HostResourceAccessKind.Allow);
                mappedMusicFolder = folder;
            }
            catch (Exception error)
            {
                DiagLog.Write("console", "Mapping the music folder failed: " + error.Message);
            }
        }

        private static string TrackUrl(string folder, string file)
        {
            string relative = file.Substring(folder.TrimEnd('\\', '/').Length).TrimStart('\\', '/');
            return $"https://{MusicHost}/" + string.Join("/", relative.Split('\\', '/').Select(Uri.EscapeDataString));
        }

        private void Power(string action)
        {
            switch (action)
            {
                case "sleep":
                    Task.Run(() =>
                    {
                        if (!ConsoleNative.SetSuspendState(false, false, false)) Hub.Toast("Sleep is not available on this PC", "error");
                    });
                    break;
                case "restart":
                    RunShutdown("/r /t 0");
                    break;
                case "shutdown":
                    RunShutdown("/s /t 0");
                    break;
                case "signout":
                    RunShutdown("/l");
                    break;
                case "exit":
                    Hub.RunOnUi(Hub.Form.QuitApplication);
                    break;
            }
        }

        private void RunShutdown(string arguments)
        {
            try
            {
                string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
                using (Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false, CreateNoWindow = true }))
                {
                }
            }
            catch (Exception error)
            {
                DiagLog.Write("console", "Power action failed: " + error.Message);
                Hub.Toast("That power action failed", "error");
            }
        }

        private void Resume(string id, double? pid)
        {
            GameEntry game = string.IsNullOrEmpty(id) ? null : Hub.Find(id);
            var tracker = Hub.Module<PlayTracker>();
            var tracked = tracker?.Running().FirstOrDefault(entry => entry.Id == id);

            Task.Run(() =>
            {
                var processIds = new HashSet<uint>();
                if (pid.HasValue && pid.Value > 0) processIds.Add((uint)pid.Value);
                if (tracked != null)
                {
                    if (tracked.MainProcessId > 0) processIds.Add((uint)tracked.MainProcessId);
                    foreach (int processId in tracked.ProcessIds) processIds.Add((uint)processId);
                }
                if (processIds.Count == 0 && !string.IsNullOrEmpty(game?.InstallPath)) AddInstallProcesses(game.InstallPath, processIds);
                processIds.Remove((uint)ownProcessId);

                IntPtr window = IntPtr.Zero;
                if (processIds.Count > 0)
                {
                    List<IntPtr> windows = ConsoleNative.TopWindowsOf(processIds);
                    window = windows.FirstOrDefault(ConsoleNative.CoversMonitor);
                    if (window == IntPtr.Zero) window = windows.FirstOrDefault();
                }

                if (window == IntPtr.Zero)
                {
                    Hub.Toast("The game window could not be found", "error");
                    return;
                }
                Hub.RunOnUi(() => ConsoleNative.ForceForeground(window));
            });
        }

        private static void AddInstallProcesses(string installPath, HashSet<uint> processIds)
        {
            string prefix = installPath.TrimEnd('\\', '/') + "\\";
            foreach (Process process in Process.GetProcesses())
            {
                using (process)
                {
                    string path = ConsoleNative.ProcessPath((uint)process.Id);
                    if (path != null && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) processIds.Add((uint)process.Id);
                }
            }
        }
    }
}
