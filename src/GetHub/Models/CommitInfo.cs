using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace GetHub.Models
{
    // One commit in the offline history view. Cached commits come from the GitHub
    // REST API, while local repositories are read straight from their .git folder.
    public class CommitInfo
    {
        public string Sha { get; set; }
        public string Message { get; set; }
        public string AuthorName { get; set; }
        public string AuthorLogin { get; set; }
        public DateTime AuthorDateUtc { get; set; }
        public string HtmlUrl { get; set; }
        public List<string> Parents { get; set; }

        public CommitInfo()
        {
            Sha = string.Empty;
            Message = string.Empty;
            AuthorName = string.Empty;
            AuthorLogin = string.Empty;
            HtmlUrl = string.Empty;
            Parents = new List<string>();
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
        public string AuthorDisplay
        {
            get
            {
                if (!string.IsNullOrEmpty(AuthorLogin))
                {
                    return AuthorLogin;
                }

                return string.IsNullOrEmpty(AuthorName) ? "unknown" : AuthorName;
            }
        }

        [ScriptIgnore]
        public string Subject
        {
            get
            {
                string message = Message == null ? string.Empty : Message;
                int newline = message.IndexOfAny(new char[] { '\r', '\n' });
                if (newline >= 0)
                {
                    message = message.Substring(0, newline);
                }

                message = message.Trim();
                return message.Length == 0 ? "(no commit message)" : message;
            }
        }
    }
}
