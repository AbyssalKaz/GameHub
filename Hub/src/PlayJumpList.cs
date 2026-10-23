using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace GameHub
{
    internal sealed class PlayJumpList : HubModule
    {
        private const int MaxGames = 8;
        private string lastSignature;

        public override int Order => 80;

        public override void Start()
        {
            PlayTracker tracker = Hub.Module<PlayTracker>();
            if (tracker != null) tracker.SessionEnded += session => Hub.RunOnUi(Update);
        }

        public override void OnLibraryLoaded(LibraryDocument library) => Hub.RunOnUi(Update);

        public override void AfterLaunch(GameEntry game, LaunchRequest request, System.Diagnostics.Process process) => Hub.RunOnUi(Update);

        private void Update()
        {
            PlayWindow window = Hub.Module<PlayWindow>();
            List<GameEntry> games = window?.RecentGames(MaxGames) ?? new List<GameEntry>();
            string signature = string.Join("|", games.Select(game => game.Id + ":" + game.Name));
            if (signature == lastSignature) return;
            try
            {
                Commit(games);
                lastSignature = signature;
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.WriteLine("PlayJumpList: " + error.Message);
            }
        }

        private static void Commit(List<GameEntry> games)
        {
            string exe = Application.ExecutablePath;
            var list = (ICustomDestinationList)new DestinationList();
            Guid arrayId = typeof(IObjectArray).GUID;
            list.BeginList(out uint slots, ref arrayId, out object removed);
            try
            {
                var removedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (removed is IObjectArray removedItems)
                {
                    Guid linkId = typeof(IShellLinkW).GUID;
                    removedItems.GetCount(out uint count);
                    for (uint index = 0; index < count; index++)
                    {
                        removedItems.GetAt(index, ref linkId, out object item);
                        if (item is IShellLinkW link)
                        {
                            var arguments = new StringBuilder(1024);
                            link.GetArguments(arguments, arguments.Capacity);
                            Match match = Regex.Match(arguments.ToString(), "--launch\\s+\"([^\"]+)\"");
                            if (match.Success) removedIds.Add(match.Groups[1].Value);
                        }
                    }
                }

                int room = (int)Math.Max(1, Math.Min(MaxGames, slots == 0 ? MaxGames : slots - 1));
                List<IShellLinkW> gameLinks = games
                    .Where(game => !removedIds.Contains(game.Id))
                    .Take(room)
                    .Select(game => CreateLink(exe, $"--launch \"{game.Id}\"", game.Name, IconFor(game, exe), $"Play {game.Name}"))
                    .ToList();

                var tasks = (IObjectCollection)new EnumerableObjectCollection();
                tasks.AddObject(CreateLink(exe, "--home", "Home screen", (exe, 0), "Open the home screen"));

                if (gameLinks.Count > 0)
                {
                    var category = (IObjectCollection)new EnumerableObjectCollection();
                    foreach (IShellLinkW link in gameLinks) category.AddObject(link);
                    int result = list.AppendCategory("Recent games", (IObjectArray)category);
                    if (result < 0)
                    {
                        foreach (IShellLinkW link in gameLinks) tasks.AddObject(link);
                    }
                }

                list.AddUserTasks((IObjectArray)tasks);
                list.CommitList();
            }
            catch
            {
                list.AbortList();
                throw;
            }
        }

        private static (string Path, int Index) IconFor(GameEntry game, string fallback)
        {
            foreach (string candidate in new[] { game.Icon, game.Exe })
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                string path = candidate;
                int index = 0;
                Match match = Regex.Match(candidate, "^(.*),\\s*(-?\\d+)$");
                if (match.Success)
                {
                    path = match.Groups[1].Value;
                    index = int.Parse(match.Groups[2].Value);
                }
                path = path.Trim('"');
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (!File.Exists(path)) continue;
                if (extension == ".ico") return (path, index);
                if ((extension == ".exe" || extension == ".dll") && !PlayMatcher.IsIgnoredExe(Path.GetFileName(path))) return (path, index);
            }
            return (fallback, 0);
        }

        private static IShellLinkW CreateLink(string exe, string arguments, string title, (string Path, int Index) icon, string description)
        {
            var link = (IShellLinkW)new ShellLink();
            link.SetPath(exe);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(Path.GetDirectoryName(exe));
            link.SetIconLocation(icon.Path, icon.Index);
            link.SetDescription(description);

            var store = (IPropertyStore)link;
            var key = new PropertyKey(new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), 2);
            var value = new PropVariant(title);
            try
            {
                store.SetValue(ref key, ref value);
                store.Commit();
            }
            finally
            {
                value.Clear();
            }
            return link;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid FormatId;
            public uint PropertyId;

            public PropertyKey(Guid formatId, uint propertyId)
            {
                FormatId = formatId;
                PropertyId = propertyId;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct PropVariant
        {
            [FieldOffset(0)]
            private ushort valueType;

            [FieldOffset(8)]
            private IntPtr pointer;

            public PropVariant(string text)
            {
                valueType = 31;
                pointer = Marshal.StringToCoTaskMemUni(text);
            }

            public void Clear() => PropVariantClear(ref this);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PropVariant value);

        [ComImport, Guid("77f10cf0-3db5-4966-b520-b7c54fd35ed6"), ClassInterface(ClassInterfaceType.None)]
        private class DestinationList
        {
        }

        [ComImport, Guid("2d3468c1-36a7-43b6-ac24-d3f02fd9607a"), ClassInterface(ClassInterfaceType.None)]
        private class EnumerableObjectCollection
        {
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
        private class ShellLink
        {
        }

        [ComImport, Guid("6332debf-87b5-4670-90c0-5e57b408a49e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICustomDestinationList
        {
            void SetAppID([MarshalAs(UnmanagedType.LPWStr)] string appId);

            void BeginList(out uint minSlots, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object removed);

            [PreserveSig]
            int AppendCategory([MarshalAs(UnmanagedType.LPWStr)] string category, [MarshalAs(UnmanagedType.Interface)] IObjectArray items);

            void AppendKnownCategory(int category);

            void AddUserTasks([MarshalAs(UnmanagedType.Interface)] IObjectArray tasks);

            void CommitList();

            void GetRemovedDestinations(ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object removed);

            void DeleteList([MarshalAs(UnmanagedType.LPWStr)] string appId);

            void AbortList();
        }

        [ComImport, Guid("92ca9dcd-5622-4bba-a805-5e9f541bd8c9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IObjectArray
        {
            void GetCount(out uint count);

            void GetAt(uint index, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object item);
        }

        [ComImport, Guid("5632b1a4-e38a-400a-928a-d4cd63230295"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IObjectCollection
        {
            void GetCount(out uint count);

            void GetAt(uint index, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object item);

            void AddObject([MarshalAs(UnmanagedType.Interface)] object item);

            void AddFromArray([MarshalAs(UnmanagedType.Interface)] IObjectArray items);

            void RemoveObjectAt(uint index);

            void Clear();
        }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);

            void GetIDList(out IntPtr idList);

            void SetIDList(IntPtr idList);

            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);

            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);

            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxPath);

            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

            void GetHotkey(out short hotkey);

            void SetHotkey(short hotkey);

            void GetShowCmd(out int showCommand);

            void SetShowCmd(int showCommand);

            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);

            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);

            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);

            void Resolve(IntPtr window, uint flags);

            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
        }

        [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint count);

            void GetAt(uint index, out PropertyKey key);

            void GetValue(ref PropertyKey key, out PropVariant value);

            void SetValue(ref PropertyKey key, ref PropVariant value);

            void Commit();
        }
    }
}
