using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GameHub
{
    internal sealed class FreeOffer
    {
        public string Key { get; set; }
        public string Store { get; set; }
        public string Section { get; set; }
        public string Title { get; set; }
        public string Image { get; set; }
        public string ImageFit { get; set; }
        public string StoreUrl { get; set; }
        public string ClientUrl { get; set; }
        public string OriginalPrice { get; set; }
        public string StartsAt { get; set; }
        public string EndsAt { get; set; }
        public string ReleasedAt { get; set; }
        public string SteamAppId { get; set; }
        public string Reviews { get; set; }
    }

    internal sealed class FreeGamesSnapshot
    {
        public string UpdatedAt { get; set; }
        public string Country { get; set; }
        public List<FreeOffer> Offers { get; set; } = new List<FreeOffer>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    internal sealed class FreeGamesService
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(45);
        private static readonly string CacheFile = System.IO.Path.Combine(HubPaths.CacheDir, "free-games.json");
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly HttpClient Http = CreateClient();
        private static FreeGamesSnapshot cached = Json.ReadFile<FreeGamesSnapshot>(CacheFile);

        public static event Action<FreeGamesSnapshot> Updated;

        public static FreeGamesSnapshot Cached => cached;

        private static HttpClient CreateClient()
        {
            var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate })
            {
                Timeout = TimeSpan.FromSeconds(25),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GameHub/1.0");
            return client;
        }

        public async Task<FreeGamesSnapshot> GetAsync(bool force)
        {
            FreeGamesSnapshot fresh;
            await Gate.WaitAsync();
            try
            {
                if (!force && cached?.UpdatedAt != null && DateTime.UtcNow - DateTime.Parse(cached.UpdatedAt).ToUniversalTime() < MaxAge)
                {
                    return cached;
                }

                string country = SteamStore.Country;
                var snapshot = new FreeGamesSnapshot { Country = country, UpdatedAt = DateTime.UtcNow.ToString("o") };

                var sources = new (string Name, Func<Task<List<FreeOffer>>> Load)[]
                {
                    ("Epic Games", () => GetEpicAsync(country)),
                    ("Steam", GetSteamGiveawaysAsync),
                    ("Steam free-to-play", GetSteamNewFreeToPlayAsync),
                    ("Steam free weekends", GetSteamFreeWeekendsAsync),
                    ("GOG", () => GetGogAsync(country)),
                    ("Ubisoft", GetUbisoftAsync),
                    ("itch.io", GetItchAsync),
                };

                var results = await Task.WhenAll(sources.Select(async source =>
                {
                    try
                    {
                        return (source.Name, Offers: await source.Load(), Error: (string)null);
                    }
                    catch (Exception error)
                    {
                        return (source.Name, Offers: new List<FreeOffer>(), Error: error.Message);
                    }
                }));

                foreach (var result in results)
                {
                    if (result.Error != null) snapshot.Errors.Add(result.Name + ": " + result.Error);
                    foreach (FreeOffer offer in result.Offers)
                    {
                        if (snapshot.Offers.All(existing => existing.Key != offer.Key)) snapshot.Offers.Add(offer);
                    }
                }

                if (snapshot.Errors.Count > 0 && snapshot.Offers.Count == 0 && cached != null)
                {
                    cached.Errors = snapshot.Errors;
                    return cached;
                }

                cached = snapshot;
                Json.WriteFile(CacheFile, cached);
                fresh = cached;
            }
            finally
            {
                Gate.Release();
            }

            try
            {
                Updated?.Invoke(fresh);
            }
            catch
            {
            }
            return fresh;
        }

        private static async Task<List<FreeOffer>> GetEpicAsync(string country)
        {
            string url = $"https://store-site-backend-static-ipv4.ak.epicgames.com/freeGamesPromotions?locale=en-US&country={country}&allowCountries={country}";
            object root = Json.Parse(await Http.GetStringAsync(url));
            var elements = SteamStore.Path(root, "data", "Catalog", "searchStore", "elements") as object[] ?? new object[0];
            var offers = new List<FreeOffer>();
            DateTime now = DateTime.UtcNow;

            foreach (Dictionary<string, object> element in elements.OfType<Dictionary<string, object>>())
            {
                var current = FreeWindows(SteamStore.Path(element, "promotions", "promotionalOffers")).FirstOrDefault(window => window.Start <= now && window.End > now);
                var upcoming = FreeWindows(SteamStore.Path(element, "promotions", "upcomingPromotionalOffers")).FirstOrDefault(window => window.Start > now);
                if (current.End == default && upcoming.End == default) continue;

                string slug = EpicSlug(element);
                var images = (element.TryGetValue("keyImages", out object keyImages) ? keyImages as object[] : null) ?? new object[0];
                string image = images.OfType<Dictionary<string, object>>()
                    .OrderBy(entry => Array.IndexOf(new[] { "OfferImageWide", "DieselStoreFrontWide", "featuredMedia", "Thumbnail", "OfferImageTall" }, entry.GetString("type")) is int rank && rank >= 0 ? rank : 99)
                    .Select(entry => entry.GetString("url"))
                    .FirstOrDefault(link => !string.IsNullOrEmpty(link));

                bool isCurrent = current.End != default;
                var window = isCurrent ? current : upcoming;
                offers.Add(new FreeOffer
                {
                    Key = "epic:" + element.GetString("id"),
                    Store = "epic",
                    Section = isCurrent ? "now" : "upcoming",
                    Title = element.GetString("title"),
                    Image = image,
                    StoreUrl = slug == null ? "https://store.epicgames.com/en-US/free-games" : "https://store.epicgames.com/en-US/p/" + slug,
                    ClientUrl = slug == null ? null : "com.epicgames.launcher://store/p/" + slug,
                    OriginalPrice = SteamStore.Path(element, "price", "totalPrice", "fmtPrice", "originalPrice") as string,
                    StartsAt = window.Start.ToString("o"),
                    EndsAt = window.End.ToString("o"),
                });
            }
            return offers;
        }

        private static IEnumerable<(DateTime Start, DateTime End)> FreeWindows(object groups)
        {
            foreach (Dictionary<string, object> group in (groups as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
            {
                foreach (Dictionary<string, object> offer in (SteamStore.Path(group, "promotionalOffers") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
                {
                    object percentage = SteamStore.Path(offer, "discountSetting", "discountPercentage");
                    if (percentage == null || Convert.ToInt32(percentage) != 0) continue;

                    if (DateTime.TryParse(offer.GetString("startDate"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime start) &&
                        DateTime.TryParse(offer.GetString("endDate"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime end))
                    {
                        yield return (start, end);
                    }
                }
            }
        }

        private static string EpicSlug(Dictionary<string, object> element)
        {
            foreach (Dictionary<string, object> mapping in (SteamStore.Path(element, "catalogNs", "mappings") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
            {
                string slug = mapping.GetString("pageSlug");
                if (!string.IsNullOrEmpty(slug) && mapping.GetString("pageType") == "productHome") return slug;
            }
            foreach (Dictionary<string, object> mapping in (SteamStore.Path(element, "offerMappings") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
            {
                string slug = mapping.GetString("pageSlug");
                if (!string.IsNullOrEmpty(slug)) return slug;
            }

            string product = element.GetString("productSlug");
            if (!string.IsNullOrEmpty(product) && product != "[]") return product.Replace("/home", "");
            string urlSlug = element.GetString("urlSlug");
            return string.IsNullOrEmpty(urlSlug) || urlSlug.Length == 32 ? null : urlSlug;
        }

        private static async Task<List<FreeOffer>> GetSteamGiveawaysAsync()
        {
            List<string> ids = await SteamStore.SearchResultAppIdsAsync(Http, "maxprice=free&specials=1&category1=998&start=0&count=50");
            if (ids.Count == 0) return new List<FreeOffer>();

            var items = await SteamStore.GetItemsAsync(Http, ids, purchase: true, release: true, reviews: true);
            var offers = new List<FreeOffer>();
            foreach (string id in ids)
            {
                if (!items.TryGetValue(id, out Dictionary<string, object> item)) continue;
                object option = SteamStore.Path(item, "best_purchase_option");
                offers.Add(SteamOffer(id, item, "now", SteamStore.UnixToIso(SteamStore.Path(option, "active_discounts", 0, "discount_end_date")), SteamStore.Path(option, "formatted_original_price") as string));
            }
            return offers;
        }

        private static async Task<List<FreeOffer>> GetSteamNewFreeToPlayAsync()
        {
            List<string> ids = (await SteamStore.SearchResultAppIdsAsync(Http, "filter=popularnew&sort_by=Released_DESC&maxprice=free&category1=998&start=0&count=24")).Take(16).ToList();
            if (ids.Count == 0) return new List<FreeOffer>();

            var items = await SteamStore.GetItemsAsync(Http, ids, release: true, reviews: true);
            return ids.Where(items.ContainsKey).Select(id => SteamOffer(id, items[id], "freeToPlay", null, null)).ToList();
        }

        private static async Task<List<FreeOffer>> GetSteamFreeWeekendsAsync()
        {
            var root = Json.ParseObject(await Http.GetStringAsync("https://store.steampowered.com/api/featuredcategories?l=english&cc=" + SteamStore.Country));
            var spotlightIds = new List<string>();
            var candidateIds = new List<string>();

            foreach (var category in root?.Values.OfType<Dictionary<string, object>>() ?? Enumerable.Empty<Dictionary<string, object>>())
            {
                foreach (Dictionary<string, object> item in (SteamStore.Path(category, "items") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
                {
                    string url = item.GetString("url") ?? "";
                    Match app = Regex.Match(url, @"/app/(\d+)");
                    string id = app.Success ? app.Groups[1].Value : item.TryGetValue("id", out object raw) ? Convert.ToString(raw, CultureInfo.InvariantCulture) : null;
                    if (string.IsNullOrEmpty(id) || id == "0") continue;

                    string text = (item.GetString("name") ?? "") + " " + (item.GetString("body") ?? "");
                    if (app.Success && Regex.IsMatch(text, @"free\s*(weekend|week-end|to play this|play days)|play\s+(it\s+)?(for\s+)?free", RegexOptions.IgnoreCase))
                    {
                        spotlightIds.Add(id);
                    }
                    candidateIds.Add(id);
                }
            }

            var ids = spotlightIds.Concat(candidateIds).Distinct().Take(90).ToList();
            if (ids.Count == 0) return new List<FreeOffer>();

            var items = await SteamStore.GetItemsAsync(Http, ids, release: true, reviews: true);
            var offers = new List<FreeOffer>();
            foreach (string id in ids)
            {
                if (!items.TryGetValue(id, out Dictionary<string, object> item)) continue;
                object weekend = SteamStore.Path(item, "free_weekend");
                if (weekend == null && !spotlightIds.Contains(id)) continue;

                FreeOffer offer = SteamOffer(id, item, "weekend", SteamStore.UnixToIso(SteamStore.Path(weekend, "end_time")), null);
                offer.Key = "steam-weekend:" + id;
                offer.StartsAt = SteamStore.UnixToIso(SteamStore.Path(weekend, "start_time"));
                offers.Add(offer);
            }
            return offers;
        }

        private static FreeOffer SteamOffer(string appId, Dictionary<string, object> item, string section, string endsAt, string originalPrice)
        {
            return new FreeOffer
            {
                Key = "steam:" + appId,
                Store = "steam",
                Section = section,
                Title = item.GetString("name"),
                Image = SteamStore.AssetUrl(item, "main_capsule", "header") ?? $"{SteamStore.AssetBase}steam/apps/{appId}/header.jpg",
                StoreUrl = "https://store.steampowered.com/app/" + appId,
                ClientUrl = "steam://store/" + appId,
                OriginalPrice = originalPrice,
                EndsAt = endsAt,
                ReleasedAt = SteamStore.UnixToIso(SteamStore.Path(item, "release", "steam_release_date")),
                SteamAppId = appId,
                Reviews = SteamStore.Path(item, "reviews", "summary_filtered", "review_score_label") as string,
            };
        }

        private static async Task<List<FreeOffer>> GetGogAsync(string country)
        {
            var offers = new List<FreeOffer>();
            string currency = DealsMoney.CurrencyFor(country);
            string catalog = $"https://catalog.gog.com/v1/catalog?limit=24&price=between:0,0&discounted=eq:true&order=desc:trending&productType=in:game,pack,dlc&page=1&countryCode={country}&locale=en-US&currencyCode={currency}";
            var root = Json.ParseObject(await Http.GetStringAsync(catalog));
            foreach (Dictionary<string, object> product in (SteamStore.Path(root, "products") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
            {
                offers.Add(GogOffer(product));
            }

            FreeOffer giveaway = await GetGogGiveawayAsync(country, currency);
            if (giveaway != null && offers.All(offer => offer.Key != giveaway.Key)) offers.Insert(0, giveaway);
            return offers;
        }

        private static FreeOffer GogOffer(Dictionary<string, object> product)
        {
            string slug = product.GetString("slug");
            return new FreeOffer
            {
                Key = "gog:" + (product.GetString("id") ?? slug),
                Store = "gog",
                Section = "now",
                Title = product.GetString("title"),
                Image = product.GetString("coverHorizontal") ?? product.GetString("coverVertical"),
                StoreUrl = "https://www.gog.com/en/game/" + slug,
                OriginalPrice = SteamStore.Path(product, "price", "base") as string,
                ReleasedAt = ParseGogDate(product.GetString("releaseDate")),
            };
        }

        private static string ParseGogDate(string text)
        {
            return DateTime.TryParseExact(text, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date)
                ? date.ToString("o")
                : null;
        }

        private static async Task<FreeOffer> GetGogGiveawayAsync(string country, string currency)
        {
            string html = await Http.GetStringAsync("https://www.gog.com/en/");
            int start = html.IndexOf("class=\"giveaway", StringComparison.OrdinalIgnoreCase);
            if (start < 0) start = html.IndexOf("<giveaway", StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;

            string block = html.Substring(start, Math.Min(25000, html.Length - start));
            Match link = Regex.Match(block, "href=\"(?:https://www\\.gog\\.com)?/(?:[a-z]{2}/)?game/([a-z0-9_]+)\"", RegexOptions.IgnoreCase);
            if (!link.Success) return null;
            string slug = link.Groups[1].Value;

            string endsAt = null;
            Match end = Regex.Match(block, "end-?date=\"(\\d{10,13})\"|\"endDate\"\\s*:\\s*\"?(\\d{10,13})", RegexOptions.IgnoreCase);
            if (end.Success && long.TryParse(end.Groups[1].Success ? end.Groups[1].Value : end.Groups[2].Value, out long stamp))
            {
                endsAt = (stamp > 9999999999 ? DateTimeOffset.FromUnixTimeMilliseconds(stamp) : DateTimeOffset.FromUnixTimeSeconds(stamp)).UtcDateTime.ToString("o");
            }

            string query = Uri.EscapeDataString(slug.Replace('_', ' '));
            var root = Json.ParseObject(await Http.GetStringAsync($"https://catalog.gog.com/v1/catalog?limit=10&query=like:{query}&productType=in:game,pack,dlc&countryCode={country}&locale=en-US&currencyCode={currency}"));
            var product = (SteamStore.Path(root, "products") as object[] ?? new object[0]).OfType<Dictionary<string, object>>().FirstOrDefault(entry => entry.GetString("slug") == slug);

            FreeOffer offer = product != null ? GogOffer(product) : new FreeOffer
            {
                Key = "gog:" + slug,
                Store = "gog",
                Section = "now",
                Title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug.Replace('_', ' ')),
                StoreUrl = "https://www.gog.com/en/game/" + slug,
            };
            offer.Key = "gog-giveaway:" + slug;
            offer.EndsAt = endsAt;
            offer.ClientUrl = "https://www.gog.com/en/#giveaway";
            return offer;
        }

        private static async Task<List<FreeOffer>> GetUbisoftAsync()
        {
            string html = await Http.GetStringAsync("https://free.ubisoft.com/");
            const string marker = "window.__PRELOADED_STATE__ = ";
            int start = html.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) throw new InvalidOperationException("page layout changed");
            start += marker.Length;
            int end = html.IndexOf("</script>", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) throw new InvalidOperationException("page layout changed");
            string json = html.Substring(start, end - start).Trim().TrimEnd(';');

            var root = Json.ParseObject(json);
            var feeds = SteamStore.Path(root, "NewsPersonalization") as Dictionary<string, object>;
            var offers = new List<FreeOffer>();
            DateTime now = DateTime.UtcNow;

            foreach (var feed in feeds?.Values.OfType<Dictionary<string, object>>() ?? Enumerable.Empty<Dictionary<string, object>>())
            {
                foreach (Dictionary<string, object> news in (SteamStore.Path(feed, "data", "news") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
                {
                    string type = news.GetString("type") ?? "";
                    string section = Regex.IsMatch(type, "giveaway|freegame|free2keep|keep", RegexOptions.IgnoreCase) ? "now"
                        : Regex.IsMatch(type, "freeweekend|freeweek", RegexOptions.IgnoreCase) ? "weekend"
                        : null;
                    if (section == null) continue;

                    string endsAt = ParseUbisoftDate(news.GetString("expirationDate"));
                    if (endsAt != null && DateTime.Parse(endsAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) < now) continue;

                    string link = (SteamStore.Path(news, "links", 0, "param") as string) ?? "https://free.ubisoft.com/";
                    if (!link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) link = "https://free.ubisoft.com/";
                    string title = Regex.Replace(news.GetString("title") ?? "", @"\s+(Free Weekend|Giveaway|Free Week-end)$", "", RegexOptions.IgnoreCase).Trim();

                    offers.Add(new FreeOffer
                    {
                        Key = "ubisoft:" + (news.GetString("newsId") ?? title),
                        Store = "ubisoft",
                        Section = section,
                        Title = title,
                        Image = news.GetString("mediaURL"),
                        ImageFit = "contain",
                        StoreUrl = link,
                        StartsAt = ParseUbisoftDate(news.GetString("publicationDate")),
                        EndsAt = endsAt,
                    });
                }
            }
            return offers;
        }

        private static string ParseUbisoftDate(string text)
        {
            if (string.IsNullOrEmpty(text) || text.StartsWith("2222", StringComparison.Ordinal)) return null;
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime date)
                ? date.ToString("o")
                : null;
        }

        private static async Task<List<FreeOffer>> GetItchAsync()
        {
            var feeds = new[] { "https://itch.io/games/top-rated/on-sale.xml", "https://itch.io/games/on-sale.xml" };
            var offers = new List<FreeOffer>();
            foreach (string feed in feeds)
            {
                XDocument document = XDocument.Parse(await Http.GetStringAsync(feed));
                foreach (XElement item in document.Descendants("item"))
                {
                    if ((string)item.Element("discountpercent") != "100") continue;
                    string link = (string)item.Element("link") ?? (string)item.Element("guid");
                    if (string.IsNullOrEmpty(link) || !link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;

                    string title = (string)item.Element("plainTitle") ?? (string)item.Element("title");
                    if (Regex.IsMatch(title ?? "", @"\b(demo|prototype|jam)\b", RegexOptions.IgnoreCase)) continue;

                    string endsAt = DateTime.TryParse((string)item.Element("saleends"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out DateTime ends)
                        ? ends.ToString("o")
                        : null;

                    offers.Add(new FreeOffer
                    {
                        Key = "itch:" + link,
                        Store = "itch",
                        Section = "now",
                        Title = title,
                        Image = (string)item.Element("imageurl"),
                        StoreUrl = link,
                        OriginalPrice = (string)item.Element("fullPrice"),
                        EndsAt = endsAt,
                    });
                }
            }
            return offers.GroupBy(offer => offer.Key).Select(group => group.First()).Take(12).ToList();
        }
    }
}
