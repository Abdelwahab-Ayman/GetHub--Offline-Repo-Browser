using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GetHub.Models;

namespace GetHub.Services
{
    // Reads issues (not pull requests) for a repository through the REST API.
    // The list and each detail page are cached separately so the Issues tab can
    // keep showing what was last downloaded when GitHub is unreachable.
    public class GitHubIssueService
    {
        private const int PageSize = 100;
        private const int MaxIssues = 300;
        private const int MaxCommentPages = 2;

        private readonly string _token;

        public GitHubIssueService(string token)
        {
            _token = token == null ? string.Empty : token.Trim();
        }

        public async Task<List<IssueSummary>> FetchIssuesAsync(
            RepoRecord record,
            CancellationToken cancellationToken)
        {
            ValidateRecord(record);
            string baseUrl = BuildRepositoryUrl(record) + "/issues";

            List<IssueSummary> issues = new List<IssueSummary>();
            for (int page = 1; issues.Count < MaxIssues; page++)
            {
                string url = baseUrl +
                    "?state=all&per_page=" + PageSize.ToString(CultureInfo.InvariantCulture) +
                    "&page=" + page.ToString(CultureInfo.InvariantCulture) +
                    "&sort=updated&direction=desc";
                string json = await GitHubApi.GetStringAsync(url, _token, cancellationToken).ConfigureAwait(false);
                List<object> rows = GitHubApi.DeserializeArray(json);
                if (rows.Count == 0)
                {
                    break;
                }

                foreach (object row in rows)
                {
                    Dictionary<string, object> map = GitHubApi.AsMap(row);
                    if (map == null)
                    {
                        continue;
                    }

                    // The issues endpoint also returns pull requests; the Issues
                    // section intentionally lists only real issues.
                    if (map.ContainsKey("pull_request"))
                    {
                        continue;
                    }

                    IssueSummary issue = ParseIssue(map);
                    if (issue.Number > 0)
                    {
                        issues.Add(issue);
                    }
                }

                if (rows.Count < PageSize || issues.Count >= MaxIssues)
                {
                    break;
                }
            }

            return issues;
        }

        public async Task<IssueDetail> FetchIssueAsync(
            RepoRecord record,
            int number,
            CancellationToken cancellationToken)
        {
            ValidateRecord(record);
            if (number <= 0)
            {
                throw new GitHubException("That issue number is not valid.");
            }

            string url = BuildRepositoryUrl(record) + "/issues/" + number.ToString(CultureInfo.InvariantCulture);
            string json = await GitHubApi.GetStringAsync(url, _token, cancellationToken).ConfigureAwait(false);
            Dictionary<string, object> map = GitHubApi.DeserializeObject(json);
            IssueDetail detail = ParseDetail(map);
            detail.Number = number;

            string commentsUrl = url + "/comments?per_page=100";
            int fetched = 0;
            for (int page = 1; page <= MaxCommentPages; page++)
            {
                string commentsJson = await GitHubApi.GetStringAsync(
                    commentsUrl + "&page=" + page.ToString(CultureInfo.InvariantCulture),
                    _token,
                    cancellationToken).ConfigureAwait(false);
                List<object> rows = GitHubApi.DeserializeArray(commentsJson);
                foreach (object row in rows)
                {
                    Dictionary<string, object> commentMap = GitHubApi.AsMap(row);
                    if (commentMap == null)
                    {
                        continue;
                    }

                    IssueComment comment = ParseComment(commentMap);
                    if (comment != null)
                    {
                        detail.Comments.Add(comment);
                        fetched++;
                    }
                }

                if (rows.Count < 100)
                {
                    break;
                }
            }

            int expected = detail.CommentCount;
            detail.CommentsTruncated = expected > fetched;
            return detail;
        }

