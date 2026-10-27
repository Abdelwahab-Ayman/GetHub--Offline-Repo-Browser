namespace GetHub.Rendering
{
    public class RepoContext
    {
        public string RepoRoot { get; set; }
        public string CurrentDirectory { get; set; }
        public string CurrentFileRelative { get; set; }
        public string Owner { get; set; }
        public string Name { get; set; }
        public string Branch { get; set; }

        public RepoContext()
        {
            RepoRoot = string.Empty;
            CurrentDirectory = string.Empty;
            CurrentFileRelative = string.Empty;
            Owner = string.Empty;
            Name = string.Empty;
            Branch = string.Empty;
        }
    }
}
