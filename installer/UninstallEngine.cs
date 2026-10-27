using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GetHub.Installer
{
    internal sealed class UninstallResult
    {
        public bool Removed { get; set; }
        public List<string> Warnings { get; private set; }

        public UninstallResult()
        {
            Warnings = new List<string>();
        }
    }

    // Removes the program files, shortcuts and Apps & Features entry. Settings and
    // downloaded repositories are kept unless the caller asks for them to go too.
    internal static class UninstallEngine
    {
        public static UninstallResult Run(string directory, bool removeData, Control owner, bool interactive)
        {
            UninstallResult result = new UninstallResult();
            string error;
            if (!InstallerInfo.ValidateDirectory(directory, out error))
            {
                result.Warnings.Add(error);
                return result;
            }

            string installDir = InstallerInfo.Normalize(directory);
            string appPath = InstallerInfo.AppPath(installDir);
            string uninstallPath = InstallerInfo.UninstallPath(installDir);
            if (!File.Exists(appPath) && !File.Exists(uninstallPath))
            {
                result.Warnings.Add("GetHub was not found in that folder.");
                return result;
            }

            if (!RunningApp.CloseAll(owner, interactive))
            {
                return result;
            }

            InstallManifest manifest = InstallManifest.Load(InstallerInfo.ManifestPath(installDir));
            RemoveShortcuts(manifest);
            RemoveRegistration();
            RemoveBrowserEmulation();

            if (removeData)
            {
                TryDeleteDirectory(InstallerInfo.DataDirectory, result.Warnings);
            }

            DeleteProgramFiles(installDir);
            ScheduleDirectoryRemoval(installDir, result.Warnings);

            result.Removed = true;
            return result;
        }

        public static bool Exists(string directory)
        {
            string error;
            if (!InstallerInfo.ValidateDirectory(directory, out error))
            {
                return false;
            }

            return File.Exists(InstallerInfo.AppPath(InstallerInfo.Normalize(directory)))
                || File.Exists(InstallerInfo.UninstallPath(InstallerInfo.Normalize(directory)));
        }

        private static void RemoveShortcuts(InstallManifest manifest)
        {
            if (manifest != null && manifest.Shortcuts.Count > 0)
            {
                foreach (string shortcut in manifest.Shortcuts)
                {
                    Shortcut.Remove(shortcut);
                }

                return;
            }

            // An older or hand-copied install may have no manifest. Remove the
            // links setup would have created, without touching anything else.
            Shortcut.Remove(Shortcut.DesktopPath());
            Shortcut.Remove(Shortcut.StartMenuPath());
        }

        private static void RemoveRegistration()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(InstallerInfo.UninstallRegistryPath, false);
            }
            catch
            {
            }
        }

        private static void RemoveBrowserEmulation()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    InstallerInfo.BrowserEmulationPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(InstallerInfo.AppFileName, false);
                    }
                }
            }
            catch
            {
            }
        }

        private static void DeleteProgramFiles(string installDir)
        {
            string self = Path.GetFullPath(Application.ExecutablePath);
            try
            {
                foreach (string file in Directory.GetFiles(installDir))
                {
                    try
                    {
                        if (!string.Equals(Path.GetFullPath(file), self, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }

                foreach (string directory in Directory.GetDirectories(installDir))
                {
                    TryDeleteDirectory(directory, null);
                }
            }
            catch
            {
            }
        }

        // Uninstall.exe cannot delete itself while it is running. A detached shell
        // waits a moment, then removes the folder and everything still in it.
        private static void ScheduleDirectoryRemoval(string installDir, List<string> warnings)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = "cmd.exe";
                start.Arguments = "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + installDir + "\"";
                start.CreateNoWindow = true;
                start.UseShellExecute = false;
                start.WindowStyle = ProcessWindowStyle.Hidden;
                start.WorkingDirectory = Path.GetTempPath();
                Process.Start(start);
            }
            catch (Exception ex)
            {
                if (warnings != null)
                {
                    warnings.Add(
                        "The install folder could not be removed automatically (" + ex.Message +
                        "). You can delete it by hand after closing this window.");
                }
            }
        }

        private static void TryDeleteDirectory(string path, List<string> warnings)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                if (warnings != null)
                {
                    warnings.Add("Some data in " + path + " could not be removed: " + ex.Message);
                }
            }
        }
    }
}
