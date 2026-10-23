using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GameHub
{
    internal static class IconService
    {
        public static string GetUrl(string file, int size)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return null;

            string name = Hash(file + "|" + File.GetLastWriteTimeUtc(file).Ticks + "|" + size) + ".png";
            string target = Path.Combine(HubPaths.IconCacheDir, name);
            if (!File.Exists(target) && !Extract(file, size, target)) return null;
            return "https://cache.hub/icons/" + name;
        }

        public static Dictionary<string, string> GetPlatformIcons(Dictionary<string, string> launchers)
        {
            var icons = new Dictionary<string, string>();
            string steamRoot = HubPaths.SteamRoot;
            if (steamRoot != null) icons["steam"] = GetUrl(Path.Combine(steamRoot, "steam.exe"), 64);
            icons["epic"] = GetUrl(HubPaths.GetProtocolHandlerExe("com.epicgames.launcher"), 64);
            icons["hydra"] = GetUrl(HubPaths.GetProtocolHandlerExe("hydralauncher"), 64);

            foreach (var launcher in launchers ?? new Dictionary<string, string>())
            {
                string icon = GetUrl(launcher.Value, 64);
                if (icon != null) icons[launcher.Key] = icon;
            }
            return icons;
        }

        private static bool Extract(string file, int size, string target)
        {
            IntPtr large = IntPtr.Zero;
            IntPtr small = IntPtr.Zero;
            try
            {
                if (string.Equals(Path.GetExtension(file), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(file, target, true);
                    return true;
                }

                uint sizes = (uint)((16 << 16) | (size & 0xFFFF));
                Bitmap bitmap = null;
                if (NativeMethods.SHDefExtractIconW(file, 0, 0, out large, out small, sizes) == 0 && large != IntPtr.Zero)
                {
                    using Icon icon = Icon.FromHandle(large);
                    bitmap = icon.ToBitmap();
                }
                else
                {
                    using Icon icon = Icon.ExtractAssociatedIcon(file);
                    bitmap = icon?.ToBitmap();
                }
                if (bitmap == null) return false;

                using (bitmap) bitmap.Save(target, ImageFormat.Png);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (large != IntPtr.Zero) NativeMethods.DestroyIcon(large);
                if (small != IntPtr.Zero) NativeMethods.DestroyIcon(small);
            }
        }

        private static string Hash(string value)
        {
            using var md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }
}
