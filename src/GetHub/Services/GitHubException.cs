using System;

namespace GetHub.Services
{
    public class GitHubException : Exception
    {
        public GitHubException(string message)
            : base(message)
        {
        }

        public GitHubException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
