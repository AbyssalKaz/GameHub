using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class ManifestCondition
    {
        public string Os { get; set; }
        public string Store { get; set; }
    }

    internal sealed class ManifestPath
    {
        public string Template { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public List<ManifestCondition> When { get; set; } = new List<ManifestCondition>();

        public bool AppliesTo(string store)
        {
            if (When.Count == 0) return true;
            return When.Any(condition =>
                (condition.Os == null || condition.Os == "windows") &&
                (condition.Store == null || store == null || string.Equals(condition.Store, store, StringComparison.OrdinalIgnoreCase)));
        }
    }

    internal sealed class ManifestEntry
    {
        public string Name { get; set; }
        public string Alias { get; set; }
        public List<string> SteamIds { get; set; } = new List<string>();
        public List<string> InstallDirs { get; set; } = new List<string>();
        public List<ManifestPath> Files { get; set; } = new List<ManifestPath>();
        public List<ManifestPath> Registry { get; set; } = new List<ManifestPath>();
    }

    internal sealed class ManifestQuery
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string SteamAppId { get; set; }
    }

    internal static class ManifestYaml
    {
        private struct Line
        {
            public int Indent;
            public string Text;
        }

        public static object Parse(IEnumerable<string> rawLines)
        {
            var lines = new List<Line>();
            foreach (string raw in rawLines)
            {
                string trimmed = raw.TrimEnd('\r', ' ', '\t');
                string content = trimmed.TrimStart(' ');
                if (content.Length == 0 || content.StartsWith("#") || content == "---" || content == "...") continue;
                lines.Add(new Line { Indent = trimmed.Length - content.Length, Text = content });
            }
            if (lines.Count == 0) return null;
            int index = 0;
            return ParseNode(lines, ref index, lines[0].Indent);
        }

        private static bool IsSequenceItem(string text) => text == "-" || text.StartsWith("- ");

        private static object ParseNode(List<Line> lines, ref int index, int indent)
        {
            if (index >= lines.Count) return null;
            return IsSequenceItem(lines[index].Text) ? ParseSequence(lines, ref index, indent) : ParseMapping(lines, ref index, indent);
        }

        private static List<object> ParseSequence(List<Line> lines, ref int index, int indent)
        {
            var items = new List<object>();
            while (index < lines.Count && lines[index].Indent == indent && IsSequenceItem(lines[index].Text))
            {
                string rest = lines[index].Text.Length > 1 ? lines[index].Text.Substring(2).TrimStart(' ') : "";
                if (rest.Length == 0)
                {
                    index++;
                    items.Add(index < lines.Count && lines[index].Indent > indent ? ParseNode(lines, ref index, lines[index].Indent) : null);
                }
                else if (IsSequenceItem(rest) || SplitKey(rest, out _, out _))
                {
                    int childIndent = indent + (lines[index].Text.Length - rest.Length);
                    lines[index] = new Line { Indent = childIndent, Text = rest };
                    items.Add(ParseNode(lines, ref index, childIndent));
                }
                else
                {
                    items.Add(ParseScalar(rest));
                    index++;
                }
            }
            return items;
        }

        private static Dictionary<string, object> ParseMapping(List<Line> lines, ref int index, int indent)
        {
            var map = new Dictionary<string, object>(StringComparer.Ordinal);
            while (index < lines.Count && lines[index].Indent == indent && !IsSequenceItem(lines[index].Text))
            {
                if (!SplitKey(lines[index].Text, out string key, out string value))
                {
                    index++;
                    continue;
                }
                index++;

                if (value.Length > 0)
                {
                    map[key] = ParseScalar(value);
                }
                else if (index < lines.Count && (lines[index].Indent > indent || (lines[index].Indent == indent && IsSequenceItem(lines[index].Text))))
                {
                    map[key] = ParseNode(lines, ref index, lines[index].Indent);
                }
                else
                {
                    map[key] = null;
                }
            }
            return map;
        }

        public static bool SplitKey(string text, out string key, out string value)
        {
            key = null;
            value = "";
            int position;
            if (text.StartsWith("\"") || text.StartsWith("'"))
            {
                key = ReadQuoted(text, out position);
                if (key == null) return false;
                while (position < text.Length && text[position] == ' ') position++;
                if (position >= text.Length || text[position] != ':') return false;
                value = text.Substring(position + 1).Trim();
                return true;
            }

            int separator = text.IndexOf(": ", StringComparison.Ordinal);
            if (separator < 0)
            {
                if (!text.EndsWith(":")) return false;
                key = text.Substring(0, text.Length - 1).TrimEnd();
                return true;
            }
            key = text.Substring(0, separator).TrimEnd();
            value = text.Substring(separator + 2).Trim();
            return true;
        }

        public static object ParseScalar(string text)
        {
            if (text == "{}") return new Dictionary<string, object>();
            if (text == "[]") return new List<object>();
            if (text.StartsWith("[") && text.EndsWith("]"))
            {
                return text.Substring(1, text.Length - 2).Split(',').Select(part => ParseScalar(part.Trim())).Where(part => part is string value && value.Length > 0).ToList();
            }
            if (text.StartsWith("\"") || text.StartsWith("'"))
            {
                return ReadQuoted(text, out _) ?? text;
            }
            int comment = text.IndexOf(" #", StringComparison.Ordinal);
            return comment >= 0 ? text.Substring(0, comment).TrimEnd() : text;
        }

        private static string ReadQuoted(string text, out int end)
        {
            char quote = text[0];
            var builder = new StringBuilder();
            int position = 1;
            while (position < text.Length)
            {
                char current = text[position];
                if (quote == '\'' && current == '\'')
                {
                    if (position + 1 < text.Length && text[position + 1] == '\'')
                    {
                        builder.Append('\'');
                        position += 2;
                        continue;
                    }
                    end = position + 1;
                    return builder.ToString();
                }
                if (quote == '"' && current == '"')
                {
                    end = position + 1;
                    return builder.ToString();
                }
                if (quote == '"' && current == '\\' && position + 1 < text.Length)
                {
                    char escaped = text[position + 1];
                    position += 2;
                    switch (escaped)
                    {
                        case 'n': builder.Append('\n'); break;
                        case 't': builder.Append('\t'); break;
                        case 'r': builder.Append('\r'); break;
                        case '0': builder.Append('\0'); break;
                        case 'u' when position + 4 <= text.Length && int.TryParse(text.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code):
                            builder.Append((char)code);
                            position += 4;
                            break;
                        case 'x' when position + 2 <= text.Length && int.TryParse(text.Substring(position, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int small):
                            builder.Append((char)small);
                            position += 2;
                            break;
                        default: builder.Append(escaped); break;
                    }
                    continue;
                }
                builder.Append(current);
                position++;
            }
            end = text.Length;
            return null;
        }
    }

    internal static class SavesManifest
    {
        public const string Source = "https://raw.githubusercontent.com/mtkennerly/ludusavi-manifest/master/data/manifest.yaml";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromDays(7);
        private static readonly object DownloadGate = new object();
        private static Task<bool> pendingDownload;

        public static string FilePath => Path.Combine(HubPaths.CacheDir, "ludusavi-manifest.yaml");
        private static string TagFile => FilePath + ".etag";

        public static bool IsAvailable => File.Exists(FilePath);

        public static DateTime? UpdatedAt => File.Exists(FilePath) ? File.GetLastWriteTimeUtc(FilePath) : (DateTime?)null;

        public static bool IsStale => !File.Exists(FilePath) || DateTime.UtcNow - File.GetLastWriteTimeUtc(FilePath) > RefreshInterval;

        public static Task<bool> EnsureFreshAsync(bool force = false)
        {
            lock (DownloadGate)
            {
                if (pendingDownload != null && !pendingDownload.IsCompleted) return pendingDownload;
                if (!force && !IsStale) return Task.FromResult(false);
                pendingDownload = Task.Run(DownloadAsync);
                return pendingDownload;
            }
        }

        private static async Task<bool> DownloadAsync()
        {
            Directory.CreateDirectory(HubPaths.CacheDir);
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(4) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GameHub/1.0");
            using var request = new HttpRequestMessage(HttpMethod.Get, Source);
            if (File.Exists(FilePath) && File.Exists(TagFile))
            {
                string tag = File.ReadAllText(TagFile).Trim();
                if (tag.Length > 0) request.Headers.TryAddWithoutValidation("If-None-Match", tag);
            }

            try
            {
                using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    File.SetLastWriteTimeUtc(FilePath, DateTime.UtcNow);
                    return false;
                }
                if (!response.IsSuccessStatusCode) return false;

                string temporary = FilePath + ".download";
                using (Stream body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (FileStream output = File.Create(temporary))
                {
                    await body.CopyToAsync(output).ConfigureAwait(false);
                }
                if (new FileInfo(temporary).Length < 1024 * 1024)
                {
                    File.Delete(temporary);
                    return false;
                }
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temporary, FilePath);
                string etag = response.Headers.ETag?.Tag;
                if (etag != null) File.WriteAllText(TagFile, etag);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string decomposed = name.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(character)) builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        public static Dictionary<string, ManifestEntry> Lookup(IEnumerable<ManifestQuery> queries) => Lookup(FilePath, queries);

        public static Dictionary<string, ManifestEntry> Lookup(string file, IEnumerable<ManifestQuery> queries)
        {
            var result = new Dictionary<string, ManifestEntry>();
            if (!File.Exists(file)) return result;

            List<ManifestQuery> list = queries.Where(query => query?.Key != null).ToList();
            var byName = new Dictionary<string, List<ManifestQuery>>();
            var bySteam = new Dictionary<string, List<ManifestQuery>>();
            foreach (ManifestQuery query in list)
            {
                string name = NormalizeName(query.Name);
                if (name.Length > 0) Add(byName, name, query);
                if (!string.IsNullOrEmpty(query.SteamAppId)) Add(bySteam, query.SteamAppId, query);
            }

            var scores = new Dictionary<string, int>();
            var aliasTargets = new Dictionary<string, List<ManifestQuery>>();

            void Consider(string name, List<string> block)
            {
                var matched = new List<(ManifestQuery Query, int Score)>();
                ManifestScan scan = ManifestScan.Of(block);
                foreach (string id in scan.SteamIds)
                {
                    if (bySteam.TryGetValue(id, out var hits)) matched.AddRange(hits.Select(hit => (hit, id == scan.PrimarySteamId ? 3 : 2)));
                }
                if (byName.TryGetValue(NormalizeName(name), out var named)) matched.AddRange(named.Select(hit => (hit, 1)));
                if (matched.Count == 0) return;

                ManifestEntry entry = null;
                foreach (var (query, score) in matched)
                {
                    if (scores.TryGetValue(query.Key, out int existing) && existing >= score) continue;
                    if (scan.Alias != null && score == 1)
                    {
                        string target = NormalizeName(scan.Alias);
                        if (target.Length > 0) Add(aliasTargets, target, query);
                        continue;
                    }
                    entry ??= Build(name, block);
                    if (entry == null) continue;
                    scores[query.Key] = score;
                    result[query.Key] = entry;
                }
            }

            ReadBlocks(file, Consider);

            var unresolved = aliasTargets
                .Select(pair => new KeyValuePair<string, List<ManifestQuery>>(pair.Key, pair.Value.Where(query => !result.ContainsKey(query.Key)).ToList()))
                .Where(pair => pair.Value.Count > 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            if (unresolved.Count > 0)
            {
                ReadBlocks(file, (name, block) =>
                {
                    if (!unresolved.TryGetValue(NormalizeName(name), out var waiting)) return;
                    ManifestEntry entry = Build(name, block);
                    if (entry == null) return;
                    foreach (ManifestQuery query in waiting)
                    {
                        if (!result.ContainsKey(query.Key)) result[query.Key] = entry;
                    }
                });
            }
            return result;
        }

        private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
        {
            if (!map.TryGetValue(key, out var list)) map[key] = list = new List<T>();
            list.Add(value);
        }

        private static void ReadBlocks(string file, Action<string, List<string>> consider)
        {
            using var reader = new StreamReader(file, Encoding.UTF8, true, 1 << 16);
            string currentName = null;
            var block = new List<string>(64);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0) continue;
                char first = line[0];
                if (first != ' ' && first != '#' && first != '-')
                {
                    if (currentName != null) consider(currentName, block);
                    block.Clear();
                    currentName = ManifestYaml.SplitKey(line.TrimEnd(), out string key, out _) ? key : null;
                    continue;
                }
                if (currentName != null && first == ' ') block.Add(line);
            }
            if (currentName != null) consider(currentName, block);
        }

        private sealed class ManifestScan
        {
            public string PrimarySteamId;
            public string Alias;
            public List<string> SteamIds = new List<string>();

            public static ManifestScan Of(List<string> block)
            {
                var scan = new ManifestScan();
                string section = null;
                string subsection = null;
                foreach (string line in block)
                {
                    int indent = line.Length - line.TrimStart(' ').Length;
                    string text = line.Trim();
                    if (indent == 2)
                    {
                        ManifestYaml.SplitKey(text, out section, out string value);
                        subsection = null;
                        if (section == "alias" && value.Length > 0) scan.Alias = ManifestYaml.ParseScalar(value) as string;
                    }
                    else if (indent == 4 && section == "steam" && text.StartsWith("id:"))
                    {
                        scan.PrimarySteamId = text.Substring(3).Trim().Trim('"', '\'');
                        scan.SteamIds.Add(scan.PrimarySteamId);
                    }
                    else if (indent == 4 && section == "id")
                    {
                        ManifestYaml.SplitKey(text, out subsection, out _);
                    }
                    else if (indent >= 6 && section == "id" && subsection == "steamExtra" && text.StartsWith("- "))
                    {
                        scan.SteamIds.Add(text.Substring(2).Trim().Trim('"', '\''));
                    }
                }
                return scan;
            }
        }

        private static ManifestEntry Build(string name, List<string> block)
        {
            if (!(ManifestYaml.Parse(block) is Dictionary<string, object> root)) return null;
            var entry = new ManifestEntry { Name = name, Alias = root.TryGetValue("alias", out object alias) ? alias as string : null };

            if (root.TryGetValue("steam", out object steam) && steam is Dictionary<string, object> steamMap && steamMap.TryGetValue("id", out object id) && id != null)
            {
                entry.SteamIds.Add(id.ToString());
            }
            if (root.TryGetValue("id", out object ids) && ids is Dictionary<string, object> idMap && idMap.TryGetValue("steamExtra", out object extra) && extra is List<object> extraIds)
            {
                entry.SteamIds.AddRange(extraIds.Where(value => value != null).Select(value => value.ToString()));
            }
            if (root.TryGetValue("installDir", out object dirs) && dirs is Dictionary<string, object> dirMap)
            {
                entry.InstallDirs.AddRange(dirMap.Keys);
            }
            entry.Files = ReadPaths(root, "files");
            entry.Registry = ReadPaths(root, "registry");
            return entry;
        }

        private static List<ManifestPath> ReadPaths(Dictionary<string, object> root, string section)
        {
            var paths = new List<ManifestPath>();
            if (!root.TryGetValue(section, out object value) || !(value is Dictionary<string, object> map)) return paths;

            foreach (var item in map)
            {
                var path = new ManifestPath { Template = item.Key };
                if (item.Value is Dictionary<string, object> details)
                {
                    if (details.TryGetValue("tags", out object tags) && tags is List<object> tagList)
                    {
                        path.Tags.AddRange(tagList.OfType<string>());
                    }
                    if (details.TryGetValue("when", out object when) && when is List<object> conditions)
                    {
                        foreach (Dictionary<string, object> condition in conditions.OfType<Dictionary<string, object>>())
                        {
                            path.When.Add(new ManifestCondition
                            {
                                Os = condition.TryGetValue("os", out object os) ? os as string : null,
                                Store = condition.TryGetValue("store", out object store) ? store as string : null,
                            });
                        }
                    }
                }
                paths.Add(path);
            }
            return paths;
        }
    }
}
