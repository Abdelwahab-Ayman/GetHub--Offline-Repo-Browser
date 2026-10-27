using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using GetHub.Models;

namespace GetHub.Services
{
    // Stores one JSON file per commit under {repo cache}/commits. The graph only
    // caches the newest 100 commits, but their messages and diffs are fetched on
    // demand and then kept on disk so the detail tabs work without a connection.
    public static class CommitCache
    {
        private const int MaxCachedCommits = 80;
        private static readonly Regex ShaPattern =
            new Regex("^[0-9a-fA-F]{7,40}$", RegexOptions.Compiled);
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static string GetDirectory(RepoRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.LocalPath))
            {
                return string.Empty;
            }

            return Path.Combine(record.LocalPath, "commits");
        }

        public static string GetPath(RepoRecord record, string sha)
        {
            if (record == null ||
                string.IsNullOrEmpty(record.LocalPath) ||
                string.IsNullOrEmpty(sha) ||
                !ShaPattern.IsMatch(sha))
            {
                return null;
            }

            return Path.Combine(GetDirectory(record), sha.ToLowerInvariant() + ".json");
        }

        public static CommitDetail Read(RepoRecord record, string sha)
        {
            string path = GetPath(record, sha);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                CommitDetail detail = Serializer.Deserialize<CommitDetail>(
                    File.ReadAllText(path, Encoding.UTF8));
                if (detail == null || string.IsNullOrEmpty(detail.Sha))
                {
                    return null;
                }

                return detail;
            }
            catch
            {
                // A partially written cache file is treated as a cache miss.
                return null;
            }
        }

        public static void Write(RepoRecord record, CommitDetail detail)
        {
            if (detail == null || string.IsNullOrEmpty(detail.Sha))
            {
                return;
            }

            string path = GetPath(record, detail.Sha);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, Serializer.Serialize(detail), new UTF8Encoding(false));
                Prune(Path.GetDirectoryName(path));
            }
            catch
            {
                // Caching is a convenience; never break the history view because of it.
            }
        }

        // Keeps the cache bounded so browsing many commits cannot fill the disk.
        private static void Prune(string directory)
        {
            try
            {
                string[] files = Directory.GetFiles(directory, "*.json");
                if (files.Length <= MaxCachedCommits)
                {
                    return;
                }

                List<FileInfo> infos = new List<FileInfo>();
                foreach (string file in files)
                {
                    infos.Add(new FileInfo(file));
                }

                infos.Sort(delegate(FileInfo left, FileInfo right)
                {
                    return right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
                });

                for (int i = MaxCachedCommits; i < infos.Count; i++)
                {
                    try
                    {
                        infos[i].Delete();
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
