using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameHub
{
    internal sealed class PlayMatcher
    {
        private static readonly HashSet<string> IgnoredExeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "steam.exe", "steamwebhelper.exe", "steamservice.exe", "steamerrorreporter.exe", "steamerrorreporter64.exe",
            "gameoverlayui.exe", "gameoverlayui64.exe", "steamsysinfo.exe",
            "epicgameslauncher.exe", "epicwebhelper.exe", "eosoverlayrenderer-win64-shipping.exe", "eosoverlayrenderer-win32-shipping.exe",
            "unitycrashhandler64.exe", "unitycrashhandler32.exe", "crashpad_handler.exe", "crashreportclient.exe", "crashreporter.exe",
            "crashsender.exe", "bugsplat.exe", "bssndrpt.exe", "bssndrpt64.exe", "werfault.exe",
            "unrealcefsubprocess.exe", "cefsharp.browsersubprocess.exe", "qtwebengineprocess.exe", "cefprocess.exe",
            "easyanticheat.exe", "easyanticheat_eos.exe", "easyanticheat_setup.exe", "easyanticheat_eos_setup.exe",
            "beservice.exe", "beservice_x64.exe", "vgc.exe", "vgtray.exe", "xigncode.exe", "nprotect.exe",
            "riotclientservices.exe", "riotclientux.exe", "riotclientuxrender.exe", "riotclientcrashhandler.exe",
            "hydra.exe", "hydralauncher.exe", "eadesktop.exe", "eabackgroundservice.exe", "ealocalhostsvc.exe", "link2ea.exe",
            "rockstarservice.exe", "socialclubhelper.exe", "rockstarerrorhandler.exe", "hoyoplay.exe", "hyp.exe",
            "gamehub.exe", "conhost.exe", "cmd.exe", "powershell.exe", "explorer.exe", "dxsetup.exe", "dxwebsetup.exe",
            "uninstall.exe", "uninst.exe", "redist.exe", "oalinst.exe", "ue4prereqsetup_x64.exe", "ueprereqsetup_x64.exe",
        };

        private static readonly string[] IgnoredExePrefixes =
        {
            "unins0", "vc_redist", "vcredist", "crashreport", "crashhandler", "easyanticheat", "beservice", "battleye",
            "epiconlineservices", "dotnetfx", "ndp4", "physx", "directx", "dxsetup",
        };

        private readonly List<Root> roots;

        private PlayMatcher(List<Root> roots)
        {
            this.roots = roots;
        }

        public int RootCount => roots.Count;

        public static PlayMatcher Empty { get; } = new PlayMatcher(new List<Root>());

        public static PlayMatcher Build(IEnumerable<GameEntry> games, IEnumerable<string> broadFolders = null)
        {
            var broad = new HashSet<string>((broadFolders ?? DefaultBroadFolders()).Select(Normalize).Where(path => path != null), StringComparer.OrdinalIgnoreCase);
            var candidates = new List<Root>();

            foreach (GameEntry game in games)
            {
                if (game == null || string.IsNullOrEmpty(game.Id) || game.Status != "installed") continue;
                string exe = string.IsNullOrEmpty(game.Exe) ? null : game.Exe;
                var folders = new List<string>();
                if (!string.IsNullOrEmpty(game.InstallPath)) folders.Add(game.InstallPath);
                foreach (GamePart part in game.Parts ?? new List<GamePart>())
                {
                    if (!string.IsNullOrEmpty(part.Path)) folders.Add(part.Path);
                }
                if (folders.Count == 0 && exe != null && !IsIgnoredExe(Path.GetFileName(exe)))
                {
                    folders.Add(Path.GetDirectoryName(exe));
                }

                foreach (string folder in folders)
                {
                    string root = Normalize(folder);
                    if (root == null || broad.Contains(root) || Depth(root) < 1) continue;
                    candidates.Add(new Root { Path = root, GameId = game.Id, Exe = exe });
                }
            }

            var containers = new HashSet<string>(candidates
                .Where(candidate => candidates
                    .Where(other => other.GameId != candidate.GameId && other.Path.Length > candidate.Path.Length && other.Path.StartsWith(candidate.Path, StringComparison.OrdinalIgnoreCase))
                    .Select(other => other.GameId)
                    .Distinct()
                    .Count() >= 2)
                .Select(candidate => candidate.Path), StringComparer.OrdinalIgnoreCase);

            var roots = candidates
                .Where(candidate => !containers.Contains(candidate.Path))
                .OrderByDescending(candidate => candidate.Path.Length)
                .ToList();
            return new PlayMatcher(roots);
        }

        public string Match(string processPath)
        {
            if (string.IsNullOrEmpty(processPath) || roots.Count == 0) return null;
            if (IsIgnoredExe(Path.GetFileName(processPath))) return null;

            Root best = null;
            foreach (Root root in roots)
            {
                if (best != null && root.Path.Length < best.Path.Length) break;
                if (!processPath.StartsWith(root.Path, StringComparison.OrdinalIgnoreCase)) continue;
                if (best == null || (!IsExe(best, processPath) && IsExe(root, processPath))) best = root;
            }
            return best?.GameId;
        }

        private static bool IsExe(Root root, string processPath) => root.Exe != null && string.Equals(root.Exe, processPath, StringComparison.OrdinalIgnoreCase);

        public static bool IsIgnoredExe(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return true;
            if (IgnoredExeNames.Contains(fileName)) return true;
            string lower = fileName.ToLowerInvariant();
            return IgnoredExePrefixes.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal));
        }

        public static string Normalize(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;
            try
            {
                string full = Path.GetFullPath(folder.Trim()).TrimEnd('\\', '/');
                return full.Length < 2 ? null : full + "\\";
            }
            catch
            {
                return null;
            }
        }

        private static int Depth(string root) => root.TrimEnd('\\').Split('\\').Length - 1;

        private static IEnumerable<string> DefaultBroadFolders()
        {
            var folders = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Epic Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Epic Games"),
            };
            string steam = SafeSteamRoot();
            if (steam != null)
            {
                folders.Add(steam);
                folders.Add(Path.Combine(steam, @"steamapps\common"));
            }
            return folders.Where(folder => !string.IsNullOrEmpty(folder));
        }

        private static string SafeSteamRoot()
        {
            try
            {
                return HubPaths.SteamRoot;
            }
            catch
            {
                return null;
            }
        }

        private sealed class Root
        {
            public string Path;
            public string GameId;
            public string Exe;
        }
    }
}
