using System;
using System.Threading;
using System.Windows.Forms;

namespace GetHub.Installer
{
    internal static class UninstallerProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool quiet = HasFlag(args, "quiet") || HasFlag(args, "silent") || HasFlag(args, "s");
            bool removeData = HasFlag(args, "removedata");
            string directory = AppDomain.CurrentDomain.BaseDirectory;

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Local\\GetHubUninstall", out createdNew))
            {
                if (!createdNew)
                {
                    return 1;
                }

                try
                {
                    if (quiet)
                    {
                        UninstallResult result = UninstallEngine.Run(directory, removeData, null, false);
                        if (!result.Removed)
                        {
                            foreach (string warning in result.Warnings)
                            {
                                Console.Error.WriteLine("error: " + warning);
                            }

                            return 1;
                        }

                        return 0;
                    }

                    using (UninstallForm form = new UninstallForm(directory))
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

        private static bool HasFlag(string[] args, string name)
        {
            if (args == null)
            {
                return false;
            }

            foreach (string arg in args)
            {
                if (arg == null)
                {
                    continue;
                }

                string value = arg.Trim().TrimStart('/', '-').ToLowerInvariant();
                if (value == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
