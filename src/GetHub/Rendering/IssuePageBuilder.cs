using System;
using System.Collections.Generic;
using System.Text;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Rendering
{
    // Builds the Issues list, the issue detail page and the sign-in / empty /
    // error states. Every in-app link uses the og:// scheme so the hosting
    // WebBrowser can keep the reader inside the program window.
    internal static class IssuePageBuilder
    {
        public static string BuildListPage(
            RepoRecord record,
            List<IssueSummary> issues,
            string filter,
            DateTime cachedAtUtc,
            string notice,
            bool noticeIsError,
            bool signedIn)
        {
            if (issues == null)
            {
                issues = new List<IssueSummary>();
            }

            string normalizedFilter = NormalizeFilter(filter);
            int openCount = 0;
            int closedCount = 0;
            foreach (IssueSummary issue in issues)
            {
                if (issue.IsOpen)
                {
                    openCount++;
                }
                else
                {
                    closedCount++;
                }
            }

            List<IssueSummary> visible = new List<IssueSummary>();
            foreach (IssueSummary issue in issues)
            {
                if (normalizedFilter == "open" && !issue.IsOpen)
                {
                    continue;
                }

                if (normalizedFilter == "closed" && issue.IsOpen)
                {
                    continue;
                }

                visible.Add(issue);
            }

            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"gh-top\"><div class=\"gh-right\">");
            if (signedIn)
            {
                body.Append("<a class=\"gh-btn secondary\" href=\"og://refresh\">Refresh</a>");
            }
            else
            {
                body.Append("<a class=\"gh-btn\" href=\"og://signin\">Sign in</a>");
            }

            body.Append("</div><h1 class=\"gh-title\">Issues");
            if (issues.Count > 0)
            {
                body.Append(" <span class=\"num\">&middot; ");
                body.Append(issues.Count);
                body.Append("</span>");
            }

            body.Append("</h1><div class=\"gh-sub\">");
            body.Append(signedIn
                ? "Read issues and comments without leaving GetHub."
                : "Sign in to download this repository's issues into GetHub.");
            body.Append("</div></div>");

            if (!string.IsNullOrEmpty(notice))
            {
                body.Append("<div class=\"gh-notice");
                if (noticeIsError)
                {
                    body.Append(" bad");
                }

                body.Append("\">");
                body.Append(notice);
                if (!signedIn && noticeIsError)
                {
                    body.Append(" <a href=\"og://signin\">Sign in again</a>.");
                }

                body.Append("</div>");
            }

            body.Append("<div class=\"gh-filters\">");
            body.Append(FilterLink("Open", "open", openCount, normalizedFilter));
            body.Append(FilterLink("Closed", "closed", closedCount, normalizedFilter));
            body.Append(FilterLink("All", "all", issues.Count, normalizedFilter));
            body.Append("</div>");

            if (visible.Count == 0)
            {
                body.Append("<div class=\"gh-empty\"><div class=\"mark\">&#9675;</div><h2>");
                body.Append(issues.Count == 0 ? "No issues yet" : "Nothing in this filter");
                body.Append("</h2><p>");
                body.Append(issues.Count == 0
                    ? (signedIn
                        ? "This repository has no issues, or they have not been downloaded yet. Choose Refresh to check GitHub again."
                        : "Sign in and choose Refresh to download this repository's issues.")
                    : "No " + (normalizedFilter == "all" ? string.Empty : normalizedFilter + " ") +
                      "issues are stored for this repository.");
                body.Append("</p>");
                if (!signedIn)
                {
                    body.Append("<a class=\"gh-btn\" href=\"og://signin\">Sign in to GitHub</a>");
                }

                body.Append("</div>");
            }
            else
            {
                body.Append("<div class=\"gh-list\">");
                foreach (IssueSummary issue in visible)
                {
                    AppendRow(body, issue);
                }

                body.Append("</div>");
            }

            AppendFooter(body, cachedAtUtc, signedIn);
            return SiteBuilder.BuildPage(record.GetDisplayName() + " issues", body.ToString(), GitHubPageStyles.Css);
        }

        public static string BuildDetailPage(
            RepoRecord record,
            IssueDetail detail,
            DateTime cachedAtUtc,
            string notice,
            bool noticeIsError,
            bool signedIn)
        {
            StringBuilder body = new StringBuilder();
            if (detail == null)
            {
                return BuildMessagePage(record, "Issue unavailable",
                    "The issue could not be loaded. Return to the list and try again.", signedIn);
            }

            body.Append("<div class=\"gh-top\"><div class=\"gh-right\">");
            body.Append("<a class=\"gh-btn secondary\" href=\"og://issues?state=all\">&larr; All issues</a>");
            if (signedIn)
            {
                body.Append(" <a class=\"gh-btn secondary\" href=\"og://issue?number=");
                body.Append(detail.Number);
                body.Append("&amp;refresh=1\">Refresh</a>");
            }

            body.Append("</div><h1 class=\"gh-title\">");
            body.Append(GitHubPageStyles.E(detail.Title));
            body.Append(" <span class=\"num\">#");
            body.Append(detail.Number);
            body.Append("</span></h1><div class=\"gh-sub\">");
            body.Append(GitHubPageStyles.StateBadge(detail));
            if (!string.IsNullOrEmpty(detail.AuthorLogin))
            {
                body.Append(" <b>");
                body.Append(GitHubPageStyles.E(detail.AuthorLogin));
                body.Append("</b>");
            }

            body.Append(" opened ");
            body.Append(GitHubPageStyles.E(Display.RelativeTime(detail.CreatedAtUtc)));
            body.Append(" &middot; ");
            body.Append(detail.CommentCount);
            body.Append(detail.CommentCount == 1 ? " comment" : " comments");
            if (detail.ClosedAtUtc != DateTime.MinValue)
            {
                body.Append(" &middot; closed ");
                body.Append(GitHubPageStyles.E(Display.RelativeTime(detail.ClosedAtUtc)));
            }

            body.Append("</div></div>");

            if (detail.Labels != null && detail.Labels.Count > 0)
            {
                body.Append("<div class=\"gh-filters\">");
                foreach (IssueLabel label in detail.Labels)
                {
                    body.Append(GitHubPageStyles.LabelHtml(label));
                }

                body.Append("</div>");
            }

            if (!string.IsNullOrEmpty(notice))
            {
                body.Append("<div class=\"gh-notice");
                if (noticeIsError)
                {
                    body.Append(" bad");
                }

                body.Append("\">");
                body.Append(notice);
                body.Append("</div>");
            }

            body.Append("<div class=\"gh-detail\"><div class=\"gh-detail-head\">");
            body.Append("<b>");
            body.Append(GitHubPageStyles.E(string.IsNullOrEmpty(detail.AuthorLogin) ? "unknown" : detail.AuthorLogin));
            body.Append("</b> opened this issue ");
            body.Append(GitHubPageStyles.E(Display.RelativeTime(detail.CreatedAtUtc)));
            body.Append("</div>");
            AppendMarkdown(body, record, detail.Body, "No description provided.");
            body.Append("</div>");

            if (detail.Comments != null && detail.Comments.Count > 0)
            {
                body.Append("<h2 class=\"gh-sub\" style=\"font-size:16px;margin:22px 0 4px;color:");
                body.Append(GitHubPageStyles.Hex(UiTheme.Text));
                body.Append(";\">Comments</h2>");
                foreach (IssueComment comment in detail.Comments)
                {
                    body.Append("<div class=\"gh-comment\"><div class=\"gh-comment-head\"><b>");
                    body.Append(GitHubPageStyles.E(string.IsNullOrEmpty(comment.AuthorLogin) ? "unknown" : comment.AuthorLogin));
                    body.Append("</b> commented ");
                    body.Append(GitHubPageStyles.E(Display.RelativeTime(comment.CreatedAtUtc)));
                    if (!string.IsNullOrEmpty(comment.AuthorAssociation) &&
                        !string.Equals(comment.AuthorAssociation, "NONE", StringComparison.OrdinalIgnoreCase))
                    {
                        body.Append(" &middot; ");
                        body.Append(GitHubPageStyles.E(comment.AuthorAssociation.ToLowerInvariant()));
                    }

                    body.Append("</div>");
                    AppendMarkdown(body, record, comment.Body, "This comment is empty.");
                    body.Append("</div>");
                }
            }
            else if (detail.CommentCount > 0)
            {
                body.Append("<div class=\"gh-notice\">The comments have not been downloaded yet. ");
                if (signedIn)
                {
                    body.Append("Choose Refresh to load them.");
                }
                else
                {
                    body.Append("Sign in and choose Refresh to load them.");
                }

                body.Append("</div>");
            }

            if (detail.CommentsTruncated)
            {
                body.Append("<div class=\"gh-notice\">This thread is longer than GetHub downloads. ");
                body.Append("Open it on GitHub to read the remaining comments.</div>");
            }

            body.Append("<div class=\"gh-foot\">");
            if (!string.IsNullOrEmpty(detail.HtmlUrl))
            {
                body.Append(GitHubPageStyles.OpenInGitHub(detail.HtmlUrl, "View this issue on GitHub"));
                body.Append(" &middot; ");
            }

            body.Append("Cached ");
            body.Append(GitHubPageStyles.E(cachedAtUtc == DateTime.MinValue ? "just now" : Display.RelativeTime(cachedAtUtc)));
            body.Append("</div>");
            return SiteBuilder.BuildPage(
                record.GetDisplayName() + " issue #" + detail.Number,
                body.ToString(),
                GitHubPageStyles.Css);
        }

        public static string BuildSignInPage(RepoRecord record, string message)
        {
            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"gh-empty\"><div class=\"mark\">&#9675;</div>");
            body.Append("<h2>Sign in to read issues</h2><p>");
            body.Append(string.IsNullOrEmpty(message)
                ? "GetHub can show this repository's issues, their descriptions and their comments inside this window. " +
                  "Sign in with a GitHub access token to download them."
                : message);
            body.Append("</p><a class=\"gh-btn\" href=\"og://signin\">Sign in to GitHub</a>");
            body.Append("<p class=\"gh-foot\" style=\"margin:20px auto 0;\">");
            body.Append("GetHub asks for the <b>repo</b> and <b>read:project</b> scopes. ");
            body.Append("The token is stored in your Windows profile and is only sent to api.github.com.");
            body.Append("</p></div>");
            return SiteBuilder.BuildPage(record.GetDisplayName() + " issues", body.ToString(), GitHubPageStyles.Css);
        }

        public static string BuildMessagePage(RepoRecord record, string title, string message, bool signedIn)
        {
            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"gh-empty\"><div class=\"mark\">!</div><h2>");
            body.Append(GitHubPageStyles.E(title));
            body.Append("</h2><p>");
            body.Append(GitHubPageStyles.E(message));
            body.Append("</p>");
            if (signedIn)
            {
                body.Append("<a class=\"gh-btn secondary\" href=\"og://refresh\">Try again</a>");
            }
            else
            {
                body.Append("<a class=\"gh-btn\" href=\"og://signin\">Sign in to GitHub</a>");
            }

            body.Append("</div>");
            return SiteBuilder.BuildPage(record.GetDisplayName() + " issues", body.ToString(), GitHubPageStyles.Css);
        }

        public static string BuildLocalPage(RepoRecord record)
        {
            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"gh-empty\"><div class=\"mark\">&#9675;</div>");
            body.Append("<h2>Issues are not available for local folders</h2><p>");
            body.Append("This repository was opened from a folder on this computer. Issues live on GitHub, so ");
            body.Append("add the repository by its GitHub address to read them here.");
            body.Append("</p></div>");
            return SiteBuilder.BuildPage(record.GetDisplayName() + " issues", body.ToString(), GitHubPageStyles.Css);
        }

        private static void AppendRow(StringBuilder body, IssueSummary issue)
        {
            body.Append("<div class=\"gh-row\"><div class=\"gh-state ");
            body.Append(GitHubPageStyles.StateClass(issue));
            body.Append("\">");
            body.Append(GitHubPageStyles.StateMark(issue));
            body.Append("</div><div><a class=\"gh-row-title\" href=\"og://issue?number=");
            body.Append(issue.Number);
            body.Append("\">");
            body.Append(GitHubPageStyles.E(issue.Title));
            body.Append("</a>");
            if (issue.Labels != null)
            {
                foreach (IssueLabel label in issue.Labels)
                {
                    body.Append(GitHubPageStyles.LabelHtml(label));
                }
            }

            body.Append("</div><div class=\"gh-meta\">#");
            body.Append(issue.Number);
            body.Append(" opened ");
            body.Append(GitHubPageStyles.E(Display.RelativeTime(issue.CreatedAtUtc)));
            if (!string.IsNullOrEmpty(issue.AuthorLogin))
            {
                body.Append(" by <b>");
                body.Append(GitHubPageStyles.E(issue.AuthorLogin));
                body.Append("</b>");
            }

            AppendComments(body, issue);
            if (issue.UpdatedAtUtc != DateTime.MinValue)
            {
                body.Append(" &middot; updated ");
                body.Append(GitHubPageStyles.E(Display.RelativeTime(issue.UpdatedAtUtc)));
            }

            body.Append("</div></div>");
        }

        private static void AppendComments(StringBuilder body, IssueSummary issue)
        {
            if (issue.CommentCount <= 0)
            {
                return;
            }

            body.Append(" &middot; ");
            body.Append(issue.CommentCount);
            body.Append(issue.CommentCount == 1 ? " comment" : " comments");
        }

        private static string FilterLink(string text, string state, int count, string current)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<a class=\"gh-filter");
            if (string.Equals(state, current, StringComparison.OrdinalIgnoreCase))
            {
                builder.Append(" active");
            }

            builder.Append("\" href=\"og://issues?state=");
            builder.Append(state);
            builder.Append("\">");
            builder.Append(text);
            builder.Append(" <span class=\"gh-col-count\">");
            builder.Append(count);
            builder.Append("</span></a>");
            return builder.ToString();
        }

        private static void AppendMarkdown(StringBuilder body, RepoRecord record, string markdown, string emptyText)
        {
            string html = string.Empty;
            if (!string.IsNullOrEmpty(markdown) && markdown.Trim().Length > 0)
            {
                try
                {
                    RepoContext context = new RepoContext();
                    context.RepoRoot = record == null ? string.Empty : record.GetSnapshotPath();
                    context.CurrentDirectory = string.Empty;
                    context.CurrentFileRelative = string.Empty;
                    context.Owner = record == null ? string.Empty : record.Owner;
                    context.Name = record == null ? string.Empty : record.Name;
                    context.Branch = record == null ? string.Empty : record.Branch;
                    html = new MarkdownRenderer().Render(markdown, context);
                }
                catch
                {
                    html = string.Empty;
                }
            }

            if (string.IsNullOrEmpty(html))
            {
                body.Append("<div class=\"gh-comment-body empty\">");
                body.Append(GitHubPageStyles.E(emptyText));
                body.Append("</div>");
                return;
            }

            body.Append("<div class=\"gh-comment-body markdown-body\">");
            body.Append(html);
            body.Append("</div>");
        }

        private static void AppendFooter(StringBuilder body, DateTime cachedAtUtc, bool signedIn)
        {
            body.Append("<div class=\"gh-foot\">");
            if (cachedAtUtc == DateTime.MinValue)
            {
                body.Append("Nothing cached yet.");
            }
            else
            {
                body.Append("Cached from GitHub ");
                body.Append(GitHubPageStyles.E(Display.RelativeTime(cachedAtUtc)));
                body.Append(".");
            }

            if (signedIn)
            {
                body.Append(" Choose Refresh to update.");
            }
            else
            {
                body.Append(" Sign in to update this list.");
            }

            body.Append("</div>");
        }

        private static string NormalizeFilter(string filter)
        {
            if (string.Equals(filter, "closed", StringComparison.OrdinalIgnoreCase))
            {
                return "closed";
            }

            if (string.Equals(filter, "all", StringComparison.OrdinalIgnoreCase))
            {
                return "all";
            }

            return "open";
        }
    }
}
