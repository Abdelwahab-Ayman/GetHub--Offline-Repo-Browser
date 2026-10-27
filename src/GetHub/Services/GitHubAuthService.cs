using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GetHub.Models;
using GetHub.Services;

namespace GetHub.Services
{
    // Validates a GitHub access token and turns the /user response into the
    // account shown in Settings and in the Issues / Projects sign-in prompts.
    public class GitHubAuthService
    {
        private readonly string _token;

        public GitHubAuthService(string token)
        {
            _token = token == null ? string.Empty : token.Trim();
        }

        public static string DescribeTokenScopes()
        {
            return "repo, read:project";
        }

        public static string BuildTokenUrl()
        {
            return "https://github.com/settings/tokens/new?scopes=repo,read:project&description=GetHub";
        }

        public async Task<GitHubAccount> SignInAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_token))
            {
                throw new GitHubException("Enter a GitHub access token.");
            }

            using (System.Net.Http.HttpClient client = GitHubApi.CreateClient())
            using (System.Net.Http.HttpRequestMessage request =
                GitHubApi.CreateRequest(GitHubApi.ApiRoot + "/user", _token, "application/vnd.github+json"))
            using (System.Net.Http.HttpResponseMessage response =
                await GitHubApi.SendAsync(client, request, cancellationToken).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                GitHubApi.ThrowIfFailed(response, body);

                Dictionary<string, object> map = GitHubApi.DeserializeObject(body);
                GitHubAccount account = new GitHubAccount();
                account.Login = GitHubApi.GetString(map, "login");
                account.Name = GitHubApi.GetString(map, "name");
                account.AvatarUrl = GitHubApi.GetString(map, "avatar_url");
                account.HtmlUrl = GitHubApi.GetString(map, "html_url");
                account.Scopes = GitHubApi.ReadHeader(response, "X-OAuth-Scopes");

                if (string.IsNullOrEmpty(account.Login))
                {
                    throw new GitHubException("GitHub answered, but the account details were empty.");
                }

                return account;
            }
        }

        // Returns true when the token can read issues in this repository. A token
        // without a classic scope list (fine-grained tokens) is given the benefit
        // of the doubt and only the actual request can prove otherwise.
        public static bool CanReadIssues(GitHubAccount account)
        {
            return account == null || account.HasScope("repo") || string.IsNullOrEmpty(account.Scopes);
        }

        public static bool CanReadProjects(GitHubAccount account)
        {
            return account == null ||
                account.HasScope("read:project") ||
                account.HasScope("project") ||
                string.IsNullOrEmpty(account.Scopes);
        }
    }
}
