using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GetHub.Installer
{
    // A small line-based record of what setup wrote, so Uninstall.exe knows which
    // shortcuts to remove without guessing. The format is deliberately plain:
    //
    //   version=1.0.0
    //   dir=C:\Users\me\AppData\Local\Programs\GetHub
    //   file=GetHub.exe
    //   shortcut=C:\Users\me\Desktop\GetHub.lnk
    internal sealed class InstallManifest
    {
        public string Version { get; set; }
        public string Directory { get; set; }
        public List<string> Files { get; private set; }
        public List<string> Shortcuts { get; private set; }

        public InstallManifest()
        {
            Version = string.Empty;
            Directory = string.Empty;
            Files = new List<string>();
            Shortcuts = new List<string>();
        }

        public static InstallManifest Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            InstallManifest manifest = new InstallManifest();
            foreach (string line in File.ReadAllLines(path))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separator).Trim().ToLowerInvariant();
                string value = line.Substring(separator + 1).Trim();
                if (value.Length == 0)
                {
                    continue;
                }

                if (key == "version")
                {
                    manifest.Version = value;
                }
                else if (key == "dir")
                {
                    manifest.Directory = value;
                }
                else if (key == "file")
                {
                    manifest.Files.Add(value);
                }
                else if (key == "shortcut")
                {
                    manifest.Shortcuts.Add(value);
                }
            }

            return manifest;
        }

        public void Save(string path)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("version=").AppendLine(Version);
            builder.Append("dir=").AppendLine(Directory);
            foreach (string file in Files)
            {
                builder.Append("file=").AppendLine(file);
            }

            foreach (string shortcut in Shortcuts)
            {
                builder.Append("shortcut=").AppendLine(shortcut);
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        public bool HasShortcutNamed(string fileName)
        {
            foreach (string shortcut in Shortcuts)
            {
                if (string.Equals(Path.GetFileName(shortcut), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
