using System;

namespace OmniSharp.Options
{
    public class FileOptions
    {
        public string[] SystemExcludeSearchPatterns { get; set; } = new[] { "**/node_modules/**/*", "**/bin/**/*", "**/obj/**/*", "**/.git/**/*" };

        public string[] ExcludeSearchPatterns { get; set; } = Array.Empty<string>();
    }
}
