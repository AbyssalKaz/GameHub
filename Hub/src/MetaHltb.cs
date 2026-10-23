using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class MetaHltbClient
    {
        private const string Site = "https://howlongtobeat.com";
        private const string FallbackSearchPath = "/api/search/site";
        private static readonly Regex ScriptPath = new Regex("/_next/static/[^\"'\\s]+?\\.js");
        private static readonly Regex InitCall = new Regex("fetch\\(\\s*[`\"'](/api/[A-Za-z0-9/_-]+?)/init\\?");
        private static readonly Regex PostCall = new Regex("fetch\\(\\s*[`\"'](/api/[A-Za-z0-9/_-]+?)[`\"']\\s*,\\s*\\{\\s*method\\s*:\\s*[\"']POST[\"']");

        private readonly HttpClient http;
        private string searchPath;
        private bool usesInit = true;
        private Dictionary<string, object> session;
        private DateTime sessionAt;
        private DateTime discoveredAt;

        public MetaHltbClient()
        {
            var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseCookies = true, CookieContainer = new CookieContainer() };
            http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.Referrer = new Uri(Site + "/");
        }

        public async Task<MetaHltb> SearchAsync(string name, CancellationToken token)
        {
            string key = ArtService.NameKey(name);
            if (key.Length == 0) return null;

            if (searchPath == null || DateTime.UtcNow - discoveredAt > TimeSpan.FromHours(12)) await DiscoverAsync(token);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (usesInit && (session == null || DateTime.UtcNow - sessionAt > TimeSpan.FromMinutes(15))) await StartSessionAsync(token);

                using var request = new HttpRequestMessage(HttpMethod.Post, Site + searchPath);
                request.Headers.TryAddWithoutValidation("Origin", Site);
                request.Headers.TryAddWithoutValidation("Accept", "*/*");
                string sessionToken = session?.GetString("token");
                if (sessionToken != null) request.Headers.TryAddWithoutValidation("x-auth-token", sessionToken);
                request.Content = new StringContent(SearchBody(MetaText.SearchName(name)), Encoding.UTF8, "application/json");

                using HttpResponseMessage response = await http.SendAsync(request, token);
                if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    session = null;
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.MethodNotAllowed)
                {
                    searchPath = null;
                    session = null;
                    await DiscoverAsync(token);
                    continue;
                }
                if (!response.IsSuccessStatusCode) throw new HttpRequestException("HowLongToBeat returned " + (int)response.StatusCode);

                var result = Json.ParseObject(await response.Content.ReadAsStringAsync());
                return Pick(result?["data"] as object[], key);
            }
            throw new HttpRequestException("HowLongToBeat search is unavailable");
        }

        private static MetaHltb Pick(object[] data, string key)
        {
            if (data == null) return new MetaHltb { CheckedAt = MetaText.Now() };

            foreach (Dictionary<string, object> game in data.OfType<Dictionary<string, object>>())
            {
                string title = game.GetString("game_name");
                string aliases = game.GetString("game_alias") ?? "";
                bool matches = ArtService.NameKey(title) == key || aliases.Split(',').Any(alias => ArtService.NameKey(alias) == key);
                if (!matches) continue;

                double? Hours(string field)
                {
                    int? seconds = MetaText.Int(game.TryGetValue(field, out object value) ? value : null);
                    if (!seconds.HasValue || seconds.Value <= 0) return null;
                    return Math.Max(0.5, Math.Round(seconds.Value / 3600.0 * 2, MidpointRounding.AwayFromZero) / 2);
                }

                return new MetaHltb
                {
                    Main = Hours("comp_main"),
                    Extra = Hours("comp_plus"),
                    Complete = Hours("comp_100"),
                    Name = title,
                    GameId = Convert.ToString(game.TryGetValue("game_id", out object id) ? id : null),
                    CheckedAt = MetaText.Now(),
                };
            }
            return new MetaHltb { CheckedAt = MetaText.Now() };
        }

        private async Task DiscoverAsync(CancellationToken token)
        {
            discoveredAt = DateTime.UtcNow;
            searchPath = FallbackSearchPath;
            usesInit = true;

            try
            {
                string home = await http.GetStringAsync(Site + "/");
                var scripts = ScriptPath.Matches(home).Cast<Match>().Select(match => match.Value).Distinct().Take(24).ToList();
                foreach (string script in scripts)
                {
                    token.ThrowIfCancellationRequested();
                    string code;
                    try
                    {
                        code = await http.GetStringAsync(Site + script);
                    }
                    catch (HttpRequestException)
                    {
                        continue;
                    }
                    if (!code.Contains("searchTerms")) continue;

                    Match init = InitCall.Match(code);
                    if (init.Success)
                    {
                        searchPath = init.Groups[1].Value;
                        usesInit = true;
                        return;
                    }

                    Match post = PostCall.Matches(code).Cast<Match>().FirstOrDefault(match => match.Groups[1].Value.Contains("search") || match.Groups[1].Value.Contains("find") || match.Groups[1].Value.Contains("seek"));
                    if (post != null)
                    {
                        searchPath = post.Groups[1].Value;
                        usesInit = false;
                        return;
                    }
                }
            }
            catch (HttpRequestException)
            {
            }
        }

        private async Task StartSessionAsync(CancellationToken token)
        {
            session = null;
            using HttpResponseMessage response = await http.GetAsync(Site + searchPath + "/init?t=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), token);
            if (!response.IsSuccessStatusCode)
            {
                usesInit = false;
                return;
            }
            session = Json.ParseObject(await response.Content.ReadAsStringAsync());
            sessionAt = DateTime.UtcNow;
        }

        private string SearchBody(string name)
        {
            var body = new Dictionary<string, object>
            {
                ["searchType"] = "games",
                ["searchTerms"] = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries),
                ["searchPage"] = 1,
                ["size"] = 20,
                ["searchOptions"] = new Dictionary<string, object>
                {
                    ["games"] = new Dictionary<string, object>
                    {
                        ["userId"] = 0,
                        ["platform"] = "",
                        ["sortCategory"] = "popular",
                        ["rangeCategory"] = "main",
                        ["rangeTime"] = new Dictionary<string, object> { ["min"] = null, ["max"] = null },
                        ["gameplay"] = new Dictionary<string, object> { ["perspective"] = "", ["flow"] = "", ["genre"] = "", ["difficulty"] = "" },
                        ["rangeYear"] = new Dictionary<string, object> { ["min"] = "", ["max"] = "" },
                        ["modifier"] = "",
                    },
                    ["users"] = new Dictionary<string, object> { ["sortCategory"] = "postcount" },
                    ["lists"] = new Dictionary<string, object> { ["sortCategory"] = "follows" },
                    ["filter"] = "",
                    ["sort"] = 0,
                    ["randomizer"] = 0,
                },
                ["useCache"] = true,
            };

            string hpKey = session?.GetString("hpKey");
            string hpValue = session?.GetString("hpVal");
            if (!string.IsNullOrEmpty(hpKey) && hpValue != null) body[hpKey] = hpValue;
            return Json.Serialize(body);
        }
    }
}
