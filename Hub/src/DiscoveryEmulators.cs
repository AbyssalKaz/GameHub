using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GameHub
{
    internal static class DiscoveryEmulators
    {
        private static readonly string[] DolphinExtensions = { "iso", "gcm", "gcz", "rvz", "wbfs", "ciso", "wia", "wad", "elf", "dol" };
        private static readonly string[] RetroArchExtensions = { "nes", "sfc", "smc", "gb", "gbc", "gba", "n64", "z64", "v64", "md", "gen", "sms", "gg", "pce", "cue", "chd", "zip", "7z" };

        public static List<object> Detect()
        {
            var results = new List<object>();
            try
            {
                List<string> remembered = RememberedPrograms();

                string dolphin = FirstExisting(remembered.Where(path => Path.GetFileName(path).Equals("Dolphin.exe", StringComparison.OrdinalIgnoreCase))
                    .Concat(new[]
                    {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Dolphin\Dolphin.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Dolphin-x64\Dolphin.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Dolphin\Dolphin.exe"),
                    }));
                if (dolphin != null)
                {
                    results.Add(new
                    {
                        Name = "Dolphin",
                        Exe = dolphin,
                        Arguments = "-b -e \"{rom}\"",
                        RomFolders = DolphinGameFolders(dolphin),
                        Extensions = DolphinExtensions,
                        Cores = new string[0],
                    });
                }

                string steamRoot = HubPaths.SteamRoot;
                string retroArch = FirstExisting(remembered.Where(path => Path.GetFileName(path).Equals("retroarch.exe", StringComparison.OrdinalIgnoreCase))
                    .Concat(new[]
                    {
                        @"C:\RetroArch-Win64\retroarch.exe",
                        @"C:\RetroArch\retroarch.exe",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"RetroArch\retroarch.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"RetroArch\retroarch.exe"),
                        steamRoot == null ? null : Path.Combine(steamRoot, @"steamapps\common\RetroArch\retroarch.exe"),
                    }));
                if (retroArch != null)
                {
                    string coresFolder = Path.Combine(Path.GetDirectoryName(retroArch), "cores");
                    string[] cores = Directory.Exists(coresFolder)
                        ? Directory.GetFiles(coresFolder, "*_libretro.dll").Select(Path.GetFileName).OrderBy(name => name).ToArray()
                        : new string[0];
                    string core = cores.FirstOrDefault();
                    results.Add(new
                    {
                        Name = "RetroArch",
                        Exe = retroArch,
                        Arguments = core == null ? "\"{rom}\"" : $"-L \"cores\\{core}\" \"{{rom}}\"",
                        RomFolders = new string[0],
                        Extensions = RetroArchExtensions,
                        Cores = cores,
                    });
                }
            }
            catch
            {
            }
            return results;
        }

        private static string FirstExisting(IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) return Path.GetFullPath(path);
                }
                catch
                {
                }
            }
            return null;
        }

        private static List<string> RememberedPrograms()
        {
            var paths = new List<string>();
            AddValueNames(paths, @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache", name => Regex.Replace(name, @"\.(FriendlyAppName|ApplicationCompany)$", ""));
            AddValueNames(paths, @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store", name => name);
            return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddValueNames(List<string> paths, string keyPath, Func<string, string> clean)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(keyPath);
                if (key == null) return;
                foreach (string name in key.GetValueNames())
                {
                    string path = clean(name);
                    if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
                }
            }
            catch
            {
            }
        }

        private static string[] DolphinGameFolders(string exe)
        {
            var folders = new List<string>();
            foreach (string userFolder in DolphinUserFolders(exe))
            {
                string ini = Path.Combine(userFolder, @"Config\Dolphin.ini");
                if (!File.Exists(ini)) continue;
                try
                {
                    foreach (string line in File.ReadAllLines(ini, Encoding.UTF8))
                    {
                        Match match = Regex.Match(line, @"^\s*ISOPath\d+\s*=\s*(.+?)\s*$");
                        if (match.Success && Directory.Exists(match.Groups[1].Value)) folders.Add(match.Groups[1].Value.Replace('/', '\\').TrimEnd('\\'));
                    }
                }
                catch
                {
                }
                break;
            }
            return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static IEnumerable<string> DolphinUserFolders(string exe)
        {
            string folder = Path.GetDirectoryName(exe);
            if (File.Exists(Path.Combine(folder, "portable.txt"))) yield return Path.Combine(folder, "User");

            string configured = null;
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Dolphin Emulator");
                configured = key?.GetValue("UserConfigPath") as string;
            }
            catch
            {
            }
            if (!string.IsNullOrEmpty(configured)) yield return configured;

            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dolphin Emulator");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Dolphin Emulator");
        }
    }
}
