namespace GetHub.Services
{
    public class RepoReference
    {
        public string Owner { get; set; }
        public string Name { get; set; }
        public string Branch { get; set; }

        public RepoReference()
        {
            Owner = string.Empty;
            Name = string.Empty;
            Branch = string.Empty;
        }

        public string FullName
        {
            get { return Owner + "/" + Name; }
        }
    }
}
