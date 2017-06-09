using System;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public RoslynExtensionsOptions RoslynExtensionsOptions { get; } = new RoslynExtensionsOptions();

        public FormattingOptions FormattingOptions { get; } = new FormattingOptions();
    }
}
