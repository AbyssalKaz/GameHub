using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class MetaMovie
    {
        public string Name { get; set; }
        public string Thumb { get; set; }
        public string Poster { get; set; }
        public string Mp4 { get; set; }
        public string Webm { get; set; }
        public string Micro { get; set; }
        public string MicroMp4 { get; set; }
        public string Dash { get; set; }
        public string Hls { get; set; }
    }

    internal sealed class MetaNewsItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Date { get; set; }
        public string Author { get; set; }
        public string Feed { get; set; }
        public string Excerpt { get; set; }
        public bool Patch { get; set; }
    }

    internal sealed class MetaRecord
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Genres { get; set; }
        public List<string> Developers { get; set; }
        public List<string> Publishers { get; set; }
        public string ReleaseDate { get; set; }
        public string ReleaseText { get; set; }
        public bool ComingSoon { get; set; }
        public List<string> Screenshots { get; set; }
        public List<string> ScreenshotThumbs { get; set; }
        public List<MetaMovie> Movies { get; set; }
        public int? Metacritic { get; set; }
        public string MetacriticUrl { get; set; }
        public int? ReviewScore { get; set; }
        public string ReviewSummary { get; set; }
        public int? ReviewCount { get; set; }
        public string Website { get; set; }
        public bool Missing { get; set; }
        public string DetailsAt { get; set; }
        public string ItemsAt { get; set; }
        public string ReviewsAt { get; set; }
        public string NewsAt { get; set; }
        public List<MetaNewsItem> News { get; set; }
    }

    internal sealed class MetaNameMatch
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string CheckedAt { get; set; }
    }

    internal sealed class MetaHltb
    {
        public double? Main { get; set; }
        public double? Extra { get; set; }
        public double? Complete { get; set; }
        public string Name { get; set; }
        public string GameId { get; set; }
        public string CheckedAt { get; set; }
    }

    internal sealed class MetaCache
    {
        public Dictionary<string, MetaRecord> Apps { get; set; } = new Dictionary<string, MetaRecord>();
        public Dictionary<string, MetaNameMatch> Names { get; set; } = new Dictionary<string, MetaNameMatch>();
        public Dictionary<string, MetaHltb> Hltb { get; set; } = new Dictionary<string, MetaHltb>();
        public Dictionary<string, string> ArtTried { get; set; } = new Dictionary<string, string>();
    }

    internal sealed class MetaGate
    {
        private sealed class Slot
        {
            public readonly SemaphoreSlim Lock = new SemaphoreSlim(1);
            public DateTime Next = DateTime.MinValue;
        }

        private readonly Dictionary<string, Slot> slots = new Dictionary<string, Slot>();

        private Slot Get(string host)
        {
            lock (slots)
            {
                if (!slots.TryGetValue(host, out Slot slot))
                {
                    slot = new Slot();
                    slots[host] = slot;
                }
                return slot;
            }
        }

        public async Task<bool> WaitAsync(string host, TimeSpan spacing, CancellationToken token, TimeSpan? maxWait = null)
        {
            Slot slot = Get(host);
            await slot.Lock.WaitAsync(token);
            try
            {
                TimeSpan wait = slot.Next - DateTime.UtcNow;
                if (maxWait.HasValue && wait > maxWait.Value) return false;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, token);
                slot.Next = DateTime.UtcNow + spacing;
                return true;
            }
            finally
            {
                slot.Lock.Release();
            }
        }

        public void Cooldown(string host, TimeSpan span)
        {
            Slot slot = Get(host);
            DateTime until = DateTime.UtcNow + span;
            if (until > slot.Next) slot.Next = until;
        }
    }

    internal static class MetaText
    {
        private static readonly Regex Tags = new Regex("<[^>]+>|\\[/?[a-z0-9*]+(=[^\\]]*)?\\]", RegexOptions.IgnoreCase);
        private static readonly Regex ClanImages = new Regex("\\{STEAM_CLAN_(LOC_)?IMAGE\\}\\S*|https?://\\S+\\.(png|jpe?g|gif|webp)\\S*", RegexOptions.IgnoreCase);
        private static readonly Regex Spaces = new Regex("\\s+");
        private static readonly Regex Junk = new Regex("\\b(soundtrack|ost|demo|playtest|artbook|art book|season pass|dedicated server|sdk|benchmark)\\b", RegexOptions.IgnoreCase);

        public static string Plain(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;
            string text = ClanImages.Replace(html, " ");
            text = Tags.Replace(text, " ");
            text = WebUtility.HtmlDecode(text);
            text = Spaces.Replace(text, " ").Trim();
            return text.Length == 0 ? null : text;
        }

        public static bool IsJunk(string name) => name != null && Junk.IsMatch(name);

        public static string SearchName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return Spaces.Replace(Regex.Replace(name, "[™®©]", " "), " ").Trim();
        }

        public static bool Fresh(string stamp, TimeSpan ttl)
        {
            return stamp != null && DateTime.TryParse(stamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime time) && DateTime.UtcNow - time.ToUniversalTime() < ttl;
        }

        public static string Now() => DateTime.UtcNow.ToString("o");

        public static List<string> Names(object list, string key)
        {
            if (!(list is object[] items)) return null;
            var names = items.OfType<Dictionary<string, object>>().Select(item => item.GetString(key)).Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct().ToList();
            return names.Count == 0 ? null : names;
        }

        public static List<string> Strings(object list)
        {
            if (!(list is object[] items)) return null;
            var values = items.OfType<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct().ToList();
            return values.Count == 0 ? null : values;
        }

        public static int? Int(object value)
        {
            if (value == null) return null;
            return int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out int number) ? number : (int?)null;
        }

        public static string ParseReleaseDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string[] formats = { "d MMM, yyyy", "MMM d, yyyy", "d MMMM, yyyy", "MMMM d, yyyy", "d MMM yyyy", "MMM d yyyy", "yyyy-MM-dd" };
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            if (DateTime.TryParseExact(text.Trim(), formats, culture, System.Globalization.DateTimeStyles.None, out DateTime date)) return date.ToString("yyyy-MM-dd");
            return null;
        }
    }
}
