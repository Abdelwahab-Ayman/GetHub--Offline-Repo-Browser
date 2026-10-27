using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace GetHub.Models
{
    // One card on a GitHub project board. Content can be an issue, a pull request
    // or a draft issue created inside the project itself.
    public class ProjectItem
    {
        public string Type { get; set; }
        public int Number { get; set; }
        public string Title { get; set; }
        public string State { get; set; }
        public string Url { get; set; }
        public string Status { get; set; }

        public ProjectItem()
        {
            Type = string.Empty;
            Title = string.Empty;
            State = string.Empty;
            Url = string.Empty;
            Status = string.Empty;
        }

        [ScriptIgnore]
        public bool IsIssue
        {
            get { return string.Equals(Type, "Issue", StringComparison.OrdinalIgnoreCase); }
        }

        [ScriptIgnore]
        public bool IsPullRequest
        {
            get { return string.Equals(Type, "PullRequest", StringComparison.OrdinalIgnoreCase); }
        }

        [ScriptIgnore]
        public string TypeLabel
        {
            get
            {
                if (IsIssue)
                {
                    return "Issue";
                }

                if (IsPullRequest)
                {
                    return "Pull request";
                }

                return "Draft";
            }
        }
    }

    // A GitHub Projects (v2) board with its first page of items.
    public class ProjectSummary
    {
        public int Number { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public bool Closed { get; set; }
        public int TotalItems { get; set; }
        public List<ProjectItem> Items { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public ProjectSummary()
        {
            Title = string.Empty;
            Description = string.Empty;
            Url = string.Empty;
            Items = new List<ProjectItem>();
        }

        [ScriptIgnore]
        public string ItemCountLabel
        {
            get
            {
                int count = TotalItems > 0 ? TotalItems : (Items == null ? 0 : Items.Count);
                return count == 1 ? "1 item" : count + " items";
            }
        }
    }
}
