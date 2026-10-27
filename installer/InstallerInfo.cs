using System;
using System.IO;
using Microsoft.Win32;

namespace GetHub.Installer
{
    // Constants and paths shared by GetHubSetup.exe and Uninstall.exe. The two
    // executables are compiled separately, so the product details live here.
    internal static class InstallerInfo
    {
        public const string ProductName = "GetHub";
        public const string Version = "1.0.0";
        public const string Publisher = "GetHub";
        public const string AppFileName = "GetHub.exe";
        public const string ConfigFileName = "GetHub.exe.config";
        public const string UninstallFileName = "Uninstall.exe";
        public const string ManifestFileName = "install.txt";
        public const string PayloadResource = "GetHub.Installer.payload.zip";
        public const string LogoResource = "GetHub.Installer.Logo.png";
        public const string UninstallRegistryPath =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\GetHub";
        public const string BrowserEmulationPath =
            @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";

        public static string DefaultInstallDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs",
                    ProductName);
            }
        }

        // GetHub keeps settings, render output and downloaded repositories under
        // %LocalAppData%\GetHub. Uninstalling keeps that folder unless asked.
        public static string DataDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    ProductName);
            }
        }

        public static string AppPath(string directory)
        {
            return Path.Combine(directory, AppFileName);
        }

        public static string UninstallPath(string directory)
        {
            return Path.Combine(directory, UninstallFileName);
        }

        public static string ManifestPath(string directory)
        {
            return Path.Combine(directory, ManifestFileName);
        }

        // Returns the folder of an existing install, or null when GetHub has not
        // been installed for this account yet. The registry entry is authoritative
        // because the user may have picked a folder other than the default.
        public static string FindInstalledDirectory()
        {
            string directory = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallRegistryPath))
                {
                    if (key != null)
                    {
                        directory = key.GetValue("InstallLocation") as string;
                    }
                }
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(directory) && File.Exists(AppPath(directory)))
            {
                return directory;
            }

            string fallback = DefaultInstallDirectory;
            return File.Exists(AppPath(fallback)) ? fallback : null;
        }

        public static string Normalize(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return string.Empty;
            }

            string trimmed = directory.Trim().Trim('"');
            try
            {
                string full = Path.GetFullPath(trimmed);
                if (full.Length > 3)
                {
                    full = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                return full;
            }
            catch
            {
                return trimmed;
            }
        }

        // Keeps setup out of drive roots, the Windows folder and GetHub's own data
        // folder. Writing to Program Files is allowed but will fail without
        // elevation, which the install step reports with a friendly message.
        public static bool ValidateDirectory(string directory, out string error)
        {
            error = string.Empty;
            string full = Normalize(directory);
            if (full.Length == 0)
            {
                error = "Choose a folder for GetHub.";
                return false;
            }

            string root;
            try
            {
                root = Path.GetPathRoot(full);
            }
            catch
            {
                error = "That folder path is not valid.";
                return false;
            }

            if (string.IsNullOrEmpty(root) || !Path.IsPathRooted(full))
            {
                error = "Choose a full folder path, such as the default location.";
                return false;
            }

            if (string.Equals(full.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            {
                error = "Choose a folder other than the root of a drive.";
                return false;
            }

            string data = Normalize(DataDirectory);
            if (string.Equals(full, data, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(data + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                error = "GetHub keeps your downloads in that folder. Choose a different one.";
                return false;
            }

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windows)
                && full.StartsWith(windows + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                error = "Windows system folders cannot be used.";
                return false;
            }

            return true;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes.ToString() + " bytes";
            }

            if (bytes < 1024 * 1024)
            {
                return (bytes / 1024.0).ToString("0") + " KB";
            }

            return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
        }
    }
}
