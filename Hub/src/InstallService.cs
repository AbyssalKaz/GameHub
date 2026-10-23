using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GameHub
{
    internal static class InstallService
    {
        public const string UninstallArgument = "--uninstall-self";
        private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GameHub";
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static string ExePath => Application.ExecutablePath;

        public static string ShortcutPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Game Hub.lnk");

        public static bool HasShortcut => File.Exists(ShortcutPath);

        public static bool IsRegistered
        {
            get
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
                return key != null;
            }
        }

        public static object Status() => new
        {
            installed = HasShortcut && IsRegistered,
            shortcut = HasShortcut,
            registered = IsRegistered,
            shortcutPath = ShortcutPath,
            exe = ExePath,
        };

        public static bool HandleCommandLine(string[] arguments)
        {
            if (!arguments.Skip(1).Any(argument => string.Equals(argument, UninstallArgument, StringComparison.OrdinalIgnoreCase))) return false;

            Application.EnableVisualStyles();
            DialogResult answer = MessageBox.Show(
                "Remove Game Hub from the Start Menu and from Installed apps?\n\nThe app files and your library data stay where they are.",
                "Uninstall Game Hub",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return true;

            try
            {
                UninstallAsync(IntPtr.Zero).GetAwaiter().GetResult();
                MessageBox.Show("Game Hub was removed from the Start Menu and Installed apps.", "Uninstall Game Hub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            {
                DiagLog.Write("install", error.ToString());
                MessageBox.Show("Game Hub could not be fully removed: " + error.Message, "Uninstall Game Hub", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return true;
        }

        public static void Install()
        {
            CreateShortcut();
            Register();
        }

        public static async Task UninstallAsync(IntPtr owner)
        {
            if (HasShortcut) await UninstallService.RecycleAsync(ShortcutPath, owner);
            RemoveRunEntries();
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false);
        }

        private static void CreateShortcut()
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is not available.");
            object shell = Activator.CreateInstance(shellType);
            object link = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath));
                link = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { ShortcutPath });
                Type linkType = link.GetType();
                void Set(string name, object value) => linkType.InvokeMember(name, BindingFlags.SetProperty, null, link, new[] { value });
                Set("TargetPath", ExePath);
                Set("WorkingDirectory", Path.GetDirectoryName(ExePath));
                Set("IconLocation", ExePath + ",0");
                Set("Description", "Game Hub");
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally
            {
                if (link != null) Marshal.FinalReleaseComObject(link);
                Marshal.FinalReleaseComObject(shell);
            }
        }

        private static void Register()
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKeyPath);
            string version = File.GetLastWriteTime(ExePath).ToString("yyyy.M.d");
            key.SetValue("DisplayName", "Game Hub");
            key.SetValue("DisplayIcon", ExePath + ",0");
            key.SetValue("DisplayVersion", version);
            key.SetValue("Publisher", Environment.UserName);
            key.SetValue("InstallLocation", HubPaths.HubDir);
            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("UninstallString", $"\"{ExePath}\" {UninstallArgument}");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, FolderBytes(HubPaths.AppDir) / 1024), RegistryValueKind.DWord);
        }

        private static void RemoveRunEntries()
        {
            using RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (run == null) return;
            foreach (string name in run.GetValueNames().ToList())
            {
                if (run.GetValue(name) is string command && command.IndexOf(ExePath, StringComparison.OrdinalIgnoreCase) >= 0) run.DeleteValue(name, false);
            }
        }

        private static long FolderBytes(string folder)
        {
            try
            {
                return new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(file => file.Length);
            }
            catch
            {
                return 0;
            }
        }
    }
}
