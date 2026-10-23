using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class FolderMeasure
    {
        public long Bytes { get; set; }
        public int Files { get; set; }
        public bool HasLinks { get; set; }
        public bool Truncated { get; set; }
    }

    internal static class CleanupSafety
    {
        private static readonly HashSet<string> DeniedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "microsoft", "windows", "google", "nvidia", "nvidiacorporation", "amd", "ati", "intel", "steam", "valve", "epicgames", "epicgameslauncher",
            "unity", "unitytechnologies", "unrealengine", "unreal", "packages", "temp", "tmp", "programs", "mozilla", "discord", "hydralauncher",
            "crashdumps", "d3dscache", "comms", "connecteddevicesplatform", "apple", "applecomputer", "adobe", "gamehub", "mygames", "savedgames",
            "common", "data", "save", "saves", "savegames", "config", "settings", "cache", "logs", "game", "games", "default", "user", "users",
            "local", "roaming", "locallow", "bin", "binaries", "win64", "win32", "x64", "x86", "launcher", "updater", "installer", "setup",
            "riotgames", "riotclient", "ea", "electronicarts", "eadesktop", "origin", "ubisoft", "ubisoftgamelauncher", "rockstargames",
            "battlenet", "blizzard", "blizzardentertainment", "gog", "gogcom", "galaxy", "xbox", "xboxgames", "onedrive", "dropbox", "nvidiagameframes",
            "squirreltemp", "electron", "chromium", "packagecache", "nuget", "npm", "pip", "python", "java", "oracle", "dotnet", "powershell",
            "documents", "downloads", "desktop", "pictures", "music", "videos", "public", "programdata", "programfiles", "appdata",
        };

        public static bool IsDeniedName(string normalizedName) => string.IsNullOrEmpty(normalizedName) || normalizedName.Length < 3 || DeniedNames.Contains(normalizedName);

        public static string Full(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
                return full.Length > 3 ? full.TrimEnd('\\') : full;
            }
            catch
            {
                return null;
            }
        }

        public static bool SamePath(string first, string second) =>
            first != null && second != null && string.Equals(first.TrimEnd('\\'), second.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        public static bool IsInside(string path, string folder)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder)) return false;
            return (path.TrimEnd('\\') + "\\").StartsWith(folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) && !SamePath(path, folder);
        }

        public static bool IsSameOrInside(string path, string folder) => SamePath(path, folder) || IsInside(path, folder);

        public static bool IsDriveRoot(string path)
        {
            string full = Full(path);
            return full == null || full.TrimEnd('\\').Length <= 2 || string.Equals(Path.GetPathRoot(full)?.TrimEnd('\\'), full.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
        }

        public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static string LocalLow => Path.Combine(Home, @"AppData\LocalLow");
        public static string SavedGames => Path.Combine(Home, "Saved Games");
        public static string MyGames => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games");
        public static string Downloads => Path.Combine(Home, "Downloads");

        public static List<string> ProtectedRoots(params string[] extra)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var roots = new List<string>(UninstallService.ProtectedFolders())
            {
                LocalLow,
                SavedGames,
                MyGames,
                Downloads,
                Environment.GetEnvironmentVariable("PUBLIC"),
                Environment.GetEnvironmentVariable("OneDrive"),
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
                Path.Combine(appData, "Microsoft"),
                Path.Combine(localAppData, "Microsoft"),
                Path.Combine(localAppData, "Packages"),
                Path.Combine(localAppData, "Programs"),
                Path.Combine(localAppData, "Temp"),
                Path.Combine(localAppData, "EpicGamesLauncher"),
                Path.Combine(programData, "Epic"),
                HubPaths.HydraDataDir,
                HubPaths.HubDir,
                Path.GetTempPath(),
            };
            roots.AddRange(extra);
            foreach (DriveInfo drive in SafeDrives()) roots.Add(drive.RootDirectory.FullName);
            return roots.Select(Full).Where(root => !string.IsNullOrEmpty(root)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static IEnumerable<DriveInfo> SafeDrives()
        {
            try
            {
                return DriveInfo.GetDrives().Where(drive => drive.IsReady).ToList();
            }
            catch
            {
                return Array.Empty<DriveInfo>();
            }
        }

        public static bool IsProtected(string path, IEnumerable<string> protectedRoots)
        {
            string full = Full(path);
            if (full == null || IsDriveRoot(full)) return true;
            return protectedRoots.Any(root => SamePath(full, root) || IsInside(root, full));
        }

        public static string Check(string path, IReadOnlyList<GameEntry> library, string ownerGameId, bool allowFile, params string[] extraProtected)
        {
            string full = Full(path);
            if (full == null) return "The path isn't valid.";

            bool isFile = File.Exists(full);
            bool isFolder = Directory.Exists(full);
            if (!isFile && !isFolder) return "It no longer exists.";
            if (isFile && !allowFile) return "This is a file, not a folder.";

            try
            {
                if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0) return "This is a link to another location.";
            }
            catch
            {
                return "Game Hub can't read this location.";
            }

            if (IsDriveRoot(full) || full.Split('\\').Length < 3) return "This is too close to the root of the drive.";

            string lower = full.ToLowerInvariant() + "\\";
            if (lower.Contains("\\steamapps\\")) return "Steam manages this folder.";
            if (lower.Contains("\\epic games\\")) return "Epic Games manages this folder.";
            string steamRoot = HubPaths.SteamRoot;
            if (!string.IsNullOrEmpty(steamRoot) && IsSameOrInside(full, steamRoot)) return "Steam manages this folder.";

            foreach (string root in ProtectedRoots(extraProtected))
            {
                if (SamePath(full, root) || IsInside(root, full)) return "This is a system or library folder.";
            }

            foreach (GameEntry other in library ?? new List<GameEntry>())
            {
                if (other.Id == ownerGameId) continue;
                foreach (string otherPath in other.MeasuredPaths)
                {
                    string otherFull = Full(otherPath);
                    if (otherFull == null) continue;
                    if (IsSameOrInside(full, otherFull) || IsInside(otherFull, full)) return $"{other.Name} also uses this location.";
                    if (!string.IsNullOrEmpty(other.Exe) && IsInside(other.Exe, full) && other.Status == "installed") return $"{other.Name} is installed here.";
                }
            }
            return null;
        }

        public static async Task<bool> RecycleAsync(string path, IntPtr owner)
        {
            string full = Full(path);
            if (full == null || (!File.Exists(full) && !Directory.Exists(full))) return false;
            await UninstallService.RecycleAsync(full, owner).ConfigureAwait(false);
            return !File.Exists(full) && !Directory.Exists(full);
        }

        public static FolderMeasure Measure(string path, int fileLimit = 400000)
        {
            var result = new FolderMeasure();
            try
            {
                if (File.Exists(path))
                {
                    result.Bytes = new FileInfo(path).Length;
                    result.Files = 1;
                    return result;
                }
                if (!Directory.Exists(path)) return result;

                var pending = new Stack<DirectoryInfo>();
                pending.Push(new DirectoryInfo(path));
                while (pending.Count > 0)
                {
                    DirectoryInfo current = pending.Pop();
                    FileSystemInfo[] entries;
                    try
                    {
                        entries = current.GetFileSystemInfos();
                    }
                    catch
                    {
                        continue;
                    }
                    foreach (FileSystemInfo entry in entries)
                    {
                        if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            result.HasLinks = true;
                            continue;
                        }
                        if (entry is DirectoryInfo folder)
                        {
                            pending.Push(folder);
                        }
                        else if (entry is FileInfo file)
                        {
                            result.Bytes += file.Length;
                            if (++result.Files >= fileLimit)
                            {
                                result.Truncated = true;
                                return result;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        public static List<string> RunningFrom(string folder)
        {
            var names = new List<string>();
            string full = Full(folder);
            if (full == null || IsDriveRoot(full)) return names;
            try
            {
                using var searcher = new System.Management.ManagementObjectSearcher("SELECT Name, ExecutablePath FROM Win32_Process");
                using var results = searcher.Get();
                foreach (System.Management.ManagementBaseObject process in results)
                {
                    using (process)
                    {
                        string path = process["ExecutablePath"] as string;
                        if (!string.IsNullOrEmpty(path) && IsInside(path, full)) names.Add(process["Name"] as string ?? Path.GetFileName(path));
                    }
                }
            }
            catch
            {
            }
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static void Reveal(string path)
        {
            string full = Full(path);
            if (full == null) return;
            if (File.Exists(full)) System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{full}\"");
            else if (Directory.Exists(full)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(full) { UseShellExecute = true });
        }

        public static string SafeFileName(string name)
        {
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            string cleaned = new string((name ?? "Game").Select(character => invalid.Contains(character) ? ' ' : character).ToArray()).Trim().TrimEnd('.');
            while (cleaned.Contains("  ")) cleaned = cleaned.Replace("  ", " ");
            if (cleaned.Length > 80) cleaned = cleaned.Substring(0, 80).Trim();
            return cleaned.Length == 0 ? "Game" : cleaned;
        }
    }
}
