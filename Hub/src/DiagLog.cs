using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class DiagEntry
    {
        public string Time { get; set; }
        public string Area { get; set; }
        public string Message { get; set; }
    }

    internal static class DiagLog
    {
        private const int MaxFileBytes = 512 * 1024;
        private const int MaxRecent = 200;

        private static readonly object Gate = new object();
        private static readonly List<DiagEntry> Recent = new List<DiagEntry>();
        private static bool attached;

        public static string FilePath => Path.Combine(HubPaths.CacheDir, "diagnostics.log");

        public static void Write(string area, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            var entry = new DiagEntry
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Area = string.IsNullOrWhiteSpace(area) ? "hub" : area.Trim(),
                Message = message.Trim(),
            };

            lock (Gate)
            {
                Recent.Add(entry);
                if (Recent.Count > MaxRecent) Recent.RemoveRange(0, Recent.Count - MaxRecent);
                try
                {
                    Directory.CreateDirectory(HubPaths.CacheDir);
                    File.AppendAllText(FilePath, $"{entry.Time}\t{entry.Area}\t{entry.Message.Replace("\r", "").Replace("\n", "\n\t\t")}\n", new UTF8Encoding(false));
                    TrimFile();
                }
                catch
                {
                }
            }
        }

        public static List<DiagEntry> Latest(int count)
        {
            lock (Gate)
            {
                if (Recent.Count == 0) LoadFromFile();
                return Recent.Skip(Math.Max(0, Recent.Count - count)).Reverse().ToList();
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                Recent.Clear();
                try
                {
                    if (File.Exists(FilePath)) File.WriteAllText(FilePath, "");
                }
                catch
                {
                }
            }
        }

        public static void Attach()
        {
            lock (Gate)
            {
                if (attached) return;
                attached = true;
            }
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => Write("crash", args.ExceptionObject?.ToString());
            TaskScheduler.UnobservedTaskException += (sender, args) => Write("task", args.Exception?.GetBaseException().ToString());
        }

        private static void TrimFile()
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists || info.Length <= MaxFileBytes) return;
            string text = File.ReadAllText(FilePath, Encoding.UTF8);
            int start = text.IndexOf('\n', text.Length / 2);
            File.WriteAllText(FilePath, start < 0 ? "" : text.Substring(start + 1), new UTF8Encoding(false));
        }

        private static void LoadFromFile()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadLines(FilePath, Encoding.UTF8).Reverse().Where(line => !line.StartsWith("\t")).Take(MaxRecent).Reverse())
                {
                    string[] parts = line.Split(new[] { '\t' }, 3);
                    if (parts.Length == 3) Recent.Add(new DiagEntry { Time = parts[0], Area = parts[1], Message = parts[2] });
                }
            }
            catch
            {
            }
        }
    }
}
