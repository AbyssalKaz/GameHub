using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GameHub
{
    internal sealed class PlayLaunchOptions : HubModule
    {
        public const string ArgsKey = "launch.args";
        public const string AdminKey = "launch.admin";
        public const string DirectKey = "launch.direct";
        public const string ExeKey = "launch.exe";

        private static readonly string[] SkippedFolders =
        {
            "_commonredist", "redist", "redistributables", "directx", "vcredist", "support", "installer", "installers",
            "easyanticheat", "battleye", "__installer", "prereqs", "prerequisites", "dotnet", "tools", "crashreport",
        };

        public override int Order => 60;

        public static bool CanStartDirectly(GameEntry game)
        {
            if (game == null || string.IsNullOrEmpty(game.Url)) return false;
            if (game.Url.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)) return false;
            return !string.IsNullOrEmpty(game.InstallPath) || File.Exists(game.Exe ?? "");
        }

        public override void BeforeLaunch(GameEntry game, LaunchRequest request)
        {
            string extraArgs = (Hub.UserData.GetString(game.Id, ArgsKey) ?? "").Trim();
            bool admin = Hub.UserData.GetBool(game.Id, AdminKey);
            bool direct = Hub.UserData.GetBool(game.Id, DirectKey) && CanStartDirectly(game);
            ProcessStartInfo start = request.StartInfo;
            bool throughSteam = false;

            if (direct)
            {
                string exe = ResolveExe(game);
                if (exe == null)
                {
                    request.Cancel = true;
                    request.CancelMessage = $"Couldn't find the program for {game.Name}. Pick it in Launch options.";
                    return;
                }
                bool keepArguments = string.Equals(exe, game.Exe, StringComparison.OrdinalIgnoreCase) && !IsLauncherExe(game);
                start = new ProcessStartInfo(exe, keepArguments ? game.Arguments ?? "" : "")
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                };
            }
            else if (extraArgs.Length > 0 && IsSteamUrl(start.FileName) && !string.IsNullOrEmpty(game.SteamAppId))
            {
                string steamExe = SteamExe();
                if (steamExe != null)
                {
                    start = new ProcessStartInfo(steamExe, $"-applaunch {game.SteamAppId} {extraArgs}") { UseShellExecute = true };
                    extraArgs = "";
                    throughSteam = true;
                }
            }

            bool isProgram = IsProgram(start.FileName);
            if (extraArgs.Length > 0 && isProgram) start.Arguments = string.IsNullOrWhiteSpace(start.Arguments) ? extraArgs : start.Arguments.TrimEnd() + " " + extraArgs;
            if (admin && isProgram && !throughSteam && !IsLauncherExe(start.FileName))
            {
                start.UseShellExecute = true;
                start.Verb = "runas";
            }
            request.StartInfo = start;
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "playExeCandidates":
                {
                    string id = message.GetString("id");
                    Task.Run(() =>
                    {
                        GameEntry game = Hub.Find(id);
                        var items = game == null ? new List<object>() : FindExeCandidates(game).Take(12).Select(file => (object)new
                        {
                            path = file.FullName,
                            name = file.Name,
                            folder = RelativeFolder(game, file),
                            bytes = file.Length,
                        }).ToList();
                        Hub.Post(new { type = "playExeCandidates", id, items, current = game == null ? null : ResolveExe(game) });
                    });
                    return true;
                }
                case "playBrowseExe":
                {
                    string id = message.GetString("id");
                    Hub.Form.BeginInvoke(new Action(() => BrowseExe(id)));
                    return true;
                }
                default:
                    return false;
            }
        }

        private void BrowseExe(string id)
        {
            GameEntry game = Hub.Find(id);
            if (game == null) return;
            string current = ResolveExe(game);
            using var dialog = new OpenFileDialog
            {
                Title = $"Choose the program for {game.Name}",
                Filter = "Programs (*.exe)|*.exe",
                CheckFileExists = true,
                InitialDirectory = current != null ? Path.GetDirectoryName(current) : Directory.Exists(game.InstallPath ?? "") ? game.InstallPath : "",
            };
            if (dialog.ShowDialog(Hub.Form) != DialogResult.OK) return;
            Dictionary<string, object> data = Hub.UserData.Merge(id, new Dictionary<string, object> { [ExeKey] = dialog.FileName });
            Hub.Post(new { type = "gameData", id, data });
        }

        public string ResolveExe(GameEntry game)
        {
            string chosen = Hub.UserData.GetString(game.Id, ExeKey);
            if (!string.IsNullOrEmpty(chosen) && File.Exists(chosen)) return chosen;
            if (!string.IsNullOrEmpty(game.Exe) && File.Exists(game.Exe) && !IsLauncherExe(game)) return game.Exe;
            return FindExeCandidates(game).FirstOrDefault()?.FullName;
        }

        private static bool IsLauncherExe(GameEntry game) => IsLauncherExe(game.Exe);

        private static bool IsLauncherExe(string path) => PlayMatcher.IsIgnoredExe(Path.GetFileName(path ?? ""));

        public static IEnumerable<FileInfo> FindExeCandidates(GameEntry game)
        {
            if (string.IsNullOrEmpty(game.InstallPath) || !Directory.Exists(game.InstallPath)) return Enumerable.Empty<FileInfo>();
            var files = new List<FileInfo>();
            Collect(new DirectoryInfo(game.InstallPath), 0, files);
            string wanted = Simplify(game.Name);
            return files
                .Select(file => (File: file, Score: Score(file, game.InstallPath, wanted)))
                .Where(item => item.Score > -100)
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.File.Length)
                .Select(item => item.File)
                .ToList();
        }

        private static void Collect(DirectoryInfo folder, int depth, List<FileInfo> files)
        {
            if (depth > 4 || files.Count > 400) return;
            try
            {
                foreach (FileInfo file in folder.EnumerateFiles("*.exe")) files.Add(file);
                foreach (DirectoryInfo child in folder.EnumerateDirectories())
                {
                    if ((child.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if (SkippedFolders.Contains(child.Name.ToLowerInvariant())) continue;
                    Collect(child, depth + 1, files);
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        internal static int Score(FileInfo file, string installPath, string wantedName)
        {
            if (PlayMatcher.IsIgnoredExe(file.Name)) return -1000;
            string lower = file.Name.ToLowerInvariant();
            string stem = Simplify(Path.GetFileNameWithoutExtension(file.Name));
            int score = 0;

            if (Regex.IsMatch(lower, "(setup|install|update|patch|config|settings|launcherpatch|helper|report|server|benchmark|editor|tool|crash|uninst|redist|prereq)")) score -= 60;
            if (lower.Contains("shipping")) score += 40;
            if (wantedName.Length > 0 && stem.Length > 0)
            {
                if (stem == wantedName) score += 60;
                else if (wantedName.StartsWith(stem, StringComparison.Ordinal) || stem.StartsWith(wantedName, StringComparison.Ordinal)) score += 35;
                else if (wantedName.Contains(stem) || stem.Contains(wantedName)) score += 20;
            }
            if (lower.Contains("launcher")) score -= 15;

            string relative = file.DirectoryName.Length > installPath.Length ? file.DirectoryName.Substring(installPath.Length).Trim('\\') : "";
            int depth = relative.Length == 0 ? 0 : relative.Split('\\').Length;
            if (relative.IndexOf("binaries\\win64", StringComparison.OrdinalIgnoreCase) >= 0) score += 15;
            else score -= depth * 6;
            if (relative.IndexOf("engine\\", StringComparison.OrdinalIgnoreCase) >= 0 || relative.StartsWith("engine", StringComparison.OrdinalIgnoreCase)) score -= 40;

            long megabytes = file.Length / (1024 * 1024);
            score += (int)Math.Min(30, megabytes / 4);
            if (file.Length < 200 * 1024) score -= 20;
            return score;
        }

        private static string Simplify(string text) => Regex.Replace((text ?? "").ToLowerInvariant(), "[^a-z0-9]", "");

        private static string RelativeFolder(GameEntry game, FileInfo file)
        {
            string root = (game.InstallPath ?? "").TrimEnd('\\');
            string folder = file.DirectoryName ?? "";
            return folder.Length > root.Length && folder.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? folder.Substring(root.Length).Trim('\\') : "";
        }

        private static bool IsSteamUrl(string fileName) => fileName != null && fileName.StartsWith("steam://", StringComparison.OrdinalIgnoreCase);

        private static bool IsProgram(string fileName) => fileName != null && fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !fileName.Contains("://");

        private static string SteamExe()
        {
            try
            {
                string root = HubPaths.SteamRoot;
                string exe = root == null ? null : Path.Combine(root, "steam.exe");
                return exe != null && File.Exists(exe) ? exe : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
