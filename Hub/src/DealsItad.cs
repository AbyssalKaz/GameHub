using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class ItadLow
    {
        public long Cents { get; set; }
        public string Currency { get; set; }
        public string Shop { get; set; }
        public int Cut { get; set; }
        public string Date { get; set; }
        public string Url { get; set; }
    }

    internal sealed class ItadDeal
    {
        public string Shop { get; set; }
        public long Cents { get; set; }
        public long RegularCents { get; set; }
        public string Currency { get; set; }
        public int Cut { get; set; }
        public string Url { get; set; }
    }

    internal sealed class ItadGame
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Image { get; set; }
        public ItadLow Low { get; set; }
        public List<ItadDeal> Deals { get; set; } = new List<ItadDeal>();
    }

    internal sealed class DealsItad
    {
        private const string Api = "https://api.isthereanydeal.com/";
        private const int SteamShopId = 61;

        private readonly string key;

        public DealsItad(string key)
        {
            this.key = key;
        }

        public bool Enabled => !string.IsNullOrWhiteSpace(key);

        public async Task<Dictionary<string, string>> LookupSteamAsync(IEnumerable<string> appIds)
        {
            var result = new Dictionary<string, string>();
            foreach (var chunk in Chunks(appIds.Distinct().Select(id => "app/" + id), 200))
            {
                var response = Json.ParseObject(await PostAsync($"lookup/id/shop/{SteamShopId}/v1", chunk));
                foreach (var pair in response ?? new Dictionary<string, object>())
                {
                    if (pair.Value is string id && !string.IsNullOrEmpty(id)) result[pair.Key.Substring(4)] = id;
                }
            }
            return result;
        }

        public async Task<Dictionary<string, ItadLow>> LowestAsync(IEnumerable<string> itadIds, string country)
        {
            var result = new Dictionary<string, ItadLow>();
            foreach (var chunk in Chunks(itadIds.Distinct(), 200))
            {
                var response = Json.ParseObject(await PostAsync("games/overview/v2?country=" + country, chunk));
                foreach (Dictionary<string, object> entry in (SteamStore.Path(response, "prices") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
                {
                    ItadLow low = ReadLow(SteamStore.Path(entry, "lowest") as Dictionary<string, object>);
                    if (low != null) result[entry.GetString("id")] = low;
                }
            }
            return result;
        }

        public async Task<List<ItadGame>> SearchAsync(string title, string country, int count)
        {
            string json = await GetAsync("games/search/v1?results=" + count + "&title=" + Uri.EscapeDataString(title));
            var games = (Json.Parse(json) as object[] ?? new object[0]).OfType<Dictionary<string, object>>()
                .Where(entry => entry.GetString("type") == null || entry.GetString("type") == "game")
                .Select(entry => new ItadGame
                {
                    Id = entry.GetString("id"),
                    Title = entry.GetString("title"),
                    Image = SteamStore.Path(entry, "assets", "banner400") as string ?? SteamStore.Path(entry, "assets", "banner300") as string,
                })
                .Where(game => !string.IsNullOrEmpty(game.Id))
                .ToList();
            if (games.Count == 0) return games;

            var prices = Json.Parse(await PostAsync("games/prices/v3?country=" + country, games.Select(game => game.Id).ToList())) as object[] ?? new object[0];
            foreach (Dictionary<string, object> entry in prices.OfType<Dictionary<string, object>>())
            {
                ItadGame game = games.FirstOrDefault(candidate => candidate.Id == entry.GetString("id"));
                if (game == null) continue;

                var historyLow = SteamStore.Path(entry, "historyLow", "all") as Dictionary<string, object>;
                if (historyLow != null && DealsMoney.CentsFromAmount(historyLow.TryGetValue("amount", out object amount) ? amount : null) is long lowCents)
                {
                    game.Low = new ItadLow { Cents = lowCents, Currency = historyLow.GetString("currency") };
                }

                foreach (Dictionary<string, object> deal in (SteamStore.Path(entry, "deals") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
                {
                    long? cents = DealsMoney.CentsFromAmount(SteamStore.Path(deal, "price", "amount"));
                    if (cents == null) continue;
                    game.Deals.Add(new ItadDeal
                    {
                        Shop = SteamStore.Path(deal, "shop", "name") as string,
                        Cents = cents.Value,
                        RegularCents = DealsMoney.CentsFromAmount(SteamStore.Path(deal, "regular", "amount")) ?? cents.Value,
                        Currency = SteamStore.Path(deal, "price", "currency") as string,
                        Cut = Convert.ToInt32(SteamStore.Path(deal, "cut") ?? 0),
                        Url = deal.GetString("url"),
                    });
                }
            }
            return games;
        }

        private static ItadLow ReadLow(Dictionary<string, object> lowest)
        {
            if (lowest == null) return null;
            long? cents = DealsMoney.CentsFromAmount(SteamStore.Path(lowest, "price", "amount"));
            if (cents == null) return null;
            return new ItadLow
            {
                Cents = cents.Value,
                Currency = SteamStore.Path(lowest, "price", "currency") as string,
                Shop = SteamStore.Path(lowest, "shop", "name") as string,
                Cut = Convert.ToInt32(SteamStore.Path(lowest, "cut") ?? 0),
                Date = lowest.GetString("timestamp"),
            };
        }

        private async Task<string> GetAsync(string path)
        {
            using var response = await DealsHttp.Client.GetAsync(Api + path + (path.Contains("?") ? "&" : "?") + "key=" + Uri.EscapeDataString(key.Trim()));
            return await ReadAsync(response);
        }

        private async Task<string> PostAsync(string path, List<string> body)
        {
            using var content = new StringContent(Json.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await DealsHttp.Client.PostAsync(Api + path + (path.Contains("?") ? "&" : "?") + "key=" + Uri.EscapeDataString(key.Trim()), content);
            return await ReadAsync(response);
        }

        private static async Task<string> ReadAsync(HttpResponseMessage response)
        {
            string text = await response.Content.ReadAsStringAsync();
            if ((int)response.StatusCode == 401 || (int)response.StatusCode == 403) throw new InvalidOperationException("the API key was rejected");
            if ((int)response.StatusCode == 429) throw new InvalidOperationException("rate limited, try again later");
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("HTTP " + (int)response.StatusCode);
            return text;
        }

        private static IEnumerable<List<string>> Chunks(IEnumerable<string> source, int size)
        {
            var chunk = new List<string>(size);
            foreach (string item in source)
            {
                chunk.Add(item);
                if (chunk.Count == size)
                {
                    yield return chunk;
                    chunk = new List<string>(size);
                }
            }
            if (chunk.Count > 0) yield return chunk;
        }
    }
}
