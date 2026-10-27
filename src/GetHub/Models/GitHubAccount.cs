using System;
using System.Web.Script.Serialization;

namespace GetHub.Models
{
    // The signed-in GitHub account. Only identity details are kept; the access
    // token itself stays in AppSettings.GitHubToken.
    public class GitHubAccount
    {
        public string Login { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
        public string HtmlUrl { get; set; }
        public string Scopes { get; set; }

        public GitHubAccount()
        {
            Login = string.Empty;
            Name = string.Empty;
            AvatarUrl = string.Empty;
            HtmlUrl = string.Empty;
            Scopes = string.Empty;
        }

        [ScriptIgnore]
        public bool IsAuthenticated
        {
            get { return !string.IsNullOrEmpty(Login); }
        }

        [ScriptIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Name))
                {
                    return Name;
                }

                return string.IsNullOrEmpty(Login) ? string.Empty : "@" + Login;
            }
        }

        [ScriptIgnore]
        public string Initials
        {
            get
            {
                string source = string.IsNullOrEmpty(Name) ? Login : Name;
                if (string.IsNullOrEmpty(source))
                {
                    return "?";
                }

                string[] parts = source.Split(
                    new char[] { ' ', '-', '_', '.' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
                }

                return source.Length == 1
                    ? source.ToUpperInvariant()
                    : source.Substring(0, 2).ToUpperInvariant();
            }
        }

        // Fine-grained tokens do not report classic scopes, so an empty list is
        // treated as "unknown" rather than "no permissions".
        public bool HasScope(string scope)
        {
            if (string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(Scopes))
            {
                return true;
            }

            string[] values = Scopes.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string value in values)
            {
                if (string.Equals(value, scope, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
