using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class MetaModule : HubModule
    {
        private const string StoreHost = "store";
        private const string ApiHost = "api";
        private const string HltbHost = "hltb";
        private const string VideoBase = "https://video.akamai.steamstatic.com/store_trailers/";
        private const string AssetBase = "https://shared.akamai.steamstatic.com/store_item_assets/";

        private static readonly TimeSpan StoreSpacing = TimeSpan.FromSeconds(1.6);
        private static readonly TimeSpan ApiSpacing = TimeSpan.FromSeconds(0.6);
        private static readonly TimeSpan HltbSpacing = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan DetailsTtl = TimeSpan.FromDays(14);
        private static readonly TimeSpan ReviewsTtl = TimeSpan.FromDays(3);
        private static readonly TimeSpan NewsTtl = TimeSpan.FromHours(6);
        private static readonly TimeSpan NameMissTtl = TimeSpan.FromDays(14);
        private static readonly TimeSpan HltbTtl = TimeSpan.FromDays(60);
        private static readonly TimeSpan HltbMissTtl = TimeSpan.FromDays(21);
        private static readonly TimeSpan DrawerMaxWait = TimeSpan.FromSeconds(8);

        private readonly object sync = new object();
        private readonly MetaGate gate = new MetaGate();
        private readonly HttpClient http;
        private readonly MetaHltbClient hltb = new MetaHltbClient();
        private readonly HashSet<string> drawerFetches = new HashSet<string>();
        private string CacheFile => Path.Combine(HubPaths.CacheDir, "meta.json");

        private MetaCache cache = new MetaCache();
        private bool dirty;
        private Timer saveTimer;
        private CancellationTokenSource stopping = new CancellationTokenSource();
        private Task worker;
        private bool workerAgain;
        private int networkFailures;
        private MetaArtFiller filler;

        private string status = "idle";
        private int statusDone;
        private int statusTotal;

        public MetaModule()
        {
            http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate }) { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GameHub/1.0");
        }

        public override void Start()
        {
            cache = Json.ReadFile<MetaCache>(CacheFile) ?? new MetaCache();
            cache.Apps ??= new Dictionary<string, MetaRecord>();
            cache.Names ??= new Dictionary<string, MetaNameMatch>();
            cache.Hltb ??= new Dictionary<string, MetaHltb>();
            cache.ArtTried ??= new Dictionary<string, string>();

            saveTimer = new Timer(_ => SaveIfDirty(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            filler = new MetaArtFiller(Hub, AppIdFor);
            if (ArtService.Current != null) ArtService.Current.AppIdResolver = CachedAppId;
        }

        public override void Stop()
        {
            stopping.Cancel();
            filler?.Cancel();
            saveTimer?.Dispose();
            SaveIfDirty();
        }

        public override void AddLibraryData(Dictionary<string, object> extras)
        {
            var all = new Dictionary<string, object>();
            foreach (GameEntry game in Hub.Library.Games.ToList())
            {
                object payload = Payload(game, false);
                if (payload != null) all[game.Id] = payload;
            }
            extras["meta"] = all;
            extras["metaStatus"] = StatusPayload();
        }

        public override void OnLibraryLoaded(LibraryDocument library) => StartWorker();

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            switch (type)
            {
                case "metaRequest":
                    _ = DrawerFetchAsync(message.GetString("id"), message.TryGetValue("force", out object force) && force is bool forced && forced);
                    return true;
                case "metaSearch":
                    _ = SearchAsync(message.GetString("id"), message.GetString("query"), message.GetString("requestId"));
                    return true;
                case "metaRefresh":
                    RefreshAll();
                    return true;
                case "metaStatus":
                    Hub.Post(StatusPayload());
                    return true;
                case "metaFillArt":
                {
                    var kinds = (message.TryGetValue("kinds", out object list) ? list as object[] : null)?.OfType<string>().ToList() ?? new List<string> { "cover" };
                    _ = filler.RunAsync(kinds);
                    return true;
                }
                case "metaFillCancel":
                    filler.Cancel();
                    return true;
                case "iconArt":
                    _ = filler.HandleIconAsync(message);
                    return true;
                case "metaOpenUrl":
                    OpenExternal(message.GetString("url"));
                    return true;
            }
            return false;
        }

        private void OpenExternal(string url)
        {
            if (!Uri.TryCreate(url ?? "", UriKind.Absolute, out Uri address) || (address.Scheme != Uri.UriSchemeHttps && address.Scheme != Uri.UriSchemeHttp)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception error)
            {
                Hub.Toast(error.Message, "error");
            }
        }

        private string CachedAppId(GameEntry game)
        {
            string manual = Hub.UserData.GetString(game.Id, "meta.steamAppId");
            if (manual == "none") return null;
            if (!string.IsNullOrEmpty(manual)) return manual;
            if (!string.IsNullOrEmpty(game.SteamAppId)) return game.SteamAppId;
            lock (sync)
            {
                return cache.Names.TryGetValue(ArtService.NameKey(game.Name), out MetaNameMatch match) ? match.AppId : null;
            }
        }

        private string AppIdFor(GameEntry game) => CachedAppId(game);

        private string MatchKind(GameEntry game)
        {
            string manual = Hub.UserData.GetString(game.Id, "meta.steamAppId");
            if (manual == "none") return "none";
            if (!string.IsNullOrEmpty(manual)) return "manual";
            if (!string.IsNullOrEmpty(game.SteamAppId)) return "steam";
            lock (sync)
            {
                if (!cache.Names.TryGetValue(ArtService.NameKey(game.Name), out MetaNameMatch match)) return "pending";
                return match.AppId != null ? "name" : "none";
            }
        }

        private MetaRecord Record(string appId, bool create = false)
        {
            if (appId == null) return null;
            lock (sync)
            {
                if (cache.Apps.TryGetValue(appId, out MetaRecord record)) return record;
                if (!create) return null;
                record = new MetaRecord { AppId = appId };
                cache.Apps[appId] = record;
                return record;
            }
        }

        private string HltbKey(GameEntry game)
        {
            string appId = CachedAppId(game);
            string name = Record(appId)?.Name;
            return ArtService.NameKey(string.IsNullOrEmpty(name) ? game.Name : name);
        }

        private object Payload(GameEntry game, bool includeNews)
        {
            string appId = CachedAppId(game);
            MetaRecord record = Record(appId);
            MetaHltb times;
            lock (sync) cache.Hltb.TryGetValue(HltbKey(game), out times);
            string match = MatchKind(game);
            if (record == null && times == null && match == "pending") return null;

            lock (sync)
            {
                return new Dictionary<string, object>
                {
                    ["Name"] = record?.Name,
                    ["Description"] = record?.Description,
                    ["Genres"] = record?.Genres?.ToList() ?? new List<string>(),
                    ["Developers"] = record?.Developers?.ToList() ?? new List<string>(),
                    ["Publishers"] = record?.Publishers?.ToList() ?? new List<string>(),
                    ["ReleaseDate"] = record?.ReleaseDate,
                    ["ReleaseText"] = record?.ReleaseText,
                    ["ComingSoon"] = record?.ComingSoon ?? false,
                    ["Screenshots"] = record?.Screenshots?.ToList() ?? new List<string>(),
                    ["ScreenshotThumbs"] = record?.ScreenshotThumbs?.ToList() ?? new List<string>(),
                    ["Movies"] = record?.Movies?.ToList() ?? new List<MetaMovie>(),
                    ["Metacritic"] = record?.Metacritic,
                    ["MetacriticUrl"] = record?.MetacriticUrl,
                    ["ReviewScore"] = record?.ReviewScore,
                    ["ReviewSummary"] = record?.ReviewSummary,
                    ["ReviewCount"] = record?.ReviewCount,
                    ["Hltb"] = times != null && (times.Main ?? times.Extra ?? times.Complete) != null
                        ? new Dictionary<string, object> { ["Main"] = times.Main, ["Extra"] = times.Extra, ["Complete"] = times.Complete, ["Name"] = times.Name, ["Url"] = times.GameId != null ? "https://howlongtobeat.com/game/" + times.GameId : null }
                        : null,
                    ["SteamAppId"] = appId,
                    ["StoreUrl"] = appId != null ? "https://store.steampowered.com/app/" + appId + "/" : null,
                    ["Website"] = record?.Website,
                    ["Match"] = match,
                    ["Complete"] = record?.DetailsAt != null,
                    ["News"] = includeNews ? record?.News?.ToList() : null,
                };
            }
        }

        private void PostMeta(GameEntry game, bool includeNews = false)
        {
            object meta = Payload(game, includeNews);
            if (meta != null) Hub.Post(new { type = "meta", id = game.Id, meta });
        }

        private void PostMetaForApp(string appId)
        {
            foreach (GameEntry game in Hub.Library.Games.ToList().Where(entry => CachedAppId(entry) == appId)) PostMeta(game, drawerFetches.Contains(game.Id));
        }

        private object StatusPayload() => new { type = "metaStatus", phase = status, done = statusDone, total = statusTotal, apps = cache.Apps.Count };

        private void SetStatus(string phase, int done, int total)
        {
            status = phase;
            statusDone = done;
            statusTotal = total;
            Hub.Post(StatusPayload());
        }

        private void MarkDirty()
        {
            lock (sync) dirty = true;
        }

        private void SaveIfDirty()
        {
            lock (sync)
            {
                if (!dirty) return;
                dirty = false;
                try
                {
                    Json.WriteFile(CacheFile, cache);
                }
                catch (IOException)
                {
                    dirty = true;
                }
            }
        }

        private void RefreshAll()
        {
            lock (sync)
            {
                foreach (MetaRecord record in cache.Apps.Values)
                {
                    record.DetailsAt = null;
                    record.ItemsAt = null;
                    record.ReviewsAt = null;
                    record.NewsAt = null;
                }
                foreach (string key in cache.Names.Where(entry => entry.Value.AppId == null).Select(entry => entry.Key).ToList()) cache.Names.Remove(key);
                foreach (string key in cache.Hltb.Where(entry => entry.Value.Main == null && entry.Value.Extra == null && entry.Value.Complete == null).Select(entry => entry.Key).ToList()) cache.Hltb.Remove(key);
                dirty = true;
            }
            networkFailures = 0;
            StartWorker(TimeSpan.Zero);
            Hub.Toast("Refreshing game info in the background");
        }

        private void StartWorker(TimeSpan? delay = null)
        {
            lock (sync)
            {
                if (worker != null && !worker.IsCompleted)
                {
                    workerAgain = true;
                    return;
                }
                worker = Task.Run(() => RunWorkerAsync(delay ?? TimeSpan.FromSeconds(6), stopping.Token));
            }
        }

        private async Task RunWorkerAsync(TimeSpan delay, CancellationToken token)
        {
            try
            {
                await Task.Delay(delay, token);
                do
                {
                    lock (sync) workerAgain = false;
                    await RunPassAsync(token);
                }
                while (workerAgain && !token.IsCancellationRequested);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.WriteLine("MetaModule: " + error);
            }
            finally
            {
                SetStatus("idle", 0, 0);
                SaveIfDirty();
            }
        }

        private async Task RunPassAsync(CancellationToken token)
        {
            List<GameEntry> games = Hub.Library.Games.ToList().Where(game => game.Status != "leftover").ToList();

            var unmatched = games.Where(game => CachedAppId(game) == null && NameLookupDue(game)).ToList();
            var newlyMatched = new List<GameEntry>();
            for (int index = 0; index < unmatched.Count && !Offline; index++)
            {
                SetStatus("matching", index, unmatched.Count);
                if (await LookupAppIdAsync(unmatched[index], token, null) != null) newlyMatched.Add(unmatched[index]);
            }
            if (newlyMatched.Count > 0) _ = ResolveArtAsync(newlyMatched);

            var appIds = games.Select(CachedAppId).Where(id => id != null).Distinct().ToList();
            var itemsDue = appIds.Where(id => !MetaText.Fresh(Record(id)?.ItemsAt, DetailsTtl)).ToList();
            for (int start = 0; start < itemsDue.Count && !Offline; start += 25)
            {
                SetStatus("media", start, itemsDue.Count);
                await FetchItemsAsync(itemsDue.Skip(start).Take(25).ToList(), token);
            }

            var detailsDue = appIds.Where(id => !MetaText.Fresh(Record(id)?.DetailsAt, DetailsTtl)).ToList();
            for (int index = 0; index < detailsDue.Count && !Offline; index++)
            {
                SetStatus("details", index, detailsDue.Count);
                await FetchDetailsAsync(detailsDue[index], token, null);
            }

            await AutoFillArtAsync(games, token);

            var hltbDue = games.Where(game => game.Status == "installed" && HltbDue(game)).ToList();
            for (int index = 0; index < hltbDue.Count && !Offline; index++)
            {
                SetStatus("playtime", index, hltbDue.Count);
                if (!await FetchHltbAsync(hltbDue[index], token, null)) break;
            }
        }

        private async Task AutoFillArtAsync(List<GameEntry> games, CancellationToken token)
        {
            if (Offline || string.IsNullOrWhiteSpace(Hub.State.SteamGridDbKey) || !Hub.Settings.GetBool("meta.autoGridArt", true)) return;
            List<GameEntry> due;
            lock (sync)
            {
                due = games.Where(game => game.Status == "installed" && (game.HeroUrl == null || game.LogoUrl == null) &&
                    !(cache.ArtTried.TryGetValue(game.Id, out string tried) && MetaText.Fresh(tried, DetailsTtl))).ToList();
                foreach (GameEntry game in due) cache.ArtTried[game.Id] = MetaText.Now();
                if (due.Count > 0) dirty = true;
            }
            if (due.Count == 0) return;
            SetStatus("artwork", 0, due.Count);
            token.ThrowIfCancellationRequested();
            await filler.RunAsync(new List<string> { "hero", "logo" }, due, true);
        }

        private bool Offline => networkFailures >= 4;

        private void NoteSuccess() => networkFailures = 0;

        private void NoteFailure(Exception error)
        {
            if (error is HttpRequestException || error is TaskCanceledException || error is WebException) networkFailures++;
        }

        private bool NameLookupDue(GameEntry game)
        {
            if (!string.IsNullOrEmpty(Hub.UserData.GetString(game.Id, "meta.steamAppId"))) return false;
            lock (sync)
            {
                if (!cache.Names.TryGetValue(ArtService.NameKey(game.Name), out MetaNameMatch match)) return true;
                return match.AppId == null && !MetaText.Fresh(match.CheckedAt, NameMissTtl);
            }
        }

        private bool HltbDue(GameEntry game)
        {
            lock (sync)
            {
                if (!cache.Hltb.TryGetValue(HltbKey(game), out MetaHltb times)) return true;
                bool found = (times.Main ?? times.Extra ?? times.Complete) != null;
                return !MetaText.Fresh(times.CheckedAt, found ? HltbTtl : HltbMissTtl);
            }
        }

        private async Task<string> LookupAppIdAsync(GameEntry game, CancellationToken token, TimeSpan? maxWait)
        {
            string key = ArtService.NameKey(game.Name);
            if (key.Length < 2) return null;
            try
            {
                if (!await gate.WaitAsync(StoreHost, StoreSpacing, token, maxWait)) return null;
                var results = await SteamStore.SearchAsync(http, MetaText.SearchName(game.Name));
                NoteSuccess();
                bool gameIsJunk = MetaText.IsJunk(game.Name);
                var match = results.FirstOrDefault(result => ArtService.NameKey(result.Name) == key && (gameIsJunk || !MetaText.IsJunk(result.Name)));
                lock (sync)
                {
                    cache.Names[key] = new MetaNameMatch { AppId = match.Id, Name = match.Name, CheckedAt = MetaText.Now() };
                    dirty = true;
                }
                return match.Id;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                NoteFailure(error);
                return null;
            }
        }

        private async Task FetchItemsAsync(List<string> appIds, CancellationToken token)
        {
            string ids = string.Join(",", appIds.Select(id => "{\"appid\":" + id + "}"));
            string input = "{\"ids\":[" + ids + "],\"context\":{\"language\":\"english\",\"country_code\":\"" + SteamStore.Country + "\"},\"data_request\":{\"include_trailers\":true,\"include_screenshots\":true,\"include_basic_info\":true,\"include_release\":true,\"include_reviews\":true}}";
            Dictionary<string, object> response;
            try
            {
                await gate.WaitAsync(ApiHost, ApiSpacing, token);
                string json = await http.GetStringAsync("https://api.steampowered.com/IStoreBrowseService/GetItems/v1?input_json=" + Uri.EscapeDataString(input));
                response = Json.ParseObject(json)?["response"] as Dictionary<string, object>;
                NoteSuccess();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                NoteFailure(error);
                return;
            }

            var items = (response?.TryGetValue("store_items", out object list) == true ? list as object[] : null)?.OfType<Dictionary<string, object>>().ToList() ?? new List<Dictionary<string, object>>();
            foreach (string appId in appIds)
            {
                Dictionary<string, object> item = items.FirstOrDefault(entry => Convert.ToString(entry.TryGetValue("id", out object id) ? id : null) == appId);
                MetaRecord record = Record(appId, true);
                lock (sync)
                {
                    record.ItemsAt = MetaText.Now();
                    if (item != null && MetaText.Int(item.TryGetValue("success", out object success) ? success : null) == 1) ApplyItem(record, item);
                    dirty = true;
                }
                PostMetaForApp(appId);
            }
        }

        private static void ApplyItem(MetaRecord record, Dictionary<string, object> item)
        {
            record.Name ??= item.GetString("name");
            record.Description ??= MetaText.Plain(SteamStore.Path(item, "basic_info", "short_description") as string);
            record.Developers ??= MetaText.Names(SteamStore.Path(item, "basic_info", "developers"), "name");
            record.Publishers ??= MetaText.Names(SteamStore.Path(item, "basic_info", "publishers"), "name");

            string released = SteamStore.UnixToIso(SteamStore.Path(item, "release", "steam_release_date") ?? SteamStore.Path(item, "release", "original_release_date"));
            if (released != null) record.ReleaseDate = released.Substring(0, 10);
            if (SteamStore.Path(item, "release", "is_coming_soon") is bool soon) record.ComingSoon = soon;

            if (SteamStore.Path(item, "reviews", "summary_filtered") is Dictionary<string, object> reviews)
            {
                record.ReviewScore = MetaText.Int(reviews.TryGetValue("percent_positive", out object percent) ? percent : null);
                record.ReviewSummary = reviews.GetString("review_score_label");
                record.ReviewCount = MetaText.Int(reviews.TryGetValue("review_count", out object count) ? count : null);
                if (record.ReviewCount == 0) record.ReviewScore = null;
            }

            if (record.Screenshots == null || record.Screenshots.Count == 0)
            {
                var shots = (SteamStore.Path(item, "screenshots", "all_ages_screenshots") as object[])?.OfType<Dictionary<string, object>>().Select(shot => shot.GetString("filename")).Where(name => !string.IsNullOrEmpty(name)).ToList();
                if (shots != null && shots.Count > 0)
                {
                    record.Screenshots = shots.Select(name => AssetBase + name.Replace(".jpg", ".1920x1080.jpg")).ToList();
                    record.ScreenshotThumbs = shots.Select(name => AssetBase + name.Replace(".jpg", ".600x338.jpg")).ToList();
                }
            }

            var movies = new List<MetaMovie>();
            foreach (string group in new[] { "highlights", "other_trailers" })
            {
                foreach (Dictionary<string, object> trailer in (SteamStore.Path(item, "trailers", group) as object[] ?? new object[0]).OfType<Dictionary<string, object>>())
                {
                    string format = trailer.GetString("trailer_url_format") ?? "steam/apps/${FILENAME}";
                    string Asset(string file) => string.IsNullOrEmpty(file) ? null : AssetBase + format.Replace("${FILENAME}", file);
                    string Video(string listKey, string type)
                    {
                        var files = (trailer.TryGetValue(listKey, out object value) ? value as object[] : null)?.OfType<Dictionary<string, object>>();
                        string file = files?.FirstOrDefault(entry => entry.GetString("type") == type)?.GetString("filename");
                        return file == null ? null : VideoBase + file;
                    }
                    string Adaptive(string encoding)
                    {
                        var files = (trailer.TryGetValue("adaptive_trailers", out object value) ? value as object[] : null)?.OfType<Dictionary<string, object>>();
                        string path = files?.FirstOrDefault(entry => entry.GetString("encoding") == encoding)?.GetString("cdn_path");
                        return path == null ? null : VideoBase + path;
                    }

                    movies.Add(new MetaMovie
                    {
                        Name = trailer.GetString("trailer_name")?.Trim(),
                        Thumb = Asset(trailer.GetString("screenshot_medium")),
                        Poster = Asset(trailer.GetString("screenshot_full")) ?? Asset(trailer.GetString("screenshot_medium")),
                        Mp4 = Video("trailer_max", "video/mp4") ?? Video("trailer_480p", "video/mp4"),
                        Webm = Video("trailer_max", "video/webm") ?? Video("trailer_480p", "video/webm"),
                        Micro = Video("microtrailer", "video/webm") ?? Video("microtrailer", "video/mp4"),
                        MicroMp4 = Video("microtrailer", "video/mp4"),
                        Dash = Adaptive("dash_h264"),
                        Hls = Adaptive("hls_h264"),
                    });
                }
            }
            if (movies.Count > 0) record.Movies = movies;
        }

        private async Task<bool> FetchDetailsAsync(string appId, CancellationToken token, TimeSpan? maxWait)
        {
            Dictionary<string, object> root;
            try
            {
                if (!await gate.WaitAsync(StoreHost, StoreSpacing, token, maxWait)) return false;
                using HttpResponseMessage response = await http.GetAsync("https://store.steampowered.com/api/appdetails?appids=" + appId + "&cc=" + SteamStore.Country + "&l=english", token);
                if ((int)response.StatusCode == 429 || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    gate.Cooldown(StoreHost, TimeSpan.FromMinutes(5));
                    return false;
                }
                if (!response.IsSuccessStatusCode) return false;
                root = Json.ParseObject(await response.Content.ReadAsStringAsync());
                NoteSuccess();
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                NoteFailure(error);
                return false;
            }

            var entry = root?.TryGetValue(appId, out object value) == true ? value as Dictionary<string, object> : null;
            var data = entry?.TryGetValue("data", out object payload) == true ? payload as Dictionary<string, object> : null;
            MetaRecord record = Record(appId, true);
            lock (sync)
            {
                record.DetailsAt = MetaText.Now();
                record.Missing = data == null;
                if (data != null) ApplyDetails(record, data);
                dirty = true;
            }
            PostMetaForApp(appId);
            return true;
        }

        private static void ApplyDetails(MetaRecord record, Dictionary<string, object> data)
        {
            record.Name = data.GetString("name") ?? record.Name;
            record.Description = MetaText.Plain(data.GetString("short_description")) ?? record.Description;
            record.Genres = MetaText.Names(data.TryGetValue("genres", out object genres) ? genres : null, "description") ?? record.Genres;
            record.Developers = MetaText.Strings(data.TryGetValue("developers", out object developers) ? developers : null) ?? record.Developers;
            record.Publishers = MetaText.Strings(data.TryGetValue("publishers", out object publishers) ? publishers : null) ?? record.Publishers;

            string website = data.GetString("website");
            record.Website = Uri.TryCreate(website ?? "", UriKind.Absolute, out Uri site) && (site.Scheme == Uri.UriSchemeHttps || site.Scheme == Uri.UriSchemeHttp) ? site.ToString() : null;

            if (data.TryGetValue("metacritic", out object metacritic) && metacritic is Dictionary<string, object> critic)
            {
                record.Metacritic = MetaText.Int(critic.TryGetValue("score", out object score) ? score : null);
                record.MetacriticUrl = critic.GetString("url");
            }
            else
            {
                record.Metacritic = null;
                record.MetacriticUrl = null;
            }

            if (data.TryGetValue("release_date", out object release) && release is Dictionary<string, object> releaseInfo)
            {
                record.ReleaseText = releaseInfo.GetString("date");
                record.ComingSoon = releaseInfo.TryGetValue("coming_soon", out object soon) && soon is bool upcoming && upcoming;
                record.ReleaseDate ??= MetaText.ParseReleaseDate(record.ReleaseText);
            }

            var shots = (data.TryGetValue("screenshots", out object screenshots) ? screenshots as object[] : null)?.OfType<Dictionary<string, object>>().ToList();
            if (shots != null && shots.Count > 0)
            {
                record.Screenshots = shots.Select(shot => shot.GetString("path_full")).Where(url => url != null).ToList();
                record.ScreenshotThumbs = shots.Select(shot => shot.GetString("path_thumbnail") ?? shot.GetString("path_full")).Where(url => url != null).ToList();
            }

            if (record.Movies == null || record.Movies.Count == 0)
            {
                var movies = (data.TryGetValue("movies", out object list) ? list as object[] : null)?.OfType<Dictionary<string, object>>()
                    .Select(movie => new MetaMovie
                    {
                        Name = movie.GetString("name"),
                        Thumb = movie.GetString("thumbnail"),
                        Poster = movie.GetString("thumbnail"),
                        Mp4 = SteamStore.Path(movie, "mp4", "max") as string ?? SteamStore.Path(movie, "mp4", "480") as string,
                        Webm = SteamStore.Path(movie, "webm", "max") as string ?? SteamStore.Path(movie, "webm", "480") as string,
                        Dash = movie.GetString("dash_h264"),
                        Hls = movie.GetString("hls_h264"),
                    })
                    .ToList();
                if (movies != null && movies.Count > 0) record.Movies = movies;
            }
        }

        private async Task FetchReviewsAsync(string appId, CancellationToken token, TimeSpan? maxWait)
        {
            try
            {
                if (!await gate.WaitAsync(StoreHost, StoreSpacing, token, maxWait)) return;
                string json = await http.GetStringAsync("https://store.steampowered.com/appreviews/" + appId + "?json=1&language=all&purchase_type=all&num_per_page=0");
                var summary = Json.ParseObject(json)?["query_summary"] as Dictionary<string, object>;
                NoteSuccess();
                if (summary == null) return;

                int total = MetaText.Int(summary.TryGetValue("total_reviews", out object reviews) ? reviews : null) ?? 0;
                int positive = MetaText.Int(summary.TryGetValue("total_positive", out object good) ? good : null) ?? 0;
                MetaRecord record = Record(appId, true);
                lock (sync)
                {
                    record.ReviewsAt = MetaText.Now();
                    if (total > 0)
                    {
                        record.ReviewScore = (int)Math.Round(positive * 100.0 / total);
                        record.ReviewCount = total;
                        record.ReviewSummary = summary.GetString("review_score_desc") ?? record.ReviewSummary;
                    }
                    dirty = true;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                NoteFailure(error);
            }
        }

        private async Task FetchNewsAsync(string appId, CancellationToken token)
        {
            try
            {
                await gate.WaitAsync(ApiHost, ApiSpacing, token);
                string json = await http.GetStringAsync("https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=" + appId + "&count=5&maxlength=400&format=json");
                var items = SteamStore.Path(Json.ParseObject(json), "appnews", "newsitems") as object[];
                NoteSuccess();

                var news = (items ?? new object[0]).OfType<Dictionary<string, object>>().Select(item =>
                {
                    var tags = (item.TryGetValue("tags", out object tagList) ? tagList as object[] : null)?.OfType<string>() ?? Enumerable.Empty<string>();
                    string url = item.GetString("url");
                    return new MetaNewsItem
                    {
                        Title = MetaText.Plain(item.GetString("title")),
                        Url = Uri.TryCreate(url ?? "", UriKind.Absolute, out Uri address) && address.Scheme == Uri.UriSchemeHttps ? url : null,
                        Date = SteamStore.UnixToIso(item.TryGetValue("date", out object date) ? date : null),
                        Author = MetaText.Plain(item.GetString("author")),
                        Feed = MetaText.Plain(item.GetString("feedlabel")),
                        Excerpt = MetaText.Plain(item.GetString("contents")),
                        Patch = tags.Any(tag => tag == "patchnotes"),
                    };
                }).Where(item => item.Title != null).ToList();

                MetaRecord record = Record(appId, true);
                lock (sync)
                {
                    record.News = news;
                    record.NewsAt = MetaText.Now();
                    dirty = true;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                NoteFailure(error);
            }
        }

        private async Task<bool> FetchHltbAsync(GameEntry game, CancellationToken token, TimeSpan? maxWait)
        {
            string appId = CachedAppId(game);
            string name = Record(appId)?.Name;
            if (string.IsNullOrEmpty(name)) name = game.Name;
            string key = ArtService.NameKey(name);
            try
            {
                if (!await gate.WaitAsync(HltbHost, HltbSpacing, token, maxWait)) return false;
                MetaHltb times = await hltb.SearchAsync(name, token);
                if (times == null) return true;
                if (times.Main == null && times.Extra == null && times.Complete == null && ArtService.NameKey(game.Name) != key)
                {
                    await gate.WaitAsync(HltbHost, HltbSpacing, token);
                    MetaHltb retry = await hltb.SearchAsync(game.Name, token);
                    if (retry != null) times = retry;
                }
                lock (sync)
                {
                    cache.Hltb[key] = times;
                    dirty = true;
                }
                PostMeta(game, drawerFetches.Contains(game.Id));
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                gate.Cooldown(HltbHost, TimeSpan.FromMinutes(30));
                return false;
            }
        }

        private async Task DrawerFetchAsync(string id, bool force)
        {
            GameEntry game = Hub.Find(id);
            if (game == null) return;
            CancellationToken token = stopping.Token;

            lock (sync)
            {
                if (!drawerFetches.Add(id)) return;
            }

            try
            {
                PostMeta(game, true);
                if (force)
                {
                    networkFailures = 0;
                    lock (sync)
                    {
                        cache.Names.Remove(ArtService.NameKey(game.Name));
                        MetaRecord stale = Record(CachedAppId(game));
                        if (stale != null) stale.DetailsAt = stale.ItemsAt = stale.ReviewsAt = stale.NewsAt = null;
                    }
                }

                string appId = CachedAppId(game);
                if (appId == null && NameLookupDue(game))
                {
                    appId = await LookupAppIdAsync(game, token, DrawerMaxWait);
                    if (appId != null) _ = ResolveArtAsync(new List<GameEntry> { game });
                    PostMeta(game, true);
                }

                if (appId != null)
                {
                    MetaRecord record = Record(appId);
                    var work = new List<Task>();
                    if (!MetaText.Fresh(record?.ItemsAt, DetailsTtl)) work.Add(FetchItemsAsync(new List<string> { appId }, token));
                    if (!MetaText.Fresh(record?.NewsAt, NewsTtl)) work.Add(FetchNewsAsync(appId, token));
                    await Task.WhenAll(work);

                    if (!MetaText.Fresh(Record(appId)?.DetailsAt, DetailsTtl)) await FetchDetailsAsync(appId, token, DrawerMaxWait);
                    if (!MetaText.Fresh(Record(appId)?.ReviewsAt, ReviewsTtl)) await FetchReviewsAsync(appId, token, DrawerMaxWait);
                }

                if (force && ArtService.Current != null)
                {
                    ArtService.Current.ApplyLocal(game);
                    MetaArtFiller.PostArt(Hub, ArtService.Current)(game);
                    if (appId != null) _ = ResolveArtAsync(new List<GameEntry> { game });
                }

                PostMeta(game, true);
                if (HltbDue(game)) await FetchHltbAsync(game, token, DrawerMaxWait);
                PostMeta(game, true);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                lock (sync) drawerFetches.Remove(id);
                Hub.Post(new { type = "metaLoaded", id });
            }
        }

        private async Task SearchAsync(string id, string query, string requestId)
        {
            GameEntry game = Hub.Find(id);
            if (game == null) return;
            query = string.IsNullOrWhiteSpace(query) ? game.Name : query.Trim();

            var results = new List<object>();
            string error = null;
            try
            {
                if (await gate.WaitAsync(StoreHost, StoreSpacing, stopping.Token, DrawerMaxWait))
                {
                    foreach (var (appId, name) in (await SteamStore.SearchAsync(http, MetaText.SearchName(query))).Take(10))
                    {
                        results.Add(new { AppId = appId, Name = name, Image = AssetBase + "steam/apps/" + appId + "/capsule_231x87.jpg" });
                    }
                }
                else
                {
                    error = "Steam is busy. Try again in a few minutes.";
                }
            }
            catch (Exception exception)
            {
                error = exception is HttpRequestException ? "Couldn't reach Steam." : exception.Message;
            }
            Hub.Post(new { type = "metaSearchResults", id, requestId, results, error });
        }

        private async Task ResolveArtAsync(List<GameEntry> games)
        {
            ArtService art = ArtService.Current;
            if (art == null) return;
            var needing = games.Where(ArtService.NeedsArt).ToList();
            if (needing.Count == 0) return;
            await art.ResolveRemoteAsync(needing, MetaArtFiller.PostArt(Hub, art));
        }
    }
}
