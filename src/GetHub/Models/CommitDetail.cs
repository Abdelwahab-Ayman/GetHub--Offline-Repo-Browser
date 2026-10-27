using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace GetHub.Models
{
    // Everything shown for a single commit: the full message, the changed files and
    // the unified diff. GitHub repositories get this from the REST API, local
    // repositories from git, and both are cached so the tabs keep working offline.
    public class CommitDetail
    {
        public string Sha { get; set; }
        public string Message { get; set; }
        public string AuthorName { get; set; }
        public string AuthorEmail { get; set; }
        public DateTime AuthorDateUtc { get; set; }
        public string CommitterName { get; set; }
        public string CommitterEmail { get; set; }
        public DateTime CommitDateUtc { get; set; }
        public string HtmlUrl { get; set; }
        public List<string> Parents { get; set; }
        public int Additions { get; set; }
        public int Deletions { get; set; }
        public bool Truncated { get; set; }
        public List<CommitFile> Files { get; set; }

        public CommitDetail()
        {
            Sha = string.Empty;
            Message = string.Empty;
            AuthorName = string.Empty;
            AuthorEmail = string.Empty;
            CommitterName = string.Empty;
            CommitterEmail = string.Empty;
            HtmlUrl = string.Empty;
            Parents = new List<string>();
            Files = new List<CommitFile>();
        }

        [ScriptIgnore]
        public string ShortSha
        {
            get
            {
                if (string.IsNullOrEmpty(Sha))
                {
                    return string.Empty;
                }

                return Sha.Length > 7 ? Sha.Substring(0, 7) : Sha;
            }
        }

        [ScriptIgnore]
        public string Subject
        {
            get
            {
                string message = Message == null ? string.Empty : Message.TrimStart('\r', '\n');
                int newline = message.IndexOfAny(new char[] { '\r', '\n' });
                if (newline >= 0)
                {
                    message = message.Substring(0, newline);
                }

                message = message.Trim();
                return message.Length == 0 ? "(no commit message)" : message;
            }
        }

        [ScriptIgnore]
        public string Body
        {
            get
            {
                string message = Message == null ? string.Empty : Message.TrimStart('\r', '\n');
                int newline = message.IndexOfAny(new char[] { '\r', '\n' });
                if (newline < 0)
                {
                    return string.Empty;
                }

                return message.Substring(newline).Trim('\r', '\n', ' ', '\t');
            }
        }

        [ScriptIgnore]
        public string AuthorDisplay
        {
            get
            {
                return string.IsNullOrEmpty(AuthorName)
                    ? "unknown"
                    : AuthorName;
            }
        }

        [ScriptIgnore]
        public bool HasDifferentCommitter
        {
            get
            {
                if (string.IsNullOrEmpty(CommitterName))
                {
                    return false;
                }

                return !string.Equals(CommitterName, AuthorName, StringComparison.Ordinal) ||
                    !string.Equals(CommitterEmail, AuthorEmail, StringComparison.OrdinalIgnoreCase);
            }
        }

        [ScriptIgnore]
        public int ChangedFiles
        {
            get { return Files == null ? 0 : Files.Count; }
        }

        [ScriptIgnore]
        public bool IsMerge
        {
            get { return Parents != null && Parents.Count > 1; }
        }
    }
}
