using System;
using System.Collections.Generic;
using System.Text;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Rendering
{
    // Builds the Projects overview and the project board. Boards are laid out as
    // a table of status columns because the embedded IE11 renderer supports
    // tables reliably and they wrap better than floated columns.
    internal static class ProjectPageBuilder
    {
        public static string BuildListPage(
            RepoRecord record,
            List<ProjectSummary> projects,
            DateTime cachedAtUtc,
            string notice,
            bool noticeIsError,
            bool signedIn)
        {
            if (projects == null)
            {
                projects = new List<ProjectSummary>();
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

            body.Append("</div><h1 class=\"gh-title\">Projects</h1>");
            body.Append("<div class=\"gh-sub\">");
            body.Append(signedIn
                ? "Project boards and their cards, read inside GetHub."
                : "Sign in to download this repository's project boards into GetHub.");
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

            if (projects.Count == 0)
            {
                body.Append("<div class=\"gh-empty\"><div class=\"mark\">&#9776;</div>");
                body.Append("<h2>No projects yet</h2><p>");
                body.Append(signedIn
                    ? "This repository has no GitHub Projects boards, or they have not been downloaded yet. " +
                      "Choose Refresh to check GitHub again."
                    : "Sign in and choose Refresh to download this repository's GitHub Projects boards.");
                body.Append("</p>");
                if (!signedIn)
                {
                    body.Append("<a class=\"gh-btn\" href=\"og://signin\">Sign in to GitHub</a>");
                }
                else
                {
                    body.Append("<a class=\"gh-btn secondary\" href=\"og://refresh\">Try again</a>");
                }

                body.Append("</div>");
            }
            else
            {
                body.Append("<div class=\"gh-list\">");
                foreach (ProjectSummary project in projects)
                {
                    AppendProjectRow(body, project);
                }

                body.Append("</div>");
            }

            AppendFooter(body, cachedAtUtc, signedIn);
            return SiteBuilder.BuildPage(record.GetDisplayName() + " projects", body.ToString(), GitHubPageStyles.Css);
        }

        public static string BuildBoardPage(
            RepoRecord record,
            ProjectSummary project,
            DateTime cachedAtUtc,
            string notice,
            bool noticeIsError,
            bool signedIn)
        {
            if (project == null)
            {
                return BuildMessagePage(record, "Project unavailable",
                    "The board could not be loaded. Return to the project list and try again.", signedIn);
            }

            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"gh-top\"><div class=\"gh-right\">");
            body.Append("<a class=\"gh-btn secondary\" href=\"og://projects\">&larr; All projects</a>");
            if (signedIn)
            {
                body.Append(" <a class=\"gh-btn secondary\" href=\"og://project?number=");
                body.Append(project.Number);
                body.Append("&amp;refresh=1\">Refresh</a>");
            }

            body.Append("</div><h1 class=\"gh-title\">");
            body.Append(GitHubPageStyles.E(project.Title));
            body.Append("</h1><div class=\"gh-sub\">");
            body.Append(GitHubPageStyles.E(project.ItemCountLabel));
            body.Append(" &middot; updated ");
            body.Append(GitHubPageStyles.E(Display.RelativeTime(project.UpdatedAtUtc)));
            body.Append(" &middot; ");
            body.Append(project.Closed ? "Closed" : "Public board");
            body.Append("</div>");

            if (!string.IsNullOrEmpty(project.Description))
            {
                body.Append("<div class=\"gh-desc\">");
                body.Append(GitHubPageStyles.E(project.Description));
                body.Append("</div>");
            }

            body.Append("</div>");

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

            if (project.Items == null || project.Items.Count == 0)
            {
                body.Append("<div class=\"gh-empty\"><div class=\"mark\">&#9776;</div>");
                body.Append("<h2>This board is empty</h2><p>");
                body.Append(project.TotalItems > 0
                    ? "GitHub reports items on this board, but they were not returned by the API."
                    : "No cards have been added to this project yet.");
                body.Append("</p></div>");
            }
            else
            {
                AppendBoard(body, project);
            }

            if (project.TotalItems > project.Items.Count && project.Items.Count > 0)
            {
                body.Append("<div class=\"gh-notice\">Showing the first ");
                body.Append(project.Items.Count);
                body.Append(" of ");
                body.Append(project.TotalItems);
                body.Append(" cards. Open the board on GitHub to see the rest.</div>");
            }

            body.Append("<div class=\"gh-foot\">");
            if (!string.IsNullOrEmpty(project.Url))
            {
                body.Append(GitHubPageStyles.OpenInGitHub(project.Url, "View this project on GitHub"));
                body.Append(" &middot; ");
            }

            body.Append("Cached ");
            body.Append(GitHubPageStyles.E(cachedAtUtc == DateTime.MinValue ? "just now" : Display.RelativeTime(cachedAtUtc)));
            body.Append("</div>");
            return SiteBuilder.BuildPage(record.GetDisplayName() + " project", body.ToString(), GitHubPageStyles.Css);
        }

        public static string BuildSignInPage(RepoRecord record, string message)
        {
            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"gh-empty\"><div class=\"mark\">&#9776;</div>");
            body.Append("<h2>Sign in to read projects</h2><p>");
            body.Append(string.IsNullOrEmpty(message)
                ? "GetHub can show this repository's GitHub Projects boards inside this window. " +
                  "Sign in with an access token that includes the read:project scope."
                : message);
            body.Append("</p><a class=\"gh-btn\" href=\"og://signin\">Sign in to GitHub</a>");
            body.Append("<p class=\"gh-foot\" style=\"margin:20px auto 0;\">");
            body.Append("Project boards are read through GitHub's GraphQL API. ");
            body.Append("The token is stored in your Windows profile and is only sent to api.github.com.");
            body.Append("</p></div>");
            return SiteBuilder.BuildPage(record.GetDisplayName() + " projects", body.ToString(), GitHubPageStyles.Css);
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
            return SiteBuilder.BuildPage(record.GetDisplayName() + " projects", body.ToString(), GitHubPageStyles.Css);
        }

        public static string BuildLocalPage(RepoRecord record)
        {
            StringBuilder body = new StringBuilder();
            body.Append("<div class=\"gh-empty\"><div class=\"mark\">&#9776;</div>");
            body.Append("<h2>Projects are not available for local folders</h2><p>");
            body.Append("This repository was opened from a folder on this computer. Project boards live on GitHub, ");
            body.Append("so add the repository by its GitHub address to read them here.");
            body.Append("</p></div>");
            return SiteBuilder.BuildPage(record.GetDisplayName() + " projects", body.ToString(), GitHubPageStyles.Css);
        }

        private static void AppendProjectRow(StringBuilder body, ProjectSummary project)
        {
            body.Append("<div class=\"gh-row\"><div class=\"gh-state open\">&#9776;</div><div>");
            body.Append("<a class=\"gh-row-title\" href=\"og://project?number=");
            body.Append(project.Number);
            body.Append("\">");
            body.Append(GitHubPageStyles.E(project.Title));
            body.Append("</a>");
            if (project.Closed)
            {
                body.Append(" <span class=\"gh-label\" style=\"background:#6e7781;color:#ffffff;\">Closed</span>");
            }

            body.Append("</div><div class=\"gh-meta\">");
            body.Append(GitHubPageStyles.E(project.ItemCountLabel));
            if (!string.IsNullOrEmpty(project.Description))
            {
                body.Append(" &middot; ");
                body.Append(GitHubPageStyles.E(project.Description));
            }

            if (project.UpdatedAtUtc != DateTime.MinValue)
            {
                body.Append(" &middot; updated ");
                body.Append(GitHubPageStyles.E(Display.RelativeTime(project.UpdatedAtUtc)));
            }

            body.Append("</div></div>");
        }

        private static void AppendBoard(StringBuilder body, ProjectSummary project)
        {
            List<string> columns = new List<string>();
            Dictionary<string, List<ProjectItem>> grouped =
                new Dictionary<string, List<ProjectItem>>(StringComparer.OrdinalIgnoreCase);

            foreach (ProjectItem item in project.Items)
            {
                string status = string.IsNullOrEmpty(item.Status) ? "No status" : item.Status.Trim();
                List<ProjectItem> list;
                if (!grouped.TryGetValue(status, out list))
                {
                    list = new List<ProjectItem>();
                    grouped[status] = list;
                    columns.Add(status);
                }

                list.Add(item);
            }

            columns.Sort(delegate(string left, string right)
            {
                return StatusRank(left).CompareTo(StatusRank(right));
            });

            body.Append("<table class=\"board\"><tr>");
            foreach (string column in columns)
            {
                body.Append("<td class=\"gh-column\"><div class=\"gh-col-title\">");
                body.Append(GitHubPageStyles.E(column));
                body.Append(" <span class=\"gh-col-count\">");
                body.Append(grouped[column].Count);
                body.Append("</span></div>");
                foreach (ProjectItem item in grouped[column])
                {
                    AppendCard(body, item);
                }

                if (grouped[column].Count == 0)
                {
                    body.Append("<div class=\"gh-column-empty\">No cards</div>");
                }

                body.Append("</td>");
            }

            body.Append("</tr></table>");
        }

        private static void AppendCard(StringBuilder body, ProjectItem item)
        {
            body.Append("<div class=\"gh-card\">");
            body.Append("<span class=\"gh-badge ");
            body.Append(item.IsIssue ? "issue" : (item.IsPullRequest ? "pr" : "draft"));
            body.Append("\">");
            body.Append(GitHubPageStyles.E(item.TypeLabel));
            body.Append("</span><div style=\"margin-top:6px;\">");

            if (item.IsIssue && item.Number > 0)
            {
                body.Append("<a href=\"og://issue?number=");
                body.Append(item.Number);
                body.Append("\">#");
                body.Append(item.Number);
                body.Append(" ");
                body.Append(GitHubPageStyles.E(item.Title));
                body.Append("</a>");
            }
            else if (!string.IsNullOrEmpty(item.Url))
            {
                body.Append(GitHubPageStyles.OpenInGitHub(item.Url, item.Title));
            }
            else
            {
                body.Append(GitHubPageStyles.E(item.Title));
            }

            body.Append("</div>");
            if (!string.IsNullOrEmpty(item.State))
            {
                body.Append("<div class=\"meta\">");
                body.Append(GitHubPageStyles.E(item.State));
                body.Append("</div>");
            }

            body.Append("</div>");
        }

        private static int StatusRank(string status)
        {
            if (string.Equals(status, "Todo", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "To do", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(status, "In Progress", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(status, "No status", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            return 2;
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

            body.Append(signedIn ? " Choose Refresh to update." : " Sign in to update this list.");
            body.Append("</div>");
        }
    }
}
