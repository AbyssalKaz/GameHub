using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GameHub
{
    internal static class NativeMethods
    {
        public static readonly IntPtr DpiAwarenessPerMonitorV2 = new IntPtr(-4);
        public static readonly IntPtr InvalidHandle = new IntPtr(-1);

        private const int DwmUseImmersiveDarkMode = 20;
        private const int DwmCaptionColor = 35;
        private const int ShowRestore = 9;

        public const int FindExInfoBasic = 1;
        public const int FindFirstExLargeFetch = 2;
        public const uint FileAttributeDirectory = 0x10;
        public const uint FileAttributeReparsePoint = 0x400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct Win32FindData
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint Reserved0;
            public uint Reserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string FileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string AlternateFileName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindFirstFileExW(string fileName, int infoLevel, out Win32FindData findData, int searchOp, IntPtr searchFilter, int additionalFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool FindNextFileW(IntPtr findHandle, out Win32FindData findData);

        [DllImport("kernel32.dll")]
        public static extern bool FindClose(IntPtr findHandle);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHDefExtractIconW(string iconFile, int iconIndex, uint flags, out IntPtr largeIcon, out IntPtr smallIcon, uint iconSize);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowW(string className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public const int WmSize = 0x0005;
        public const int WmNcCalcSize = 0x0083;
        public const int WmNcLeftButtonDown = 0x00A1;
        public const int HitCaption = 2;
        private const int DwmBorderColor = 34;
        private const int StyleIndex = -16;
        private const int CaptionStyle = 0x00C00000;
        private const int ThickFrameStyle = 0x00040000;
        private const uint FrameChanged = 0x0020;
        private const uint NoZOrder = 0x0004;
        private const uint NoMove = 0x0002;
        private const uint NoSize = 0x0001;
        private const uint NoOwnerZOrder = 0x0200;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NcCalcSizeParams
        {
            public Rect Proposed;
            public Rect Previous;
            public Rect PreviousClient;
            public IntPtr Position;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr window, int index, int value);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

        public static void UseDarkTitleBar(IntPtr window, Color caption)
        {
            int enabled = 1;
            DwmSetWindowAttribute(window, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
            int colorRef = caption.R | (caption.G << 8) | (caption.B << 16);
            DwmSetWindowAttribute(window, DwmCaptionColor, ref colorRef, sizeof(int));
        }

        public static void UseBorderlessFrame(IntPtr window, Color border)
        {
            int colorRef = border.R | (border.G << 8) | (border.B << 16);
            DwmSetWindowAttribute(window, DwmBorderColor, ref colorRef, sizeof(int));
            var margins = new Margins { Top = 1 };
            DwmExtendFrameIntoClientArea(window, ref margins);
            SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, FrameChanged | NoMove | NoSize | NoZOrder | NoOwnerZOrder);
        }

        public static Rect GetWorkArea(IntPtr window)
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            GetMonitorInfo(MonitorFromWindow(window, 2), ref info);
            return info.Work;
        }

        public const int FrameStyles = CaptionStyle | ThickFrameStyle;
        public const int WmStyleChanging = 0x007C;
        public const int StyleIndexValue = StyleIndex;

        [StructLayout(LayoutKind.Sequential)]
        public struct StyleStruct
        {
            public int Old;
            public int New;
        }

        public static void AddFrameStyles(IntPtr window)
        {
            SetWindowLong(window, StyleIndex, GetWindowLong(window, StyleIndex) | FrameStyles);
        }

        public static void SetFramed(IntPtr window, bool framed, Rectangle bounds)
        {
            int style = GetWindowLong(window, StyleIndex);
            style = framed ? style | CaptionStyle | ThickFrameStyle : style & ~(CaptionStyle | ThickFrameStyle);
            SetWindowLong(window, StyleIndex, style);
            SetWindowPos(window, IntPtr.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height, FrameChanged | NoOwnerZOrder);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowPlacement
        {
            public int Length;
            public int Flags;
            public int ShowCommand;
            public Point MinPosition;
            public Point MaxPosition;
            public Rect NormalPosition;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr window, ref WindowPlacement placement);

        public static Rectangle GetNormalBounds(IntPtr window)
        {
            var placement = new WindowPlacement { Length = Marshal.SizeOf(typeof(WindowPlacement)) };
            GetWindowPlacement(window, ref placement);

            var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            GetMonitorInfo(MonitorFromWindow(window, 2), ref info);
            int offsetX = info.Work.Left - info.Monitor.Left;
            int offsetY = info.Work.Top - info.Monitor.Top;

            Rect normal = placement.NormalPosition;
            return new Rectangle(normal.Left + offsetX, normal.Top + offsetY, normal.Right - normal.Left, normal.Bottom - normal.Top);
        }

        public static IntPtr PackPoint(Point point)
        {
            return new IntPtr((point.Y << 16) | (point.X & 0xFFFF));
        }

        public static void ActivateExistingWindow(string title)
        {
            IntPtr window = FindWindowW(null, title);
            if (window == IntPtr.Zero) return;
            if (IsIconic(window)) ShowWindow(window, ShowRestore);
            SetForegroundWindow(window);
        }
    }
}
