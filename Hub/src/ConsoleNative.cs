using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GameHub
{
    internal static class ConsoleNative
    {
        private const int SwRestore = 9;
        private const uint MonitorDefaultToNearest = 2;
        private const byte VkMenu = 0x12;
        private const uint KeyEventKeyUp = 0x0002;
        private const uint ProcessQueryLimitedInformation = 0x1000;

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor;
            public Rect Work;
            public uint Flags;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out Rect rect);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr window, uint command);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint attach, uint attachTo, bool join);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("secur32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetUserNameEx(int format, StringBuilder name, ref int size);

        public static uint WindowProcessId(IntPtr window)
        {
            if (window == IntPtr.Zero) return 0;
            GetWindowThreadProcessId(window, out uint processId);
            return processId;
        }

        public static string ProcessPath(uint processId)
        {
            if (processId == 0) return null;
            IntPtr process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero) return null;
            try
            {
                var builder = new StringBuilder(1024);
                int size = builder.Capacity;
                return QueryFullProcessImageName(process, 0, builder, ref size) ? builder.ToString(0, size) : null;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        public static bool CoversMonitor(IntPtr window)
        {
            if (window == IntPtr.Zero || !GetWindowRect(window, out Rect rect)) return false;
            IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return false;
            return rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top &&
                   rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom;
        }

        public static void ForceForeground(IntPtr window)
        {
            if (window == IntPtr.Zero) return;
            if (IsIconic(window)) ShowWindow(window, SwRestore);

            IntPtr foreground = GetForegroundWindow();
            if (foreground == window) return;

            uint current = GetCurrentThreadId();
            uint target = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
            bool attached = target != 0 && target != current && AttachThreadInput(current, target, true);
            try
            {
                keybd_event(VkMenu, 0, 0, UIntPtr.Zero);
                keybd_event(VkMenu, 0, KeyEventKeyUp, UIntPtr.Zero);
                BringWindowToTop(window);
                SetForegroundWindow(window);
            }
            finally
            {
                if (attached) AttachThreadInput(current, target, false);
            }
        }

        public static List<IntPtr> TopWindowsOf(ISet<uint> processIds)
        {
            var windows = new List<IntPtr>();
            EnumWindows((window, _) =>
            {
                if (!IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero) return true;
                if (!processIds.Contains(WindowProcessId(window))) return true;
                if (GetWindowTextLength(window) == 0 && !IsIconic(window)) return true;
                windows.Add(window);
                return true;
            }, IntPtr.Zero);
            return windows;
        }

        public static string DisplayName()
        {
            try
            {
                var builder = new StringBuilder(256);
                int size = builder.Capacity;
                if (GetUserNameEx(3, builder, ref size) && size > 0) return builder.ToString(0, size);
            }
            catch (Exception)
            {
            }
            return Environment.UserName;
        }

        [DllImport("wlanapi.dll")]
        private static extern int WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr handle);

        [DllImport("wlanapi.dll")]
        private static extern int WlanCloseHandle(IntPtr handle, IntPtr reserved);

        [DllImport("wlanapi.dll")]
        private static extern int WlanEnumInterfaces(IntPtr handle, IntPtr reserved, out IntPtr list);

        [DllImport("wlanapi.dll")]
        private static extern int WlanQueryInterface(IntPtr handle, ref Guid interfaceGuid, int opCode, IntPtr reserved, out int size, out IntPtr data, out int valueType);

        [DllImport("wlanapi.dll")]
        private static extern void WlanFreeMemory(IntPtr memory);

        public sealed class WifiConnection
        {
            public string Ssid { get; set; }
            public int Signal { get; set; }
        }

        public static WifiConnection CurrentWifi()
        {
            IntPtr handle = IntPtr.Zero;
            IntPtr list = IntPtr.Zero;
            try
            {
                if (WlanOpenHandle(2, IntPtr.Zero, out _, out handle) != 0) return null;
                if (WlanEnumInterfaces(handle, IntPtr.Zero, out list) != 0) return null;

                int count = Marshal.ReadInt32(list, 0);
                for (int index = 0; index < count; index++)
                {
                    IntPtr item = list + 8 + index * 532;
                    int interfaceState = Marshal.ReadInt32(item, 528);
                    if (interfaceState != 1) continue;

                    var guid = Marshal.PtrToStructure<Guid>(item);
                    if (WlanQueryInterface(handle, ref guid, 7, IntPtr.Zero, out _, out IntPtr data, out _) != 0) continue;
                    try
                    {
                        int ssidLength = Math.Min(32, Marshal.ReadInt32(data, 520));
                        var ssid = new byte[ssidLength];
                        Marshal.Copy(data + 524, ssid, 0, ssidLength);
                        return new WifiConnection
                        {
                            Ssid = Encoding.UTF8.GetString(ssid),
                            Signal = Math.Max(0, Math.Min(100, Marshal.ReadInt32(data, 576))),
                        };
                    }
                    finally
                    {
                        WlanFreeMemory(data);
                    }
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (list != IntPtr.Zero) WlanFreeMemory(list);
                if (handle != IntPtr.Zero) WlanCloseHandle(handle, IntPtr.Zero);
            }
        }

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject
        {
        }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid interfaceId, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object result);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr notify);
            int UnregisterControlChangeNotify(IntPtr notify);
            int GetChannelCount(out uint count);
            int SetMasterVolumeLevel(float levelDb, ref Guid eventContext);
            int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            int GetMasterVolumeLevel(out float levelDb);
            int GetMasterVolumeLevelScalar(out float level);
            int SetChannelVolumeLevel(uint channel, float levelDb, ref Guid eventContext);
            int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid eventContext);
            int GetChannelVolumeLevel(uint channel, out float levelDb);
            int GetChannelVolumeLevelScalar(uint channel, out float level);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        public sealed class VolumeState
        {
            public double Level { get; set; }
            public bool Muted { get; set; }
        }

        private static IAudioEndpointVolume EndpointVolume()
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice device) != 0 || device == null) return null;
                try
                {
                    Guid id = typeof(IAudioEndpointVolume).GUID;
                    if (device.Activate(ref id, 23, IntPtr.Zero, out object result) != 0) return null;
                    return result as IAudioEndpointVolume;
                }
                finally
                {
                    Marshal.ReleaseComObject(device);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }

        public static VolumeState ChangeVolume(double? delta, double? level, bool? mute)
        {
            IAudioEndpointVolume volume = null;
            try
            {
                volume = EndpointVolume();
                if (volume == null) return null;
                Guid context = Guid.Empty;

                volume.GetMasterVolumeLevelScalar(out float current);
                if (level.HasValue || delta.HasValue)
                {
                    double next = level ?? Math.Round((current + delta.Value) * 20) / 20;
                    next = Math.Max(0, Math.Min(1, next));
                    volume.SetMasterVolumeLevelScalar((float)next, ref context);
                    if (next > 0 && !mute.HasValue) volume.SetMute(false, ref context);
                }
                if (mute.HasValue) volume.SetMute(mute.Value, ref context);

                volume.GetMasterVolumeLevelScalar(out float result);
                volume.GetMute(out bool muted);
                return new VolumeState { Level = Math.Round(result, 3), Muted = muted };
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (volume != null) Marshal.ReleaseComObject(volume);
            }
        }
    }
}
