using System;
using System.IO;
using Microsoft.Win32;

namespace GameHub
{
    internal static class HubPaths
    {
        public static readonly string AppDir = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string HubDir = Path.GetFullPath(Path.Combine(AppDir, ".."));
        public static readonly string SyncDir = Path.GetFullPath(Path.Combine(HubDir, ".."));
        public static readonly string WebDir = Path.Combine(AppDir, "web");

        public static readonly string LibraryFile = Path.Combine(SyncDir, "library.json");
        public static readonly string SyncScript = Path.Combine(SyncDir, "GameShortcutSync.ps1");

        public static readonly string CacheDir = Path.Combine(HubDir, "cache");
        public static readonly string PublicCacheDir = Path.Combine(CacheDir, "public");
        public static readonly string IconCacheDir = Path.Combine(PublicCacheDir, "icons");
        public static readonly string ArtCacheDir = Path.Combine(PublicCacheDir, "art");
        public static readonly string WebViewDataDir = Path.Combine(CacheDir, "webview");
        public static readonly string SizeCacheFile = Path.Combine(CacheDir, "sizes.json");
        public static readonly string ArtIndexFile = Path.Combine(CacheDir, "art.json");
        public static readonly string StateFile = Path.Combine(CacheDir, "state.json");
        public static readonly string UserDataFile = Path.Combine(CacheDir, "userdata.json");
        public static readonly string SettingsFile = Path.Combine(CacheDir, "settings.json");
        public static readonly string ConfigFile = Path.Combine(SyncDir, "config.json");

        public static readonly string HydraDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "hydralauncher");
        public static readonly string HydraAssetsDir = Path.Combine(HydraDataDir, "Assets");
        public static readonly string EpicCatalogFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Catalog\catcache.bin");

        public static string SteamRoot
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                string path = key?.GetValue("SteamPath") as string;
                return string.IsNullOrEmpty(path) ? null : Path.GetFullPath(path).TrimEnd('\\');
            }
        }

        public static string SteamLibraryCacheDir
        {
            get
            {
                string root = SteamRoot;
                return root == null ? null : Path.Combine(root, @"appcache\librarycache");
            }
        }

        public static void EnsureCacheFolders()
        {
            Directory.CreateDirectory(IconCacheDir);
            Directory.CreateDirectory(ArtCacheDir);
            Directory.CreateDirectory(WebViewDataDir);
        }

        public static string GetProtocolHandlerExe(string scheme)
        {
            using var key = Registry.ClassesRoot.OpenSubKey(scheme + @"\shell\open\command");
            string command = key?.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(command)) return null;

            command = command.Trim();
            string exe = command.StartsWith("\"")
                ? command.Substring(1, command.IndexOf('"', 1) - 1)
                : command.Split(' ')[0];
            return File.Exists(exe) ? exe : null;
        }
    }
}
