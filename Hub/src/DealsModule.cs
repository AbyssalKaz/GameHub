using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class DealsAlertLog
    {
        public List<string> Free { get; set; } = new List<string>();
        public List<string> Sales { get; set; } = new List<string>();
    }

    internal sealed class DealsModule : HubModule
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(3);
        private static readonly string AlertsFile = Path.Combine(HubPaths.CacheDir, "deals-alerts.json");

        private readonly FreeGamesService freeGames = new FreeGamesService();
        private readonly DealsWishlist wishlist = new DealsWishlist();
        private readonly DealsSearch search = new DealsSearch();
        private readonly object alertGate = new object();
        private Timer timer;

        public override void Start()
        {
            FreeGamesService.Updated += OnFreeGamesUpdated;
            Hub.Settings.Changed += OnSettingChanged;
            timer = new Timer(_ => _ = BackgroundCheckAsync(), null, TimeSpan.FromMinutes(2), CheckInterval);
        }

        public override void Stop()
        {
            FreeGamesService.Updated -= OnFreeGamesUpdated;
            timer?.Dispose();
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "dealsWishlist":
                    _ = SendWishlistAsync(message.TryGetValue("force", out object force) && force is bool forced && forced);
                    return true;
                case "dealsSearch":
                    _ = SendSearchAsync(message.GetString("query"), message.GetString("requestId"));
                    return true;
                case "dealsOpen":
                    OpenExternal(message.GetString("url"));
                    return true;
                default:
                    return false;
            }
        }

        private void OnSettingChanged(string key, object value)
        {
            if (key == "deals.steamId" || key == "deals.itadKey") _ = SendWishlistAsync(false);
        }

        private async Task BackgroundCheckAsync()
        {
            try
            {
                await freeGames.GetAsync(false);
            }
            catch
            {
            }

            if (!Hub.Settings.GetBool("deals.notifySales", true)) return;
            try
            {
                string steamId = DealsWishlist.ResolveSteamId(Hub.Settings.GetString("deals.steamId"));
                if (steamId == null) return;
                WishlistSnapshot snapshot = await wishlist.GetAsync(steamId, Hub.Settings.GetString("deals.itadKey"), false);
                Hub.Post(new { type = "dealsWishlist", data = WishlistPayload(snapshot) });
                CheckSales(snapshot);
            }
            catch
            {
            }
        }

        private void OnFreeGamesUpdated(FreeGamesSnapshot snapshot)
        {
            Hub.Post(new { type = "freeGames", snapshot });
            if (!Hub.Settings.GetBool("deals.notifyFree", true)) return;

            bool includeItch = Hub.Settings.GetBool("deals.notifyItch", false);
            var claimed = Hub.Settings.Get("deals.claimed") as Dictionary<string, object> ?? new Dictionary<string, object>();
            var current = snapshot.Offers.Where(offer => offer.Section == "now" && (includeItch || offer.Store != "itch")).ToList();
            List<FreeOffer> fresh;

            lock (alertGate)
            {
                DealsAlertLog log = LoadAlerts();
                fresh = current.Where(offer => !log.Free.Contains(offer.Key) && !claimed.ContainsKey(offer.Key)).ToList();
                if (fresh.Count == 0) return;

                log.Free.AddRange(fresh.Select(offer => offer.Key));
                if (log.Free.Count > 400) log.Free.RemoveRange(0, log.Free.Count - 400);
                SaveAlerts(log);
            }

            string title = fresh.Count == 1 ? "Free to keep: " + fresh[0].Title : fresh.Count + " new games are free to keep";
            string text = fresh.Count == 1
                ? StoreName(fresh[0].Store) + (EndText(fresh[0].EndsAt) is string ends ? " · " + ends : "")
                : string.Join(", ", fresh.Take(4).Select(offer => offer.Title)) + (fresh.Count > 4 ? "…" : "");
            Notify(title, text, "free");
        }

        private void CheckSales(WishlistSnapshot snapshot)
        {
            if (snapshot?.Items == null || !Hub.Settings.GetBool("deals.notifySales", true)) return;

            string rule = Hub.Settings.GetString("deals.saleRule", "any");
            var targets = Hub.Settings.Get("deals.targets") as Dictionary<string, object> ?? new Dictionary<string, object>();
            var matches = new List<WishItem>();

            foreach (WishItem item in snapshot.Items)
            {
                if (!(item.PriceCents is long price)) continue;
                bool hit;
                if (targets.TryGetValue(item.AppId, out object rawTarget) && DealsMoney.Cents(rawTarget) is long target && target > 0)
                {
                    hit = price <= target;
                }
                else
                {
                    long? low = item.Low?.Cents ?? item.SeenLowCents;
                    hit = item.Discount > 0 && rule switch
                    {
                        "lowest" => low == null || price <= low,
                        "nearLow" => low == null || price <= Math.Ceiling(low.Value * 1.1),
                        _ => true,
                    };
                }
                if (hit) matches.Add(item);
            }

            List<WishItem> fresh;
            lock (alertGate)
            {
                DealsAlertLog log = LoadAlerts();
                var keys = matches.ToDictionary(item => item.AppId + ":" + item.PriceCents.Value.ToString(CultureInfo.InvariantCulture), item => item);
                fresh = keys.Where(pair => !log.Sales.Contains(pair.Key)).Select(pair => pair.Value).ToList();
                log.Sales = log.Sales.Where(key => keys.ContainsKey(key)).Concat(keys.Keys.Where(key => !log.Sales.Contains(key))).ToList();
                SaveAlerts(log);
            }
            if (fresh.Count == 0) return;

            fresh = fresh.OrderByDescending(item => item.Discount).ToList();
            string title = fresh.Count == 1
                ? fresh[0].Name + (fresh[0].Discount > 0 ? $" is {fresh[0].Discount}% off" : " hit your target price")
                : fresh.Count + " wishlisted games are on sale";
            string text = fresh.Count == 1
                ? (fresh[0].PriceText ?? "") + (fresh[0].IsLowest ? " · lowest price ever" : "")
                : string.Join(", ", fresh.Take(4).Select(item => item.Name + (item.Discount > 0 ? $" −{item.Discount}%" : ""))) + (fresh.Count > 4 ? "…" : "");
            Notify(title, text, "deals");
        }

        private void Notify(string title, string text, string view)
        {
            Hub.RunOnUi(() => Hub.Notify(title, text, () =>
            {
                Hub.Form.ShowAndActivate();
                Hub.Post(new { type = "dealsShow", view });
            }));
        }

        private static DealsAlertLog LoadAlerts()
        {
            var log = Json.ReadFile<DealsAlertLog>(AlertsFile) ?? new DealsAlertLog();
            log.Free ??= new List<string>();
            log.Sales ??= new List<string>();
            return log;
        }

        private static void SaveAlerts(DealsAlertLog log)
        {
            try
            {
                Json.WriteFile(AlertsFile, log);
            }
            catch
            {
            }
        }

        private static string StoreName(string store) => store switch
        {
            "steam" => "Steam",
            "epic" => "Epic Games Store",
            "gog" => "GOG",
            "ubisoft" => "Ubisoft",
            "itch" => "itch.io",
            _ => store,
        };

        private static string EndText(string iso)
        {
            if (string.IsNullOrEmpty(iso) || !DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime ends)) return null;
            return "until " + ends.ToLocalTime().ToString("ddd d MMM, h:mm tt", CultureInfo.GetCultureInfo("en-AU"));
        }

        private async Task SendWishlistAsync(bool force)
        {
            Hub.Post(new { type = "dealsWishlistLoading" });
            var accounts = DealsWishlist.Accounts();
            string steamId = DealsWishlist.ResolveSteamId(Hub.Settings.GetString("deals.steamId"));
            WishlistSnapshot snapshot;
            try
            {
                snapshot = steamId == null
                    ? new WishlistSnapshot { UpdatedAt = DateTime.UtcNow.ToString("o"), Country = SteamStore.Country, Currency = DealsMoney.CurrencyFor(SteamStore.Country) }
                    : await wishlist.GetAsync(steamId, Hub.Settings.GetString("deals.itadKey"), force);
            }
            catch (Exception error)
            {
                snapshot = new WishlistSnapshot { SteamId = steamId, UpdatedAt = DateTime.UtcNow.ToString("o"), Errors = { error.Message } };
            }

            Hub.Post(new { type = "dealsWishlist", data = WishlistPayload(snapshot), accounts = accounts.Select(account => new { account.SteamId, account.Name, account.Active }) });
            CheckSales(snapshot);
        }

        private static object WishlistPayload(WishlistSnapshot snapshot) => new
        {
            snapshot.UpdatedAt,
            snapshot.SteamId,
            snapshot.Country,
            snapshot.Currency,
            snapshot.Empty,
            snapshot.HasItad,
            snapshot.Items,
            snapshot.Errors,
        };

        private async Task SendSearchAsync(string query, string requestId)
        {
            DealsSearchResult result;
            try
            {
                result = await search.SearchAsync(query, Hub.Settings.GetString("deals.itadKey"));
            }
            catch (Exception error)
            {
                result = new DealsSearchResult { Query = query, Errors = { error.Message } };
            }
            Hub.Post(new { type = "dealsSearch", requestId, result });
        }

        private void OpenExternal(string url)
        {
            if (!DealsLinks.IsAllowed(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception error)
            {
                Hub.Toast(error.Message, "error");
            }
        }
    }
}
