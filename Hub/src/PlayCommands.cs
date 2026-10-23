using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameHub
{
    internal sealed class PlayCommandLine
    {
        public string LaunchId { get; private set; }
        public bool Home { get; private set; }
        public bool Fullscreen { get; private set; }
        public bool Minimized { get; private set; }

        public bool HasActions => LaunchId != null || Home || Fullscreen;

        public static PlayCommandLine Parse(IEnumerable<string> args)
        {
            var result = new PlayCommandLine();
            List<string> list = (args ?? Enumerable.Empty<string>()).ToList();
            for (int index = 0; index < list.Count; index++)
            {
                switch (list[index].ToLowerInvariant())
                {
                    case "--launch":
                        if (index + 1 < list.Count) result.LaunchId = list[++index];
                        break;
                    case "--home":
                        result.Home = true;
                        break;
                    case "--fullscreen":
                        result.Fullscreen = true;
                        break;
                    case "--minimized":
                        result.Minimized = true;
                        break;
                }
            }
            return result;
        }
    }

    internal sealed class PlayCommands : HubModule
    {
        public const string StartWithWindowsKey = "play.startWithWindows";
        public const string StartMinimizedKey = "play.startMinimized";
        public const string StartConsoleKey = "play.startConsole";

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Game Hub";

        private static readonly string PipeName = "GameHub.Commands." + CurrentUserSid();

        private readonly object gate = new object();
        private readonly List<PlayCommandLine> pending = new List<PlayCommandLine>();
        private readonly CancellationTokenSource stopping = new CancellationTokenSource();
        private bool libraryReady;

        public static string[] StartupArgs { get; set; } = new string[0];

        public override int Order => 90;

        public static bool Forward(string[] args)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(2000);
                byte[] payload = Encoding.UTF8.GetBytes(Json.Serialize(args ?? new string[0]));
                client.Write(payload, 0, payload.Length);
                client.Flush();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override void Start()
        {
            PlayCommandLine startup = PlayCommandLine.Parse(StartupArgs);
            if (startup.Fullscreen) Hub.Form.BeginInvoke(new Action(() =>
            {
                if (!Hub.Form.IsFullscreen) Hub.Form.ToggleFullscreen();
            }));
            if (startup.LaunchId != null || startup.Home) Queue(startup);

            SyncRunAtStartup();
            Hub.Settings.Changed += (key, value) =>
            {
                if (key == StartWithWindowsKey || key == StartMinimizedKey || key == StartConsoleKey) SyncRunAtStartup();
            };

            Task.Run(ListenAsync);
        }

        public override void Stop() => stopping.Cancel();

        public override void OnLibraryLoaded(LibraryDocument library)
        {
            List<PlayCommandLine> ready;
            lock (gate)
            {
                libraryReady = true;
                ready = pending.ToList();
                pending.Clear();
            }
            foreach (PlayCommandLine command in ready) Hub.RunOnUi(() => Execute(command));
        }

        private void Queue(PlayCommandLine command)
        {
            lock (gate)
            {
                if (!libraryReady)
                {
                    pending.Add(command);
                    return;
                }
            }
            Hub.RunOnUi(() => Execute(command));
        }

        private void Execute(PlayCommandLine command)
        {
            if (command.Home) Hub.Post(new { type = "openHome" });
            if (command.LaunchId != null)
            {
                GameEntry game = Hub.Find(command.LaunchId);
                if (game == null) Hub.Toast("That game isn't in your library any more.", "error");
                else if (Hub.Module<PlayTracker>()?.IsRunning(game.Id) == true) Hub.Toast($"{game.Name} is already running.");
                else Hub.Launch(game.Id, "jumplist");
            }
        }

        private void Receive(string[] args)
        {
            PlayCommandLine command = PlayCommandLine.Parse(args);
            Hub.RunOnUi(() =>
            {
                bool launchOnly = command.LaunchId != null && !command.Home && !command.Fullscreen;
                if (!launchOnly || Hub.Form.Visible) Hub.Form.ShowAndActivate();
                if (command.Fullscreen && !Hub.Form.IsFullscreen) Hub.Form.ToggleFullscreen();
            });
            if (command.LaunchId != null || command.Home) Queue(command);
        }

        private async Task ListenAsync()
        {
            while (!stopping.IsCancellationRequested)
            {
                try
                {
                    using var server = CreateServer();
                    await server.WaitForConnectionAsync(stopping.Token).ConfigureAwait(false);
                    using var buffer = new MemoryStream();
                    var chunk = new byte[4096];
                    int read;
                    while (buffer.Length < 65536 && (read = await server.ReadAsync(chunk, 0, chunk.Length, stopping.Token).ConfigureAwait(false)) > 0)
                    {
                        buffer.Write(chunk, 0, read);
                    }
                    string text = Encoding.UTF8.GetString(buffer.ToArray());
                    string[] args = (Json.Parse(text) as object[] ?? new object[0]).Select(item => item?.ToString() ?? "").ToArray();
                    Receive(args);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception error)
                {
                    System.Diagnostics.Trace.WriteLine("PlayCommands: " + error.Message);
                    try
                    {
                        await Task.Delay(1000, stopping.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private static NamedPipeServerStream CreateServer()
        {
            var security = new PipeSecurity();
            SecurityIdentifier user = WindowsIdentity.GetCurrent().User;
            security.AddAccessRule(new PipeAccessRule(user, PipeAccessRights.FullControl, AccessControlType.Allow));
            return new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
        }

        private static string CurrentUserSid()
        {
            try
            {
                return WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            }
            catch
            {
                return Environment.UserName;
            }
        }

        public static string RunCommand(bool minimized, bool console)
        {
            var command = new StringBuilder("\"" + Application.ExecutablePath + "\"");
            if (console) command.Append(" --home --fullscreen");
            if (minimized) command.Append(" --minimized");
            return command.ToString();
        }

        private void SyncRunAtStartup()
        {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true) ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
                if (key == null) return;
                string current = key.GetValue(RunValueName) as string;
                if (Hub.Settings.GetBool(StartWithWindowsKey))
                {
                    bool console = Hub.Settings.GetBool(StartConsoleKey);
                    bool minimized = !console && Hub.Settings.GetBool(StartMinimizedKey);
                    string wanted = RunCommand(minimized, console);
                    if (current != wanted) key.SetValue(RunValueName, wanted, RegistryValueKind.String);
                }
                else if (current != null)
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
            catch (Exception error)
            {
                Hub.Toast("Couldn't change the Windows startup entry: " + error.Message, "error");
            }
        }
    }
}
