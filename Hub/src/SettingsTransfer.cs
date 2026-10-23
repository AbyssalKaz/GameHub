using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GameHub
{
    internal sealed class SettingsBackup
    {
        public Dictionary<string, object> Data { get; set; }
        public Dictionary<string, byte[]> ArtFiles { get; set; } = new Dictionary<string, byte[]>();
        public string Path { get; set; }
    }

    internal static class SettingsTransfer
    {
        private const string DataEntry = "game-hub.json";
        private const string ArtPrefix = "art/";

        public static Dictionary<string, object> BuildData(HubContext hub, Dictionary<string, object> local)
        {
            return new Dictionary<string, object>
            {
                ["app"] = "Game Hub",
                ["format"] = 1,
                ["exportedAt"] = DateTime.UtcNow.ToString("o"),
                ["local"] = local ?? new Dictionary<string, object>(),
                ["userData"] = hub.UserData.Snapshot(),
                ["settings"] = hub.Settings.Snapshot(),
                ["artOverrides"] = ReadArtOverrides(),
            };
        }

        public static void Write(string path, Dictionary<string, object> data)
        {
            string json = HubConfig.Indent(Json.Serialize(data));
            if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(path, json, new UTF8Encoding(false));
                return;
            }

            string temporary = path + ".tmp";
            using (var stream = File.Create(temporary))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry dataEntry = archive.CreateEntry(DataEntry, CompressionLevel.Optimal);
                using (var writer = new StreamWriter(dataEntry.Open(), new UTF8Encoding(false))) writer.Write(json);

                foreach (string relative in ArtFilesOf(data))
                {
                    string source = System.IO.Path.Combine(HubPaths.ArtCacheDir, relative);
                    if (!File.Exists(source)) continue;
                    archive.CreateEntryFromFile(source, ArtPrefix + relative.Replace('\\', '/'), CompressionLevel.NoCompression);
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        public static SettingsBackup Read(string path)
        {
            var backup = new SettingsBackup { Path = path };
            if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using var archive = ZipFile.OpenRead(path);
                ZipArchiveEntry dataEntry = archive.GetEntry(DataEntry) ?? throw new InvalidDataException("This zip is not a Game Hub backup.");
                using (var reader = new StreamReader(dataEntry.Open(), Encoding.UTF8)) backup.Data = Json.ParseObject(reader.ReadToEnd());
                foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => entry.FullName.StartsWith(ArtPrefix, StringComparison.OrdinalIgnoreCase) && entry.Length > 0))
                {
                    string relative = entry.FullName.Substring(ArtPrefix.Length).Replace('/', '\\');
                    if (!IsSafeRelative(relative) || entry.Length > 25 * 1024 * 1024) continue;
                    using var source = entry.Open();
                    using var memory = new MemoryStream();
                    source.CopyTo(memory);
                    backup.ArtFiles[relative] = memory.ToArray();
                }
            }
            else
            {
                backup.Data = Json.ParseObject(File.ReadAllText(path, Encoding.UTF8));
            }

            if (backup.Data == null || backup.Data.GetString("app") != "Game Hub") throw new InvalidDataException("This file is not a Game Hub backup.");
            return backup;
        }

        public static object Summarize(SettingsBackup backup)
        {
            Dictionary<string, object> data = backup.Data;
            int Count(string key) => data.TryGetValue(key, out object value) && value is Dictionary<string, object> map ? map.Count : 0;
            return new
            {
                file = System.IO.Path.GetFileName(backup.Path),
                exportedAt = data.GetString("exportedAt"),
                games = Count("userData"),
                settings = Count("settings"),
                art = Count("artOverrides"),
                artFiles = backup.ArtFiles.Count,
            };
        }

        public static string Apply(HubContext hub, SettingsBackup backup)
        {
            Dictionary<string, object> data = backup.Data;
            int games = 0;
            if (data.TryGetValue("userData", out object userData) && userData is Dictionary<string, object> perGame)
            {
                foreach (var pair in perGame)
                {
                    if (!(pair.Value is Dictionary<string, object> patch) || patch.Count == 0) continue;
                    hub.UserData.Merge(pair.Key, patch);
                    games++;
                }
            }

            int settings = 0;
            if (data.TryGetValue("settings", out object settingsValue) && settingsValue is Dictionary<string, object> values)
            {
                foreach (var pair in values)
                {
                    hub.Settings.Set(pair.Key, pair.Value);
                    settings++;
                }
            }

            int art = ApplyArt(data, backup.ArtFiles);
            return $"Imported data for {games} game{(games == 1 ? "" : "s")}, {settings} setting{(settings == 1 ? "" : "s")} and {art} custom artwork override{(art == 1 ? "" : "s")}.";
        }

        private static int ApplyArt(Dictionary<string, object> data, Dictionary<string, byte[]> files)
        {
            if (!data.TryGetValue("artOverrides", out object value) || !(value is Dictionary<string, object> overrides) || overrides.Count == 0) return 0;

            foreach (var file in files)
            {
                string target = System.IO.Path.Combine(HubPaths.ArtCacheDir, file.Key);
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                File.WriteAllBytes(target, file.Value);
            }

            var incoming = new Dictionary<string, Dictionary<string, string>>();
            foreach (var game in overrides)
            {
                if (!(game.Value is Dictionary<string, object> kinds)) continue;
                var valid = kinds
                    .Where(kind => kind.Value is string relative && IsSafeRelative(relative) && File.Exists(System.IO.Path.Combine(HubPaths.ArtCacheDir, relative)))
                    .ToDictionary(kind => kind.Key, kind => (string)kind.Value);
                if (valid.Count > 0) incoming[game.Key] = valid;
            }
            if (incoming.Count == 0) return 0;

            if (ArtIndexAccess.TryGet(out ArtIndex index, out object gate))
            {
                lock (gate)
                {
                    Merge(index.Overrides, incoming);
                    Json.WriteFile(HubPaths.ArtIndexFile, index);
                }
            }
            else
            {
                ArtIndex stored = Json.ReadFile<ArtIndex>(HubPaths.ArtIndexFile) ?? new ArtIndex();
                stored.Overrides ??= new Dictionary<string, Dictionary<string, string>>();
                Merge(stored.Overrides, incoming);
                Json.WriteFile(HubPaths.ArtIndexFile, stored);
            }
            return incoming.Sum(game => game.Value.Count);
        }

        private static void Merge(Dictionary<string, Dictionary<string, string>> target, Dictionary<string, Dictionary<string, string>> incoming)
        {
            foreach (var game in incoming)
            {
                if (!target.TryGetValue(game.Key, out var kinds)) target[game.Key] = kinds = new Dictionary<string, string>();
                foreach (var kind in game.Value) kinds[kind.Key] = kind.Value;
            }
        }

        private static Dictionary<string, Dictionary<string, string>> ReadArtOverrides()
        {
            if (ArtIndexAccess.TryGet(out ArtIndex index, out object gate))
            {
                lock (gate) return index.Overrides.ToDictionary(pair => pair.Key, pair => new Dictionary<string, string>(pair.Value));
            }
            return (Json.ReadFile<ArtIndex>(HubPaths.ArtIndexFile) ?? new ArtIndex()).Overrides ?? new Dictionary<string, Dictionary<string, string>>();
        }

        private static IEnumerable<string> ArtFilesOf(Dictionary<string, object> data)
        {
            if (!data.TryGetValue("artOverrides", out object value) || !(value is Dictionary<string, Dictionary<string, string>> overrides)) return Enumerable.Empty<string>();
            return overrides.Values.SelectMany(kinds => kinds.Values).Where(IsSafeRelative).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsSafeRelative(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || System.IO.Path.IsPathRooted(relative)) return false;
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(HubPaths.ArtCacheDir, relative));
            return full.StartsWith(HubPaths.ArtCacheDir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class ArtIndexAccess
    {
        public static bool TryGet(out ArtIndex index, out object gate)
        {
            index = null;
            gate = null;
            ArtService service = ArtService.Current;
            if (service == null) return false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            index = typeof(ArtService).GetField("index", flags)?.GetValue(service) as ArtIndex;
            gate = typeof(ArtService).GetField("sync", flags)?.GetValue(service);
            return index != null && gate != null;
        }
    }
}
