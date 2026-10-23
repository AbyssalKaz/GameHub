using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameHub
{
    internal sealed class StoreVisit
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Host { get; set; }
        public long Visited { get; set; }
    }

    internal sealed class StoreHistory
    {
        private const int Limit = 500;
        private static readonly string HistoryFile = Path.Combine(HubPaths.CacheDir, "store-history.json");

        private readonly List<StoreVisit> visits;
        private readonly System.Windows.Forms.Timer saveDelay = new System.Windows.Forms.Timer { Interval = 2000 };

        public StoreHistory()
        {
            visits = Json.ReadFile<List<StoreVisit>>(HistoryFile) ?? new List<StoreVisit>();
            saveDelay.Tick += (sender, args) =>
            {
                saveDelay.Stop();
                Save();
            };
        }

        public IReadOnlyList<StoreVisit> Visits => visits;

        public void Record(string url, string title)
        {
            string host = StoreDomains.HostOf(url);
            if (host.Length == 0) return;

            visits.RemoveAll(visit => visit.Url == url);
            visits.Insert(0, new StoreVisit { Url = url, Title = string.IsNullOrWhiteSpace(title) ? host : title, Host = host, Visited = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
            if (visits.Count > Limit) visits.RemoveRange(Limit, visits.Count - Limit);
            ScheduleSave();
        }

        public void UpdateTitle(string url, string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return;
            StoreVisit visit = visits.FirstOrDefault(entry => entry.Url == url);
            if (visit == null || visit.Title == title) return;
            visit.Title = title;
            ScheduleSave();
        }

        public void Remove(string url)
        {
            if (visits.RemoveAll(visit => visit.Url == url) > 0) ScheduleSave();
        }

        public void Clear()
        {
            visits.Clear();
            Save();
        }

        public void Flush()
        {
            if (!saveDelay.Enabled) return;
            saveDelay.Stop();
            Save();
        }

        private void ScheduleSave()
        {
            saveDelay.Stop();
            saveDelay.Start();
        }

        private void Save()
        {
            try
            {
                Json.WriteFile(HistoryFile, visits);
            }
            catch
            {
            }
        }
    }
}
