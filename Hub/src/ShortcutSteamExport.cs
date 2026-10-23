using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GameHub
{
    internal sealed class ShortcutSteamExport : HubModule
    {
        private const string Tag = "Game Hub";
        private static readonly string StateFile = Path.Combine(HubPaths.CacheDir, "steam-export.json");
        private static readonly string BackupDir = Path.Combine(HubPaths.CacheDir, "steam-backups");
        private static readonly string[] ArtExtensions = { ".png", ".jpg", ".jpeg" };

        private bool busy;

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "discovery.steamPreview":
                    _ = RunAsync(false);
                    return true;
                case "discovery.steamApply":
                    _ = RunAsync(true);
                    return true;
            }
            return false;
        }

        private async Task RunAsync(bool apply)
        {
            if (busy) return;
            busy = true;
            List<GameEntry> games = Hub.Library?.Games?.ToList() ?? new List<GameEntry>();
            try
            {
                object result = await Task.Run(() => Build(games, apply));
                Hub.Post(result);
            }
            catch (Exception error)
            {
                DiagLog.Write("discovery", "Steam export failed: " + error);
                Hub.Post(new { type = apply ? "discovery.steamApplied" : "discovery.steamPreview", error = error.Message });
            }
            finally
            {
                busy = false;
            }
        }

        private static object Build(List<GameEntry> games, bool apply)
        {
            string messageType = apply ? "discovery.steamApplied" : "discovery.steamPreview";
            string steamRoot = HubPaths.SteamRoot;
            string userId = steamRoot == null ? null : FindUser(steamRoot);
            if (userId == null) return new { type = messageType, error = "No Steam account was found on this PC." };

            string configDir = Path.Combine(steamRoot, "userdata", userId, "config");
            string file = Path.Combine(configDir, "shortcuts.vdf");
            string gridDir = Path.Combine(configDir, "grid");
            bool steamRunning = Process.GetProcessesByName("steam").Length > 0;

            VdfNode root = ShortcutVdf.ReadFile(file);
            VdfNode shortcuts = root.Child("shortcuts");
            if (shortcuts == null)
            {
                shortcuts = new VdfNode();
                root.SetNode("shortcuts", shortcuts);
            }

            ExportState state = Json.ReadFile<ExportState>(StateFile) ?? new ExportState();
            state.Games ??= new Dictionary<string, ExportRecord>();

            var foreignTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ours = new Dictionary<uint, VdfNode>();
            foreach (VdfField field in shortcuts.Fields)
            {
                if (!(field.Value is VdfNode entry)) continue;
                if (IsOurs(entry) && entry.GetUInt("appid") is uint ourId)
                {
                    ours[ourId] = entry;
                    continue;
                }
                string exe = Unquote(entry.GetString("Exe") ?? entry.GetString("exe"));
                if (!string.IsNullOrEmpty(exe)) foreignTargets.Add(NormalizePath(exe));
                string options = (entry.GetString("LaunchOptions") ?? "").Trim();
                if (Regex.IsMatch(options, @"^[a-z][\w.+-]*:", RegexOptions.IgnoreCase)) foreignTargets.Add(options);
            }

            var items = new List<object>();
            var skipped = new List<object>();
            var pending = new List<(GameEntry Game, VdfNode Entry, uint AppId, string Action, ExportRecord Record)>();

            foreach (GameEntry game in games.OrderBy(game => game.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (game.Status != "installed" || game.Source == "Steam" || game.Source == "SteamShortcut" || !game.CanLaunch) continue;

                bool useUrl = !string.IsNullOrEmpty(game.Url);
                string target = useUrl ? game.Url : game.Exe;
                if (string.IsNullOrEmpty(target)) continue;
                bool known = foreignTargets.Contains(useUrl ? target : NormalizePath(target))
                    || (!string.IsNullOrEmpty(game.Exe) && foreignTargets.Contains(NormalizePath(game.Exe)));
                if (known)
                {
                    skipped.Add(new { id = game.Id, name = game.Name, reason = "Already in Steam" });
                    continue;
                }

                string exe = useUrl ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") : game.Exe;
                string quotedExe = Quote(exe);
                string key = userId + "|" + game.Id;
                state.Games.TryGetValue(key, out ExportRecord record);

                uint appId = record?.AppId ?? ShortcutVdf.ShortcutAppId(quotedExe, game.Name);
                ours.TryGetValue(appId, out VdfNode existing);
                VdfNode entry = existing ?? new VdfNode();
                string action = existing == null ? "add" : "update";

                entry.SetUInt("appid", appId);
                entry.SetString("AppName", game.Name);
                entry.SetString("Exe", quotedExe);
                entry.SetString("StartDir", Quote(useUrl ? Path.GetDirectoryName(exe) : Path.GetDirectoryName(game.Exe)));
                entry.SetString("icon", IconFor(game, exe));
                if (entry.Find("ShortcutPath") == null) entry.SetString("ShortcutPath", "");
                entry.SetString("LaunchOptions", useUrl ? game.Url : game.Arguments ?? "");
                if (entry.Find("IsHidden") == null) entry.SetUInt("IsHidden", 0);
                if (entry.Find("AllowDesktopConfig") == null) entry.SetUInt("AllowDesktopConfig", 1);
                if (entry.Find("AllowOverlay") == null) entry.SetUInt("AllowOverlay", 1);
                if (entry.Find("OpenVR") == null) entry.SetUInt("OpenVR", 0);
                if (entry.Find("Devkit") == null) entry.SetUInt("Devkit", 0);
                if (entry.Find("DevkitGameID") == null) entry.SetString("DevkitGameID", "");
                if (entry.Find("DevkitOverrideAppID") == null) entry.SetUInt("DevkitOverrideAppID", 0);
                if (entry.Find("LastPlayTime") == null) entry.SetUInt("LastPlayTime", 0);
                if (entry.Find("FlatpakAppID") == null) entry.SetString("FlatpakAppID", "");
                VdfNode tags = entry.Child("tags") ?? new VdfNode();
                if (!tags.Fields.Any(field => field.Value as string == Tag)) tags.SetString(tags.Fields.Count.ToString(), Tag);
                entry.SetNode("tags", tags);

                List<ArtCopy> art = PlanArt(game, appId, gridDir, record);
                items.Add(new { id = game.Id, name = game.Name, action, appId, art = art.Count, target = useUrl ? game.Url : game.Exe });
                pending.Add((game, existing == null ? entry : null, appId, action, record));
            }

            if (!apply)
            {
                return new { type = messageType, steamRunning, user = userId, file, items, skipped };
            }

            if (steamRunning) return new { type = messageType, error = "Close Steam first, then try again. Steam rewrites this file while it runs." };
            if (pending.Count == 0) return new { type = messageType, added = 0, updated = 0, items, skipped };

            Directory.CreateDirectory(BackupDir);
            string backup = null;
            if (File.Exists(file))
            {
                backup = Path.Combine(BackupDir, $"shortcuts-{userId}-{DateTime.Now:yyyyMMdd-HHmmss}.vdf");
                File.Copy(file, backup, false);
            }

            int nextIndex = shortcuts.Fields.Select(field => int.TryParse(field.Key, out int index) ? index + 1 : 0).DefaultIfEmpty(0).Max();
            foreach (var item in pending.Where(item => item.Entry != null))
            {
                shortcuts.SetNode((nextIndex++).ToString(), item.Entry);
            }

            Directory.CreateDirectory(configDir);
            string temporary = file + ".hubtmp";
            File.WriteAllBytes(temporary, ShortcutVdf.Write(root));
            if (File.Exists(file)) File.Replace(temporary, file, null);
            else File.Move(temporary, file);

            Directory.CreateDirectory(gridDir);
            foreach (var item in pending)
            {
                string key = userId + "|" + item.Game.Id;
                var record = item.Record ?? new ExportRecord();
                record.AppId = item.AppId;
                record.Name = item.Game.Name;
                record.Art ??= new List<string>();
                foreach (ArtCopy copy in PlanArt(item.Game, item.AppId, gridDir, item.Record))
                {
                    try
                    {
                        foreach (string extension in ArtExtensions)
                        {
                            string other = Path.Combine(gridDir, copy.BaseName + extension);
                            if (extension != copy.Extension && File.Exists(other) && record.Art.Contains(Path.GetFileName(other))) File.Delete(other);
                        }
                        File.Copy(copy.Source, Path.Combine(gridDir, copy.BaseName + copy.Extension), true);
                        string written = copy.BaseName + copy.Extension;
                        if (!record.Art.Contains(written)) record.Art.Add(written);
                    }
                    catch (Exception error)
                    {
                        DiagLog.Write("discovery", $"Could not copy Steam art for {item.Game.Name}: {error.Message}");
                    }
                }
                state.Games[key] = record;
            }
            Json.WriteFile(StateFile, state);

            return new
            {
                type = messageType,
                added = pending.Count(item => item.Action == "add"),
                updated = pending.Count(item => item.Action == "update"),
                backup,
                items,
                skipped,
            };
        }

        private static List<ArtCopy> PlanArt(GameEntry game, uint appId, string gridDir, ExportRecord record)
        {
            var copies = new List<ArtCopy>();
            void Add(string url, string suffix)
            {
                string source = ShortcutIcons.LocalArtFile(url);
                if (source == null) return;
                string extension = Path.GetExtension(source).ToLowerInvariant();
                if (!ArtExtensions.Contains(extension)) return;

                string baseName = appId + suffix;
                bool foreign = ArtExtensions.Any(other =>
                {
                    string path = Path.Combine(gridDir, baseName + other);
                    return File.Exists(path) && (record?.Art == null || !record.Art.Contains(baseName + other));
                });
                if (foreign) return;
                copies.Add(new ArtCopy { Source = source, BaseName = baseName, Extension = extension == ".jpeg" ? ".jpg" : extension });
            }

            Add(game.CoverUrl, "p");
            Add(game.HeroUrl, "_hero");
            Add(game.LogoUrl, "_logo");
            Add(game.HeaderUrl, "");
            return copies;
        }

        private static string IconFor(GameEntry game, string exe)
        {
            string icon = game.Icon;
            if (!string.IsNullOrEmpty(icon) && File.Exists(icon) && Regex.IsMatch(icon, @"\.(exe|ico|png)$", RegexOptions.IgnoreCase)) return Quote(icon);
            return string.IsNullOrEmpty(game.Exe) ? "" : Quote(game.Exe ?? exe);
        }

        private static bool IsOurs(VdfNode entry)
        {
            return entry.Child("tags")?.Fields.Any(field => field.Value as string == Tag) == true;
        }

        private static string FindUser(string steamRoot)
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
                if (key?.GetValue("ActiveUser") is int active && active != 0) return ((uint)active).ToString();
            }
            catch
            {
            }

            string loginUsers = Path.Combine(steamRoot, @"config\loginusers.vdf");
            if (File.Exists(loginUsers))
            {
                try
                {
                    Dictionary<string, object> users = Vdf.Navigate(Vdf.Parse(File.ReadAllText(loginUsers, Encoding.UTF8)), "users");
                    foreach (var user in users ?? new Dictionary<string, object>())
                    {
                        if (user.Value is Dictionary<string, object> values && values.GetString("MostRecent") == "1" && ulong.TryParse(user.Key, out ulong steamId))
                        {
                            return (steamId - 76561197960265728UL).ToString();
                        }
                    }
                }
                catch
                {
                }
            }

            string userdata = Path.Combine(steamRoot, "userdata");
            if (!Directory.Exists(userdata)) return null;
            return Directory.GetDirectories(userdata)
                .Select(Path.GetFileName)
                .Where(name => name != "0" && name.All(char.IsDigit))
                .OrderByDescending(name => Directory.GetLastWriteTimeUtc(Path.Combine(userdata, name)))
                .FirstOrDefault();
        }

        private static string Quote(string path) => string.IsNullOrEmpty(path) ? "" : "\"" + path.Trim('"') + "\"";

        private static string Unquote(string path) => (path ?? "").Trim().Trim('"');

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(Unquote(path)).TrimEnd('\\');
            }
            catch
            {
                return Unquote(path);
            }
        }

        private sealed class ArtCopy
        {
            public string Source { get; set; }
            public string BaseName { get; set; }
            public string Extension { get; set; }
        }

        private sealed class ExportState
        {
            public Dictionary<string, ExportRecord> Games { get; set; } = new Dictionary<string, ExportRecord>();
        }

        private sealed class ExportRecord
        {
            public uint AppId { get; set; }
            public string Name { get; set; }
            public List<string> Art { get; set; } = new List<string>();
        }
    }
}
