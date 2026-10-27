using System;
using System.Collections.Generic;

namespace GetHub.Models
{
    // A comment on an issue. Only the fields needed by the offline reader are
    // kept so the cached JSON stays small.
    public class IssueComment
    {
        public string AuthorLogin { get; set; }
        public string AuthorAvatarUrl { get; set; }
        public string Body { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string AuthorAssociation { get; set; }

        public IssueComment()
        {
            AuthorLogin = string.Empty;
            AuthorAvatarUrl = string.Empty;
            Body = string.Empty;
            AuthorAssociation = string.Empty;
        }
    }

    // The full issue, including its Markdown body and comments, cached separately
    // from the list so opening a single issue does not rewrite the whole index.
    public class IssueDetail : IssueSummary
    {
        public string Body { get; set; }
        public List<IssueComment> Comments { get; set; }
        public bool CommentsTruncated { get; set; }

        public IssueDetail()
        {
            Body = string.Empty;
            Comments = new List<IssueComment>();
        }
    }
}
