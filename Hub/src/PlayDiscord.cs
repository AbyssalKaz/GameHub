using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class PlayDiscord : HubModule
    {
        public const string EnabledKey = "play.discord";
        public const string AppIdKey = "play.discordAppId";

        private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(3);

        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly int ownProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
        private PlayTracker tracker;
        private Thread worker;
        private volatile bool stopping;
        private NamedPipeClientStream pipe;
        private string connectedAppId;
        private string shownSignature;

        public override int Order => 95;

        public override void Start()
        {
            tracker = Hub.Module<PlayTracker>();
            if (tracker != null) tracker.RunningChanged += () => wake.Set();
            Hub.Settings.Changed += (key, value) =>
            {
                if (key == EnabledKey || key == AppIdKey) wake.Set();
            };
            worker = new Thread(Run) { IsBackground = true, Name = "Discord presence" };
            worker.Start();
        }

        public override void Stop()
        {
            stopping = true;
            wake.Set();
            Disconnect();
        }

        private void Run()
        {
            while (!stopping)
            {
                try
                {
                    Update();
                }
                catch (Exception error)
                {
                    System.Diagnostics.Trace.WriteLine("PlayDiscord: " + error.Message);
                    Disconnect();
                }
                wake.WaitOne(RetryInterval);
            }
        }

        private void Update()
        {
            string appId = (Hub.Settings.GetString(AppIdKey) ?? "").Trim();
            bool enabled = Hub.Settings.GetBool(EnabledKey) && appId.Length > 0 && appId.All(char.IsDigit);
            PlayRunningGame current = enabled ? tracker?.Running().OrderByDescending(game => game.StartedUtc).FirstOrDefault() : null;
            GameEntry game = current == null ? null : Hub.Find(current.Id);

            if (game == null)
            {
                Disconnect();
                return;
            }

            if (pipe != null && connectedAppId != appId) Disconnect();
            if (pipe == null && !Connect(appId)) return;

            string signature = game.Id + "@" + current.StartedUtc.Ticks;
            if (signature == shownSignature) return;

            long start = new DateTimeOffset(DateTime.SpecifyKind(current.StartedUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
            var activity = new Dictionary<string, object>
            {
                ["details"] = Trim(game.Name, 128),
                ["timestamps"] = new Dictionary<string, object> { ["start"] = start },
            };
            string platform = PlatformName(game.Source);
            if (platform != null) activity["state"] = Trim("on " + platform, 128);

            var command = new Dictionary<string, object>
            {
                ["cmd"] = "SET_ACTIVITY",
                ["args"] = new Dictionary<string, object> { ["pid"] = ownProcessId, ["activity"] = activity },
                ["nonce"] = Guid.NewGuid().ToString(),
            };
            Send(1, command);
            Receive();
            shownSignature = signature;
        }

        private bool Connect(string appId)
        {
            for (int index = 0; index < 10; index++)
            {
                var candidate = new NamedPipeClientStream(".", "discord-ipc-" + index, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    candidate.Connect(300);
                    pipe = candidate;
                    Send(0, new Dictionary<string, object> { ["v"] = 1, ["client_id"] = appId });
                    (int op, string _) = Receive();
                    if (op != 1)
                    {
                        Disconnect();
                        continue;
                    }
                    connectedAppId = appId;
                    shownSignature = null;
                    return true;
                }
                catch
                {
                    if (pipe == candidate) pipe = null;
                    candidate.Dispose();
                }
            }
            return false;
        }

        private void Disconnect()
        {
            NamedPipeClientStream current = pipe;
            pipe = null;
            connectedAppId = null;
            shownSignature = null;
            try
            {
                current?.Dispose();
            }
            catch
            {
            }
        }

        private void Send(int op, object payload)
        {
            byte[] body = Encoding.UTF8.GetBytes(Json.Serialize(payload));
            var frame = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(body.Length).CopyTo(frame, 4);
            body.CopyTo(frame, 8);
            pipe.Write(frame, 0, frame.Length);
            pipe.Flush();
        }

        private (int Op, string Body) Receive()
        {
            byte[] header = ReadExactly(8);
            int op = BitConverter.ToInt32(header, 0);
            int length = BitConverter.ToInt32(header, 4);
            if (length < 0 || length > 1 << 20) throw new InvalidDataException("Discord sent an invalid frame.");
            string body = Encoding.UTF8.GetString(ReadExactly(length));
            if (op == 2) throw new IOException("Discord closed the connection: " + body);
            return (op, body);
        }

        private byte[] ReadExactly(int count)
        {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                Task<int> read = pipe.ReadAsync(buffer, offset, count - offset);
                if (!read.Wait(ReplyTimeout)) throw new TimeoutException("Discord didn't answer.");
                if (read.Result == 0) throw new EndOfStreamException();
                offset += read.Result;
            }
            return buffer;
        }

        private static string Trim(string text, int length) => text.Length <= length ? text : text.Substring(0, length - 1) + "…";

        private static string PlatformName(string source)
        {
            return source switch
            {
                "Steam" => "Steam",
                "Epic" => "Epic Games",
                "Riot" => "Riot",
                "Xbox" => "Xbox",
                "EA" => "EA",
                "Ubisoft" => "Ubisoft",
                "GOG" => "GOG",
                "Battle.net" => "Battle.net",
                _ => null,
            };
        }
    }
}
