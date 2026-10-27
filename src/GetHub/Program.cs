using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using GetHub.Forms;
using GetHub.Models;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ConfigureBrowserEmulation();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += ApplicationThreadException;
            AppDomain.CurrentDomain.UnhandledException += AppDomainUnhandledException;

            RepoStore store;
            try
            {
                store = new RepoStore();
                try
                {
                    AppSettings settings = store.LoadSettings();
                    if (settings != null && string.Equals(settings.Theme, "Light", StringComparison.OrdinalIgnoreCase))
                    {
                        UiTheme.CurrentTheme = AppTheme.Light;
                    }
                    else
                    {
                        UiTheme.CurrentTheme = AppTheme.Dark;
                    }
                }
                catch
                {
                    UiTheme.CurrentTheme = AppTheme.Dark;
                }

                store.CleanTemporaryData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "GetHub could not start.\n\n" + ex.Message,
                    "GetHub",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            try
            {
                Application.Run(new MainForm(store));
            }
            finally
            {
                store.CleanTemporaryData();
            }
        }

        private static void ConfigureBrowserEmulation()
        {
            try
            {
                string exeName = Path.GetFileName(Application.ExecutablePath);
                if (string.IsNullOrEmpty(exeName))
                {
                    return;
                }

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                {
                    if (key != null)
                    {
                        key.SetValue(exeName, 11001, RegistryValueKind.DWord);
                    }
                }
            }
            catch
            {
                // Internet Explorer emulation is a nicety; the browser still works without it.
            }
        }

        private static void ApplicationThreadException(object sender, ThreadExceptionEventArgs e)
        {
            ShowUnexpectedError(e.Exception);
        }

        private static void AppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowUnexpectedError(e.ExceptionObject as Exception);
        }

        private static void ShowUnexpectedError(Exception exception)
        {
            string message = exception == null ? "An unknown error occurred." : exception.Message;
            MessageBox.Show(
                "GetHub ran into an unexpected problem.\n\n" + message,
                "GetHub",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
