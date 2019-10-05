using System;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public RoslynExtensionsOptions RoslynExtensionsOptions { get; set; } = new RoslynExtensionsOptions();

        public FormattingOptions FormattingOptions { get; set; } = new FormattingOptions();

        public FileOptions FileOptions { get; set; } = new FileOptions();

        public RenameOptions RenameOptions { get; set; } = new RenameOptions();

        public OmniSharpExtensionsOptions Plugins { get; set; } = new OmniSharpExtensionsOptions();
    }
}
