using System;

namespace GetHub.Models
{
    public class ContributorInfo
    {
        public string Login { get; set; }
        public string HtmlUrl { get; set; }
        public int Contributions { get; set; }

        public ContributorInfo()
        {
            Login = string.Empty;
            HtmlUrl = string.Empty;
        }
    }
}
