namespace OmniSharp.Options
{
    public class FileOptions
    {
        public string[] ExcludeSearchPatterns { get; set; } = new[] { "**/node_modules/**/*", "**/bin/**/*", "**/obj/**/*" };
    }
}
