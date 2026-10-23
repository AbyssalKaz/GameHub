using System;
using System.Net;
using System.Runtime.Versioning;
using System.Threading;
using System.Windows.Forms;

[assembly: TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]

namespace GameHub
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (InstallService.HandleCommandLine(Environment.GetCommandLineArgs())) return;
            using var instanceLock = new Mutex(true, "GameHub.SingleInstance", out bool isFirstInstance);
            if (!isFirstInstance)
            {
                PlayNative.AllowAnyForegroundWindow();
                if (!PlayCommands.Forward(args)) NativeMethods.ActivateExistingWindow(MainForm.WindowTitle);
                return;
            }

            PlayCommands.StartupArgs = args ?? new string[0];
            PlayCommandLine startup = PlayCommandLine.Parse(PlayCommands.StartupArgs);

            NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DpiAwarenessPerMonitorV2);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new MainForm();
            if (startup.Minimized && !startup.HasActions) StartHidden(form);
            Application.Run(form);
        }

        private static void StartHidden(MainForm form)
        {
            form.Opacity = 0;
            form.Shown += (sender, args) =>
            {
                form.Hide();
                form.Opacity = 1;
            };
        }
    }
}
