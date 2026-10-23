using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GameHub
{
    internal sealed class PlaySession
    {
        public string Id { get; set; }
        public string Start { get; set; }
        public string End { get; set; }
        public int Minutes { get; set; }
    }

    internal sealed class PlayActiveSession
    {
        public string Id { get; set; }
        public string Start { get; set; }
        public string LastSeen { get; set; }
    }

    internal sealed class PlayLogData
    {
        public List<PlaySession> Sessions { get; set; } = new List<PlaySession>();
        public List<PlayActiveSession> Active { get; set; } = new List<PlayActiveSession>();
    }

    internal sealed class PlayLogStore
    {
        public static readonly string FilePath = Path.Combine(HubPaths.CacheDir, "playlog.json");

        private readonly object gate = new object();
        private readonly string path;
        private PlayLogData data;

        public PlayLogStore(string path = null)
        {
            this.path = path ?? FilePath;
            data = Json.ReadFile<PlayLogData>(this.path) ?? new PlayLogData();
            data.Sessions ??= new List<PlaySession>();
            data.Active ??= new List<PlayActiveSession>();
            data.Sessions.RemoveAll(session => session == null || string.IsNullOrEmpty(session.Id) || ParseTime(session.Start) == null);
        }

        public List<PlaySession> Sessions()
        {
            lock (gate) return data.Sessions.ToList();
        }

        public List<PlayActiveSession> TakeActive()
        {
            lock (gate)
            {
                var active = data.Active.ToList();
                data.Active.Clear();
                return active;
            }
        }

        public void Add(PlaySession session)
        {
            lock (gate)
            {
                data.Sessions.Add(session);
                data.Active.RemoveAll(active => active.Id == session.Id);
                Save();
            }
        }

        public void SetActive(IEnumerable<PlayActiveSession> active)
        {
            lock (gate)
            {
                data.Active = active.ToList();
                Save();
            }
        }

        public Dictionary<string, int> MinutesByGame()
        {
            lock (gate)
            {
                return data.Sessions
                    .GroupBy(session => session.Id)
                    .ToDictionary(group => group.Key, group => group.Sum(session => Math.Max(0, session.Minutes)));
            }
        }

        public Dictionary<string, string> LastEndByGame()
        {
            lock (gate)
            {
                return data.Sessions
                    .GroupBy(session => session.Id)
                    .ToDictionary(group => group.Key, group => group.Max(session => session.End ?? session.Start));
            }
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                Json.WriteFile(path, data);
            }
            catch
            {
            }
        }

        public static string FormatTime(DateTime utc) => utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

        public static DateTime? ParseTime(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime value) ? value : (DateTime?)null;
        }
    }
}
