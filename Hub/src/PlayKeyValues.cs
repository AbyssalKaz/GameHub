using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace GameHub
{
    internal static class PlayKeyValues
    {
        private const byte TypeNone = 0;
        private const byte TypeString = 1;
        private const byte TypeInt32 = 2;
        private const byte TypeFloat = 3;
        private const byte TypePointer = 4;
        private const byte TypeWideString = 5;
        private const byte TypeColor = 6;
        private const byte TypeUInt64 = 7;
        private const byte TypeEnd = 8;
        private const byte TypeInt64 = 10;
        private const byte TypeAlternateEnd = 11;

        public static Dictionary<string, object> Parse(byte[] bytes)
        {
            int position = 0;
            return ParseObject(bytes, ref position, 0);
        }

        public static Dictionary<string, object> Child(Dictionary<string, object> node, string key)
        {
            return node != null && node.TryGetValue(key, out object value) ? value as Dictionary<string, object> : null;
        }

        public static long? Number(Dictionary<string, object> node, string key)
        {
            if (node == null || !node.TryGetValue(key, out object value)) return null;
            return value switch
            {
                int number => number,
                long number => number,
                ulong number => (long)number,
                float number => (long)number,
                string text when long.TryParse(text, out long parsed) => parsed,
                _ => null,
            };
        }

        private static Dictionary<string, object> ParseObject(byte[] bytes, ref int position, int depth)
        {
            if (depth > 64) throw new InvalidDataException("KeyValues nesting is too deep.");
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                if (position >= bytes.Length) return result;
                byte type = bytes[position++];
                if (type == TypeEnd || type == TypeAlternateEnd) return result;

                string key = ReadString(bytes, ref position);
                object value;
                switch (type)
                {
                    case TypeNone:
                        value = ParseObject(bytes, ref position, depth + 1);
                        break;
                    case TypeString:
                        value = ReadString(bytes, ref position);
                        break;
                    case TypeInt32:
                    case TypePointer:
                    case TypeColor:
                        Require(bytes, position, 4);
                        value = BitConverter.ToInt32(bytes, position);
                        position += 4;
                        break;
                    case TypeFloat:
                        Require(bytes, position, 4);
                        value = BitConverter.ToSingle(bytes, position);
                        position += 4;
                        break;
                    case TypeUInt64:
                        Require(bytes, position, 8);
                        value = BitConverter.ToUInt64(bytes, position);
                        position += 8;
                        break;
                    case TypeInt64:
                        Require(bytes, position, 8);
                        value = BitConverter.ToInt64(bytes, position);
                        position += 8;
                        break;
                    case TypeWideString:
                        value = ReadWideString(bytes, ref position);
                        break;
                    default:
                        throw new InvalidDataException($"Unknown KeyValues type {type} at byte {position - 1}.");
                }
                result[key] = value;
            }
        }

        private static void Require(byte[] bytes, int position, int count)
        {
            if (position + count > bytes.Length) throw new InvalidDataException("KeyValues data ends early.");
        }

        private static string ReadString(byte[] bytes, ref int position)
        {
            int end = Array.IndexOf(bytes, (byte)0, position);
            if (end < 0) throw new InvalidDataException("Unterminated KeyValues string.");
            string text = Encoding.UTF8.GetString(bytes, position, end - position);
            position = end + 1;
            return text;
        }

        private static string ReadWideString(byte[] bytes, ref int position)
        {
            int end = position;
            while (end + 1 < bytes.Length && (bytes[end] != 0 || bytes[end + 1] != 0)) end += 2;
            if (end + 1 >= bytes.Length) throw new InvalidDataException("Unterminated KeyValues wide string.");
            string text = Encoding.Unicode.GetString(bytes, position, end - position);
            position = end + 2;
            return text;
        }
    }

    internal sealed class PlayAchievementProgress
    {
        public int Unlocked { get; set; }
        public int Total { get; set; }
        public string LastUnlocked { get; set; }
    }

    internal static class PlayAchievements
    {
        public static string StatsFolder
        {
            get
            {
                string root = HubPaths.SteamRoot;
                return root == null ? null : Path.Combine(root, @"appcache\stats");
            }
        }

        public static string ActiveAccountId()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
                return key?.GetValue("ActiveUser") is int id && id != 0 ? ((uint)id).ToString() : null;
            }
            catch
            {
                return null;
            }
        }

        public static Dictionary<string, PlayAchievementProgress> ReadAll(IEnumerable<string> appIds)
        {
            var result = new Dictionary<string, PlayAchievementProgress>();
            string folder = StatsFolder;
            if (folder == null || !Directory.Exists(folder)) return result;
            string account = ActiveAccountId();
            foreach (string appId in appIds.Where(id => !string.IsNullOrEmpty(id)).Distinct())
            {
                PlayAchievementProgress progress = Read(folder, appId, account);
                if (progress != null) result[appId] = progress;
            }
            return result;
        }

        public static PlayAchievementProgress Read(string folder, string appId, string account)
        {
            try
            {
                string schemaFile = Path.Combine(folder, $"UserGameStatsSchema_{appId}.bin");
                if (!File.Exists(schemaFile)) return null;
                string statsFile = FindStatsFile(folder, appId, account);
                return Compute(File.ReadAllBytes(schemaFile), statsFile == null ? null : File.ReadAllBytes(statsFile), appId);
            }
            catch
            {
                return null;
            }
        }

        public static PlayAchievementProgress Compute(byte[] schemaBytes, byte[] statsBytes, string appId)
        {
            Dictionary<string, object> schema = PlayKeyValues.Parse(schemaBytes);
            Dictionary<string, object> root = PlayKeyValues.Child(schema, appId) ?? schema.Values.OfType<Dictionary<string, object>>().FirstOrDefault();
            Dictionary<string, object> stats = PlayKeyValues.Child(root, "stats");
            if (stats == null) return null;

            var bitsByStat = new Dictionary<string, List<int>>();
            foreach (var stat in stats)
            {
                if (!(stat.Value is Dictionary<string, object> definition)) continue;
                Dictionary<string, object> bits = PlayKeyValues.Child(definition, "bits");
                object type = definition.TryGetValue("type", out object value) ? value : null;
                bool achievementStat = (type is string text && (text.Equals("ACHIEVEMENTS", StringComparison.OrdinalIgnoreCase) || text == "4")) || (type is int number && number == 4);
                if (!achievementStat || bits == null) continue;

                var indexes = new List<int>();
                foreach (var bit in bits)
                {
                    long? index = PlayKeyValues.Number(bit.Value as Dictionary<string, object>, "bit");
                    if (index == null && int.TryParse(bit.Key, out int keyIndex)) index = keyIndex;
                    if (index != null && index >= 0 && index < 32) indexes.Add((int)index);
                }
                bitsByStat[stat.Key] = indexes;
            }

            int total = bitsByStat.Values.Sum(list => list.Count);
            if (total == 0) return null;

            int unlocked = 0;
            long lastUnlocked = 0;
            if (statsBytes != null)
            {
                Dictionary<string, object> userStats = PlayKeyValues.Parse(statsBytes);
                Dictionary<string, object> cache = PlayKeyValues.Child(userStats, "cache") ?? userStats;
                foreach (var stat in bitsByStat)
                {
                    Dictionary<string, object> node = PlayKeyValues.Child(cache, stat.Key);
                    long data = PlayKeyValues.Number(node, "data") ?? 0;
                    uint mask = unchecked((uint)data);
                    unlocked += stat.Value.Count(index => (mask >> index & 1) == 1);

                    Dictionary<string, object> times = PlayKeyValues.Child(node, "AchievementTimes");
                    if (times == null) continue;
                    foreach (var time in times)
                    {
                        long seconds = time.Value is int value32 ? (uint)value32 : time.Value is long value64 ? value64 : 0;
                        if (seconds > lastUnlocked) lastUnlocked = seconds;
                    }
                }
            }

            return new PlayAchievementProgress
            {
                Unlocked = Math.Min(unlocked, total),
                Total = total,
                LastUnlocked = lastUnlocked > 0 ? PlayLogStore.FormatTime(DateTimeOffset.FromUnixTimeSeconds(lastUnlocked).UtcDateTime) : null,
            };
        }

        private static string FindStatsFile(string folder, string appId, string account)
        {
            if (account != null)
            {
                string preferred = Path.Combine(folder, $"UserGameStats_{account}_{appId}.bin");
                if (File.Exists(preferred)) return preferred;
            }
            return Directory.EnumerateFiles(folder, $"UserGameStats_*_{appId}.bin")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
    }
}
