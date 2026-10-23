using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GameHub
{
    internal sealed class HubController
    {
        private readonly MainForm form;
        private readonly WebView2 webView;
        private readonly HubState state;
        private readonly SizeService sizes = new SizeService();
        private readonly ArtService art = new ArtService();
        private readonly CoverFinder finder = new CoverFinder();
        private readonly FreeGamesService freeGames = new FreeGamesService();
        private readonly System.Windows.Forms.Timer reloadDelay = new System.Windows.Forms.Timer { Interval = 2000 };

        private LibraryDocument library = new LibraryDocument();
        private Dictionary<string, string> platformIcons = new Dictionary<string, string>();
        private FileSystemWatcher libraryWatcher;
        private bool syncRunning;
        private bool artRunning;
        private readonly HubContext hub;
        private readonly List<HubModule> modules;

        public HubController(MainForm form, WebView2 webView, HubState state)
        {
            this.form = form;
            this.webView = webView;
            this.state = state;
            form.WindowModeChanged += PostWindowMode;

            hub = new HubContext(this, form, state);
            modules = typeof(HubModule).Assembly.GetTypes()
                .Where(type => typeof(HubModule).IsAssignableFrom(type) && !type.IsAbstract)
                .Select(type => (HubModule)Activator.CreateInstance(type, true))
                .OrderBy(module => module.Order)
                .ToList();
            hub.Modules.AddRange(modules);
            foreach (HubModule module in modules) module.Attach(hub);

            reloadDelay.Tick += async (sender, args) =>
            {
                reloadDelay.Stop();
                if (!syncRunning) await LoadLibraryAsync();
            };
        }

        public async Task InitializeAsync()
        {
            HubPaths.EnsureCacheFolders();
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(null, HubPaths.WebViewDataDir);
            await webView.EnsureCoreWebView2Async(environment);

            CoreWebView2 core = webView.CoreWebView2;
            hub.Web = core;
            hub.Environment = environment;
            hub.Tray = new TrayService(form);
            form.ShuttingDown += () =>
            {
                foreach (HubModule module in modules) Safe(module, module.Stop);
                hub.Tray.Dispose();
            };
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;

            core.SetVirtualHostNameToFolderMapping("app.hub", HubPaths.WebDir, CoreWebView2HostResourceAccessKind.Allow);
            core.SetVirtualHostNameToFolderMapping("cache.hub", HubPaths.PublicCacheDir, CoreWebView2HostResourceAccessKind.Allow);
            string steamCache = HubPaths.SteamLibraryCacheDir;
            if (steamCache != null && Directory.Exists(steamCache)) core.SetVirtualHostNameToFolderMapping("steamart.hub", steamCache, CoreWebView2HostResourceAccessKind.Allow);
            if (Directory.Exists(HubPaths.HydraAssetsDir)) core.SetVirtualHostNameToFolderMapping("hydraart.hub", HubPaths.HydraAssetsDir, CoreWebView2HostResourceAccessKind.Allow);

            core.WebMessageReceived += OnWebMessage;
            core.NewWindowRequested += (sender, args) => args.Handled = true;

            WatchLibraryFile();
            foreach (HubModule module in modules) Safe(module, module.Start);
            core.Navigate("https://app.hub/index.html");
        }

        internal LibraryDocument CurrentLibrary => library;

        internal void PostMessage(object payload) => Post(payload);

        internal void LaunchGame(string id, string origin) => Launch(id, origin);

        internal Task RefreshLibraryAsync() => RefreshAsync();

        internal Task ReloadLibraryAsync() => LoadLibraryAsync();

        internal void ResendLibrary() => SendLibrary();

        private static void Safe(HubModule module, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                Trace.WriteLine($"{module.GetType().Name}: {error}");
                DiagLog.Write(module.GetType().Name, error.ToString());
            }
        }

        private void WatchLibraryFile()
        {
            libraryWatcher = new FileSystemWatcher(HubPaths.SyncDir, Path.GetFileName(HubPaths.LibraryFile))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            FileSystemEventHandler changed = (sender, args) => form.RunOnUi(() =>
            {
                reloadDelay.Stop();
                reloadDelay.Start();
            });
            libraryWatcher.Changed += changed;
            libraryWatcher.Created += changed;
            libraryWatcher.EnableRaisingEvents = true;
        }

        private void Post(object payload)
        {
            string json = Json.Serialize(payload);
            form.RunOnUi(() => webView.CoreWebView2?.PostWebMessageAsJson(json));
        }

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            Dictionary<string, object> message;
            try
            {
                message = Json.ParseObject(args.WebMessageAsJson);
            }
            catch
            {
                return;
            }

            switch (message.GetString("type"))
            {
                case "ready":
                    lastWindowMode = null;
                    PostWindowMode();
                    _ = LoadLibraryAsync();
                    break;
                case "window":
                    HandleWindowAction(message.GetString("action"), message.TryGetValue("hit", out object hit) ? Convert.ToInt32(hit) : 0);
                    break;
                case "launch":
                    Launch(message.GetString("id"), message.GetString("origin") ?? "hub");
                    break;
                case "setGameData":
                {
                    string id = message.GetString("id");
                    var data = hub.UserData.Merge(id, message.TryGetValue("patch", out object patch) ? patch as Dictionary<string, object> : null);
                    Post(new { type = "gameData", id, data });
                    break;
                }
                case "setSetting":
                {
                    string key = message.GetString("key");
                    if (!string.IsNullOrEmpty(key)) hub.Settings.Set(key, message.TryGetValue("value", out object value) ? value : null);
                    Post(new { type = "settings", settings = hub.Settings.Snapshot() });
                    break;
                }
                case "openFolder":
                    OpenFolder(message.GetString("path"));
                    break;
                case "openLauncher":
                    OpenLauncher(message.GetString("id"));
                    break;
                case "refresh":
                    _ = RefreshAsync();
                    break;
                case "rescan":
                    sizes.ForceNextScan = true;
                    _ = ScanSizesAsync(true);
                    break;
                case "findArt":
                    _ = FindArtAsync(message.GetString("id"), message.GetString("query"), message.GetString("requestId"));
                    break;
                case "setArt":
                    _ = SetArtAsync(message.GetString("id"), message.GetString("kind"), message.GetString("url"));
                    break;
                case "setArtFile":
                    SetArtFromFile(message.GetString("id"), message.GetString("kind"));
                    break;
                case "resetArt":
                    ResetArt(message.GetString("id"), message.GetString("kind"));
                    break;
                case "setGridKey":
                    state.SteamGridDbKey = (message.GetString("value") ?? "").Trim();
                    state.Save();
                    Post(new { type = "gridKey", hasKey = state.SteamGridDbKey.Length > 0 });
                    break;
                case "freeGames":
                    _ = SendFreeGamesAsync(message.TryGetValue("force", out object force) && force is bool forced && forced);
                    break;
                case "openStore":
                    OpenStore(message.GetString("url"));
                    break;
                case "uninstallPlan":
                    SendUninstallPlan(message.GetString("id"));
                    break;
                case "uninstall":
                    _ = UninstallAsync(message.GetString("id"), (message.TryGetValue("folders", out object folders) ? folders as object[] : null)?.OfType<string>().ToList() ?? new List<string>());
                    break;
                default:
                {
                    string type = message.GetString("type");
                    foreach (HubModule module in modules)
                    {
                        bool handled = false;
                        Safe(module, () => handled = module.Handle(type, message));
                        if (handled) break;
                    }
                    break;
                }
            }
        }

        private string lastWindowMode;

        private void PostWindowMode()
        {
            string mode = form.IsFullscreen ? "fullscreen" : form.IsMaximized ? "maximized" : "normal";
            if (mode == lastWindowMode) return;
            lastWindowMode = mode;
            Post(new { type = "windowMode", mode });
        }

        private void HandleWindowAction(string action, int hit)
        {
            switch (action)
            {
                case "drag":
                    form.BeginDrag();
                    break;
                case "resize":
                    form.BeginResize(hit);
                    break;
                case "maximize":
                    form.ToggleMaximize();
                    break;
                case "fullscreen":
                    form.ToggleFullscreen();
                    break;
                case "minimize":
                    form.Minimize();
                    break;
                case "close":
                    form.Close();
                    break;
            }
        }

        private void PostArt(GameEntry game)
        {
            Post(new
            {
                type = "art",
                id = game.Id,
                cover = game.CoverUrl,
                hero = game.HeroUrl,
                logo = game.LogoUrl,
                header = game.HeaderUrl,
                icon = game.IconUrl,
                custom = art.OverriddenKinds(game),
            });
        }

        private async Task FindArtAsync(string id, string query, string requestId)
        {
            GameEntry game = Find(id);
            if (game == null) return;

            ArtSearchResult result = await finder.FindAsync(game, query, state.SteamGridDbKey);
            Post(new
            {
                type = "artCandidates",
                requestId,
                id,
                candidates = result.Candidates,
                errors = result.Errors,
                hasGridKey = !string.IsNullOrEmpty(state.SteamGridDbKey),
                custom = art.OverriddenKinds(game),
            });
        }

        private async Task SetArtAsync(string id, string kind, string url)
        {
            GameEntry game = Find(id);
            if (game == null || !IsArtKind(kind)) return;

            string error;
            try
            {
                error = await art.SetOverrideFromUrlAsync(game, kind, url);
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
            FinishArtChange(game, kind, error);
        }

        private void SetArtFromFile(string id, string kind)
        {
            GameEntry game = Find(id);
            if (game == null || !IsArtKind(kind)) return;

            using var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = $"Choose {(kind == "hero" ? "background" : kind)} art for {game.Name}",
                Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp",
            };
            if (dialog.ShowDialog(form) != System.Windows.Forms.DialogResult.OK)
            {
                Post(new { type = "artSet", id, kind, cancelled = true });
                return;
            }

            string error;
            try
            {
                error = art.SetOverrideFromFile(game, kind, dialog.FileName);
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }
            FinishArtChange(game, kind, error);
        }

        private void ResetArt(string id, string kind)
        {
            GameEntry game = Find(id);
            if (game == null) return;
            art.ResetOverride(game, IsArtKind(kind) ? kind : null);
            FinishArtChange(game, kind, null);
        }

        private void FinishArtChange(GameEntry game, string kind, string error)
        {
            if (error == null)
            {
                art.ApplyLocal(game);
                PostArt(game);
            }
            Post(new { type = "artSet", id = game.Id, kind, ok = error == null, error });
        }

        private static bool IsArtKind(string kind) => kind == "cover" || kind == "hero" || kind == "logo";

        private async Task SendFreeGamesAsync(bool force)
        {
            Post(new { type = "freeGamesLoading" });
            FreeGamesSnapshot snapshot = await freeGames.GetAsync(force);
            Post(new { type = "freeGames", snapshot });
        }

        private void OpenStore(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            bool allowed = url.StartsWith("https://store.steampowered.com/", StringComparison.OrdinalIgnoreCase) ||
                           url.StartsWith("https://store.epicgames.com/", StringComparison.OrdinalIgnoreCase) ||
                           url.StartsWith("steam://store/", StringComparison.OrdinalIgnoreCase) ||
                           url.StartsWith("com.epicgames.launcher://store/", StringComparison.OrdinalIgnoreCase);
            if (!allowed) return;

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception error)
            {
                Post(new { type = "toast", kind = "error", message = error.Message });
            }
        }

        private void SendUninstallPlan(string id)
        {
            GameEntry game = Find(id);
            if (game == null) return;

            UninstallPlan plan;
            try
            {
                plan = UninstallService.BuildPlan(game, library.Games);
            }
            catch (Exception error)
            {
                plan = new UninstallPlan { GameId = id, Method = "none", Title = "Can't uninstall from here", Blocked = "Game Hub couldn't work out how to remove this game: " + error.Message };
            }
            Post(new { type = "uninstallPlan", id, plan });
        }

        private async Task UninstallAsync(string id, List<string> requestedFolders)
        {
            GameEntry game = Find(id);
            if (game == null) return;

            UninstallPlan plan = UninstallService.BuildPlan(game, library.Games);
            var folders = plan.Folders
                .Where(folder => folder.Allowed && requestedFolders.Any(requested => string.Equals(requested, folder.Path, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (plan.Method == "none" || (plan.Method == "recycle" && folders.Count == 0))
            {
                Post(new { type = "uninstallResult", id, ok = false, message = plan.Blocked ?? "Nothing was selected." });
                return;
            }

            try
            {
                if (plan.Method == "steam" || plan.Method == "launcher" || plan.Method == "uninstaller")
                {
                    UninstallService.StartExternal(plan);
                }

                var failed = new List<string>();
                IntPtr owner = form.Handle;
                foreach (UninstallFolder folder in folders)
                {
                    if (!await UninstallService.RecycleAsync(folder.Path, owner)) failed.Add(folder.Label);
                }

                string message = plan.Method switch
                {
                    "steam" => $"Steam is uninstalling {game.Name}. Game Hub updates when it's gone.",
                    "launcher" => $"Opened {plan.LauncherName}. Uninstall {game.Name} there.",
                    "uninstaller" => $"{game.Name}'s uninstaller is running. Game Hub updates when it's done.",
                    _ => failed.Count == 0 ? $"Moved {game.Name} to the Recycle Bin." : $"Some folders weren't moved: {string.Join(", ", failed)}.",
                };
                Post(new { type = "uninstallResult", id, ok = failed.Count == 0, message });

                if (folders.Count > 0) _ = RefreshAsync();
                if (plan.Method == "steam" || plan.Method == "uninstaller") _ = RefreshWhenRemovedAsync(game);
            }
            catch (Exception error)
            {
                Post(new { type = "uninstallResult", id, ok = false, message = error.Message });
            }
        }

        private async Task RefreshWhenRemovedAsync(GameEntry game)
        {
            DateTime deadline = DateTime.UtcNow.AddMinutes(20);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(4000);
                bool installGone = !string.IsNullOrEmpty(game.InstallPath) && !Directory.Exists(game.InstallPath);
                bool exeGone = !string.IsNullOrEmpty(game.Exe) && !File.Exists(game.Exe);
                if (installGone || exeGone)
                {
                    await RefreshAsync();
                    return;
                }
            }
        }

        private async Task LoadLibraryAsync()
        {
            LibraryDocument loaded = await Task.Run(() =>
            {
                LibraryDocument document = LibraryService.Load();
                LibraryService.ApplyPlayData(document, state);
                foreach (GameEntry game in document.Games)
                {
                    art.ApplyLocal(game);
                    game.IconUrl ??= IconService.GetUrl(game.Icon, 128) ?? IconService.GetUrl(game.Exe, 128);
                }
                foreach (HubModule module in modules) Safe(module, () => module.Decorate(document));
                platformIcons = IconService.GetPlatformIcons(document.Launchers);
                return document;
            });

            library = loaded;
            SendLibrary();
            foreach (HubModule module in modules) Safe(module, () => module.OnLibraryLoaded(loaded));
            _ = ScanSizesAsync(sizes.IsFullScanDue);
            _ = ResolveArtAsync();
            _ = SendFreeGamesAsync(false);
        }

        private void SendLibrary()
        {
            var extras = new Dictionary<string, object>();
            foreach (HubModule module in modules) Safe(module, () => module.AddLibraryData(extras));
            Post(new
            {
                extras,
                userData = hub.UserData.Snapshot(),
                settings = hub.Settings.Snapshot(),
                type = "library",
                generatedAt = library.GeneratedAt,
                games = library.Games,
                platforms = platformIcons,
                drives = GetDrives(),
                sizes = sizes.Snapshot(),
                scanning = sizes.IsScanning,
                syncing = syncRunning,
                hasGridKey = !string.IsNullOrEmpty(state.SteamGridDbKey),
            });
        }

        private List<object> GetDrives()
        {
            var drives = new List<object>();
            var roots = library.Games
                .SelectMany(game => game.MeasuredPaths)
                .Select(path => Path.GetPathRoot(path)?.ToUpperInvariant())
                .Where(root => !string.IsNullOrEmpty(root))
                .Distinct()
                .OrderBy(root => root);

            foreach (string root in roots)
            {
                try
                {
                    var drive = new DriveInfo(root);
                    if (!drive.IsReady) continue;
                    drives.Add(new { root, label = drive.VolumeLabel, total = drive.TotalSize, free = drive.TotalFreeSpace });
                }
                catch
                {
                }
            }
            return drives;
        }

        private async Task ScanSizesAsync(bool everything)
        {
            if (sizes.IsScanning) return;
            List<string> paths = library.Games.SelectMany(game => game.MeasuredPaths).ToList();

            await sizes.ScanAsync(
                paths,
                everything,
                record => Post(new { type = "size", record }),
                (done, total) => Post(new { type = "scan", running = done < total, done, total }));

            Post(new { type = "scan", running = false, done = 0, total = 0 });
        }

        private async Task ResolveArtAsync()
        {
            if (artRunning) return;
            artRunning = true;
            try
            {
                List<GameEntry> games = library.Games.ToList();
                await art.ResolveRemoteAsync(games, game => Post(new
                {
                    type = "art",
                    id = game.Id,
                    cover = game.CoverUrl,
                    hero = game.HeroUrl,
                    logo = game.LogoUrl,
                    header = game.HeaderUrl,
                    icon = game.IconUrl,
                }));
            }
            finally
            {
                artRunning = false;
            }
        }

        private async Task RefreshAsync()
        {
            if (syncRunning) return;
            syncRunning = true;
            Post(new { type = "sync", running = true });

            bool succeeded = await LibraryService.RunSyncScriptAsync();
            syncRunning = false;
            await LoadLibraryAsync();
            Post(new { type = "sync", running = false, succeeded, log = LibraryService.ReadLastSyncLog() });
        }

        private GameEntry Find(string id) => library.Games.FirstOrDefault(game => game.Id == id);

        private void Launch(string id, string origin = "hub")
        {
            GameEntry game = Find(id);
            if (game == null || !game.CanLaunch) return;

            try
            {
                var start = !string.IsNullOrEmpty(game.Url)
                    ? new ProcessStartInfo(game.Url) { UseShellExecute = true }
                    : new ProcessStartInfo(game.Exe, game.Arguments ?? "") { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(game.Exe) };
                var request = new LaunchRequest { StartInfo = start, Origin = origin };
                foreach (HubModule module in modules) Safe(module, () => module.BeforeLaunch(game, request));
                if (request.Cancel)
                {
                    if (!string.IsNullOrEmpty(request.CancelMessage)) Post(new { type = "toast", kind = "error", message = request.CancelMessage });
                    return;
                }
                Process process = Process.Start(request.StartInfo);
                foreach (HubModule module in modules) Safe(module, () => module.AfterLaunch(game, request, process));

                string now = DateTime.UtcNow.ToString("o");
                state.Launches[game.Id] = now;
                state.Save();
                game.LastPlayed = now;
                Post(new { type = "launched", id = game.Id, lastPlayed = now });
            }
            catch (Exception error)
            {
                Post(new { type = "toast", kind = "error", message = $"Couldn't start {game.Name}: {error.Message}" });
            }
        }

        private void OpenFolder(string path)
        {
            bool known = library.Games.Any(game => game.MeasuredPaths.Any(measured => string.Equals(measured, path, StringComparison.OrdinalIgnoreCase)));
            if (!known || !Directory.Exists(path)) return;
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
        }

        private void OpenLauncher(string id)
        {
            GameEntry game = Find(id);
            if (game == null) return;

            try
            {
                if (!string.IsNullOrEmpty(game.LauncherPage))
                {
                    Process.Start(new ProcessStartInfo(game.LauncherPage) { UseShellExecute = true });
                }
                else if (game.Source == "Hydra")
                {
                    string hydra = HubPaths.GetProtocolHandlerExe("hydralauncher");
                    if (hydra != null) Process.Start(new ProcessStartInfo(hydra) { UseShellExecute = true });
                }
            }
            catch (Exception error)
            {
                Post(new { type = "toast", kind = "error", message = error.Message });
            }
        }
    }
}
