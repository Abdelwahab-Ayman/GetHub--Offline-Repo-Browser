using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using GetHub.Models;

namespace GetHub.Services
{
    // Project boards are stored as one JSON document per repository. They only
    // change when the user presses Refresh, so a single file keeps the offline
    // copy simple and cheap to rewrite.
    public static class ProjectCache
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public class Snapshot
        {
            public DateTime UpdatedAtUtc { get; set; }
            public List<ProjectSummary> Projects { get; set; }

            public Snapshot()
            {
                Projects = new List<ProjectSummary>();
            }
        }

        public static string GetPath(RepoRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.LocalPath))
            {
                return null;
            }

            return Path.Combine(record.LocalPath, "projects.json");
        }

        public static Snapshot Read(RepoRecord record)
        {
            string path = GetPath(record);
            if (path == null || !File.Exists(path))
            {
                return null;
            }

            try
            {
                Snapshot snapshot = Serializer.Deserialize<Snapshot>(File.ReadAllText(path, Encoding.UTF8));
                if (snapshot == null || snapshot.Projects == null)
                {
                    return null;
                }

                return snapshot;
            }
            catch
            {
                return null;
            }
        }

        public static void Write(RepoRecord record, List<ProjectSummary> projects)
        {
            string path = GetPath(record);
            if (path == null || projects == null)
            {
                return;
            }

            try
            {
                Snapshot snapshot = new Snapshot();
                snapshot.UpdatedAtUtc = DateTime.UtcNow;
                snapshot.Projects = projects;
                GitHubApi.WriteAtomic(path, Serializer.Serialize(snapshot));
            }
            catch
            {
            }
        }

        public static void Clear(RepoRecord record)
        {
            try
            {
                string path = GetPath(record);
                if (path != null && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
