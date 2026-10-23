using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GameHub
{
    internal sealed class UninstallFolder
    {
        public string Path { get; set; }
        public string Label { get; set; }
        public bool Allowed { get; set; }
        public bool Selected { get; set; }
        public string Reason { get; set; }
    }

    internal sealed class UninstallPlan
    {
        public string GameId { get; set; }
        public string Method { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Command { get; set; }
        public string Blocked { get; set; }
        public string Warning { get; set; }
        public string ActionLabel { get; set; }
        public string LauncherName { get; set; }
        public List<UninstallFolder> Folders { get; set; } = new List<UninstallFolder>();

        internal string FileName;
        internal string Arguments;
    }

    internal static class UninstallService
    {
        private static readonly HashSet<string> RecyclableSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Folder", "Extra", "Downloads", "Hydra" };

        private static readonly Dictionary<string, string> LauncherNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Riot"] = "Riot Client",
            ["Xbox"] = "the Xbox app",
            ["HoYoPlay"] = "HoYoPlay",
            ["EA"] = "the EA app",
            ["Rockstar"] = "Rockstar Games Launcher",
        };

        private const uint FileOperationDelete = 3;
        private const ushort AllowUndo = 0x40;
        private const ushort NoConfirmation = 0x10;
        private const ushort WantNukeWarning = 0x4000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShellFileOperation
        {
            public IntPtr Window;
            public uint Function;
            public string From;
            public string To;
            public ushort Flags;
            public bool AnyOperationsAborted;
            public IntPtr NameMappings;
            public string ProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperationW(ref ShellFileOperation operation);

        public static UninstallPlan BuildPlan(GameEntry game, IReadOnlyList<GameEntry> library)
        {
            var plan = new UninstallPlan { GameId = game.Id };
            plan.Warning = SharedFolderWarning(game, library);
            UninstallPlan finished = BuildMethod(game, library, plan);
            return finished;
        }

        private static string SharedFolderWarning(GameEntry game, IReadOnlyList<GameEntry> library)
        {
            if (string.IsNullOrEmpty(game.InstallPath)) return null;
            string install = game.InstallPath.TrimEnd('\\');
            var sharing = library
                .Where(other => other != game && other.MeasuredPaths.Any(path =>
                    string.Equals(path.TrimEnd('\\'), install, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(install + "\\", StringComparison.OrdinalIgnoreCase) ||
                    install.StartsWith(path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)))
                .Select(other => other.Name)
                .Distinct()
                .ToList();
            return sharing.Count == 0 ? null : $"{string.Join(", ", sharing)} {(sharing.Count == 1 ? "uses" : "use")} the same folder, so removing this game may break {(sharing.Count == 1 ? "it" : "them")} too.";
        }

        private static UninstallPlan BuildMethod(GameEntry game, IReadOnlyList<GameEntry> library, UninstallPlan plan)
        {

            if (game.Source == "Steam" && game.Status == "installed" && !string.IsNullOrEmpty(game.SteamAppId))
            {
                plan.Method = "steam";
                plan.ActionLabel = "Open Steam uninstall";
                plan.Title = "Uninstall with Steam";
                plan.Description = "Steam asks you to confirm, then removes the game together with its workshop content and shader cache.";
                plan.Command = "steam://uninstall/" + game.SteamAppId;
                plan.FileName = plan.Command;
                return plan;
            }

            if (game.Source == "Epic" && game.Status == "installed")
            {
                plan.Method = "launcher";
                plan.Title = "Uninstall in Epic Games";
                plan.Description = "Only the Epic Games Launcher can remove its games cleanly. Game Hub opens your Epic library. Click the three dots on this game, then Uninstall.";
                plan.Command = "com.epicgames.launcher://store/library";
                plan.FileName = plan.Command;
                plan.LauncherName = "your Epic library";
                plan.ActionLabel = "Open Epic library";
                return plan;
            }

            var uninstaller = game.Status == "installed" ? FindUninstaller(game) : null;

            if (uninstaller == null && game.Status == "installed" && !RecyclableSources.Contains(game.Source ?? ""))
            {
                string launcher = LauncherNames.TryGetValue(game.Source ?? "", out string name) ? name : game.Source + "'s launcher";
                string target = !string.IsNullOrEmpty(game.LauncherPage) ? game.LauncherPage : game.Exe;
                if (string.IsNullOrEmpty(target))
                {
                    plan.Method = "none";
                    plan.Title = "Can't uninstall from here";
                    plan.Blocked = $"Uninstall {game.Name} from {launcher}.";
                    return plan;
                }

                plan.Method = "launcher";
                plan.Title = $"Uninstall in {launcher}";
                plan.Description = game.Source == "Xbox"
                    ? "Xbox games are Windows apps, so only the Xbox app or Windows Settings can remove them. Game Hub opens this game in the Xbox app. Choose Manage, then Uninstall."
                    : $"{char.ToUpper(launcher[0]) + launcher.Substring(1)} manages this game, so it has to be removed there. Game Hub opens {launcher} for you.";
                plan.Command = target;
                plan.FileName = target;
                plan.LauncherName = launcher;
                plan.ActionLabel = "Open " + launcher.Replace("the ", "");
                return plan;
            }

            if (uninstaller != null)
            {
                plan.Method = "uninstaller";
                plan.ActionLabel = "Run uninstaller";
                plan.Title = "Run the game's uninstaller";
                plan.Description = $"Starts {System.IO.Path.GetFileName(uninstaller.Value.File)}, the uninstaller that came with the game. It asks before it removes anything.";
                plan.FileName = uninstaller.Value.File;
                plan.Arguments = uninstaller.Value.Arguments;
                plan.Command = string.IsNullOrEmpty(plan.Arguments) ? plan.FileName : $"\"{plan.FileName}\" {plan.Arguments}";
            }
            else
            {
                plan.Method = "recycle";
                plan.ActionLabel = "Move to Recycle Bin";
                plan.Title = "Move to the Recycle Bin";
                plan.Description = "The selected folders go to the Recycle Bin, so you can restore them. If a folder is too big for the Recycle Bin, Windows asks before deleting it for good.";
            }

            var otherGames = library.Where(other => other != game).ToList();
            bool first = true;
            foreach (string path in game.MeasuredPaths)
            {
                bool isInstallFolder = first && !string.IsNullOrEmpty(game.InstallPath);
                first = false;
                if (plan.Method == "uninstaller" && isInstallFolder) continue;

                string label = isInstallFolder
                    ? (game.Status == "leftover" ? "Leftover files" : "Game files")
                    : game.Parts.FirstOrDefault(part => string.Equals(part.Path, path, StringComparison.OrdinalIgnoreCase))?.Label ?? "Folder";
                string reason = CheckRecyclable(path, otherGames);
                plan.Folders.Add(new UninstallFolder
                {
                    Path = path,
                    Label = label,
                    Allowed = reason == null,
                    Reason = reason,
                    Selected = reason == null && plan.Method == "recycle",
                });
            }

            if (plan.Method == "recycle" && !plan.Folders.Any(folder => folder.Allowed))
            {
                plan.Method = "none";
                plan.Title = "Can't uninstall safely";
                plan.Blocked = plan.Folders.FirstOrDefault()?.Reason ?? "No folder was found for this game.";
            }
            return plan;
        }

        public static string CheckRecyclable(string path, IReadOnlyList<GameEntry> otherGames)
        {
            string full;
            try
            {
                full = System.IO.Path.GetFullPath(path).TrimEnd('\\');
            }
            catch
            {
                return "The folder path isn't valid.";
            }

            var info = new DirectoryInfo(full);
            if (!info.Exists) return "This folder no longer exists.";
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0) return "This folder is a link to another location.";
            if (full.Split('\\').Length < 3) return "This folder is too close to the root of the drive.";

            string lower = full.ToLowerInvariant() + "\\";
            if (lower.Contains("\\steamapps\\")) return "Steam manages this folder.";
            if (lower.Contains("\\epic games\\")) return "Epic Games manages this folder.";

            foreach (string protectedFolder in ProtectedFolders())
            {
                if (string.Equals(full, protectedFolder, StringComparison.OrdinalIgnoreCase) ||
                    protectedFolder.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    return "This is a system or library folder, not a game folder.";
                }
            }

            foreach (GameEntry other in otherGames)
            {
                foreach (string otherPath in other.MeasuredPaths)
                {
                    string otherFull = otherPath.TrimEnd('\\');
                    if (string.Equals(otherFull, full, StringComparison.OrdinalIgnoreCase) ||
                        otherFull.StartsWith(full + "\\", StringComparison.OrdinalIgnoreCase) ||
                        full.StartsWith(otherFull + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{other.Name} also uses this folder.";
                    }
                }
            }
            return null;
        }

        internal static IEnumerable<string> ProtectedFolders()
        {
            var folders = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                HubPaths.SyncDir,
                HubPaths.SteamRoot,
            };

            try
            {
                var config = Json.ParseObject(File.ReadAllText(System.IO.Path.Combine(HubPaths.SyncDir, "config.json")));
                foreach (string key in new[] { "LibraryFolders", "SearchFolders" })
                {
                    if (config != null && config.TryGetValue(key, out object list) && list is object[] entries)
                    {
                        folders.AddRange(entries.OfType<string>().Select(Environment.ExpandEnvironmentVariables));
                    }
                }
            }
            catch
            {
            }

            return folders.Where(folder => !string.IsNullOrEmpty(folder)).Select(folder => folder.TrimEnd('\\'));
        }

        private static (string File, string Arguments)? FindUninstaller(GameEntry game)
        {
            string install = game.InstallPath?.TrimEnd('\\');
            if (string.IsNullOrEmpty(install) || !Directory.Exists(install)) return null;
            string exe = string.IsNullOrWhiteSpace(game.Exe) ? null : game.Exe;

            foreach (var (location, command) in RegisteredUninstallers())
            {
                bool exact = string.Equals(location, install, StringComparison.OrdinalIgnoreCase);
                bool containsExe = exe != null && exe.StartsWith(location + "\\", StringComparison.OrdinalIgnoreCase) &&
                                   (location + "\\").StartsWith(install + "\\", StringComparison.OrdinalIgnoreCase);
                if (!exact && !containsExe) continue;

                var parsed = SplitCommand(command);
                if (parsed != null) return parsed;
            }

            var folders = new List<string>();
            if (exe != null)
            {
                for (string folder = System.IO.Path.GetDirectoryName(exe); folder != null && (folder + "\\").StartsWith(install + "\\", StringComparison.OrdinalIgnoreCase); folder = System.IO.Path.GetDirectoryName(folder))
                {
                    folders.Add(folder);
                }
            }
            if (!folders.Contains(install, StringComparer.OrdinalIgnoreCase)) folders.Add(install);

            foreach (string folder in folders)
            {
                string found = Directory.GetFiles(folder, "unins*.exe").OrderBy(file => file).FirstOrDefault();
                if (found != null) return (found, "");
            }
            return null;
        }

        private static IEnumerable<(string Location, string Command)> RegisteredUninstallers()
        {
            var roots = new[]
            {
                (Hive: RegistryHive.LocalMachine, View: RegistryView.Registry64),
                (Hive: RegistryHive.LocalMachine, View: RegistryView.Registry32),
                (Hive: RegistryHive.CurrentUser, View: RegistryView.Registry64),
            };

            foreach (var root in roots)
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(root.Hive, root.View);
                using RegistryKey uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall == null) continue;

                foreach (string name in uninstall.GetSubKeyNames())
                {
                    using RegistryKey entry = uninstall.OpenSubKey(name);
                    string location = (entry?.GetValue("InstallLocation") as string)?.Trim().Trim('"').TrimEnd('\\');
                    string command = entry?.GetValue("UninstallString") as string;
                    if (string.IsNullOrEmpty(location) || string.IsNullOrEmpty(command)) continue;
                    if (command.IndexOf("steam.exe", StringComparison.OrdinalIgnoreCase) >= 0 || command.IndexOf("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    yield return (location, command);
                }
            }
        }

        private static (string File, string Arguments)? SplitCommand(string command)
        {
            command = command.Trim();
            if (command.StartsWith("\""))
            {
                int end = command.IndexOf('"', 1);
                if (end < 0) return null;
                return (command.Substring(1, end - 1), command.Substring(end + 1).Trim());
            }

            int exeEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeEnd < 0) return null;
            return (command.Substring(0, exeEnd + 4), command.Substring(exeEnd + 4).Trim());
        }

        public static void StartExternal(UninstallPlan plan)
        {
            var start = new ProcessStartInfo(plan.FileName) { UseShellExecute = true };
            if (!string.IsNullOrEmpty(plan.Arguments)) start.Arguments = plan.Arguments;
            if (plan.Method == "uninstaller" && File.Exists(plan.FileName)) start.WorkingDirectory = System.IO.Path.GetDirectoryName(plan.FileName);
            Process.Start(start);
        }

        public static Task<bool> RecycleAsync(string folder, IntPtr owner)
        {
            var completion = new TaskCompletionSource<bool>();
            var thread = new Thread(() =>
            {
                try
                {
                    var operation = new ShellFileOperation
                    {
                        Window = owner,
                        Function = FileOperationDelete,
                        From = folder.TrimEnd('\\') + "\0",
                        Flags = (ushort)(AllowUndo | NoConfirmation | WantNukeWarning),
                    };
                    int code = SHFileOperationW(ref operation);
                    completion.TrySetResult(code == 0 && !operation.AnyOperationsAborted && !Directory.Exists(folder));
                }
                catch
                {
                    completion.TrySetResult(false);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return completion.Task;
        }
    }
}
