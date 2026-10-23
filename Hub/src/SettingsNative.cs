using System;
using System.IO;
using System.Runtime.InteropServices;

namespace GameHub
{
    internal static class SettingsNative
    {
        private const uint PickFolders = 0x20;
        private const uint ForceFileSystem = 0x40;
        private const uint PathMustExist = 0x800;
        private const uint FileSystemPath = 0x80058000;
        private const int Cancelled = unchecked((int)0x800704C7);

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr context, ref Guid handler, ref Guid interfaceId, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint kind, [MarshalAs(UnmanagedType.LPWStr)] out string name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem item);
            void SetFolder(IShellItem item);
            void GetFolder(out IShellItem item);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(string path, IntPtr context, [In] ref Guid interfaceId, out IShellItem item);

        public static string PickFolder(IntPtr owner, string title, string initial)
        {
            var dialog = (IFileDialog)new FileOpenDialog();
            try
            {
                dialog.GetOptions(out uint options);
                dialog.SetOptions(options | PickFolders | ForceFileSystem | PathMustExist);
                if (!string.IsNullOrEmpty(title)) dialog.SetTitle(title);
                if (!string.IsNullOrEmpty(initial) && Directory.Exists(initial))
                {
                    try
                    {
                        Guid shellItemId = typeof(IShellItem).GUID;
                        SHCreateItemFromParsingName(initial, IntPtr.Zero, ref shellItemId, out IShellItem folder);
                        dialog.SetFolder(folder);
                    }
                    catch
                    {
                    }
                }

                int result = dialog.Show(owner);
                if (result == Cancelled || result != 0) return null;
                dialog.GetResult(out IShellItem item);
                item.GetDisplayName(FileSystemPath, out string path);
                return path;
            }
            finally
            {
                Marshal.FinalReleaseComObject(dialog);
            }
        }
    }
}
