using System;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public RoslynExtensionsOptions RoslynExtensionsOptions { get; } = new RoslynExtensionsOptions();

        public FormattingOptions FormattingOptions { get; } = new FormattingOptions();

        public FileOptions FileOptions { get; } = new FileOptions();
    }

    public class FileOptions
    {
        public string[] ExcludeSearchPatterns { get; set; } = new[] { "**/node_modules/**/*", "**/bin/**/*", "**/obj/**/*" };

    }
}
