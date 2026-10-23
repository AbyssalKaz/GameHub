using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class ArtCandidate
    {
        public string Kind { get; set; }
        public string Url { get; set; }
        public string Thumb { get; set; }
        public string Source { get; set; }
        public string Label { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Title { get; set; }
        public string AppId { get; set; }
    }

    internal sealed class ArtSearchResult
    {
        public List<ArtCandidate> Candidates { get; } = new List<ArtCandidate>();
        public List<string> Errors { get; } = new List<string>();
    }

    internal sealed class CoverFinder
    {
        private const string GridApi = "https://www.steamgriddb.com/api/v2/";
        private const string SteamCommunityIconBase = "https://shared.akamai.steamstatic.com/steamcommunity/public/images/apps/";

        private readonly HttpClient http;
        private object[] epicCatalog;

        public CoverFinder()
        {
            http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GameHub/1.0");
        }

        public async Task<ArtSearchResult> FindAsync(GameEntry game, string query, string gridKey)
        {
            var result = new ArtSearchResult();
            query = string.IsNullOrWhiteSpace(query) ? game.Name : query.Trim();

            Task steam = AddSteamAsync(game, query, result);
            Task grid = string.IsNullOrWhiteSpace(gridKey) ? Task.CompletedTask : AddSteamGridAsync(game, query, gridKey.Trim(), result);
            AddEpic(query, result);
            await Task.WhenAll(steam, grid);

            var seen = new HashSet<string>();
            var unique = result.Candidates.Where(candidate => candidate.Url != null && seen.Add(candidate.Kind + "|" + candidate.Url)).ToList();
            result.Candidates.Clear();
            result.Candidates.AddRange(unique);
            return result;
        }

        private async Task AddSteamAsync(GameEntry game, string query, ArtSearchResult result)
        {
            try
            {
                var apps = new List<(string Id, string Name)>();
                if (!string.IsNullOrEmpty(game.SteamAppId)) apps.Add((game.SteamAppId, game.Name));
                apps.AddRange((await SteamStore.SearchAsync(http, query)).Take(10));
                if (apps.Count == 0) return;

                var items = await SteamStore.GetItemsAsync(http, apps.Select(app => app.Id));
                foreach (var app in apps.GroupBy(app => app.Id).Select(group => group.First()))
                {
                    items.TryGetValue(app.Id, out Dictionary<string, object> item);
                    string label = item?.GetString("name") ?? app.Name;
                    string legacy = SteamStore.AssetBase + "steam/apps/" + app.Id + "/";

                    string cover = SteamStore.AssetUrl(item, "library_capsule_2x", "library_capsule") ?? legacy + "library_600x900_2x.jpg";
                    string coverThumb = SteamStore.AssetUrl(item, "library_capsule") ?? legacy + "library_600x900.jpg";
                    Add(result, "cover", cover, coverThumb, "Steam", label, 600, 900, label, app.Id);

                    string hero = SteamStore.AssetUrl(item, "library_hero_2x", "library_hero") ?? legacy + "library_hero.jpg";
                    Add(result, "hero", hero, SteamStore.AssetUrl(item, "library_hero") ?? hero, "Steam", label, 1920, 620, label, app.Id);

                    string header = SteamStore.AssetUrl(item, "hero_capsule_2x", "hero_capsule", "main_capsule");
                    if (header != null) Add(result, "hero", header, header, "Steam store", label, 0, 0, label, app.Id);

                    string logo = SteamStore.AssetUrl(item, "library_logo_2x", "library_logo") ?? legacy + "logo.png";
                    Add(result, "logo", logo, logo, "Steam", label, 0, 0, label, app.Id);

                    string communityIcon = (SteamStore.Path(item, "assets", "community_icon") as string);
                    if (!string.IsNullOrEmpty(communityIcon))
                    {
                        string icon = SteamCommunityIconBase + app.Id + "/" + communityIcon + ".jpg";
                        Add(result, "icon", icon, icon, "Steam", label, 184, 184, label, app.Id);
                    }
                }
            }
            catch (Exception error)
            {
                lock (result) result.Errors.Add("Steam: " + error.Message);
            }
        }

        private void AddEpic(string query, ArtSearchResult result)
        {
            try
            {
                if (epicCatalog == null)
                {
                    if (!File.Exists(HubPaths.EpicCatalogFile)) return;
                    string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllText(HubPaths.EpicCatalogFile)));
                    epicCatalog = Json.Parse(decoded) as object[] ?? new object[0];
                }

                string key = ArtService.NameKey(query);
                if (key.Length < 2) return;

                var matches = epicCatalog.OfType<Dictionary<string, object>>()
                    .Where(item => ArtService.NameKey(item.GetString("title")).Contains(key))
                    .Take(8);

                foreach (Dictionary<string, object> item in matches)
                {
                    string title = item.GetString("title");
                    foreach (Dictionary<string, object> image in (SteamStore.Path(item, "keyImages") as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
                    {
                        string url = image.GetString("url");
                        string kind = image.GetString("type") switch
                        {
                            "DieselGameBoxTall" => "cover",
                            "OfferImageTall" => "cover",
                            "DieselGameBox" => "hero",
                            "OfferImageWide" => "hero",
                            "DieselGameBoxLogo" => "logo",
                            _ => null,
                        };
                        if (kind == null || string.IsNullOrEmpty(url)) continue;

                        string thumb = url + (url.Contains("?") ? "&" : "?") + (kind == "cover" ? "h=480&w=360&resize=1" : "w=720&resize=1");
                        Add(result, kind, url, thumb, "Epic Games", title, 0, 0, title);
                    }
                }
            }
            catch (Exception error)
            {
                lock (result) result.Errors.Add("Epic Games: " + error.Message);
            }
        }

        private async Task AddSteamGridAsync(GameEntry game, string query, string key, ArtSearchResult result)
        {
            try
            {
                var gameIds = new List<(string Id, string Name)>();
                var search = await GridGetAsync("search/autocomplete/" + Uri.EscapeDataString(query), key);
                foreach (Dictionary<string, object> entry in (search?["data"] as object[] ?? new object[0]).OfType<Dictionary<string, object>>().Take(2))
                {
                    gameIds.Add((Convert.ToString(entry["id"]), entry.GetString("name")));
                }

                foreach (var (id, name) in gameIds)
                {
                    await AddGridImagesAsync("grids/game/" + id + "?dimensions=600x900,342x482,660x930&types=static&nsfw=false&humor=any", "cover", name, key, result, 30);
                    await AddGridImagesAsync("heroes/game/" + id + "?types=static&nsfw=false", "hero", name, key, result, 15);
                    await AddGridImagesAsync("logos/game/" + id + "?types=static&nsfw=false", "logo", name, key, result, 15);
                    await AddGridImagesAsync("icons/game/" + id + "?types=static&nsfw=false", "icon", name, key, result, 15);
                }
            }
            catch (HttpRequestException error) when (error.Message.Contains("401"))
            {
                lock (result) result.Errors.Add("SteamGridDB rejected the API key.");
            }
            catch (Exception error)
            {
                lock (result) result.Errors.Add("SteamGridDB: " + error.Message);
            }
        }

        private async Task AddGridImagesAsync(string path, string kind, string name, string key, ArtSearchResult result, int limit)
        {
            var response = await GridGetAsync(path, key);
            foreach (Dictionary<string, object> image in (response?["data"] as object[] ?? new object[0]).OfType<Dictionary<string, object>>().Take(limit))
            {
                string author = SteamStore.Path(image, "author", "name") as string;
                int width = image.TryGetValue("width", out object w) ? Convert.ToInt32(w) : 0;
                int height = image.TryGetValue("height", out object h) ? Convert.ToInt32(h) : 0;
                Add(result, kind, image.GetString("url"), image.GetString("thumb") ?? image.GetString("url"), "SteamGridDB", author == null ? name : $"{name} · {author}", width, height, name);
            }
        }

        private async Task<Dictionary<string, object>> GridGetAsync(string path, string key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GridApi + path);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
            using HttpResponseMessage response = await http.SendAsync(request);
            if ((int)response.StatusCode == 401) throw new HttpRequestException("401");
            if (!response.IsSuccessStatusCode) return null;
            return Json.ParseObject(await response.Content.ReadAsStringAsync());
        }

        private static void Add(ArtSearchResult result, string kind, string url, string thumb, string source, string label, int width, int height, string title = null, string appId = null)
        {
            if (string.IsNullOrEmpty(url)) return;
            lock (result)
            {
                result.Candidates.Add(new ArtCandidate { Kind = kind, Url = url, Thumb = thumb ?? url, Source = source, Label = label, Width = width, Height = height, Title = title ?? label, AppId = appId });
            }
        }
    }
}
