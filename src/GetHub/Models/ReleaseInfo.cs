using System;

namespace GetHub.Models
{
    public class ReleaseInfo
    {
        public string TagName { get; set; }
        public string Name { get; set; }
        public string HtmlUrl { get; set; }
        public DateTime PublishedAtUtc { get; set; }
        public bool IsPrerelease { get; set; }

        public ReleaseInfo()
        {
            TagName = string.Empty;
            Name = string.Empty;
            HtmlUrl = string.Empty;
        }

        public string GetTitle()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                return Name;
            }

            return TagName;
        }
    }
}
