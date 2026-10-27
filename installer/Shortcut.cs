using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GetHub.Installer
{
    // Creates and removes .lnk files through the shell's own WScript.Shell
    // object. Late binding keeps the installer free of COM interop assemblies,
    // which matches the rest of the project's no-dependency build.
    internal static class Shortcut
    {
        public static string DesktopPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                InstallerInfo.ProductName + ".lnk");
        }

        public static string StartMenuPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                InstallerInfo.ProductName + ".lnk");
        }

        public static void Create(
            string shortcutPath,
            string targetPath,
            string workingDirectory,
            string description)
        {
            if (string.IsNullOrEmpty(shortcutPath))
            {
                throw new ArgumentException("Shortcut path is missing.", "shortcutPath");
            }

            string parent = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                throw new InvalidOperationException("The Windows shell is not available.");
            }

            object shell = null;
            object link = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                link = shellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shell,
                    new object[] { shortcutPath });

                Type linkType = link.GetType();
                SetProperty(linkType, link, "TargetPath", targetPath);
                SetProperty(linkType, link, "WorkingDirectory", workingDirectory);
                SetProperty(linkType, link, "Description", description);
                SetProperty(linkType, link, "IconLocation", targetPath + ",0");
                linkType.InvokeMember("Save", BindingFlags.InvokeMethod, null, link, null);
            }
            finally
            {
                Release(link);
                Release(shell);
            }
        }

        public static void Remove(string shortcutPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(shortcutPath) && File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
            catch
            {
                // A shortcut that cannot be deleted is not worth failing an uninstall.
            }
        }

        private static void SetProperty(Type type, object target, string name, object value)
        {
            type.InvokeMember(
                name,
                BindingFlags.SetProperty,
                null,
                target,
                new object[] { value });
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                try
                {
                    Marshal.FinalReleaseComObject(value);
                }
                catch
                {
                }
            }
        }
    }
}
