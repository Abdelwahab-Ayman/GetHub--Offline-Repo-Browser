using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using GetHub.Models;

namespace GetHub.Services
{
    // The issues index and one JSON file per opened issue. Keeping the index and
    // the details apart means refreshing the list never throws away downloaded
    // comment threads, and opening an issue does not rewrite the whole index.
    public static class IssueCache
    {
        private const int MaxCachedDetails = 120;
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public class Index
        {
            public DateTime UpdatedAtUtc { get; set; }
            public List<IssueSummary> Issues { get; set; }

            public Index()
            {
                Issues = new List<IssueSummary>();
            }
        }

        public static string GetIndexPath(RepoRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.LocalPath))
            {
                return null;
            }

            return Path.Combine(record.LocalPath, "issues.json");
        }

        public static string GetDetailDirectory(RepoRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.LocalPath))
            {
                return null;
            }

            return Path.Combine(record.LocalPath, "issues");
        }

        public static string GetDetailPath(RepoRecord record, int number)
        {
            string directory = GetDetailDirectory(record);
            if (directory == null || number <= 0)
            {
                return null;
            }

            return Path.Combine(directory, number.ToString() + ".json");
        }

        public static Index ReadIndex(RepoRecord record)
        {
            string path = GetIndexPath(record);
            if (path == null || !File.Exists(path))
            {
                return null;
            }

            try
            {
                Index index = Serializer.Deserialize<Index>(File.ReadAllText(path, Encoding.UTF8));
                if (index == null || index.Issues == null)
                {
                    return null;
                }

                return index;
            }
            catch
            {
                return null;
            }
        }

        public static void WriteIndex(RepoRecord record, List<IssueSummary> issues)
        {
            string path = GetIndexPath(record);
            if (path == null || issues == null)
            {
                return;
            }

            try
            {
                Index index = new Index();
                index.UpdatedAtUtc = DateTime.UtcNow;
                index.Issues = issues;
                GitHubApi.WriteAtomic(path, Serializer.Serialize(index));
            }
            catch
            {
                // Caching is best effort; the live view still works.
            }
        }

        public static IssueDetail ReadDetail(RepoRecord record, int number)
        {
            string path = GetDetailPath(record, number);
            if (path == null || !File.Exists(path))
            {
                return null;
            }

            try
            {
                IssueDetail detail = Serializer.Deserialize<IssueDetail>(File.ReadAllText(path, Encoding.UTF8));
                if (detail == null || detail.Number != number)
                {
                    return null;
                }

                return detail;
            }
            catch
            {
                return null;
            }
        }

        public static void WriteDetail(RepoRecord record, IssueDetail detail)
        {
            if (detail == null || detail.Number <= 0)
            {
                return;
            }

            string path = GetDetailPath(record, detail.Number);
            if (path == null)
            {
                return;
            }

            try
            {
                GitHubApi.WriteAtomic(path, Serializer.Serialize(detail));
                PruneDetails(GetDetailDirectory(record));
            }
            catch
            {
            }
        }

        public static void Clear(RepoRecord record)
        {
            try
            {
                string index = GetIndexPath(record);
                if (index != null && File.Exists(index))
                {
                    File.Delete(index);
                }
            }
            catch
            {
            }
        }

        private static void PruneDetails(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            try
            {
                string[] files = Directory.GetFiles(directory, "*.json");
                if (files.Length <= MaxCachedDetails)
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

                for (int i = MaxCachedDetails; i < infos.Count; i++)
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
