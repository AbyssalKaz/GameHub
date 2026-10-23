using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;

namespace GameHub
{
    internal static class ThemeNative
    {
        public static readonly Color LightBorder = Color.FromArgb(206, 211, 222);

        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmBorderColor = 34;
        private const int DwmCaptionColor = 35;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public static void ApplyFrame(IntPtr window, Color caption, Color border, bool dark)
        {
            int darkMode = dark ? 1 : 0;
            DwmSetWindowAttribute(window, DwmUseImmersiveDarkMode, ref darkMode, sizeof(int));
            int captionRef = ToColorRef(caption);
            DwmSetWindowAttribute(window, DwmCaptionColor, ref captionRef, sizeof(int));
            int borderRef = ToColorRef(border);
            DwmSetWindowAttribute(window, DwmBorderColor, ref borderRef, sizeof(int));
        }

        public static Color? ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string hex = value.Trim().TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return null;
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        private static int ToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);
    }
}
