using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace GameHub
{
    internal sealed class XInputModule : HubModule
    {
        private const int PollIntervalMs = 100;
        private const int RescanIntervalMs = 2000;
        private const int GameHoldMs = 1000;

        private readonly object gate = new object();
        private readonly bool[] connected = new bool[XInputNative.MaxControllers];
        private readonly long[] guidePressedAt = new long[XInputNative.MaxControllers];
        private readonly int[] guideHoldNeeded = new int[XInputNative.MaxControllers];
        private readonly bool[] guideFired = new bool[XInputNative.MaxControllers];
        private readonly int ownProcessId = Process.GetCurrentProcess().Id;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private Thread worker;
        private volatile bool running;
        private long lastRescan = -RescanIntervalMs;

        public override int Order => 60;

        public override void Start()
        {
            running = true;
            worker = new Thread(PollLoop) { IsBackground = true, Name = "XInputPoll", Priority = ThreadPriority.BelowNormal };
            worker.Start();
        }

        public override void Stop()
        {
            running = false;
        }

        public List<Dictionary<string, object>> ConnectedPads()
        {
            var pads = new List<Dictionary<string, object>>();
            for (int index = 0; index < XInputNative.MaxControllers; index++)
            {
                lock (gate)
                {
                    if (!connected[index]) continue;
                }
                string battery = XInputNative.TryGetBattery(index, out XInputNative.BatteryInformation information)
                    ? XInputNative.BatteryLabel(information)
                    : "unknown";
                pads.Add(new Dictionary<string, object> { ["index"] = index, ["battery"] = battery, ["type"] = "xbox" });
            }
            return pads;
        }

        private string GuideMode()
        {
            object value = Hub.Settings.Get("console.guideButton");
            return value switch
            {
                bool enabled => enabled ? "smart" : "off",
                string mode when mode == "off" || mode == "smart" || mode == "always" => mode,
                _ => "smart",
            };
        }

        private void PollLoop()
        {
            while (running)
            {
                try
                {
                    PollOnce();
                }
                catch (Exception error)
                {
                    DiagLog.Write("xinput", "Polling failed: " + error.Message);
                    Thread.Sleep(RescanIntervalMs);
                }
                Thread.Sleep(PollIntervalMs);
            }
        }

        private void PollOnce()
        {
            long now = clock.ElapsedMilliseconds;
            bool rescan = now - lastRescan >= RescanIntervalMs;
            if (rescan) lastRescan = now;
            string mode = GuideMode();

            for (int index = 0; index < XInputNative.MaxControllers; index++)
            {
                bool wasConnected;
                lock (gate) wasConnected = connected[index];
                if (!wasConnected && !rescan) continue;

                bool isConnected = XInputNative.TryGetState(index, out XInputNative.GamepadState state);
                lock (gate) connected[index] = isConnected;
                if (!isConnected)
                {
                    guidePressedAt[index] = 0;
                    guideFired[index] = false;
                    continue;
                }

                bool guideDown = (state.Buttons & XInputNative.GuideButton) != 0;
                if (!guideDown || mode == "off")
                {
                    guidePressedAt[index] = 0;
                    guideFired[index] = false;
                    continue;
                }

                if (guidePressedAt[index] == 0)
                {
                    guidePressedAt[index] = now;
                    guideFired[index] = false;
                    guideHoldNeeded[index] = mode == "smart" && GameInForeground() ? GameHoldMs : 0;
                }

                if (!guideFired[index] && now - guidePressedAt[index] >= guideHoldNeeded[index])
                {
                    guideFired[index] = true;
                    OpenHome();
                }
            }
        }

        private bool GameInForeground()
        {
            try
            {
                IntPtr window = ConsoleNative.GetForegroundWindow();
                uint processId = ConsoleNative.WindowProcessId(window);
                if (processId == 0 || processId == ownProcessId) return false;

                var tracker = Hub.Module<PlayTracker>();
                if (tracker != null && tracker.Running().Any(game => game.MainProcessId == processId || game.ProcessIds.Contains((int)processId))) return true;

                string path = ConsoleNative.ProcessPath(processId);
                if (!string.IsNullOrEmpty(path))
                {
                    string name = Path.GetFileName(path);
                    if (string.Equals(name, "explorer.exe", StringComparison.OrdinalIgnoreCase)) return false;
                    if (UnderInstallPath(path)) return true;
                }
                return ConsoleNative.CoversMonitor(window);
            }
            catch (Exception)
            {
                return true;
            }
        }

        private bool UnderInstallPath(string path)
        {
            var library = Hub.Library;
            if (library?.Games == null) return false;
            foreach (GameEntry game in library.Games.ToList())
            {
                string root = game.InstallPath;
                if (string.IsNullOrEmpty(root) || root.Length < 4) continue;
                string prefix = root.TrimEnd('\\', '/') + "\\";
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void OpenHome()
        {
            Hub.RunOnUi(() =>
            {
                Hub.Form.ShowAndActivate();
                if (Hub.Form.IsHandleCreated) ConsoleNative.ForceForeground(Hub.Form.Handle);
                Hub.Post(new { type = "openHome", source = "guide" });
            });
        }
    }
}
