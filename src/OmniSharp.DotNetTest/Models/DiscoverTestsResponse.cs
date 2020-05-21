namespace OmniSharp.DotNetTest.Models
{
    public class Test
    {
        public string FullyQualifiedName { get; set; }

        public string DisplayName { get; set; }

        public string Source { get; set; }
        
        public string CodeFilePath { get; set; }

        public int LineNumber { get; set; }
    }

    public class DiscoverTestsResponse
    {
        public Test[] Tests { get; set; }
    }
}