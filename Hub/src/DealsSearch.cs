using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class StorePrice
    {
        public string Store { get; set; }
        public string StoreName { get; set; }
        public long? Cents { get; set; }
        public long? RegularCents { get; set; }
        public string Currency { get; set; }
        public string PriceText { get; set; }
        public string RegularText { get; set; }
        public int Discount { get; set; }
        public bool Free { get; set; }
        public string Url { get; set; }
        public string ClientUrl { get; set; }
    }

    internal sealed class SearchGame
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Image { get; set; }
        public string SteamAppId { get; set; }
        public ItadLow Low { get; set; }
        public List<StorePrice> Prices { get; set; } = new List<StorePrice>();
    }

    internal sealed class DealsSearchResult
    {
        public string Query { get; set; }
        public string Currency { get; set; }
        public bool HasItad { get; set; }
        public List<SearchGame> Games { get; set; } = new List<SearchGame>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    internal sealed class DealsSearch
    {
        private const string EpicQuery = "query searchStoreQuery($keywords: String, $country: String!, $locale: String, $count: Int) { Catalog { searchStore(keywords: $keywords, country: $country, locale: $locale, count: $count, category: \"games/edition/base\", sortBy: \"relevancy\", sortDir: \"DESC\") { elements { title id productSlug urlSlug keyImages { type url } catalogNs { mappings(pageType: \"productHome\") { pageSlug pageType } } offerMappings { pageSlug pageType } price(country: $country) { totalPrice { discountPrice originalPrice discount currencyCode fmtPrice(locale: $locale) { originalPrice discountPrice } } } } } } }";
        private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

        private readonly ConcurrentDictionary<string, (DateTime At, DealsSearchResult Result)> memory = new ConcurrentDictionary<string, (DateTime, DealsSearchResult)>();

        public async Task<DealsSearchResult> SearchAsync(string query, string itadKey)
        {
            query = (query ?? "").Trim();
            string country = SteamStore.Country;
            string cacheKey = country + "|" + (string.IsNullOrWhiteSpace(itadKey) ? "" : "itad") + "|" + query.ToLowerInvariant();
            if (memory.TryGetValue(cacheKey, out var hit) && DateTime.UtcNow - hit.At < MaxAge) return hit.Result;

            var result = new DealsSearchResult { Query = query, Currency = DealsMoney.CurrencyFor(country) };
            if (query.Length < 2) return result;

            var itad = new DealsItad(itadKey);
            result.HasItad = itad.Enabled;

            var steamTask = Capture("Steam", () => SteamAsync(query, country));
            var epicTask = Capture("Epic Games", () => EpicAsync(query, country));
            var gogTask = Capture("GOG", () => GogAsync(query, country));
            var itadTask = itad.Enabled ? Capture("IsThereAnyDeal", () => itad.SearchAsync(query, country, 6)) : Task.FromResult((Value: new List<ItadGame>(), Error: (string)null));
            await Task.WhenAll(steamTask, epicTask, gogTask, itadTask);

            var groups = new List<SearchGame>();
            SearchGame GroupFor(string title, string image)
            {
                string key = DealsText.Key(title);
                SearchGame game = groups.FirstOrDefault(entry => entry.Key == key);
                if (game == null)
                {
                    game = new SearchGame { Key = key, Title = title, Image = image };
                    groups.Add(game);
                }
                game.Image ??= image;
                return game;
            }

            foreach (var source in new[] { steamTask.Result, epicTask.Result, gogTask.Result })
            {
                if (source.Error != null) result.Errors.Add(source.Error);
                foreach (var entry in source.Value)
                {
                    SearchGame game = GroupFor(entry.Title, entry.Image);
                    if (entry.SteamAppId != null) game.SteamAppId ??= entry.SteamAppId;
                    if (game.Prices.All(price => price.Store != entry.Price.Store)) game.Prices.Add(entry.Price);
                }
            }

            if (itadTask.Result.Error != null) result.Errors.Add(itadTask.Result.Error);
            foreach (ItadGame found in itadTask.Result.Value)
            {
                string key = DealsText.Key(found.Title);
                SearchGame game = groups.FirstOrDefault(entry => entry.Key == key);
                if (game == null)
                {
                    if (found.Deals.Count == 0) continue;
                    game = GroupFor(found.Title, found.Image);
                }
                game.Low ??= found.Low;
                foreach (ItadDeal deal in found.Deals.OrderBy(deal => deal.Cents))
                {
                    string store = StoreKeyFromShop(deal.Shop);
                    if (game.Prices.Any(price => price.Store == store)) continue;
                    game.Prices.Add(new StorePrice
                    {
                        Store = store,
                        StoreName = deal.Shop,
                        Cents = deal.Cents,
                        RegularCents = deal.RegularCents,
                        Currency = deal.Currency,
                        Discount = deal.Cut,
                        Free = deal.Cents == 0,
                        Url = deal.Url,
                    });
                }
            }

            string queryKey = DealsText.Key(query);
            result.Games = groups
                .Select((game, index) => (game, index))
                .OrderByDescending(entry => entry.game.Key == queryKey)
                .ThenByDescending(entry => entry.game.Key.StartsWith(queryKey, StringComparison.Ordinal))
                .ThenByDescending(entry => entry.game.Prices.Count)
                .ThenBy(entry => entry.index)
                .Select(entry => entry.game)
                .Take(10)
                .ToList();
            foreach (SearchGame game in result.Games)
            {
                game.Prices = game.Prices.OrderBy(price => price.Cents ?? long.MaxValue).ToList();
            }

            if (result.Errors.Count < 3) memory[cacheKey] = (DateTime.UtcNow, result);
            if (memory.Count > 60)
            {
                foreach (var old in memory.OrderBy(pair => pair.Value.At).Take(20).ToList()) memory.TryRemove(old.Key, out _);
            }
            return result;
        }

        private static string StoreKeyFromShop(string shop)
        {
            string key = DealsText.Key(shop);
            if (key.StartsWith("steam", StringComparison.Ordinal)) return "steam";
            if (key.StartsWith("epic", StringComparison.Ordinal)) return "epic";
            if (key.StartsWith("gog", StringComparison.Ordinal)) return "gog";
            if (key.Contains("ubisoft")) return "ubisoft";
            if (key.Contains("humble")) return "humble";
            if (key.StartsWith("itch", StringComparison.Ordinal)) return "itch";
            if (key.Contains("microsoft") || key.Contains("xbox")) return "xbox";
            if (key.StartsWith("ea", StringComparison.Ordinal)) return "ea";
            return "itad-" + key;
        }

        private static async Task<(T Value, string Error)> Capture<T>(string name, Func<Task<T>> load) where T : new()
        {
            try
            {
                return (await load(), null);
            }
            catch (Exception error)
            {
                return (new T(), name + ": " + error.Message);
            }
        }

        private sealed class Found
        {
            public string Title { get; set; }
            public string Image { get; set; }
            public string SteamAppId { get; set; }
            public StorePrice Price { get; set; }
        }

        private static async Task<List<Found>> SteamAsync(string query, string country)
        {
            string json = await DealsHttp.Client.GetStringAsync("https://store.steampowered.com/api/storesearch/?l=english&cc=" + country + "&term=" + Uri.EscapeDataString(query));
            var found = new List<Found>();
            foreach (Dictionary<string, object> item in (SteamStore.Path(Json.ParseObject(json), "items") as object[] ?? new object[0]).OfType<Dictionary<string, object>>().Take(8))
            {
                if (item.GetString("type") != "app") continue;
                string id = Convert.ToString(item["id"], CultureInfo.InvariantCulture);
                var price = item.TryGetValue("price", out object raw) ? raw as Dictionary<string, object> : null;
                long? final = DealsMoney.Cents(price != null && price.TryGetValue("final", out object finalValue) ? finalValue : null);
                long? initial = DealsMoney.Cents(price != null && price.TryGetValue("initial", out object initialValue) ? initialValue : null) ?? final;

                found.Add(new Found
                {
                    Title = item.GetString("name"),
                    Image = $"{SteamStore.AssetBase}steam/apps/{id}/header.jpg",
                    SteamAppId = id,
                    Price = new StorePrice
                    {
                        Store = "steam",
                        StoreName = "Steam",
                        Cents = final ?? 0,
                        RegularCents = initial ?? 0,
                        Currency = price?.GetString("currency"),
                        Discount = initial > 0 && final < initial ? (int)Math.Round(100.0 - final.Value * 100.0 / initial.Value) : 0,
                        Free = final == null || final == 0,
                        Url = "https://store.steampowered.com/app/" + id,
                        ClientUrl = "steam://store/" + id,
                    },
                });
            }
            return found;
        }

        private static async Task<List<Found>> EpicAsync(string query, string country)
        {
            string variables = Json.Serialize(new { keywords = query, country, locale = "en-US", count = 8 });
            string url = "https://store.epicgames.com/graphql?query=" + Uri.EscapeDataString(EpicQuery) + "&variables=" + Uri.EscapeDataString(variables);
            var root = Json.ParseObject(await DealsHttp.GetWithCurlFallbackAsync(url));
            var elements = SteamStore.Path(root, "data", "Catalog", "searchStore", "elements") as object[];
            if (elements == null) throw new InvalidOperationException("search is unavailable right now");

            var found = new List<Found>();
            foreach (Dictionary<string, object> element in elements.OfType<Dictionary<string, object>>())
            {
                string slug = EpicSlug(element);
                if (slug == null) continue;
                var total = SteamStore.Path(element, "price", "totalPrice") as Dictionary<string, object>;
                long? final = DealsMoney.Cents(total != null && total.TryGetValue("discountPrice", out object discountPrice) ? discountPrice : null);
                long? original = DealsMoney.Cents(total != null && total.TryGetValue("originalPrice", out object originalPrice) ? originalPrice : null) ?? final;
                string image = (element.TryGetValue("keyImages", out object keyImages) ? keyImages as object[] : null)?
                    .OfType<Dictionary<string, object>>()
                    .OrderBy(entry => Array.IndexOf(new[] { "OfferImageWide", "DieselStoreFrontWide", "Thumbnail" }, entry.GetString("type")) is int rank && rank >= 0 ? rank : 99)
                    .Select(entry => entry.GetString("url"))
                    .FirstOrDefault();

                found.Add(new Found
                {
                    Title = element.GetString("title"),
                    Image = image == null ? null : image + "?w=460&h=215&resize=1&quality=medium",
                    Price = new StorePrice
                    {
                        Store = "epic",
                        StoreName = "Epic Games",
                        Cents = final,
                        RegularCents = original,
                        Currency = total?.GetString("currencyCode"),
                        PriceText = SteamStore.Path(total, "fmtPrice", "discountPrice") as string,
                        RegularText = SteamStore.Path(total, "fmtPrice", "originalPrice") as string,
                        Discount = original > 0 && final < original ? (int)Math.Round(100.0 - final.Value * 100.0 / original.Value) : 0,
                        Free = final == 0,
                        Url = "https://store.epicgames.com/en-US/p/" + slug,
                        ClientUrl = "com.epicgames.launcher://store/p/" + slug,
                    },
                });
            }
            return found;
        }

        private static string EpicSlug(Dictionary<string, object> element)
        {
            foreach (string path in new[] { "catalogNs", "offerMappings" })
            {
                object node = path == "catalogNs" ? SteamStore.Path(element, "catalogNs", "mappings") : SteamStore.Path(element, "offerMappings");
                foreach (Dictionary<string, object> mapping in (node as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
                {
                    string slug = mapping.GetString("pageSlug");
                    if (!string.IsNullOrEmpty(slug)) return slug;
                }
            }
            string product = element.GetString("productSlug");
            if (!string.IsNullOrEmpty(product) && product != "[]") return product.Replace("/home", "");
            string urlSlug = element.GetString("urlSlug");
            return string.IsNullOrEmpty(urlSlug) || urlSlug.Length == 32 ? null : urlSlug;
        }

        private static async Task<List<Found>> GogAsync(string query, string country)
        {
            string currency = DealsMoney.CurrencyFor(country);
            string url = $"https://catalog.gog.com/v1/catalog?limit=8&query=like:{Uri.EscapeDataString(query)}&order=desc:score&productType=in:game,pack&countryCode={country}&locale=en-US&currencyCode={currency}";
            var root = Json.ParseObject(await DealsHttp.Client.GetStringAsync(url));
            var words = query.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(DealsText.Key).Where(word => word.Length > 0).ToList();
            var found = new List<Found>();
            foreach (Dictionary<string, object> product in (SteamStore.Path(root, "products") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
            {
                string titleKey = DealsText.Key(product.GetString("title"));
                if (!words.All(titleKey.Contains)) continue;
                long? final = DealsMoney.CentsFromAmount(SteamStore.Path(product, "price", "finalMoney", "amount"));
                long? regular = DealsMoney.CentsFromAmount(SteamStore.Path(product, "price", "baseMoney", "amount")) ?? final;
                string image = product.GetString("coverHorizontal");
                found.Add(new Found
                {
                    Title = product.GetString("title"),
                    Image = image,
                    Price = new StorePrice
                    {
                        Store = "gog",
                        StoreName = "GOG",
                        Cents = final,
                        RegularCents = regular,
                        Currency = SteamStore.Path(product, "price", "finalMoney", "currency") as string,
                        PriceText = SteamStore.Path(product, "price", "final") as string,
                        RegularText = SteamStore.Path(product, "price", "base") as string,
                        Discount = regular > 0 && final < regular ? (int)Math.Round(100.0 - final.Value * 100.0 / regular.Value) : 0,
                        Free = final == 0,
                        Url = "https://www.gog.com/en/game/" + product.GetString("slug"),
                    },
                });
            }
            return found;
        }
    }
}
