using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GameHub
{
    internal sealed class StoreBrowser : HubModule
    {
        public const int MaxTabs = 8;
        private const int ClosedTabMemory = 10;
        private static readonly Color BrowserBackground = Color.FromArgb(18, 21, 28);
        private static readonly TimeSpan BackgroundSuspendDelay = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan HiddenSuspendDelay = TimeSpan.FromMinutes(3);
        private static readonly double[] ZoomSteps = { 0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0, 4.0, 5.0 };

        private readonly List<StoreTab> tabs = new List<StoreTab>();
        private readonly List<(string Url, string Profile)> closedTabs = new List<(string Url, string Profile)>();
        private readonly System.Windows.Forms.Timer suspendTimer = new System.Windows.Forms.Timer { Interval = 15000 };
        private readonly System.Windows.Forms.Timer zoomSaveDelay = new System.Windows.Forms.Timer { Interval = 800 };
        private Dictionary<string, double> zoomBySite = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private StoreTab active;
        private Rectangle bounds = Rectangle.Empty;
        private bool wantVisible;
        private bool statePending;
        private int nextId = 1;
        private StoreAdBlock adBlock;
        private DownloadManager downloads;
        private StoreHistory history;

        public DownloadManager Downloads => downloads;

        public override void Start()
        {
            adBlock = new StoreAdBlock(Hub.Settings);
            downloads = new DownloadManager(Hub);
            downloads.RetryRequested += url => _ = OpenAsync(url, active?.Profile, false);
            history = new StoreHistory();
            zoomBySite = ReadZoom();

            Hub.Settings.Changed += (key, value) => Hub.RunOnUi(() => OnSettingChanged(key));
            suspendTimer.Tick += (sender, args) => SuspendIdleTabs();
            suspendTimer.Start();
            zoomSaveDelay.Tick += (sender, args) =>
            {
                zoomSaveDelay.Stop();
                SaveZoom();
            };
            _ = RefreshFiltersAsync(false);
        }

        public override void Stop()
        {
            history?.Flush();
            if (zoomSaveDelay.Enabled) SaveZoom();
        }

        public override bool Handle(string type, Dictionary<string, object> message)
        {
            if (type != "browser") return false;
            _ = HandleSafelyAsync(message);
            return true;
        }

        public Task OpenUrlAsync(string url, bool newTab = false) => OpenAsync(url, null, newTab);

        private async Task HandleSafelyAsync(Dictionary<string, object> message)
        {
            try
            {
                await HandleAsync(message);
            }
            catch (Exception error)
            {
                Trace.WriteLine("StoreBrowser: " + error);
            }
        }

        private async Task HandleAsync(Dictionary<string, object> message)
        {
            string action = message.GetString("action");
            Rectangle? requested = ReadBounds(message);
            if (requested.HasValue) bounds = requested.Value;

            switch (action)
            {
                case "show":
                    wantVisible = true;
                    if (tabs.Count == 0 && !string.IsNullOrEmpty(message.GetString("url")))
                    {
                        await OpenAsync(message.GetString("url"), message.GetString("profile"), true);
                    }
                    ApplyLayout();
                    PostState();
                    break;
                case "bounds":
                    ApplyLayout();
                    break;
                case "hide":
                    wantVisible = false;
                    if (active != null) active.LastActive = DateTime.UtcNow;
                    if (ReadBool(message, "snapshot")) await SnapshotAsync();
                    if (!wantVisible) ApplyLayout();
                    break;
                case "state":
                    PostState();
                    break;
                case "open":
                    await OpenAsync(message.GetString("url"), message.GetString("profile"), ReadBool(message, "newTab"), ReadBool(message, "background"));
                    break;
                case "navigate":
                    await OpenAsync(message.GetString("url"), active?.Profile, false);
                    break;
                case "newTab":
                    await NewTabAsync(message.GetString("url"), false);
                    break;
                case "closeTab":
                    CloseTab(FindTab(message));
                    break;
                case "activate":
                    Activate(FindTab(message));
                    break;
                case "cycle":
                    Cycle(ReadInt(message, "delta", 1), false);
                    break;
                case "reopen":
                    await ReopenAsync();
                    break;
                case "back":
                    if (active?.Core?.CanGoBack == true) active.Core.GoBack();
                    break;
                case "forward":
                    if (active?.Core?.CanGoForward == true) active.Core.GoForward();
                    break;
                case "reload":
                    if (active?.Loading == true) active.Core?.Stop();
                    else active?.Core?.Reload();
                    break;
                case "external":
                    OpenExternally(active?.Url);
                    break;
                case "zoom":
                    Zoom(ReadInt(message, "delta", 0));
                    break;
                case "toggleAds":
                    ToggleAds();
                    break;
                case "mute":
                {
                    StoreTab tab = FindTab(message) ?? active;
                    if (tab?.Core != null) tab.Core.IsMuted = !tab.Core.IsMuted;
                    MarkDirty();
                    break;
                }
                case "history":
                    PostHistory();
                    break;
                case "historyRemove":
                    history.Remove(message.GetString("url"));
                    PostHistory();
                    break;
                case "clearHistory":
                    history.Clear();
                    PostHistory();
                    Hub.Toast("Browsing history cleared", "info");
                    break;
                case "download":
                    downloads.Handle(message.GetString("command"), message.GetString("id"));
                    break;
                case "downloadDecision":
                    downloads.Decide(message.GetString("id"), message.GetString("folder"));
                    break;
                case "pickDownloadFolder":
                    PickDownloadFolder();
                    break;
                case "refreshFilters":
                    await RefreshFiltersAsync(true);
                    break;
            }
        }

        private void OnSettingChanged(string key)
        {
            switch (key)
            {
                case "store.filterListUrl":
                    _ = RefreshFiltersAsync(true);
                    break;
                case "store.adAllowSites":
                    adBlock.ReadAllowedSites();
                    MarkDirty();
                    break;
                case "store.blockAds":
                    MarkDirty();
                    break;
                case "store.downloadFolder":
                    downloads.PostList();
                    break;
            }
        }

        private async Task RefreshFiltersAsync(bool force)
        {
            await adBlock.RefreshUserListAsync(force);
            Hub.RunOnUi(MarkDirty);
        }

        private int OpenTabCount => tabs.Count(tab => !tab.Closed);

        private StoreTab FindTab(Dictionary<string, object> message)
        {
            int id = ReadInt(message, "id", 0);
            return tabs.FirstOrDefault(tab => tab.Id == id && !tab.Closed);
        }

        private async Task OpenAsync(string input, string profile, bool newTab, bool background = false)
        {
            string url = NormalizeAddress(input);
            profile = CleanProfile(profile);

            StoreTab target = null;
            if (!newTab)
            {
                if (active != null && active.Profile == profile) target = active;
                else if (profile.Length > 0) target = tabs.LastOrDefault(tab => tab.Profile == profile && !tab.Closed);
            }

            if (target == null)
            {
                if (OpenTabCount >= MaxTabs)
                {
                    Hub.Toast($"You can have up to {MaxTabs} tabs open. Close one to open another.", "info");
                    return;
                }
                target = await CreateTabAsync(profile);
                if (target == null) return;
                if (url != null) target.PendingUri = url;
                if (!background || active == null) Activate(target);
                else MarkDirty();
            }
            else if (target != active)
            {
                Activate(target);
            }

            if (url != null) Navigate(target, url);
        }

        private async Task NewTabAsync(string input, bool focusAddress)
        {
            if (OpenTabCount >= MaxTabs)
            {
                Hub.Toast($"You can have up to {MaxTabs} tabs open. Close one to open another.", "info");
                return;
            }
            StoreTab tab = await CreateTabAsync("");
            if (tab == null) return;
            Activate(tab);
            string url = NormalizeAddress(input);
            if (url != null) Navigate(tab, url);
            else if (focusAddress) FocusAddress();
            else Hub.Post(new { type = "browserCommand", command = "focusAddress" });
        }

        private async Task ReopenAsync()
        {
            if (closedTabs.Count == 0) return;
            (string url, string profile) = closedTabs[closedTabs.Count - 1];
            closedTabs.RemoveAt(closedTabs.Count - 1);
            await OpenAsync(url, profile, true);
        }

        private async Task<StoreTab> CreateTabAsync(string profile)
        {
            var view = new StoreWebView { DefaultBackgroundColor = BrowserBackground, Visible = false };
            if (bounds.Width > 1) view.Bounds = bounds;
            var tab = new StoreTab(nextId++, profile, view);
            view.Shortcut = keys => OnShortcut(keys);
            tabs.Add(tab);
            Hub.Form.Controls.Add(view);
            MarkDirty();

            try
            {
                CoreWebView2ControllerOptions options = null;
                if (tab.Profile.Length > 0)
                {
                    options = Hub.Environment.CreateCoreWebView2ControllerOptions();
                    options.ProfileName = tab.Profile;
                }
                tab.Ready = options == null ? view.EnsureCoreWebView2Async(Hub.Environment) : view.EnsureCoreWebView2Async(Hub.Environment, options);
                await tab.Ready;
            }
            catch (Exception error)
            {
                Trace.WriteLine("StoreBrowser tab: " + error);
                DisposeTab(tab);
                Hub.Toast("Couldn't open a browser tab: " + error.Message, "error");
                return null;
            }

            if (tab.Closed) return null;
            Configure(tab);
            return tab;
        }

        private void Configure(StoreTab tab)
        {
            CoreWebView2 core = tab.Core;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = true;
            core.Settings.IsStatusBarEnabled = true;
            core.Settings.IsZoomControlEnabled = true;
            try
            {
                core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark;
            }
            catch
            {
            }

            try
            {
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
            }
            catch
            {
                core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            }

            core.WebResourceRequested += (sender, args) => OnResourceRequested(tab, args);
            core.NewWindowRequested += (sender, args) => OnNewWindowRequested(tab, args);
            core.WindowCloseRequested += (sender, args) => Hub.Form.BeginInvoke(new Action(() => CloseTab(tab)));
            core.NavigationStarting += (sender, args) =>
            {
                tab.Loading = true;
                tab.PendingUri = args.Uri;
                tab.Blocked = 0;
                tab.PopupsBlocked = 0;
                MarkDirty();
            };
            core.NavigationCompleted += (sender, args) =>
            {
                tab.Loading = false;
                tab.PendingUri = null;
                ApplyLayout();
                MarkDirty();
            };
            core.SourceChanged += (sender, args) =>
            {
                tab.PageHost = StoreDomains.HostOf(core.Source);
                if (tab.PageHost.Length > 0) history.Record(core.Source, core.DocumentTitle);
                ApplyZoomFor(tab);
                ApplyLayout();
                MarkDirty();
            };
            core.DocumentTitleChanged += (sender, args) =>
            {
                history.UpdateTitle(core.Source, core.DocumentTitle);
                MarkDirty();
            };
            core.HistoryChanged += (sender, args) => MarkDirty();
            core.FaviconChanged += (sender, args) => MarkDirty();
            core.IsDocumentPlayingAudioChanged += (sender, args) => MarkDirty();
            core.IsMutedChanged += (sender, args) => MarkDirty();
            core.ContainsFullScreenElementChanged += (sender, args) =>
            {
                tab.FullscreenElement = core.ContainsFullScreenElement;
                ApplyLayout();
            };
            core.ProcessFailed += (sender, args) =>
            {
                if (args.ProcessFailedKind != CoreWebView2ProcessFailedKind.RenderProcessExited && args.ProcessFailedKind != CoreWebView2ProcessFailedKind.RenderProcessUnresponsive) return;
                tab.Loading = false;
                MarkDirty();
                if (tab == active) Hub.Toast("This page stopped working. Reload it to try again.", "error");
            };
            tab.View.ZoomFactorChanged += (sender, args) => OnZoomChanged(tab);
            downloads.Attach(core, () => tab.PageHost);
        }

        private void OnResourceRequested(StoreTab tab, CoreWebView2WebResourceRequestedEventArgs args)
        {
            if (!adBlock.Enabled) return;
            string pageHost = tab.PageHost;
            if (adBlock.IsSiteAllowed(pageHost)) return;

            string uri = args.Request.Uri;
            if (uri == tab.PendingUri) return;
            string host = StoreDomains.HostOf(uri);
            if (host.Length == 0 || host == pageHost || !adBlock.ShouldBlock(host, pageHost)) return;

            args.Response = Hub.Environment.CreateWebResourceResponse(null, 403, "Blocked", "Content-Type: text/plain");
            tab.Blocked++;
            MarkDirty();
        }

        private async void OnNewWindowRequested(StoreTab tab, CoreWebView2NewWindowRequestedEventArgs args)
        {
            string uri = args.Uri ?? "";
            bool web = uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            bool blank = uri.Length == 0 || uri == "about:blank";
            if (!web && !blank)
            {
                args.Handled = true;
                return;
            }

            if (!args.IsUserInitiated && adBlock.Enabled && !adBlock.IsSiteAllowed(tab.PageHost))
            {
                args.Handled = true;
                tab.PopupsBlocked++;
                tab.Blocked++;
                MarkDirty();
                return;
            }

            if (OpenTabCount >= MaxTabs)
            {
                args.Handled = true;
                if (web) Navigate(tab, uri);
                return;
            }

            CoreWebView2Deferral deferral = args.GetDeferral();
            try
            {
                StoreTab created = await CreateTabAsync(tab.Profile);
                if (created?.Core != null)
                {
                    if (web) created.PendingUri = uri;
                    args.NewWindow = created.Core;
                    Activate(created);
                }
                args.Handled = true;
            }
            catch (Exception error)
            {
                Trace.WriteLine("StoreBrowser new window: " + error);
                args.Handled = true;
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void Navigate(StoreTab tab, string url)
        {
            if (tab?.Core == null || url == null) return;
            tab.PendingUri = url;
            tab.Core.Navigate(url);
            MarkDirty();
        }

        private void Activate(StoreTab tab)
        {
            if (tab == null || tab.Closed) return;
            if (active != null && active != tab) active.LastActive = DateTime.UtcNow;
            active = tab;
            tab.LastActive = DateTime.UtcNow;
            try
            {
                if (tab.Core != null)
                {
                    tab.Core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Normal;
                    if (tab.Core.IsSuspended) tab.Core.Resume();
                }
            }
            catch
            {
            }
            ApplyLayout();
            MarkDirty();
        }

        private void Cycle(int delta, bool focus)
        {
            List<StoreTab> open = tabs.Where(tab => !tab.Closed).ToList();
            if (open.Count < 2) return;
            int index = Math.Max(0, open.IndexOf(active));
            Activate(open[((index + delta) % open.Count + open.Count) % open.Count]);
            if (focus) FocusActive();
        }

        private void CloseTab(StoreTab tab)
        {
            if (tab == null || tab.Closed) return;
            int index = tabs.IndexOf(tab);
            if (!tab.IsBlank)
            {
                closedTabs.Add((tab.Url, tab.Profile));
                if (closedTabs.Count > ClosedTabMemory) closedTabs.RemoveAt(0);
            }
            DisposeTab(tab);

            if (active == tab)
            {
                active = null;
                if (tabs.Count > 0) Activate(tabs[Math.Min(index, tabs.Count - 1)]);
            }
            ApplyLayout();
            MarkDirty();
        }

        private void DisposeTab(StoreTab tab)
        {
            tab.Closed = true;
            tabs.Remove(tab);
            StoreWebView view = tab.View;
            view.Visible = false;
            Hub.Form.BeginInvoke(new Action(() =>
            {
                Hub.Form.Controls.Remove(view);
                view.Dispose();
            }));
        }

        private void ApplyLayout()
        {
            foreach (StoreTab tab in tabs.ToList())
            {
                StoreWebView view = tab.View;
                if (view.IsDisposed) continue;

                bool show = wantVisible && tab == active && bounds.Width > 1 && tab.Core != null && !tab.IsBlank;
                Rectangle target = show && tab.FullscreenElement ? Hub.Form.ClientRectangle : bounds;
                if (target.Width > 1 && view.Bounds != target) view.Bounds = target;

                if (show && !view.Visible)
                {
                    view.Visible = true;
                    view.BringToFront();
                }
                else if (show && tab.FullscreenElement)
                {
                    view.BringToFront();
                }
                else if (!show && view.Visible)
                {
                    view.Visible = false;
                }
            }
        }

        private async Task SnapshotAsync()
        {
            StoreTab tab = active;
            if (tab?.Core == null || !tab.View.Visible || tab.IsBlank) return;
            try
            {
                using var stream = new MemoryStream();
                await tab.Core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, stream);
                if (wantVisible) return;
                Hub.Post(new { type = "browserSnapshot", id = tab.Id, image = "data:image/jpeg;base64," + Convert.ToBase64String(stream.ToArray()) });
                await Task.Delay(40);
            }
            catch (Exception error)
            {
                Trace.WriteLine("StoreBrowser snapshot: " + error);
            }
        }

        private void SuspendIdleTabs()
        {
            DateTime now = DateTime.UtcNow;
            foreach (StoreTab tab in tabs.ToList())
            {
                CoreWebView2 core = tab.Core;
                if (tab.Closed || core == null) continue;
                if (tab.View.Visible)
                {
                    tab.LastActive = now;
                    continue;
                }
                try
                {
                    if (core.IsSuspended || core.IsDocumentPlayingAudio || tab.Loading) continue;
                    TimeSpan limit = tab == active ? HiddenSuspendDelay : BackgroundSuspendDelay;
                    if (now - tab.LastActive < limit) continue;
                    core.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low;
                    _ = SuspendAsync(tab);
                }
                catch
                {
                }
            }
        }

        private async Task SuspendAsync(StoreTab tab)
        {
            try
            {
                if (await tab.Core.TrySuspendAsync()) MarkDirty();
            }
            catch
            {
            }
        }

        private bool OnShortcut(Keys keys)
        {
            Keys key = keys & Keys.KeyCode;
            bool control = (keys & Keys.Control) != 0;
            bool shift = (keys & Keys.Shift) != 0;
            bool alt = (keys & Keys.Alt) != 0;

            if (control && !alt)
            {
                switch (key)
                {
                    case Keys.T:
                        if (shift) _ = ReopenAsync();
                        else _ = NewTabAsync(null, true);
                        return true;
                    case Keys.W:
                        CloseTab(active);
                        FocusActive();
                        return true;
                    case Keys.Tab:
                        Cycle(shift ? -1 : 1, true);
                        return true;
                    case Keys.PageDown:
                        Cycle(1, true);
                        return true;
                    case Keys.PageUp:
                        Cycle(-1, true);
                        return true;
                    case Keys.L:
                        FocusAddress();
                        return true;
                    case Keys.D:
                        Hub.Post(new { type = "browserCommand", command = "bookmark" });
                        return true;
                    case Keys.H:
                        Hub.Post(new { type = "browserCommand", command = "history" });
                        return true;
                    case Keys.J:
                        Hub.Post(new { type = "browserCommand", command = "downloads" });
                        return true;
                }
                if (key >= Keys.D1 && key <= Keys.D9 && !shift)
                {
                    List<StoreTab> open = tabs.Where(tab => !tab.Closed).ToList();
                    int index = key == Keys.D9 ? open.Count - 1 : key - Keys.D1;
                    if (index >= 0 && index < open.Count)
                    {
                        Activate(open[index]);
                        FocusActive();
                    }
                    return true;
                }
            }

            if ((alt && !control && key == Keys.D) || (keys == Keys.F6))
            {
                FocusAddress();
                return true;
            }
            return false;
        }

        private void FocusActive()
        {
            if (active?.View.Visible == true) active.View.Focus();
            else FocusAddress();
        }

        private void FocusAddress()
        {
            Hub.Form.Controls.OfType<WebView2>().FirstOrDefault(view => !(view is StoreWebView))?.Focus();
            Hub.Post(new { type = "browserCommand", command = "focusAddress" });
        }

        private void Zoom(int delta)
        {
            if (active == null || active.Core == null) return;
            double current = active.View.ZoomFactor;
            double next = delta switch
            {
                > 0 => ZoomSteps.Where(step => step > current + 0.001).DefaultIfEmpty(ZoomSteps.Last()).First(),
                < 0 => ZoomSteps.Where(step => step < current - 0.001).DefaultIfEmpty(ZoomSteps.First()).Last(),
                _ => 1.0,
            };
            active.View.ZoomFactor = next;
        }

        private void OnZoomChanged(StoreTab tab)
        {
            string site = SiteKey(tab.PageHost);
            if (site.Length > 0)
            {
                double zoom = Math.Round(tab.View.ZoomFactor, 3);
                bool known = zoomBySite.TryGetValue(site, out double saved);
                if (Math.Abs(zoom - 1.0) < 0.001)
                {
                    if (known)
                    {
                        zoomBySite.Remove(site);
                        RestartZoomSave();
                    }
                }
                else if (!known || Math.Abs(saved - zoom) > 0.001)
                {
                    zoomBySite[site] = zoom;
                    RestartZoomSave();
                }

                foreach (StoreTab other in tabs.Where(entry => entry != tab && !entry.Closed && entry.Core != null && SiteKey(entry.PageHost) == site))
                {
                    if (Math.Abs(other.View.ZoomFactor - zoom) > 0.001) other.View.ZoomFactor = zoom;
                }
            }
            MarkDirty();
        }

        private void ApplyZoomFor(StoreTab tab)
        {
            string site = SiteKey(tab.PageHost);
            if (site.Length == 0) return;
            double target = zoomBySite.TryGetValue(site, out double saved) ? saved : 1.0;
            if (Math.Abs(tab.View.ZoomFactor - target) > 0.001) tab.View.ZoomFactor = target;
        }

        private void RestartZoomSave()
        {
            zoomSaveDelay.Stop();
            zoomSaveDelay.Start();
        }

        private Dictionary<string, double> ReadZoom()
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (Hub.Settings.Get("store.zoom") is Dictionary<string, object> saved)
            {
                foreach (KeyValuePair<string, object> entry in saved)
                {
                    try
                    {
                        double zoom = Convert.ToDouble(entry.Value);
                        if (zoom >= 0.25 && zoom <= 5) result[entry.Key] = zoom;
                    }
                    catch
                    {
                    }
                }
            }
            return result;
        }

        private void SaveZoom()
        {
            Hub.Settings.Set("store.zoom", zoomBySite.Count == 0 ? null : zoomBySite.ToDictionary(entry => entry.Key, entry => (object)entry.Value));
        }

        private static string SiteKey(string host)
        {
            if (string.IsNullOrEmpty(host)) return "";
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
        }

        private void ToggleAds()
        {
            string host = active?.PageHost;
            if (string.IsNullOrEmpty(host)) return;
            bool allowed = adBlock.ToggleSite(host);
            Hub.Post(new { type = "settings", settings = Hub.Settings.Snapshot() });
            Hub.Toast(allowed ? $"Ads and pop-ups allowed on {SiteKey(host)}" : $"Blocking ads and pop-ups on {SiteKey(host)}", "info");
            active.Core?.Reload();
            MarkDirty();
        }

        private void PickDownloadFolder()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose where the store browser saves downloads",
                SelectedPath = downloads.DefaultFolder,
                ShowNewFolderButton = true,
            };
            if (dialog.ShowDialog(Hub.Form) != DialogResult.OK || string.IsNullOrEmpty(dialog.SelectedPath)) return;
            Hub.Settings.Set("store.downloadFolder", dialog.SelectedPath);
            Hub.Post(new { type = "settings", settings = Hub.Settings.Snapshot() });
        }

        private void PostHistory()
        {
            Hub.Post(new { type = "browserHistory", items = history.Visits.ToList() });
        }

        private void MarkDirty()
        {
            if (statePending || Hub.Form.IsDisposed || !Hub.Form.IsHandleCreated) return;
            statePending = true;
            Hub.Form.BeginInvoke(new Action(() =>
            {
                statePending = false;
                PostState();
            }));
        }

        private void PostState()
        {
            StoreTab current = active;
            CoreWebView2 core = current?.Core;
            string host = current?.PageHost ?? "";
            Hub.Post(new
            {
                type = "browserState",
                activeId = current?.Id ?? 0,
                maxTabs = MaxTabs,
                tabs = tabs.Where(tab => !tab.Closed).Select(tab => new
                {
                    id = tab.Id,
                    title = tab.Title,
                    url = tab.Url,
                    favicon = SafeCall(() => tab.Core?.FaviconUri) ?? "",
                    loading = tab.Loading,
                    blank = tab.IsBlank,
                    audible = SafeCall(() => tab.Core?.IsDocumentPlayingAudio) ?? false,
                    muted = SafeCall(() => tab.Core?.IsMuted) ?? false,
                    suspended = SafeCall(() => tab.Core?.IsSuspended) ?? false,
                    profile = tab.Profile,
                }).ToList(),
                url = current?.Url ?? "",
                title = current?.Title ?? "",
                blank = current?.IsBlank ?? true,
                canGoBack = core?.CanGoBack ?? false,
                canGoForward = core?.CanGoForward ?? false,
                loading = current?.Loading ?? false,
                zoom = current?.View.ZoomFactor ?? 1.0,
                host,
                profile = current?.Profile ?? "",
                blockAds = adBlock.Enabled,
                siteAllowed = adBlock.IsSiteAllowed(host),
                official = StoreDomains.IsOfficial(host),
                blocked = current?.Blocked ?? 0,
                popups = current?.PopupsBlocked ?? 0,
                filters = new { builtIn = adBlock.BuiltInCount, user = adBlock.UserCount, status = adBlock.UserListStatus },
                downloadsActive = downloads.ActiveCount,
            });
        }

        private static T SafeCall<T>(Func<T> read)
        {
            try
            {
                return read();
            }
            catch
            {
                return default;
            }
        }

        private static void OpenExternally(string url)
        {
            if (string.IsNullOrEmpty(url) || !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))) return;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private static Rectangle? ReadBounds(Dictionary<string, object> message)
        {
            if (!(message.TryGetValue("bounds", out object raw) && raw is Dictionary<string, object> box)) return null;
            try
            {
                double scale = message.TryGetValue("scale", out object value) ? Convert.ToDouble(value) : 1.0;
                int left = (int)Math.Round(Convert.ToDouble(box["x"]) * scale);
                int top = (int)Math.Round(Convert.ToDouble(box["y"]) * scale);
                int right = (int)Math.Round((Convert.ToDouble(box["x"]) + Convert.ToDouble(box["width"])) * scale);
                int bottom = (int)Math.Round((Convert.ToDouble(box["y"]) + Convert.ToDouble(box["height"])) * scale);
                return new Rectangle(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            }
            catch
            {
                return null;
            }
        }

        private static bool ReadBool(Dictionary<string, object> message, string key) => message.TryGetValue(key, out object value) && value is bool flag && flag;

        private static int ReadInt(Dictionary<string, object> message, string key, int fallback)
        {
            try
            {
                return message.TryGetValue(key, out object value) && value != null ? Convert.ToInt32(value) : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string CleanProfile(string profile)
        {
            if (string.IsNullOrWhiteSpace(profile)) return "";
            var builder = new StringBuilder();
            foreach (char character in profile.Trim())
            {
                if (char.IsLetterOrDigit(character) && character < 128 || character == '-' || character == '_') builder.Append(character);
            }
            string clean = builder.ToString();
            return clean.Length > 60 ? clean.Substring(0, 60) : clean;
        }

        private static string NormalizeAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            input = input.Trim();

            if (Uri.TryCreate(input, UriKind.Absolute, out Uri address) && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps))
            {
                return address.ToString();
            }
            if (!input.Contains(" ") && (input.Contains(".") || input.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)) && Uri.TryCreate("https://" + input, UriKind.Absolute, out address))
            {
                return address.ToString();
            }
            return "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
        }
    }
}
