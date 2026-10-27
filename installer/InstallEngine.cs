using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace GetHub.Installer
{
    internal sealed class InstallOptions
    {
        public string Directory { get; set; }
        public bool DesktopShortcut { get; set; }
        public bool StartMenuShortcut { get; set; }

        public InstallOptions()
        {
            Directory = string.Empty;
            DesktopShortcut = true;
            StartMenuShortcut = true;
        }
    }

    internal sealed class InstallResult
    {
        public string InstallDirectory { get; set; }
        public List<string> Warnings { get; private set; }

        public InstallResult()
        {
            InstallDirectory = string.Empty;
            Warnings = new List<string>();
        }
    }

    // Copies the embedded payload into place, writes the uninstall manifest and
    // registers GetHub in the per-user Apps & Features list. Everything here is
    // per-user, so setup never needs an administrator prompt.
    internal static class InstallEngine
    {
        public static InstallResult Install(InstallOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            string error;
            if (!InstallerInfo.ValidateDirectory(options.Directory, out error))
            {
                throw new InvalidOperationException(error);
            }

            string directory = InstallerInfo.Normalize(options.Directory);
            InstallResult result = new InstallResult();
            result.InstallDirectory = directory;

            string staging = Path.Combine(
                Path.GetTempPath(),
                "GetHubSetup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);

            try
            {
                ExtractPayload(staging);
                Directory.CreateDirectory(directory);
                CopyInto(staging, directory);

                InstallManifest manifest = new InstallManifest();
                manifest.Version = InstallerInfo.Version;
                manifest.Directory = directory;
                manifest.Files.Add(InstallerInfo.AppFileName);
                manifest.Files.Add(InstallerInfo.ConfigFileName);
                manifest.Files.Add(InstallerInfo.UninstallFileName);
                manifest.Files.Add(InstallerInfo.ManifestFileName);

                string appPath = InstallerInfo.AppPath(directory);
                if (options.DesktopShortcut)
                {
                    AddShortcut(manifest, Shortcut.DesktopPath(), appPath, directory, result);
                }

                if (options.StartMenuShortcut)
                {
                    AddShortcut(manifest, Shortcut.StartMenuPath(), appPath, directory, result);
                }

                manifest.Save(InstallerInfo.ManifestPath(directory));
                RegisterUninstall(directory);
            }
            catch (UnauthorizedAccessException)
            {
                throw new InvalidOperationException(
                    "Windows did not allow setup to write to that folder. " +
                    "Choose a folder in your user profile, such as the default location.");
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    "Setup could not write the program files: " + ex.Message);
            }
            finally
            {
                TryDeleteDirectory(staging);
            }

            return result;
        }

        public static void Launch(string directory)
        {
            try
            {
                string appPath = InstallerInfo.AppPath(directory);
                if (!File.Exists(appPath))
                {
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = appPath,
                    WorkingDirectory = directory,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Launching is a convenience; the app is installed either way.
            }
        }

        public static void OpenFolder(string directory)
        {
            try
            {
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = "\"" + directory + "\"",
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
            }
        }

        private static void ExtractPayload(string destination)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(InstallerInfo.PayloadResource))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "The setup file is damaged: its program files are missing. Download GetHubSetup.exe again.");
                }

                using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            continue;
                        }

                        string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                        if (!target.StartsWith(
                            Path.GetFullPath(destination) + Path.DirectorySeparatorChar,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("The setup file contains an invalid path.");
                        }

                        string parent = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            Directory.CreateDirectory(parent);
                        }

                        using (Stream input = entry.Open())
                        using (FileStream output = new FileStream(
                            target,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None))
                        {
                            input.CopyTo(output);
                        }
                    }
                }
            }
        }

        private static void CopyInto(string staging, string directory)
        {
            CopyFile(staging, directory, InstallerInfo.AppFileName);
            CopyFile(staging, directory, InstallerInfo.ConfigFileName);
            CopyFile(staging, directory, InstallerInfo.UninstallFileName);
        }

        private static void CopyFile(string staging, string directory, string fileName)
        {
            string source = Path.Combine(staging, fileName);
            if (!File.Exists(source))
            {
                throw new InvalidOperationException(
                    "The setup file is damaged: " + fileName + " is missing. Download GetHubSetup.exe again.");
            }

            File.Copy(source, Path.Combine(directory, fileName), true);
        }

        private static void AddShortcut(
            InstallManifest manifest,
            string shortcutPath,
            string targetPath,
            string workingDirectory,
            InstallResult result)
        {
            try
            {
                Shortcut.Create(
                    shortcutPath,
                    targetPath,
                    workingDirectory,
                    "Browse GitHub repositories offline");
                manifest.Shortcuts.Add(shortcutPath);
            }
            catch (Exception ex)
            {
                result.Warnings.Add(
                    "The " + Path.GetFileNameWithoutExtension(shortcutPath) + " shortcut could not be created: " +
                    ex.Message);
            }
        }

        private static void RegisterUninstall(string directory)
        {
            string appPath = InstallerInfo.AppPath(directory);
            string uninstallPath = InstallerInfo.UninstallPath(directory);
            int sizeKb = (int)Math.Max(1, EstimateSize(directory) / 1024);

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(InstallerInfo.UninstallRegistryPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("Windows did not allow setup to register the uninstaller.");
                }

                key.SetValue("DisplayName", InstallerInfo.ProductName);
                key.SetValue("DisplayVersion", InstallerInfo.Version);
                key.SetValue("Publisher", InstallerInfo.Publisher);
                key.SetValue("DisplayIcon", appPath + ",0");
                key.SetValue("InstallLocation", directory);
                key.SetValue("UninstallString", "\"" + uninstallPath + "\"");
                key.SetValue("QuietUninstallString", "\"" + uninstallPath + "\" /quiet");
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("Comments", "Download GitHub repositories and read them offline.");
                key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        private static long EstimateSize(string directory)
        {
            long total = 0;
            try
            {
                foreach (string file in Directory.GetFiles(directory))
                {
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return total;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }
    }
}
