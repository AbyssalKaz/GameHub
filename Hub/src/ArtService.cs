using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class RemoteArt
    {
        public string Cover { get; set; }
        public string Hero { get; set; }
        public string Logo { get; set; }
        public string Header { get; set; }
        public string Icon { get; set; }
        public string CheckedAt { get; set; }
    }

    internal sealed class NameLookup
    {
        public string AppId { get; set; }
        public string CheckedAt { get; set; }
    }

    internal sealed class ArtIndex
    {
        public Dictionary<string, RemoteArt> Items { get; set; } = new Dictionary<string, RemoteArt>();
        public Dictionary<string, NameLookup> Lookups { get; set; } = new Dictionary<string, NameLookup>();
        public Dictionary<string, Dictionary<string, string>> Overrides { get; set; } = new Dictionary<string, Dictionary<string, string>>();
    }

    internal sealed class ArtService
    {
        private const string SteamAssetBase = "https://shared.akamai.steamstatic.com/store_item_assets/";
        private const string SteamCommunityIconBase = "https://shared.akamai.steamstatic.com/steamcommunity/public/images/apps/";
        private static readonly TimeSpan RetryMissingAfter = TimeSpan.FromDays(7);
        private static readonly Regex SteamIconFile = new Regex("^[0-9a-f]{40}\\.jpg$", RegexOptions.IgnoreCase);

        private readonly object sync = new object();
        private readonly ArtIndex index;
        private readonly HttpClient http;
        private readonly SemaphoreSlim downloadSlots = new SemaphoreSlim(4);

        public static ArtService Current { get; private set; }

        public Func<GameEntry, string> AppIdResolver { get; set; }

        public ArtService()
        {
            Current = this;
            index = Json.ReadFile<ArtIndex>(HubPaths.ArtIndexFile) ?? new ArtIndex();
            index.Items ??= new Dictionary<string, RemoteArt>();
            index.Lookups ??= new Dictionary<string, NameLookup>();
            index.Overrides ??= new Dictionary<string, Dictionary<string, string>>();

            http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GameHub/1.0");
        }

        public static string NameKey(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return Regex.Replace(name.ToLowerInvariant(), "[^\\p{L}\\p{Nd}]", "");
        }

        private string ResolveAppId(GameEntry game)
        {
            if (!string.IsNullOrEmpty(game.SteamAppId)) return game.SteamAppId;
            string resolved = AppIdResolver?.Invoke(game);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
            lock (sync)
            {
                return index.Lookups.TryGetValue(NameKey(game.Name), out NameLookup lookup) && !string.IsNullOrEmpty(lookup.AppId) ? lookup.AppId : null;
            }
        }

        private static string EpicAppName(GameEntry game)
        {
            return game.Id != null && game.Id.StartsWith("epic:") ? game.Id.Substring(5) : null;
        }

        public void ApplyLocal(GameEntry game)
        {
            string appId = ResolveAppId(game);
            string cover = null, hero = null, logo = null, header = null, icon = null;

            if (appId != null)
            {
                FindSteamCacheArt(appId, ref cover, ref hero, ref logo, ref header, ref icon);
                FindHydraArt(appId, ref hero, ref logo, ref header);
                ApplyRemote("steam:" + appId, ref cover, ref hero, ref logo, ref header, ref icon);
            }

            string epicName = EpicAppName(game);
            if (epicName != null) ApplyRemote("epic:" + epicName, ref cover, ref hero, ref logo, ref header, ref icon);

            Dictionary<string, string> overrides;
            lock (sync) index.Overrides.TryGetValue(game.Id ?? "", out overrides);
            string Override(string kind) => overrides != null && overrides.TryGetValue(kind, out string relative) && File.Exists(Path.Combine(HubPaths.ArtCacheDir, relative))
                ? "https://cache.hub/art/" + EncodePath(relative)
                : null;

            game.CoverUrl = Override("cover") ?? cover;
            game.HeroUrl = Override("hero") ?? hero ?? header;
            game.LogoUrl = Override("logo") ?? logo;
            game.HeaderUrl = header;
            if (game.Source == "Steam" && icon != null) game.IconUrl = icon;

            string iconOverride = Override("icon");
            if (iconOverride != null)
            {
                game.IconUrl = iconOverride;
            }
            else if (game.IconUrl != null && game.IconUrl.StartsWith("https://cache.hub/art/custom/", StringComparison.OrdinalIgnoreCase))
            {
                game.IconUrl = (game.Source == "Steam" ? icon : null) ?? IconService.GetUrl(game.Icon, 128) ?? IconService.GetUrl(game.Exe, 128) ?? icon;
            }
        }

        public static bool NeedsArt(GameEntry game) => game.CoverUrl == null || game.HeroUrl == null || game.LogoUrl == null;

        public async Task<string> SetOverrideFromUrlAsync(GameEntry game, string kind, string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri address) || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp))
            {
                return "That isn't a web image address.";
            }

            using HttpResponseMessage response = await http.GetAsync(address);
            if (!response.IsSuccessStatusCode) return $"The image couldn't be downloaded ({(int)response.StatusCode}).";

            string mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
            string extension = mediaType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => null,
            };
            if (extension == null) return "That link doesn't point to a JPG, PNG, WebP or GIF image.";

            byte[] data = await response.Content.ReadAsByteArrayAsync();
            if (data.Length > 25 * 1024 * 1024) return "That image is larger than 25 MB.";

            SaveOverride(game, kind, data, Hash(url), extension);
            return null;
        }

        public string SetOverrideFromFile(GameEntry game, string kind, string file)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension == ".jpeg") extension = ".jpg";
            if (!new[] { ".jpg", ".png", ".webp", ".gif", ".bmp" }.Contains(extension)) return "Pick a JPG, PNG, WebP, GIF or BMP image.";

            var info = new FileInfo(file);
            if (!info.Exists) return "That file no longer exists.";
            if (info.Length > 25 * 1024 * 1024) return "That image is larger than 25 MB.";

            SaveOverride(game, kind, File.ReadAllBytes(file), Hash(file + info.LastWriteTimeUtc.Ticks), extension);
            return null;
        }

        public void ResetOverride(GameEntry game, string kind)
        {
            lock (sync)
            {
                if (!index.Overrides.TryGetValue(game.Id, out var overrides)) return;
                if (kind == null) overrides.Clear();
                else overrides.Remove(kind);
                if (overrides.Count == 0) index.Overrides.Remove(game.Id);
                Json.WriteFile(HubPaths.ArtIndexFile, index);
            }
        }

        public List<string> OverriddenKinds(GameEntry game)
        {
            lock (sync) return index.Overrides.TryGetValue(game.Id ?? "", out var overrides) ? overrides.Keys.ToList() : new List<string>();
        }

        private void SaveOverride(GameEntry game, string kind, byte[] data, string name, string extension)
        {
            string relative = "custom\\" + Hash(game.Id) + "\\" + kind + "-" + name + extension;
            string target = Path.Combine(HubPaths.ArtCacheDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, data);

            lock (sync)
            {
                if (!index.Overrides.TryGetValue(game.Id, out var overrides))
                {
                    overrides = new Dictionary<string, string>();
                    index.Overrides[game.Id] = overrides;
                }
                overrides[kind] = relative;
                Json.WriteFile(HubPaths.ArtIndexFile, index);
            }
        }

        private static string Hash(string value)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
            return BitConverter.ToString(bytes, 0, 8).Replace("-", "").ToLowerInvariant();
        }

        private static void FindSteamCacheArt(string appId, ref string cover, ref string hero, ref string logo, ref string header, ref string icon)
        {
            string cacheRoot = HubPaths.SteamLibraryCacheDir;
            if (cacheRoot == null) return;
            string folder = Path.Combine(cacheRoot, appId);
            if (!Directory.Exists(folder)) return;

            var files = new List<string>(Directory.GetFiles(folder));
            foreach (string sub in Directory.GetDirectories(folder)) files.AddRange(Directory.GetFiles(sub));

            string Pick(params string[] names)
            {
                foreach (string name in names)
                {
                    string match = files.FirstOrDefault(file => string.Equals(Path.GetFileName(file), name, StringComparison.OrdinalIgnoreCase));
                    if (match != null) return "https://steamart.hub/" + EncodePath(match.Substring(cacheRoot.Length + 1));
                }
                return null;
            }

            cover ??= Pick("library_600x900_2x.jpg", "library_600x900.jpg", "library_capsule.jpg");
            hero ??= Pick("library_hero.jpg");
            logo ??= Pick("logo.png", "logo_2x.png");
            header ??= Pick("header.jpg");

            string iconFile = files.FirstOrDefault(file => Path.GetDirectoryName(file) == folder && SteamIconFile.IsMatch(Path.GetFileName(file)));
            if (iconFile != null) icon ??= "https://steamart.hub/" + EncodePath(iconFile.Substring(cacheRoot.Length + 1));
        }

        private static void FindHydraArt(string appId, ref string hero, ref string logo, ref string header)
        {
            string folder = Path.Combine(HubPaths.HydraAssetsDir, "steam-" + appId);
            if (!Directory.Exists(folder)) return;

            string Pick(string name)
            {
                string file = Path.Combine(folder, name);
                return File.Exists(file) ? "https://hydraart.hub/" + EncodePath("steam-" + appId + "\\" + name) : null;
            }

            hero ??= Pick("hero.jpg");
            logo ??= Pick("logo.png");
            header ??= Pick("library.jpg");
        }

        private void ApplyRemote(string key, ref string cover, ref string hero, ref string logo, ref string header, ref string icon)
        {
            RemoteArt art;
            lock (sync)
            {
                if (!index.Items.TryGetValue(key, out art)) return;
            }

            string Url(string relative) => relative != null && File.Exists(Path.Combine(HubPaths.ArtCacheDir, relative)) ? "https://cache.hub/art/" + EncodePath(relative) : null;
            cover ??= Url(art.Cover);
            hero ??= Url(art.Hero);
            logo ??= Url(art.Logo);
            header ??= Url(art.Header);
            icon ??= Url(art.Icon);
        }

        private static string EncodePath(string relative)
        {
            return string.Join("/", relative.Split('\\', '/').Select(Uri.EscapeDataString));
        }

        public async Task ResolveRemoteAsync(IReadOnlyList<GameEntry> games, Action<GameEntry> updated)
        {
            try
            {
                await LookupSteamIdsByNameAsync(games);
                await DownloadEpicArtAsync(games, updated);
                await DownloadSteamArtAsync(games, updated);
            }
            catch
            {
            }
            finally
            {
                lock (sync) Json.WriteFile(HubPaths.ArtIndexFile, index);
            }
        }

        private bool NeedsRemote(string key)
        {
            lock (sync)
            {
                if (!index.Items.TryGetValue(key, out RemoteArt art)) return true;
                bool Has(string relative) => relative != null && File.Exists(Path.Combine(HubPaths.ArtCacheDir, relative));
                if (Has(art.Cover) && Has(art.Hero) && Has(art.Logo)) return false;
                return art.CheckedAt == null || DateTime.UtcNow - DateTime.Parse(art.CheckedAt).ToUniversalTime() > RetryMissingAfter;
            }
        }

        private async Task LookupSteamIdsByNameAsync(IReadOnlyList<GameEntry> games)
        {
            foreach (GameEntry game in games)
            {
                if (!string.IsNullOrEmpty(game.SteamAppId) || EpicAppName(game) != null) continue;

                string key = NameKey(game.Name);
                lock (sync)
                {
                    if (index.Lookups.TryGetValue(key, out NameLookup existing) && existing.CheckedAt != null &&
                        (existing.AppId != null || DateTime.UtcNow - DateTime.Parse(existing.CheckedAt).ToUniversalTime() < RetryMissingAfter))
                    {
                        continue;
                    }
                }

                string appId = null;
                try
                {
                    string json = await http.GetStringAsync("https://store.steampowered.com/api/storesearch/?l=english&cc=US&term=" + Uri.EscapeDataString(game.Name));
                    if (Json.ParseObject(json)?["items"] is object[] items)
                    {
                        var results = items.OfType<Dictionary<string, object>>()
                            .Select(item => new { Id = Convert.ToString(item["id"]), Key = NameKey(item.GetString("name")) })
                            .ToList();
                        var match = results.FirstOrDefault(result => result.Key == key)
                            ?? results.FirstOrDefault(result => key.Length >= 8 && result.Key.StartsWith(key))
                            ?? results.FirstOrDefault(result => result.Key.Length >= 8 && key.StartsWith(result.Key));
                        appId = match?.Id;
                    }
                }
                catch
                {
                    continue;
                }

                lock (sync) index.Lookups[key] = new NameLookup { AppId = appId, CheckedAt = DateTime.UtcNow.ToString("o") };
            }
        }

        private async Task DownloadEpicArtAsync(IReadOnlyList<GameEntry> games, Action<GameEntry> updated)
        {
            var epicGames = games.Where(game => EpicAppName(game) != null && game.CoverUrl == null && NeedsRemote("epic:" + EpicAppName(game))).ToList();
            if (epicGames.Count == 0 || !File.Exists(HubPaths.EpicCatalogFile)) return;

            object[] catalog;
            try
            {
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(HubPaths.EpicCatalogFile)));
                catalog = Json.Parse(decoded) as object[] ?? new object[0];
            }
            catch
            {
                return;
            }

            foreach (GameEntry game in epicGames)
            {
                string appName = EpicAppName(game);
                var item = catalog.OfType<Dictionary<string, object>>().FirstOrDefault(entry =>
                    entry.TryGetValue("releaseInfo", out object releases) && releases is object[] list &&
                    list.OfType<Dictionary<string, object>>().Any(release => release.GetString("appId") == appName));

                var images = new Dictionary<string, string>();
                if (item != null && item.TryGetValue("keyImages", out object keyImages) && keyImages is object[] imageList)
                {
                    foreach (Dictionary<string, object> image in imageList.OfType<Dictionary<string, object>>())
                    {
                        string type = image.GetString("type");
                        if (type != null && !images.ContainsKey(type)) images[type] = image.GetString("url");
                    }
                }

                string folder = "epic-" + Regex.Replace(appName, "[^A-Za-z0-9_-]", "_");
                var art = new RemoteArt { CheckedAt = DateTime.UtcNow.ToString("o") };
                art.Cover = await DownloadAsync(Lookup(images, "DieselGameBoxTall", "OfferImageTall"), folder, "cover.jpg");
                art.Hero = await DownloadAsync(Lookup(images, "DieselGameBox", "OfferImageWide", "DieselStoreFrontWide"), folder, "hero.jpg");
                art.Logo = await DownloadAsync(Lookup(images, "DieselGameBoxLogo"), folder, "logo.png");
                art.Header = art.Hero;

                lock (sync) index.Items["epic:" + appName] = art;
                ApplyLocal(game);
                updated(game);
            }
        }

        private static string Lookup(Dictionary<string, string> images, params string[] types)
        {
            foreach (string type in types)
            {
                if (images.TryGetValue(type, out string url) && !string.IsNullOrEmpty(url)) return url;
            }
            return null;
        }

        private async Task DownloadSteamArtAsync(IReadOnlyList<GameEntry> games, Action<GameEntry> updated)
        {
            var byAppId = games
                .Where(game => NeedsArt(game) && ResolveAppId(game) != null)
                .GroupBy(ResolveAppId)
                .Where(group => NeedsRemote("steam:" + group.Key))
                .ToList();

            foreach (var chunk in byAppId.Select((group, position) => new { group, position }).GroupBy(entry => entry.position / 25))
            {
                var groups = chunk.Select(entry => entry.group).ToList();
                Dictionary<string, Dictionary<string, object>> assets = await FetchSteamAssetsAsync(groups.Select(group => group.Key));

                await Task.WhenAll(groups.Select(async group =>
                {
                    string appId = group.Key;
                    assets.TryGetValue(appId, out Dictionary<string, object> asset);
                    string format = asset?.GetString("asset_url_format");

                    string AssetUrl(params string[] keys)
                    {
                        if (asset == null || format == null) return null;
                        foreach (string key in keys)
                        {
                            string file = asset.GetString(key);
                            if (!string.IsNullOrEmpty(file)) return SteamAssetBase + format.Replace("${FILENAME}", file);
                        }
                        return null;
                    }

                    string legacy = SteamAssetBase + "steam/apps/" + appId + "/";
                    string folder = "steam-" + appId;
                    var art = new RemoteArt { CheckedAt = DateTime.UtcNow.ToString("o") };
                    art.Cover = await DownloadAsync(AssetUrl("library_capsule_2x", "library_capsule") ?? legacy + "library_600x900_2x.jpg", folder, "cover.jpg");
                    art.Hero = await DownloadAsync(AssetUrl("library_hero", "library_hero_2x") ?? legacy + "library_hero.jpg", folder, "hero.jpg");
                    art.Logo = await DownloadAsync(AssetUrl("library_logo", "library_logo_2x") ?? legacy + "logo.png", folder, "logo.png");
                    art.Header = await DownloadAsync(AssetUrl("header", "main_capsule") ?? legacy + "header.jpg", folder, "header.jpg");

                    string communityIcon = asset?.GetString("community_icon");
                    if (!string.IsNullOrEmpty(communityIcon)) art.Icon = await DownloadAsync(SteamCommunityIconBase + appId + "/" + communityIcon + ".jpg", folder, "icon.jpg");

                    lock (sync) index.Items["steam:" + appId] = art;
                    foreach (GameEntry game in group)
                    {
                        ApplyLocal(game);
                        updated(game);
                    }
                }));
            }
        }

        private async Task<Dictionary<string, Dictionary<string, object>>> FetchSteamAssetsAsync(IEnumerable<string> appIds)
        {
            var result = new Dictionary<string, Dictionary<string, object>>();
            string ids = string.Join(",", appIds.Select(id => "{\"appid\":" + id + "}"));
            string input = "{\"ids\":[" + ids + "],\"context\":{\"language\":\"english\",\"country_code\":\"US\"},\"data_request\":{\"include_assets\":true}}";

            try
            {
                string json = await http.GetStringAsync("https://api.steampowered.com/IStoreBrowseService/GetItems/v1?input_json=" + Uri.EscapeDataString(input));
                var response = Json.ParseObject(json)?["response"] as Dictionary<string, object>;
                if (response != null && response.TryGetValue("store_items", out object items) && items is object[] list)
                {
                    foreach (Dictionary<string, object> item in list.OfType<Dictionary<string, object>>())
                    {
                        if (item.TryGetValue("assets", out object assets) && assets is Dictionary<string, object> assetMap)
                        {
                            result[Convert.ToString(item["id"])] = assetMap;
                        }
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        private async Task<string> DownloadAsync(string url, string folder, string fileName)
        {
            if (string.IsNullOrEmpty(url)) return null;
            string relative = folder + "\\" + fileName;
            string target = Path.Combine(HubPaths.ArtCacheDir, relative);
            if (File.Exists(target)) return relative;

            await downloadSlots.WaitAsync();
            try
            {
                using HttpResponseMessage response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;
                byte[] data = await response.Content.ReadAsByteArrayAsync();
                if (data.Length < 512) return null;

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.WriteAllBytes(target, data);
                return relative;
            }
            catch
            {
                return null;
            }
            finally
            {
                downloadSlots.Release();
            }
        }
    }
}
