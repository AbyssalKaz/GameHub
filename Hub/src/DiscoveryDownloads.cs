using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace GameHub
{
    internal sealed class DiscoveryDownloads : HubModule
    {
        private const long FullyInstalled = 4;
        private const long UpdateRequired = 2;
        private const long UpdatePaused = 512;
        private const long ActiveFlags = 256 | 512 | 1024 | 262144 | 524288 | 1048576 | 2097152 | 4194304;
        private static readonly TimeSpan FastPoll = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SlowPoll = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan RefreshSpacing = TimeSpan.FromMinutes(2);

        private readonly object gate = new object();
        private readonly Dictionary<string, DateTime> manifestTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> lastStates = new Dictionary<string, string>();
        private Timer timer;
        private List<string> libraries = new List<string>();
        private bool polling;
        private bool primed;
        private DateTime lastRefresh = DateTime.MinValue;

        public override void Start()
        {
            timer = new Timer(Poll, null, TimeSpan.FromSeconds(15), SlowPoll);
        }

        public override void OnLibraryLoaded(LibraryDocument library)
        {
            libraries = SteamLibraries();
            bool active = library.Games.Any(game => game.Source == "Steam" && (game.Download != null || game.Status == "downloading"));
            lock (gate)
            {
                lastStates.Clear();
                foreach (GameEntry game in library.Games.Where(game => game.Source == "Steam" && game.SteamAppId != null))
                {
                    lastStates[game.SteamAppId] = StateKey(game.Status, game.Download, game.NeedsUpdate);
                }
                timer?.Change(active ? FastPoll : SlowPoll, active ? FastPoll : SlowPoll);
            }
        }

        public override void Stop()
        {
            lock (gate)
            {
                timer?.Dispose();
                timer = null;
            }
        }

        private void Poll(object state)
        {
            lock (gate)
            {
                if (polling) return;
                polling = true;
            }

            try
            {
                var updates = new List<SteamManifestState>();
                bool needsRefresh = false;
                bool anyActive = false;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string library in libraries)
                {
                    string apps = Path.Combine(library, "steamapps");
                    if (!Directory.Exists(apps)) continue;

                    foreach (string manifest in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
                    {
                        seen.Add(manifest);
                        DateTime written = File.GetLastWriteTimeUtc(manifest);
                        bool known = manifestTimes.TryGetValue(manifest, out DateTime previous);
                        if (known && previous == written)
                        {
                            if (IsActive(manifest)) anyActive = true;
                            continue;
                        }
                        manifestTimes[manifest] = written;

                        SteamManifestState parsed = Read(manifest);
                        if (parsed == null) continue;
                        if (parsed.Active) anyActive = true;
                        activeManifests[manifest] = parsed.Active;

                        string key = StateKey(parsed.Status, parsed.Download, parsed.NeedsUpdate);
                        string last;
                        lock (gate) lastStates.TryGetValue(parsed.AppId, out last);
                        if (!primed || key == last) continue;
                        lock (gate) lastStates[parsed.AppId] = key;

                        if (last == null || (last.StartsWith("downloading") && parsed.Status == "installed"))
                        {
                            if (parsed.Status != null) needsRefresh = true;
                            continue;
                        }
                        updates.Add(parsed);
                    }
                }

                foreach (string gone in manifestTimes.Keys.Where(path => !seen.Contains(path)).ToList())
                {
                    manifestTimes.Remove(gone);
                    activeManifests.Remove(gone);
                    if (primed) needsRefresh = true;
                }

                primed = true;
                if (updates.Count > 0) Apply(updates);
                if (needsRefresh && DateTime.UtcNow - lastRefresh > RefreshSpacing)
                {
                    lastRefresh = DateTime.UtcNow;
                    Hub.RunOnUi(() => _ = Hub.RefreshLibraryAsync());
                }

                lock (gate) timer?.Change(anyActive ? FastPoll : SlowPoll, anyActive ? FastPoll : SlowPoll);
            }
            catch (Exception error)
            {
                DiagLog.Write("discovery", "Download check failed: " + error.Message);
            }
            finally
            {
                lock (gate) polling = false;
            }
        }

        private readonly Dictionary<string, bool> activeManifests = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        private bool IsActive(string manifest) => activeManifests.TryGetValue(manifest, out bool active) && active;

        private void Apply(List<SteamManifestState> updates)
        {
            Hub.RunOnUi(() =>
            {
                foreach (SteamManifestState update in updates)
                {
                    GameEntry game = Hub.Find("steam:" + update.AppId);
                    if (game == null) continue;
                    game.Download = update.Download;
                    game.NeedsUpdate = update.NeedsUpdate;
                    if (update.Status != null) game.Status = update.Status;
                }
                Hub.Post(new
                {
                    type = "discovery.downloads",
                    games = updates.Select(update => new { id = "steam:" + update.AppId, status = update.Status, download = update.Download, needsUpdate = update.NeedsUpdate }).ToList(),
                });
            });
        }

        private static string StateKey(string status, GameDownload download, bool? needsUpdate)
        {
            if (download == null) return $"{status}|{needsUpdate}";
            return $"{status}|{needsUpdate}|{download.State}|{download.BytesDone}|{download.BytesTotal}";
        }

        private static List<string> SteamLibraries()
        {
            var result = new List<string>();
            string root = HubPaths.SteamRoot;
            if (root == null) return result;
            result.Add(root);

            string file = Path.Combine(root, @"steamapps\libraryfolders.vdf");
            try
            {
                if (File.Exists(file))
                {
                    foreach (Match match in Regex.Matches(File.ReadAllText(file, Encoding.UTF8), "\"path\"\\s+\"([^\"]+)\""))
                    {
                        result.Add(Path.GetFullPath(match.Groups[1].Value.Replace(@"\\", @"\")).TrimEnd('\\'));
                    }
                }
            }
            catch
            {
            }
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static SteamManifestState Read(string manifest)
        {
            Dictionary<string, object> state;
            try
            {
                using var stream = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                state = Vdf.Navigate(Vdf.Parse(reader.ReadToEnd()), "AppState");
            }
            catch
            {
                return null;
            }
            if (state == null) return null;

            string appId = state.GetString("appid");
            long flags = Number(state, "StateFlags");
            if (string.IsNullOrEmpty(appId)) return null;

            bool installed = (flags & FullyInstalled) != 0;
            GameDownload download = DownloadOf(state, flags);
            return new SteamManifestState
            {
                AppId = appId,
                Active = (flags & ActiveFlags) != 0 && (flags & UpdatePaused) == 0,
                Status = installed ? "installed" : download != null ? "downloading" : null,
                Download = download,
                NeedsUpdate = installed && (flags & UpdateRequired) != 0 ? true : (bool?)null,
            };
        }

        internal static GameDownload DownloadOf(Dictionary<string, object> state, long flags)
        {
            bool installed = (flags & FullyInstalled) != 0;
            if (installed && (flags & ActiveFlags) == 0) return null;

            long toDownload = Number(state, "BytesToDownload");
            long downloaded = Number(state, "BytesDownloaded");
            long toStage = Number(state, "BytesToStage");
            long staged = Number(state, "BytesStaged");
            if (!installed && (flags & ActiveFlags) == 0 && toDownload == 0) return null;

            bool staging = toDownload > 0 && downloaded >= toDownload && toStage > 0;
            long done = staging ? staged : downloaded;
            long total = staging ? toStage : toDownload;
            if (installed && total > 0 && done >= total) return null;

            string label = "queued";
            if ((flags & UpdatePaused) != 0) label = "paused";
            else if (staging || (flags & (2097152 | 4194304)) != 0) label = "installing";
            else if ((flags & (256 | 1048576 | 262144 | 524288)) != 0) label = "downloading";

            return new GameDownload
            {
                Progress = total > 0 ? Math.Round(Math.Min(1.0, (double)done / total), 4) : (double?)null,
                BytesDone = total > 0 ? done : (long?)null,
                BytesTotal = total > 0 ? total : (long?)null,
                State = label,
            };
        }

        private static long Number(Dictionary<string, object> values, string key)
        {
            return long.TryParse(values.GetString(key), out long number) ? number : 0;
        }

        private sealed class SteamManifestState
        {
            public string AppId { get; set; }
            public bool Active { get; set; }
            public string Status { get; set; }
            public GameDownload Download { get; set; }
            public bool? NeedsUpdate { get; set; }
        }
    }
}
