using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace GameHub
{
    internal sealed class DiscoveryWatcher : HubModule
    {
        private const string SettingKey = "discovery.watchFolders";
        private static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan LongestWait = TimeSpan.FromMinutes(10);
        private static readonly HashSet<string> TriggerExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".zip", ".rar", ".7z", ".iso",
        };

        private readonly object gate = new object();
        private readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        private Timer timer;
        private string watchedKey = "";
        private DateTime? firstTrigger;
        private DateTime lastActivity;

        public override void Start()
        {
            timer = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);
            Hub.Settings.Changed += (key, value) =>
            {
                if (key == SettingKey) Rebuild(true);
            };
        }

        public override void OnLibraryLoaded(LibraryDocument library) => Rebuild(false);

        public override void Stop()
        {
            lock (gate)
            {
                DisposeWatchers();
                timer?.Dispose();
                timer = null;
            }
        }

        private void Rebuild(bool force)
        {
            bool enabled = Hub.Settings.GetBool(SettingKey, true);
            List<string> folders = enabled ? WatchedFolders() : new List<string>();
            string key = string.Join("|", folders);

            lock (gate)
            {
                if (!force && key == watchedKey) return;
                watchedKey = key;
                DisposeWatchers();
                foreach (string folder in folders) AddWatchers(folder);
            }
        }

        private static List<string> WatchedFolders()
        {
            Dictionary<string, object> config = HubConfig.Load();
            return HubConfig.GetList(config, "LibraryFolders")
                .Concat(HubConfig.GetList(config, "SearchFolders"))
                .Select(DiscoveryModule.ExpandPath)
                .Where(folder => !string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void AddWatchers(string folder)
        {
            try
            {
                var top = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
                    InternalBufferSize = 16384,
                };
                top.Created += (sender, args) => OnTopLevel(args.FullPath);
                top.Renamed += (sender, args) => OnTopLevel(args.FullPath);
                top.EnableRaisingEvents = true;
                watchers.Add(top);

                var deep = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.Size,
                    InternalBufferSize = 16384,
                };
                deep.Created += (sender, args) => OnActivity();
                deep.Changed += (sender, args) => OnActivity();
                deep.Renamed += (sender, args) => OnActivity();
                deep.EnableRaisingEvents = true;
                watchers.Add(deep);
            }
            catch (Exception error)
            {
                DiagLog.Write("discovery", $"Could not watch {folder}: {error.Message}");
            }
        }

        private void DisposeWatchers()
        {
            foreach (FileSystemWatcher watcher in watchers)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch
                {
                }
            }
            watchers.Clear();
        }

        private void OnTopLevel(string path)
        {
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name) || name.StartsWith(".") || name.StartsWith("~")) return;

            bool relevant;
            try
            {
                relevant = Directory.Exists(path) || TriggerExtensions.Contains(Path.GetExtension(path));
            }
            catch
            {
                relevant = false;
            }
            if (!relevant) return;

            lock (gate)
            {
                firstTrigger ??= DateTime.UtcNow;
                lastActivity = DateTime.UtcNow;
                timer?.Change(QuietPeriod, Timeout.InfiniteTimeSpan);
            }
        }

        private void OnActivity()
        {
            lock (gate)
            {
                if (firstTrigger == null) return;
                lastActivity = DateTime.UtcNow;
                if (DateTime.UtcNow - firstTrigger.Value < LongestWait) timer?.Change(QuietPeriod, Timeout.InfiniteTimeSpan);
            }
        }

        private void OnTimer(object state)
        {
            lock (gate)
            {
                if (firstTrigger == null) return;
                bool quiet = DateTime.UtcNow - lastActivity >= QuietPeriod - TimeSpan.FromSeconds(1);
                bool waitedTooLong = DateTime.UtcNow - firstTrigger.Value >= LongestWait;
                if (!quiet && !waitedTooLong)
                {
                    timer?.Change(QuietPeriod, Timeout.InfiniteTimeSpan);
                    return;
                }
                firstTrigger = null;
            }
            DiagLog.Write("discovery", "New files in a game folder, refreshing the library");
            Hub.RunOnUi(() => _ = Hub.RefreshLibraryAsync());
        }
    }
}
