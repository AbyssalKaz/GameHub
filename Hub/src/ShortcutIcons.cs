using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GameHub
{
    internal sealed class ShortcutIcons : HubModule
    {
        public const string SettingKey = "discovery.coverIcons";
        private static readonly string IconDir = Path.Combine(HubPaths.CacheDir, "shortcut-icons");
        private static readonly string MapFile = Path.Combine(IconDir, "map.json");
        private static readonly int[] Sizes = { 256, 64, 48, 32, 16 };
        private static readonly TimeSpan RefreshSpacing = TimeSpan.FromMinutes(10);

        private readonly object gate = new object();
        private bool building;
        private bool buildAgain;
        private DateTime lastRefresh = DateTime.MinValue;

        public override void Start()
        {
            Hub.Settings.Changed += (key, value) =>
            {
                if (key == SettingKey) Schedule(true);
            };
        }

        public override void OnLibraryLoaded(LibraryDocument library) => Schedule(false);

        private void Schedule(bool settingChanged)
        {
            lock (gate)
            {
                if (building)
                {
                    buildAgain = true;
                    return;
                }
                building = true;
            }

            bool enabled = Hub.Settings.GetBool(SettingKey, false);
            List<GameEntry> games = Hub.Library?.Games?.ToList() ?? new List<GameEntry>();
            Task.Run(() =>
            {
                bool changed = false;
                try
                {
                    changed = Build(games, enabled);
                }
                catch (Exception error)
                {
                    DiagLog.Write("discovery", "Shortcut icons failed: " + error.Message);
                }

                bool again;
                lock (gate)
                {
                    building = false;
                    again = buildAgain;
                    buildAgain = false;
                }

                if (changed && (settingChanged || DateTime.UtcNow - lastRefresh > RefreshSpacing))
                {
                    lastRefresh = DateTime.UtcNow;
                    Hub.RunOnUi(() => _ = Hub.RefreshLibraryAsync());
                }
                if (again) Hub.RunOnUi(() => Schedule(false));
            });
        }

        private static bool Build(List<GameEntry> games, bool enabled)
        {
            Directory.CreateDirectory(IconDir);
            Dictionary<string, string> previous = ReadMap();
            var map = new Dictionary<string, string>(StringComparer.Ordinal);

            if (enabled)
            {
                foreach (GameEntry game in games)
                {
                    if (game.Status != "installed") continue;
                    string source = LocalArtFile(game.CoverUrl);
                    if (source == null) continue;

                    string target = Path.Combine(IconDir, Hash(game.Id) + ".ico");
                    try
                    {
                        if (!File.Exists(target) || File.GetLastWriteTimeUtc(target) < File.GetLastWriteTimeUtc(source))
                        {
                            if (!WriteIcon(source, target)) continue;
                        }
                        map[game.Id] = target;
                    }
                    catch (Exception error)
                    {
                        DiagLog.Write("discovery", $"Could not make a shortcut icon for {game.Name}: {error.Message}");
                    }
                }
            }

            bool changed = previous.Count != map.Count || map.Any(pair => !previous.TryGetValue(pair.Key, out string old) || !string.Equals(old, pair.Value, StringComparison.OrdinalIgnoreCase));
            if (changed || !File.Exists(MapFile)) Json.WriteFile(MapFile, map);
            return changed;
        }

        private static Dictionary<string, string> ReadMap()
        {
            try
            {
                return Json.ReadFile<Dictionary<string, string>>(MapFile) ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public static string LocalArtFile(string url)
        {
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return null;

            string root;
            switch (uri.Host.ToLowerInvariant())
            {
                case "cache.hub":
                    root = HubPaths.PublicCacheDir;
                    break;
                case "steamart.hub":
                    root = HubPaths.SteamLibraryCacheDir;
                    break;
                case "hydraart.hub":
                    root = HubPaths.HydraAssetsDir;
                    break;
                default:
                    return null;
            }
            if (root == null) return null;

            string relative = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')).Replace('/', '\\');
            string full = Path.GetFullPath(Path.Combine(root, relative));
            if (!full.StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(full) ? full : null;
        }

        private static bool WriteIcon(string source, string target)
        {
            Image image;
            try
            {
                using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var loaded = Image.FromStream(stream);
                image = new Bitmap(loaded);
            }
            catch
            {
                return false;
            }

            using (image)
            {
                var frames = new List<byte[]>();
                foreach (int size in Sizes)
                {
                    using var canvas = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                    using (Graphics graphics = Graphics.FromImage(canvas))
                    {
                        graphics.Clear(Color.Transparent);
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality = CompositingQuality.HighQuality;

                        double scale = Math.Min((double)size / image.Width, (double)size / image.Height);
                        int width = Math.Max(1, (int)Math.Round(image.Width * scale));
                        int height = Math.Max(1, (int)Math.Round(image.Height * scale));
                        using var attributes = new ImageAttributes();
                        attributes.SetWrapMode(WrapMode.TileFlipXY);
                        graphics.DrawImage(image, new Rectangle((size - width) / 2, (size - height) / 2, width, height), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }

                    using var png = new MemoryStream();
                    canvas.Save(png, ImageFormat.Png);
                    frames.Add(png.ToArray());
                }

                string temporary = target + ".tmp";
                using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(output))
                {
                    writer.Write((ushort)0);
                    writer.Write((ushort)1);
                    writer.Write((ushort)frames.Count);
                    int offset = 6 + 16 * frames.Count;
                    for (int index = 0; index < frames.Count; index++)
                    {
                        int size = Sizes[index];
                        writer.Write((byte)(size >= 256 ? 0 : size));
                        writer.Write((byte)(size >= 256 ? 0 : size));
                        writer.Write((byte)0);
                        writer.Write((byte)0);
                        writer.Write((ushort)1);
                        writer.Write((ushort)32);
                        writer.Write(frames[index].Length);
                        writer.Write(offset);
                        offset += frames[index].Length;
                    }
                    foreach (byte[] frame in frames) writer.Write(frame);
                }
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target);
            }
            return true;
        }

        private static string Hash(string id)
        {
            using var sha = SHA1.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(id ?? ""));
            return string.Concat(bytes.Take(10).Select(value => value.ToString("x2")));
        }
    }
}
