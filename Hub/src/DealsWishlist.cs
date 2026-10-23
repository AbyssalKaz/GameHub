using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace GameHub
{
    internal sealed class SteamAccount
    {
        public string SteamId { get; set; }
        public string Name { get; set; }
        public long LastLogin { get; set; }
        public bool Active { get; set; }
    }

    internal sealed class WishItem
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string Image { get; set; }
        public int Priority { get; set; }
        public string AddedAt { get; set; }
        public string ReleasedAt { get; set; }
        public bool ComingSoon { get; set; }
        public string ReleaseText { get; set; }
        public bool FreeToPlay { get; set; }
        public bool Purchasable { get; set; }
        public long? PriceCents { get; set; }
        public long? OriginalCents { get; set; }
        public int Discount { get; set; }
        public string PriceText { get; set; }
        public string OriginalText { get; set; }
        public string DiscountEndsAt { get; set; }
        public ItadLow Low { get; set; }
        public long? SeenLowCents { get; set; }
        public string SeenLowText { get; set; }
        public bool IsLowest { get; set; }
    }

    internal sealed class WishlistSnapshot
    {
        public string UpdatedAt { get; set; }
        public string SteamId { get; set; }
        public string Country { get; set; }
        public string Currency { get; set; }
        public bool Empty { get; set; }
        public bool HasItad { get; set; }
        public string ItadKeyHash { get; set; }
        public List<WishItem> Items { get; set; } = new List<WishItem>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    internal sealed class SeenLows
    {
        public Dictionary<string, long> Cents { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, string> Texts { get; set; } = new Dictionary<string, string>();
    }

    internal sealed class DealsWishlist
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromHours(3);
        private static readonly TimeSpan MinForcedGap = TimeSpan.FromMinutes(2);
        private static readonly string CacheFile = Path.Combine(HubPaths.CacheDir, "deals-wishlist.json");
        private static readonly string LowsFile = Path.Combine(HubPaths.CacheDir, "deals-seen-lows.json");
        private const long SteamIdBase = 76561197960265728;

        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private WishlistSnapshot cached = Json.ReadFile<WishlistSnapshot>(CacheFile);

        public WishlistSnapshot Cached => cached;

        public static List<SteamAccount> Accounts()
        {
            var accounts = new List<SteamAccount>();
            try
            {
                string root = HubPaths.SteamRoot;
                string file = string.IsNullOrEmpty(root) ? null : Path.Combine(root, "config", "loginusers.vdf");
                if (file == null || !File.Exists(file)) return accounts;

                var users = Vdf.Navigate(Vdf.Parse(File.ReadAllText(file)), "users");
                string active = ActiveSteamId();
                foreach (var pair in users ?? new Dictionary<string, object>())
                {
                    if (!(pair.Value is Dictionary<string, object> user) || !ulong.TryParse(pair.Key, out _)) continue;
                    accounts.Add(new SteamAccount
                    {
                        SteamId = pair.Key,
                        Name = user.GetString("PersonaName") ?? user.GetString("AccountName") ?? pair.Key,
                        LastLogin = long.TryParse(user.GetString("Timestamp"), out long stamp) ? stamp : 0,
                        Active = pair.Key == active || (active == null && user.GetString("MostRecent") == "1"),
                    });
                }
            }
            catch
            {
            }
            return accounts.OrderByDescending(account => account.Active).ThenByDescending(account => account.LastLogin).ToList();
        }

        private static string ActiveSteamId()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
                object value = key?.GetValue("ActiveUser");
                long accountId = value == null ? 0 : Convert.ToInt64(value);
                return accountId > 0 ? (SteamIdBase + accountId).ToString(CultureInfo.InvariantCulture) : null;
            }
            catch
            {
                return null;
            }
        }

        public static string ResolveSteamId(string preferred)
        {
            if (!string.IsNullOrWhiteSpace(preferred) && ulong.TryParse(preferred.Trim(), out _)) return preferred.Trim();
            return Accounts().FirstOrDefault()?.SteamId;
        }

        public async Task<WishlistSnapshot> GetAsync(string steamId, string itadKey, bool force)
        {
            await gate.WaitAsync();
            try
            {
                string keyHash = string.IsNullOrWhiteSpace(itadKey) ? "" : itadKey.Trim().GetHashCode().ToString("x", CultureInfo.InvariantCulture);
                bool sameSource = cached != null && cached.SteamId == steamId && cached.ItadKeyHash == keyHash && cached.Country == SteamStore.Country;
                TimeSpan age = cached?.UpdatedAt == null ? TimeSpan.MaxValue : DateTime.UtcNow - DateTime.Parse(cached.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
                if (sameSource && (age < (force ? MinForcedGap : MaxAge))) return cached;

                var snapshot = await LoadAsync(steamId, itadKey);
                snapshot.ItadKeyHash = keyHash;
                if (snapshot.Items.Count == 0 && snapshot.Errors.Count > 0 && sameSource)
                {
                    cached.Errors = snapshot.Errors;
                    return cached;
                }

                cached = snapshot;
                Json.WriteFile(CacheFile, cached);
                return cached;
            }
            finally
            {
                gate.Release();
            }
        }

        private static async Task<WishlistSnapshot> LoadAsync(string steamId, string itadKey)
        {
            string country = SteamStore.Country;
            var snapshot = new WishlistSnapshot
            {
                SteamId = steamId,
                Country = country,
                Currency = DealsMoney.CurrencyFor(country),
                UpdatedAt = DateTime.UtcNow.ToString("o"),
            };
            if (string.IsNullOrEmpty(steamId)) return snapshot;

            var entries = new List<Dictionary<string, object>>();
            try
            {
                string json = await DealsHttp.Client.GetStringAsync("https://api.steampowered.com/IWishlistService/GetWishlist/v1/?steamid=" + Uri.EscapeDataString(steamId));
                entries = (SteamStore.Path(Json.ParseObject(json), "response", "items") as object[] ?? new object[0]).OfType<Dictionary<string, object>>().ToList();
            }
            catch (Exception error)
            {
                snapshot.Errors.Add("Steam wishlist: " + error.Message);
                return snapshot;
            }

            snapshot.Empty = entries.Count == 0;
            if (entries.Count == 0) return snapshot;

            var ids = entries.Select(entry => Convert.ToString(entry["appid"], CultureInfo.InvariantCulture)).ToList();
            Dictionary<string, Dictionary<string, object>> items;
            try
            {
                items = await SteamStore.GetItemsAsync(DealsHttp.Client, ids, purchase: true, release: true);
            }
            catch (Exception error)
            {
                snapshot.Errors.Add("Steam prices: " + error.Message);
                items = new Dictionary<string, Dictionary<string, object>>();
            }

            var lows = Json.ReadFile<SeenLows>(LowsFile) ?? new SeenLows();
            lows.Cents ??= new Dictionary<string, long>();
            lows.Texts ??= new Dictionary<string, string>();

            foreach (var entry in entries)
            {
                string id = Convert.ToString(entry["appid"], CultureInfo.InvariantCulture);
                items.TryGetValue(id, out Dictionary<string, object> item);
                var wish = BuildItem(id, entry, item);

                if (wish.PriceCents is long price && price > 0)
                {
                    if (!lows.Cents.TryGetValue(id, out long seen) || price < seen)
                    {
                        lows.Cents[id] = price;
                        lows.Texts[id] = wish.PriceText;
                    }
                    wish.SeenLowCents = lows.Cents[id];
                    wish.SeenLowText = lows.Texts.TryGetValue(id, out string text) ? text : null;
                }
                snapshot.Items.Add(wish);
            }

            try
            {
                Json.WriteFile(LowsFile, lows);
            }
            catch
            {
            }

            var itad = new DealsItad(itadKey);
            snapshot.HasItad = itad.Enabled;
            if (itad.Enabled)
            {
                try
                {
                    var map = await itad.LookupSteamAsync(ids);
                    var history = await itad.LowestAsync(map.Values, country);
                    foreach (WishItem wish in snapshot.Items)
                    {
                        if (map.TryGetValue(wish.AppId, out string itadId) && history.TryGetValue(itadId, out ItadLow low))
                        {
                            wish.Low = low;
                            low.Url = "https://isthereanydeal.com/game/id:" + itadId + "/history/";
                        }
                    }
                }
                catch (Exception error)
                {
                    snapshot.Errors.Add("IsThereAnyDeal: " + error.Message);
                }
            }

            foreach (WishItem wish in snapshot.Items)
            {
                wish.IsLowest = wish.PriceCents is long price && price > 0 && wish.Discount > 0 && wish.Low != null && price <= wish.Low.Cents;
            }
            return snapshot;
        }

        private static WishItem BuildItem(string id, Dictionary<string, object> entry, Dictionary<string, object> item)
        {
            var wish = new WishItem
            {
                AppId = id,
                Name = item?.GetString("name") ?? "App " + id,
                Image = SteamStore.AssetUrl(item, "header", "main_capsule") ?? $"{SteamStore.AssetBase}steam/apps/{id}/header.jpg",
                Priority = entry.TryGetValue("priority", out object priority) ? Convert.ToInt32(priority) : 0,
                AddedAt = SteamStore.UnixToIso(entry.TryGetValue("date_added", out object added) ? added : null),
                ReleasedAt = SteamStore.UnixToIso(SteamStore.Path(item, "release", "steam_release_date")),
                ComingSoon = SteamStore.Path(item, "release", "is_coming_soon") is bool soon && soon,
                ReleaseText = SteamStore.Path(item, "release", "custom_release_date_message") as string,
                FreeToPlay = SteamStore.Path(item, "is_free") is bool free && free,
            };

            object option = SteamStore.Path(item, "best_purchase_option");
            if (option != null)
            {
                wish.Purchasable = true;
                wish.PriceCents = DealsMoney.Cents(SteamStore.Path(option, "final_price_in_cents"));
                wish.OriginalCents = DealsMoney.Cents(SteamStore.Path(option, "original_price_in_cents")) ?? wish.PriceCents;
                wish.PriceText = SteamStore.Path(option, "formatted_final_price") as string;
                wish.OriginalText = SteamStore.Path(option, "formatted_original_price") as string ?? wish.PriceText;
                wish.Discount = SteamStore.Path(option, "discount_pct") is object pct ? Convert.ToInt32(pct) : 0;
                if (wish.Discount == 0 && wish.OriginalCents > 0 && wish.PriceCents < wish.OriginalCents)
                {
                    wish.Discount = (int)Math.Round(100.0 - wish.PriceCents.Value * 100.0 / wish.OriginalCents.Value);
                }
                wish.DiscountEndsAt = SteamStore.UnixToIso(SteamStore.Path(option, "active_discounts", 0, "discount_end_date"));
            }
            return wish;
        }
    }
}
