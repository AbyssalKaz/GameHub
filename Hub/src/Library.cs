using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class GamePart
    {
        public string Label { get; set; }
        public string Path { get; set; }
    }

    internal sealed class GameDownload
    {
        public double? Progress { get; set; }
        public long? BytesDone { get; set; }
        public long? BytesTotal { get; set; }
        public string State { get; set; }
    }

    internal sealed class GameEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Source { get; set; }
        public string Status { get; set; }
        public string Url { get; set; }
        public string Exe { get; set; }
        public string Arguments { get; set; }
        public string Icon { get; set; }
        public string SteamAppId { get; set; }
        public string InstallPath { get; set; }
        public List<GamePart> Parts { get; set; } = new List<GamePart>();
        public string LastPlayed { get; set; }
        public int? PlaytimeMinutes { get; set; }
        public long? ReportedBytes { get; set; }
        public string LauncherPage { get; set; }

        public string InstalledAt { get; set; }
        public string IconUrl { get; set; }
        public string CoverUrl { get; set; }
        public string HeroUrl { get; set; }
        public string LogoUrl { get; set; }
        public string HeaderUrl { get; set; }
        public GameDownload Download { get; set; }
        public bool? NeedsUpdate { get; set; }

        public IEnumerable<string> MeasuredPaths
        {
            get
            {
                if (!string.IsNullOrEmpty(InstallPath)) yield return InstallPath;
                foreach (GamePart part in Parts ?? new List<GamePart>())
                {
                    if (!string.IsNullOrEmpty(part.Path)) yield return part.Path;
                }
            }
        }

        public bool CanLaunch => Status == "installed" && (!string.IsNullOrEmpty(Url) || File.Exists(Exe ?? ""));
    }

    internal sealed class LibraryDocument
    {
        public string GeneratedAt { get; set; }
        public string ConfigStamp { get; set; }
        public Dictionary<string, string> Launchers { get; set; } = new Dictionary<string, string>();
        public List<GameEntry> Games { get; set; } = new List<GameEntry>();
    }

    internal static class LibraryService
    {
        public static LibraryDocument Load()
        {
            return Json.ReadFile<LibraryDocument>(HubPaths.LibraryFile) ?? new LibraryDocument();
        }

        public static void ApplyPlayData(LibraryDocument library, HubState state)
        {
            Dictionary<string, SteamPlayRecord> steamPlay = SteamPlaytime.Load();

            foreach (GameEntry game in library.Games)
            {
                game.Parts ??= new List<GamePart>();

                if (game.Source == "Steam" && game.SteamAppId != null && steamPlay.TryGetValue(game.SteamAppId, out SteamPlayRecord record))
                {
                    if (record.Minutes > 0) game.PlaytimeMinutes = record.Minutes;
                    game.LastPlayed = Latest(game.LastPlayed, record.LastPlayed);
                }

                if (state.Launches.TryGetValue(game.Id, out string launched))
                {
                    game.LastPlayed = Latest(game.LastPlayed, launched);
                }

                string folder = game.InstallPath ?? game.Parts.FirstOrDefault()?.Path;
                if (folder != null && Directory.Exists(folder))
                {
                    game.InstalledAt = Directory.GetCreationTimeUtc(folder).ToString("o");
                }
            }
        }

        private static string Latest(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second;
            if (string.IsNullOrEmpty(second)) return first;
            return DateTime.Parse(first).ToUniversalTime() >= DateTime.Parse(second).ToUniversalTime() ? first : second;
        }

        public static Task<bool> RunSyncScriptAsync()
        {
            var completion = new TaskCompletionSource<bool>();
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{HubPaths.SyncScript}\" -Quiet",
                    WorkingDirectory = HubPaths.SyncDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };
            process.Exited += (sender, args) =>
            {
                completion.TrySetResult(process.ExitCode == 0);
                process.Dispose();
            };

            try
            {
                process.Start();
            }
            catch
            {
                completion.TrySetResult(false);
            }
            return completion.Task;
        }

        public static string ReadLastSyncLog()
        {
            string path = Path.Combine(HubPaths.SyncDir, "last-run.log");
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : "";
        }
    }

    internal sealed class SteamPlayRecord
    {
        public int Minutes;
        public string LastPlayed;
    }

    internal static class SteamPlaytime
    {
        public static Dictionary<string, SteamPlayRecord> Load()
        {
            var result = new Dictionary<string, SteamPlayRecord>();
            string root = HubPaths.SteamRoot;
            string userData = root == null ? null : Path.Combine(root, "userdata");
            if (userData == null || !Directory.Exists(userData)) return result;

            foreach (string account in Directory.GetDirectories(userData))
            {
                string configFile = Path.Combine(account, @"config\localconfig.vdf");
                if (!File.Exists(configFile)) continue;

                Dictionary<string, object> apps;
                try
                {
                    var document = Vdf.Parse(File.ReadAllText(configFile, Encoding.UTF8));
                    apps = Vdf.Navigate(document, "UserLocalConfigStore", "Software", "Valve", "Steam", "apps");
                }
                catch
                {
                    continue;
                }
                if (apps == null) continue;

                foreach (var app in apps)
                {
                    if (!(app.Value is Dictionary<string, object> values)) continue;
                    if (!result.TryGetValue(app.Key, out SteamPlayRecord record))
                    {
                        record = new SteamPlayRecord();
                        result[app.Key] = record;
                    }

                    if (values.TryGetValue("Playtime", out object playtime) && int.TryParse(playtime as string, out int minutes))
                    {
                        record.Minutes += minutes;
                    }
                    if (values.TryGetValue("LastPlayed", out object lastPlayed) && long.TryParse(lastPlayed as string, out long seconds) && seconds > 0)
                    {
                        string iso = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime.ToString("o");
                        if (record.LastPlayed == null || string.CompareOrdinal(iso, record.LastPlayed) > 0) record.LastPlayed = iso;
                    }
                }
            }
            return result;
        }
    }

    internal static class Vdf
    {
        public static Dictionary<string, object> Parse(string text)
        {
            int position = 0;
            return ParseObject(text, ref position);
        }

        public static Dictionary<string, object> Navigate(Dictionary<string, object> node, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (node == null || !node.TryGetValue(key, out object child)) return null;
                node = child as Dictionary<string, object>;
            }
            return node;
        }

        private static Dictionary<string, object> ParseObject(string text, ref int position)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                string key = NextToken(text, ref position, out bool keyIsBrace);
                if (key == null || (keyIsBrace && key == "}")) return result;

                string value = NextToken(text, ref position, out bool valueIsBrace);
                if (value == null) return result;
                result[key] = valueIsBrace && value == "{" ? ParseObject(text, ref position) : (object)value;
            }
        }

        private static string NextToken(string text, ref int position, out bool isBrace)
        {
            isBrace = false;
            while (position < text.Length)
            {
                char current = text[position];
                if (char.IsWhiteSpace(current))
                {
                    position++;
                }
                else if (current == '/' && position + 1 < text.Length && text[position + 1] == '/')
                {
                    while (position < text.Length && text[position] != '\n') position++;
                }
                else
                {
                    break;
                }
            }
            if (position >= text.Length) return null;

            char first = text[position];
            if (first == '{' || first == '}')
            {
                position++;
                isBrace = true;
                return first.ToString();
            }

            var builder = new StringBuilder();
            if (first == '"')
            {
                position++;
                while (position < text.Length && text[position] != '"')
                {
                    if (text[position] == '\\' && position + 1 < text.Length)
                    {
                        position++;
                        char escaped = text[position];
                        builder.Append(escaped == 'n' ? '\n' : escaped == 't' ? '\t' : escaped);
                    }
                    else
                    {
                        builder.Append(text[position]);
                    }
                    position++;
                }
                position++;
                return builder.ToString();
            }

            while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] != '{' && text[position] != '}')
            {
                builder.Append(text[position++]);
            }
            return builder.ToString();
        }
    }
}
