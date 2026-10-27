using System;
using System.Web.Script.Serialization;

namespace GetHub.Models
{
    public class AppSettings
    {
        public string GitHubToken { get; set; }
        public string Theme { get; set; }

        // Identity of the signed-in account. The token above remains the only
        // credential; these fields only describe who it belongs to so the
        // account badge and the offline Issues / Projects headers can be drawn
        // before any network request finishes.
        public string GitHubLogin { get; set; }
        public string GitHubName { get; set; }
        public string GitHubAvatarUrl { get; set; }
        public string GitHubHtmlUrl { get; set; }
        public string GitHubScopes { get; set; }
        public DateTime GitHubVerifiedAtUtc { get; set; }

        public AppSettings()
        {
            GitHubToken = string.Empty;
            Theme = "Dark";
            GitHubLogin = string.Empty;
            GitHubName = string.Empty;
            GitHubAvatarUrl = string.Empty;
            GitHubHtmlUrl = string.Empty;
            GitHubScopes = string.Empty;
        }

        public GitHubAccount GetAccount()
        {
            GitHubAccount account = new GitHubAccount();
            account.Login = GitHubLogin ?? string.Empty;
            account.Name = GitHubName ?? string.Empty;
            account.AvatarUrl = GitHubAvatarUrl ?? string.Empty;
            account.HtmlUrl = GitHubHtmlUrl ?? string.Empty;
            account.Scopes = GitHubScopes ?? string.Empty;
            return account;
        }

        public void SetAccount(GitHubAccount account)
        {
            if (account == null)
            {
                GitHubLogin = string.Empty;
                GitHubName = string.Empty;
                GitHubAvatarUrl = string.Empty;
                GitHubHtmlUrl = string.Empty;
                GitHubScopes = string.Empty;
                GitHubVerifiedAtUtc = DateTime.MinValue;
                return;
            }

            GitHubLogin = account.Login ?? string.Empty;
            GitHubName = account.Name ?? string.Empty;
            GitHubAvatarUrl = account.AvatarUrl ?? string.Empty;
            GitHubHtmlUrl = account.HtmlUrl ?? string.Empty;
            GitHubScopes = account.Scopes ?? string.Empty;
            GitHubVerifiedAtUtc = DateTime.UtcNow;
        }

        public void ClearAccount()
        {
            GitHubToken = string.Empty;
            SetAccount(null);
        }

        // True when a token is present. Older settings files may contain a token
        // without the cached identity, so both cases count as a usable sign-in.
        [ScriptIgnore]
        public bool IsSignedIn
        {
            get { return !string.IsNullOrEmpty(GitHubToken); }
        }
    }
}
