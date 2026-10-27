using System;
using System.Collections.Generic;
using System.IO;

namespace GetHub.Models
{
    public class RepoRecord
    {
        public string Owner { get; set; }
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Description { get; set; }
        public string DefaultBranch { get; set; }
        public string Branch { get; set; }
        public string HtmlUrl { get; set; }
        public string Language { get; set; }
        public int Stars { get; set; }
        public int Forks { get; set; }
        public int OpenIssues { get; set; }
        public long SizeKb { get; set; }
        public string LicenseName { get; set; }
        public string LocalPath { get; set; }
        public DateTime DownloadedAtUtc { get; set; }
        public string AvatarUrl { get; set; }

        // Extra repository details shown in the About / Releases / Packages / Contributors
        // column. They are filled from the GitHub REST API and cached in repo.json.
        public string Homepage { get; set; }
        public int Watchers { get; set; }
        public List<string> Topics { get; set; }
        public List<ReleaseInfo> Releases { get; set; }
        public List<ContributorInfo> Contributors { get; set; }
        public List<string> Packages { get; set; }
        public bool DetailsLoaded { get; set; }
        public DateTime DetailsUpdatedAtUtc { get; set; }

        // Commit history shown by the History tab. GitHub repositories cache the most
        // recent commits in repo.json; local repositories read their original .git folder.
        public List<CommitInfo> Commits { get; set; }
        public DateTime CommitsUpdatedAtUtc { get; set; }
        public string SourcePath { get; set; }

        public RepoRecord()
        {
            Owner = string.Empty;
            Name = string.Empty;
            FullName = string.Empty;
            Description = string.Empty;
            DefaultBranch = string.Empty;
            Branch = string.Empty;
            HtmlUrl = string.Empty;
            Language = string.Empty;
            LicenseName = string.Empty;
            LocalPath = string.Empty;
            AvatarUrl = string.Empty;
            Homepage = string.Empty;
            Topics = new List<string>();
            Releases = new List<ReleaseInfo>();
            Contributors = new List<ContributorInfo>();
            Packages = new List<string>();
            Commits = new List<CommitInfo>();
            SourcePath = string.Empty;
        }

        public string GetSnapshotPath()
        {
            return Path.Combine(LocalPath, "snapshot");
        }

        public string GetDisplayName()
        {
            if (!string.IsNullOrEmpty(FullName))
            {
                return FullName;
            }

            return Owner + "/" + Name;
        }

        public bool HasSnapshot()
        {
            return !string.IsNullOrEmpty(LocalPath) && Directory.Exists(GetSnapshotPath());
        }
    }
}
