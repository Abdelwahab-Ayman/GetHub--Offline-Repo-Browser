using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using GetHub.Models;

namespace GetHub.Services
{
    public class RepoStore
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public string RootPath { get; private set; }
        public string ReposPath { get; private set; }
        public string RenderPath { get; private set; }
        public string TempPath { get; private set; }
        public string SettingsPath { get; private set; }
        public AppSettings Settings { get; private set; }

        public RepoStore()
        {
            RootPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GetHub");
            ReposPath = Path.Combine(RootPath, "repos");
            RenderPath = Path.Combine(RootPath, "render");
            TempPath = Path.Combine(RootPath, "temp");
            SettingsPath = Path.Combine(RootPath, "settings.json");

            EnsureDirectories();
            Settings = LoadSettings();
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(ReposPath);
            Directory.CreateDirectory(RenderPath);
            Directory.CreateDirectory(TempPath);
        }

        public void CleanTemporaryData()
        {
            TryDeleteDirectoryContents(TempPath);
            TryDeleteDirectoryContents(RenderPath);
        }

        public List<RepoRecord> LoadAll()
        {
            List<RepoRecord> records = new List<RepoRecord>();
            if (!Directory.Exists(ReposPath))
            {
                return records;
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(ReposPath);
            }
            catch
            {
                return records;
            }

            foreach (string directory in directories)
            {
                string metadataPath = Path.Combine(directory, "repo.json");
                if (!File.Exists(metadataPath))
                {
                    continue;
                }

                try
                {
                    string json = File.ReadAllText(metadataPath, Encoding.UTF8);
                    RepoRecord record = _serializer.Deserialize<RepoRecord>(json);
                    if (record == null)
                    {
                        continue;
                    }

                    record.LocalPath = directory;
                    if (!record.HasSnapshot())
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(record.FullName))
                    {
                        record.FullName = record.Owner + "/" + record.Name;
                    }

                    records.Add(record);
                }
                catch
                {
                    // A broken cache entry should not stop the rest of the library loading.
                }
            }

            records.Sort(delegate(RepoRecord a, RepoRecord b)
            {
                return string.Compare(a.GetDisplayName(), b.GetDisplayName(), StringComparison.OrdinalIgnoreCase);
            });

            // Cache folder names can change between versions, so a re-downloaded repository
            // may exist twice on disk. Show only the most recent copy.
            Dictionary<string, RepoRecord> newest = new Dictionary<string, RepoRecord>(StringComparer.OrdinalIgnoreCase);
            foreach (RepoRecord record in records)
            {
                string key = record.GetDisplayName();
                RepoRecord existing;
                if (!newest.TryGetValue(key, out existing) ||
                    record.DownloadedAtUtc > existing.DownloadedAtUtc)
                {
                    newest[key] = record;
                }
            }

            records = new List<RepoRecord>(newest.Values);
            records.Sort(delegate(RepoRecord a, RepoRecord b)
            {
                return string.Compare(a.GetDisplayName(), b.GetDisplayName(), StringComparison.OrdinalIgnoreCase);
            });

            return records;
        }

        public void SaveRecord(RepoRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.LocalPath))
            {
                throw new ArgumentException("The repository record has no local path.");
            }

            Directory.CreateDirectory(record.LocalPath);
            string json = _serializer.Serialize(record);
            File.WriteAllText(
                Path.Combine(record.LocalPath, "repo.json"),
                json,
                new UTF8Encoding(false));
        }

        public void DeleteRecord(RepoRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.LocalPath))
            {
                return;
            }

            string fullPath = Path.GetFullPath(record.LocalPath);
            string repoRoot = Path.GetFullPath(ReposPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(repoRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to delete a folder outside the repository cache.");
            }

            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                    AppSettings settings = _serializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        if (settings.GitHubToken == null)
                        {
                            settings.GitHubToken = string.Empty;
                        }

                        if (settings.Theme == null)
                        {
                            settings.Theme = "Dark";
                        }

                        if (settings.GitHubLogin == null)
                        {
                            settings.GitHubLogin = string.Empty;
                        }

                        if (settings.GitHubName == null)
                        {
                            settings.GitHubName = string.Empty;
                        }

                        if (settings.GitHubAvatarUrl == null)
                        {
                            settings.GitHubAvatarUrl = string.Empty;
                        }

                        if (settings.GitHubHtmlUrl == null)
                        {
                            settings.GitHubHtmlUrl = string.Empty;
                        }

                        if (settings.GitHubScopes == null)
                        {
                            settings.GitHubScopes = string.Empty;
                        }

                        return settings;
                    }
                }
            }
            catch
            {
                // Fall back to defaults if settings.json is unreadable.
            }

            return new AppSettings();
        }

        public void SaveSettings()
        {
            string json = _serializer.Serialize(Settings);
            File.WriteAllText(SettingsPath, json, new UTF8Encoding(false));
        }

        public string CreateTemporaryDirectory()
        {
            string path = Path.Combine(TempPath, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static string GetSlug(string owner, string name)
        {
            string slug = ((owner ?? string.Empty) + "__" + (name ?? string.Empty)).ToLowerInvariant();
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(slug.Length);
            foreach (char c in slug)
            {
                if (Array.IndexOf<char>(invalid, c) >= 0 || c == '/' || c == '\\')
                {
                    builder.Append('_');
                }
                else
                {
                    builder.Append(c);
                }
            }

            if (builder.Length == 0)
            {
                builder.Append("repository");
            }

            // Keep folder names short: the snapshot inside them has to hold deeply nested
            // repository paths that must still fit within the Windows path limit.
            string value = builder.ToString();
            if (value.Length <= 28)
            {
                return value;
            }

            string prefix = value.Substring(0, 19).TrimEnd(' ', '.', '-', '_');
            if (prefix.Length == 0)
            {
                prefix = "repository";
            }

            return prefix + "~" + ShortHash(value);
        }

        private static string ShortHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }

                return (hash & 0x7FFFFFFF).ToString("x8");
            }
        }

        public string GetRepositoryDirectory(string owner, string name)
        {
            return Path.Combine(ReposPath, GetSlug(owner, name));
        }

        public void InstallSnapshot(string repositoryDirectory, string sourceDirectory)
        {
            Directory.CreateDirectory(repositoryDirectory);
            string snapshot = Path.Combine(repositoryDirectory, "snapshot");
            string backup = snapshot + ".old-" + Guid.NewGuid().ToString("N");

            bool hadExisting = Directory.Exists(snapshot);
            if (hadExisting)
            {
                Directory.Move(snapshot, backup);
            }

            try
            {
                MoveDirectory(sourceDirectory, snapshot);
            }
            catch
            {
                if (hadExisting && !Directory.Exists(snapshot) && Directory.Exists(backup))
                {
                    try
                    {
                        Directory.Move(backup, snapshot);
                    }
                    catch
                    {
                    }
                }

                throw;
            }

            if (Directory.Exists(backup))
            {
                TryDeleteDirectory(backup);
            }
        }

        public static void MoveDirectory(string source, string destination)
        {
            try
            {
                Directory.Move(source, destination);
            }
            catch (IOException)
            {
                CopyDirectory(source, destination);
                Directory.Delete(source, true);
            }
        }

        public static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                string parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.Copy(file, target, true);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteDirectoryContents(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch
                    {
                    }
                }

                foreach (string directory in Directory.GetDirectories(path))
                {
                    TryDeleteDirectory(directory);
                }
            }
            catch
            {
            }
        }
    }
}
