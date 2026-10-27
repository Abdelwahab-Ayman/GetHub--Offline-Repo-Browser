using System;
using System.Threading;
using System.Windows.Forms;

namespace GetHub.Installer
{
    internal sealed class SetupOptions
    {
        public bool Silent { get; set; }
        public string Directory { get; set; }
        public bool DesktopShortcut { get; set; }
        public bool StartMenuShortcut { get; set; }
        public bool Launch { get; set; }
        public bool LaunchSpecified { get; set; }

        public SetupOptions()
        {
            Directory = string.Empty;
            DesktopShortcut = true;
            StartMenuShortcut = true;
            Launch = true;
        }

        // Supports the usual installer switches, for example:
        //   GetHubSetup.exe /silent /dir="D:\Apps\GetHub" /nodesktop /nolaunch
        public static SetupOptions Parse(string[] args)
        {
            SetupOptions options = new SetupOptions();
            if (args == null)
            {
                return options;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string raw = args[i] == null ? string.Empty : args[i].Trim();
                string value = raw.TrimStart('/', '-');
                if (value.Length == 0)
                {
                    continue;
                }

                string key = value.ToLowerInvariant();
                if (key == "silent" || key == "quiet" || key == "s")
                {
                    options.Silent = true;
                }
                else if (key == "nodesktop")
                {
                    options.DesktopShortcut = false;
                }
                else if (key == "nostartmenu")
                {
                    options.StartMenuShortcut = false;
                }
                else if (key == "nolaunch")
                {
                    options.Launch = false;
                    options.LaunchSpecified = true;
                }
                else if (key == "launch")
                {
                    options.Launch = true;
                    options.LaunchSpecified = true;
                }
                else if (key.StartsWith("dir=", StringComparison.Ordinal))
                {
                    options.Directory = value.Substring(4).Trim('"');
                }
                else if (key == "dir" && i + 1 < args.Length)
                {
                    options.Directory = (args[++i] ?? string.Empty).Trim('"');
                }
                else if (key.StartsWith("desktop=", StringComparison.Ordinal))
                {
                    options.DesktopShortcut = ReadBoolean(value.Substring(8), true);
                }
                else if (key.StartsWith("startmenu=", StringComparison.Ordinal))
                {
                    options.StartMenuShortcut = ReadBoolean(value.Substring(10), true);
                }
                else if (key.StartsWith("launch=", StringComparison.Ordinal))
                {
                    options.Launch = ReadBoolean(value.Substring(7), true);
                    options.LaunchSpecified = true;
                }
            }

            return options;
        }

        private static bool ReadBoolean(string text, bool fallback)
        {
            string value = (text ?? string.Empty).Trim().Trim('"').ToLowerInvariant();
            if (value == "0" || value == "false" || value == "no" || value == "off")
            {
                return false;
            }

            if (value == "1" || value == "true" || value == "yes" || value == "on")
            {
                return true;
            }

            return fallback;
        }
    }

    internal static class SetupProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            SetupOptions options = SetupOptions.Parse(args);
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\GetHubSetup", out createdNew))
            {
                if (!createdNew)
                {
                    if (!options.Silent)
                    {
                        MessageBox.Show(
                            "GetHub Setup is already running.",
                            "GetHub Setup",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }

                    return 1;
                }

                try
                {
                    if (options.Silent)
                    {
                        return RunSilent(options);
                    }

                    using (SetupForm form = new SetupForm())
                    {
                        Application.Run(form);
                        return form.ExitCode;
                    }
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        private static int RunSilent(SetupOptions options)
        {
            try
            {
                string directory = InstallerInfo.Normalize(options.Directory);
                if (directory.Length == 0)
                {
                    string existing = InstallerInfo.FindInstalledDirectory();
                    directory = existing ?? InstallerInfo.DefaultInstallDirectory;
                }

                if (!RunningApp.CloseAll(null, false))
                {
                    Console.Error.WriteLine("error: GetHub is still running.");
                    return 1;
                }

                InstallOptions install = new InstallOptions();
                install.Directory = directory;
                install.DesktopShortcut = options.DesktopShortcut;
                install.StartMenuShortcut = options.StartMenuShortcut;

                InstallResult result = InstallEngine.Install(install);
                foreach (string warning in result.Warnings)
                {
                    Console.WriteLine("warning: " + warning);
                }

                Console.WriteLine("installed: " + result.InstallDirectory);
                if (options.LaunchSpecified && options.Launch)
                {
                    InstallEngine.Launch(result.InstallDirectory);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return 1;
            }
        }
    }
}
