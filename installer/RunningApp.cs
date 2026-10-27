using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace GetHub.Installer
{
    // Setup cannot replace GetHub.exe while the app is open. This closes the
    // running copy, asking first when there is a user to ask.
    internal static class RunningApp
    {
        public static bool CloseAll(Control owner, bool interactive)
        {
            Process[] processes = Process.GetProcessesByName(
                System.IO.Path.GetFileNameWithoutExtension(InstallerInfo.AppFileName));
            if (processes == null || processes.Length == 0)
            {
                return true;
            }

            if (interactive)
            {
                DialogResult answer = MessageBox.Show(
                    owner,
                    "GetHub is running. It must be closed before setup can continue.\n\nClose GetHub now?",
                    "GetHub Setup",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    return false;
                }
            }

            foreach (Process process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.CloseMainWindow();
                    }
                }
                catch
                {
                }
            }

            // Give the app a moment to shut down cleanly before forcing it.
            for (int attempt = 0; attempt < 25; attempt++)
            {
                if (AllExited(processes))
                {
                    return true;
                }

                Thread.Sleep(200);
            }

            foreach (Process process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                }
                catch
                {
                }
            }

            return AllExited(processes);
        }

        private static bool AllExited(Process[] processes)
        {
            foreach (Process process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        return false;
                    }
                }
                catch
                {
                    // A process we cannot query is treated as gone.
                }
            }

            return true;
        }
    }
}
