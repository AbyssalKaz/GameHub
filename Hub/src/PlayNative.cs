using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GameHub
{
    internal sealed class PlayProcessEntry
    {
        public int Id;
        public int ParentId;
        public string ExeName;
    }

    internal static class PlayNative
    {
        private const uint SnapshotProcesses = 0x00000002;
        private const uint QueryLimitedInformation = 0x1000;
        private const int AllowAnyForeground = -1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry32
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry32 entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageNameW(IntPtr process, int flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int processId);

        public static void AllowAnyForegroundWindow() => AllowSetForegroundWindow(AllowAnyForeground);

        public static List<PlayProcessEntry> ListProcesses()
        {
            var result = new List<PlayProcessEntry>(400);
            IntPtr snapshot = CreateToolhelp32Snapshot(SnapshotProcesses, 0);
            if (snapshot == IntPtr.Zero || snapshot == NativeMethods.InvalidHandle) return result;
            try
            {
                var entry = new ProcessEntry32 { Size = (uint)Marshal.SizeOf(typeof(ProcessEntry32)) };
                if (!Process32FirstW(snapshot, ref entry)) return result;
                do
                {
                    result.Add(new PlayProcessEntry { Id = (int)entry.ProcessId, ParentId = (int)entry.ParentProcessId, ExeName = entry.ExeFile });
                }
                while (Process32NextW(snapshot, ref entry));
            }
            finally
            {
                CloseHandle(snapshot);
            }
            return result;
        }

        public static bool TryGetProcessInfo(int processId, out string path, out DateTime startedUtc)
        {
            path = null;
            startedUtc = DateTime.MinValue;
            IntPtr process = OpenProcess(QueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero) return false;
            try
            {
                var builder = new StringBuilder(1024);
                int size = builder.Capacity;
                if (!QueryFullProcessImageNameW(process, 0, builder, ref size)) return false;
                path = builder.ToString(0, size);
                if (GetProcessTimes(process, out long creation, out _, out _, out _) && creation > 0)
                {
                    startedUtc = DateTime.FromFileTimeUtc(creation);
                }
                return true;
            }
            finally
            {
                CloseHandle(process);
            }
        }
    }
}
