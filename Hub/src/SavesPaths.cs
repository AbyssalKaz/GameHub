using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GameHub
{
    internal sealed class SaveContext
    {
        public string Base { get; set; }
        public string GameFolder { get; set; }
        public List<string> Roots { get; set; } = new List<string>();
        public string StoreGameId { get; set; }
        public string Store { get; set; }
        public string Home { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public string AppData { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        public string LocalAppData { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        public string Documents { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public string ProgramData { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        public string Public { get; set; } = Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public";
        public string WindowsDir { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        public string UserName { get; set; } = Environment.UserName;
    }

    internal static class SavesPaths
    {
        private static readonly Regex Placeholder = new Regex("<([A-Za-z]+)>", RegexOptions.Compiled);

        public static List<string> Expand(string template, SaveContext context)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(template)) return results;

            var unknown = false;
            var multi = new List<string>();
            string single = Placeholder.Replace(template, match =>
            {
                switch (match.Groups[1].Value)
                {
                    case "base":
                        if (string.IsNullOrEmpty(context.Base)) unknown = true;
                        return Escape(context.Base ?? "");
                    case "game":
                        if (string.IsNullOrEmpty(context.GameFolder)) unknown = true;
                        return Escape(context.GameFolder ?? "");
                    case "root":
                        if (context.Roots.Count == 0) unknown = true;
                        multi.AddRange(context.Roots);
                        return "\u0001";
                    case "home": return Escape(context.Home);
                    case "winAppData": return Escape(context.AppData);
                    case "winLocalAppData": return Escape(context.LocalAppData);
                    case "winLocalAppDataLow": return Escape(Path.Combine(context.Home, @"AppData\LocalLow"));
                    case "winDocuments": return Escape(context.Documents);
                    case "winProgramData": return Escape(context.ProgramData);
                    case "winPublic": return Escape(context.Public);
                    case "winDir": return Escape(context.WindowsDir);
                    case "osUserName": return Escape(context.UserName);
                    case "storeUserId": return "*";
                    case "storeGameId":
                        if (string.IsNullOrEmpty(context.StoreGameId)) unknown = true;
                        return context.StoreGameId ?? "";
                    default:
                        unknown = true;
                        return "";
                }
            });
            if (unknown) return results;

            IEnumerable<string> variants = single.Contains("\u0001")
                ? multi.Distinct(StringComparer.OrdinalIgnoreCase).Select(root => single.Replace("\u0001", Escape(root)))
                : new[] { single };

            foreach (string variant in variants)
            {
                string normalized = Normalize(variant);
                if (normalized != null && !results.Contains(normalized, StringComparer.OrdinalIgnoreCase)) results.Add(normalized);
            }
            return results;
        }

        public static string Escape(string literal)
        {
            if (string.IsNullOrEmpty(literal)) return literal;
            var builder = new StringBuilder(literal.Length);
            foreach (char character in literal)
            {
                if (character == '*' || character == '?' || character == '[' || character == ']') builder.Append('[').Append(character).Append(']');
                else builder.Append(character);
            }
            return builder.ToString();
        }

        private static string Normalize(string pattern)
        {
            string path = pattern.Replace('/', '\\');
            while (path.Contains("\\\\")) path = path.Replace("\\\\", "\\");
            path = path.TrimEnd('\\');
            if (path.Length < 3 || !char.IsLetter(path[0]) || path[1] != ':' || path[2] != '\\') return null;
            var segments = path.Split('\\').ToList();
            for (int index = 1; index < segments.Count; index++)
            {
                if (segments[index] == ".")
                {
                    segments.RemoveAt(index--);
                }
                else if (segments[index] == "..")
                {
                    if (index < 2) return null;
                    segments.RemoveRange(index - 1, 2);
                    index -= 2;
                }
            }
            return string.Join("\\", segments);
        }

        public static bool HasWildcards(string segment) => segment.IndexOfAny(new[] { '*', '?', '[' }) >= 0;

        public static string Unescape(string segment)
        {
            return Regex.Replace(segment, @"\[(.)\]", "$1");
        }

        public static Regex SegmentRegex(string segment)
        {
            var builder = new StringBuilder("^");
            for (int index = 0; index < segment.Length; index++)
            {
                char current = segment[index];
                switch (current)
                {
                    case '*':
                        builder.Append(".*");
                        break;
                    case '?':
                        builder.Append('.');
                        break;
                    case '[':
                        int close = segment.IndexOf(']', index + 2);
                        if (close < 0)
                        {
                            builder.Append(@"\[");
                            break;
                        }
                        string inner = segment.Substring(index + 1, close - index - 1);
                        bool negate = inner.StartsWith("!");
                        if (negate) inner = inner.Substring(1);
                        builder.Append('[').Append(negate ? "^" : "").Append(inner.Replace("\\", "\\\\").Replace("[", "\\[").Replace("]", "\\]").Replace("^", "\\^")).Append(']');
                        index = close;
                        break;
                    default:
                        builder.Append(Regex.Escape(current.ToString()));
                        break;
                }
            }
            builder.Append('$');
            return new Regex(builder.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        public static List<string> Find(string pattern, int limit = 200)
        {
            var found = new List<string>();
            string[] segments = pattern.Split('\\');
            int firstWild = Array.FindIndex(segments, HasWildcards);
            if (firstWild < 0)
            {
                string literal = Unescape(pattern);
                if (File.Exists(literal) || Directory.Exists(literal)) found.Add(literal);
                return found;
            }

            string start = string.Join("\\", segments.Take(firstWild).Select(Unescape));
            if (firstWild == 1) start += "\\";
            if (!Directory.Exists(start)) return found;
            Walk(start.TrimEnd('\\'), segments, firstWild, found, limit, 0);
            return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void Walk(string current, string[] segments, int index, List<string> found, int limit, int depth)
        {
            if (found.Count >= limit || depth > 24) return;
            if (index >= segments.Length)
            {
                found.Add(current);
                return;
            }

            string segment = segments[index];
            bool last = index == segments.Length - 1;

            if (segment == "**")
            {
                Walk(current, segments, index + 1, found, limit, depth + 1);
                foreach (string child in SafeDirectories(current))
                {
                    if (IsLink(child)) continue;
                    Walk(child, segments, index, found, limit, depth + 1);
                }
                return;
            }

            if (!HasWildcards(segment))
            {
                string next = Path.Combine(current + "\\", Unescape(segment));
                if (last ? (File.Exists(next) || Directory.Exists(next)) : Directory.Exists(next)) Walk(next, segments, index + 1, found, limit, depth + 1);
                return;
            }

            Regex matcher = SegmentRegex(segment);
            IEnumerable<string> candidates = last ? SafeEntries(current) : SafeDirectories(current);
            foreach (string candidate in candidates)
            {
                if (!matcher.IsMatch(Path.GetFileName(candidate))) continue;
                Walk(candidate, segments, index + 1, found, limit, depth + 1);
                if (found.Count >= limit) return;
            }
        }

        private static bool IsLink(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return true;
            }
        }

        private static IEnumerable<string> SafeDirectories(string folder)
        {
            try
            {
                return Directory.GetDirectories(folder);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> SafeEntries(string folder)
        {
            try
            {
                return Directory.GetFileSystemEntries(folder);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
