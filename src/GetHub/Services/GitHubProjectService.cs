using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using GetHub.Models;

namespace GetHub.Services
{
    // Reads GitHub Projects (v2) boards through the GraphQL API. Classic project
    // boards were retired by GitHub, so projectsV2 is the only representation
    // available to a desktop client.
    public class GitHubProjectService
    {
        private const int MaxProjects = 20;
        private const int MaxItems = 100;

        private readonly string _token;

        // One string so the nesting is easy to audit:
        // repository -> projectsV2 -> nodes -> items -> nodes -> fieldValues.
        private static readonly string BalancedQuery =
            "query GetHubProjects($owner:String!,$name:String!){" +
            "repository(owner:$owner,name:$name){" +
            "projectsV2(first:" + MaxProjects + ",orderBy:{field:UPDATED_AT,direction:DESC}){" +
            "totalCount " +
            "nodes{" +
            "number title shortDescription url closed public updatedAt " +
            "items(first:" + MaxItems + "){" +
            "totalCount " +
            "nodes{" +
            "content{" +
            "__typename " +
            "... on Issue{number title url state} " +
            "... on PullRequest{number title url state} " +
            "... on DraftIssue{title}" +
            "} " +
            "fieldValues(first:50){" +
            "nodes{" +
            "__typename " +
            "... on ProjectV2ItemFieldSingleSelectValue{name field{... on ProjectV2SingleSelectField{name}}} " +
            "}" +
            "}" +
            "}" +
            "}" +
            "}" +
            "}" +
            "}" +
            "}";

        public GitHubProjectService(string token)
        {
            _token = token == null ? string.Empty : token.Trim();
        }

        public async Task<List<ProjectSummary>> FetchProjectsAsync(
            RepoRecord record,
            CancellationToken cancellationToken)
        {
            if (record == null ||
                string.IsNullOrEmpty(record.Owner) ||
                string.IsNullOrEmpty(record.Name) ||
                string.Equals(record.Owner, "local", StringComparison.OrdinalIgnoreCase))
            {
                throw new GitHubException("Projects are only available for repositories hosted on GitHub.");
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> variables = new Dictionary<string, object>();
            variables["owner"] = record.Owner;
            variables["name"] = record.Name;
            Dictionary<string, object> payload = new Dictionary<string, object>();
            payload["query"] = BalancedQuery;
            payload["variables"] = variables;

            string json = await GitHubApi.PostJsonAsync(
                GitHubApi.GraphQlUrl,
                _token,
                serializer.Serialize(payload),
                cancellationToken).ConfigureAwait(false);

            return ParseProjects(json);
        }

        public static List<ProjectSummary> ParseProjects(string json)
        {
            List<ProjectSummary> projects = new List<ProjectSummary>();
            Dictionary<string, object> root = GitHubApi.DeserializeObject(json);
            ThrowGraphQlErrors(root);

            Dictionary<string, object> data = GitHubApi.GetMap(root, "data");
            Dictionary<string, object> repository = GitHubApi.GetMap(data, "repository");
            if (repository == null)
            {
                throw new GitHubException(
                    "GitHub did not return this repository. The token may not have access to it.");
            }

            Dictionary<string, object> connection = GitHubApi.GetMap(repository, "projectsV2");
            if (connection == null)
            {
                return projects;
            }

            foreach (object nodeValue in GitHubApi.GetList(connection, "nodes"))
            {
                Dictionary<string, object> node = GitHubApi.AsMap(nodeValue);
                if (node == null)
                {
                    continue;
                }

                ProjectSummary project = new ProjectSummary();
                project.Number = GitHubApi.GetInt(node, "number");
                project.Title = GitHubApi.GetString(node, "title");
                project.Description = GitHubApi.GetString(node, "shortDescription");
                project.Url = GitHubApi.GetString(node, "url");
                project.Closed = GitHubApi.GetBool(node, "closed");
                project.UpdatedAtUtc = GitHubApi.GetDate(node, "updatedAt");

                Dictionary<string, object> items = GitHubApi.GetMap(node, "items");
                if (items != null)
                {
                    project.TotalItems = GitHubApi.GetInt(items, "totalCount");
                    int parsed = 0;
                    foreach (object itemValue in GitHubApi.GetList(items, "nodes"))
                    {
                        ProjectItem item = ParseItem(GitHubApi.AsMap(itemValue));
                        if (item != null)
                        {
                            project.Items.Add(item);
                            parsed++;
                        }
                    }

                    if (project.TotalItems < parsed)
                    {
                        project.TotalItems = parsed;
                    }
                }

                projects.Add(project);
            }

            return projects;
        }

        private static ProjectItem ParseItem(Dictionary<string, object> map)
        {
            if (map == null)
            {
                return null;
            }

            Dictionary<string, object> content = GitHubApi.GetMap(map, "content");
            if (content == null)
            {
                return null;
            }

            ProjectItem item = new ProjectItem();
            item.Type = GitHubApi.GetString(content, "__typename");
            item.Number = GitHubApi.GetInt(content, "number");
            item.Title = GitHubApi.GetString(content, "title");
            item.State = GitHubApi.GetString(content, "state");
            item.Url = GitHubApi.GetString(content, "url");

            Dictionary<string, object> fields = GitHubApi.GetMap(map, "fieldValues");
            if (fields != null)
            {
                item.Status = ReadStatus(fields);
            }

            if (string.IsNullOrEmpty(item.Title))
            {
                return null;
            }

            return item;
        }

        private static string ReadStatus(Dictionary<string, object> fields)
        {
            string fallback = string.Empty;
            foreach (object valueNode in GitHubApi.GetList(fields, "nodes"))
            {
                Dictionary<string, object> value = GitHubApi.AsMap(valueNode);
                if (value == null)
                {
                    continue;
                }

                string type = GitHubApi.GetString(value, "__typename");
                if (!string.Equals(type, "ProjectV2ItemFieldSingleSelectValue", StringComparison.Ordinal))
                {
                    continue;
                }

                string name = GitHubApi.GetString(value, "name");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                Dictionary<string, object> field = GitHubApi.GetMap(value, "field");
                string fieldName = field == null ? string.Empty : GitHubApi.GetString(field, "name");
                if (string.Equals(fieldName, "Status", StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }

                if (fallback.Length == 0)
                {
                    fallback = name;
                }
            }

            return fallback;
        }

        private static void ThrowGraphQlErrors(Dictionary<string, object> root)
        {
            List<object> errors = GitHubApi.GetList(root, "errors");
            if (errors.Count == 0)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            foreach (object errorValue in errors)
            {
                Dictionary<string, object> error = GitHubApi.AsMap(errorValue);
                if (error == null)
                {
                    continue;
                }

                string message = GitHubApi.GetString(error, "message");
                if (string.IsNullOrEmpty(message))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" ");
                }

                builder.Append(message.TrimEnd('.'));
            }

            string text = builder.ToString();
            if (text.IndexOf("INSUFFICIENT_SCOPES", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("does not have permission", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Resource not accessible", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new GitHubException(
                    "This token cannot read Projects. Sign in again with a token that includes the " +
                    "read:project scope (and repo for private repositories). GitHub said: " + text);
            }

            if (text.Length > 0)
            {
                throw new GitHubException("GitHub could not load the projects: " + text);
            }
        }
    }
}
