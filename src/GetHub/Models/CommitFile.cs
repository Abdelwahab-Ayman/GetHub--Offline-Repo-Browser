using System;
using System.Web.Script.Serialization;

namespace GetHub.Models
{
    // One row in the Files tab. Status uses the GitHub vocabulary
    // (added, modified, removed, renamed, copied) so cached details and the
    // local git reader produce the same UI.
    public class CommitFile
    {
        public string Path { get; set; }
        public string PreviousPath { get; set; }
        public string Status { get; set; }
        public string Patch { get; set; }
        public int Additions { get; set; }
        public int Deletions { get; set; }
        public int Changes { get; set; }
        public bool IsBinary { get; set; }
        public bool Truncated { get; set; }

        public CommitFile()
        {
            Path = string.Empty;
            PreviousPath = string.Empty;
            Status = "modified";
            Patch = string.Empty;
        }

        [ScriptIgnore]
        public string StatusLetter
        {
            get
            {
                switch ((Status == null ? string.Empty : Status).ToLowerInvariant())
                {
                    case "added":
                        return "A";
                    case "removed":
                    case "deleted":
                        return "D";
                    case "renamed":
                        return "R";
                    case "copied":
                        return "C";
                    default:
                        return "M";
                }
            }
        }

        [ScriptIgnore]
        public string DisplayPath
        {
            get
            {
                if (string.IsNullOrEmpty(PreviousPath) ||
                    string.Equals(PreviousPath, Path, StringComparison.Ordinal))
                {
                    return Path == null ? string.Empty : Path;
                }

                return PreviousPath + " \u2192 " + Path;
            }
        }

        [ScriptIgnore]
        public bool HasPatch
        {
            get { return !string.IsNullOrEmpty(Patch); }
        }
    }
}
