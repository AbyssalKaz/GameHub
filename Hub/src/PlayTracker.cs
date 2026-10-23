using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class PlayRunningGame
    {
        public string Id { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public List<int> ProcessIds { get; set; } = new List<int>();
        public int MainProcessId { get; set; }

        public PlayRunningGame Copy() => new PlayRunningGame
        {
            Id = Id,
            StartedUtc = StartedUtc,
            LastSeenUtc = LastSeenUtc,
            ProcessIds = ProcessIds.ToList(),
            MainProcessId = MainProcessId,
        };
    }

    internal sealed class PlayTracker : HubModule
    {
        private const int PollMilliseconds = 5000;
        private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(12);
        private static readonly TimeSpan ResumeWindow = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan ActiveSaveInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan AchievementDelay = TimeSpan.FromSeconds(12);

        private static readonly HashSet<string> UntrackedByDefault = new HashSet<string> { "431960", "1070560", "228980" };

        private readonly DateTime trackerStartedUtc = DateTime.UtcNow;
        private readonly object gate = new object();
        private readonly Dictionary<int, CachedProcess> processes = new Dictionary<int, CachedProcess>();
        private readonly Dictionary<string, PlayRunningGame> running = new Dictionary<string, PlayRunningGame>();
        private readonly int ownProcessId = Process.GetCurrentProcess().Id;

        private PlayMatcher matcher = PlayMatcher.Empty;
        private int matcherVersion;
        private bool libraryReady;
        private List<PlayActiveSession> restored;
        private Timer timer;
        private int ticking;
        private DateTime lastActiveSave = DateTime.MinValue;
        private string runningSignature = "";
        private Dictionary<string, int> reported = new Dictionary<string, int>();
        private Dictionary<string, PlayAchievementProgress> achievements = new Dictionary<string, PlayAchievementProgress>();

        public PlayLogStore Log { get; } = new PlayLogStore();

        public event Action RunningChanged;

        public event Action<PlaySession> SessionEnded;

        public event Action<string> GameStarted;

        public override int Order => 50;

        public List<PlayRunningGame> Running()
        {
            lock (gate) return running.Values.Select(game => game.Copy()).OrderBy(game => game.StartedUtc).ToList();
        }

        public bool IsRunning(string id)
        {
            lock (gate) return id != null && running.ContainsKey(id);
        }

        public int TrackedMinutes(string id) => Log.MinutesByGame().TryGetValue(id ?? "", out int minutes) ? minutes : 0;

        public override void Start()
        {
            restored = Log.TakeActive();
            timer = new Timer(_ => Tick(), null, 2000, PollMilliseconds);
        }

        public override void Stop()
        {
            timer?.Dispose();
            timer = null;
            SaveActive(DateTime.UtcNow, true);
        }

        public override void OnLibraryLoaded(LibraryDocument library)
        {
            matcher = PlayMatcher.Build(library.Games.Where(IsTracked));
            Interlocked.Increment(ref matcherVersion);
            libraryReady = true;
        }

        public bool IsTracked(GameEntry game)
        {
            return Hub.UserData.Get(game.Id, "play.tracked") is bool tracked ? tracked : !UntrackedByDefault.Contains(game.SteamAppId ?? "");
        }

        public override void Decorate(LibraryDocument library)
        {
            Dictionary<string, int> tracked = Log.MinutesByGame();
            Dictionary<string, string> lastEnds = Log.LastEndByGame();
            var reportedNow = new Dictionary<string, int>();

            foreach (GameEntry game in library.Games)
            {
                int launcherMinutes = game.PlaytimeMinutes ?? 0;
                if (launcherMinutes > 0) reportedNow[game.Id] = launcherMinutes;
                if (tracked.TryGetValue(game.Id, out int minutes) && minutes > launcherMinutes) game.PlaytimeMinutes = minutes;
                if (lastEnds.TryGetValue(game.Id, out string lastEnd)) game.LastPlayed = Latest(game.LastPlayed, lastEnd);
            }
            reported = reportedNow;

            var steamGames = library.Games.Where(game => game.Source == "Steam" && !string.IsNullOrEmpty(game.SteamAppId)).ToList();
            Dictionary<string, PlayAchievementProgress> byApp = PlayAchievements.ReadAll(steamGames.Select(game => game.SteamAppId));
            var byGame = new Dictionary<string, PlayAchievementProgress>();
            foreach (GameEntry game in steamGames)
            {
                if (byApp.TryGetValue(game.SteamAppId, out PlayAchievementProgress progress)) byGame[game.Id] = progress;
            }
            achievements = byGame;
        }

        public override void AddLibraryData(Dictionary<string, object> extras)
        {
            extras["play"] = new
            {
                sessions = Log.Sessions(),
                tracked = Log.MinutesByGame(),
                reported,
                achievements,
                running = RunningPayload(),
            };
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "playState":
                    PostRunning();
                    return true;
                case "stopGame":
                {
                    string id = message.GetString("id");
                    Task.Run(() => StopGame(id));
                    return true;
                }
                case "playTracked":
                {
                    string id = message.GetString("id");
                    bool tracked = message.TryGetValue("tracked", out object value) && value is bool flag && flag;
                    Hub.UserData.Set(id, "play.tracked", tracked);
                    LibraryDocument library = Hub.Library;
                    if (library != null) OnLibraryLoaded(library);
                    if (!tracked)
                    {
                        lock (gate) running.Remove(id);
                        PostRunning();
                    }
                    return true;
                }
                default:
                    return false;
            }
        }

        private void Tick()
        {
            if (Interlocked.Exchange(ref ticking, 1) == 1) return;
            try
            {
                if (libraryReady) Poll(DateTime.UtcNow);
            }
            catch (Exception error)
            {
                Trace.WriteLine("PlayTracker: " + error);
            }
            finally
            {
                Interlocked.Exchange(ref ticking, 0);
            }
        }

        private void Poll(DateTime now)
        {
            PlayMatcher currentMatcher = matcher;
            int version = Volatile.Read(ref matcherVersion);
            var alive = new HashSet<int>();
            var found = new Dictionary<string, PlayRunningGame>();

            foreach (PlayProcessEntry entry in PlayNative.ListProcesses())
            {
                if (entry.Id <= 4 || entry.Id == ownProcessId) continue;
                alive.Add(entry.Id);

                if (!processes.TryGetValue(entry.Id, out CachedProcess cached) || !string.Equals(cached.ExeName, entry.ExeName, StringComparison.OrdinalIgnoreCase))
                {
                    cached = new CachedProcess { ExeName = entry.ExeName, Version = -1 };
                    if (!PlayMatcher.IsIgnoredExe(entry.ExeName) && PlayNative.TryGetProcessInfo(entry.Id, out string path, out DateTime processStarted))
                    {
                        cached.Path = path;
                        cached.StartedUtc = processStarted;
                    }
                    processes[entry.Id] = cached;
                }
                if (cached.Version != version)
                {
                    cached.GameId = cached.Path == null ? null : currentMatcher.Match(cached.Path);
                    cached.Version = version;
                }
                if (cached.GameId == null) continue;

                if (!found.TryGetValue(cached.GameId, out PlayRunningGame game))
                {
                    game = new PlayRunningGame { Id = cached.GameId, StartedUtc = DateTime.MaxValue };
                    found[cached.GameId] = game;
                }
                game.ProcessIds.Add(entry.Id);
                DateTime processStart = cached.StartedUtc < trackerStartedUtc ? trackerStartedUtc : cached.StartedUtc;
                if (processStart < game.StartedUtc)
                {
                    game.StartedUtc = processStart;
                    game.MainProcessId = entry.Id;
                }
            }

            foreach (int dead in processes.Keys.Where(id => !alive.Contains(id)).ToList()) processes.Remove(dead);

            var ended = new List<PlaySession>();
            var started = new List<string>();
            bool changed;
            lock (gate)
            {
                foreach (PlayRunningGame current in found.Values)
                {
                    if (running.TryGetValue(current.Id, out PlayRunningGame existing))
                    {
                        existing.LastSeenUtc = now;
                        existing.ProcessIds = current.ProcessIds;
                        if (!current.ProcessIds.Contains(existing.MainProcessId)) existing.MainProcessId = current.MainProcessId;
                        continue;
                    }

                    DateTime start = current.StartedUtc > now ? now : current.StartedUtc;
                    PlayActiveSession resumed = restored?.FirstOrDefault(active => active.Id == current.Id);
                    if (resumed != null)
                    {
                        restored.Remove(resumed);
                        DateTime? resumedStart = PlayLogStore.ParseTime(resumed.Start);
                        DateTime? resumedSeen = PlayLogStore.ParseTime(resumed.LastSeen);
                        if (resumedStart != null && resumedSeen != null && start <= resumedSeen.Value + ResumeWindow) start = resumedStart.Value;
                        else AddIfValid(ended, MakeSession(resumed.Id, resumedStart, resumedSeen));
                    }

                    running[current.Id] = new PlayRunningGame
                    {
                        Id = current.Id,
                        StartedUtc = start,
                        LastSeenUtc = now,
                        ProcessIds = current.ProcessIds,
                        MainProcessId = current.MainProcessId,
                    };
                    started.Add(current.Id);
                }

                foreach (PlayRunningGame game in running.Values.ToList())
                {
                    if (found.ContainsKey(game.Id) || now - game.LastSeenUtc < ExitGrace) continue;
                    running.Remove(game.Id);
                    AddIfValid(ended, MakeSession(game.Id, game.StartedUtc, game.LastSeenUtc));
                }

                if (restored != null)
                {
                    foreach (PlayActiveSession stale in restored)
                    {
                        GameEntry staleGame = Hub.Find(stale.Id);
                        if (staleGame != null && !IsTracked(staleGame)) continue;
                        AddIfValid(ended, MakeSession(stale.Id, PlayLogStore.ParseTime(stale.Start), PlayLogStore.ParseTime(stale.LastSeen)));
                    }
                    restored = null;
                }

                string signature = string.Join("|", running.Values.OrderBy(game => game.Id).Select(game => game.Id + "@" + game.StartedUtc.Ticks + "#" + game.MainProcessId));
                changed = signature != runningSignature;
                runningSignature = signature;
            }

            foreach (PlaySession session in ended) FinishSession(session);
            if (changed || ended.Count > 0 || now - lastActiveSave > ActiveSaveInterval) SaveActive(now, false);
            if (changed)
            {
                PostRunning();
                RunningChanged?.Invoke();
            }
            foreach (string id in started) GameStarted?.Invoke(id);
        }

        private static void AddIfValid(List<PlaySession> sessions, PlaySession session)
        {
            if (session != null) sessions.Add(session);
        }

        internal static PlaySession MakeSession(string id, DateTime? start, DateTime? end)
        {
            if (string.IsNullOrEmpty(id) || start == null || end == null || end <= start) return null;
            int minutes = (int)Math.Round((end.Value - start.Value).TotalMinutes);
            if (minutes < 1) return null;
            return new PlaySession { Id = id, Start = PlayLogStore.FormatTime(start.Value), End = PlayLogStore.FormatTime(end.Value), Minutes = minutes };
        }

        private void SaveActive(DateTime now, bool force)
        {
            List<PlayActiveSession> active;
            lock (gate)
            {
                if (!force && running.Count == 0 && lastActiveSave != DateTime.MinValue && runningSignature.Length == 0 && restored == null)
                {
                    active = new List<PlayActiveSession>();
                }
                else
                {
                    active = running.Values.Select(game => new PlayActiveSession
                    {
                        Id = game.Id,
                        Start = PlayLogStore.FormatTime(game.StartedUtc),
                        LastSeen = PlayLogStore.FormatTime(force ? now : game.LastSeenUtc),
                    }).ToList();
                }
                if (restored != null) active.AddRange(restored);
            }
            lastActiveSave = now;
            Log.SetActive(active);
        }

        private void FinishSession(PlaySession session)
        {
            Log.Add(session);
            int tracked = TrackedMinutes(session.Id);
            int launcher = reported.TryGetValue(session.Id, out int value) ? value : 0;
            int minutes = Math.Max(tracked, launcher);

            GameEntry game = Hub.Find(session.Id);
            string lastPlayed = session.End;
            if (game != null)
            {
                game.PlaytimeMinutes = minutes;
                game.LastPlayed = Latest(game.LastPlayed, session.End);
                lastPlayed = game.LastPlayed;
            }
            Hub.Post(new { type = "playSession", id = session.Id, session, minutes, tracked, lastPlayed });
            SessionEnded?.Invoke(session);

            if (game != null && game.Source == "Steam" && !string.IsNullOrEmpty(game.SteamAppId)) _ = RefreshAchievementsAsync(game);
        }

        private async Task RefreshAchievementsAsync(GameEntry game)
        {
            await Task.Delay(AchievementDelay);
            PlayAchievementProgress progress = PlayAchievements.ReadAll(new[] { game.SteamAppId }).TryGetValue(game.SteamAppId, out var found) ? found : null;
            if (progress == null) return;
            var updated = new Dictionary<string, PlayAchievementProgress>(achievements) { [game.Id] = progress };
            achievements = updated;
            Hub.Post(new { type = "achievements", id = game.Id, progress });
        }

        private object RunningPayload()
        {
            return Running().Select(game => new
            {
                id = game.Id,
                startedAt = PlayLogStore.FormatTime(game.StartedUtc),
                pid = game.MainProcessId,
            }).ToList();
        }

        private void PostRunning() => Hub.Post(new { type = "running", games = RunningPayload() });

        private void StopGame(string id)
        {
            PlayRunningGame game;
            lock (gate)
            {
                game = id != null && running.TryGetValue(id, out PlayRunningGame value) ? value.Copy() : null;
            }
            GameEntry entry = Hub.Find(id);
            string name = entry?.Name ?? "The game";
            if (game == null || game.ProcessIds.Count == 0)
            {
                Hub.Post(new { type = "stopResult", id, ok = false, message = $"{name} isn't running." });
                return;
            }

            List<int> targets = ProcessTree(game);
            int failed = 0;
            foreach (int pid in targets)
            {
                try
                {
                    using Process process = Process.GetProcessById(pid);
                    process.Kill();
                }
                catch (ArgumentException)
                {
                }
                catch (Exception)
                {
                    failed++;
                }
            }

            string message = failed == 0
                ? $"Closed {name}."
                : failed == targets.Count
                    ? $"Windows wouldn't let Game Hub close {name}. It may be running as administrator."
                    : $"Closed most of {name}, but {failed} process{(failed == 1 ? "" : "es")} refused to close.";
            Hub.Post(new { type = "stopResult", id, ok = failed < targets.Count, message });
            Tick();
        }

        private static List<int> ProcessTree(PlayRunningGame game)
        {
            List<PlayProcessEntry> all = PlayNative.ListProcesses();
            var children = all.GroupBy(entry => entry.ParentId).ToDictionary(group => group.Key, group => group.Select(entry => entry.Id).ToList());
            var rootStarts = new Dictionary<int, DateTime>();
            foreach (int pid in game.ProcessIds)
            {
                rootStarts[pid] = PlayNative.TryGetProcessInfo(pid, out _, out DateTime started) ? started : DateTime.MinValue;
            }

            var ordered = new List<int>();
            var seen = new HashSet<int>(game.ProcessIds);
            var queue = new Queue<(int Id, DateTime Started)>(rootStarts.Select(pair => (pair.Key, pair.Value)));
            while (queue.Count > 0)
            {
                var (parent, parentStarted) = queue.Dequeue();
                if (!children.TryGetValue(parent, out List<int> kids)) continue;
                foreach (int kid in kids)
                {
                    if (seen.Contains(kid)) continue;
                    if (!PlayNative.TryGetProcessInfo(kid, out _, out DateTime kidStarted) || kidStarted < parentStarted) continue;
                    seen.Add(kid);
                    ordered.Add(kid);
                    queue.Enqueue((kid, kidStarted));
                }
            }
            ordered.Reverse();
            ordered.AddRange(game.ProcessIds);
            return ordered;
        }

        internal static string Latest(string first, string second)
        {
            DateTime? a = PlayLogStore.ParseTime(first);
            DateTime? b = PlayLogStore.ParseTime(second);
            if (a == null) return b == null ? first : second;
            if (b == null) return first;
            return a.Value >= b.Value ? first : second;
        }

        private sealed class CachedProcess
        {
            public string ExeName;
            public string Path;
            public DateTime StartedUtc;
            public string GameId;
            public int Version;
        }
    }
}
