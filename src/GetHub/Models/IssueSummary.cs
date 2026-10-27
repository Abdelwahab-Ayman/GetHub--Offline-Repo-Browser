using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace GetHub.Models
{
    // One label on an issue, as returned by the REST API. Color is the six-digit
    // hex value GitHub provides without the leading '#'.
    public class IssueLabel
    {
        public string Name { get; set; }
        public string Color { get; set; }

        public IssueLabel()
        {
            Name = string.Empty;
            Color = string.Empty;
        }

        [ScriptIgnore]
        public string Background
        {
            get { return string.IsNullOrEmpty(Color) ? "#8b949e" : "#" + Color.TrimStart('#'); }
        }

        [ScriptIgnore]
        public string Foreground
        {
            get
            {
                string color = string.IsNullOrEmpty(Color) ? string.Empty : Color.TrimStart('#');
                if (color.Length != 6)
                {
                    return "#ffffff";
                }

                int red;
                int green;
                int blue;
                if (!int.TryParse(color.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out red) ||
                    !int.TryParse(color.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out green) ||
                    !int.TryParse(color.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out blue))
                {
                    return "#ffffff";
                }

                // Perceived luminance, same idea as GitHub's label contrast rule.
                double luminance = ((red * 0.299) + (green * 0.587) + (blue * 0.114)) / 255.0;
                return luminance > 0.6 ? "#1f2328" : "#ffffff";
            }
        }
    }

    // A row in the Issues list. Pull requests are filtered out before this model
    // is created, because the Issues section mirrors GitHub's issue-only list.
    public class IssueSummary
    {
        public int Number { get; set; }
        public string Title { get; set; }
        public string State { get; set; }
        public string StateReason { get; set; }
        public string AuthorLogin { get; set; }
        public string AuthorAvatarUrl { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime ClosedAtUtc { get; set; }
        public int CommentCount { get; set; }
        public string HtmlUrl { get; set; }
        public List<IssueLabel> Labels { get; set; }

        public IssueSummary()
        {
            Title = string.Empty;
            State = "open";
            StateReason = string.Empty;
            AuthorLogin = string.Empty;
            AuthorAvatarUrl = string.Empty;
            HtmlUrl = string.Empty;
            Labels = new List<IssueLabel>();
        }

        [ScriptIgnore]
        public bool IsOpen
        {
            get { return string.Equals(State, "open", StringComparison.OrdinalIgnoreCase); }
        }

        [ScriptIgnore]
        public string StateLabel
        {
            get
            {
                if (IsOpen)
                {
                    return "Open";
                }

                if (string.Equals(StateReason, "not_planned", StringComparison.OrdinalIgnoreCase))
                {
                    return "Not planned";
                }

                return "Closed";
            }
        }
    }
}
