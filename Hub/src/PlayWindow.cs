using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace GameHub
{
    internal sealed class PlayWindow : HubModule
    {
        public const string OnLaunchKey = "play.onLaunch";
        public const string RestoreOnExitKey = "play.restoreOnExit";
        public const string CloseToTrayKey = "play.closeToTray";

        private static readonly TimeSpan NoGameTimeout = TimeSpan.FromMinutes(2);
        private const int RecentCount = 8;

        private PlayTracker tracker;
        private ToolStripMenuItem recentMenu;
        private FormWindowState restoreState = FormWindowState.Normal;
        private string hiddenFor;
        private bool hiddenToTray;
        private bool sawGameRun;
        private System.Threading.Timer fallbackTimer;

        public override int Order => 70;

        public override void Start()
        {
            tracker = Hub.Module<PlayTracker>();
            ApplyCloseToTray();
            Hub.Settings.Changed += (key, value) =>
            {
                if (key == CloseToTrayKey) Hub.RunOnUi(ApplyCloseToTray);
            };

            Hub.Form.Resize += (sender, args) =>
            {
                if (Hub.Form.WindowState != FormWindowState.Minimized)
                {
                    restoreState = Hub.Form.WindowState;
                    return;
                }
                if (!Hub.Settings.GetBool(CloseToTrayKey) || !Hub.Form.Visible) return;
                Hub.Form.Hide();
                Hub.Form.WindowState = restoreState;
            };

            if (Hub.Tray != null)
            {
                recentMenu = Hub.Tray.AddMenuItem("Recent games", () => { });
                recentMenu.DropDownItems.Add(new ToolStripMenuItem("Nothing played yet") { Enabled = false });
                Hub.Tray.Opening += menu => RebuildRecentMenu();
            }

            if (tracker != null)
            {
                tracker.GameStarted += id =>
                {
                    if (hiddenFor != null && id == hiddenFor) sawGameRun = true;
                };
                tracker.RunningChanged += () =>
                {
                    if (hiddenFor == null || !sawGameRun || tracker.IsRunning(hiddenFor)) return;
                    ClearHidden();
                    if (Hub.Settings.GetBool(RestoreOnExitKey)) Hub.ShowWindow();
                };
            }
        }

        public override void Stop()
        {
            fallbackTimer?.Dispose();
        }

        public override void AfterLaunch(GameEntry game, LaunchRequest request, System.Diagnostics.Process process)
        {
            string mode = Hub.Settings.GetString(OnLaunchKey, "none");
            if (mode != "minimize" && mode != "tray") return;

            ClearHidden();
            hiddenFor = game.Id;
            hiddenToTray = mode == "tray";
            sawGameRun = tracker != null && tracker.IsRunning(game.Id);
            Hub.RunOnUi(() =>
            {
                if (hiddenToTray)
                {
                    if (Hub.Form.WindowState != FormWindowState.Minimized) restoreState = Hub.Form.WindowState;
                    Hub.Form.Hide();
                }
                else
                {
                    Hub.Form.Minimize();
                }
            });
            fallbackTimer = new System.Threading.Timer(_ => CheckNoGame(game.Id), null, NoGameTimeout, Timeout.InfiniteTimeSpan);
        }

        private void CheckNoGame(string id)
        {
            if (hiddenFor != id || sawGameRun) return;
            if (tracker != null && tracker.IsRunning(id))
            {
                sawGameRun = true;
                return;
            }
            bool wasTray = hiddenToTray;
            ClearHidden();
            if (wasTray || Hub.Settings.GetBool(RestoreOnExitKey)) Hub.ShowWindow();
        }

        private void ClearHidden()
        {
            fallbackTimer?.Dispose();
            fallbackTimer = null;
            hiddenFor = null;
            hiddenToTray = false;
            sawGameRun = false;
        }

        private void ApplyCloseToTray() => Hub.Form.HideOnClose = Hub.Settings.GetBool(CloseToTrayKey);

        private void RebuildRecentMenu()
        {
            if (recentMenu == null || Hub.Library == null) return;
            List<GameEntry> recent = RecentGames(RecentCount);
            recentMenu.DropDownItems.Clear();
            if (recent.Count == 0)
            {
                recentMenu.DropDownItems.Add(new ToolStripMenuItem("Nothing played yet") { Enabled = false });
                return;
            }
            foreach (GameEntry game in recent)
            {
                string id = game.Id;
                bool running = tracker != null && tracker.IsRunning(id);
                var item = new ToolStripMenuItem(game.Name.Replace("&", "&&"), null, (sender, args) => Hub.Launch(id, "tray"))
                {
                    Checked = running,
                    Enabled = !running,
                };
                recentMenu.DropDownItems.Add(item);
            }
        }

        public List<GameEntry> RecentGames(int count)
        {
            return Hub.Library.Games
                .Where(game => game.CanLaunch && PlayLogStore.ParseTime(game.LastPlayed) != null)
                .OrderByDescending(game => PlayLogStore.ParseTime(game.LastPlayed))
                .Take(count)
                .ToList();
        }
    }
}
