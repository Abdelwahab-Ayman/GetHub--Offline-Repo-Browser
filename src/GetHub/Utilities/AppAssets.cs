using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GetHub.Utilities
{
    // Loads the bundled GetHub artwork from embedded resources so the window icon and the
    // in-app logo look the same no matter where the executable is started from.
    public static class AppAssets
    {
        private const string IconResource = "GetHub.Resources.GetHub.ico";
        private const string LogoResource = "GetHub.Resources.GetHub.png";

        private static Icon _appIcon;
        private static Bitmap _logo;

        public static Icon AppIcon
        {
            get
            {
                if (_appIcon == null)
                {
                    _appIcon = LoadIcon();
                }

                return _appIcon;
            }
        }

        public static Bitmap Logo
        {
            get
            {
                if (_logo == null)
                {
                    _logo = LoadLogo();
                }

                return _logo;
            }
        }

        private static Icon LoadIcon()
        {
            try
            {
                using (Stream stream = OpenResource(IconResource))
                {
                    if (stream != null)
                    {
                        return new Icon(stream, new Size(32, 32));
                    }
                }
            }
            catch
            {
            }

            string[] candidates = new string[]
            {
                Path.Combine(Application.StartupPath, "GetHub.ico"),
                Path.Combine(Application.StartupPath, "Resources", "GetHub.ico")
            };

            foreach (string path in candidates)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        return new Icon(path);
                    }
                }
                catch
                {
                }
            }

            return SystemIcons.Application;
        }

        private static Bitmap LoadLogo()
        {
            try
            {
                using (Stream stream = OpenResource(LogoResource))
                {
                    if (stream != null)
                    {
                        using (Image image = Image.FromStream(stream))
                        {
                            return new Bitmap(image);
                        }
                    }
                }
            }
            catch
            {
            }

            string[] candidates = new string[]
            {
                Path.Combine(Application.StartupPath, "GetHub.png"),
                Path.Combine(Application.StartupPath, "Resources", "GetHub.png"),
                Path.Combine(Application.StartupPath, "Resources", "GetHub_logo.jpg")
            };

            foreach (string path in candidates)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        using (Image image = Image.FromFile(path))
                        {
                            return new Bitmap(image);
                        }
                    }
                }
                catch
                {
                }
            }

            try
            {
                using (Icon icon = (Icon)AppIcon.Clone())
                {
                    return icon.ToBitmap();
                }
            }
            catch
            {
            }

            return null;
        }

        private static Stream OpenResource(string name)
        {
            Assembly assembly = typeof(AppAssets).Assembly;
            return assembly.GetManifestResourceStream(name);
        }
    }
}
