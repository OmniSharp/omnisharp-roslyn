using System;

namespace OmniSharp.Options
{
    public class OmniSharpOptions
    {
        public CodeActionOptions CodeActions { get; } = new CodeActionOptions();

        public FormattingOptions FormattingOptions { get; } = new FormattingOptions();
    }
}