        public static IssueSummary ParseIssue(Dictionary<string, object> map)
        {
            IssueSummary issue = new IssueSummary();
            if (map == null)
            {
                return issue;
            }

            issue.Number = GitHubApi.GetInt(map, "number");
            issue.Title = Clean(GitHubApi.GetString(map, "title"));
            issue.State = Clean(GitHubApi.GetString(map, "state"));
            issue.StateReason = Clean(GitHubApi.GetString(map, "state_reason"));
            issue.CommentCount = GitHubApi.GetInt(map, "comments");
            issue.CreatedAtUtc = GitHubApi.GetDate(map, "created_at");
            issue.UpdatedAtUtc = GitHubApi.GetDate(map, "updated_at");
            issue.ClosedAtUtc = GitHubApi.GetDate(map, "closed_at");
            issue.HtmlUrl = Clean(GitHubApi.GetString(map, "html_url"));

            Dictionary<string, object> user = GitHubApi.GetMap(map, "user");
            if (user != null)
            {
                issue.AuthorLogin = Clean(GitHubApi.GetString(user, "login"));
                issue.AuthorAvatarUrl = Clean(GitHubApi.GetString(user, "avatar_url"));
            }

            foreach (object labelValue in GitHubApi.GetList(map, "labels"))
            {
                Dictionary<string, object> labelMap = GitHubApi.AsMap(labelValue);
                if (labelMap == null)
                {
                    continue;
                }

                IssueLabel label = new IssueLabel();
                label.Name = Clean(GitHubApi.GetString(labelMap, "name"));
                label.Color = Clean(GitHubApi.GetString(labelMap, "color")).TrimStart('#');
                if (label.Name.Length > 0)
                {
                    issue.Labels.Add(label);
                }
            }

            return issue;
        }

        public static IssueDetail ParseDetail(Dictionary<string, object> map)
        {
            IssueDetail detail = new IssueDetail();
            IssueSummary summary = ParseIssue(map);
            detail.Number = summary.Number;
            detail.Title = summary.Title;
            detail.State = summary.State;
            detail.StateReason = summary.StateReason;
            detail.AuthorLogin = summary.AuthorLogin;
            detail.AuthorAvatarUrl = summary.AuthorAvatarUrl;
            detail.CreatedAtUtc = summary.CreatedAtUtc;
            detail.UpdatedAtUtc = summary.UpdatedAtUtc;
            detail.ClosedAtUtc = summary.ClosedAtUtc;
            detail.CommentCount = summary.CommentCount;
            detail.HtmlUrl = summary.HtmlUrl;
            detail.Labels = summary.Labels;

            detail.Body = GitHubApi.GetString(map, "body");
            return detail;
        }

        public static IssueComment ParseComment(Dictionary<string, object> map)
        {
            if (map == null)
            {
                return null;
            }

            IssueComment comment = new IssueComment();
            comment.Body = GitHubApi.GetString(map, "body");
            comment.CreatedAtUtc = GitHubApi.GetDate(map, "created_at");
            comment.AuthorAssociation = Clean(GitHubApi.GetString(map, "author_association"));
            Dictionary<string, object> user = GitHubApi.GetMap(map, "user");
            if (user != null)
            {
                comment.AuthorLogin = Clean(GitHubApi.GetString(user, "login"));
                comment.AuthorAvatarUrl = Clean(GitHubApi.GetString(user, "avatar_url"));
            }

            return comment;
        }

        private static string BuildRepositoryUrl(RepoRecord record)
        {
            return GitHubApi.ApiRoot + "/repos/" +
                Uri.EscapeDataString(record.Owner) + "/" +
                Uri.EscapeDataString(record.Name);
        }

        private static void ValidateRecord(RepoRecord record)
        {
            if (record == null ||
                string.IsNullOrEmpty(record.Owner) ||
                string.IsNullOrEmpty(record.Name) ||
                string.Equals(record.Owner, "local", StringComparison.OrdinalIgnoreCase))
            {
                throw new GitHubException("Issues are only available for repositories hosted on GitHub.");
            }
        }

        private static string Clean(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}
